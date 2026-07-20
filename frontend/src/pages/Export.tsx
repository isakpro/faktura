import { useState } from "react";
import { openAuthed } from "../api/client";
import { Nav } from "../components/Nav";
import { Button, Card, Field } from "../components/ui";
import { tokens } from "../theme/tokens";

const currentYear = new Date().getFullYear();
const years = Array.from({ length: 6 }, (_, i) => currentYear - i);

/** SIE4-export för bokföring (spec 015): ett räkenskapsårs fakturor som .se-fil. */
export function Export() {
  const [year, setYear] = useState(currentYear);
  const [busy, setBusy] = useState(false);

  async function download() {
    setBusy(true);
    try {
      await openAuthed(`/api/export/sie?year=${year}`);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div style={{ maxWidth: 780, margin: "0 auto", padding: tokens.space.md, display: "grid", gap: tokens.space.lg }}>
      <Nav />
      <Card>
        <h2 style={{ marginTop: 0, fontSize: tokens.font.size.lg }}>Bokföringsexport (SIE4)</h2>
        <p style={{ color: tokens.color.textMuted, fontSize: tokens.font.size.sm, marginTop: `-${tokens.space.sm}` }}>
          Ladda ner räkenskapsårets skickade fakturor och kreditfakturor som en SIE4-fil,
          importerbar i Fortnox, Visma och liknande bokföringsprogram.
        </p>
        <div style={{ display: "flex", gap: tokens.space.sm, alignItems: "end" }}>
          <div style={{ width: 140 }}>
            <Field label="Räkenskapsår">
              <select
                value={year}
                onChange={(e) => setYear(Number(e.target.value))}
                style={{ background: tokens.color.surface, color: tokens.color.text, border: `1px solid ${tokens.color.border}`, borderRadius: tokens.radius.sm, padding: tokens.space.sm, width: "100%" }}
              >
                {years.map((y) => (
                  <option key={y} value={y}>{y}</option>
                ))}
              </select>
            </Field>
          </div>
          <Button onClick={download} disabled={busy}>Ladda ner .se-fil</Button>
        </div>
      </Card>
    </div>
  );
}
