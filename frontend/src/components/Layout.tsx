import type { ReactNode } from "react";
import { Link, useLocation } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { Button } from "./ui";
import { tokens } from "../theme/tokens";

const LINKS: Array<[string, string]> = [
  ["/", "Översikt"],
  ["/customers", "Kunder"],
  ["/articles", "Artiklar"],
  ["/invoices", "Fakturor"],
  ["/recurring", "Abonnemang"],
  ["/settings", "Inställningar"],
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
          {LINKS.map(([to, label]) => (
            <Link key={to} to={to} className={`tab-link${isActive(to, pathname) ? " tab-link--active" : ""}`}>
              {label}
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
