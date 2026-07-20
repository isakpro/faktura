# Feature Specification: Realtidsuppdateringar (SignalR)

**Feature Branch**: `feature/017-signalr-realtid` · **Created**: 2026-07-20 · **Status**: Draft

## Översikt

Push:a organisationens aktivitet live till alla inloggade klienter i samma tenant, så att
Aktivitet-kortet och Översikten uppdateras direkt när en kollega skickar/betalar en faktura —
utan att användaren behöver ladda om sidan.

## User Stories
### US1 — Live aktivitetsflöde (P1)
När en användare i samma organisation gör en muterande åtgärd (skickar faktura, lägger till
kund, bjuder in medlem, …) ser andra inloggade användare i samma organisation Aktivitet-kortet
och nyckeltalen uppdateras inom någon sekund, utan manuell omladdning.

### US2 — Tenant-isolerad kanal (P1)
Realtidshändelser läcker aldrig mellan organisationer — varje uppkoppling ansluter bara till sin
egen tenants kanal, härledd ur JWT-claims (aldrig ett klient-skickat tenant-id).

## Requirements
- **FR-001**: `ActivityHub` (SignalR) på `/hubs/activity`, kräver autentisering. Vid anslutning
  läggs uppkopplingen i gruppen `tenant:{tenantId}` härledd ur den autentiserade principalens
  claims (samma mönster som `ITenantContext`).
- **FR-002**: JWT-bearer-autentisering accepterar access-token via query-strängen
  (`?access_token=`) specifikt för hub-vägen, eftersom webbläsarens WebSocket-API inte kan sätta
  en Authorization-header.
- **FR-003**: `AuditMiddleware` (spec 008) sänder samma händelse den redan loggar till tenantens
  grupp direkt efter lyckad loggning — återanvänder befintlig instrumentering i stället för att
  varje tjänst behöver känna till realtid explicit. Ett fel i sändningen påverkar aldrig svaret
  (samma feltolerans som befintlig audit-loggning).
- **FR-004**: Frontend ansluter automatiskt när användaren är inloggad (kopplar ner vid utloggning)
  och invaliderar `["audit"]`/`["dashboard"]`-frågorna vid mottagen händelse, så TanStack Query
  hämtar färsk data.

## Success Criteria
- **SC-001**: En händelse skickad av användare A i tenant X når endast andra uppkopplingar i
  tenant X, aldrig tenant Y (API-test med två parallella hub-anslutningar).
- **SC-002**: Anslutning utan giltig token nekas (401/anslutningsfel).
- **SC-003**: En muterande request (t.ex. skapa kund) resulterar i ett mottaget hub-meddelande
  med samma innehåll som audit-loggens post.

## Out of Scope
Presence/"vem är online", riktade notiser till enskild användare, realtid för det publika
kundportal-/API-ytorna, offline-köad leverans till frånkopplade klienter.
