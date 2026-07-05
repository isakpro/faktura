# Tasks: Artikelregister (005)

**Tests**: ingår (constitution III) + Testcontainers (portfolio-ambition).

## Phase 1: US1 — Registret (P1)
- [x] T001 [Domain] `Article` (validering: namn, pris ≥ 0, momssats) + arkivering + domäntester
- [x] T002 [Infra] `ArticleDocument` + `MongoArticleRepository` (TenantScopedRepository) + index (unikt sparse `{tenantId, sku}`) + DI
- [x] T003 [Api] `ArticleService` + endpoints (CRUD + archive, `sku_taken` 409) — integrationstester: CRUD, SKU-kollision, isolering, arkivfilter

## Phase 2: US2+US3 — Rad från artikel + enhet (P1/P2)
- [x] T004 [Domain] `InvoiceLine.Unit` (valfri) + dokument/DTO:er (bakåtkompatibelt) + PDF ("10 tim")
- [x] T005 Tester: enhet flödar rad→DTO→PDF; rad utan enhet som förut

## Phase 3: Testcontainers (portfolio)
- [x] T006 `Testcontainers.MongoDb` + SkippableFact: riktig Mongo — SKU-index (unikt/sparse/per tenant), tenant-filter på riktiga queries, `MongoInvoiceNumberSequence` parallellism

## Phase 4: Frontend (kreativ redesign + artikel-UI)
- [x] T007 Ledger/kvitto-tema via tokens + ui-komponenter + Nav (slår igenom på alla sidor)
- [x] T008 Artiklar-sida (register, arkivering) + artikelväljare i utkast-editorn (förifyller rad) + enhetsfält
- [x] T009 Vitest fortsatt grönt; build + lint

## Phase 5: PR
- [x] T010 Docs uppdaterade; PR mot `develop` när allt grönt

**Klart:** dotnet test = 124 gröna + 3 Testcontainers (skippas utan Docker, körs i CI);
frontend 7 vitest + build + oxlint gröna. Ledger-tema levererat (tokens/ui/Nav/Badge).
