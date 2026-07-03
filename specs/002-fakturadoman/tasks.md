# Tasks: Fakturadomänen (002)

**Input**: Design från `specs/002-fakturadoman/` (plan, spec, research, data-model, contracts)
**Tests**: Ingår — constitution III (TDD för moms/avrundning/nummerserie/kredit/lås).
**Organisation**: grupperat per user story (US1–US6). Bygger på 001 (återanvänder
`TenantScopedRepository`, JWT/RBAC, Mongo, `problem+json`).

Format: `[ID] [P?] [Story] Beskrivning` · **[P]** = parallelliserbart (olika filer).

---

## Phase 1: Setup

- [ ] T001 Lägg NuGet **QuestPDF** i `Faktura.Infrastructure`; nya mappar `Domain/Customers`,
  `Domain/Invoicing`, `Infrastructure/Pdf`, `Api/Features/Customers`, `Api/Features/Invoicing`
- [ ] T002 [P] Registrera `Decimal128`-serialisering/BSON-konventioner för belopp

## Phase 2: Foundational (blockerar user stories)

- [ ] T003 [P] [Domain] `Money`-värdetyp (decimal, öresavrundning away-from-zero) + tester
- [ ] T004 [P] [Domain] Enums `VatRate` (25/12/6/0), `InvoiceStatus` (Draft/Sent/Paid/Credited), `InvoiceType` (Invoice/CreditNote)
- [ ] T005 [Domain] `InvoiceCalculator` (rad-netto/moms, summa netto, moms per sats, brutto) — **TDD-kärna**
- [ ] T006 [P] [Domain] Abstraktioner: `ICustomerRepository`, `IInvoiceRepository`, `IInvoiceNumberSequence`, `IInvoicePdfGenerator`, `IClock` (finns)
- [ ] T007 [Infra] Mongo-collections + index (customers, invoices, invoiceCounters) i `MongoContext`

## Phase 3: US1 — Kunder (P1)

- [ ] T008 [P] [US1] Domäntester: `Customer` (namn obligatoriskt, arkivering)
- [ ] T009 [P] [US1] Integrationstester: CRUD + arkiv, cross-tenant (A ser ej B)
- [ ] T010 [US1] Domän `Customer` + Infra `MongoCustomerRepository` (TenantScopedRepository)
- [ ] T011 [US1] Api `CustomerService` + endpoints (`/api/customers`) — grön T009

**Checkpoint**: kunder fungerar, tenant-isolerat.

## Phase 4: US2 — Fakturautkast + moms (P1)

- [ ] T012 [P] [US2] Domäntester: beräkning per sats + blandat + öresavrundning (SC-001)
- [ ] T013 [P] [US2] Integrationstester: skapa/ändra utkast, summor i svar
- [ ] T014 [US2] Domän `Invoice`/`InvoiceLine` (utkast: lägg/ändra/ta bort rad, räkna om via `InvoiceCalculator`)
- [ ] T015 [US2] Infra `MongoInvoiceRepository`; Api `InvoiceService` + endpoints (skapa/hämta/ändra utkast) — grön T013

**Checkpoint**: utkast med korrekt moms.

## Phase 5: US3 — Skicka: nummer + låsning (P1) 🎯 MVP-mål

- [ ] T016 [P] [US3] Domäntester: skick sätter status/datum; mutation efter skick nekas (`invoice_locked`)
- [ ] T017 [P] [US3] Integrationstester: skick ger nummer + förfallodatum; skickad ej ändringsbar; **concurrency** (parallella skick → unika obrutna nummer, SC-002)
- [ ] T018 [US3] Infra `MongoInvoiceNumberSequence` (`FindOneAndUpdate $inc`, upsert, atomiskt)
- [ ] T019 [US3] Domän/tjänst: `Send()` (nummer, faktura-/förfallodatum, lås); endpoint `/send` — grön T016/T017

**Checkpoint ✅ (MVP)**: kund → utkast+moms → skicka med obruten serie + oföränderlig. Validera grönt.

## Phase 6: US4 — Betalstatus (P2)

- [ ] T020 [P] [US4] Domäntester: markera betald; härledd förfallostatus
- [ ] T021 [P] [US4] Integrationstester: mark-paid; list-filter `overdue`
- [ ] T022 [US4] Domän/tjänst + endpoint `/mark-paid` + list-filter — grön

## Phase 7: US5 — Kreditfaktura (P2)

- [ ] T023 [P] [US5] Domäntester: kredit refererar original, negativa belopp, kredittak (SC-005)
- [ ] T024 [P] [US5] Integrationstester: `/credit` ger eget nummer; överkreditering → 409
- [ ] T025 [US5] Domän/tjänst + endpoint `/credit` — grön

## Phase 8: US6 — PDF + frontend + PR

- [ ] T026 [P] [US6] Infra `QuestPdfInvoiceGenerator` (obligatoriska fält); endpoint `/pdf` (endast skickad)
- [ ] T027 [P] [US6] Test: skickad faktura → icke-tom PDF; utkast → nekas
- [ ] T028 [P] Frontend: sidor Kunder (lista/formulär) + Fakturor (lista/utkast-editor/detalj + skicka/betala/kreditera/PDF)
- [ ] T029 Lokal code/security review; `quickstart`-röktest; öppna PR mot `develop` när grönt

## Dependencies & ordning
Setup → Foundational → US1 → US2 → US3 (**MVP-stopp & validera**) → US4 → US5 → US6/frontend.
Inom story: tester först (ska faila) → domän → infra → endpoints. En PR per spec, först när grönt.
