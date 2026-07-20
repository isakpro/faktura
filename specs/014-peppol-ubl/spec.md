# Feature Specification: E-faktura — Peppol BIS Billing 3.0 (UBL)

**Feature Branch**: `feature/014-peppol-ubl` · **Created**: 2026-07-20 · **Status**: Draft

## Översikt

Exportera en skickad faktura som ett **UBL 2.1-dokument enligt Peppol BIS Billing 3.0**
(EN 16931-profilen) — den europeiska standarden för e-fakturor mellan företag och till
offentlig sektor. Ger appen en riktig enterprise-integrationspunkt utöver PDF.

## User Stories
### US1 — Ladda ner Peppol-XML (P1)
I detaljvyn för en skickad faktura (typ Invoice) finns knappen "Peppol-XML". Den laddar ner
ett giltigt UBL `Invoice`-dokument med rätt namnrymder, `CustomizationID`/`ProfileID` för
BIS Billing 3.0, säljare (från fakturaprofilen), köpare (från kundens ögonblicksbild), rader,
momsuppdelning och totaler.

### US2 — Kreditfaktura som UBL CreditNote (P2)
Kreditfakturor exporteras som ett UBL `CreditNote`-dokument (egen rotelement, `CreditNoteLine`)
med referens till originalfakturans nummer.

## Requirements
- **FR-001**: `PeppolInvoiceGenerator` i domänen (rent XML, inga I/O) bygger UBL 2.1 enligt
  BIS Billing 3.0: `Invoice`/`CreditNote`-rot, `cbc:CustomizationID` =
  `urn:cen.eu:en16931:2017#compliant#urn:fdc:peppol.eu:2017:poacc:billing:3.0`,
  `cbc:ProfileID` = `urn:fdc:peppol.eu:2017:poacc:billing:01:1.0`, `ID`/`IssueDate`/`DueDate`,
  `AccountingSupplierParty`/`AccountingCustomerParty`, `InvoiceLine`/`CreditNoteLine` per rad
  (kvantitet, pris, momskategori S/Z beroende på sats), `TaxTotal` per momssats,
  `LegalMonetaryTotal`.
- **FR-002**: Belopp i `SEK` (`currencyID`); saknas köparens VAT/org-nummer utelämnas
  motsvarande element (giltig XML även med ofullständig kunddata — annat än PDF:en görs
  ingen data obligatorisk i UI:t).
- **FR-003**: `GET /api/invoices/{id}/peppol` (auktoriserad, tenant-scoped) returnerar
  `application/xml`; 409 för utkast, 404 för okänd/annan tenants faktura.
- **FR-004**: Frontend: "Peppol-XML"-knapp i detaljvyn bredvid PDF-knappen (samma mönster som
  `openAuthed`).

## Success Criteria
- **SC-001**: Genererad XML är well-formed och innehåller rätt `CustomizationID`/`ProfileID`
  (domäntest, strängbaserad + `XDocument`-parsning).
- **SC-002**: Belopp/momsuppdelning i XML:en matchar `InvoiceCalculator` exakt, inklusive
  flera momssatser (domäntest).
- **SC-003**: Kreditfaktura genererar `CreditNote`-rot med `BillingReference` till originalet
  (domäntest).
- **SC-004**: Endpointen kräver auth + rätt tenant, 409 för utkast (API-test).

## Out of Scope
Faktisk Peppol-nätverksöverföring (Access Point-integration/AS4), inkommande e-fakturor,
validering mot fullständigt EN 16931-schematron (vi verifierar strukturen vi själva sätter,
inte tredjeparts XSD/Schematron-filer).
