import { useState, type FormEvent } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { ApiError } from "../api/client";
import { Button, Card, ErrorText, Field, Input } from "../components/ui";
import { tokens } from "../theme/tokens";

export function AcceptInvite() {
  const { token = "" } = useParams();
  const { acceptInvitation } = useAuth();
  const navigate = useNavigate();
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await acceptInvitation(token, password);
      navigate("/");
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Kunde inte acceptera inbjudan.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div style={{ maxWidth: 380, margin: "10vh auto" }}>
      <Card>
        <h1 style={{ fontSize: tokens.font.size.xl, marginTop: 0 }}>Acceptera inbjudan</h1>
        <p style={{ color: tokens.color.textMuted, fontSize: tokens.font.size.sm }}>Välj ett lösenord för ditt konto.</p>
        <form onSubmit={onSubmit}>
          <Field label="Lösenord (minst 8 tecken, bokstäver + siffror)">
            <Input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required />
          </Field>
          {error && <ErrorText>{error}</ErrorText>}
          <Button type="submit" disabled={busy} style={{ width: "100%" }}>
            {busy ? "Ansluter…" : "Gå med"}
          </Button>
        </form>
      </Card>
    </div>
  );
}
