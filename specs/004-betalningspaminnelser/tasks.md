# Tasks: Betalningspåminnelser (004)

**Input**: Design från `specs/004-betalningspaminnelser/` · **Tests**: ingår (constitution III).
Bygger på 002 (förfallostatus/PDF) + 003 (`IEmailSender`).

## Phase 1: Foundational

- [x] T001 [P] [Domain] `InvoiceReminder` (typ/mottagare/sequence/status), `ReminderSettings`, `ReminderRules.CanRemind` + domäntester
- [x] T002 [P] [Domain] `IInvoiceReminderRepository`, `IReminderSettingsRepository` (inkl. `ListAutoEnabledAsync` för jobbet)
- [x] T003 [Infra] Documents + `MongoInvoiceReminderRepository` (TenantScopedRepository) + `MongoReminderSettingsRepository` + collections/index + DI

## Phase 2: US1 — Manuell påminnelse (P1)

- [x] T004 [P] [US1] Integrationstester (fejkad sändare + styrbar klocka): förfallen → mejl m. PDF + logg sequence 1→2; ej förfallen/betald/kreditfaktura → 409; saknad mottagare → 422; override; SMTP-fel → 502 + failed-logg, fakturan orörd; cross-tenant → 404
- [x] T005 [US1] `ReminderMailer` (delad kärna: ämne/kropp/PDF/logg) + `ReminderService` + endpoint `POST /invoices/{id}/remind` + `GET /invoices/{id}/reminders` — grön

## Phase 3: US2 — Automatiskt jobb (P2)

- [x] T006 [P] [US2] Jobbtester (kör `ReminderJob` direkt): auto på + förfallen ≥ X dagar → exakt 1 automatisk påminnelse; omkörning → 0 dubbletter; av → 0; betald → 0; saknad e-post → failed-logg utan att jobbet stannar
- [x] T007 [US2] `ReminderJob` (systemkontext, feltolerant per faktura) + `ReminderBackgroundService` (dagligt intervall, ej registrerad i Testing) — grön

## Phase 4: US3 — Inställningar + frontend + PR

- [x] T008 [P] [US3] Integrationstester: GET/PUT `/reminder-settings` (Member → 403, Owner/Admin → 200, default av/7)
- [x] T009 [US3] Endpoints `GET/PUT /api/reminder-settings` — grön
- [x] T010 [P] Frontend: "Påminn"-knapp på förfallna fakturor + inställningskort (Owner/Admin) på Dashboard
- [x] T011 Kod/säkerhetsgenomgång; PR mot `develop` när grönt

## Ordning
Foundational → US1 → US2 → US3/frontend → PR. Tester först inom varje story.

**Klart:** dotnet test = 111 gröna (58 domän + 53 API); frontend build + oxlint gröna.
