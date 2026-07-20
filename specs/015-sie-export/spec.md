# Feature Specification: SIE4-export för bokföring

**Feature Branch**: `feature/015-sie-export` · **Created**: 2026-07-20 · **Status**: Draft

## Översikt

Exportera ett räkenskapsårs fakturor som en **SIE4-fil** (Sveriges standardformat för
bokföringsdata) så att en revisor/redovisningsbyrå kan importera dem i Fortnox, Visma eller
liknande. Varje skickad faktura/kreditfaktura blir en verifikation som bokförs mot en enkel,
inbyggd BAS-liknande kontoplan (kundfordringar/försäljning per momssats/utgående moms).

## User Stories
### US1 — Ladda ner SIE4-fil för ett år (P1)
Owner/Admin väljer ett räkenskapsår på en ny sida ("Export") och laddar ner en `.se`-fil med
alla det årets skickade fakturor och kreditfakturor som verifikationer, importerbar i extern
bokföringsprogramvara.

### US2 — Verifikationerna balanserar (P1)
Varje verifikation bokför bruttot mot kundfordringar (1510) och nettot/momsen mot
försäljnings-/momskonton per sats — alltid summa noll. Kreditfakturor bokförs med omvänt
tecken (raderna är redan negerade i domänen).

## Requirements
- **FR-001**: `SieExporter` (ren domänklass) genererar SIE4-text för ett givet räkenskapsår:
  `#FLAGGA`/`#PROGRAM`/`#FORMAT`/`#GEN`/`#SIETYP`/`#FNAMN`(+`#ORGNR` om profilen har ett)/`#RAR`,
  `#KONTO`-deklarationer för endast de konton som faktiskt används, samt `#VER`/`#TRANS`-block —
  ett per faktura (serie "F", `#VER`-nummer = fakturanumret).
- **FR-002**: Kontoplan (inbyggd, ej konfigurerbar i denna version): 1510 Kundfordringar,
  3001/3002/3003/3004 Försäljning 25/12/6/0 %, 2611/2621/2631 Utgående moms 25/12/6 %.
- **FR-003**: Endast fakturor med `InvoiceDate.Year == år` och status ≠ Draft ingår. Belopp per
  rad grupperas per momssats direkt från fakturans rader (inte återhärlett ur totalerna) så
  krediteringars negerade belopp automatiskt ger korrekt motbokning.
- **FR-004**: `GET /api/export/sie?year={år}` — kräver Owner/Admin (403 för Member); filen
  levereras som `application/octet-stream`, `.se`-filändelse, ISO-8859-1-kodad (SIE-standard).
- **FR-005**: Frontend: enkel Export-sida med årsväljare och nedladdningsknapp, länkad från
  Inställningar.

## Success Criteria
- **SC-001**: Varje verifikation balanserar exakt (summan av `#TRANS`-beloppen är noll) —
  testat för fakturor med flera momssatser och för kreditfakturor (domäntest).
- **SC-002**: Endast det valda årets fakturor ingår; utkast exkluderas (domäntest).
- **SC-003**: Endpointen kräver Owner/Admin (403 för Member) och är tenant-isolerad (API-test).
- **SC-004**: Filen är giltig text i angiven kodning och innehåller `#SIETYP 4` (API-test).

## Out of Scope
Ingående/utgående balanser (`#IB`/`#UB`), betalningar/bankavstämning i filen, redigerbar
kontoplan, SRU-koder, andra SIE-typer (1–3), fakturor grupperade på annat än kalenderår.
