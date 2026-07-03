# REST-kontrakt (Phase 1): Fakturadomänen

Bas: `/api`. JSON. Auth via `Authorization: Bearer <access-jwt>` (från 001). Fel: `problem+json`.
Alla resurser tenant-isolerade; `tenantId` härleds ur token. Belopp i SEK, exkl. moms där ej annat.

## Felkoder (utöver 001)
| Kod | När |
|---|---|
| 409 `invoice_locked` | ändring av icke-utkast |
| 409 `over_credit` | kreditbelopp överstiger kvarvarande |
| 422 | valideringsfel (t.ex. ogiltig momssats, tom faktura) |
| 404 | resurs saknas i egen tenant |

---

## Kunder

### GET /api/customers → 200 `[{ id, name, email, status, ... }]`
### POST /api/customers *(namn obligatoriskt)* → 201 CustomerDto
```jsonc
{ "name":"Kund AB", "email":"k@kund.se", "orgNumber":"556000-0000",
  "vatNumber":"SE556000000001", "address":{...}, "paymentTermsDays":30 }
```
### GET /api/customers/{id} → 200 CustomerDto
### PUT /api/customers/{id} → 200 CustomerDto
### POST /api/customers/{id}/archive → 204  *(arkiverar; hård-radering nekas om fakturor finns)*

---

## Fakturor

### GET /api/invoices?status=draft|sent|paid|overdue|credited → 200 `[InvoiceListDto]`
`overdue` är ett härlett filter (sent + obetald + förfallen).

### POST /api/invoices *(skapar utkast)* → 201 InvoiceDto
```jsonc
{ "customerId":"...", "lines":[
  { "description":"Konsult", "quantity":10, "unitPriceExclVat":1200, "vatRate":25 } ] }
```

### GET /api/invoices/{id} → 200 InvoiceDto  (rader, netto, momsPerSats[], brutto, status)

### PUT /api/invoices/{id} *(endast utkast)* → 200 InvoiceDto  | 409 `invoice_locked`
Ersätter kund/rader; summor räknas om.

### POST /api/invoices/{id}/send → 200 InvoiceDto
Tilldelar nästa nummer (atomiskt), sätter faktura-/förfallodatum, status `sent`, låser. 422 om tom.

### POST /api/invoices/{id}/mark-paid → 200 InvoiceDto
```jsonc
{ "paidDate":"2026-07-20" }   // status -> paid
```

### POST /api/invoices/{id}/credit → 201 InvoiceDto *(kreditfaktura)*
Skapar kreditdokument mot en skickad faktura; eget nummer, negativa belopp. 409 `over_credit`
om beloppet överstiger kvarvarande. Body valfri (default = full kreditering).

### GET /api/invoices/{id}/pdf → 200 `application/pdf`
Endast skickad faktura/kreditfaktura. Utkast → 409/422.

---

## DTO-noteringar
- `InvoiceDto`: `{ id, type, status, number|null, customer, lines[], totals:{ net, vatByRate[], gross }, invoiceDate|null, dueDate|null, paidDate|null, originalInvoiceId|null }`.
- `vatByRate[]`: `[{ rate, vatAmount }]`.
- `status` som API returnerar kan inkludera härlett `overdue` i list-/detaljvyn även om lagrad status är `sent`.
- Belopp som decimaltal (2 decimaler). Tider/datum ISO-8601; faktura-/förfallodatum är datum (utan tid).
