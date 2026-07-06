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

Testläge: 147 backend (76 domän + 71 API) + 3 Testcontainers (CI) + 7 vitest + 1 Playwright-E2E.
Ingen aktiv feature. Kvar: skarp deploy (kräver användarens konton: Render/Cloudflare/Atlas +
GitHub Secrets), uppföljning e-post-enumerering vid register (kräver clarify).
<!-- SPECKIT END -->
