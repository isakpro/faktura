# Feature Specification: Publikt API & webhooks

**Feature Branch**: `feature/016-publikt-api-webhooks` · **Created**: 2026-07-20 · **Status**: Draft

## Översikt

Gör Faktura integrerbart: kunder ska kunna hämta/skapa data programmatiskt (API-nycklar) och
få pushade händelser i realtid till sina egna system (webhooks) — den mest "SaaS-mogna"
byggstenen i plattformen.

## User Stories
### US1 — API-nycklar (P1)
Owner/Admin skapar en namngiven API-nyckel med ett eller flera scopes
(`invoices:read`/`customers:read`/`customers:write`). Den råa nyckeln visas endast vid
skapandet. Nyckeln kan återkallas när som helst.

### US2 — Publikt API (P1)
`/api/v1/invoices` (list/get) och `/api/v1/customers` (list/get/create) autentiseras via
header `X-Api-Key` i stället för JWT. Varje endpoint kräver rätt scope på nyckeln; saknat
scope ⇒ 403. Anropen är tenant-isolerade precis som SPA:ns egna endpoints.

### US3 — Webhooks (P2)
Owner/Admin registrerar en eller flera mottagar-URL:er. När en faktura skickas, blir betald
eller krediteras POST:as en signerad händelse (`invoice.sent`/`invoice.paid`/`invoice.credited`)
till varje mottagare. Ett misslyckat leveransförsök görs om en gång; alla försök loggas.

## Requirements
- **FR-001**: `ApiKeyGenerator` (domän) genererar en rå nyckel (`fkt_live_`-prefix) + SHA-256-
  hash; endast hashen lagras. `ApiKey`-entitet: namn, scopes, skapad/senast använd/återkallad.
- **FR-002**: `ApiKeyAuthenticationHandler` — eget auth-scheme som läser `X-Api-Key`, slår upp
  nyckeln på hash (systemkontext, likt portal-token i spec 013), och bygger samma claim-typer
  som JWT-flödet (`tenantId`/`role`/`sub`) plus ett `scopes`-claim. `ITenantContext` och
  tenant-scopade tjänster fungerar därför oförändrat.
- **FR-003**: `/api/v1/*`-endpoints kräver `ApiKey`-schemat specifikt (inte JWT) och kontrollerar
  scope explicit per endpoint; återanvänder `InvoiceService`/`CustomerService` rakt av.
- **FR-004**: `WebhookEndpoint` (URL + genererad hemlighet) + `IWebhookDispatcher.DispatchAsync`
  — signerar nyttolasten med HMAC-SHA256 (`X-Faktura-Signature`), en retry vid fel, loggar varje
  försök (`WebhookDelivery`, append-only). `InvoiceService` anropar dispatch efter Send/
  betalning-blir-Paid/kreditering.
- **FR-005**: `GET/POST/DELETE /api/api-keys` och `/api/webhooks` (Owner/Admin, 403 för Member).
  Frontend: Utvecklare-sida (nyckel-/mottagarhantering, engångsvisning av hemligheter) länkad
  från Inställningar.

## Success Criteria
- **SC-001**: Rå nyckel/hemlighet visas aldrig igen efter skapandet (varken i list-svar eller
  efterföljande GET) — API-test.
- **SC-002**: Saknat scope ger 403 trots giltig nyckel; okänd/återkallad nyckel ger 401 — API-test.
- **SC-003**: `/api/v1/customers` skapar en kund som syns via SPA:ns vanliga `/api/customers` —
  bevisar att samma tjänst/data återanvänds, inte en dubblerad datamodell.
- **SC-004**: Send/betalning-till-Paid/kreditering dispatchar respektive händelsetyp — verifierat
  med en fångande dispatcher-fake (ingen riktig HTTP i testsviten).
- **SC-005**: HMAC-signeringen är deterministisk och skiljer sig vid annan hemlighet/body
  (domäntest av den rena signeringsfunktionen).

## Out of Scope
Fler händelsetyper (kund/artikel-CRUD), webhook-återleverans från UI, konfigurerbar
retry-policy/backoff, per-nyckel IP-allowlist, OpenAPI-dokumentation specifikt för /api/v1,
paginering av /api/v1-listor (delas med server-side sök/paginering, spec 019).
