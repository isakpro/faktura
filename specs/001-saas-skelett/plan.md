# Implementation Plan: SaaS-skelett

**Branch**: `feature/001-saas-skelett` | **Date**: 2026-06-28 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `specs/001-saas-skelett/spec.md`

## Summary

Bygg det multi-tenanta SaaS-skelettet: self-service-registrering av organisation +
Owner, egen JWT-auth (e-post/lösenord), roller Owner/Admin/Member med serverside-RBAC,
**tenant-isolering tvingad i datalagret** (delad MongoDB + `tenantId`), plan Free/Pro via
Stripe (testläge) med webhook-driven status, seat-gräns och **rate limiting per
organisation**. Stacken är .NET 10-API (clean architecture) + React/Vite, enligt
SPEC-BRIEF och constitution. Fakturadomänen ligger i spec 002.

## Technical Context

**Language/Version**: C# / .NET 10 (backend), TypeScript 5 / React 19 (frontend)
**Primary Dependencies**:
- Backend: ASP.NET Core Web API, `MongoDB.Driver`, `Microsoft.AspNetCore.Authentication.JwtBearer`,
  inbyggd rate limiting (`Microsoft.AspNetCore.RateLimiting` / `System.Threading.RateLimiting`),
  `Stripe.net`, lösenordshashning via ASP.NET Core `PasswordHasher<T>` (PBKDF2) eller `BCrypt.Net-Next`.
- Frontend: Vite, `@tanstack/react-query`, `react-router-dom`.
**Storage**: MongoDB (delad databas, `tenantId`-diskriminator på varje tenant-ägd collection)
**Testing**: xUnit + FluentAssertions; integrationstester mot Mongo via Testcontainers
(eller `Mongo2Go`) + `WebApplicationFactory`. Frontend: Vitest + Testing Library.
**Target Platform**: Linux-container (Render) för API, statisk hosting (Cloudflare Pages) för web, MongoDB Atlas
**Project Type**: Web application (backend + frontend)
**Performance Goals**: p95 < 300 ms för API-endpoints under normal last (hobby/starter-skala)
**Constraints**: tenant-isolering i datalagret (ingen query utan tenant-filter), hemligheter
via miljövariabler, Stripe endast testläge, ingen e-postleverantör krävs i v1
**Scale/Scope**: liten initialt (tiotals tenants); designen ska inte hindra horisontell skalning av API:t

## Constitution Check

*GATE: Måste passera före Phase 0. Omprövas efter Phase 1.*

| Princip | Hur planen uppfyller den |
|---|---|
| I. Spec-driven | Planen härleds ur spec 001 + Clarifications; ingen funktion utanför specen. |
| II. Clean Code & SOLID | Tre lager (Api/Domain/Infrastructure). Domänlogik (RBAC, seat-/plan-regler, isoleringskontrakt) i `Domain`, fri från Mongo/HTTP/Stripe. Beroenden via interfaces. |
| III. TDD för domänlogik | Domänregler (rollbehörighet, seat-gräns, plan-gating, "minst en Owner", token-claims) skrivs test-först i `Faktura.Domain.Tests`. Isolering + 401/403/429 + webhook-idempotens täcks av integrationstester i `Faktura.Api.Tests`. |
| IV. API-kontrakt först | REST-kontraktet definieras i [contracts/rest-api.md](contracts/rest-api.md) innan implementation; DTO:er och felformat (inkl. 401/403/429 + `Retry-After`) låsta. Frontend och backend följer samma kontrakt. |
| V. Multi-tenant isolering & säkerhet | `ITenantContext` härleds **enbart** ur JWT. Alla repositories går via en `TenantScopedRepository`-bas som tvingar `tenantId`-filter; klientangivet tenantId ignoreras. Webhooks signaturverifieras. Hemligheter i env. |
| VI. CI/CD & grön pipeline | Befintlig `ci.yml` blir skarp när `backend/` + `frontend/` landar (build+test+lint). PR mot `develop` öppnas först när 001 är helt klart och grönt. |

**Resultat:** PASS — inga avvikelser. Complexity Tracking ej tillämpligt.

## Project Structure

### Documentation (this feature)

```text
specs/001-saas-skelett/
├── plan.md              # Denna fil
├── research.md          # Phase 0 — tekniska beslut
├── data-model.md        # Phase 1 — entiteter, collections, index
├── quickstart.md        # Phase 1 — kör lokalt
├── contracts/
│   └── rest-api.md      # Phase 1 — REST-kontrakt (auth, tenant, members, billing)
├── checklists/
│   └── requirements.md  # Spec-kvalitetschecklista
└── tasks.md             # Phase 2 — skapas av /speckit-tasks
```

### Source Code (repository root)

```text
backend/
├── Faktura.sln
├── Dockerfile                         # för Render
├── src/
│   ├── Faktura.Api/                    # ASP.NET Core Web API (composition root)
│   │   ├── Endpoints/                  # auth, organizations, members, billing
│   │   ├── Middleware/                 # tenant-resolution, fel→problem+details
│   │   ├── Auth/                       # JWT-konfig, policies, roller
│   │   ├── RateLimiting/               # per-tenant partitionering
│   │   └── Program.cs
│   ├── Faktura.Domain/                 # rena entiteter + domänlogik + interfaces
│   │   ├── Organizations/              # Organization, Plan, Subscription
│   │   ├── Users/                      # User, Role, Invitation
│   │   ├── Authorization/              # behörighetsregler (RBAC), seat-/plan-regler
│   │   ├── Abstractions/               # ITenantContext, repository-interfaces, IClock
│   │   └── Common/                     # Result/Error-typer, value objects
│   └── Faktura.Infrastructure/         # Mongo, JWT, hashing, Stripe
│       ├── Persistence/                # MongoContext, TenantScopedRepository, repos
│       ├── Security/                   # JwtTokenService, PasswordHasher
│       └── Billing/                    # StripeClient-wrapper, webhook-verifiering
└── tests/
    ├── Faktura.Domain.Tests/           # enhetstester (TDD-kärna)
    └── Faktura.Api.Tests/              # integrationstester (WebApplicationFactory + Mongo)

frontend/
├── package.json
├── src/
│   ├── api/                            # fetch-klient, react-query-hooks, typer från kontraktet
│   ├── auth/                           # token-lagring, AuthContext, skyddade routes
│   ├── pages/                          # Signup, Login, Members, Billing
│   ├── components/
│   └── theme/                          # design-tokens (ingen hårdkodad styling)
└── tests/                              # Vitest + Testing Library
```

**Structure Decision**: Web application — backend (clean architecture i tre lager enligt
VMPage-mönstret) + frontend (Vite/React). Domänlogiken isoleras i `Faktura.Domain` för
TDD; infrastruktur (Mongo/JWT/Stripe) bakom interfaces i `Faktura.Infrastructure`; `Api`
är composition root med auth-, tenant- och rate-limit-middleware.

## Phases

- **Phase 0 — Research** ([research.md](research.md)): låser tekniska val (JWT-bibliotek &
  claims, lösenordshashning, Mongo-isoleringsmönster, rate limiting-partitionering, Stripe-
  webhookverifiering, teststrategi).
- **Phase 1 — Design**: [data-model.md](data-model.md) (collections + index + tenant-nyckel),
  [contracts/rest-api.md](contracts/rest-api.md) (endpoints + DTO + felkoder), [quickstart.md](quickstart.md).
- **Phase 2 — Tasks**: `/speckit-tasks` genererar `tasks.md` (TDD-ordning: domäntester →
  domän → infrastruktur → endpoints → frontend → integrationstester).

## Complexity Tracking

> Ej tillämpligt — Constitution Check passerar utan avvikelser.
