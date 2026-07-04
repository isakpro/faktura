# Tasks: Fakturadomänen (002)

**Input**: Design från `specs/002-fakturadoman/` (plan, spec, research, data-model, contracts)
**Tests**: Ingår — constitution III (TDD för moms/avrundning/nummerserie/kredit/lås).
**Organisation**: grupperat per user story (US1–US6). Bygger på 001 (återanvänder
`TenantScopedRepository`, JWT/RBAC, Mongo, `problem+json`).

Format: `[ID] [P?] [Story] Beskrivning` · **[P]** = parallelliserbart (olika filer).

---

## Phase 1: Setup

- [~] T001 Nya mappar Domain/Customers, Domain/Invoicing, Api/Features/{Customers,Invoicing}. (QuestPDF = US6)
- [x] T002 [P] `Decimal128`-representation på beloppsfält i dokumenten

## Phase 2: Foundational

- [x] T003 [P] [Domain] `Money`-värdetyp (decimal, öresavrundning away-from-zero)
- [x] T004 [P] [Domain] Enums `VatRate` (25/12/6/0), `InvoiceStatus`, `InvoiceType`
- [x] T005 [Domain] `InvoiceCalculator` (rad-netto/moms, summa netto, moms per sats, brutto) — TDD, grön
- [x] T006 [P] [Domain] Abstraktioner: `ICustomerRepository`, `IInvoiceRepository`, `IInvoiceNumberSequence`
- [x] T007 [Infra] Mongo-collections + index (customers, invoices, invoiceCounters) i `MongoContext`

## Phase 3: US1 — Kunder (P1)

- [x] T008 [P] [US1] Domän `Customer` (namn obligatoriskt, arkivering)
- [x] T009 [P] [US1] Integrationstest: CRUD + cross-tenant (A ser ej B)
- [x] T010 [US1] Infra `MongoCustomerRepository` (TenantScopedRepository)
- [x] T011 [US1] Api `CustomerService` + endpoints (`/api/customers`) — grön

## Phase 4: US2 — Fakturautkast + moms (P1)

- [x] T012 [P] [US2] Domäntester: beräkning per sats + blandat + öresavrundning (SC-001)
- [x] T013 [P] [US2] Integrationstest: skapa/ändra utkast, summor i svar
- [x] T014 [US2] Domän `Invoice`/`InvoiceLine` (utkast: räkna om via `InvoiceCalculator`)
- [x] T015 [US2] Infra `MongoInvoiceRepository`; Api `InvoiceService` + endpoints — grön

## Phase 5: US3 — Skicka: nummer + låsning (P1) 🎯 MVP

- [x] T016 [P] [US3] Domäntester: skick sätter status/datum; mutation efter skick nekas (`invoice_locked`)
- [x] T017 [P] [US3] Integrationstest: skick ger nummer + förfallodatum; skickad ej ändringsbar; **concurrency** (20 parallella skick → unika obrutna nummer, SC-002)
- [x] T018 [US3] Infra `MongoInvoiceNumberSequence` (`FindOneAndUpdate $inc`, atomiskt)
- [x] T019 [US3] Domän/tjänst: `Send()` + endpoint `/send`; `/mark-paid` — grön

**Checkpoint ✅ (MVP)**: kund → utkast+moms → skicka med obruten serie + oföränderlig + betalstatus.
`dotnet test` = 74 gröna (44 domän + 30 API). Kvar: US5 kreditfaktura, US6 PDF, frontend.

## Phase 6: US4 — Betalstatus (P2)

- [x] T020 [P] [US4] Domäntester: markera betald; härledd förfallostatus
- [x] T021 [P] [US4] Integrationstest: mark-paid; list-filter `overdue`
- [x] T022 [US4] Domän/tjänst + endpoint `/mark-paid` + list-filter — grön

## Phase 7: US5 — Kreditfaktura (P2)

- [x] T023 [P] [US5] Domäntester: kredit refererar original, negerade belopp, kredittak (SC-005)
- [x] T024 [P] [US5] Integrationstest: `/credit` ger eget nummer; överkreditering → 409
- [x] T025 [US5] Domän (`CreateCreditNote`/`RegisterCredit`) + endpoint `/credit` — grön

## Phase 8: US6 — PDF + frontend + PR

- [x] T026 [P] [US6] Infra `QuestPdfInvoiceGenerator` (Community-licens); endpoint `/pdf` (endast skickad)
- [x] T027 [P] [US6] Test: skickad faktura → `%PDF`; utkast → 409
- [x] T028 [P] Frontend: sidor Kunder + Fakturor (utkast-editor + skicka/betala/kreditera/PDF), delad Nav
- [x] T029 Kod/säkerhetsgenomgång; PR mot `develop`

**Klart:** dotnet test = 78 gröna (46 domän + 32 API); frontend build + oxlint gröna.

## Dependencies & ordning
Setup → Foundational → US1 → US2 → US3 (**MVP-stopp & validera**) → US4 → US5 → US6/frontend.
Inom story: tester först (ska faila) → domän → infra → endpoints. En PR per spec, först när grönt.
