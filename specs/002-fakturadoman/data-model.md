# Data Model (Phase 1): Fakturadomänen

MongoDB, delad databas `faktura`. Nya tenant-ägda collections bär `tenantId` (sammansatt index
med `tenantId` först) och går via `TenantScopedRepository` (från 001). Belopp: `Decimal128`.

## Collections

### `customers`
| Fält | Typ | Noteringar |
|---|---|---|
| `_id` | ObjectId/string | |
| `tenantId` | ref organizations | isoleringsnyckel |
| `name` | string | obligatoriskt |
| `email` | string? | |
| `orgNumber` | string? | org-/personnummer |
| `vatNumber` | string? | |
| `address` | objekt? | rad1, rad2, postnr, ort, land |
| `paymentTermsDays` | int | standard 30 |
| `status` | string | `active` \| `archived` |
| `createdAt` | DateTime (UTC) | |

### `invoices` (rader inbäddade)
| Fält | Typ | Noteringar |
|---|---|---|
| `_id` | ObjectId/string | |
| `tenantId` | ref organizations | isoleringsnyckel |
| `customerId` | ref customers | |
| `customerSnapshot` | objekt | kopia av kunduppgifter vid skick (oföränderlig) |
| `type` | string | `invoice` \| `credit_note` |
| `status` | string | `draft` \| `sent` \| `paid` \| `credited` |
| `number` | long? | tilldelas vid skick (null i utkast) |
| `invoiceDate` | Date? | sätts vid skick |
| `dueDate` | Date? | invoiceDate + paymentTermsDays |
| `paidDate` | Date? | vid markera betald |
| `originalInvoiceId` | ref invoices? | endast kreditfaktura |
| `creditedAmount` | Decimal128 | på original: hittills krediterat (default 0) |
| `lines` | array<InvoiceLine> | se nedan |
| `totals` | objekt | netto, momsPerSats[], brutto (härledda, cachas) |
| `createdAt` / `updatedAt` | DateTime (UTC) | |

**InvoiceLine (inbäddad)**
| Fält | Typ | Noteringar |
|---|---|---|
| `description` | string | |
| `quantity` | Decimal128 | |
| `unitPriceExclVat` | Decimal128 | á-pris exkl. moms |
| `vatRate` | int | 25 \| 12 \| 6 \| 0 |
| `netAmount` | Decimal128 | härledd: round(quantity × unitPrice) |
| `vatAmount` | Decimal128 | härledd: round(net × sats) |

**totals.momsPerSats**: lista av `{ rate, vatAmount }`.

### `invoiceCounters`
| Fält | Typ | Noteringar |
|---|---|---|
| `_id` | string | = `tenantId` |
| `seq` | long | senast tilldelade nummer; `$inc` atomiskt vid skick |

## Index

- `customers`: `{ tenantId: 1, name: 1 }`; `{ tenantId: 1, status: 1 }`.
- `invoices`: `{ tenantId: 1, status: 1 }`; `{ tenantId: 1, customerId: 1 }`;
  unikt `{ tenantId: 1, number: 1 }` (sparse — utkast har inget nummer);
  `{ tenantId: 1, originalInvoiceId: 1 }`.
- `invoiceCounters`: `_id` (tenantId).

## Invarianter (testas — constitution III/V)

1. Alla customer/invoice-queries filtreras på `tenantId` (FR-002/017).
2. Utkast saknar `number`; nummer tilldelas **atomiskt** och unikt vid skick (FR-008/009).
3. `status != draft` ⇒ rader/belopp/kund oföränderliga (FR-010).
4. Radernas netto/moms och fakturans totaler är konsistenta; deltotaler summerar till brutto (FR-006).
5. Summa krediterat på en faktura ≤ dess brutto (FR-014).
6. `credit_note` har alltid `originalInvoiceId` och negativa radbelopp.
