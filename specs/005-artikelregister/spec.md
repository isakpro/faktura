# Feature Specification: Artikelregister

**Feature Branch**: `feature/005-artikelregister`
**Created**: 2026-07-05
**Status**: Draft
**Input**: User description: "Artikel-/produktregister: sparade artiklar (namn, enhet, pris exkl. moms, momssats, valfritt artikelnummer) som kan väljas på fakturarader så att namn/pris/moms förifylls."

## Översikt

Bygger på fakturadomänen (002): organisationen underhåller ett **artikelregister** — sparade
artiklar med namn, valfritt **artikelnummer**, **enhet** (st/tim/kg …), á-pris exkl. moms och
momssats. När en fakturarad skapas kan användaren **välja en artikel** och få beskrivning,
enhet, pris och momssats **förifyllda**; raden kan därefter justeras fritt. Radens värden är en
**kopia** (snapshot) — senare prisändringar i registret påverkar aldrig befintliga fakturor.
Fakturarader får ett valfritt **enhetsfält** som visas på PDF:en. Fritextrader fungerar precis
som idag. Allt tenant-isolerat med RBAC enligt 001 (alla roller hanterar artiklar).

## Clarifications

### Session 2026-07-05

- **Enhet:** Ja i v1 — artikeln bär enhet som följer med till fakturaraden och visas på PDF:en.
  Befintliga rader saknar enhet (bakåtkompatibelt, fältet är valfritt).
- **Artikelnummer (SKU):** Valfritt; **unikt inom organisationen** när det anges.
- **Behörighet:** Alla roller (Owner/Admin/Member) får skapa/redigera/arkivera artiklar — samma
  mönster som kunder/fakturor.
- **Snapshot-princip:** Att välja en artikel **kopierar** värden till raden (ingen levande
  referens). Prisändringar i registret påverkar bara framtida rader.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Hantera artiklar (Priority: P1)

En användare lägger upp och underhåller organisationens artiklar (namn, valfritt artikelnummer,
enhet, á-pris exkl. moms, momssats) och kan arkivera artiklar som inte längre används.

**Why this priority**: Registret är grunden — utan artiklar finns inget att välja på raderna.

**Independent Test**: Skapa en artikel, lista/sök, redigera pris, arkivera — allt inom egen
organisation; en annan organisation ser den aldrig.

**Acceptance Scenarios**:

1. **Given** en inloggad användare, **When** hon skapar en artikel med namn (obligatoriskt),
   enhet, pris och momssats, **Then** sparas artikeln och syns i registret.
2. **Given** ett artikelnummer som redan används i organisationen, **When** en artikel skapas
   med samma nummer, **Then** nekas det med tydligt fel (unikt inom organisationen).
3. **Given** en artikel, **When** användaren redigerar pris/momssats, **Then** uppdateras
   registret — men **befintliga fakturarader påverkas inte**.
4. **Given** en artikel som inte längre säljs, **When** användaren arkiverar den, **Then**
   försvinner den ur väljaren men historiken/fakturor är opåverkade.
5. **Given** två organisationer A och B, **When** A listar artiklar, **Then** visas endast A:s.

---

### User Story 2 - Välj artikel på fakturarad (Priority: P1)

När en användare bygger ett fakturautkast kan hon välja en artikel och få radens beskrivning,
enhet, á-pris och momssats förifyllda, samt justera fritt efteråt (t.ex. antal eller rabatterat
pris). Fritextrader fungerar som förut.

**Why this priority**: Själva nyttan — snabbare och konsekventare fakturering.

**Independent Test**: Skapa utkast, välj en artikel på en rad → fälten förifylls; ändra antal
och pris → summorna räknas om enligt 002:s regler; blanda med en fritextrad.

**Acceptance Scenarios**:

1. **Given** ett utkast och en aktiv artikel, **When** användaren väljer artikeln på en rad,
   **Then** förifylls beskrivning, enhet, á-pris exkl. moms och momssats från artikeln.
2. **Given** en förifylld rad, **When** användaren ändrar antal/pris/beskrivning, **Then**
   gäller radens egna värden (snapshot) och summorna räknas om.
3. **Given** en faktura skickas, **When** artikelns pris senare ändras i registret, **Then**
   är den skickade fakturan oförändrad (oföränderlighet enligt 002).
4. **Given** en rad utan artikel (fritext), **When** utkastet sparas, **Then** fungerar det
   precis som idag.

---

### User Story 3 - Enhet på rad och PDF (Priority: P2)

Fakturarader kan bära en enhet (st, tim, kg …) som visas i radtabellen på fakturans PDF.

**Why this priority**: Gör fakturan komplett och professionell; litet, bakåtkompatibelt tillägg.

**Independent Test**: Skapa en rad med enhet "tim", skicka fakturan och verifiera att PDF:ens
radtabell visar enheten; en rad utan enhet renderas som idag.

**Acceptance Scenarios**:

1. **Given** en rad med enhet, **When** fakturan visas/PDF genereras, **Then** visas enheten
   vid antalet (t.ex. "10 tim").
2. **Given** en befintlig/fritext-rad utan enhet, **When** PDF genereras, **Then** renderas
   raden som idag (fältet är valfritt).

### Edge Cases

- Artikel arkiveras medan den är vald i ett öppet utkast — raden behåller sina kopierade värden.
- Artikelnummer-unikhet vid samtidiga skapanden — tvingas i datalagret.
- Tomt artikelnummer på flera artiklar — tillåtet (unikhet gäller endast angivna nummer).
- Negativt eller noll-pris på artikel — valideras (pris ≥ 0).
- Ogiltig momssats på artikel — endast 25/12/6/0 tillåts (som 002).

## Requirements *(mandatory)*

### Functional Requirements

**Registret**
- **FR-001**: Systemet MÅSTE låta användare skapa, lista, hämta, redigera och **arkivera**
  artiklar inom sin organisation (namn obligatoriskt; artikelnummer, enhet valfria; á-pris
  exkl. moms ≥ 0; momssats 25/12/6/0).
- **FR-002**: Artiklar MÅSTE vara tenant-isolerade (endast egna artiklar syns/nås).
- **FR-003**: Angivet artikelnummer MÅSTE vara unikt inom organisationen (tomt tillåts för
  flera); kollision nekas med tydligt fel.
- **FR-004**: Arkiverade artiklar MÅSTE döljas i artikelväljaren men förbli läsbara i registret.
- **FR-005**: Alla roller (Owner/Admin/Member) FÅR hantera artiklar.

**Fakturarader**
- **FR-006**: En fakturarad MÅSTE kunna skapas **från en artikel** så att beskrivning, enhet,
  á-pris och momssats förifylls som en **kopia** — raden kan därefter ändras fritt.
- **FR-007**: Senare ändringar/arkivering av artikeln får ALDRIG påverka befintliga rader eller
  skickade fakturor.
- **FR-008**: Fakturarader MÅSTE kunna bära en valfri **enhet**; saknad enhet är giltigt
  (bakåtkompatibelt med befintliga fakturor).
- **FR-009**: Fritextrader (utan artikel) MÅSTE fortsätta fungera oförändrat.

**PDF**
- **FR-010**: Fakturans PDF MÅSTE visa radens enhet när den finns.

### Key Entities *(include if feature involves data)*

- **Artikel (Article)**: tillhör en organisation. Namn, artikelnummer (valfritt, unikt inom
  org när angivet), enhet (valfri), á-pris exkl. moms, momssats, status (aktiv/arkiverad).
- **Fakturarad (InvoiceLine, utökas)**: + valfri enhet. (Ingen levande artikelreferens krävs
  i v1 — värdena är kopior.)

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: En rad skapad från en artikel får exakt artikelns beskrivning/enhet/pris/momssats
  och kan därefter ändras fritt — verifierat i test.
- **SC-002**: Prisändring/arkivering av artikel lämnar befintliga utkast och skickade fakturor
  oförändrade (0 påverkan, test).
- **SC-003**: Artikelnummer-kollision inom organisationen nekas alltid; samma nummer i två
  olika organisationer tillåts (test).
- **SC-004**: Ingen organisation kan se/nå en annans artiklar (0 läckor, test).
- **SC-005**: PDF visar enhet för rader som har en, och renderar rader utan enhet som tidigare
  (test).

## Assumptions

- **Snapshot utan referens:** raden lagrar ingen artikelkoppling i v1 (statistik per artikel
  kan bli senare feature — då kan en valfri referens läggas till).
- **Sökning:** enkel listning/filtrering på namn/nummer räcker i v1 (ingen fritextsökmotor).
- **Bygger på:** 001 (isolering/RBAC), 002 (utkast/beräkning/PDF/oföränderlighet).
- **Frontend:** artikelväljare i utkast-editorn + registersida. Siddesignen görs enligt
  användarens direktiv: mer kreativ, inte default-mallen (design-tokens behålls).

## Out of Scope (v1 för denna spec)

- Lagersaldo, inköpspriser/marginaler, leverantörer.
- Statistik/försäljning per artikel (kräver radreferens — senare feature).
- Import/export (CSV), bilder, kategorier/taggar.
- Prislistor per kund, rabattregler.
