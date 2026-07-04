# Tasks: E-postutskick av faktura (003)

**Input**: Design från `specs/003-faktura-epost/` (plan, spec, data-model, contracts)
**Tests**: Ingår — constitution III. Bygger på 001 (isolering/auth) + 002 (fakturans PDF).

Format: `[ID] [P?] [Story] Beskrivning` · **[P]** = parallelliserbart.

## Phase 1: Setup & Foundational

- [ ] T001 NuGet **MailKit** i `Faktura.Infrastructure`; mapp `Infrastructure/Email`, `Domain/Emailing`
- [ ] T002 [P] [Domain] `EmailMessage` + `EmailAttachment`; `IEmailSender`; `InvoiceEmail` (logg-entitet, sent/failed)
- [ ] T003 [P] [Domain] `IInvoiceEmailRepository`; utöka `ITenantContext` med `Email` (ur JWT-claim)
- [ ] T004 [Infra] `MongoInvoiceEmailRepository` (TenantScopedRepository) + document + collection/index i `MongoContext`
- [ ] T005 [Infra] `SmtpEmailSender` (MailKit) + `SmtpOptions`; DI-registrering; `HttpTenantContext.Email`

## Phase 2: US1 — Mejla en skickad faktura (P1)

- [ ] T006 [P] [US1] Domäntester: recipient-val (överstyrd → kund → fel), meddelande (ämne m. nummer, PDF-bilaga)
- [ ] T007 [P] [US1] Integrationstester (fejkad `IEmailSender`): skickad faktura mejlas → mejl m. PDF + logg `sent`; utkast → 409; kund utan e-post → 422 `no_recipient`; From/Reply-To korrekt (SC-001/002)
- [ ] T008 [US1] Api `EmailService.SendInvoiceEmailAsync` (ladda faktura, välj mottagare, generera PDF, bygg meddelande, skicka, logga) + endpoint `POST /api/invoices/{id}/email` — grön

## Phase 3: US2 + US3 — Skicka om / historik (P2)

- [ ] T009 [P] [US2/US3] Integrationstester: överstyrd adress; upprepat utskick loggas separat; cross-tenant nekas (404); leveransfel → 502 + logg `failed`, fakturan orörd (SC-003/004)
- [ ] T010 [US2/US3] Api: överstyrd `recipient`, fel-loggning; endpoint `GET /api/invoices/{id}/emails` (historik) — grön

## Phase 4: Frontend + PR

- [ ] T011 [P] Frontend: knapp "Mejla" på skickad faktura (valfri adress) + visa utskicksstatus/historik
- [ ] T012 Kod/säkerhetsgenomgång; `.env.example`/appsettings.example (SMTP); PR mot `develop` när grönt

## Ordning
Setup/Foundational → US1 (mejla) → US2/US3 (om-skick/historik) → frontend/PR.
Inom story: tester först → domän → infra → endpoints. En PR per spec, först när grönt.
