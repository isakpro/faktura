# Data Model (Phase 1): SaaS-skelett

MongoDB, delad databas `faktura`. Tenant-ägda collections bär **`tenantId`** och har
sammansatta index med `tenantId` först. `organizations` är tenant-roten (dess `_id` = tenantId).

## Collections

### `organizations` (tenant-roten)
| Fält | Typ | Noteringar |
|---|---|---|
| `_id` | ObjectId/Guid | = `tenantId` |
| `name` | string | organisationsnamn |
| `plan` | string | `free` \| `pro` (aktuell nivå) |
| `subscriptionStatus` | string | `none` \| `active` \| `past_due` \| `canceled` |
| `stripeCustomerId` | string? | sätts vid första Checkout |
| `stripeSubscriptionId` | string? | aktiv prenumeration (testläge) |
| `seatLimit` | int | härleds ur plan-config; cachas för snabb kontroll |
| `createdAt` | DateTime (UTC) | |

> `plan`/`seatLimit` speglar plan-config men persisteras för enkel gating; sanningskällan
> för *vad en plan ger* är `PlanDefinition` (config), inte hårdkodning (FR-019).

### `users`
| Fält | Typ | Noteringar |
|---|---|---|
| `_id` | ObjectId/Guid | userId (`sub` i JWT) |
| `tenantId` | ref organizations | **isoleringsnyckel** |
| `email` | string | unik **globalt** i v1 (en användare = en org) |
| `passwordHash` | string | PBKDF2 |
| `role` | string | `owner` \| `admin` \| `member` |
| `status` | string | `active` (v1: aktiv direkt, ingen e-postverifiering) |
| `createdAt` | DateTime (UTC) | |

### `invitations`
| Fält | Typ | Noteringar |
|---|---|---|
| `_id` | ObjectId/Guid | |
| `tenantId` | ref organizations | isoleringsnyckel |
| `email` | string | inbjuden e-post |
| `role` | string | avsedd roll (`admin`\|`member`) |
| `tokenHash` | string | hash av accept-token (token skickas till mottagaren) |
| `status` | string | `pending` \| `accepted` \| `revoked` \| `expired` |
| `expiresAt` | DateTime (UTC) | |
| `createdAt` | DateTime (UTC) | |

### `refreshTokens`
| Fält | Typ | Noteringar |
|---|---|---|
| `_id` | ObjectId/Guid | |
| `tenantId` | ref organizations | |
| `userId` | ref users | |
| `tokenHash` | string | hash av refresh-token (roterande) |
| `expiresAt` | DateTime (UTC) | |
| `revokedAt` | DateTime? (UTC) | sätts vid logout/rotation |

### `processedStripeEvents` (idempotens)
| Fält | Typ | Noteringar |
|---|---|---|
| `_id` | string | Stripe `event.id` (unikt → idempotens, FR-017) |
| `type` | string | event-typ |
| `processedAt` | DateTime (UTC) | |

### `planDefinitions` (config, ej tenant-ägd)
Datadriven plan-konfiguration (FR-019). Kan seedas vid start.
| Fält | Typ | Noteringar |
|---|---|---|
| `_id` | string | `free` \| `pro` |
| `seatLimit` | int | Free = 2, Pro = t.ex. 25 |
| `rateLimit` | object | `{ permitLimit, windowSeconds }` per plan |
| `stripePriceId` | string? | testläge-pris (för Pro) |

## Index

- `organizations`: `_id` (default).
- `users`: unikt `{ email: 1 }` (global unik v1); `{ tenantId: 1, _id: 1 }`; `{ tenantId: 1, role: 1 }`.
- `invitations`: `{ tenantId: 1, email: 1 }`; `{ tokenHash: 1 }`; TTL på `expiresAt`.
- `refreshTokens`: `{ tokenHash: 1 }`; `{ userId: 1 }`; TTL på `expiresAt`.
- `processedStripeEvents`: `_id` unikt (event-id).

## Invarianter (testas — constitution III/V)

1. Varje tenant-ägt dokument har `tenantId`; alla queries filtrerar på `tenantId` (FR-007).
2. `tenantId` härleds ur JWT, aldrig ur request-body (FR-008).
3. En organisation har alltid ≥ 1 `owner` (FR-013).
4. Antal `active` users per tenant ≤ `seatLimit` (FR-025).
5. Planändring sker bara via verifierad, ej tidigare sedd Stripe-event (FR-017).
