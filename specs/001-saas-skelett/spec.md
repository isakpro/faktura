# Feature Specification: SaaS-skelett (multi-tenancy, auth, roller, plan, rate limiting)

**Feature Branch**: `feature/001-saas-skelett`
**Created**: 2026-06-26
**Status**: Draft
**Input**: User description: "SaaS-skelett med multi-tenancy + auth, JWT, roller, rate limiting per tenant och Stripe i testläge (prenumeration av tenants). React + .NET + MongoDB. Fakturadomänen tas i en senare spec."

## Översikt

Det här är den första, vertikala skivan av Faktura: själva **SaaS-skelettet** som all
fakturafunktionalitet senare vilar på. En person kan registrera en **organisation**
(tenant), logga in, bjuda in kollegor med **roller**, teckna en **plan** (Free/Pro via
betalleverantör i testläge) och får ett konto vars data är **helt isolerat** från andra
organisationer. Anrop **rate-limitas per organisation**. Ingen fakturadomän ingår här —
den byggs i spec 002 ovanpå detta skelett.

## Clarifications

### Session 2026-06-28

- **Onboarding:** Self-service — vem som helst registrerar en organisation från en publik
  signup-sida och blir Owner direkt.
- **Tenant-routing:** Endast via JWT-claim. En gemensam app-URL; ingen synlig tenant i
  subdomän eller path i v1.
- **Free vs Pro:** Skiljer på **seats + rate-limit**. Free = begränsat antal användare
  (standard 2) + lägre anropskvot; Pro = fler användare + högre kvot. Gränserna är
  datadrivna (plan-konfiguration). Fakturaspecifika gränser definieras i spec 002.
- **E-postverifiering:** Ingen i v1 — kontot blir aktivt direkt vid registrering (kan
  läggas till senare; kräver då en e-postleverantör).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Registrera organisation och bli ägare (Priority: P1)

En ny användare registrerar sig med e-post och lösenord och skapar samtidigt sin
organisation. Hon blir automatiskt **Owner** för organisationen och kan logga in.

**Why this priority**: Utan att kunna skapa en organisation och ett första konto finns
ingen ingång till produkten. Detta är den minsta levererbara skivan — efter den finns en
inloggningsbar, isolerad tenant.

**Independent Test**: Registrera en ny organisation, logga in, och bekräfta att ett
Owner-konto kopplat till en nyskapad, tom organisation existerar och kan autentiseras.

**Acceptance Scenarios**:

1. **Given** ingen befintlig användare med e-posten, **When** hon registrerar organisation
   med organisationsnamn + e-post + lösenord, **Then** skapas en organisation och ett
   Owner-konto, och hon kan logga in.
2. **Given** en e-post som redan används, **When** hon försöker registrera igen, **Then**
   nekas registreringen med ett tydligt fel utan att avslöja kontodetaljer.
3. **Given** ett svagt lösenord (under policygräns), **When** hon registrerar, **Then**
   nekas registreringen med vägledande felmeddelande.
4. **Given** giltiga uppgifter, **When** hon loggar in, **Then** får hon en
   autentiserad session som identifierar henne som Owner i sin organisation.

---

### User Story 2 - Isolerad åtkomst till enbart sin organisations data (Priority: P1)

En inloggad användare kommer enbart åt sin egen organisations data. Ingen begäran, oavsett
hur den utformas, får returnera eller påverka en annan organisations data.

**Why this priority**: Tenant-isolering är SaaS:ets icke-förhandlingsbara säkerhetskärna
(constitution princip V). Den måste finnas från första skivan — att efterhandsmontera
isolering är en vanlig källa till allvarliga dataläckor.

**Independent Test**: Skapa två organisationer med var sitt konto, lägg upp data i båda,
och verifiera att konto A aldrig kan läsa, ändra, räkna eller ana konto B:s data — varken
via list-, hämta-, uppdatera- eller raderingsförsök.

**Acceptance Scenarios**:

1. **Given** två organisationer A och B med data, **When** A:s användare begär en lista,
   **Then** returneras endast A:s poster.
2. **Given** A känner till (eller gissar) ett id som tillhör B, **When** A begär den posten
   direkt, **Then** nekas åtkomst (behandlas som "finns inte" för A).
3. **Given** en begäran som försöker ange en annan organisation än den inloggades, **When**
   den tas emot, **Then** ignoreras det angivna värdet och endast den inloggades
   organisation gäller.
4. **Given** en utgången eller manipulerad session/token, **When** den används, **Then**
   nekas åtkomst.

---

### User Story 3 - Bjuda in teammedlemmar och styra behörighet via roller (Priority: P2)

En Owner eller Admin bjuder in kollegor till organisationen och tilldelar roller
(**Owner / Admin / Member**). Vad en användare får göra avgörs av rollen och tvingas på
serversidan.

**Why this priority**: Team-samarbete med olika behörigheter är centralt för ett
verksamhets-SaaS, men förutsätter att US1–US2 finns. Rollerna är fundament för
fakturadomänens behörigheter senare.

**Independent Test**: Bjud in en användare som Member, logga in som den, och verifiera att
tillåtna åtgärder lyckas och otillåtna (t.ex. hantera medlemmar/plan) nekas — samt att
samma åtgärd lyckas som Admin/Owner.

**Acceptance Scenarios**:

1. **Given** en inloggad Owner/Admin, **When** hon bjuder in en e-post med roll Member,
   **Then** kan den inbjudna acceptera och få ett Member-konto i samma organisation.
2. **Given** en Member, **When** den försöker bjuda in användare eller ändra roller,
   **Then** nekas åtgärden (403) med tydligt fel.
3. **Given** en Admin, **When** den sätter en annan användares roll till Admin eller
   Member, **Then** uppdateras rollen; men endast Owner kan tilldela/ta bort Owner.
4. **Given** den enda Owner:n, **When** hon försöker ta bort/nedgradera sig själv, **Then**
   förhindras det så att organisationen aldrig blir utan Owner.
5. **Given** en Free-organisation som redan nått sin seat-gräns (standard 2), **When**
   Owner/Admin bjuder in ytterligare en användare, **Then** nekas det med hänvisning till
   uppgradering till Pro.

---

### User Story 4 - Teckna och hantera plan (Free/Pro) via betalleverantör i testläge (Priority: P2)

En Owner väljer en plan. Free gäller utan betalning; Pro tecknas via betalleverantörens
testläge (Checkout). Planen styr vilka funktioner/kvoter organisationen har, och
plan-status hålls i synk med betalleverantören.

**Why this priority**: Affärsmodellen (freemium-gate) är ett uttalat krav, men är inte
nödvändig för att skelettets kärna (US1–US3) ska ge värde. Den byggs efter att tenant/auth
står.

**Independent Test**: Teckna Pro i betalleverantörens testläge och verifiera att
organisationens plan blir Pro samt att en Pro-gatead funktion blir tillgänglig; säg sedan
upp/nedgradera och verifiera att den gatas av igen.

**Acceptance Scenarios**:

1. **Given** en organisation på Free, **When** Owner startar Pro-tecknande och slutför
   testbetalning, **Then** uppdateras organisationen till Pro och Pro-funktioner låses upp.
2. **Given** en avslutad/utebliven prenumeration (signal från betalleverantören), **When**
   den tas emot och verifieras äkta, **Then** nedgraderas organisationen enligt regelverket.
3. **Given** en inkommande betalsignal, **When** den inte kan verifieras som äkta, **Then**
   avvisas den och ingen planändring sker.
4. **Given** samma betalsignal levereras flera gånger, **When** den behandlas, **Then** sker
   planändringen idempotent (ingen dubbel effekt).
5. **Given** en Member eller Admin, **When** den försöker ändra plan, **Then** nekas
   åtgärden — endast Owner hanterar plan.

---

### User Story 5 - Rättvis användning via rate limiting per organisation (Priority: P3)

Anrop begränsas per organisation enligt dess plan. En organisation som överskrider sin
kvot får ett tydligt "för många anrop"-svar och kan inte påverka andra organisationers
prestanda.

**Why this priority**: Skydd mot missbruk/överbelastning är viktigt men kan läggas till
sist i skelettet utan att blockera övrigt värde.

**Independent Test**: Skicka anrop över kvoten för organisation A och verifiera att A får
ett begränsningssvar med vänta-information, medan organisation B samtidigt är opåverkad.

**Acceptance Scenarios**:

1. **Given** en organisation som nått sin kvot, **When** nästa anrop kommer, **Then**
   svaras med en begränsningsstatus och information om när det går att försöka igen.
2. **Given** organisation A är begränsad, **When** organisation B gör anrop, **Then**
   påverkas B inte av A:s begränsning.
3. **Given** en organisation på Pro, **When** dess kvot jämförs med Free, **Then** är
   Pro:s kvot minst lika hög (plan-styrd gräns).

---

### Edge Cases

- Vad händer när en inbjudan accepteras av en e-post som redan har konto i en **annan**
  organisation? (En användaridentitet hör till en organisation i v1 — se Assumptions.)
- Hur hanteras en inbjudan som aldrig accepteras eller som återkallas innan accept?
- Vad händer vid inloggningsförsök mot en organisation vars prenumeration upphört —
  spärras inloggning eller bara Pro-funktioner? (v1: inloggning tillåts, Pro-funktioner gatas.)
- Hur svarar systemet på samtidiga rolländringar av samma användare?
- Vad händer om betalleverantörens signaler kommer i oordning (uppsägning före tecknande)?
- Hur hanteras många misslyckade inloggningar (skydd mot lösenordsgissning)?

## Requirements *(mandatory)*

### Functional Requirements

**Organisation & registrering**
- **FR-001**: Systemet MÅSTE låta en ny användare skapa en organisation och samtidigt ett
  första konto som blir organisationens **Owner**.
- **FR-002**: Systemet MÅSTE avvisa registrering med en e-post som redan är upptagen, utan
  att läcka huruvida kontot finns på ett sätt som möjliggör enumerering.
- **FR-003**: Systemet MÅSTE upprätthålla en lösenordspolicy (minsta styrka) och lagra
  lösenord enbart i hashad form.

**Autentisering & session**
- **FR-004**: Systemet MÅSTE autentisera användare via e-post + lösenord och utfärda en
  tidsbegränsad, verifierbar session som bär användarens organisation och roll.
- **FR-005**: Systemet MÅSTE avvisa utgångna, ogiltiga eller manipulerade sessioner.
- **FR-006**: Systemet MÅSTE låta en användare logga ut så att sessionen inte längre ger
  åtkomst.

**Tenant-isolering (icke-förhandlingsbart)**
- **FR-007**: Varje datapost MÅSTE tillhöra exakt en organisation, och all läsning/skrivning
  MÅSTE filtreras på den inloggades organisation.
- **FR-008**: Systemet MÅSTE härleda organisationstillhörighet enbart från den
  autentiserade sessionen och ALDRIG från klientangivna värden.
- **FR-009**: En begäran mot en post i en annan organisation MÅSTE nekas och får inte
  avslöja att posten existerar.

**Roller & behörighet**
- **FR-010**: Systemet MÅSTE stödja rollerna **Owner**, **Admin** och **Member** per
  organisation och tvinga behörighet på serversidan.
- **FR-011**: Owner/Admin MÅSTE kunna bjuda in användare; endast Owner MÅSTE kunna
  tilldela eller ta bort Owner-rollen.
- **FR-012**: Member MÅSTE nekas medlems- och planhantering.
- **FR-013**: Systemet MÅSTE förhindra att en organisation lämnas utan någon Owner.
- **FR-014**: En inbjuden användare MÅSTE kunna acceptera inbjudan och få ett konto med
  tilldelad roll i organisationen.

**Plan & betalning (testläge)**
- **FR-015**: Systemet MÅSTE stödja minst två plannivåer (**Free** och **Pro**) per
  organisation.
- **FR-016**: Owner MÅSTE kunna teckna Pro via betalleverantörens **testläge** och systemet
  MÅSTE uppdatera organisationens plan vid genomförd betalning.
- **FR-017**: Systemet MÅSTE ta emot och **verifiera äktheten** av plan-/betalsignaler från
  betalleverantören innan plan-status ändras, och behandla dem **idempotent**.
- **FR-018**: Systemet MÅSTE nedgradera en organisation när dess prenumeration upphör/avslutas.
- **FR-019**: Vilka funktioner/kvoter en plan ger MÅSTE styras av **plan-konfiguration**
  (datadrivet), inte hårdkodade villkor spridda i logiken.
- **FR-025**: Systemet MÅSTE begränsa antalet aktiva användare per organisation till
  planens **seat-gräns** (standard Free = 2). Ett inbjudnings-/aktiveringsförsök utöver
  gränsen MÅSTE nekas med ett tydligt fel som hänvisar till uppgradering.

**Rate limiting**
- **FR-020**: Systemet MÅSTE begränsa anropsfrekvens **per organisation** enligt dess plan.
- **FR-021**: Vid överskriden kvot MÅSTE systemet svara med en begränsningsstatus och
  information om när nytt försök kan göras.
- **FR-022**: En organisations begränsning får INTE påverka andra organisationers tillgänglighet.

**Säkerhet & loggning**
- **FR-023**: Systemet MÅSTE skydda mot upprepade misslyckade inloggningar (t.ex. broms/
  spärr) för att försvåra lösenordsgissning.
- **FR-024**: Systemet MÅSTE logga säkerhetsrelevanta händelser (registrering, inloggning,
  rolländring, planändring) utan att logga hemligheter eller lösenord.

### Key Entities *(include if feature involves data)*

- **Organisation (tenant)**: En kund/organisation. Attribut: namn, skapad, plan-status
  (Free/Pro), prenumerationsreferens. Äger all annan data.
- **Användare**: Ett konto som tillhör en organisation. Attribut: e-post, hashat lösenord,
  roll (Owner/Admin/Member), status. Tillhör exakt en organisation (v1).
- **Inbjudan**: En väntande inbjudan till en e-post med en avsedd roll, kopplad till en
  organisation; kan accepteras eller upphöra.
- **Prenumeration/plan**: Organisationens aktuella plan och status, samt referens till
  betalleverantörens motsvarighet (testläge).
- **Plan-definition**: Datadriven beskrivning av vad varje plan ger (funktioner + kvoter,
  inkl. rate-limit).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: En ny användare kan gå från start till inloggad Owner i en ny organisation på
  under 2 minuter.
- **SC-002**: I test kan ingen organisation komma åt någon annan organisations data genom
  någon dokumenterad åtgärd (0 cross-tenant-läckor) — verifierat med automatiska tester.
- **SC-003**: 100 % av rollbegränsade åtgärder nekas korrekt för Member och tillåts för
  behörig roll, verifierat med automatiska tester.
- **SC-004**: En organisation kan uppgradera till Pro i testläge och se en Pro-gatead
  funktion bli tillgänglig inom en (1) interaktion efter genomförd testbetalning.
- **SC-005**: När en organisation överskrider sin kvot får den ett begränsningssvar, medan
  en annan organisation samtidigt betjänas normalt — verifierat med test.
- **SC-006**: Förfalskade eller upprepade betalsignaler ändrar aldrig en organisations plan
  felaktigt (verifierad signatur + idempotens), verifierat med test.

## Assumptions

- **Onboarding:** Self-service — vem som helst registrerar en ny organisation i v1 (beslut
  2026-06-28, se Clarifications).
- **Tenant-routing:** Organisationstillhörighet bärs enbart i sessionen/token; ingen synlig
  subdomän eller path-baserad tenant-routing i v1 (beslut 2026-06-28).
- **E-postverifiering:** Ingen i v1 — kontot är aktivt direkt vid registrering (beslut
  2026-06-28).
- **En användare = en organisation** i v1. Samma person i flera organisationer (org-byte)
  är utanför scope och kräver senare spec.
- **Betalleverantör:** Stripe i **testläge** (per SPEC-BRIEF/constitution). Endast
  SaaS-prenumerationen hanteras — inte indrivning av kundfakturor.
- **Plannivåer (beslut 2026-06-28):** Free och Pro skiljer på **seats + rate-limit**.
  Standard: Free = 2 användare + lägre kvot, Pro = fler användare + högre kvot. Exakta
  rate-limit-tak finjusteras i `/speckit-plan`; gränserna ska vara datadrivna.
- **Stack** (ej en del av detta *vad*, men låst i brief/constitution): .NET-API + MongoDB +
  React, egen JWT. Detaljeras i `/speckit-plan`.

## Out of Scope (v1 för denna spec)

- **Fakturadomänen** (kunder, fakturor, rader, moms, status, PDF) — egen spec 002.
- Indrivning av kundfakturor via betalleverantör; e-faktura/Peppol; BankID; Skatteverket.
- Databas-per-tenant och extern IdP (medvetet bortvalt i brief/constitution).
- Organisationsbyte / användare i flera organisationer; SSO; e-postverifiering utöver
  inbjudningsflödet (kan tas i senare spec).
