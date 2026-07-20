import { Navigate, Route, Routes } from "react-router-dom";
import type { ReactNode } from "react";
import { useAuth } from "./auth/AuthContext";
import { Login } from "./pages/Login";
import { Signup } from "./pages/Signup";
import { Dashboard } from "./pages/Dashboard";
import { AcceptInvite } from "./pages/AcceptInvite";
import { Customers } from "./pages/Customers";
import { Articles } from "./pages/Articles";
import { Invoices } from "./pages/Invoices";
import { Recurring } from "./pages/Recurring";
import { InvoiceDetail } from "./pages/InvoiceDetail";
import { ForgotPassword } from "./pages/ForgotPassword";
import { ResetPassword } from "./pages/ResetPassword";
import { Settings } from "./pages/Settings";
import { PublicInvoice } from "./pages/PublicInvoice";
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
      <Route path="/forgot" element={<ForgotPassword />} />
      <Route path="/reset/:token" element={<ResetPassword />} />
      <Route path="/f/:token" element={<PublicInvoice />} />
      <Route path="/" element={<Protected><Dashboard /></Protected>} />
      <Route path="/customers" element={<Protected><Customers /></Protected>} />
      <Route path="/articles" element={<Protected><Articles /></Protected>} />
      <Route path="/invoices" element={<Protected><Invoices /></Protected>} />
      <Route path="/recurring" element={<Protected><Recurring /></Protected>} />
      <Route path="/invoices/:id" element={<Protected><InvoiceDetail /></Protected>} />
      <Route path="/settings" element={<Protected><Settings /></Protected>} />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
