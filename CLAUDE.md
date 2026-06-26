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
- Spec: [specs/001-saas-skelett/spec.md](specs/001-saas-skelett/spec.md)
- Checklista: [specs/001-saas-skelett/checklists/requirements.md](specs/001-saas-skelett/checklists/requirements.md)
Nästa steg: `/speckit-clarify` (bekräfta öppna val) → `/speckit-plan`. Fakturadomänen = spec 002.
<!-- SPECKIT END -->
