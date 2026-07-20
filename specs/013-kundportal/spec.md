# Feature Specification: Kundportal — publik fakturalänk

**Feature Branch**: `feature/013-kundportal` · **Created**: 2026-07-19 · **Status**: Draft

## Översikt

Mottagaren av en faktura ska kunna öppna den i webbläsaren utan konto: en delbar
kapabilitets-länk `/f/{token}` visar fakturan (säljare, rader, summor, OCR, saldo, status)
och låter kunden ladda ner PDF:en. Det ger äkta SaaS-känsla och gör e-postutskicken
klickbara på riktigt.

## User Stories
### US1 — Dela fakturan (P1)
Användaren hämtar en kundlänk för en skickad faktura ("Kundlänk"-knapp i detaljvyn).
Länken skapas första gången den begärs (även för äldre fakturor) och är stabil därefter.
Utkast kan inte delas.

### US2 — Kunden ser fakturan (P1)
Kunden öppnar `/f/{token}` utan inloggning: säljarens namn + fakturaprofil, kundnamn,
nummer/datum/förfallodag, rader, summor per momssats, OCR, betalt/saldo och status-stämpel.
Ogiltig token ger "hittades inte". PDF kan laddas ner från sidan utan inloggning.

### US3 — E-postutskicken länkar till portalen (P2)
Faktura- och påminnelsemejl innehåller portallänken när `App__BaseUrl` är satt.

## Requirements
- **FR-001**: `Invoice.ShareToken` (128-bit slumpmässig hex) tilldelas via
  `POST /api/invoices/{id}/share` (auktoriserad, idempotent — återanvänder befintlig).
  Endast typ Invoice med nummer. Svar: `{ url }` byggd av `App__BaseUrl`.
- **FR-002**: Publika endpoints utan auth: `GET /api/public/invoices/{token}` (begränsad DTO,
  inga tenant-/kund-id:n) och `GET /api/public/invoices/{token}/pdf`. Uppslag via unikt
  partial-index på `shareToken`; läsningen är systemkontext (tvär-tenant per definition,
  dokumenterad). Okänd token ⇒ 404.
- **FR-003**: Publik SPA-route `/f/{token}` utan Nav/auth — "papperslik" fakturavy i
  Huvudboken-tema + PDF-knapp.
- **FR-004**: `InvoiceMailer`/`ReminderMailer` inkluderar portallänk i mejltexten när
  BaseUrl finns och fakturan har/får token.

## Success Criteria
- **SC-001**: Share är idempotent, kräver auth + rätt tenant (annan tenant ⇒ 404), utkast ⇒ 409.
- **SC-002**: Publik GET fungerar utan Authorization-header; svaret innehåller inte tenantId/
  customerId; fel token ⇒ 404 (API-test).
- **SC-003**: Publik PDF = samma bytes-typ som auktoriserad PDF (API-test).
- **SC-004**: Delbetalningar syns i portalen (saldo), status visas som stämpel.

## Out of Scope
Betalning i portalen (Stripe checkout för slutkund), kommentarer, flera länkar/återkallning,
lösenordsskyddade länkar, spårning av visningar.

## Beslut & avvägningar
Token lagras i klartext på fakturadokumentet (kapabilitets-URL som måste kunna visas igen);
128 bitar slumpmässighet gör gissning ogörlig. Återkallning är utanför scope.
