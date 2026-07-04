# REST-kontrakt (Phase 1): Betalningspåminnelser

Bas: `/api`. Auth via Bearer (001). Tenant-isolerat. Fel: `problem+json`.

## Felkoder (återanvända från 001–003)
| Kod | När |
|---|---|
| 409 `invalid_state` | fakturan är inte förfallen (utkast/ej förfallen/betald/krediterad/kreditfaktura) |
| 422 `no_recipient` / `invalid_recipient` | mottagare saknas/ogiltig |
| 502 `email_failed` | SMTP-fel (påminnelsen loggas som `failed`) |
| 403 `forbidden` | Member försöker ändra inställningar |

---

### POST /api/invoices/{id}/remind
Skickar en betalningspåminnelse för en **förfallen** faktura (original-PDF bifogas).
```jsonc
// body (valfri)
{ "recipient": "ekonomi@kund.se" }
// 200
{ "id","invoiceId","type":"Manual","recipient","subject","sequence":1,"status":"Sent","sentAt" }
```

### GET /api/invoices/{id}/reminders → 200 `[InvoiceReminderDto]`
Påminnelsehistorik (typ, mottagare, ordningsnummer, status, tidpunkt), senaste först.

### GET /api/reminder-settings → 200
```jsonc
{ "autoEnabled": false, "daysAfterDue": 7 }
```
Läsbar för alla inloggade i organisationen.

### PUT /api/reminder-settings *(Owner/Admin)* → 200
```jsonc
{ "autoEnabled": true, "daysAfterDue": 10 }   // daysAfterDue >= 0
```
Member → 403.

## Jobbet (inget HTTP-kontrakt)
`ReminderBackgroundService` kör `ReminderJob` minst dagligen: för varje organisation med
`autoEnabled` skickas automatiska påminnelser (`type=Automatic`, utan Reply-To) till fakturor
förfallna ≥ `daysAfterDue` dagar som saknar tidigare automatisk påminnelse.
