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

Aktiv feature: **001 — SaaS-skelett** (tenancy + auth + roller + plan/Stripe + rate limiting).
- Plan (läs först): [specs/001-saas-skelett/plan.md](specs/001-saas-skelett/plan.md)
- Spec: [specs/001-saas-skelett/spec.md](specs/001-saas-skelett/spec.md)
- Design: [research.md](specs/001-saas-skelett/research.md) · [data-model.md](specs/001-saas-skelett/data-model.md) · [contracts/rest-api.md](specs/001-saas-skelett/contracts/rest-api.md) · [quickstart.md](specs/001-saas-skelett/quickstart.md)
Clarify klar (2026-06-28): self-service onboarding, JWT-claim-routing, Free/Pro = seats + rate-limit, ingen e-postverifiering i v1.
Plan klar: .NET 10 clean architecture (Api/Domain/Infrastructure) + React/Vite, JWT access+refresh, TenantScopedRepository, inbyggd rate limiting per tenant, Stripe.net webhooks.
**Implementerat** (feature/001-saas-skelett): backend US1–US5 (registrering/login/refresh/me,
tenant-isolering, roller/inbjudningar/seat, Stripe-plan/webhooks, rate limiting per tenant) +
React/Vite-frontend. `dotnet test` = 58 gröna (35 domän + 23 API). Kod i `backend/` (Api/Domain/
Infrastructure + tester) och `frontend/`.
Kända uppföljningar: member-borttagning (DELETE /api/members/{id}), frontend-tester, e-post-
enumerering vid register. Nästa: PR till `develop`; fakturadomänen = spec 002.
<!-- SPECKIT END -->
