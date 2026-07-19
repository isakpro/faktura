import { useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { api } from "../api/client";
import { Button, Card, Field, Input } from "../components/ui";
import { tokens } from "../theme/tokens";

export function ForgotPassword() {
  const [email, setEmail] = useState("");
  const [sent, setSent] = useState(false);
  const [busy, setBusy] = useState(false);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    try {
      await api.post("/api/auth/forgot-password", { email });
    } finally {
      // Svaret är alltid generiskt — visa samma besked oavsett utfall.
      setSent(true);
      setBusy(false);
    }
  }

  return (
    <div style={{ maxWidth: 380, margin: "10vh auto" }}>
      <Card>
        <h1 style={{ fontSize: tokens.font.size.xl, marginTop: 0 }}>Glömt lösenord</h1>
        {sent ? (
          <p>Om kontot finns har ett mejl med en återställningslänk skickats. Kolla din inkorg.</p>
        ) : (
          <form onSubmit={onSubmit}>
            <Field label="E-post">
              <Input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
            </Field>
            <Button type="submit" disabled={busy} style={{ width: "100%", marginTop: tokens.space.sm }}>
              Skicka återställningslänk
            </Button>
          </form>
        )}
        <p style={{ marginTop: tokens.space.md, fontSize: tokens.font.size.sm }}>
          <Link to="/login" style={{ color: tokens.color.textMuted }}>← Tillbaka till inloggningen</Link>
        </p>
      </Card>
    </div>
  );
}
