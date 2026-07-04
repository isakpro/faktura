# Data Model (Phase 1): Betalningspåminnelser

MongoDB. Två nya collections; `invoices` oförändrad.

## `invoiceReminders` (tenant-ägd)
| Fält | Typ | Noteringar |
|---|---|---|
| `_id` | ObjectId/string | |
| `tenantId` | ref organizations | isoleringsnyckel |
| `invoiceId` | ref invoices | |
| `type` | string | `manual` \| `automatic` |
| `recipient` | string | mottagaradress |
| `subject` | string | mejlets ämne |
| `sequence` | int | påminnelse nr (antal tidigare lyckade + 1) |
| `status` | string | `sent` \| `failed` |
| `error` | string? | felorsak vid `failed` |
| `sentAt` | DateTime (UTC) | |

Index: `{ tenantId: 1, invoiceId: 1 }`.

## `reminderSettings` (per organisation)
| Fält | Typ | Noteringar |
|---|---|---|
| `_id` | string | = tenantId |
| `autoEnabled` | bool | standard `false` (opt-in) |
| `daysAfterDue` | int | standard 7 |

## Invarianter (testas)
1. Alla påminnelse-queries filtreras på `tenantId` (FR-012); jobbets skrivningar sker alltid med explicit tenantId.
2. Endast förfallna fakturor av typen Invoice kan påminnas (FR-001/002).
3. Max en **automatisk** logg-post per faktura — omkörning av jobbet ger 0 dubbletter (FR-008/SC-003).
4. `invoices` muteras aldrig av en påminnelse (FR-011).
5. Saknad inställning ⇒ automatik av (opt-in).
