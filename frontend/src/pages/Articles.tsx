import { useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, ApiError } from "../api/client";
import type { ArticleDto } from "../api/types";
import { Layout } from "../components/Layout";
import { Badge, Button, Card, ErrorText, Field, Input } from "../components/ui";
import { tokens } from "../theme/tokens";

const VAT_RATES = [25, 12, 6, 0];
const kr = (n: number) => `${n.toFixed(2)} kr`;

const selectStyle = {
  background: tokens.color.surface,
  color: tokens.color.text,
  border: `1px solid ${tokens.color.border}`,
  borderRadius: tokens.radius.sm,
  padding: tokens.space.sm,
};

export function Articles() {
  const qc = useQueryClient();
  const invalidate = () => qc.invalidateQueries({ queryKey: ["articles"] });
  const articles = useQuery({
    queryKey: ["articles", "all"],
    queryFn: () => api.get<ArticleDto[]>("/api/articles?status=all"),
  });

  const [name, setName] = useState("");
  const [sku, setSku] = useState("");
  const [unit, setUnit] = useState("");
  const [price, setPrice] = useState(0);
  const [vat, setVat] = useState(25);
  const [error, setError] = useState<string | null>(null);

  const create = useMutation({
    mutationFn: () =>
      api.post<ArticleDto>("/api/articles", {
        name,
        sku: sku.trim() || null,
        unit: unit.trim() || null,
        unitPriceExclVat: price,
        vatRate: vat,
      }),
    onSuccess: () => {
      setName("");
      setSku("");
      setUnit("");
      setPrice(0);
      setError(null);
      invalidate();
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : "Kunde inte spara artikeln."),
  });

  const archive = useMutation({
    mutationFn: (id: string) => api.post(`/api/articles/${id}/archive`),
    onSuccess: invalidate,
  });

  function onSubmit(e: FormEvent) {
    e.preventDefault();
    create.mutate();
  }

  return (
    <Layout>
      <div className="split-grid">
      <Card>
        <h2 style={{ marginTop: 0, fontSize: tokens.font.size.lg }}>Ny artikel</h2>
        <form onSubmit={onSubmit} style={{ display: "flex", gap: tokens.space.sm, alignItems: "end", flexWrap: "wrap" }}>
          <div style={{ flex: 3, minWidth: 160 }}>
            <Field label="Namn"><Input value={name} onChange={(e) => setName(e.target.value)} required /></Field>
          </div>
          <div style={{ width: 110 }}>
            <Field label="Artikelnr"><Input value={sku} onChange={(e) => setSku(e.target.value)} placeholder="—" /></Field>
          </div>
          <div style={{ width: 90 }}>
            <Field label="Enhet"><Input value={unit} onChange={(e) => setUnit(e.target.value)} placeholder="st" /></Field>
          </div>
          <div style={{ width: 130 }}>
            <Field label="Pris exkl. moms">
              <Input type="number" step="0.01" value={price} onChange={(e) => setPrice(Number(e.target.value))} />
            </Field>
          </div>
          <div style={{ width: 90 }}>
            <Field label="Moms">
              <select value={vat} onChange={(e) => setVat(Number(e.target.value))} style={{ ...selectStyle, width: "100%" }}>
                {VAT_RATES.map((r) => <option key={r} value={r}>{r}%</option>)}
              </select>
            </Field>
          </div>
          <div style={{ marginBottom: tokens.space.md }}>
            <Button type="submit" disabled={create.isPending}>Spara</Button>
          </div>
        </form>
        {error && <ErrorText>{error}</ErrorText>}
      </Card>

      <Card>
        <h2 style={{ marginTop: 0, fontSize: tokens.font.size.lg }}>Artikelregister</h2>
        {articles.isLoading && <p>Laddar…</p>}
        <table style={{ width: "100%", borderCollapse: "collapse" }}>
          <thead>
            <tr style={{ color: tokens.color.textMuted, textAlign: "left", fontSize: tokens.font.size.sm, textTransform: "uppercase", letterSpacing: "0.08em" }}>
              <th style={{ padding: tokens.space.sm }}>Artikelnr</th>
              <th>Namn</th>
              <th>Enhet</th>
              <th style={{ textAlign: "right" }}>Pris exkl. moms</th>
              <th style={{ textAlign: "right" }}>Moms</th>
              <th>Status</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {articles.data?.map((a) => (
              <tr key={a.id} style={{ borderTop: tokens.line.perforated, opacity: a.status === "Archived" ? 0.55 : 1 }}>
                <td style={{ padding: tokens.space.sm, color: tokens.color.textMuted }}>{a.sku ?? "—"}</td>
                <td style={{ fontWeight: 600 }}>{a.name}</td>
                <td>{a.unit ?? "—"}</td>
                <td style={{ textAlign: "right" }}>{kr(a.unitPriceExclVat)}</td>
                <td style={{ textAlign: "right" }}>{a.vatRate}%</td>
                <td><Badge status={a.status} /></td>
                <td style={{ textAlign: "right" }}>
                  {a.status === "Active" && (
                    <Button
                      onClick={() => archive.mutate(a.id)}
                      style={{ background: tokens.color.surfaceAlt, borderColor: tokens.color.surfaceAlt }}
                    >
                      Arkivera
                    </Button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </Card>
      </div>
    </Layout>
  );
}
