import { useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, ApiError, openAuthed } from "../api/client";
import type { ArticleDto, CustomerDto, InvoiceDto, InvoiceLineInput, InvoiceListItemDto } from "../api/types";
import { Nav } from "../components/Nav";
import { Badge, Button, Card, ErrorText, Field, Input } from "../components/ui";
import { tokens } from "../theme/tokens";

const VAT_RATES = [25, 12, 6, 0];
const emptyLine = (): InvoiceLineInput => ({ description: "", quantity: 1, unitPriceExclVat: 0, vatRate: 25, unit: null });
const kr = (n: number) => `${n.toFixed(2)} kr`;

const selectStyle = {
  background: tokens.color.bg,
  color: tokens.color.text,
  border: `1px solid ${tokens.color.border}`,
  borderRadius: tokens.radius.sm,
  padding: tokens.space.sm,
};

export function Invoices() {
  const qc = useQueryClient();
  const invalidate = () => qc.invalidateQueries({ queryKey: ["invoices"] });

  const customers = useQuery({ queryKey: ["customers"], queryFn: () => api.get<CustomerDto[]>("/api/customers") });
  const invoices = useQuery({ queryKey: ["invoices"], queryFn: () => api.get<InvoiceListItemDto[]>("/api/invoices") });
  const articles = useQuery({ queryKey: ["articles", "active"], queryFn: () => api.get<ArticleDto[]>("/api/articles") });

  const [customerId, setCustomerId] = useState("");
  const [lines, setLines] = useState<InvoiceLineInput[]>([emptyLine()]);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const email = useMutation({
    mutationFn: ({ id, recipient }: { id: string; recipient: string | null }) =>
      api.post(`/api/invoices/${id}/email`, { recipient }),
    onSuccess: () => setNotice("Fakturan mejlades till kunden."),
    onError: (err) => setNotice(err instanceof ApiError ? `Kunde inte mejla: ${err.message}` : "Kunde inte mejla."),
  });

  function mailInvoice(id: string) {
    const r = window.prompt("Mottagare (lämna tomt för kundens e-post):", "");
    if (r === null) return;
    email.mutate({ id, recipient: r.trim() || null });
  }

  const remind = useMutation({
    mutationFn: (id: string) => api.post(`/api/invoices/${id}/remind`, { recipient: null }),
    onSuccess: () => setNotice("Betalningspåminnelse skickad."),
    onError: (err) => setNotice(err instanceof ApiError ? `Kunde inte påminna: ${err.message}` : "Kunde inte påminna."),
  });

  const create = useMutation({
    mutationFn: () => api.post<InvoiceDto>("/api/invoices", { customerId, lines }),
    onSuccess: () => {
      setLines([emptyLine()]);
      setError(null);
      invalidate();
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : "Kunde inte skapa faktura."),
  });

  const action = useMutation({
    mutationFn: ({ id, path, body }: { id: string; path: string; body?: unknown }) =>
      api.post(`/api/invoices/${id}/${path}`, body),
    onSuccess: invalidate,
    onError: (err) => setError(err instanceof ApiError ? err.message : "Åtgärden misslyckades."),
  });

  const customerName = (id: string) => customers.data?.find((c) => c.id === id)?.name ?? id;
  const today = new Date().toISOString().slice(0, 10);

  function updateLine(i: number, patch: Partial<InvoiceLineInput>) {
    setLines((ls) => ls.map((l, idx) => (idx === i ? { ...l, ...patch } : l)));
  }

  /** Förifyller raden från vald artikel (snapshot — värdena kopieras och kan ändras fritt). */
  function applyArticle(i: number, articleId: string) {
    const article = articles.data?.find((a) => a.id === articleId);
    if (!article) return;
    updateLine(i, {
      description: article.name,
      unitPriceExclVat: article.unitPriceExclVat,
      vatRate: article.vatRate,
      unit: article.unit ?? null,
    });
  }

  function onCreate(e: FormEvent) {
    e.preventDefault();
    if (!customerId) {
      setError("Välj en kund.");
      return;
    }
    create.mutate();
  }

  return (
    <div style={{ maxWidth: 900, margin: "0 auto", padding: tokens.space.md }}>
      <Nav />

      <Card style={{ marginBottom: tokens.space.lg }}>
        <h2 style={{ marginTop: 0, fontSize: tokens.font.size.lg }}>Nytt fakturautkast</h2>
        <form onSubmit={onCreate}>
          <Field label="Kund">
            <select value={customerId} onChange={(e) => setCustomerId(e.target.value)} style={{ ...selectStyle, width: "100%" }}>
              <option value="">— välj kund —</option>
              {customers.data?.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
            </select>
          </Field>

          {lines.map((line, i) => (
            <div key={i} style={{ display: "flex", gap: tokens.space.sm, marginBottom: tokens.space.sm, alignItems: "center" }}>
              {(articles.data?.length ?? 0) > 0 && (
                <select value="" onChange={(e) => applyArticle(i, e.target.value)} style={{ ...selectStyle, width: 130 }}>
                  <option value="">Artikel…</option>
                  {articles.data?.map((a) => (
                    <option key={a.id} value={a.id}>{a.sku ? `[${a.sku}] ` : ""}{a.name}</option>
                  ))}
                </select>
              )}
              <Input placeholder="Beskrivning" value={line.description} onChange={(e) => updateLine(i, { description: e.target.value })} style={{ flex: 3 }} />
              <Input type="number" placeholder="Antal" value={line.quantity} onChange={(e) => updateLine(i, { quantity: Number(e.target.value) })} style={{ flex: 1 }} />
              <Input placeholder="Enhet" value={line.unit ?? ""} onChange={(e) => updateLine(i, { unit: e.target.value || null })} style={{ width: 70 }} />
              <Input type="number" placeholder="À-pris" value={line.unitPriceExclVat} onChange={(e) => updateLine(i, { unitPriceExclVat: Number(e.target.value) })} style={{ flex: 1 }} />
              <select value={line.vatRate} onChange={(e) => updateLine(i, { vatRate: Number(e.target.value) })} style={selectStyle}>
                {VAT_RATES.map((r) => <option key={r} value={r}>{r}%</option>)}
              </select>
            </div>
          ))}
          <div style={{ display: "flex", gap: tokens.space.sm, marginTop: tokens.space.sm }}>
            <Button type="button" onClick={() => setLines((ls) => [...ls, emptyLine()])} style={{ background: tokens.color.surfaceAlt }}>
              + Rad
            </Button>
            <Button type="submit" disabled={create.isPending}>Skapa utkast</Button>
          </div>
        </form>
        {error && <ErrorText>{error}</ErrorText>}
      </Card>

      <Card>
        <h2 style={{ marginTop: 0, fontSize: tokens.font.size.lg }}>Fakturor</h2>
        {notice && <p style={{ color: tokens.color.success, fontSize: tokens.font.size.sm }}>{notice}</p>}
        {invoices.isLoading && <p>Laddar…</p>}
        <table style={{ width: "100%", borderCollapse: "collapse" }}>
          <thead>
            <tr style={{ color: tokens.color.textMuted, textAlign: "left", fontSize: tokens.font.size.sm }}>
              <th style={{ padding: tokens.space.sm }}>Nr</th>
              <th>Kund</th>
              <th>Status</th>
              <th style={{ textAlign: "right" }}>Belopp</th>
              <th style={{ textAlign: "right" }}>Åtgärder</th>
            </tr>
          </thead>
          <tbody>
            {invoices.data?.map((inv) => (
              <tr key={inv.id} style={{ borderTop: `1px solid ${tokens.color.border}` }}>
                <td style={{ padding: tokens.space.sm }}>{inv.number ?? "—"}</td>
                <td>{customerName(inv.customerId)}</td>
                <td><Badge status={inv.status} /></td>
                <td style={{ textAlign: "right" }}>{kr(inv.gross)}</td>
                <td style={{ textAlign: "right", whiteSpace: "nowrap" }}>
                  {inv.status === "Draft" && (
                    <Button onClick={() => action.mutate({ id: inv.id, path: "send" })} style={{ marginLeft: 4 }}>Skicka</Button>
                  )}
                  {(inv.status === "Sent" || inv.status === "Overdue") && (
                    <Button onClick={() => action.mutate({ id: inv.id, path: "mark-paid", body: { paidDate: today } })} style={{ marginLeft: 4 }}>Betald</Button>
                  )}
                  {(inv.status === "Sent" || inv.status === "Overdue" || inv.status === "Paid") && (
                    <Button onClick={() => action.mutate({ id: inv.id, path: "credit" })} style={{ marginLeft: 4, background: tokens.color.surfaceAlt }}>Kreditera</Button>
                  )}
                  {inv.number != null && (
                    <Button onClick={() => openAuthed(`/api/invoices/${inv.id}/pdf`)} style={{ marginLeft: 4, background: tokens.color.surfaceAlt }}>PDF</Button>
                  )}
                  {inv.number != null && (
                    <Button onClick={() => mailInvoice(inv.id)} style={{ marginLeft: 4, background: tokens.color.surfaceAlt }}>Mejla</Button>
                  )}
                  {inv.status === "Overdue" && (
                    <Button onClick={() => remind.mutate(inv.id)} style={{ marginLeft: 4 }}>Påminn</Button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </Card>
    </div>
  );
}
