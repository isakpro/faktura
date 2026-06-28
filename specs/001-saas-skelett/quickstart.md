# Quickstart (Phase 1): SaaS-skelett

Hur man kör 001 lokalt (gäller när koden landat i Phase 2).

## Krav
- .NET 10 SDK
- Node 20+
- MongoDB lokalt på `mongodb://localhost:27017` (eller `docker run -p 27017:27017 mongo`)
- Stripe-konto i **testläge** + Stripe CLI (för webhooks lokalt)

## Miljövariabler (backend)
ASP.NET använder `__` för nästlade nycklar. Skapa `backend/src/Faktura.Api/appsettings.Development.json`
(gitignorerad) eller sätt env:
```
Mongo__ConnectionString = mongodb://localhost:27017
Mongo__Database         = faktura
Jwt__SigningKey         = <slumpad lång hemlighet>
Jwt__Issuer             = faktura
Jwt__AccessTokenMinutes = 15
Stripe__SecretKey       = sk_test_…
Stripe__WebhookSecret   = whsec_…   (från `stripe listen`)
Stripe__ProPriceId      = price_…   (testpris för Pro)
Cors__AllowedOrigins    = http://localhost:5173
```

## Frontend-env
`frontend/.env.local`:
```
VITE_API_BASE_URL = http://localhost:5173/api   # eller backend-URL, t.ex. http://localhost:5080
```

## Kör
```bash
# 1) Backend
cd backend
dotnet restore
dotnet run --project src/Faktura.Api      # API på http://localhost:5080

# 2) Stripe-webhooks (separat terminal)
stripe listen --forward-to localhost:5080/api/billing/webhook
# kopiera whsec_… till Stripe__WebhookSecret

# 3) Frontend
cd frontend
npm install
npm run dev                                # http://localhost:5173
```

## Testa
```bash
cd backend && dotnet test                  # domän + integrationstester (Mongo via Testcontainers)
cd frontend && npm test                    # Vitest
```

## Röktest (manuellt)
1. Registrera org A (Owner) på /signup → inloggad.
2. Bjud in en Member; logga in som den → kan ej hantera medlemmar/plan (403).
3. Registrera org B → bekräfta att B inte ser A:s medlemmar.
4. Owner i A: starta Pro-checkout (Stripe testkort `4242 4242 4242 4242`) → plan blir `pro`.
5. Överskrid kvoten på Free → `429` med `Retry-After`.
