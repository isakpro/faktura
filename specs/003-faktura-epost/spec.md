# Feature Specification: E-postutskick av faktura

**Feature Branch**: `feature/003-faktura-epost`
**Created**: 2026-07-04
**Status**: Draft
**Input**: User description: "Skicka fakturan (PDF) till kundens e-post — separat åtgärd på en skickad faktura, med utskickshistorik."

## Översikt

Bygger vidare på fakturadomänen (002): en användare kan **mejla en skickad faktura** (eller
kreditfaktura) till kunden med fakturan som **PDF-bilaga**. Utskicket är en **separat åtgärd**
(inte automatiskt vid skick) och kan upprepas/skickas till en annan adress. Varje utskick
**loggas** (mottagare, tidpunkt, resultat). Avsändare är en systemadress med organisationens
namn som visningsnamn och **Reply-To** satt till avsändande användares e-post. Allt är
tenant-isolerat och kräver inloggning (RBAC enligt 001). E-post skickas via **SMTP** (konfig).

## Clarifications

### Session 2026-07-04

- **Leverantör:** SMTP (konfigurerbar host/port/användare/lösenord), bakom en `IEmailSender`-
  abstraktion; fejkas i test (inga externa anrop).
- **Trigger:** Separat "Mejla"-åtgärd på en **skickad** faktura. Kan upprepas och skickas till
  en överstyrd adress.
- **Avsändare:** From = systemadress (konfig) med organisationens namn som visningsnamn;
  **Reply-To** = den avsändande användarens e-post.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Mejla en skickad faktura till kunden (Priority: P1)

En användare öppnar en skickad faktura och väljer "Mejla till kund". Systemet skickar ett
mejl till kundens e-post med fakturan som PDF-bilaga och loggar utskicket.

**Why this priority**: Att kunna leverera fakturan till kunden är hela poängen med featuren;
utan den finns bara en nedladdningsbar PDF.

**Independent Test**: Skicka en faktura, utför "Mejla" och verifiera (via den fejkade sändaren)
att ett mejl med rätt mottagare, ämne (fakturanummer) och PDF-bilaga producerades, samt att
ett utskick loggades.

**Acceptance Scenarios**:

1. **Given** en skickad faktura vars kund har e-post, **When** användaren mejlar den, **Then**
   skickas ett mejl till kundens e-post med fakturan som PDF-bilaga och utskicket loggas som lyckat.
2. **Given** ett utkast (ej skickat), **When** användaren försöker mejla det, **Then** nekas det
   (utkast har ingen faktura-PDF).
3. **Given** en kund utan e-postadress, **When** användaren mejlar utan att ange adress, **Then**
   nekas det med ett tydligt fel om att mottagare saknas.
4. **Given** en skickad faktura, **When** mejlet skapas, **Then** är From en systemadress med
   organisationens namn som visningsnamn och Reply-To satt till avsändarens e-post.

---

### User Story 2 - Skicka om eller till annan adress (Priority: P2)

En användare kan skicka fakturan på nytt, eller ange en annan mottagaradress (t.ex. kundens
ekonomiavdelning) vid utskicket.

**Why this priority**: Vanligt att fakturan behöver skickas om eller till en specifik adress,
men det bygger på US1.

**Independent Test**: Mejla samma faktura två gånger, andra gången till en överstyrd adress,
och verifiera att båda utskicken sker och loggas var för sig.

**Acceptance Scenarios**:

1. **Given** en redan mejlad faktura, **When** användaren mejlar den igen, **Then** skickas ett
   nytt mejl och ytterligare ett utskick loggas (ingen blockering av upprepade utskick).
2. **Given** en skickad faktura, **When** användaren anger en överstyrd mottagaradress, **Then**
   går mejlet till den adressen i stället för kundens standardadress.

---

### User Story 3 - Se utskickshistorik per faktura (Priority: P2)

En användare kan se om och när en faktura mejlats, till vilken adress och med vilket resultat.

**Why this priority**: Ger spårbarhet ("har kunden fått fakturan?"), men är sekundärt till
själva utskicket.

**Independent Test**: Mejla en faktura (lyckat) och en till en ogiltig adress (misslyckat) och
verifiera att historiken visar båda med rätt mottagare, tidpunkt och status.

**Acceptance Scenarios**:

1. **Given** en faktura som mejlats en eller flera gånger, **When** användaren visar dess
   utskickshistorik, **Then** listas varje utskick med mottagare, tidpunkt och status (lyckat/misslyckat).
2. **Given** ett misslyckat utskick (t.ex. SMTP-fel), **When** historiken visas, **Then** framgår
   att det misslyckades (med en kort orsak) och fakturan är i övrigt opåverkad.

### Edge Cases

- Mottagaradress med ogiltigt format — valideras innan utskick.
- SMTP-fel/timeout vid utskick — loggas som misslyckat, felet visas för användaren, fakturan
  och dess status ändras inte.
- Mycket stor PDF/bilaga — rimlig gräns (fakturor är små; ingen särskild hantering i v1).
- Kreditfaktura mejlas på samma sätt som en faktura.
- Upprepade snabba utskick — tillåts (ingen idempotensspärr); loggas var för sig.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Systemet MÅSTE låta en inloggad användare mejla en **skickad** faktura/kreditfaktura
  till en mottagare, med fakturan som **PDF-bilaga**.
- **FR-002**: Systemet MÅSTE neka utskick av utkast (utkast saknar faktura-PDF och nummer).
- **FR-003**: Standardmottagare MÅSTE vara kundens e-postadress; användaren MÅSTE kunna **överstyra**
  mottagaradressen vid utskicket.
- **FR-004**: Systemet MÅSTE avvisa utskick när ingen giltig mottagaradress finns (varken kund-
  adress eller överstyrd), med ett tydligt fel.
- **FR-005**: Mejlets **From** MÅSTE vara en konfigurerad systemadress med organisationens namn som
  visningsnamn, och **Reply-To** MÅSTE vara den avsändande användarens e-post.
- **FR-006**: Mejlets ämne/innehåll MÅSTE identifiera fakturan (fakturanummer och organisationsnamn).
- **FR-007**: Systemet MÅSTE **logga varje utskick** (faktura, mottagare, tidpunkt, status lyckat/
  misslyckat, ev. felorsak) och visa historiken per faktura.
- **FR-008**: Vid leveransfel (ogiltig adress, SMTP-fel) MÅSTE utskicket loggas som misslyckat och
  felet returneras till användaren **utan** att fakturans status/innehåll ändras.
- **FR-009**: Upprepade utskick MÅSTE tillåtas (ingen spärr); varje utskick loggas separat.
- **FR-010**: Alla åtgärder och historik MÅSTE vara tenant-isolerade (endast egna fakturor) och
  kräva inloggning (RBAC enligt 001; Member får mejla fakturor).
- **FR-011**: SMTP-uppgifter och systemavsändaradress MÅSTE komma från konfiguration/miljö —
  aldrig i repo.

### Key Entities *(include if feature involves data)*

- **Utskick (InvoiceEmail)**: en logg-post för ett e-postutskick — tillhör en organisation och en
  faktura. Attribut: mottagare, ämne, tidpunkt, status (lyckat/misslyckat), ev. felorsak.
- **Faktura (från 002)**: oförändrad; utskick lagras separat så den skickade fakturans innehåll
  förblir oföränderligt.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Ett utskick av en skickad faktura producerar ett mejl med rätt mottagare, ämne
  (fakturanummer) och en PDF-bilaga — verifierat i test via den fejkade sändaren.
- **SC-002**: Utkast kan aldrig mejlas (verifierat i test).
- **SC-003**: Ingen organisation kan mejla eller se utskickshistorik för en annan organisations
  faktura (0 läckor, verifierat i test).
- **SC-004**: Ett leveransfel loggas som misslyckat och lämnar fakturan oförändrad (verifierat i test).
- **SC-005**: Utskickshistoriken visar varje utskick med mottagare, tidpunkt och status.

## Assumptions

- **Reply-To:** den avsändande användarens e-post (från inloggningen). Egen "faktura-avsändar-
  adress" per organisation kan tillkomma senare.
- **Innehåll:** enkel svensk mejltext (hälsning + fakturanummer + belopp att betala + PDF-bilaga);
  anpassningsbara mallar är utanför v1.
- **Leverantör:** SMTP i test-/utvecklingsläge; en fejkad sändare används i tester (inga riktiga mejl).
- **Bygger på 002:** använder fakturans PDF-generator och 001:s tenant-isolering/auth.

## Out of Scope (v1 för denna spec)

- Öppnings-/bounce-/klick-spårning och leveranswebhooks.
- Schemalagda betalningspåminnelser och återkommande utskick (egna features).
- Massutskick, anpassningsbara mejlmallar/varumärkning i UI, bilagor utöver fakturans PDF.
- Domänverifiering (SPF/DKIM) per tenant för avsändning från kundens egen domän.
