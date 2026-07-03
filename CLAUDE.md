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

Aktiv feature: **002 — Fakturadomänen** (kunder, fakturor, moms, status, kreditfaktura, PDF).
- Spec: [specs/002-fakturadoman/spec.md](specs/002-fakturadoman/spec.md)
- Checklista: [specs/002-fakturadoman/checklists/requirements.md](specs/002-fakturadoman/checklists/requirements.md)
Clarify klar (2026-07-03): moms per rad (svenska satser, exkl. moms), löpande obruten fakturaserie
per tenant vid skick, server-side PDF (QuestPDF), statusflöde Utkast→Skickad(låst)→Betald/Förfallen
+ kreditfaktura, betald markeras manuellt. Bygger på 001. Nästa steg: `/speckit-plan`.
<!-- SPECKIT END -->
