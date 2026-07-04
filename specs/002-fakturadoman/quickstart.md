# Quickstart (Phase 1): Fakturadomänen

Bygger på 001-skelettets uppsättning (se `specs/001-saas-skelett/quickstart.md`) — samma backend,
Mongo, JWT och frontend. 002 lägger till kunder/fakturor/PDF. Inga nya hemligheter krävs.

## Nytt beroende
- **QuestPDF** (NuGet) i `Faktura.Infrastructure` för server-side PDF.

## Kör (samma som 001)
```bash
cd backend && dotnet run --project src/Faktura.Api      # http://localhost:5080
cd frontend && npm install && npm run dev               # http://localhost:5173
```

## Testa
```bash
cd backend && dotnet test    # domän (moms/avrundning/kredit/lås) + integration + concurrency
```

## Röktest (manuellt)
1. Logga in (konto från 001). Skapa en **kund** under Kunder.
2. Skapa ett **fakturautkast** för kunden med två rader (25 % och 12 %) → verifiera netto,
   moms per sats och brutto.
3. **Skicka** fakturan → den får nästa lediga nummer, blir låst, får förfallodatum.
4. Skicka ytterligare en → numret är +1 (obruten serie).
5. **Markera betald** → status Betald. Skapa en till, låt förfallodatum passera → visas Förfallen.
6. **Kreditera** en skickad faktura → kreditdokument med eget nummer och negativa belopp.
7. Hämta **PDF** för en skickad faktura → innehåller nummer, datum, kund, rader, moms per sats, total.
