# Feature Specification: Betalningspåminnelser

**Feature Branch**: `feature/004-betalningspaminnelser`
**Created**: 2026-07-04
**Status**: Draft
**Input**: User description: "Påminnelse-mejl för förfallna fakturor — manuellt från fakturalistan och automatiskt X dagar efter förfall, med påminnelsehistorik."

## Översikt

Bygger på fakturadomänen (002, förfallostatus) och e-postmotorn (003, SMTP + PDF-bilaga): när en
faktura är **förfallen** kan en **betalningspåminnelse** mejlas till kunden — **manuellt** via en
knapp, eller **automatiskt** via ett dagligt jobb som skickar X dagar efter förfallodatum (styrs
per organisation och kan slås av/på). Påminnelsen är ett mejl med vänlig uppmaning och original-
fakturans PDF bifogad — **inga nya belopp/avgifter** i v1. Varje påminnelse **loggas** och kan
**upprepas** (historiken räknar). Allt tenant-isolerat med RBAC enligt 001.

## Clarifications

### Session 2026-07-04

- **Trigger:** Både **manuell** ("Skicka påminnelse"-knapp på förfallen faktura) och **automatisk**
  (dagligt jobb som skickar när fakturan varit förfallen ≥ X dagar). Automatiken är en
  **inställning per organisation**: på/av + antal dagar efter förfall.
- **Eskalering:** En **enkel, upprepningsbar** påminnelsetyp (ingen nivåtrappa i v1). Historiken
  visar varje påminnelse; mejlet anger vilken påminnelse i ordningen det är.
- **Avgift:** **Ingen påminnelseavgift** i v1 — påminnelsen ändrar inga belopp; original-PDF bifogas.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Skicka påminnelse manuellt (Priority: P1)

En användare ser en förfallen faktura och skickar en betalningspåminnelse till kunden med ett
klick. Kunden får ett mejl med uppmaning och fakturan bifogad.

**Why this priority**: Kärnvärdet — driva in betalning. Manuell väg är minsta levererbara skivan
och återanvänder e-postmotorn.

**Independent Test**: Gör en faktura förfallen, skicka påminnelse, och verifiera (via fejkad
sändare) att ett mejl med påminnelsetext + PDF gick till kundens adress och att påminnelsen loggades.

**Acceptance Scenarios**:

1. **Given** en förfallen faktura vars kund har e-post, **When** användaren skickar en påminnelse,
   **Then** mejlas kunden med påminnelsetext (fakturanummer, förfallodatum, belopp) och fakturans
   PDF, och påminnelsen loggas.
2. **Given** en faktura som inte är förfallen (utkast, ej förfallen, betald eller krediterad),
   **When** användaren försöker skicka påminnelse, **Then** nekas det med tydligt fel.
3. **Given** en redan påmind faktura, **When** användaren skickar igen, **Then** skickas en ny
   påminnelse och mejlet anger att det är påminnelse nr 2 (osv.).
4. **Given** en kund utan e-post, **When** påminnelse skickas utan angiven adress, **Then** nekas
   det (mottagare saknas); en överstyrd adress kan anges precis som i 003.

---

### User Story 2 - Automatiska påminnelser (Priority: P2)

En organisation slår på automatiska påminnelser och anger antal dagar efter förfall. Ett dagligt
jobb skickar då påminnelser till alla fakturor som passerat gränsen och inte redan påmints
automatiskt.

**Why this priority**: Automatiken är bekvämlighetens kärna men bygger på US1:s utskick.

**Independent Test**: Slå på automatik (X dagar), låt en faktura vara förfallen ≥ X dagar, kör
jobbet och verifiera att exakt en påminnelse skickas och loggas; kör jobbet igen och verifiera att
ingen dubblett skickas.

**Acceptance Scenarios**:

1. **Given** automatik på (X dagar) och en obetald faktura förfallen i ≥ X dagar, **When** jobbet
   körs, **Then** skickas en påminnelse till kundens e-post och loggas som automatisk.
2. **Given** jobbet redan påmint en faktura automatiskt, **When** jobbet körs igen, **Then** skickas
   ingen ny automatisk påminnelse för samma faktura (ingen dubblett/spam).
3. **Given** automatik av, **When** jobbet körs, **Then** skickas inget för den organisationen.
4. **Given** en faktura som betalats efter förfall, **When** jobbet körs, **Then** skickas ingen
   påminnelse för den.
5. **Given** en kund utan e-post, **When** jobbet når fakturan, **Then** hoppas den över och loggas
   som misslyckad/överhoppad utan att jobbet stannar.

---

### User Story 3 - Påminnelseinställningar och historik (Priority: P2)

En Owner/Admin ställer in automatiken (på/av, dagar efter förfall). Alla användare ser per faktura
hur många påminnelser som skickats, när och med vilket resultat.

**Why this priority**: Styrning och spårbarhet; bygger på US1/US2.

**Independent Test**: Ändra inställningen och verifiera att jobbet respekterar den; skicka
påminnelser och verifiera att historiken listar dem med typ (manuell/automatisk), tidpunkt och status.

**Acceptance Scenarios**:

1. **Given** en Owner/Admin, **When** hen ändrar påminnelseinställningen, **Then** sparas den per
   organisation och styr det automatiska jobbet.
2. **Given** en Member, **When** den försöker ändra inställningen, **Then** nekas det (403);
   Member får dock skicka manuella påminnelser.
3. **Given** en faktura med skickade påminnelser, **When** historiken visas, **Then** listas varje
   påminnelse med typ (manuell/automatisk), mottagare, tidpunkt och status.

### Edge Cases

- Fakturan betalas samma dag som jobbet kör — jobbet får inte påminna betalda fakturor.
- Kreditfakturor förfaller inte — ska aldrig påminnas.
- SMTP-fel under jobbet — den fakturan loggas som misslyckad, jobbet fortsätter med resten.
- Organisation utan inställning — automatik av som standard (opt-in).
- Manuell påminnelse på icke-förfallen men skickad faktura — nekas (endast förfallna) i v1.
- Flera instanser av jobbet samtidigt — dubblettskydd via "redan automatiskt påmind"-kontrollen.

## Requirements *(mandatory)*

### Functional Requirements

**Manuell påminnelse**
- **FR-001**: Systemet MÅSTE låta en inloggad användare skicka en betalningspåminnelse för en
  **förfallen** faktura (skickad, obetald, förfallodatum passerat).
- **FR-002**: Påminnelser för utkast, ej förfallna, betalda eller krediterade fakturor samt
  kreditfakturor MÅSTE nekas.
- **FR-003**: Påminnelsemejlet MÅSTE innehålla fakturanummer, förfallodatum och belopp, ha
  fakturans **PDF bifogad**, och ange **vilken påminnelse i ordningen** det är. Avsändarregler som
  i 003 (systemadress + org-namn, Reply-To = avsändaren; för automatiska utskick utan avsändare
  utelämnas Reply-To).
- **FR-004**: Mottagare = kundens e-post; en **överstyrd adress** MÅSTE kunna anges (som 003).
  Saknas giltig mottagare nekas utskicket.
- **FR-005**: Upprepade manuella påminnelser MÅSTE tillåtas; varje påminnelse loggas separat.

**Automatiska påminnelser**
- **FR-006**: Systemet MÅSTE kunna skicka påminnelser **automatiskt** via ett återkommande (minst
  dagligt) jobb för fakturor som varit förfallna ≥ organisationens konfigurerade antal dagar.
- **FR-007**: Automatiken MÅSTE vara en **inställning per organisation**: på/av (standard **av**)
  och dagar efter förfall (standard 7). Endast Owner/Admin får ändra den.
- **FR-008**: Jobbet får INTE skicka mer än **en automatisk** påminnelse per faktura (ingen
  dubblett vid omkörning); manuella påminnelser påverkas inte av detta.
- **FR-009**: Fel för en enskild faktura (t.ex. saknad e-post, SMTP-fel) MÅSTE loggas utan att
  jobbet avbryts för övriga.

**Historik & isolering**
- **FR-010**: Varje påminnelse MÅSTE loggas med typ (manuell/automatisk), mottagare, tidpunkt och
  status, och historiken MÅSTE kunna visas per faktura.
- **FR-011**: Påminnelser ändrar ALDRIG fakturans belopp, innehåll eller status (ingen avgift i v1).
- **FR-012**: Alla åtgärder, inställningar och historik MÅSTE vara tenant-isolerade och kräva
  inloggning (RBAC enligt 001).

### Key Entities *(include if feature involves data)*

- **Påminnelse (InvoiceReminder)**: logg-post — tillhör organisation + faktura. Typ
  (manuell/automatisk), mottagare, tidpunkt, status (lyckad/misslyckad), ordningsnummer.
- **Påminnelseinställning (ReminderSettings)**: per organisation — automatik på/av, dagar efter
  förfall.
- **Faktura (002)**: oförändrad; påminnelser lagras separat.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: En manuell påminnelse på en förfallen faktura producerar ett mejl med påminnelsetext
  + PDF till rätt mottagare och loggas — verifierat i test via fejkad sändare.
- **SC-002**: Påminnelse på icke-förfallna/betalda/krediterade fakturor nekas alltid (test).
- **SC-003**: Jobbet skickar exakt **en** automatisk påminnelse per kvalificerad faktura även vid
  upprepade körningar (0 dubbletter, test).
- **SC-004**: Jobbet respekterar per-organisationsinställningen (av ⇒ 0 utskick; X dagar ⇒ endast
  fakturor förfallna ≥ X dagar) — verifierat i test.
- **SC-005**: Ett fel för en faktura stoppar inte jobbet för övriga (test).
- **SC-006**: Ingen organisation kan påminna eller se historik för en annan organisations fakturor
  (0 läckor, test).

## Assumptions

- **Jobbmekanism:** in-process schemaläggning i API:t (minst daglig körning); ingen extern
  schemaläggare i v1. Detaljeras i plan.
- **Mejltext:** enkel svensk standardtext; anpassningsbara mallar utanför v1.
- **Automatiska utskick** har ingen personlig avsändare — Reply-To utelämnas (eller sätts till
  organisationens e-post om sådan finns i framtiden).
- **Bygger på:** 002 (förfallostatus, PDF) och 003 (`IEmailSender`, utskicksmönster, fejk i test).

## Out of Scope (v1 för denna spec)

- Påminnelseavgifter, dröjsmålsränta, inkasso-/slutkravsnivåer och eskaleringstrappor.
- Anpassningsbara mallar, flera språk, SMS-påminnelser.
- Extern schemaläggare/kö (Hangfire, Azure Functions etc.) — in-process räcker i v1.
- Bounce-/leveransspårning (som 003).
