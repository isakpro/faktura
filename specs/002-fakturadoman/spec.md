# Feature Specification: Fakturadomänen (kunder, fakturor, moms, status, kreditfaktura, PDF)

**Feature Branch**: `feature/002-fakturadoman`
**Created**: 2026-07-03
**Status**: Draft
**Input**: User description: "Fakturadomänen ovanpå SaaS-skelettet (001): kunder, fakturor, rader, moms, statusflöde, kreditfaktura och PDF."

## Översikt

Bygger den affärsbärande kärnan ovanpå skelettet (spec 001): en organisation (tenant) lägger
upp **kunder**, skapar **fakturor** med **rader**, får **moms** och summor beräknade, **skickar**
fakturan (då tilldelas ett löpande fakturanummer och fakturan låses), följer **betalstatus**,
rättar via **kreditfaktura** och kan ladda ner fakturan som **PDF**. All data är tenant-isolerad
och RBAC-styrd enligt 001. Betalning markeras manuellt i v1 (ingen betalindrivning).

## Clarifications

### Session 2026-07-03

- **Moms:** Moms per **rad** med svenska satser (25/12/6/0 %). Priser anges **exkl. moms**.
  Fakturan summerar netto, moms grupperad per sats, och bruttobelopp att betala.
- **Fakturanummer:** **Löpande, obruten serie per tenant** (1, 2, 3…), tilldelas när fakturan
  **skickas** (inte i utkastläge). Uppfyller svenskt krav på unik, obruten serie.
- **PDF:** Genereras **server-side** i v1 (riktig faktura-PDF).
- **Statusflöde:** Utkast → Skickad (**låst/oföränderlig**) → Betald/Förfallen. Rättelse sker
  via **kreditfaktura** (eget dokument i samma nummerserie som refererar originalet). Betald
  markeras **manuellt**.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Hantera kunder (Priority: P1)

En inloggad användare lägger upp och underhåller organisationens kunder (namn, adress,
ev. org-/VAT-nummer, e-post, betalningsvillkor).

**Why this priority**: Utan kunder går det inte att fakturera. Minsta byggsten.

**Independent Test**: Skapa en kund, lista kunder, redigera den — allt inom den egna
organisationen; en annan organisation ser den aldrig.

**Acceptance Scenarios**:

1. **Given** en inloggad användare, **When** hon skapar en kund med namn (obligatoriskt) och
   valfria fält, **Then** sparas kunden i hennes organisation och syns i kundlistan.
2. **Given** en kund finns, **When** användaren redigerar den, **Then** uppdateras uppgifterna.
3. **Given** två organisationer A och B, **When** A listar kunder, **Then** visas endast A:s kunder.

---

### User Story 2 - Skapa och redigera fakturautkast med moms (Priority: P1)

En användare skapar ett fakturautkast för en kund, lägger till rader (beskrivning, antal,
á-pris exkl. moms, momssats) och ser netto, moms per sats och bruttobelopp beräknas korrekt.

**Why this priority**: Beräkningen är fakturans korrekthet — kärnan i produkten. Utkast kan
redigeras fritt tills det skickas.

**Independent Test**: Skapa ett utkast med flera rader i olika momssatser och verifiera att
radbelopp, netto, moms per sats och totalsumma stämmer (inkl. öresavrundning).

**Acceptance Scenarios**:

1. **Given** en kund, **When** användaren skapar ett utkast och lägger till en rad (antal ×
   á-pris), **Then** beräknas radens nettobelopp och momsbelopp enligt radens momssats.
2. **Given** ett utkast med rader i satserna 25 % och 12 %, **When** summering sker, **Then**
   visas netto totalt, momsbelopp **per sats**, och brutto att betala.
3. **Given** ett utkast, **When** användaren ändrar/tar bort rader, **Then** räknas summorna om.
4. **Given** belopp som inte går jämnt ut, **When** summering sker, **Then** avrundas enligt
   fastställd öresregel och deltotaler summerar till totalen utan differens.

---

### User Story 3 - Skicka faktura: nummer + låsning (Priority: P1)

När fakturan är klar skickar användaren den. Då tilldelas nästa lediga fakturanummer i
organisationens löpande serie, fakturadatum och förfallodatum sätts, och fakturan blir
**oföränderlig**.

**Why this priority**: En utställd faktura är ett juridiskt dokument. Nummerserie och låsning
är tvingande krav och måste finnas när fakturor lämnar utkaststadiet.

**Independent Test**: Skicka två fakturor och verifiera att de får löpande nummer utan hopp,
att numret är unikt per organisation, och att en skickad faktura inte kan ändras.

**Acceptance Scenarios**:

1. **Given** ett giltigt utkast, **When** användaren skickar det, **Then** får fakturan nästa
   lediga nummer i tenantens serie, status Skickad, samt faktura- och förfallodatum.
2. **Given** två fakturor skickas efter varandra, **When** nummer tilldelas, **Then** är serien
   obruten och varje nummer unikt inom organisationen.
3. **Given** en skickad faktura, **When** någon försöker ändra rader/belopp/kund, **Then** nekas
   ändringen (oföränderlig).
4. **Given** samtidiga skick-försök, **When** nummer tilldelas, **Then** uppstår aldrig dubbla
   nummer eller hopp i serien.

---

### User Story 4 - Betalstatus och förfallobevakning (Priority: P2)

Användaren markerar en skickad faktura som betald, och systemet visar vilka fakturor som är
obetalda respektive förfallna (efter förfallodatum).

**Why this priority**: Uppföljning av betalning är kärnvärdet efter att fakturan skickats, men
förutsätter US3.

**Independent Test**: Skicka en faktura med kort förfallotid, låt tiden passera → den listas
som förfallen; markera betald → den listas som betald och inte längre förfallen.

**Acceptance Scenarios**:

1. **Given** en skickad, obetald faktura, **When** användaren markerar den betald (med
   betaldatum), **Then** blir statusen Betald.
2. **Given** en obetald faktura vars förfallodatum passerat, **When** listan visas, **Then**
   markeras den som Förfallen.
3. **Given** en betald faktura, **When** listan visas, **Then** räknas den inte som förfallen.

---

### User Story 5 - Kreditfaktura (rättelse) (Priority: P2)

En skickad faktura som blivit fel rättas genom att skapa en kreditfaktura som refererar
originalet och neutraliserar (helt eller delvis) dess belopp.

**Why this priority**: Eftersom skickade fakturor är låsta krävs kreditfaktura för korrigering —
juridiskt korrekt hantering. Bygger på US3.

**Independent Test**: Skapa en kreditfaktura mot en skickad faktura och verifiera att den får
eget nummer i serien, refererar originalet och har negativa/krediterande belopp.

**Acceptance Scenarios**:

1. **Given** en skickad faktura, **When** användaren skapar en kreditfaktura för den, **Then**
   skapas ett kreditdokument som refererar originalet och får nästa lediga nummer i serien.
2. **Given** en kreditfaktura skapas, **When** beloppen sätts, **Then** krediteras (negativt)
   motsvarande original(delar) med samma momssatser.
3. **Given** en faktura redan är fullt krediterad, **When** man försöker kreditera igen, **Then**
   förhindras överkreditering (eller markeras tydligt).

---

### User Story 6 - Faktura som PDF (Priority: P2)

Användaren laddar ner en skickad faktura (eller kreditfaktura) som PDF med alla obligatoriska
fakturauppgifter.

**Why this priority**: En delbar/utskrivbar faktura är förväntad standard, men kan levereras
efter att data och flöde finns.

**Independent Test**: Skicka en faktura och hämta dess PDF; verifiera att PDF:en innehåller
fakturanummer, datum, säljar-/köparuppgifter, rader, moms per sats och totalsumma.

**Acceptance Scenarios**:

1. **Given** en skickad faktura, **When** användaren begär PDF, **Then** genereras en PDF med
   fakturanummer, faktura-/förfallodatum, säljare (organisation), kund, rader, moms per sats,
   och belopp att betala.
2. **Given** ett utkast (ej skickat), **When** PDF begärs, **Then** märks den tydligt som utkast
   eller nekas (utkast har inget fakturanummer).

### Edge Cases

- Vad händer om en kund tas bort som har skickade fakturor? (Kund bör inte hårt-raderas om den
  har historik — arkiveras/spärras.)
- Rad med antal 0 eller negativt á-pris i utkast — tillåtet eller valideras?
- Öresavrundning: avrundas per rad eller på totalen, och hur hanteras momsavrundning per sats?
- Förfallodatum när kundens betalningsvillkor saknas — vilken standard (t.ex. 30 dagar netto)?
- Kreditfaktura som överstiger originalets belopp — förhindras.
- Tidszon/datum för faktura-/förfallodatum (svensk tid, datum utan tid).

## Requirements *(mandatory)*

### Functional Requirements

**Kunder**
- **FR-001**: Systemet MÅSTE låta användare skapa, lista, hämta och redigera kunder inom sin
  organisation (namn obligatoriskt; adress, e-post, org-/VAT-nummer, betalningsvillkor valfria).
- **FR-002**: Kunder MÅSTE vara tenant-isolerade (endast egen organisations kunder syns/nås).
- **FR-003**: En kund med skickade fakturor får INTE hård-raderas; den arkiveras/spärras i stället.

**Fakturautkast & beräkning**
- **FR-004**: Systemet MÅSTE låta användare skapa ett fakturautkast kopplat till en kund och
  lägga till/ändra/ta bort rader (beskrivning, antal, á-pris exkl. moms, momssats 25/12/6/0 %).
- **FR-005**: Systemet MÅSTE beräkna per rad: nettobelopp (antal × á-pris) och momsbelopp
  (netto × sats), samt per faktura: summa netto, **momsbelopp per sats**, och bruttobelopp.
- **FR-006**: Avrundning MÅSTE ske enligt en fastställd öresregel så att deltotaler summerar till
  totalen utan differens.
- **FR-007**: Ett utkast MÅSTE kunna redigeras fritt och saknar fakturanummer.

**Skicka & nummerserie**
- **FR-008**: Vid skick MÅSTE fakturan tilldelas nästa lediga nummer i organisationens **löpande,
  obrutna** serie, samt faktura- och förfallodatum (förfallo = fakturadatum + betalningsvillkor).
- **FR-009**: Fakturanummer MÅSTE vara unikt inom organisationen och serien MÅSTE vara obruten
  även vid samtidiga skick (ingen dubblett, inget hopp).
- **FR-010**: En skickad faktura MÅSTE vara oföränderlig (rader, belopp, kund, datum kan ej ändras).

**Betalstatus**
- **FR-011**: Systemet MÅSTE låta användare markera en skickad faktura som betald (med betaldatum).
- **FR-012**: Systemet MÅSTE härleda status Förfallen för obetalda fakturor vars förfallodatum
  passerat, och exkludera betalda.

**Kreditfaktura**
- **FR-013**: Systemet MÅSTE kunna skapa en kreditfaktura som refererar en skickad originalfaktura,
  får eget nummer i serien och krediterar (negativt) med samma momssatser.
- **FR-014**: Systemet MÅSTE förhindra överkreditering (kreditbelopp > kvarvarande att kreditera).

**PDF**
- **FR-015**: Systemet MÅSTE generera en PDF för en skickad faktura/kreditfaktura med alla
  obligatoriska uppgifter (nummer, datum, säljare, kund, rader, moms per sats, totalsumma).
- **FR-016**: Systemet får INTE ge fakturanummer eller "riktig" PDF till ett utkast.

**Tvärgående**
- **FR-017**: Alla fakturaåtgärder MÅSTE vara tenant-isolerade och kräva inloggning (RBAC enligt 001;
  Member får hantera kunder och fakturor).

### Key Entities *(include if feature involves data)*

- **Kund (Customer)**: tillhör en organisation. Namn, adress, e-post, org-/VAT-nummer,
  betalningsvillkor (dagar), status (aktiv/arkiverad).
- **Faktura (Invoice)**: tillhör en organisation och en kund. Typ (Faktura/Kreditfaktura),
  status (Utkast/Skickad/Betald/Förfallen), fakturanummer (först vid skick), fakturadatum,
  förfallodatum, betaldatum, referens till original (för kreditfaktura), rader, summor.
- **Fakturarad (InvoiceLine)**: beskrivning, antal, á-pris (exkl. moms), momssats; härledda
  netto- och momsbelopp.
- **Nummerserie (InvoiceNumberSequence)**: per organisation; nästa lediga nummer, tilldelas atomiskt.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Momsberäkning stämmer i test för alla satser (25/12/6/0) och blandade fakturor;
  deltotaler summerar till totalen utan öresdifferens (0 avvikelser).
- **SC-002**: Vid 100 samtidiga skick i test tilldelas 100 unika, obrutna nummer — inga dubbletter
  eller hopp.
- **SC-003**: En skickad faktura kan aldrig ändras via någon dokumenterad åtgärd (verifierat i test).
- **SC-004**: Ingen organisation kan se/nå en annan organisations kunder eller fakturor (0 läckor).
- **SC-005**: En kreditfaktura kan aldrig kreditera mer än originalets kvarvarande belopp.
- **SC-006**: En skickad fakturas PDF innehåller alla obligatoriska fält (verifierat).

## Assumptions

- **Valuta:** SEK i v1 (multivaluta utanför scope).
- **Betalning:** markeras manuellt; ingen betalindrivning/Stripe för kundfakturor (enligt 001-brief).
- **Betalningsvillkor:** standard 30 dagar netto när kund saknar eget värde.
- **Bygger på 001:** tenant-isolering, auth och roller återanvänds (TenantScopedRepository, JWT).
- **Avrundning:** öresavrundning på radnivå för moms, deltotaler stäms av mot total (fastställs i plan).
- **Datum:** svensk tid; faktura-/förfallodatum är datum utan tidsdel.

## Out of Scope (v1 för denna spec)

- Betalindrivning av kundfakturor (Stripe/betallänk), påminnelser/inkasso, e-faktura/Peppol.
- Multivaluta, ROT/RUT, delbetalningar, återkommande/abonnemangsfakturor.
- Bokföring/verifikat, SIE-export, momsdeklaration, integration mot Skatteverket.
- Produkt-/artikelregister (rader skrivs fritt i v1), lager, offerter.
- Automatisk e-postutskick av faktura till kund (endast nedladdningsbar PDF i v1).
