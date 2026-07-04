# Implementation Plan: Fakturadomänen

**Branch**: `feature/002-fakturadoman` | **Date**: 2026-07-03 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `specs/002-fakturadoman/spec.md`

## Summary

Bygg fakturadomänen ovanpå 001-skelettet: kunder, fakturautkast med rader och **momsberäkning**
per rad (svenska satser, exkl. moms, öresavrundad så deltotaler summerar till total), **skick**
med atomiskt tilldelat löpande fakturanummer per tenant + låsning (oföränderlig), betalstatus/
förfallobevakning, **kreditfaktura** för rättelse och **server-side PDF**. Återanvänder 001:s
lager, `TenantScopedRepository`, JWT/RBAC och Mongo. Domänberäkningarna byggs test-först.

## Technical Context

**Language/Version**: C# / .NET 10 (backend), TypeScript/React 19 (frontend) — som 001
**Primary Dependencies**: ASP.NET Core, `MongoDB.Driver`, **QuestPDF** (PDF), återanvänder 001:s
auth/rate limiting. Frontend: TanStack Query, react-router-dom.
**Storage**: MongoDB — nya collections `customers`, `invoices` (rader inbäddade), `invoiceCounters`
(atomisk nummerserie per tenant). Belopp som `Decimal128`.
**Testing**: xUnit (domän: momsberäkning, avrundning, kreditregler, status/låsning), integration
via `WebApplicationFactory` + in-memory-repos; concurrency-test för nummerserie.
**Target Platform**: Render (API) + Cloudflare Pages (web) + Atlas — som 001
**Project Type**: Web application (backend + frontend), samma solution
**Performance Goals**: p95 < 300 ms; PDF-generering < 1 s per faktura
**Constraints**: skickad faktura oföränderlig; nummerserie obruten även vid samtidighet;
tenant-isolering i datalagret; belopp exakta (ingen float)
**Scale/Scope**: liten initialt; designen ska tåla samtidiga skick utan dubbla nummer

## Constitution Check

*GATE: Måste passera före Phase 0. Omprövas efter Phase 1.*

| Princip | Hur planen uppfyller den |
|---|---|
| I. Spec-driven | Härlett ur spec 002 + Clarifications; inget utanför specen. |
| II. Clean Code & SOLID | Momslogik, avrundning, status-/kreditregler och nummertilldelning i `Domain`, fri från Mongo/PDF/HTTP. |
| III. TDD för domänlogik | Momsberäkning per sats + öresavrundning, "skickad = oföränderlig", kredittak, förfalloberäkning skrivs test-först. Concurrency-test för unik obruten serie (SC-002). |
| IV. API-kontrakt först | Endpoints/DTO:er i [contracts/rest-api.md](contracts/rest-api.md) före implementation; belopp/moms-representation låst. |
| V. Multi-tenant isolering & säkerhet | Customer/Invoice ärver `TenantScopedRepository`; nummerserie och alla queries filtreras på `tenantId` ur JWT. |
| VI. CI/CD & grön pipeline | Samma `ci.yml`; PR mot `develop` öppnas när 002 klart och grönt. |

**Resultat:** PASS. QuestPDF-licens noteras i Complexity Tracking (ej principavvikelse).

## Project Structure

### Documentation (this feature)

```text
specs/002-fakturadoman/
├── plan.md · research.md · data-model.md · quickstart.md
├── contracts/rest-api.md
├── checklists/requirements.md
└── tasks.md            # skapas av /speckit-tasks
```

### Source Code (tillägg i befintlig solution)

```text
backend/src/
├── Faktura.Domain/
│   ├── Common/Money.cs                  # exakt belopp (decimal, öresavrundning)
│   ├── Customers/Customer.cs
│   └── Invoicing/
│       ├── Invoice.cs, InvoiceLine.cs   # utkast/skick/lås, kreditreferens
│       ├── VatRate.cs, InvoiceStatus.cs, InvoiceType.cs
│       ├── InvoiceCalculator.cs         # netto/moms per sats/brutto (TDD-kärna)
│       └── CreditNote.cs                # kreditregler (tak, referens)
├── Faktura.Infrastructure/
│   ├── Persistence/ (Mongo{Customer,Invoice}Repository, InvoiceNumberSequence)
│   └── Pdf/QuestPdfInvoiceGenerator.cs  # IInvoicePdfGenerator
└── Faktura.Api/Features/
    ├── Customers/ (service + endpoints)
    └── Invoicing/ (service + endpoints, inkl. /pdf)

frontend/src/pages/  (Customers, Invoices: lista/utkast-editor/detalj)
```

**Structure Decision**: Samma web-app-struktur och clean architecture som 001 — nya domänområden
`Customers` och `Invoicing` i `Domain`, Mongo-repos + QuestPDF i `Infrastructure`, feature-mappar
i `Api`. Ingen ny lösning; bygger vidare på befintlig.

## Complexity Tracking

| Val | Varför | Enklare alternativ förkastat |
|---|---|---|
| QuestPDF-beroende | Riktig server-side faktura-PDF (FR-015) | HTML/print räcker ej för nedladdningsbar PDF i v1; QuestPDF Community är gratis < ~$1M omsättning |
| `invoiceCounters` + atomisk `$inc` | Obruten unik serie vid samtidighet (FR-009/SC-002) | App-räkning eller "max+1" ger race/dubletter |
