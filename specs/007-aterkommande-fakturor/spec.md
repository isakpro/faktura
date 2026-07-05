# Feature Specification: Återkommande fakturor

**Feature Branch**: `feature/007-aterkommande-fakturor` · **Created**: 2026-07-05 · **Status**: Draft

## Översikt

Abonnemangsmotorn: en organisation skapar en **återkommande faktura** (kund + rader +
intervall + startdatum, valfritt slutdatum). Ett dagligt jobb genererar fakturan varje period —
den **skapas, skickas (nummer + låsning) och mejlas** till kunden med PDF, helt automatiskt.
Mallen kan **pausas/återupptas**, och varje genererad faktura är spårbar till sin mall.

## Clarifications (session 2026-07-05)

- **Automation:** generera + skicka + mejla (kund utan e-post: fakturan skickas ändå; mejlet
  loggas som misslyckat i utskickshistoriken).
- **Intervall:** månads-, kvartals- eller årsvis från startdatumet (månadsslut klampas:
  31 jan + 1 mån = 28/29 feb). Förfallodatum följer kundens betalvillkor som vanligt.
- **Livscykel:** aktiv/pausad + valfritt slutdatum (ingen generering efter slutdatumet).
  **Ikappkörning:** har jobbet inte kört på ett tag genereras alla missade perioder.

## User Stories

### US1 — Hantera mallar (P1)
Skapa/redigera/pausa/återuppta en återkommande faktura (kund, rader, intervall, start-/slutdatum).
Validering som fakturautkast (rader, momssatser); tenant-isolerat; alla roller.

### US2 — Automatisk generering (P1)
**Given** en aktiv mall vars nästa körning passerats, **When** jobbet körs, **Then** skapas en
faktura från mallens rader som skickas (nästa nummer i serien, låst) och mejlas till kunden med
PDF; mallens nästa körning flyttas fram en period. Pausad/slutdaterad mall genererar inget.
Omkörning genererar inga dubbletter; missade perioder tas ikapp; fel för en mall stoppar inte jobbet.

### US3 — Spårbarhet (P2)
Varje genererad faktura bär en referens till sin mall; mallens genererade fakturor kan listas.

## Requirements (urval)

- **FR-001**: CRUD + pausa/återuppta för mallar (tenant-isolerat, alla roller); rad-/momsvalidering som 002.
- **FR-002**: Dagligt jobb genererar per aktiv, förfallen mall: faktura → skicka (atomisk
  nummerserie, kundögonblicksbild, betalvillkor) → mejla PDF (utskicket loggas i 003:s historik).
- **FR-003**: `nextRunDate` flyttas fram per intervall efter varje generering; ikappkörning
  genererar en faktura per missad period (skyddstak); inga dubbletter vid omkörning.
- **FR-004**: Pausad mall eller passerat slutdatum ⇒ ingen generering.
- **FR-005**: Saknad kundadress: fakturan skickas ändå; mejlet loggas som misslyckat.
- **FR-006**: Genererade fakturor refererar mallen och kan listas per mall.

## Success Criteria

- **SC-001**: Jobbet ger exakt en faktura per förfallen period (test: ikapp 3 månader ⇒ 3 fakturor
  med löpande nummer; omkörning ⇒ 0 nya).
- **SC-002**: Genererad faktura är skickad + låst + mejlad (fejk-sändaren fångar PDF) (test).
- **SC-003**: Paus/slutdatum respekteras; fel för en mall stoppar inte övriga (test).
- **SC-004**: 0 cross-tenant-läckage (test).

## Out of Scope
Prorata/delperioder, prisindexering, kalenderstyrda datum (t.ex. "sista vardagen"), e-postmallar.
