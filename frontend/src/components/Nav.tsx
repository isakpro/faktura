import { Link, useLocation } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { Button } from "./ui";
import { tokens } from "../theme/tokens";

export function Nav() {
  const { organization, logout } = useAuth();
  const { pathname } = useLocation();

  const link = (to: string, label: string) => (
    <Link
      to={to}
      style={{
        color: pathname === to ? tokens.color.text : tokens.color.textMuted,
        fontWeight: pathname === to ? 600 : 400,
        marginRight: tokens.space.md,
      }}
    >
      {label}
    </Link>
  );

  return (
    <header
      style={{
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        padding: `${tokens.space.md} 0`,
        borderBottom: `1px solid ${tokens.color.border}`,
        marginBottom: tokens.space.lg,
      }}
    >
      <nav>
        <strong style={{ marginRight: tokens.space.lg }}>{organization?.name}</strong>
        {link("/", "Översikt")}
        {link("/customers", "Kunder")}
        {link("/invoices", "Fakturor")}
      </nav>
      <Button onClick={logout} style={{ background: tokens.color.surfaceAlt }}>
        Logga ut
      </Button>
    </header>
  );
}
