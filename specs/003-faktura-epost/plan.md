# Implementation Plan: E-postutskick av faktura

**Branch**: `feature/003-faktura-epost` | **Date**: 2026-07-04 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `specs/003-faktura-epost/spec.md`

## Summary

Mejla en skickad faktura/kreditfaktura till kunden med fakturans PDF som bilaga, som en separat
åtgärd. Återanvänder 002:s PDF-generator och 001:s tenant-isolering/auth. E-post skickas via SMTP
bakom en `IEmailSender`-abstraktion (fejkas i test). Varje utskick loggas i en egen collection så
den skickade fakturan förblir oföränderlig. From = systemadress + organisationens namn; Reply-To =
avsändarens e-post.

## Technical Context

**Language/Version**: C#/.NET 10 (backend), TS/React 19 (frontend) — som 001/002
**Primary Dependencies**: **MailKit** (SMTP), återanvänder `IInvoicePdfGenerator` (002),
`TenantScopedRepository`/JWT (001). Frontend: TanStack Query.
**Storage**: MongoDB — ny collection `invoiceEmails` (tenant-ägd, per faktura). Ingen ändring av
`invoices` (immutabilitet från 002 bevaras).
**Testing**: xUnit — domän (recipient-val, meddelandebyggnad), integration via
`WebApplicationFactory` + **fejkad `IEmailSender`** (fångar meddelandet, inga riktiga mejl) +
in-memory-repo.
**Target Platform**: Render/Cloudflare/Atlas — som 001/002
**Project Type**: Web application (backend + frontend), samma solution
**Constraints**: leveransfel får ej ändra fakturan; SMTP-uppgifter endast via miljö; tenant-isolering
**Scale/Scope**: litet; ingen kö/retry i v1 (synkront utskick, loggat resultat)

## Key Decisions (Phase 0)

- **SMTP via MailKit bakom `IEmailSender`.** Leverantörsoberoende; interface gör domänen/tjänsten
  testbar utan riktiga mejl. Alternativ förkastat: `System.Net.Mail.SmtpClient` (obsolet), binda
  till ett transaktions-API (leverantörslås).
- **Utskickslogg i egen collection `invoiceEmails`.** Bevarar fakturans oföränderlighet (002); ger
  historik. Alternativ förkastat: bädda in i `invoices` (muterar en låst faktura).
- **Reply-To = avsändarens e-post** (ur JWT `email`-claim). `ITenantContext` utökas med `Email`.
- **Synkront utskick + resultatlogg.** Vid SMTP-/valideringsfel loggas `Failed` och felet
  returneras (fakturan orörd). Ingen kö/retry i v1 (noteras som framtida).
- **Recipient-val:** överstyrd adress → annars kundens (snapshot/kund) e-post → annars fel.

## Constitution Check

| Princip | Uppfyllnad |
|---|---|
| I. Spec-driven | Härlett ur spec 003 + Clarifications. |
| II. Clean Code & SOLID | Recipient-val + meddelandebyggnad i Domain, fri från SMTP/HTTP. |
| III. TDD | Recipient-val, utkast-nekas, meddelande (ämne/bilaga), fel-loggning test-först. |
| IV. API-kontrakt först | Endpoints/DTO i [contracts/rest-api.md](contracts/rest-api.md) före implementation. |
| V. Isolering & säkerhet | `invoiceEmails` via `TenantScopedRepository`; SMTP-secrets i env. |
| VI. CI/CD | Samma pipeline; PR mot `develop` när grönt. |

**Resultat:** PASS.

## Project Structure (tillägg)

```text
backend/src/
├── Faktura.Domain/
│   ├── Abstractions/IEmailSender.cs, IInvoiceEmailRepository.cs
│   ├── Emailing/EmailMessage.cs (+ EmailAttachment)
│   └── Invoicing/InvoiceEmail.cs (logg-entitet)
├── Faktura.Infrastructure/
│   ├── Email/SmtpEmailSender.cs, SmtpOptions.cs
│   └── Persistence/MongoInvoiceEmailRepository.cs (+ document, collection/index)
└── Faktura.Api/Features/Invoicing/ (EmailService + endpoints email/emails)

frontend/src/pages/Invoices.tsx  (knapp "Mejla" + historik)
```

**Structure Decision**: Samma web-app/clean architecture. Ny e-post-abstraktion i Domain,
SMTP + Mongo-logg i Infrastructure, utökar faktura-featuren i Api/frontend.

## Complexity Tracking

| Val | Varför | Enklare alternativ förkastat |
|---|---|---|
| MailKit-beroende | Korrekt, underhållen SMTP-klient | `System.Net.Mail.SmtpClient` är obsolet |
| Egen `invoiceEmails`-collection | Bevarar fakturans oföränderlighet + historik | Inbäddning muterar låst faktura |
