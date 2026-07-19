import { useState, type FormEvent } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { api, ApiError } from "../api/client";
import { Button, Card, ErrorText, Field, Input } from "../components/ui";
import { tokens } from "../theme/tokens";

export function ResetPassword() {
  const { token } = useParams<{ token: string }>();
  const navigate = useNavigate();
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await api.post("/api/auth/reset-password", { token, password });
      navigate("/login");
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Kunde inte återställa lösenordet.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div style={{ maxWidth: 380, margin: "10vh auto" }}>
      <Card>
        <h1 style={{ fontSize: tokens.font.size.xl, marginTop: 0 }}>Välj nytt lösenord</h1>
        <form onSubmit={onSubmit}>
          <Field label="Nytt lösenord (minst 8 tecken, bokstäver + siffror)">
            <Input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required />
          </Field>
          <Button type="submit" disabled={busy} style={{ width: "100%", marginTop: tokens.space.sm }}>
            Byt lösenord
          </Button>
        </form>
        {error && <ErrorText>{error}</ErrorText>}
        <p style={{ marginTop: tokens.space.md, fontSize: tokens.font.size.sm }}>
          <Link to="/login" style={{ color: tokens.color.textMuted }}>← Tillbaka till inloggningen</Link>
        </p>
      </Card>
    </div>
  );
}
