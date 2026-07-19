import { Link, useLocation } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { Button } from "./ui";
import { tokens } from "../theme/tokens";

export function Nav() {
  const { organization, logout } = useAuth();
  const { pathname } = useLocation();

  const link = (to: string, label: string) => {
    const active = pathname === to;
    return (
      <Link
        to={to}
        style={{
          color: active ? tokens.color.text : tokens.color.textMuted,
          fontSize: tokens.font.size.sm,
          fontWeight: 700,
          textTransform: "uppercase",
          letterSpacing: "0.1em",
          paddingBottom: "6px",
          borderBottom: active ? `3px solid ${tokens.color.accent}` : "3px solid transparent",
          marginRight: tokens.space.lg,
        }}
      >
        {label}
      </Link>
    );
  };

  return (
    <header
      style={{
        display: "flex",
        alignItems: "flex-end",
        justifyContent: "space-between",
        padding: `${tokens.space.md} 0 0 0`,
        borderBottom: tokens.line.perforated,
        marginBottom: tokens.space.lg,
      }}
    >
      <div style={{ display: "flex", alignItems: "flex-end", gap: tokens.space.xl }}>
        <Link
          to="/"
          style={{
            fontFamily: tokens.font.display,
            fontSize: tokens.font.size.xl,
            fontWeight: 700,
            color: tokens.color.text,
            lineHeight: 1,
            paddingBottom: "4px",
          }}
        >
          Faktura<span style={{ color: tokens.color.accent }}>.</span>
        </Link>
        <nav style={{ paddingBottom: "6px" }}>
          {link("/", "Översikt")}
          {link("/customers", "Kunder")}
          {link("/articles", "Artiklar")}
          {link("/invoices", "Fakturor")}
          {link("/recurring", "Abonnemang")}
          {link("/settings", "Inställningar")}
        </nav>
      </div>
      <div style={{ display: "flex", alignItems: "center", gap: tokens.space.md, paddingBottom: "8px" }}>
        <strong style={{ fontFamily: tokens.font.display, fontSize: tokens.font.size.md, whiteSpace: "nowrap", maxWidth: 220, overflow: "hidden", textOverflow: "ellipsis" }}>
          {organization?.name}
        </strong>
        <Button onClick={logout} style={{ background: tokens.color.surfaceAlt, borderColor: tokens.color.surfaceAlt }}>
          Logga ut
        </Button>
      </div>
    </header>
  );
}
