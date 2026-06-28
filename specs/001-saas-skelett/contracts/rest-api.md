# REST-kontrakt (Phase 1): SaaS-skelett

Bas: `/api`. JSON. Auth via `Authorization: Bearer <access-jwt>`. Fel: `application/problem+json`
(RFC 7807). Inget endpoint tar emot `tenantId` från klienten — det härleds ur token.

## Felkoder (genomgående)
| Kod | När |
|---|---|
| 400/422 | valideringsfel |
| 401 | saknad/ogiltig/utgången token |
| 403 | otillräcklig roll, eller post i annan tenant (visas som "finns inte") |
| 404 | resurs saknas i den egna tenanten |
| 409 | konflikt (upptagen e-post, seat-gräns nådd) |
| 429 | rate limit överskriden; header `Retry-After: <sek>` |

---

## Auth & registrering

### POST /api/auth/register
Self-service: skapar organisation + Owner. Publikt.
```jsonc
// req
{ "organizationName": "Acme AB", "email": "a@acme.se", "password": "…" }
// 201
{ "accessToken": "…", "refreshToken": "…", "user": { "id","email","role":"owner" },
  "organization": { "id","name","plan":"free" } }
```
Fel: 409 (e-post upptagen, utan att läcka detaljer), 422 (svagt lösenord).

### POST /api/auth/login
```jsonc
{ "email":"a@acme.se", "password":"…" }  // → 200 { accessToken, refreshToken, user, organization }
```
Fel: 401 (fel uppgifter, generiskt meddelande), 429 (för många försök).

### POST /api/auth/refresh
```jsonc
{ "refreshToken":"…" }  // → 200 { accessToken, refreshToken }  (roterar)
```
Fel: 401 (ogiltig/återkallad/utgången).

### POST /api/auth/logout  *(auth)*
Återkallar aktuell refresh-token. → 204.

### GET /api/me  *(auth)*
→ 200 `{ user: {id,email,role}, organization: {id,name,plan,subscriptionStatus,seatLimit} }`

---

## Medlemmar & inbjudningar  *(auth)*

### GET /api/members  → 200 `[{ id, email, role, status }]`  (endast egen tenant)

### POST /api/invitations  *(Owner/Admin)*
```jsonc
{ "email":"kollega@acme.se", "role":"member" }  // → 201 { id, email, role, status:"pending" }
```
Fel: 403 (Member), 409 (seat-gräns nådd → "uppgradera till Pro"), 409 (redan medlem/inbjuden).

### POST /api/invitations/{token}/accept  *(publikt)*
Skapar konto för den inbjudna. → 201 `{ accessToken, refreshToken, user }`. Fel: 410 (utgången).

### DELETE /api/invitations/{id}  *(Owner/Admin)* → 204

### PUT /api/members/{id}/role  *(Owner/Admin; Owner-roll endast av Owner)*
```jsonc
{ "role":"admin" }  // → 200 { id, role }
```
Fel: 403 (Member, eller Admin som försöker sätta/ta Owner), 409 (skulle lämna org utan Owner).

### DELETE /api/members/{id}  *(Owner/Admin)* → 204. Fel: 409 (sista Owner).

---

## Billing / plan  *(Owner)*

### GET /api/billing  *(auth, Owner)*
→ 200 `{ plan, subscriptionStatus, seatLimit }`

### POST /api/billing/checkout  *(Owner)*
Skapar Stripe Checkout Session (testläge) för Pro.
```jsonc
{ "returnUrl":"https://app/billing" }  // → 200 { checkoutUrl }
```
Fel: 403 (ej Owner).

### POST /api/billing/webhook  *(publikt, signaturverifierat)*
Tar emot Stripe-events. Verifierar `Stripe-Signature`. Idempotent på `event.id`.
Hanterar `checkout.session.completed`, `customer.subscription.updated|deleted` →
uppdaterar `organization.plan`/`subscriptionStatus`. → 200 (alltid 2xx vid känt/dubblett),
400 vid felaktig signatur. **Ingen** tenant-auth (verifieras via signatur, mappas via customerId).

---

## DTO-noteringar
- Alla list-/hämtningssvar är redan tenant-filtrerade serverside; klienten skickar aldrig tenantId.
- `role` ∈ `owner|admin|member`. `plan` ∈ `free|pro`.
- Tider i ISO-8601 UTC. Belopp/valuta är inte del av detta skelett (fakturadomän = 002).
