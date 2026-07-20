<!-- SPECKIT START -->
**Faktura** — ett multi-tenant SaaS-fakturasystem. Byggs spec-driven med Spec Kit, på
samma sätt som VMPage-projektet, med inspiration/konventioner från SwingBy.

Läs först:
- Produkt-brief & beslut: [SPEC-BRIEF.md](SPEC-BRIEF.md)
- Projektprinciper (gäller före allt annat): [.specify/memory/constitution.md](.specify/memory/constitution.md)

Arbetssätt: spec → plan → tasks → implement. TDD för domänlogik, Clean Code + SOLID,
GitFlow + PR, CI måste vara grön innan merge.

Stack (planerad, som VMPage): ASP.NET Core Web API (.NET 10) i lager Api/Domain/
Infrastructure + testprojekt · MongoDB (`MongoDB.Driver`) · React 19 + Vite + TypeScript +
TanStack Query. Multi-tenancy = delad DB + `tenantId` (isolering tvingad i datalagret).
Auth = egen JWT, roller Owner/Admin/Member. Rate limiting per tenant. Stripe i testläge
(prenumeration av tenants). Deploy: Cloudflare Pages + Render + MongoDB Atlas via GitHub
Actions.

**Levererat: 001 — SaaS-skelett** (merge:at till `develop`): backend US1–US5 (registrering/login/
refresh/me, tenant-isolering via `TenantScopedRepository`, roller/inbjudningar/seat, Stripe-plan/
webhooks, rate limiting per tenant) + React/Vite-frontend. Kod i `backend/` (Api/Domain/
Infrastructure + tester) och `frontend/`. Uppföljning member-borttagning (DELETE /api/members/{id})
levererad. Kvarvarande uppföljningar: frontend-tester (Vitest), e-post-enumerering vid register.

**Levererat: 002 — Fakturadomänen** (merge:at till `develop`): US1–US6 — kunder, utkast+moms
(`InvoiceCalculator`), skick/atomisk nummerserie/låsning, betalstatus, kreditfaktura, PDF
(QuestPDF) + frontend (Kunder/Fakturor). 78 tester gröna.

**Levererat: 003 — E-postutskick av faktura** (merge:at till `develop`): US1–US3 — `EmailService`
(mejlar skickad faktura som PDF-bilaga via SMTP/MailKit bakom `IEmailSender`, loggar i egen
`invoiceEmails`-collection), endpoints `POST /invoices/{id}/email` + `GET /invoices/{id}/emails`,
frontend "Mejla"-knapp. Spec: [specs/003-faktura-epost/](specs/003-faktura-epost/spec.md).

**Levererat: 004 — Betalningspåminnelser** (merge:at till `develop`): US1–US3 — `ReminderMailer`
(delad kärna), `ReminderService` + `POST /invoices/{id}/remind` + `GET /invoices/{id}/reminders`,
`ReminderJob` + `ReminderBackgroundService` (dagligt, opt-in per org, max EN automatisk per
faktura), `GET/PUT /reminder-settings`, frontend "Påminn"-knapp + inställningskort.
Spec: [specs/004-betalningspaminnelser/](specs/004-betalningspaminnelser/spec.md).

**Release v0.4.0** på `main` (001–004 + member-borttagning). `dotnet test` = 111 gröna
(58 domän + 53 API) · frontend-tester (Vitest) = 7 gröna.

**Projektmål:** portfolio-projekt — ambitionen är så avancerat/imponerande som möjligt
(teknisk bredd + polish). Vid siddesign: var kreativ, inte default-mallen (tokens behålls).

**Levererat: 005 — Artikelregister** (merge:at): artiklar (unikt **partial**-index `{tenantId,sku}`
— sparse hade kolliderat för artiklar utan sku) förifyller rader (snapshot), `InvoiceLine.Unit`
→ DTO/PDF, **Testcontainers mot riktig Mongo** (SkippableFact utan Docker), **"Huvudboken"-temat**
(papper/bläck/stämpelröd — kreativ redesign per användardirektiv).
**Levererat: 006 — Dashboard**: `DashboardCalculator` (utestående/förfallet/betalt i år,
12-mån serie), `GET /api/dashboard`, SVG-graf + nyckeltalskort + senaste fakturor.
**Levererat: 007 — Återkommande fakturor**: `RecurringInvoice` (mån/kvartal/år, klampning,
paus/slutdatum), dagligt jobb genererar+skickar+mejlar (delad `InvoiceMailer`; ikapp utan
dubbletter; `Invoice.RecurringSourceId` för spårbarhet), Abonnemang-sida.
**Levererat: 008 — Audit trail**: `AuditMiddleware` loggar autentiserade mutationer (append-only,
tenant-isolerat), `GET /api/audit` (Owner/Admin), Aktivitet-kort med svenska etiketter.
**Infra-chores levererade:** OpenAPI/Scalar (`/scalar`), Serilog + request logging, health checks
(`/health`, `/health/ready` med Mongo-ping), Docker Compose (api+mongo+mailpit+web; curl i
api-imagen för healthcheck), **E2E (Playwright) i CI** mot compose-stacken, README-överhalning.

**Levererat: 009 — Fakturaprofil & fakturadetaljvy**: `InvoiceProfile` på Organization (orgnr,
adress, bankgiro/plusgiro, F-skatt) renderas på PDF:ns säljarblock + sidfot (bakåtkompatibelt),
`GET/PUT /api/organization-profile` (Owner/Admin skriver), detaljvy `/invoices/{id}` (rader,
summor per momssats, e-post-/påminnelsehistorik, PDF-knapp) + Fakturaprofil-kort på Översikt.
**Levererat: 010 — Kontoflöden via e-post**: registreringsbroms per adress ("register:"-nyckel i
`ILoginThrottle`, 429 + Retry-After) + varningsmejl till adressens ägare (beslut: auto-login
behålls); inbjudningar mejlas med accept-länk (`App__BaseUrl`/accept/{token}, Reply-To =
inbjudaren; mejlfel stoppar aldrig inbjudan).

**Levererat: 011 — Glömt lösenord** + Dependabot + design-polish (egen Inställningar-sida,
ren Översikt, sv-SE-belopp, graf-stödlinjer). Branch protection blockerad (kräver Pro/publikt repo).
**Levererat: 012 — Betalningsreskontra, OCR & delkreditering**: `OcrReference` (bankgirostandard,
Luhn + längdsiffra) sätts vid skick → DTO/PDF; `invoicePayments`-reskontra
(`POST/GET /invoices/{id}/payments`, status DELBETALD härledd, saldo i dashboard/detaljvy,
"Betald"-knappen betalar saldot via reskontran); delkreditering via radval i
`POST /invoices/{id}/credit` (validering före nummerförbrukning).

**Levererat: 013 — Kundportal**: publik fakturavy `/f/{token}` (kapabilitets-token, 128-bit hex
i klartext på dokumentet — medvetet beslut, dokumenterat i specen), `POST /invoices/{id}/share`
(idempotent, `PortalLinks`-hjälpare delad med mailers som numera länkar portalen i mejlen),
publika endpoints `GET /api/public/invoices/{token}(/pdf)` utan auth (IP-rate-limit-partition
för `/api/public`), "papperslik" portalsida + Kundlänk-knapp (urklipp) i detaljvyn.

**Levererat: 014 — Peppol UBL-export**: `PeppolInvoiceGenerator` (ren domänklass, `XDocument`)
bygger UBL 2.1 enligt BIS Billing 3.0 (EN 16931) — `Invoice`/`CreditNote`-rot beroende på typ,
säljare från fakturaprofilen, köpare från kundens ögonblicksbild, rader/momsuppdelning/summor
spårbart identiska med `InvoiceCalculator`. `GET /invoices/{id}/peppol` (auktoriserad,
409 för utkast) + "Peppol-XML"-knapp i detaljvyn.

**Levererat: 015 — SIE4-export**: `SieExporter` (ren domänklass) bokför ett räkenskapsårs
skickade fakturor/kreditfakturor mot en inbyggd BAS-liknande kontoplan (1510 kundfordringar,
3001–3004 försäljning per momssats, 2611/2621/2631 utgående moms) — varje verifikation
balanserar exakt, radbelopp grupperas direkt från fakturans rader så krediteringars negerade
belopp ger automatisk motbokning. `GET /api/export/sie?year=` (Owner/Admin, ISO-8859-1 `.se`-fil)
+ Export-sida (årsväljare) länkad från Inställningar.

**Levererat: 016 — Publikt API & webhooks**: API-nycklar (`fkt_live_…`, SHA-256-hash lagrad,
scopes `invoices:read`/`customers:read`/`customers:write`) autentiserar via `X-Api-Key` genom en
egen `ApiKeyAuthenticationHandler`-scheme som bygger samma claims som JWT — `/api/v1/invoices`
och `/api/v1/customers` återanvänder därför InvoiceService/CustomerService oförändrade, bara
scope-gated. Utgående webhooks (`invoice.sent`/`.paid`/`.credited`, HMAC-SHA256-signerade,
en retry, leveranslogg) via `IWebhookDispatcher` injicerad i InvoiceService. Hantering av nycklar
+ mottagare (Owner/Admin) på ny Utvecklare-sida länkad från Inställningar.

**Roadmap (användaren valde alla)**: 017 SignalR → 018 Redis rate limiting →
019 server-side sök/paginering.

Testläge: 231 backend (117 domän + 114 API inkl. Testcontainers) + 8 vitest + 1 Playwright-E2E.
**Release v0.7.0** på `main` (001–010 + infra-chores). Aktiv feature-serie: roadmapen ovan; nästa
release blir v0.8.0. Kvar: **skarp deploy** (kräver användarens konton: Render/Cloudflare/Atlas +
GitHub Secrets). Medvetna skulder (dokumenterade): in-memory rate limit/broms per instans,
refresh-tokens i localStorage.
<!-- SPECKIT END -->
