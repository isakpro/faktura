# Feature Specification: Fakturaprofil & fakturadetaljvy

**Feature Branch**: `feature/009-fakturaprofil` · **Created**: 2026-07-06 · **Status**: Draft

## Översikt

Gör fakturan komplett som svenskt dokument och appen komplett som verktyg:
1. **Fakturaprofil** per organisation — organisationsnummer, adress, bankgiro/plusgiro och
   "Godkänd för F-skatt" — som visas på fakturans PDF (säljarblock + sidfot).
2. **Fakturadetaljvy** i appen — rader, summor per momssats och all historik (utskick,
   påminnelser) på ett ställe.

## User Stories
### US1 — Fakturaprofil (P1)
Owner/Admin fyller i profilen (alla fält valfria; F-skatt är en kryssruta). PDF:er för
fakturor/kreditfakturor visar säljarens uppgifter när de finns; utan profil renderas som idag
(bakåtkompatibelt). Tenant-isolerat; alla roller kan läsa profilen.

### US2 — Detaljvy (P2)
Fakturanumret/raden i listan länkar till `/invoices/{id}` som visar status-stämpel, kund,
rader (med enhet), netto/moms per sats/brutto samt utskicks- och påminnelsehistorik. PDF-knapp.

## Requirements
- **FR-001**: `GET /api/organization-profile` (alla inloggade) + `PUT` (Owner/Admin; Member 403).
- **FR-002**: PDF-generatorn tar säljarens organisation och renderar orgnr/adress/bankgiro/
  plusgiro/F-skatt när de finns; saknad profil ⇒ oförändrad rendering.
- **FR-003**: Detaljvyn visar fakturans fulla innehåll + historik via befintliga endpoints.

## Success Criteria
- **SC-001**: PUT kräver Owner/Admin (403 för Member); profilen är tenant-isolerad (test).
- **SC-002**: PDF genereras korrekt både med och utan profil (test).
- **SC-003**: Detaljvyn renderar rader/summor/historik (byggs på befintliga, redan testade API:er).

## Out of Scope
Logotyp-uppladdning, flera bankkonton, IBAN/BIC, momsregistreringsbevis.
