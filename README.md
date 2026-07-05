# Faktura

Ett multi-tenant **SaaS-fakturasystem**: företag (tenants) registrerar en organisation,
bjuder in teammedlemmar med roller, lägger upp kunder och ställer ut/följer upp fakturor.
Byggs spec-driven med [Spec Kit](https://github.com/github/spec-kit) — samma arbetssätt
som VMPage-projektet, med konventioner lånade från SwingBy.

> Status: **grund uppsatt** (spec-kit, constitution, CI/CD). Ingen feature byggd ännu —
> nästa steg är spec 001 (SaaS-skelett).

## Vad det blir

| Område | Val |
|---|---|
| Multi-tenancy | Delad databas + `tenantId` på varje dokument, isolering tvingad i datalagret |
| Auth | Egen JWT (self-issued), lösenord hashas; roller **Owner / Admin / Member** |
| Rate limiting | Per tenant, kopplat till plan (`429` + `Retry-After` vid överskridning) |
| Betalning | Stripe i **testläge** — prenumerationsdebitering av tenants (Free/Pro), webhooks |
| Backend | ASP.NET Core Web API (.NET 10), clean architecture: `Api` / `Domain` / `Infrastructure` + tester |
| Persistens | MongoDB (`MongoDB.Driver`), inga EF-migrations |
| Frontend | React 19 + Vite + TypeScript + TanStack Query + react-router-dom |
| CI/CD | GitHub Actions: build + test + lint på varje PR; deploy till Cloudflare Pages + Render + Atlas |

## Arbetssätt

- **Spec-driven:** spec → plan → tasks → implement (`/speckit-*`). Specen är sanningskällan.
- **TDD** för domänlogik, **Clean Code + SOLID**, lokal code/security review före PR.
- **GitFlow:** `main` (release), `develop` (integration), arbete på `feature/*` m.fl. CI
  måste vara grön innan merge.

Principer: [.specify/memory/constitution.md](.specify/memory/constitution.md) ·
Produktbeslut: [SPEC-BRIEF.md](SPEC-BRIEF.md)

## Struktur (växer fram med första featuren)

```
.specify/        # Spec Kit (templates, memory/constitution.md, scripts)
specs/           # en mapp per feature: spec.md, plan.md, tasks.md, contracts/
.github/workflows/  # CI/CD
backend/         # ASP.NET Core (skapas i spec 001)
frontend/        # React + Vite (skapas i spec 001)
```

## Kör hela stacken med ett kommando

```bash
docker compose up --build
```

| Tjänst | URL |
|---|---|
| Appen (web) | http://localhost:8081 |
| API + Scalar-docs | http://localhost:5080 · `/scalar` · `/health/ready` |
| Mailpit (fångar alla mejl) | http://localhost:8025 |
| MongoDB | mongodb://localhost:27017 |

## Lokal utveckling (utan Docker)

Krav: .NET 10 SDK, Node 20+, MongoDB lokalt (`mongodb://localhost:27017`) eller Docker.
Hemligheter (JWT-nyckel, Mongo-sträng, Stripe-test-nycklar) sätts via miljövariabler —
se `*.env.example`/`appsettings.example.json`. **Checka aldrig in nycklar.**
