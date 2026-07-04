import { Navigate, Route, Routes } from "react-router-dom";
import type { ReactNode } from "react";
import { useAuth } from "./auth/AuthContext";
import { Login } from "./pages/Login";
import { Signup } from "./pages/Signup";
import { Dashboard } from "./pages/Dashboard";
import { AcceptInvite } from "./pages/AcceptInvite";
import { Customers } from "./pages/Customers";
import { Invoices } from "./pages/Invoices";
import { tokens } from "./theme/tokens";

function Protected({ children }: { children: ReactNode }) {
  const { status } = useAuth();
  if (status === "loading") return <p style={{ padding: tokens.space.xl }}>Laddar…</p>;
  if (status === "anon") return <Navigate to="/login" replace />;
  return <>{children}</>;
}

export default function App() {
  const { status } = useAuth();
  return (
    <Routes>
      <Route path="/login" element={status === "authed" ? <Navigate to="/" replace /> : <Login />} />
      <Route path="/signup" element={status === "authed" ? <Navigate to="/" replace /> : <Signup />} />
      <Route path="/accept/:token" element={<AcceptInvite />} />
      <Route path="/" element={<Protected><Dashboard /></Protected>} />
      <Route path="/customers" element={<Protected><Customers /></Protected>} />
      <Route path="/invoices" element={<Protected><Invoices /></Protected>} />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
