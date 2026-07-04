# REST-kontrakt (Phase 1): E-postutskick av faktura

Bas: `/api`. JSON. Auth via Bearer (001). Tenant-isolerat. Fel: `problem+json`.

## Felkoder (utöver 001/002)
| Kod | När |
|---|---|
| 422 `no_recipient` | ingen giltig mottagaradress (kund saknar e-post och ingen överstyrd) |
| 422 `invalid_recipient` | överstyrd adress har ogiltigt format |
| 409 `invalid_state` | försök att mejla ett utkast (ej skickat) |
| 502 `email_failed` | SMTP-/leveransfel (utskicket loggas som `failed`) |

---

### POST /api/invoices/{id}/email
Mejlar en skickad faktura/kreditfaktura till kunden med PDF-bilaga.
```jsonc
// body (valfritt); recipient överstyr kundens adress
{ "recipient": "ekonomi@kund.se" }
// 200
{ "id","invoiceId","recipient","subject","status":"sent","sentAt" }
```
Fel: 409 `invalid_state` (utkast), 422 `no_recipient`/`invalid_recipient`, 502 `email_failed`
(svaret innehåller ändå den loggade `failed`-posten via historiken).

### GET /api/invoices/{id}/emails → 200 `[InvoiceEmailDto]`
Utskickshistorik för fakturan (tenant-scoped).
```jsonc
[ { "id","invoiceId","recipient","subject","status","error","sentAt" } ]
```

## DTO-noteringar
- `InvoiceEmailDto`: `{ id, invoiceId, recipient, subject, status, error?, sentAt }`.
- `status` ∈ `sent | failed`. Tider ISO-8601 UTC.
- Mejlets From/Reply-To sätts serverside (systemadress + org-namn; Reply-To = avsändaren) och
  är inte en del av request/response.
