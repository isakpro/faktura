import { useState, type FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { ApiError } from "../api/client";
import { Button, Card, ErrorText, Field, Input } from "../components/ui";
import { tokens } from "../theme/tokens";

export function Signup() {
  const { register } = useAuth();
  const navigate = useNavigate();
  const [organizationName, setOrg] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await register(organizationName, email, password);
      navigate("/");
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Registrering misslyckades.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div style={{ maxWidth: 380, margin: "10vh auto" }}>
      <Card>
        <h1 style={{ fontSize: tokens.font.size.xl, marginTop: 0 }}>Skapa organisation</h1>
        <form onSubmit={onSubmit}>
          <Field label="Organisationsnamn">
            <Input value={organizationName} onChange={(e) => setOrg(e.target.value)} required />
          </Field>
          <Field label="E-post">
            <Input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
          </Field>
          <Field label="Lösenord (minst 8 tecken, bokstäver + siffror)">
            <Input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required />
          </Field>
          {error && <ErrorText>{error}</ErrorText>}
          <Button type="submit" disabled={busy} style={{ width: "100%", marginTop: tokens.space.sm }}>
            {busy ? "Skapar…" : "Skapa organisation"}
          </Button>
        </form>
        <p style={{ color: tokens.color.textMuted, fontSize: tokens.font.size.sm, marginBottom: 0 }}>
          Har du redan ett konto? <Link to="/login" style={{ color: tokens.color.primary }}>Logga in</Link>
        </p>
      </Card>
    </div>
  );
}
