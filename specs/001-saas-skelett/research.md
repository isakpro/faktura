# Research (Phase 0): SaaS-skelett

Tekniska beslut för 001. Format: **Beslut**, **Motiv**, **Alternativ (förkastat)**.

## 1. Auth: egen JWT med access + refresh

- **Beslut**: API:t utfärdar en kortlivad **access-JWT** (~15 min) med claims `sub`
  (userId), `tenantId`, `role`, `email`, `exp`, samt en långlivad **refresh-token**
  (opak, lagrad hashad i Mongo, ~30 dagar, roterande). Access-JWT signeras HS256 med en
  hemlighet i env (`Jwt__Signingkey`). Validering via
  `Microsoft.AspNetCore.Authentication.JwtBearer`.
- **Motiv**: Kortlivad access-token minimerar skada vid läckage; refresh ger bra UX utan
  ständig inloggning. `tenantId`+`role` som claims gör tenant-/RBAC-beslut till en O(1)-
  kontroll utan DB-slag per request (constitution V: server är auktoritet).
- **Alternativ (förkastat)**: Endast långlivad access-token (svårt att återkalla);
  serverside-sessions i Mongo per request (extra DB-last, sämre statelöshet); extern IdP
  (bortvalt i brief).

## 2. Lösenordshashning: ASP.NET Core `PasswordHasher<T>`

- **Beslut**: `PasswordHasher<User>` (PBKDF2, HMAC-SHA512, hög iterationsräkning) bakom ett
  eget `IPasswordHasher`-interface i Domain.
- **Motiv**: Inbyggt i .NET, väl underhållet, inga extra beroenden, uppfyller FR-003.
  Interface gör att vi kan byta till Argon2/BCrypt utan att röra domänen.
- **Alternativ (förkastat)**: `BCrypt.Net-Next` (fungerar, men extra paket utan tydlig
  vinst); egen krypto (aldrig).

## 3. Tenant-isolering: `ITenantContext` + `TenantScopedRepository`-bas

- **Beslut**: En `ITenantContext` (scoped) sätts av en auth-middleware **enbart** från
  JWT-claimen `tenantId`. Alla tenant-ägda repositories ärver en abstrakt
  `TenantScopedRepository<T>` som injicerar `tenantId` i **varje** filter (find/update/
  delete) och sätter `tenantId` vid insert. Inget API-skikt tar emot `tenantId` från
  klienten.
- **Motiv**: Centraliserar isoleringen till ett ställe (DRY, svårt att glömma per query),
  direkt testbart (constitution V, SC-002). En saknad tenant-filter blir omöjlig by design.
- **Alternativ (förkastat)**: Manuellt `tenantId`-filter i varje metod (lätt att glömma →
  läckage); databas-per-tenant (bortvalt i brief); Mongo-vyer per tenant (komplext).
- **Index**: sammansatta index med `tenantId` först på alla tenant-collections (se data-model).

## 4. Rate limiting: inbyggd middleware partitionerad på tenant

- **Beslut**: ASP.NET Core rate limiting med en **partition-nyckel = `tenantId`** (fallback
  IP för oautentiserade endpoints). Fixed/sliding window per plan: kvoten slås upp från
  plan-konfiguration. Vid överskridning svaras `429` med `Retry-After`.
- **Motiv**: Inbyggt, ingen extra infra; per-tenant-partition uppfyller FR-020–022 och
  hindrar att en tenant svälter ut andra. Datadriven kvot uppfyller FR-019.
- **Alternativ (förkastat)**: Endast IP-baserat (delas av en tenants alla användare, fel
  granularitet); extern gateway/Redis-limiter (overkill för v1, kan införas senare för
  fler-instans-konsistens — noteras som känd begränsning).
- **Känd begränsning**: in-memory-limiter är per instans. På en enda Render-instans ok;
  vid horisontell skalning krävs distribuerad limiter (framtida spec).

## 5. Billing: `Stripe.net`, Checkout + signaturverifierade webhooks

- **Beslut**: Pro tecknas via **Stripe Checkout Session** (testläge). Plan-status drivs av
  **webhooks** (`checkout.session.completed`, `customer.subscription.updated`,
  `customer.subscription.deleted`) som signaturverifieras med `Stripe-Signature` +
  `Stripe__WebhookSecret`. Varje webhook-event lagras med dess `event.id` för **idempotens**.
  Stripe `customerId`/`subscriptionId` sparas på organisationen.
- **Motiv**: Checkout sköter kortdata/PCI; webhooks är sanningskällan för status (FR-016–018).
  Signaturverifiering + event-id-logg uppfyller FR-017 och SC-006.
- **Alternativ (förkastat)**: Stripe Elements/egen betalform (mer PCI-ansvar); polla Stripe-
  API i stället för webhooks (latens, race conditions).

## 6. Teststrategi

- **Beslut**:
  - **Domän (enhetstester, TDD-kärna)**: RBAC-regler, seat-gräns, "minst en Owner",
    plan-gating, token-claim-byggande, status-/refresh-regler — rena, snabba, inga I/O.
  - **API (integrationstester)**: `WebApplicationFactory` + Mongo via Testcontainers/
    `Mongo2Go`. Täcker: registrering/inloggning, **cross-tenant-isolering** (A når aldrig
    B), 401/403 per roll, 429 vid kvot, webhook-signatur + idempotens.
  - **Frontend**: Vitest + Testing Library för auth-flöde och skyddade routes.
- **Motiv**: Matchar constitution III; isolering och behörighet bevisas, inte antas.
- **Alternativ (förkastat)**: Endast enhetstester med mockad DB (missar verkliga Mongo-
  filter-buggar som är just där isoleringen kan brista).

## 7. Felformat

- **Beslut**: `application/problem+json` (RFC 7807) för alla fel; `401` utan giltig token,
  `403` vid otillräcklig roll/tenant-miss, `409` vid e-postkonflikt/seat-gräns alt. `422`,
  `429` + `Retry-After` vid kvot. Felmeddelanden läcker inte kontoexistens (FR-002).
- **Motiv**: Standardiserat, lätt för frontend att hantera enhetligt (constitution IV).
