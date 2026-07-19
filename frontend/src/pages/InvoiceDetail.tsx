import { Link, useParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { api, openAuthed } from "../api/client";
import type { CustomerDto, InvoiceDto } from "../api/types";
import { Nav } from "../components/Nav";
import { Badge, Button, Card } from "../components/ui";
import { tokens } from "../theme/tokens";

interface HistoryEntry {
  recipient: string;
  subject: string;
  status: string;
  error?: string | null;
  sentAt: string;
  sequence?: number;
  type?: string;
}

const kr = (n: number) => `${n.toLocaleString("sv-SE", { minimumFractionDigits: 2 })} kr`;
const when = (iso: string) =>
  new Date(iso).toLocaleString("sv-SE", { year: "numeric", month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" });

function HistoryList({ title, items }: { title: string; items: HistoryEntry[] | undefined }) {
  if (!items || items.length === 0) return null;
  return (
    <Card>
      <h2 style={{ marginTop: 0, fontSize: tokens.font.size.lg }}>{title}</h2>
      <ul style={{ listStyle: "none", padding: 0, margin: 0 }}>
        {items.map((e, i) => (
          <li key={i} style={{ display: "flex", justifyContent: "space-between", gap: tokens.space.md, padding: tokens.space.sm, borderTop: tokens.line.perforated, fontSize: tokens.font.size.sm }}>
            <span>
              {e.type === "Automatic" ? "Automatisk" : e.sequence ? `Påminnelse ${e.sequence}` : "Utskick"} till{" "}
              <strong>{e.recipient || "—"}</strong>
              {e.status === "Failed" && <span style={{ color: tokens.color.danger }}> · misslyckades</span>}
            </span>
            <span style={{ color: tokens.color.textMuted, whiteSpace: "nowrap" }}>{when(e.sentAt)}</span>
          </li>
        ))}
      </ul>
    </Card>
  );
}

export function InvoiceDetail() {
  const { id } = useParams<{ id: string }>();
  const invoice = useQuery({ queryKey: ["invoice", id], queryFn: () => api.get<InvoiceDto>(`/api/invoices/${id}`) });
  const customers = useQuery({ queryKey: ["customers"], queryFn: () => api.get<CustomerDto[]>("/api/customers") });
  const emails = useQuery({ queryKey: ["invoice-emails", id], queryFn: () => api.get<HistoryEntry[]>(`/api/invoices/${id}/emails`) });
  const reminders = useQuery({ queryKey: ["invoice-reminders", id], queryFn: () => api.get<HistoryEntry[]>(`/api/invoices/${id}/reminders`) });

  const inv = invoice.data;
  const customerName = customers.data?.find((c) => c.id === inv?.customerId)?.name ?? "";

  return (
    <div style={{ maxWidth: 780, margin: "0 auto", padding: tokens.space.md, display: "grid", gap: tokens.space.lg }}>
      <Nav />
      <Link to="/invoices" style={{ color: tokens.color.textMuted, fontSize: tokens.font.size.sm }}>← Alla fakturor</Link>

      {inv && (
        <>
          <Card>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "start" }}>
              <div>
                <h1 style={{ margin: 0, fontSize: tokens.font.size.xl }}>
                  {inv.type === "CreditNote" ? "Kreditfaktura" : "Faktura"} {inv.number ?? "(utkast)"}
                </h1>
                <p style={{ color: tokens.color.textMuted, margin: `${tokens.space.xs} 0 0` }}>
                  {customerName}
                  {inv.invoiceDate && ` · ${inv.invoiceDate}`}
                  {inv.dueDate && ` · förfaller ${inv.dueDate}`}
                  {inv.paidDate && ` · betald ${inv.paidDate}`}
                </p>
              </div>
              <div style={{ display: "flex", gap: tokens.space.sm, alignItems: "center" }}>
                <Badge status={inv.status} />
                {inv.number != null && (
                  <Button onClick={() => openAuthed(`/api/invoices/${inv.id}/pdf`)} style={{ background: tokens.color.surfaceAlt, borderColor: tokens.color.surfaceAlt }}>
                    PDF
                  </Button>
                )}
              </div>
            </div>

            <table style={{ width: "100%", borderCollapse: "collapse", marginTop: tokens.space.lg }}>
              <thead>
                <tr style={{ color: tokens.color.textMuted, textAlign: "left", fontSize: tokens.font.size.sm, textTransform: "uppercase", letterSpacing: "0.08em" }}>
                  <th style={{ padding: tokens.space.sm }}>Beskrivning</th>
                  <th style={{ textAlign: "right" }}>Antal</th>
                  <th style={{ textAlign: "right" }}>À-pris</th>
                  <th style={{ textAlign: "right" }}>Moms</th>
                  <th style={{ textAlign: "right" }}>Netto</th>
                </tr>
              </thead>
              <tbody>
                {inv.lines.map((l, i) => (
                  <tr key={i} style={{ borderTop: tokens.line.perforated }}>
                    <td style={{ padding: tokens.space.sm }}>{l.description}</td>
                    <td style={{ textAlign: "right" }}>{l.quantity}{l.unit ? ` ${l.unit}` : ""}</td>
                    <td style={{ textAlign: "right" }}>{kr(l.unitPriceExclVat)}</td>
                    <td style={{ textAlign: "right" }}>{l.vatRate}%</td>
                    <td style={{ textAlign: "right" }}>{kr(l.net)}</td>
                  </tr>
                ))}
              </tbody>
            </table>

            <div style={{ marginTop: tokens.space.lg, marginLeft: "auto", width: "fit-content", textAlign: "right" }}>
              <div style={{ color: tokens.color.textMuted }}>Netto {kr(inv.totals.net)}</div>
              {inv.totals.vatByRate.map((v) => (
                <div key={v.rate} style={{ color: tokens.color.textMuted }}>Moms {v.rate}% {kr(v.vat)}</div>
              ))}
              <div style={{ fontSize: tokens.font.size.lg, fontWeight: 700, borderTop: `2px solid ${tokens.color.primary}`, marginTop: tokens.space.xs, paddingTop: tokens.space.xs }}>
                Att betala {kr(inv.totals.gross)}
              </div>
            </div>
          </Card>

          <HistoryList title="E-postutskick" items={emails.data} />
          <HistoryList title="Påminnelser" items={reminders.data} />
        </>
      )}
    </div>
  );
}
