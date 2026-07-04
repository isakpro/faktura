# Tasks: E-postutskick av faktura (003)

**Input**: Design från `specs/003-faktura-epost/` (plan, spec, data-model, contracts)
**Tests**: Ingår — constitution III. Bygger på 001 (isolering/auth) + 002 (fakturans PDF).

Format: `[ID] [P?] [Story] Beskrivning` · **[P]** = parallelliserbart.

## Phase 1: Setup & Foundational

- [x] T001 NuGet **MailKit**; mappar `Infrastructure/Email`, `Domain/Emailing`
- [x] T002 [P] [Domain] `EmailMessage`/`EmailAttachment`; `IEmailSender`; `InvoiceEmail` (sent/failed)
- [x] T003 [P] [Domain] `IInvoiceEmailRepository`; `ITenantContext.Email` (ur JWT `email`-claim)
- [x] T004 [Infra] `MongoInvoiceEmailRepository` (TenantScopedRepository) + document + collection/index
- [x] T005 [Infra] `SmtpEmailSender` (MailKit) + `SmtpOptions`; DI; `HttpTenantContext.Email`

## Phase 2: US1 — Mejla en skickad faktura (P1)

- [x] T007 [P] [US1] Integrationstester (fejkad `IEmailSender`): skickad faktura → mejl m. PDF + logg `sent`; utkast → 409; kund utan e-post → 422; From/Reply-To korrekt (SC-001/002)
- [x] T008 [US1] Api `EmailService.SendAsync` + endpoint `POST /api/invoices/{id}/email` — grön

## Phase 3: US2 + US3 — Skicka om / historik (P2)

- [x] T009 [P] [US2/US3] Integrationstester: överstyrd adress; upprepat utskick loggas separat; cross-tenant → 404; leveransfel → 502 + logg `failed`, fakturan orörd (SC-003/004)
- [x] T010 [US2/US3] Api: överstyrd `recipient`, fel-loggning; endpoint `GET /api/invoices/{id}/emails` — grön

## Phase 4: Frontend + PR

- [x] T011 [P] Frontend: "Mejla"-knapp (valfri adress) + bekräftelse på faktura-sidan
- [x] T012 SMTP i appsettings.example; PR mot `develop`

**Klart:** dotnet test = 85 gröna (46 domän + 39 API); frontend build + oxlint gröna.

## Ordning
Setup/Foundational → US1 (mejla) → US2/US3 (om-skick/historik) → frontend/PR.
Inom story: tester först → domän → infra → endpoints. En PR per spec, först när grönt.
