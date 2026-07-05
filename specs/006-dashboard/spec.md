# Feature Specification: Dashboard med nyckeltal

**Feature Branch**: `feature/006-dashboard` · **Created**: 2026-07-05 · **Status**: Draft
**Input**: "Översikten blir riktig: utestående/förfallet/betalt i år, omsättning per månad (graf), senaste fakturor."

## Översikt

Startsidan (Översikt) visar organisationens ekonomiska läge i realtid: tre nyckeltal
(**utestående**, **förfallet**, **betalt i år**), en **omsättningsgraf per månad** (senaste 12
månaderna) och de **senaste fakturorna** med status. Allt tenant-isolerat, läsbart för alla
roller. Ren läsvy — inga nya skrivflöden.

## Clarifications (session 2026-07-05)

- Scope enligt godkänd förhandsvisning: KPI:er utestående/förfallet/betalt i år + graf + senaste fakturor.
- **KPI-definitioner:** endast riktiga fakturor (kreditfakturor exkluderas ur summorna men syns i
  "senaste"). Utestående = summa brutto för skickade obetalda; Förfallet = delmängden vars
  förfallodatum passerat; Betalt i år = summa brutto betalda med betaldatum i innevarande år.
  Omsättning/månad = summa brutto per betalmånad, senaste 12 månaderna.

## User Stories

### US1 — Nyckeltal och senaste fakturor (P1)
**Given** en organisation med fakturor i olika status, **When** användaren öppnar Översikt,
**Then** visas korrekt utestående/förfallet/betalt i år (enligt definitionerna) och de senaste
fakturorna med nummer, kund, status och belopp. Tom organisation ⇒ nollor och tom lista.
**Isolering:** en annan organisations fakturor påverkar aldrig siffrorna (test).

### US2 — Omsättning per månad (P2)
**Given** betalda fakturor spridda över månader, **When** Översikt visas, **Then** visas en graf
med summa per månad för senaste 12 månaderna (tomma månader = 0), i Huvudboken-stilen.

## Requirements

- **FR-001**: `GET`-endpoint som returnerar nyckeltalen, månadsserien (12 punkter) och de senaste
  (≤5) fakturorna för den inloggades organisation.
- **FR-002**: Beräkningarna följer KPI-definitionerna ovan; kreditfakturor ingår ej i summorna.
- **FR-003**: Tenant-isolerat (härlett ur JWT); alla roller får läsa.
- **FR-004**: Frontenden visar nyckeltalskort, graf och senaste-listan på Översikt i Huvudboken-temat.

## Success Criteria

- **SC-001**: KPI:erna stämmer i test för en blandning av utkast/skickade/förfallna/betalda/
  krediterade fakturor (0 differens).
- **SC-002**: Månadsserien har alltid 12 punkter och summerar betalningar på rätt månad (test).
- **SC-003**: 0 cross-tenant-läckage i dashboarddata (test).

## Out of Scope
Filtrering/datumintervall, export, per-kund-statistik, kassaflödesprognoser.
