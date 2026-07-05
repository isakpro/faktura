# Data Model (Phase 1): Artikelregister

## `articles` (ny, tenant-ägd)
| Fält | Typ | Noteringar |
|---|---|---|
| `_id` | ObjectId | |
| `tenantId` | ref organizations | isoleringsnyckel |
| `name` | string | obligatoriskt |
| `sku` | string? | valfritt artikelnummer; unikt inom tenant när angivet |
| `unit` | string? | st/tim/kg … (valfri) |
| `unitPriceExclVat` | Decimal128 | ≥ 0 |
| `vatRate` | int | 25/12/6/0 |
| `status` | string | `active` \| `archived` |
| `createdAt` | DateTime (UTC) | |

Index: `{tenantId, name}`; **unikt sparse** `{tenantId, sku}` (dokument utan sku undantas).

## `invoices.lines` (utökas)
| Fält | Typ | Noteringar |
|---|---|---|
| `unit` | string? | valfri; `BsonIgnoreIfNull` — befintliga rader opåverkade |

## Invarianter (testas)
1. Artiklar tenant-isolerade; SKU-unikhet gäller per tenant (samma SKU i två orgar OK).
2. Rad-från-artikel är en kopia — pris-/statusändring i registret rör aldrig befintliga rader.
3. Rader utan enhet renderas som tidigare (PDF + DTO bakåtkompatibla).
4. Arkiverad artikel döljs i "aktiva"-listan men kvarstår i registret.
