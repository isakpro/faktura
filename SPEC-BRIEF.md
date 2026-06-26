# Spec-brief: Faktura — SaaS-fakturasystem

Underlag för `/speckit-specify`. Produktbeslut från projektägaren (2026-06-26).
Samma spec-driven arbetssätt som VMPage-projektet, men för ett multi-tenant SaaS.

## Vad vi bygger

Ett **SaaS-fakturasystem** där företag (tenants) registrerar en organisation, bjuder in
teammedlemmar med roller, lägger upp kunder och ställer ut/följer upp fakturor. Varje
tenant är isolerad från andra, betalar en **prenumerationsplan via Stripe (testläge)** för
att använda tjänsten, och har egna användare, kunder och fakturor.

## Beslut för v1 (låsta med projektägaren)

| Område | Beslut |
|---|---|
| Produkt | SaaS-fakturasystem: organisationer (tenants) ställer ut och hanterar fakturor mot sina kunder |
| Multi-tenancy | **Delad databas + `tenantId` på varje dokument.** Isoleringen tvingas i repo-/datalagret — ingen query når data utan tenant-filter. Ett MongoDB-kluster för alla tenants |
| Autentisering | **Egen JWT (self-issued).** Eget register/login i .NET, lösenord hashas (t.ex. ASP.NET Core PasswordHasher / Argon2), API:t signerar JWT med claims `sub`, `tenantId`, `role`. Ingen extern IdP i v1 |
| Roller | **Owner / Admin / Member** per tenant (RBAC via `[Authorize(Roles=...)]`). Owner skapas vid tenant-registrering; Owner/Admin kan bjuda in och sätta roller; Member hanterar fakturor men inte fakturering/medlemmar |
| Rate limiting | **Per tenant** (inte bara per IP). Kvoter kopplas till tenant + plan; överskridning ger `429` med `Retry-After`. .NET:s inbyggda rate limiting-middleware med tenant som partition |
| Betalning (Stripe) | **Prenumerationsdebitering av tenants i testläge.** Stripe Checkout för att teckna plan, webhooks uppdaterar tenantens prenumerationsstatus, features gate:as på plan (Free/Pro). **Inte** indrivning av kundfakturor i v1 |
| Backend | ASP.NET Core Web API (.NET 10, C#), clean architecture (Api / Domain / Infrastructure) + testprojekt — som VMPage |
| Persistens | MongoDB via officiella `MongoDB.Driver`. Lokal utveckling mot `mongodb://localhost:27017`, prod mot Atlas. Inga EF-migrations |
| Frontend | React 19 + Vite + TypeScript + TanStack Query + react-router-dom — som VMPage |
| Arbetssätt | **TDD**, Clean Code, **SOLID**, lokal code review + security review före PR, GitFlow + PR. Detaljer i `.specify/memory/constitution.md` |
| CI/CD | **GitHub Actions.** CI på varje PR: build + test + lint för backend och frontend (måste vara grön innan merge). CD: auto-deploy som VMPage — Cloudflare Pages (frontend) + Render (backend) + MongoDB Atlas |
| Språk | Svenska i UI; svensk valuta (SEK) och svenskt datum-/momsformat som standard |

## Roller & behörigheter (v1-utkast — förfinas i spec)

| Åtgärd | Owner | Admin | Member |
|---|:--:|:--:|:--:|
| Hantera prenumeration/plan (Stripe) | ✓ | – | – |
| Bjuda in / ta bort användare, sätta roller | ✓ | ✓ | – |
| Skapa/redigera kunder | ✓ | ✓ | ✓ |
| Skapa/skicka/markera fakturor | ✓ | ✓ | ✓ |
| Se all tenant-data | ✓ | ✓ | ✓ |
| Lämna/radera organisationen | ✓ | – | – |

## Uttryckligen INTE i scope (v1)

- Riktig bokföring/redovisning, momsdeklaration eller integration mot Skatteverket.
- E-faktura/Peppol, BankID, ROT/RUT, påminnelse-/inkassoflöden.
- Indrivning av kundfakturor via Stripe (Stripe används bara för SaaS-prenumerationen).
- Riktiga pengar — Stripe körs **enbart i testläge**.
- Databas-per-tenant eller extern IdP (medvetet bortvalt; kräver constitution-amendment).
- **ABP Framework** som backend-plattform. Övervägt (SwingBy använder ABP Commercial som
  ger multi-tenancy/auth/permissions/feature-flags inbyggt), men bortvalt i v1: ABP
  Commercial kräver betald licens och vi följer VMPage:s lätta handbyggda stack
  (Api/Domain/Infrastructure + egen JWT). Konventioner lånas dock från SwingBy.

## Öppna punkter att reda ut i spec-arbetet (`/speckit-clarify`)

- **Onboarding/signup:** self-service (vem som helst skapar en organisation) eller
  invite-only? Hur kopplas e-post → tenant vid registrering?
- **Tenant-routing:** subdomän per tenant (`acme.app`), path (`/t/acme`) eller enbart
  tenant-claim i JWT utan synlig routing?
- **Fakturamodell:** vilka fält och statusar (utkast → skickad → betald → förfallen →
  krediterad)? Fakturanummerserie per tenant (löpande, ej gissningsbar)? Moms-/radmodell?
- **Plan-gränser:** vad skiljer Free/Pro (antal fakturor/användare/månad), och vilka
  rate-limit-kvoter hör till varje plan?
- **Stripe-detaljer:** vilka produkter/priser i testläge, vilka webhooks
  (`checkout.session.completed`, `customer.subscription.updated/deleted`), och hur
  hanteras utebliven betalning (grace period / nedgradering)?
- **JWT-livscykel:** access-token-livslängd, refresh-tokens, utloggning/återkallning.

## Avgjorda klargöranden (2026-06-26)

- Delad DB med `tenantId` — isolering tvingas i datalagret, inte db-per-tenant.
- Egen JWT (self-issued) med roller i Mongo — ingen extern IdP i v1.
- Stripe i testläge debiterar **tenants prenumeration**, inte kundfakturor.
- Rate limiting sker **per tenant**, kopplat till plan.
- Deploy som VMPage: Cloudflare Pages + Render + Atlas via GitHub Actions.
