# Research (Phase 0): Fakturadomänen

Beslut för 002. Format: **Beslut**, **Motiv**, **Alternativ (förkastat)**.

## 1. Beloppsrepresentation: `decimal` + öresavrundning

- **Beslut**: Belopp hanteras som `decimal` i domänen och avrundas till **2 decimaler (öre)** med
  `MidpointRounding.AwayFromZero`. Persisteras som Mongo `Decimal128`. Ett litet `Money`-värdetyp
  kapslar avrundning och aritmetik.
- **Motiv**: `decimal` undviker binära flyttalsfel; öresavrundning away-from-zero matchar svensk
  praxis. `Decimal128` bevarar exakthet i Mongo.
- **Alternativ (förkastat)**: `double` (avrundningsfel), heltal i öre (funkar men klumpigt för
  moms-%-multiplikation), lagra som sträng.

## 2. Momsberäkning: per rad, summera per sats

- **Beslut**: Per rad: `netto = round(antal × á-pris)`, `moms = round(netto × sats)`. Per faktura:
  summa netto, **moms grupperad per sats** (summan av radernas momsbelopp per sats), brutto =
  summa netto + summa moms. Avrundning sker **per rad** så att deltotaler summerar exakt till totalen.
- **Motiv**: Uppfyller FR-005/006 och SC-001 (inga öresdifferenser). Moms per sats krävs på
  svenska fakturor.
- **Alternativ (förkastat)**: avrunda enbart på totalen (ger radvisa differenser), räkna moms på
  bruttopris (vi anger exkl. moms).

## 3. Fakturanummer: atomisk räknare per tenant

- **Beslut**: En `invoiceCounters`-collection med `_id = tenantId` och `seq`. Vid skick körs
  `FindOneAndUpdate` med `$inc: { seq: 1 }`, `IsUpsert=true`, `ReturnDocument=After` → returnerar
  nästa nummer **atomiskt**. Numret tilldelas först vid skick (utkast saknar nummer).
- **Motiv**: MongoDB:s atomiska `$inc` garanterar unika, obrutna nummer även vid samtidiga skick
  (FR-009/SC-002) utan lås i appen.
- **Alternativ (förkastat)**: "max(nummer)+1" (race → dubletter), applås/mutex (skalar dåligt),
  UUID (uppfyller inte krav på löpande serie).

## 4. Oföränderlighet efter skick

- **Beslut**: `Invoice` exponerar mutationer (lägg/ändra rad, byt kund) endast i status `Draft`;
  i `Sent`/`Paid`/`Credited` returnerar de fel (`invoice_locked`). Endast tillåtna övergångar:
  markera betald, skapa kreditfaktura. API:t saknar edit-endpoints för icke-utkast.
- **Motiv**: FR-010/SC-003 — utställd faktura är juridiskt dokument.
- **Alternativ (förkastat)**: tillåta ändring + versionering (komplext, juridiskt tveksamt).

## 5. Kreditfaktura

- **Beslut**: En `Invoice` med `Type = CreditNote`, `OriginalInvoiceId` satt, egna rader med
  **negativa** belopp och samma momssatser. Får eget nummer i samma serie vid skapande. Originalet
  spårar `CreditedAmount`; en ny kredit får inte överstiga `Total - CreditedAmount` (FR-014/SC-005).
- **Motiv**: Juridiskt korrekt rättelse utan att röra originalet.
- **Alternativ (förkastat)**: "makulera + skapa ny" (bryter nummerserie/historik), redigera original.

## 6. Status & förfallo

- **Beslut**: Lagrad status: `Draft | Sent | Paid | Credited`. **Förfallen härleds** (ej lagrad):
  `Sent` och obetald och `dueDate < today`. Förfallodatum = fakturadatum + kundens
  betalningsvillkor (standard 30 dagar). Datum utan tidsdel, svensk tid.
- **Motiv**: Förfallen är tidsberoende — härledning undviker schemalagd statusuppdatering (FR-012).
- **Alternativ (förkastat)**: lagra "Overdue" (kräver batch-jobb som håller det aktuellt).

## 7. PDF: QuestPDF bakom interface

- **Beslut**: `IInvoicePdfGenerator` i domänens abstraktioner; `QuestPdfInvoiceGenerator` i
  Infrastructure renderar fakturans obligatoriska fält. Endast skickade fakturor/kreditfakturor
  får PDF (utkast nekas — FR-016).
- **Motiv**: QuestPDF ger exakt layoutkontroll; interface gör domänen PDF-oberoende och testbar
  (tester kan verifiera datamodellen till generatorn, inte byte-innehållet).
- **Alternativ (förkastat)**: HTML→PDF headless-browser (tung drift), tredjeparts-API (extern data).
- **Licens**: QuestPDF Community (gratis < ~$1M omsättning) — noterat i plan Complexity Tracking.

## 8. Teststrategi

- **Beslut**: Domän-enhetstester (TDD) för `InvoiceCalculator` (alla satser + blandat + avrundning),
  status/lås, kredittak, förfalloberäkning. Integrationstester för customer/invoice-endpoints,
  cross-tenant-isolering och **concurrency** (parallella skick → unika obrutna nummer). PDF:
  smoke-test att en skickad faktura ger en icke-tom PDF.
- **Motiv**: Matchar constitution III och success-kriterierna.
