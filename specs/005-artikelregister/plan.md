# Implementation Plan: Artikelregister

**Branch**: `feature/005-artikelregister` | **Date**: 2026-07-05 | **Spec**: [spec.md](spec.md)

## Summary

Artikelregister (CRUD + arkivering, valfritt unikt artikelnummer, enhet, pris, momssats) och
artikel-förifyllda fakturarader enligt snapshot-principen, plus valfri **enhet** på fakturarad
och PDF (bakåtkompatibelt). Höjd portfolio-ambition i samma PR: **Testcontainers-tester mot
riktig MongoDB** (index/unikhet/atomicitet) och en **kreativ omdesign** av frontenden
(token-drivet "ledger/kvitto"-tema) tillsammans med artikel-UI:t.

## Technical Context

**Stack**: som 001–004. Nya beroenden: `Testcontainers.MongoDb` + `Xunit.SkippableFact`
(endast testprojekt).
**Storage**: ny collection `articles` (tenant-ägd; unikt **sparse** index `{tenantId, sku}`).
`invoices.lines` får valfritt `unit`-fält (BsonIgnoreIfNull — gamla dokument opåverkade).
**Testing**: domän + in-memory-integration som förut, **plus** Testcontainers-klass mot riktig
Mongo (hoppas över med SkippableFact när Docker saknas; körs alltid i CI där Docker finns).

## Key Decisions (Phase 0)

- **Snapshot i klienten:** "skapa rad från artikel" = frontend hämtar artiklar och förifyller
  radfälten; backend tar emot vanliga radvärden (+ `unit`). Ingen levande referens (FR-006/007
  uppfylls per konstruktion — raden ÄR en kopia). Alternativ förkastat: server-side
  "articleId på rad" (kräver referenshantering utan mervärde i v1).
- **SKU-unikhet i datalagret:** unikt sparse compound-index `{tenantId, sku}` + trevligt
  409-fel i tjänsten (`sku_taken`). Sparse ⇒ artiklar utan nummer är obegränsade.
- **Enhet på rad:** `InvoiceLine.Unit` (valfri sträng) → dokument (`BsonIgnoreIfNull`), DTO:er
  och PDF ("10 tim"). Berör inte beräkningen.
- **Testcontainers:** riktig Mongo verifierar det fakes inte kan: unika/sparse index,
  `TenantScopedRepository`-filter på riktiga queries, `MongoInvoiceNumberSequence`-atomicitet
  under parallellism. Skippas snyggt lokalt utan Docker (SkippableFact), obligatoriskt i CI.
- **Kreativ redesign (användardirektiv):** eget "ledger/kvitto"-tema via design-tokens —
  papper/bläck-palett med en stark accent, serif-display för rubriker, tabular-nums för belopp,
  kvitto-linjer och stämpel-lika statusbadges. Allt token-drivet (constitution) — sidorna
  använder redan tokens, så paletten/typografin slår igenom överallt; Nav och ui-komponenter
  poleras, Artiklar-sidan blir skyltfönstret.

## Constitution Check

I spec-driven ✅ · II domänlogik ren (Article-validering, Unit rör ej kalkyl) ✅ · III TDD +
utökad testpyramid (Testcontainers) ✅ · IV kontrakt först ([contracts/rest-api.md](contracts/rest-api.md)) ✅ ·
V isolering (TenantScopedRepository + index; SKU-unikhet per tenant) ✅ · VI samma CI-gate ✅.
**PASS.**

## Project Structure (tillägg)

```text
backend/src/Faktura.Domain/Articles/Article.cs (+ IArticleRepository i Abstractions)
backend/src/Faktura.Domain/Invoicing/InvoiceLine.cs (+ Unit)
backend/src/Faktura.Infrastructure/Persistence/ (ArticleDocument, MongoArticleRepository, index)
backend/src/Faktura.Api/Features/Articles/ (contracts, service, endpoints)
backend/tests/Faktura.Api.Tests/ (ArticleEndpointsTests, MongoRealDatabaseTests [Testcontainers])
frontend/src/theme/tokens.ts (ledger-tema) · components/ui.tsx · pages/Articles.tsx
frontend/src/pages/Invoices.tsx (artikelväljare + enhet på rad)
```

## Complexity Tracking

| Val | Varför | Enklare alternativ förkastat |
|---|---|---|
| Testcontainers + SkippableFact | Bevisar index/atomicitet på riktig Mongo (portfolio-ambition) | Enbart fakes missar just index-/filterbuggar |
| Tema-omdesign i samma PR | Artikel-UI:t är rätta tillfället; token-drivet = liten kodrisk | Separat design-PR ger dubbelarbete i samma filer |
