# Implementation Plan: Återkommande fakturor (007)

**Branch**: `feature/007-aterkommande-fakturor` · **Spec**: [spec.md](spec.md) · Constitution: PASS
(domänlogik ren + TDD; jobb i systemkontext enligt 004-mönstret; kontrakt nedan; isolering via
TenantScopedRepository).

## Beslut

- **`RecurringInvoice`-aggregat** (Domain): kund, rader (återanvänder `InvoiceLine`), intervall
  (Monthly/Quarterly/Yearly), start-/slutdatum, `NextRunDate`, status (Active/Paused).
  `IsDue(today)` + `AdvanceNextRun()` (DateOnly.AddMonths klampar månadsslut) — TDD-kärna.
- **`InvoiceMailer` extraheras** (delad, tenant-explicit — 003:s mejlbyggnad): används av både
  `EmailService` (manuell) och jobbet. Ingen logikduplicering.
- **`RecurringInvoiceJob`** (systemkontext som `ReminderJob`): `ListDueAsync(today)` (dokumenterat
  tvär-tenant-läsundantag) → per mall: catch-up-loop (tak 24) — skapa faktura från mallens rader,
  skicka (nummerserie + snapshot + betalvillkor), mejla (failed-logg vid saknad adress), avancera
  `NextRunDate`, spara. Feltolerant per mall. Kör dagligen via `RecurringBackgroundService`
  (ej i Testing).
- **Spårbarhet:** `Invoice.RecurringSourceId` (valfri, `BsonIgnoreIfNull` — bakåtkompatibel);
  `GET /api/recurring-invoices/{id}/generated` listar mallens fakturor.
- **Kontrakt:** `GET/POST /api/recurring-invoices`, `PUT /{id}`, `POST /{id}/pause|resume`,
  `GET /{id}/generated`. Dto: `{ id, customerId, interval, status, startDate, nextRunDate,
  endDate?, lines[], gross }`.

## Tasks

- [x] T001 [Domain] `RecurringInvoice` (validering, IsDue, AdvanceNextRun m. klampning, paus/slut) + domäntester
- [x] T002 [Infra] Document + `MongoRecurringInvoiceRepository` (TenantScopedRepository + system-`ListDueAsync`) + index `{status, nextRunDate}` + DI; `Invoice.RecurringSourceId`
- [x] T003 [Api] Extrahera `InvoiceMailer` (EmailService delegerar); `RecurringInvoiceService` + endpoints + integrationstester (CRUD/paus/isolering)
- [x] T004 [Api] `RecurringInvoiceJob` + `RecurringBackgroundService` + jobbtester (ikapp 3 perioder → 3 löpande nummer; omkörning 0; paus/slutdatum; saknad e-post → failed-logg men skickad faktura; feltolerans)
- [x] T005 [Frontend] Sida "Återkommande" (lista/skapa/pausa) + Nav-länk; vitest/build/lint gröna
- [x] T006 PR mot `develop` när grönt
