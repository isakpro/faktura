# Feature Specification: Audit trail (aktivitetslogg)

**Feature Branch**: `feature/008-audit-trail` · **Created**: 2026-07-06 · **Status**: Draft

## Översikt

Spårbar aktivitetslogg per organisation: **vem gjorde vad när**. Varje autentiserad, muterande
API-åtgärd (skapa/ändra/skicka/betala/kreditera/mejla/roller/inställningar …) loggas automatiskt
med aktör, åtgärd och tidpunkt. Loggen är **oföränderlig** (endast tillägg), tenant-isolerad och
läsbar för **Owner/Admin** i appen (senaste händelserna på Översikt).

## Clarifications (2026-07-06)
- **Mekanism:** enhetlig middleware fångar alla autentiserade POST/PUT/DELETE mot API:t —
  ingen åtgärd kan "glömmas bort" i enskilda tjänster. Anonyma anrop (login/register/webhooks)
  loggas inte här (login-försök har redan säkerhetsloggning via Serilog).
- **Läsbehörighet:** Owner/Admin (Member nekas). **Ingen radering/redigering** — append-only.

## User Stories
### US1 — Automatisk loggning (P1)
**Given** en inloggad användare utför en muterande åtgärd, **When** anropet lyckas (eller nekas
med klientfel), **Then** loggas aktör (e-post), åtgärd (metod + resurs), statuskod och tidpunkt
i organisationens logg. Anonyma anrop loggas inte. **Isolering:** A:s logg syns aldrig hos B.

### US2 — Läsa loggen (P2)
Owner/Admin ser de senaste händelserna (aktör, åtgärd på svenska, tid, status) på Översikt;
Member får 403 på logg-API:t.

## Requirements
- **FR-001**: Autentiserade muterande anrop (POST/PUT/DELETE `/api/*`) loggas automatiskt med
  tenantId (ur JWT), aktörens e-post, metod, sökväg, statuskod, tidpunkt.
- **FR-002**: Loggen är append-only och tenant-isolerad; `GET /api/audit` (senaste 50) kräver
  Owner/Admin.
- **FR-003**: Loggning får inte påverka svaret — fel i loggningen sväljs (loggas via Serilog).

## Success Criteria
- **SC-001**: Fakturaskick ger en logg-post med rätt aktör/åtgärd (test).
- **SC-002**: Member → 403 på `/api/audit`; cross-tenant ser 0 poster (test).
- **SC-003**: Anonyma anrop (register/login) skapar inga poster (test).

## Out of Scope
Diff av fältändringar, export, retention-policy, läskvitton.
