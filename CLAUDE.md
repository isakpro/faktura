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
webhooks, rate limiting per tenant) + React/Vite-frontend. 58 tester gröna. Kod i `backend/`
(Api/Domain/Infrastructure + tester) och `frontend/`. Kända uppföljningar: member-borttagning
(DELETE /api/members/{id}), frontend-tester, e-post-enumerering vid register.

**Levererat: 002 — Fakturadomänen** (merge:at till `develop`): US1–US6 — kunder, utkast+moms
(`InvoiceCalculator`), skick/atomisk nummerserie/låsning, betalstatus, kreditfaktura, PDF
(QuestPDF) + frontend (Kunder/Fakturor). 78 tester gröna.

Aktiv feature: **003 — E-postutskick av faktura** (mejla skickad faktura som PDF-bilaga).
- Spec: [specs/003-faktura-epost/spec.md](specs/003-faktura-epost/spec.md)
- Checklista: [specs/003-faktura-epost/checklists/requirements.md](specs/003-faktura-epost/checklists/requirements.md)
Clarify klar (2026-07-04): SMTP bakom `IEmailSender` (fejkas i test), separat "Mejla"-åtgärd på
skickad faktura (kan skickas om/överstyra adress), systemavsändare + Reply-To = avsändarens
e-post, utskickshistorik per faktura. Bygger på 002 (PDF) + 001 (isolering/auth).
**Implementerat** (feature/003-faktura-epost): US1–US3 — `EmailService` (mejlar skickad faktura
som PDF-bilaga via SMTP/MailKit bakom `IEmailSender`, loggar i egen `invoiceEmails`-collection),
endpoints `POST /invoices/{id}/email` + `GET /invoices/{id}/emails`, frontend "Mejla"-knapp.
`dotnet test` = 85 gröna (46 domän + 39 API). Nästa: PR till `develop`.
<!-- SPECKIT END -->
