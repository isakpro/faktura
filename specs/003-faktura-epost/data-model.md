# Data Model (Phase 1): E-postutskick av faktura

MongoDB. Ny tenant-ägd collection `invoiceEmails`; `invoices`/`customers` oförändrade.

## `invoiceEmails`
| Fält | Typ | Noteringar |
|---|---|---|
| `_id` | ObjectId/string | |
| `tenantId` | ref organizations | isoleringsnyckel |
| `invoiceId` | ref invoices | vilken faktura utskicket gäller |
| `recipient` | string | mottagaradress (kundens eller överstyrd) |
| `subject` | string | mejlets ämne |
| `status` | string | `sent` \| `failed` |
| `error` | string? | felorsak vid `failed` |
| `sentAt` | DateTime (UTC) | tidpunkt |

## Index
- `invoiceEmails`: `{ tenantId: 1, invoiceId: 1 }` (historik per faktura, tenant-scoped).

## Invarianter (testas — constitution III/V)
1. Alla utskicks-queries filtreras på `tenantId` (FR-010).
2. Ett utskick loggas för varje försök (lyckat som misslyckat) (FR-007/008).
3. `invoices` muteras aldrig av ett utskick (fakturans oföränderlighet från 002 bevaras).
4. Endast fakturor med `number != null` (skickade) kan mejlas (FR-002).
