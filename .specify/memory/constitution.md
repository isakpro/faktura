<!--
Sync Impact Report
==================
Version change: (template) → 1.0.0 (första ratificeringen)
Modified principles: n/a — alla placeholders ersatta
Added sections:
  - Produkt-DNA (SaaS-fakturasystem, multi-tenant)
  - Kärnprinciper I–VI (Spec-driven; Clean Code & SOLID; Test-först/TDD;
    API-kontrakt först; Multi-tenant isolering & säkerhet; CI/CD & grön pipeline)
  - Teknik- och produktconstraints
  - Utvecklingsprocess (GitFlow + PR + spec/chore-tröskel + CI-gate)
  - Governance
Removed sections: inga (templatens generiska sektioner ersatta med konkreta)
Templates requiring updates:
  - .specify/templates/plan-template.md ✅ "Constitution Check"-gaten är generisk och
    hämtar gates härifrån; ingen ändring krävs
  - .specify/templates/spec-template.md ✅ kravstruktur förenlig; ingen ändring krävs
  - .specify/templates/tasks-template.md ✅ taskkategorier (tests/core/integration)
    täcker princip III/IV; ingen ändring krävs
Follow-up TODOs: inga
-->

# Faktura Constitution — SaaS-fakturasystem

> Styr all spec-driven utveckling i Faktura. Bygger på samma arbetssätt som
> VMPage-projektet (en utvecklare, GitHub, spec-driven), men för ett multi-tenant SaaS
> där isolering, auth och betalning är förstklassiga krav.

## Produkt-DNA

**Vad Faktura är:** Ett SaaS-fakturasystem där företag (tenants) registrerar en
organisation, bjuder in teammedlemmar med roller (Owner/Admin/Member), lägger upp kunder
och ställer ut/följer upp fakturor. Varje tenant är logiskt isolerad i en delad databas
via `tenantId`. Tenants tecknar en prenumerationsplan via Stripe (testläge) som gate:ar
funktioner, och anrop rate-limitas per tenant. Produktbeslut och scope finns i
`SPEC-BRIEF.md` i repo-roten.

**Vi är uttryckligen INTE (v1):** ett komplett bokförings-/redovisningssystem, en
e-faktura-/Peppol-tjänst, en integration mot Skatteverket/BankID, eller en tjänst som
hanterar riktiga pengar. Stripe körs **enbart i testläge** och debiterar SaaS-
prenumerationen — inte indrivning av kundfakturor. Avsteg kräver en uppdaterad
`SPEC-BRIEF.md` och, vid principkonflikt, en amendment här.

## Kärnprinciper

### I. Spec-driven utveckling

All **funktionalitet** går genom Spec Kit-flödet: spec → plan → tasks → implement.
Specen är sanningskällan; kod som avviker från specen MÅSTE föregås av en specändring.
Varje spec MÅSTE explicit markera "out of scope" och ha minst ett mätbart
framgångskriterium. Funktioner som är bortvalda i `SPEC-BRIEF.md` (db-per-tenant, extern
IdP, indrivning av kundfakturor, riktiga pengar, e-faktura/Peppol) får inte smygas in
utan att briefen först uppdateras. Vad som räknas som funktionalitet (och därmed kräver
spec) kontra en chore avgörs av tröskeln i Utvecklingsprocessen nedan.

### II. Clean Code & SOLID (icke-förhandlingsbart)

All ny och modifierad kod SKA följa Clean Code och SOLID: tydlig namngivning, små
klasser/funktioner med ett ansvar, beroenden via abstraktioner (interfaces) där det ger
testbarhet, ingen onödig duplicering. Domänlogik (fakturaberäkning, moms/summering,
fakturastatus-övergångar, rollbehörigheter, plan-/kvotregler) MÅSTE vara separerad från
infrastruktur (MongoDB, Stripe, HTTP) så att den kan enhetstestas i isolation. Avvikelser
åtgärdas innan PR skapas.

### III. Test-först (TDD) för domänlogik (icke-förhandlingsbart)

Domänlogiken bär systemets korrekthet och säkerhet — den utvecklas **test-först**
(Red → Green → Refactor): ett misslyckande test skrivs före implementationen. Detta gäller
särskilt: fakturasummering och moms, tillåtna statusövergångar, RBAC-behörigheter,
tenant-isoleringsregler och plan-/rate-limit-kvoter. API-endpoints SKA ha
integrationstester för lyckade flöden, behörighetsavslag (401/403) och tenant-läckage
(en tenant får aldrig se en annans data). UI-kod och ren CRUD-plumbing kräver inte samma
täckning. `dotnet test` MÅSTE vara grönt innan PR öppnas.

### IV. API-kontrakt först

React-frontenden pratar med backenden enbart via det definierade REST-kontraktet (JSON
över HTTP, JWT i `Authorization`-headern). Kontraktet (endpoints, DTO:er, felformat,
felkoder inkl. 401/403/429) definieras i specens `contracts/`-fas innan implementation.
Breaking changes MÅSTE uppdatera både backend och frontend i samma PR — DTO-drift mellan
lagren är en bugg.

### V. Multi-tenant isolering & säkerhet (icke-förhandlingsbart)

Tenant-isolering och auth är överordnade bekvämlighet:

- **Tenant-isolering tvingas i datalagret.** Varje dokument bär `tenantId`, och varje
  läsning/skrivning filtreras på den inloggades `tenantId` från JWT. Ingen endpoint får
  acceptera `tenantId` från klienten som auktoritet. En tenant får ALDRIG kunna läsa,
  ändra eller räkna en annan tenants data — detta verifieras med test (princip III).
- **Auth är serverns ansvar.** JWT signeras och valideras av API:t; klienten avgör aldrig
  roll eller behörighet. RBAC (Owner/Admin/Member) enforce:as serverside med
  `[Authorize(Roles=...)]`/policies, inte i UI.
- **Rate limiting per tenant.** Kvoter partitioneras på tenant (kopplat till plan).
  Överskridning ger `429` med `Retry-After`. En tenant får inte kunna svälta ut en annan.
- **Plan-/feature-gating är datadrivet.** Vilka funktioner och kvoter en plan ger
  (Free/Pro) styrs av plan-konfiguration/feature-flaggor — inte hårdkodade `if plan ==`
  spridda i affärslogiken. Gränser ska kunna flyttas utan kodändring (lärt av SwingBy).
- **Hemligheter utanför versionshantering.** JWT-nyckel, Mongo-sträng, Stripe-nycklar och
  webhook-secret hålls i miljövariabler/secrets — aldrig i repo. `.env`/`appsettings.*`
  med hemligheter är gitignorerade.
- **Stripe-webhooks verifieras.** Inkommande webhooks MÅSTE signatur-verifieras mot
  webhook-secret innan de påverkar tenantens prenumerationsstatus; hantera idempotens.
- **OWASP Top 10** beaktas vid lokal granskning före PR — särskilt injektion, trasig
  åtkomstkontroll (tenant/roll), känslig dataexponering och sårbara beroenden.

### VI. CI/CD & grön pipeline (icke-förhandlingsbart)

Kvalitet automatiseras, inte bara dokumenteras:

- **CI på varje PR:** GitHub Actions bygger och kör `dotnet test` (backend) samt
  build/lint/`vitest` (frontend). Pipelinen MÅSTE vara grön innan merge — röd CI blockerar.
- **CD är automatisk:** merge till produktionsbranchen deployar frontend (Cloudflare
  Pages) och backend (Render) mot MongoDB Atlas. Deploy får aldrig kräva manuella steg
  utöver godkänd, grön PR.
- **Inga hemligheter i loggar/artefakter.** Deploy-konfiguration använder GitHub Secrets;
  pipelinen läcker inte nycklar.

## Teknik- och produktconstraints

- **Backend:** ASP.NET Core Web API på .NET 10 (C#), clean architecture i lager
  `Api` / `Domain` / `Infrastructure` + testprojekt. **Persistens:** MongoDB via officiella
  `MongoDB.Driver` — dokumentmodell, inga EF-migrations. Lokal utveckling mot
  `mongodb://localhost:27017`.
- **Frontend:** React 19 + Vite + TypeScript, TanStack Query för serverdata,
  react-router-dom. Svenska i UI, SEK och svenskt datum-/momsformat som standard. Styling
  via delade design-tokens; ingen hårdkodad styling i komponenter.
- **Identitet & data:** riktiga konton med e-post + lösenord lagras — därför gäller
  princip V fullt ut (hashade lösenord, minimering av persondata, säker tokenhantering).
- **Betalning:** Stripe **endast i testläge** i v1. Test-/livenycklar växlas via miljö —
  livenycklar kräver amendment.
- **Konfiguration:** miljöstyrd (ASP.NET `__`-nästlade nycklar, Vite `VITE_*`).
  `*.example`-filer visar vilka variabler som krävs, utan värden.

## Utvecklingsprocess (GitFlow + PR)

### Spec eller chore? (arbetsklassificering)

Innan nytt arbete påbörjas klassificeras det som **feature** eller **chore**. Vid
tveksamhet stäms klassificeringen av med projektägaren innan arbetet drar igång.

- **Feature → kräver full spec** (spec → plan → tasks → implement, branch `feature/<nr>-…`).
  En uppgift är en feature om den ändrar något av: **användarbeteende/regler**, **datamodell**,
  **API-kontraktet**, eller **säkerhets-/isoleringsmodellen** (auth, roller, tenant-gränser,
  rate limiting, Stripe-flöden).
- **Chore → ingen spec** (branch `chore/<kort-beskrivning>`, rakt till implementation).
  Tillåtet utan spec när beteende, datamodell, API och säkerhetsmodell är **oförändrade**:
  ren UI-/styling-ändring, refaktorering, omdöpning, beroendeuppdateringar, dokumentation,
  bygg-/CI-konfiguration. Princip II/III och relevant granskning gäller fortfarande.
- **Bugfix** följer samma tröskel: ren felrättning utan beteende-/kontrakts-/
  säkerhetsändring går som `bugfix/*` utan spec; rättar den en regel eller ändrar
  kontraktet/isoleringen behandlas den som en feature.

Gränsfall avgörs av frågan: *kan en användare märka skillnad i hur appen beter sig, eller
ändras datamodell/API/säkerhet?* Ja → feature/spec. Nej → chore.

### GitFlow & PR

- **GitFlow:** `main` (release) och `develop` (integration) är huvudbranchar; arbete sker
  på `feature/*`, `bugfix/*`, `hotfix/*`, `chore/*` — aldrig direkt på huvudbranchar.
- **Branchnamn:** features `feature/<spec-nr>-<kebab>` (t.ex. `feature/001-saas-skelett`),
  chores `chore/<kebab>` (inget spec-nr).
- **CI-gate:** PR får inte merge:as med röd pipeline (princip VI).
- **Lokal granskning före PR:** (1) code review enligt princip II, (2) security review
  enligt princip V/OWASP. Större brister åtgärdas före PR.
- **En PR per spec/feature**, öppnas först när hela scopet är klart: implementation
  enligt spec, tester gröna, dokumentation uppdaterad. Inga draft-/WIP-PRs.
- **Merge:** squash-merge mot `develop`; commit-meddelande enligt conventional commits med
  referens till spec-nummer. Branchen tas bort efter merge.
- **Spec-artefakter** (`specs/<nr>-<slug>/`, constitution, brief) versionshanteras i samma
  repo som koden.

## Governance

- Denna constitution gäller före alla andra konventioner i repot. Vid konflikt mellan spec
  och constitution vinner constitutionen — antingen ändras specen eller så görs en formell
  amendment här.
- **Amendments:** ändringar görs via PR som uppdaterar detta dokument inklusive versionsrad
  och Sync Impact Report. Versionering enligt SemVer: MAJOR = princip tas
  bort/omdefinieras inkompatibelt, MINOR = ny princip eller väsentligt utökad vägledning,
  PATCH = förtydligande/ordval.
- **Compliance:** `/speckit-plan`-stegets "Constitution Check" MÅSTE verifiera principerna
  I–VI innan implementation; avvikelser dokumenteras i planens Complexity Tracking med
  motivering.

**Version**: 1.0.0 | **Ratified**: 2026-06-26 | **Last Amended**: 2026-06-26
