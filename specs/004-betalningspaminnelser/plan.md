# Implementation Plan: Betalningspåminnelser

**Branch**: `feature/004-betalningspaminnelser` | **Date**: 2026-07-04 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `specs/004-betalningspaminnelser/spec.md`

## Summary

Påminnelse-mejl för förfallna fakturor: manuell åtgärd + automatiskt dagligt jobb styrt av en
per-organisationsinställning (opt-in, dagar efter förfall). Återanvänder 002:s förfallostatus/PDF
och 003:s `IEmailSender`. Påminnelser loggas i egen `invoiceReminders`-collection (fakturan förblir
oföränderlig); max en automatisk påminnelse per faktura.

## Technical Context

**Language/Version**: C#/.NET 10 + TS/React 19 — som tidigare
**Primary Dependencies**: inga nya paket — återanvänder MailKit/`IEmailSender` (003), QuestPDF-
generatorn (002), `TenantScopedRepository`/JWT (001)
**Storage**: MongoDB — nya collections `invoiceReminders` (tenant-ägd) och `reminderSettings`
(`_id` = tenantId)
**Testing**: xUnit — domänregel (påminnelseberättigande), integration via `WebApplicationFactory`
+ fejkad `IEmailSender` + **styrbar klocka** (`MutableClock`) för att göra fakturor förfallna;
jobbet testas genom att köra jobblogiken direkt
**Project Type**: Web application, samma solution
**Constraints**: påminnelser muterar aldrig fakturan; jobbet feltolerant per faktura; dubblettskydd

## Key Decisions (Phase 0)

- **Delad utskickskärna `ReminderMailer`** (utan `ITenantContext`) används av både den manuella
  tjänsten och jobbet: bygger mejl (ämne "Påminnelse N: Faktura X…", förfallodatum + belopp,
  original-PDF), skickar, loggar med ordningsnummer. Manuell väg sätter Reply-To = avsändaren;
  jobbet utelämnar Reply-To.
- **Jobb: `ReminderJob` (ren logikklass) + `ReminderBackgroundService`** (in-process
  `BackgroundService` med dagligt intervall). Logiken är separat och DI-skopad så den kan köras
  direkt i test; hosted service registreras inte i Testing-miljön. Alternativ förkastat: extern
  schemaläggare (Hangfire/Functions) — overkill i v1, noterat i spec Out of Scope.
- **Jobbet är systemkontext** (kör över alla tenants): `IReminderSettingsRepository.
  ListAutoEnabledAsync()` listar organisationer med automatik på; per organisation används
  tenant-scopade läsningar. Detta är ett medvetet, dokumenterat undantag från per-request-
  tenantkontexten — skrivningar sker fortfarande alltid med explicit tenantId.
- **Dubblettskydd:** jobbet skickar inte om det redan finns en **automatisk** logg-post (oavsett
  status) för fakturan — omkörning ger 0 dubbletter (SC-003); misslyckade automatiska försök
  omprövas inte (manuell väg finns). Manuella påminnelser är alltid tillåtna.
- **Berättigande:** `ReminderRules.CanRemind(invoice, today)` — endast `Type=Invoice` som är
  förfallen (skickad, obetald, förfallodatum passerat). Kreditfakturor/utkast/betalda/krediterade
  nekas (`invalid_state`).
- **Ordningsnummer** = antal tidigare *lyckade* påminnelser + 1.
- **Styrbar klocka i test:** `MutableClock` ersätter `IClock` i test-factoryn så tester kan flytta
  fram tiden och göra fakturor förfallna (JWT-validering använder verklig tid och påverkas inte).

## Constitution Check

| Princip | Uppfyllnad |
|---|---|
| I. Spec-driven | Härlett ur spec 004 + Clarifications. |
| II. Clean Code & SOLID | Berättiganderegel + mejlbyggnad i Domain/kärnkomponent, fri från HTTP/schemaläggning; jobbet är tunn orkestrering. |
| III. TDD | Regeltester + integrationstester (manuell, jobb, dubblett, inställningar, isolering) före/med implementation. |
| IV. API-kontrakt först | [contracts/rest-api.md](contracts/rest-api.md) före implementation. |
| V. Isolering & säkerhet | `invoiceReminders` via `TenantScopedRepository`; inställningar per tenant, endast Owner/Admin skriver; jobbets systemläsning dokumenterad och skrivningar alltid tenant-explicita. |
| VI. CI/CD | Samma pipeline; PR mot `develop` när grönt. |

**Resultat:** PASS.

## Project Structure (tillägg)

```text
backend/src/
├── Faktura.Domain/
│   ├── Invoicing/InvoiceReminder.cs, ReminderRules.cs, ReminderSettings.cs
│   └── Abstractions/IInvoiceReminderRepository.cs, IReminderSettingsRepository.cs
├── Faktura.Infrastructure/Persistence/ (documents + Mongo-repos + collections/index)
└── Faktura.Api/Features/Invoicing/
    ├── ReminderContracts.cs, ReminderMailer.cs, ReminderService.cs
    ├── ReminderJob.cs, ReminderBackgroundService.cs
    └── endpoints: POST /invoices/{id}/remind, GET /invoices/{id}/reminders,
                   GET/PUT /api/reminder-settings

frontend/src/pages/Invoices.tsx ("Påminn" på förfallna) + Dashboard.tsx (inställningskort)
```

## Complexity Tracking

| Val | Varför | Enklare alternativ förkastat |
|---|---|---|
| In-process BackgroundService | FR-006 kräver återkommande jobb | Extern schemaläggare = ny infra i v1 |
| Systemkontext-läsning i jobbet | Jobbet spänner över tenants | Per-request tenantkontext kan inte driva ett bakgrundsjobb |
