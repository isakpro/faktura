import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { api } from "../api/client";
import type { DashboardDto } from "../api/types";
import { Nav } from "../components/Nav";
import { RevenueChart } from "../components/RevenueChart";
import { ActivityCard } from "../components/ActivityCard";
import { Badge, Card } from "../components/ui";
import { tokens } from "../theme/tokens";

const kr = (n: number) => `${n.toLocaleString("sv-SE")} kr`;

function StatTile({ label, value, emphasize }: { label: string; value: number; emphasize?: boolean }) {
  return (
    <Card style={{ flex: 1, minWidth: 180, padding: tokens.space.md }}>
      <div style={{ color: tokens.color.textMuted, fontSize: tokens.font.size.sm, textTransform: "uppercase", letterSpacing: "0.08em" }}>
        {label}
      </div>
      <div
        style={{
          fontSize: tokens.font.size.xl,
          fontWeight: 700,
          fontVariantNumeric: "tabular-nums",
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
  const { user, organization } = useAuth();
  const canManage = user?.role === "Owner" || user?.role === "Admin";
  const dashboard = useQuery({ queryKey: ["dashboard"], queryFn: () => api.get<DashboardDto>("/api/dashboard") });

  return (
    <div style={{ maxWidth: 780, margin: "0 auto", padding: tokens.space.md, display: "grid", gap: tokens.space.lg }}>
      <Nav />
      <span style={{ color: tokens.color.textMuted, fontSize: tokens.font.size.sm, marginTop: `-${tokens.space.md}` }}>
        {user?.email} · {user?.role} · Plan: {organization?.plan}
      </span>

      {dashboard.data && (
        <>
          <div style={{ display: "flex", gap: tokens.space.md, flexWrap: "wrap" }}>
            <StatTile label="Utestående" value={dashboard.data.outstanding} />
            <StatTile label="Förfallet" value={dashboard.data.overdue} emphasize />
            <StatTile label="Betalt i år" value={dashboard.data.paidThisYear} />
          </div>

          <Card>
            <h2 style={{ marginTop: 0, fontSize: tokens.font.size.lg }}>Omsättning per månad</h2>
            <RevenueChart data={dashboard.data.monthlyRevenue} />
          </Card>

          {dashboard.data.recentInvoices.length > 0 && (
            <Card>
              <h2 style={{ marginTop: 0, fontSize: tokens.font.size.lg }}>Senaste fakturor</h2>
              <ul style={{ listStyle: "none", padding: 0, margin: 0 }}>
                {dashboard.data.recentInvoices.map((inv) => (
                  <li key={inv.id} style={{ display: "flex", justifyContent: "space-between", alignItems: "center", padding: tokens.space.sm, borderTop: tokens.line.perforated }}>
                    <Link to={`/invoices/${inv.id}`} style={{ color: tokens.color.text, fontWeight: 600 }}>
                      Faktura {inv.number ?? "(utkast)"}
                    </Link>
                    <span style={{ display: "flex", alignItems: "center", gap: tokens.space.md }}>
                      <Badge status={inv.status} />
                      <span style={{ fontVariantNumeric: "tabular-nums" }}>{kr(inv.gross)}</span>
                    </span>
                  </li>
                ))}
              </ul>
            </Card>
          )}
        </>
      )}

      {canManage && <ActivityCard />}
    </div>
  );
}
