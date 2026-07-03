# Tasks: SaaS-skelett (001)

**Input**: Design från `specs/001-saas-skelett/` (plan.md, spec.md, research.md, data-model.md, contracts/rest-api.md)
**Tests**: Ingår — constitution III kräver TDD för domänlogik och integrationstester för isolering/auth.
**Organisation**: grupperat per user story (US1–US5). Domäntester skrivs **före** implementation (Red→Green→Refactor).

Format: `[ID] [P?] [Story] Beskrivning` · **[P]** = kan köras parallellt (olika filer).

---

## Phase 1: Setup (delad grund)

- [x] T001 Skapa `backend/Faktura.slnx` + projekt `Faktura.Api`, `Faktura.Domain`, `Faktura.Infrastructure` (src/) och `Faktura.Domain.Tests`, `Faktura.Api.Tests` (tests/) med projektreferenser per plan.md
- [x] T002 [P] Lägg NuGet-beroenden: Api (JwtBearer), Infrastructure (MongoDB.Driver, Stripe.net, System.IdentityModel.Tokens.Jwt), Tests (xUnit, Mvc.Testing). (xUnit-asserts i st. f. FluentAssertions pga licens; egen PBKDF2 i st. f. Identity)
- [ ] T003 [P] Scaffolda `frontend/` (Vite + React 19 + TS + react-router-dom + @tanstack/react-query), eslint, vitest; `theme/`-tokens
- [ ] T004 [P] `backend/Dockerfile` (för Render) + `.env.example`/`appsettings.example.json` (utan hemligheter)
- [x] T005 Verifiera att `ci.yml` blir grön (backend byggs/testas; detektering via `find` för `.slnx`)

---

## Phase 2: Foundational (BLOCKERAR alla user stories)

- [x] T006 [P] [Domain] Entiteter: `Organization`, `User`, `UserRole` (owner/admin/member), `RefreshTokenRecord`, plan-värdetyper i `Faktura.Domain`. (`Invitation` = US3)
- [x] T007 [P] [Domain] Abstraktioner: `ITenantContext`, `IClock`, `IIdGenerator`, repository-interfaces (`IOrganizationRepository`, `IUserRepository`, `IRefreshTokenRepository`), `IPasswordHasher`, `ITokenService`, `IPlanCatalog`, `Result`/`Error`. (`IInvitationRepository` = US3)
- [x] T008 [Infra] `MongoContext` (collections + unika/TTL-index) + DI; konfiguration `Mongo__*`
- [~] T009 [Infra] Tenant-filter tvingas i repo-metoderna (`GetByIdAsync(tenantId, …)`); generisk `TenantScopedRepository<T>`-bas färdigställs i US2 när fler collections finns
- [x] T010 [P] [Infra] `JwtTokenService` (access-JWT med claims sub/tenantId/role/email; refresh-token-hash) + `Pbkdf2PasswordHasher`
- [x] T011 [Api] `Program.cs`: JwtBearer-auth (via IOptions), authorization, CORS, problem+json (RFC7807), DI-wiring
- [x] T012 [Api] `HttpTenantContext`: härleder tenant/roll **enbart** från JWT-claim (aldrig request-body)
- [x] T013 [P] [Infra] Datadriven plan-config (`PlanOptions`/`PlanCatalog`, Free 2 / Pro 25) — FR-019

**Checkpoint**: grund klar — user stories kan börja.

---

## Phase 3: US1 — Registrera organisation + login (P1) 🎯 MVP

**Tester först:**
- [x] T014 [P] [US1] Domäntester: org+owner skapas korrekt, lösenordspolicy, e-postnormalisering, owner-roll (17 tester gröna)
- [x] T015 [P] [US1] Integrationstester: `register` (201, 409 utan läckage, 422 svagt lösenord), `login` (200/401), `refresh` (rotation + återanvändning nekas), `/api/me` (401 utan token, 200 med) — 8 tester gröna

**Implementation:**
- [x] T016 [US1] Domän: `AccountRegistration` (skapa org+owner), `PasswordPolicy`, `EmailAddress`, login-verifiering — T014 grön
- [x] T017 [US1] Infra: `MongoOrganizationRepository`, `MongoUserRepository`, `MongoRefreshTokenRepository`
- [x] T018 [US1] Api: `AuthService` + endpoints `register`/`login`/`refresh`/`logout`/`me` per kontrakt — T015 grön
- [x] T019 [US1] Säkerhet: `InMemoryLoginThrottle` (FR-023, broms → 429 + `Retry-After`) + säkerhetsloggning av register/login (FR-024)

**Checkpoint ✅ (US1 komplett)**: registrera/logga in/förnya/`me` + login-broms. `dotnet test` = 26 gröna (17 domän + 9 API).

---

## Phase 4: US2 — Tenant-isolering (P1)

**Tester först:**
- [x] T020 [P] [US2] Integrationstester: A/B — `/api/members` visar bara egen data; cross-tenant rolländring → 404; `/api/members` utan token → 401 (SC-002)
- [x] T021 [P] [US2] `TenantScopedRepository<TDoc>`-bas tvingar tenant-filter i find/list/replace/count

**Implementation:**
- [x] T022 [US2] Tenant-ägda collections (invitations) via `TenantScopedRepository`; user-repo tenant-scopade metoder; inga endpoints tar tenantId från klient (härleds ur JWT)
- [x] T023 [US2] Cross-tenant-negativtester i `MembersEndpointsTests`

**Checkpoint ✅**: 0 cross-tenant-läckor verifierat i test.

---

## Phase 5: US3 — Medlemmar, inbjudningar, roller, seat-gräns (P2)

**Tester först:**
- [x] T024 [P] [US3] Domäntester: `MembershipRules` (Member nekas; Admin kan ej sätta/ta Owner; endast Owner), "minst en Owner" (FR-013), seat-gräns (FR-025), `Invitation`-livscykel
- [x] T025 [P] [US3] Integrationstester: invite→accept, Member kan ej bjuda in (403), seat-gräns → 409, sista-owner → 409

**Implementation:**
- [x] T026 [P] [US3] Domän: `MembershipRules` + `Invitation` — T024 grön
- [x] T027 [US3] Infra: `MongoInvitationRepository` (via TenantScopedRepository), token-hash, user-repo-utökning
- [x] T028 [US3] Api: `MemberService` + `members`/`invitations`-endpoints + RBAC per kontrakt — T025 grön

**Checkpoint ✅**: team + roller + seat-gräns fungerar. (Member-borttagning = liten uppföljning.)

---

## Phase 6: US4 — Plan/Stripe (testläge) (P2)

**Tester först:**
- [x] T029 [P] [US4] Domäntester: `Organization.ActivatePro`/`CancelToFree` (plan/status/seat/subscription)
- [x] T030 [P] [US4] Integrationstester: signatur ogiltig → 400, idempotens (re-levererat evt återaktiverar ej), aktivera → Pro, avsluta → Free, non-owner → 403 (SC-004/SC-006)

**Implementation:**
- [x] T031 [US4] Infra: `StripeBillingGateway` (Checkout Session) + `StripeWebhookEventParser` (EventUtility-signatur) + `MongoProcessedEventStore` (unik event-id)
- [x] T032 [US4] Domän/tjänst: `BillingService` + plan-övergångar mot plan-config — T029 grön
- [x] T033 [US4] Api: `billing` (GET, checkout) + `billing/webhook` (rå body + signatur) per kontrakt — T030 grön

**Checkpoint ✅**: Owner kan starta Pro-checkout (testläge); status driven av verifierade, idempotenta webhooks.

---

## Phase 7: US5 — Rate limiting per tenant (P3)

**Tester först:**
- [x] T034 [P] [US5] Integrationstest: A över kvot → 429 + `Retry-After`; B (egen partition) opåverkad (SC-005)

**Implementation:**
- [x] T035 [US5] Api: `AddRateLimiter` partitionerad på `tenantId` (anonymt = no-limit; login-broms täcker auth), kvot ur plan-config (`plan`-claim i JWT); 429 + `Retry-After` — T034 grön

**Checkpoint ✅**: rättvis per-tenant-begränsning.

---

## Phase 8: Polish & frontend-koppling

- [ ] T036 [P] Frontend: AuthContext + token-lagring + skyddade routes; sidor Signup/Login/Members/Billing mot kontraktet (TanStack Query)
- [ ] T037 [P] Frontend-tester (Vitest): auth-flöde + skyddad route
- [ ] T038 Lokal security review (OWASP, constitution V) + code review (II) före PR
- [ ] T039 Kör `quickstart.md`-röktest end-to-end
- [ ] T040 Uppdatera README/DEPLOYMENT + sätt deploy-secrets (Cloudflare/Render/Atlas/Stripe) — öppna PR mot `develop` när allt är grönt

---

## Dependencies & ordning

- Setup (P1) → Foundational (P2, blockerar allt) → US1 → US2 → US3 → US4 → US5 → Polish.
- Inom varje story: **tester först (ska faila)** → domän → infra → endpoints.
- US1 är MVP: stanna och validera efter Phase 3.
- En PR per spec (constitution): öppnas först när hela 001 är klart och CI grön — ingen WIP-PR.

## MVP-strategi
1. Phase 1–2 (grund) → 2. Phase 3 (US1) → **STOPP & validera** → 3. US2 isolering → 4. resterande stories i prioritetsordning.
