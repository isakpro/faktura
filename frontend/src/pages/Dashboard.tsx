import { useEffect, useState, type ReactNode } from "react";
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

/** Räknar upp till målvärdet på ~0,7 s (ease-out) — siffror som "bokförs" i stället för att bara stå där. */
function useCountUp(target: number, duration = 700): number {
  const [value, setValue] = useState(0);
  useEffect(() => {
    if (window.matchMedia?.("(prefers-reduced-motion: reduce)").matches) {
      setValue(target);
      return;
    }
    let raf: number;
    const start = performance.now();
    const tick = (t: number) => {
      const p = Math.min((t - start) / duration, 1);
      setValue(Math.round(target * (1 - Math.pow(1 - p, 3))));
      if (p < 1) raf = requestAnimationFrame(tick);
    };
    raf = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(raf);
  }, [target, duration]);
  return value;
}

const icons = {
  clock: (
    <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden>
      <circle cx="12" cy="12" r="9" /><path d="M12 7v5l3 3" />
    </svg>
  ),
  alert: (
    <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden>
      <path d="M12 3 2.5 20h19L12 3z" /><path d="M12 10v4" /><path d="M12 17.5v.5" />
    </svg>
  ),
  check: (
    <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden>
      <circle cx="12" cy="12" r="9" /><path d="m8.5 12.5 2.5 2.5 4.5-5" />
    </svg>
  ),
};

function StatTile({ label, value, icon, tone }: { label: string; value: number; icon: ReactNode; tone: string }) {
  const shown = useCountUp(value);
  return (
    <Card style={{ padding: tokens.space.lg, display: "flex", justifyContent: "space-between", alignItems: "center", gap: tokens.space.md }}>
      <div style={{ minWidth: 0 }}>
        <div style={{ color: tokens.color.textMuted, fontSize: tokens.font.size.sm, textTransform: "uppercase", letterSpacing: "0.08em" }}>
          {label}
        </div>
        <div style={{ fontSize: "34px", fontWeight: 700, marginTop: tokens.space.xs, whiteSpace: "nowrap", color: value > 0 ? tone : tokens.color.text }}>
          {kr(shown)}
        </div>
      </div>
      <div
        aria-hidden
        style={{
          width: 46,
          height: 46,
          flexShrink: 0,
          display: "grid",
          placeItems: "center",
          borderRadius: tokens.radius.lg,
          color: tone,
          background: `color-mix(in srgb, ${tone} 12%, transparent)`,
          border: `1px dashed color-mix(in srgb, ${tone} 35%, transparent)`,
          transform: "rotate(-2deg)",
        }}
      >
        {icon}
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
            <StatTile label="Utestående" value={dashboard.data.outstanding} icon={icons.clock} tone={tokens.color.text} />
            <StatTile label="Förfallet" value={dashboard.data.overdue} icon={icons.alert} tone={tokens.color.accent} />
            <StatTile label="Betalt i år" value={dashboard.data.paidThisYear} icon={icons.check} tone={tokens.color.success} />
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
