# Feature Specification: Betalningsreskontra, OCR-nummer & delkreditering

**Feature Branch**: `feature/012-betalningar-ocr` · **Created**: 2026-07-19 · **Status**: Draft

## Översikt

Gör betalflödet verklighetstroget enligt svensk standard:
1. **OCR-nummer** — varje skickad faktura får ett OCR-referensnummer enligt bankgirots
   standard (Luhn mod-10 med längdsiffra, "hård kontrollnivå 2"), visas i app + på PDF.
2. **Betalningsreskontra** — betalningar registreras styckvis (belopp, datum, notering);
   fakturan blir Betald först när saldot är noll. Delbetald faktura syns som DELBETALD.
3. **Delkreditering** — kreditfaktura kan skapas för valda rader/antal i stället för hela
   fakturan (betalar den dokumenterade skulden "delkreditering").

## User Stories
### US1 — OCR-nummer (P1)
När en faktura skickas genereras OCR-numret ur fakturanumret (basnummer + längdsiffra +
Luhn-kontrollsiffra). Det visas i detaljvyn och i PDF:ns betalningsblock. Befintliga fakturor
utan OCR renderas som idag (bakåtkompatibelt).

### US2 — Delbetalningar (P1)
Användaren registrerar en betalning med belopp (obligatoriskt), betaldatum (default idag) och
valfri notering. Saldo = brutto − summa betalningar. Vid 0 kr kvar sätts status Betald med
sista betalningens datum. Överbetalning avvisas. Historiken listas i detaljvyn.
Snabbknappen "Betald" i listan finns kvar och registrerar hela kvarvarande saldot som en betalning.

### US3 — Delkreditering (P2)
Kreditering kan ange rader (radindex + antal ≤ ursprungligt antal). Kreditfakturan får de valda
raderna negerade; originalets `CreditedAmount` ökas med kreditnotans bruttobelopp. Utan rader
i anropet krediteras allt (befintligt beteende). Summan av kreditering kan aldrig överstiga
originalets brutto.

## Requirements
- **FR-001**: `OcrNumber.Generate(number)` i domänen: bas + längdsiffra (totallängd mod 10) +
  Luhn-kontrollsiffra; `IsValid` verifierar. Sätts i `Invoice.Send`, lagras på dokumentet.
- **FR-002**: `POST /api/invoices/{id}/payments` (belopp > 0, ≤ saldo; endast typ Invoice i
  status Sent) + `GET /api/invoices/{id}/payments`. Egen collection `invoicePayments`,
  tenant-isolerad. `InvoiceDto` får `OcrNumber`, `PaidAmount`, `RemainingAmount`.
- **FR-003**: Härledd status: Overdue > PartiallyPaid > Sent (lagrad status är oförändrat Sent).
  Dashboardens "Utestående/Förfallet" räknar på kvarvarande saldo, inte brutto.
- **FR-004**: `POST /api/invoices/{id}/credit` tar valfri body `{ lines: [{index, quantity}] }`;
  validering före nummerförbrukning (index i intervall, 0 < antal ≤ radens antal, belopp ≤
  kvarvarande krediterbart).
- **FR-005**: Frontend: detaljvyn får betalningsformulär + betalningshistorik + OCR + saldo;
  kreditering med radval; listans "Betald"-knapp betalar saldot; Badge för DELBETALD.

## Success Criteria
- **SC-001**: OCR genereras deterministiskt och validerar med Luhn (domäntest med kända värden).
- **SC-002**: Delbetalning ger PartiallyPaid + korrekt saldo; slutbetalning ger Paid med rätt
  datum; överbetalning ger 400 (domän- + API-test).
- **SC-003**: Delkreditering skapar kreditnota med endast valda rader och uppdaterar
  originalets krediterade belopp; överkreditering avvisas (domän- + API-test).
- **SC-004**: Betalningar är tenant-isolerade (API-test mot annan tenants faktura ⇒ 404).

## Out of Scope
Automatisk inläsning av betalfiler (BgMax/camt.054), återbetalningar, ränta/påminnelseavgift,
koppling kreditnota ↔ reskontra (kreditering påverkar inte registrerade betalningar).
