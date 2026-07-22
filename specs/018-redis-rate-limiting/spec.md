# Feature Specification: Distribuerad rate limiting & inloggningsbroms (Redis)

**Feature Branch**: `feature/018-redis-rate-limiting` · **Created**: 2026-07-20 · **Status**: Draft

## Översikt

Betalar den dokumenterade skulden "in-memory rate limit/broms per instans": flyttar tenant-
rate limitern (API-kvot per plan) och inloggnings-/registreringsbromsen till Redis, så de
fungerar korrekt när appen körs som flera instanser (horisontell skalning) — i dag nollställs
båda så fort en instans startar om, och en angripare kan runda bromsen genom att träffa en
annan instans.

## User Stories
### US1 — Delad rate limit-kvot mellan instanser (P1)
En tenants API-kvot (fönster + antal anrop, satt av plan) är gemensam oavsett vilken
instans/pod som hanterar requesten. Kvoten nollställs inte vid omstart av en enskild instans.

### US2 — Delad inloggningsbroms mellan instanser (P1)
Broms för misslyckade inloggningar/registreringar/glömt-lösenord (per e-post) delas mellan
instanser: en låst nyckel förblir låst oavsett vilken instans som tar emot nästa försök.

## Requirements
- **FR-001**: `IRateLimitCounter.Increment(key, window)` (domänabstraktion) — ökar en räknare
  och sätter TTL vid första ökningen; returnerar det nya värdet. `RedisRateLimitCounter`
  (Infrastructure) implementerar den mot Redis (`INCR` + `EXPIRE` vid count==1).
- **FR-002**: `CounterFixedWindowRateLimiter : RateLimiter` (Api-lager, kopplas in i
  `PartitionedRateLimiter`) använder `IRateLimitCounter` i stället för den inbyggda
  in-memory `FixedWindowRateLimiter` — både för tenant-kvoten och kundportalens
  IP-partition (spec 013).
- **FR-003**: `RedisLoginThrottle` (Infrastructure) implementerar befintliga `ILoginThrottle`
  mot Redis: en räknarnyckel (TTL = fönstret) + en spärrnyckel (TTL = utestängningstiden,
  satt när tröskeln nås). Ersätter `InMemoryLoginThrottle` i produktions-DI; samma
  gränssnitt/semantik, ingen ändring i `AuthService`.
- **FR-004**: `docker-compose.yml` får en `redis`-tjänst; API:t pekas mot den via
  `Redis__ConnectionString`.
- **FR-005**: Testsviten (utan Docker) fortsätter köra mot in-memory-fejkade motsvarigheter
  (samma mönster som Mongo-repos); en Testcontainers-gated testklass (skippas utan Docker,
  körs i CI) verifierar de riktiga Redis-implementationerna direkt.

## Success Criteria
- **SC-001**: `RedisRateLimitCounter` ökar och TTL:ar korrekt mot en riktig Redis — verifierat
  med Testcontainers (räknaren nollställs efter fönstret, inte innan).
- **SC-002**: `RedisLoginThrottle` blockerar efter tröskeln och släpper igen efter
  utestängningstiden, mot en riktig Redis (Testcontainers).
- **SC-003**: Befintliga rate limiting-/broms-tester (API-nivå, in-memory-fejkade) fortsätter
  vara gröna oförändrade — beteendet mot HTTP-lagret är identiskt med tidigare.

## Out of Scope
Redis-baserad session-/refresh-token-lagring (dokumenterad separat skuld), sliding-window
(vs fixed-window) precision, Redis Cluster/Sentinel, hälsokontroll mot Redis i `/health`.
