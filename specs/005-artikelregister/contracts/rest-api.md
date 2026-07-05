# REST-kontrakt (Phase 1): Artikelregister

Bas: `/api`. Auth via Bearer. Tenant-isolerat. Fel: `problem+json`.

## Felkoder
| Kod | När |
|---|---|
| 409 `sku_taken` | artikelnumret används redan i organisationen |
| 422 | valideringsfel (namn saknas, pris < 0, ogiltig momssats) |
| 404 | artikel saknas i egen tenant |

### GET /api/articles?status=active|archived|all → 200 `[ArticleDto]` (default `active`)
### POST /api/articles → 201 ArticleDto
```jsonc
{ "name":"Konsulttimme", "sku":"K-100", "unit":"tim", "unitPriceExclVat":1200, "vatRate":25 }
```
### GET /api/articles/{id} → 200 · PUT /api/articles/{id} → 200 · POST /api/articles/{id}/archive → 204

`ArticleDto`: `{ id, name, sku?, unit?, unitPriceExclVat, vatRate, status }`.

## Fakturarader (utökning av 002-kontraktet)
`InvoiceLineInput`/`InvoiceLineDto` får valfritt `unit`. Klienten förifyller radfält från vald
artikel (snapshot) — servern tar emot vanliga radvärden. PDF visar `"{quantity} {unit}"` när
enhet finns.
