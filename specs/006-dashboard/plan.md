# Implementation Plan: Dashboard (006)

**Branch**: `feature/006-dashboard` · **Spec**: [spec.md](spec.md) · Constitution check: PASS
(ren läsvy; beräkning i domänlagret, tenant-isolerat via befintliga repos, kontrakt nedan, TDD).

## Beslut

- **Beräkning i domänen, inte i Mongo-aggregation:** `DashboardCalculator` (ren statisk klass)
  räknar KPI:er/månadsserie från `Invoice`-aggregat — återanvänder `InvoiceCalculator`s totals
  så definitionerna aldrig divergerar från fakturans egna summor. Skalar gott för v1-volymer;
  Mongo-pipeline är ett senare optimeringssteg.
- **Kontrakt:** `GET /api/dashboard` → `{ outstanding, overdue, paidThisYear,
  monthlyRevenue: [{ year, month, gross } × 12 (äldst→nyast)], recentInvoices: [≤5 InvoiceListItemDto] }`.
- **Graf utan bibliotek:** ren token-driven SVG/div-stapelgraf i Huvudboken-stil (bläckstaplar
  på papper, tabular-nums) — inga nya frontend-beroenden.

## Struktur (tillägg)

```text
backend/src/Faktura.Domain/Invoicing/DashboardCalculator.cs   (TDD-kärna)
backend/src/Faktura.Api/Features/Invoicing/DashboardContracts.cs + DashboardService.cs + endpoint
frontend/src/pages/Dashboard.tsx (nyckeltalskort + graf + senaste fakturor)
```

## Tasks

- [x] T001 [Domain] `DashboardCalculator` + domäntester (KPI-mix, kredit exkluderas, 12-punktsserie, årsgräns)
- [x] T002 [Api] `DashboardService` + `GET /api/dashboard` + integrationstester (inkl. isolering)
- [x] T003 [Frontend] Nyckeltalskort + SVG-graf + senaste-lista i Huvudboken-stil; vitest/build/lint gröna
- [x] T004 PR mot `develop` när grönt
