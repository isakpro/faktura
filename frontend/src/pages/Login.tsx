import { useState, type FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { ApiError } from "../api/client";
import { Button, Card, ErrorText, Field, Input } from "../components/ui";
import { tokens } from "../theme/tokens";

export function Login() {
  const { login } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await login(email, password);
      navigate("/");
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Inloggning misslyckades.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div style={{ maxWidth: 380, margin: "10vh auto" }}>
      <Card>
        <h1 style={{ fontSize: tokens.font.size.xl, marginTop: 0 }}>Logga in</h1>
        <form onSubmit={onSubmit}>
          <Field label="E-post">
            <Input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
          </Field>
          <Field label="Lösenord">
            <Input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required />
          </Field>
          {error && <ErrorText>{error}</ErrorText>}
          <Button type="submit" disabled={busy} style={{ width: "100%", marginTop: tokens.space.sm }}>
            {busy ? "Loggar in…" : "Logga in"}
          </Button>
        </form>
        <p style={{ color: tokens.color.textMuted, fontSize: tokens.font.size.sm, marginBottom: 0 }}>
          Ny här? <Link to="/signup" style={{ color: tokens.color.primary }}>Skapa organisation</Link>
          {" · "}<Link to="/forgot" style={{ color: tokens.color.textMuted }}>Glömt lösenord?</Link>
        </p>
      </Card>
    </div>
  );
}
