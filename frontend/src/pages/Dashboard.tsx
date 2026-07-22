import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { api } from "../api/client";
import type { DashboardDto } from "../api/types";
import { Layout } from "../components/Layout";
import { RevenueChart } from "../components/RevenueChart";
import { ActivityCard } from "../components/ActivityCard";
import { Badge, Card } from "../components/ui";
import { tokens } from "../theme/tokens";

const kr = (n: number) => `${n.toLocaleString("sv-SE")} kr`;

function StatTile({ label, value, emphasize }: { label: string; value: number; emphasize?: boolean }) {
  return (
    <Card style={{ padding: tokens.space.lg }}>
      <div style={{ color: tokens.color.textMuted, fontSize: tokens.font.size.sm, textTransform: "uppercase", letterSpacing: "0.08em" }}>
        {label}
      </div>
      <div
        style={{
          fontSize: "34px",
          fontWeight: 700,
          marginTop: tokens.space.xs,
          whiteSpace: "nowrap",
          color: emphasize && value > 0 ? tokens.color.accent : tokens.color.text,
        }}
      >
        {kr(value)}
      </div>
    </Card>
  );
}

/** Översikten: nyckeltal, omsättning, senaste fakturor och aktivitet. Administration bor under Inställningar. */
export function Dashboard() {
  const { user } = useAuth();
  const canManage = user?.role === "Owner" || user?.role === "Admin";
  const dashboard = useQuery({ queryKey: ["dashboard"], queryFn: () => api.get<DashboardDto>("/api/dashboard") });

  return (
    <Layout>
      {dashboard.data && (
        <>
          <div className="kpi-grid">
            <StatTile label="Utestående" value={dashboard.data.outstanding} />
            <StatTile label="Förfallet" value={dashboard.data.overdue} emphasize />
            <StatTile label="Betalt i år" value={dashboard.data.paidThisYear} />
          </div>

          <div className="dash-grid">
            <Card>
              <h2 style={{ marginTop: 0, fontSize: tokens.font.size.lg }}>Omsättning per månad</h2>
              <RevenueChart data={dashboard.data.monthlyRevenue} />
            </Card>

            <Card>
              <h2 style={{ marginTop: 0, fontSize: tokens.font.size.lg }}>Senaste fakturor</h2>
              {dashboard.data.recentInvoices.length === 0 && (
                <p style={{ color: tokens.color.textMuted, fontSize: tokens.font.size.sm }}>
                  Inga fakturor ännu — skapa den första under <Link to="/invoices" style={{ color: tokens.color.text, fontWeight: 600 }}>Fakturor</Link>.
                </p>
              )}
              <ul style={{ listStyle: "none", padding: 0, margin: 0 }}>
                {dashboard.data.recentInvoices.map((inv) => (
                  <li key={inv.id} style={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: tokens.space.sm, padding: tokens.space.sm, borderTop: tokens.line.perforated }}>
                    <Link to={`/invoices/${inv.id}`} style={{ color: tokens.color.text, fontWeight: 600, whiteSpace: "nowrap" }}>
                      Faktura {inv.number ?? "(utkast)"}
                    </Link>
                    <span style={{ display: "flex", alignItems: "center", gap: tokens.space.sm }}>
                      <Badge status={inv.status} />
                      <span style={{ fontVariantNumeric: "tabular-nums", whiteSpace: "nowrap" }}>{kr(inv.gross)}</span>
                    </span>
                  </li>
                ))}
              </ul>
            </Card>
          </div>
        </>
      )}

      {canManage && <ActivityCard />}
    </Layout>
  );
}
