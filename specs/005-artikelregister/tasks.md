# Tasks: Artikelregister (005)

**Tests**: ingår (constitution III) + Testcontainers (portfolio-ambition).

## Phase 1: US1 — Registret (P1)
- [ ] T001 [Domain] `Article` (validering: namn, pris ≥ 0, momssats) + arkivering + domäntester
- [ ] T002 [Infra] `ArticleDocument` + `MongoArticleRepository` (TenantScopedRepository) + index (unikt sparse `{tenantId, sku}`) + DI
- [ ] T003 [Api] `ArticleService` + endpoints (CRUD + archive, `sku_taken` 409) — integrationstester: CRUD, SKU-kollision, isolering, arkivfilter

## Phase 2: US2+US3 — Rad från artikel + enhet (P1/P2)
- [ ] T004 [Domain] `InvoiceLine.Unit` (valfri) + dokument/DTO:er (bakåtkompatibelt) + PDF ("10 tim")
- [ ] T005 Tester: enhet flödar rad→DTO→PDF; rad utan enhet som förut

## Phase 3: Testcontainers (portfolio)
- [ ] T006 `Testcontainers.MongoDb` + SkippableFact: riktig Mongo — SKU-index (unikt/sparse/per tenant), tenant-filter på riktiga queries, `MongoInvoiceNumberSequence` parallellism

## Phase 4: Frontend (kreativ redesign + artikel-UI)
- [ ] T007 Ledger/kvitto-tema via tokens + ui-komponenter + Nav (slår igenom på alla sidor)
- [ ] T008 Artiklar-sida (register, arkivering) + artikelväljare i utkast-editorn (förifyller rad) + enhetsfält
- [ ] T009 Vitest fortsatt grönt; build + lint

## Phase 5: PR
- [ ] T010 Docs uppdaterade; PR mot `develop` när allt grönt
