import type { ReactNode } from "react";
import { Link, useLocation } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { Button } from "./ui";
import { tokens } from "../theme/tokens";

const stroke = { fill: "none", stroke: "currentColor", strokeWidth: 2, strokeLinecap: "round", strokeLinejoin: "round" } as const;

const LINKS: Array<[string, string, ReactNode]> = [
  ["/", "Översikt", <svg key="i" width="15" height="15" viewBox="0 0 24 24" {...stroke} aria-hidden><rect x="3" y="3" width="7" height="7" rx="1" /><rect x="14" y="3" width="7" height="7" rx="1" /><rect x="3" y="14" width="7" height="7" rx="1" /><rect x="14" y="14" width="7" height="7" rx="1" /></svg>],
  ["/customers", "Kunder", <svg key="i" width="15" height="15" viewBox="0 0 24 24" {...stroke} aria-hidden><circle cx="9" cy="8" r="3.5" /><path d="M3 20c0-3.3 2.7-6 6-6s6 2.7 6 6" /><path d="M16 5a3.5 3.5 0 0 1 0 6.5" /><path d="M17.5 14.5c2.1.8 3.5 2.9 3.5 5.5" /></svg>],
  ["/articles", "Artiklar", <svg key="i" width="15" height="15" viewBox="0 0 24 24" {...stroke} aria-hidden><path d="M3 11 11 3h7a3 3 0 0 1 3 3v7l-8 8-10-10z" /><circle cx="16.5" cy="7.5" r="1.2" /></svg>],
  ["/invoices", "Fakturor", <svg key="i" width="15" height="15" viewBox="0 0 24 24" {...stroke} aria-hidden><path d="M6 2h12v20l-3-2-3 2-3-2-3 2V2z" /><path d="M9 8h6" /><path d="M9 12h6" /></svg>],
  ["/recurring", "Abonnemang", <svg key="i" width="15" height="15" viewBox="0 0 24 24" {...stroke} aria-hidden><path d="M20 11a8 8 0 0 0-14.9-3" /><path d="M4 4v4h4" /><path d="M4 13a8 8 0 0 0 14.9 3" /><path d="M20 20v-4h-4" /></svg>],
  ["/settings", "Inställningar", <svg key="i" width="15" height="15" viewBox="0 0 24 24" {...stroke} aria-hidden><circle cx="12" cy="12" r="3" /><path d="M12 2v3M12 19v3M2 12h3M19 12h3M4.9 4.9l2.1 2.1M17 17l2.1 2.1M19.1 4.9 17 7M7 17l-2.1 2.1" /></svg>],
];

/** Undersidor som hör hemma under en huvudflik (markerar rätt flik som aktiv). */
function isActive(to: string, pathname: string): boolean {
  if (to === "/") return pathname === "/";
  if (to === "/settings") return ["/settings", "/export", "/developer"].some((p) => pathname.startsWith(p));
  return pathname.startsWith(to);
}

/**
 * App-skalet (design-runda 2): kassabokens mörka pärm som sidopanel med registerflikar,
 * innehållet på papperet intill — hela skärmbredden används, med ett lästak på 1440px.
 */
export function Layout({ children, narrow }: { children: ReactNode; narrow?: boolean }) {
  const { user, organization, logout } = useAuth();
  const { pathname } = useLocation();

  return (
    <div className="app-shell">
      <aside className="app-sidebar">
        <Link
          to="/"
          style={{
            fontFamily: tokens.font.display,
            fontSize: "26px",
            fontWeight: 700,
            color: tokens.color.primaryText,
            padding: "24px 20px 20px",
            lineHeight: 1,
          }}
        >
          Faktura<span style={{ color: tokens.color.accent }}>.</span>
        </Link>

        <nav style={{ flex: 1 }}>
          {LINKS.map(([to, label, icon]) => (
            <Link key={to} to={to} className={`tab-link${isActive(to, pathname) ? " tab-link--active" : ""}`}>
              <span style={{ display: "flex", alignItems: "center", gap: "10px" }}>
                {icon}
                {label}
              </span>
            </Link>
          ))}
        </nav>

        <div style={{ padding: "16px 20px 20px", borderTop: "1px dashed rgba(248, 243, 230, 0.25)" }}>
          <div style={{ fontFamily: tokens.font.display, fontWeight: 700, fontSize: tokens.font.size.md, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
            {organization?.name}
          </div>
          <div style={{ color: "rgba(248, 243, 230, 0.62)", fontSize: tokens.font.size.sm, margin: "4px 0 12px", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
            {user?.email} · {user?.role} · {organization?.plan}
          </div>
          <Button
            onClick={logout}
            style={{ width: "100%", background: "transparent", borderColor: "rgba(248, 243, 230, 0.4)", color: tokens.color.primaryText }}
          >
            Logga ut
          </Button>
        </div>
      </aside>

      <main className="app-main">
        <div className={`app-content${narrow ? " app-content--narrow" : ""}`}>{children}</div>
      </main>
    </div>
  );
}
