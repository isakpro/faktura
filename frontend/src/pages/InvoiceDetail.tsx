import { useState, type FormEvent } from "react";
import { Link, useParams } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, ApiError, openAuthed } from "../api/client";
import type { CustomerDto, InvoiceDto, PaymentDto, ShareLinkDto } from "../api/types";
import { Nav } from "../components/Nav";
import { Badge, Button, Card, ErrorText, Field, Input } from "../components/ui";
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
                  {inv.ocrNumber && (
                    <> · OCR <strong style={{ color: tokens.color.text, fontVariantNumeric: "tabular-nums" }}>{inv.ocrNumber}</strong></>
                  )}
                </p>
              </div>
              <div style={{ display: "flex", gap: tokens.space.sm, alignItems: "center" }}>
                <Badge status={inv.status} />
                {inv.number != null && (
                  <Button onClick={() => openAuthed(`/api/invoices/${inv.id}/pdf`)} style={{ background: tokens.color.surfaceAlt, borderColor: tokens.color.surfaceAlt }}>
                    PDF
                  </Button>
                )}
                {inv.type === "Invoice" && inv.number != null && <ShareButton invoiceId={inv.id} />}
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
              {inv.paidAmount > 0 && (
                <>
                  <div style={{ color: tokens.color.textMuted, marginTop: tokens.space.xs }}>Betalt −{kr(inv.paidAmount)}</div>
                  <div style={{ fontWeight: 700, color: inv.remainingAmount > 0 ? tokens.color.accent : tokens.color.success }}>
                    Kvar {kr(inv.remainingAmount)}
                  </div>
                </>
              )}
            </div>
          </Card>

          {inv.type === "Invoice" && inv.number != null && <PaymentsCard inv={inv} />}
          {inv.type === "Invoice" && inv.number != null && inv.status !== "Credited" && <CreditCard inv={inv} />}
          <HistoryList title="E-postutskick" items={emails.data} />
          <HistoryList title="Påminnelser" items={reminders.data} />
        </>
      )}
    </div>
  );
}

/** Kundlänk (spec 013): hämtar/skapar portallänken och kopierar den till urklipp. */
function ShareButton({ invoiceId }: { invoiceId: string }) {
  const [copied, setCopied] = useState(false);
  const share = useMutation({
    mutationFn: () => api.post<ShareLinkDto>(`/api/invoices/${invoiceId}/share`),
    onSuccess: async (link) => {
      try {
        await navigator.clipboard.writeText(link.url);
        setCopied(true);
        setTimeout(() => setCopied(false), 2500);
      } catch {
        window.prompt("Kundlänk (kopiera manuellt):", link.url);
      }
    },
  });

  return (
    <Button onClick={() => share.mutate()} disabled={share.isPending} style={{ background: tokens.color.surfaceAlt, borderColor: tokens.color.surfaceAlt }}>
      {copied ? "Länk kopierad!" : "Kundlänk"}
    </Button>
  );
}

/** Betalningsreskontran: registrera betalning + historik (spec 012). */
function PaymentsCard({ inv }: { inv: InvoiceDto }) {
  const qc = useQueryClient();
  const payments = useQuery({
    queryKey: ["invoice-payments", inv.id],
    queryFn: () => api.get<PaymentDto[]>(`/api/invoices/${inv.id}/payments`),
  });

  const [amount, setAmount] = useState("");
  const [paidDate, setPaidDate] = useState("");
  const [note, setNote] = useState("");
  const [error, setError] = useState<string | null>(null);

  const register = useMutation({
    mutationFn: () =>
      api.post<InvoiceDto>(`/api/invoices/${inv.id}/payments`, {
        amount: Number(amount),
        paidDate: paidDate || null,
        note: note || null,
      }),
    onSuccess: () => {
      setAmount("");
      setNote("");
      setError(null);
      qc.invalidateQueries({ queryKey: ["invoice", inv.id] });
      qc.invalidateQueries({ queryKey: ["invoice-payments", inv.id] });
      qc.invalidateQueries({ queryKey: ["invoices"] });
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : "Kunde inte registrera betalningen."),
  });

  function onSubmit(e: FormEvent) {
    e.preventDefault();
    register.mutate();
  }

  const canPay = inv.remainingAmount > 0;
  if (!canPay && (payments.data?.length ?? 0) === 0) return null;

  return (
    <Card>
      <h2 style={{ marginTop: 0, fontSize: tokens.font.size.lg }}>Betalningar</h2>
      {canPay && (
        <form onSubmit={onSubmit} style={{ display: "flex", gap: tokens.space.sm, alignItems: "end", flexWrap: "wrap" }}>
          <div style={{ width: 130 }}>
            <Field label="Belopp (kr)">
              <Input type="number" step="0.01" min="0.01" value={amount} onChange={(e) => setAmount(e.target.value)} required />
            </Field>
          </div>
          <div style={{ width: 160 }}>
            <Field label="Betaldatum">
              <Input type="date" value={paidDate} onChange={(e) => setPaidDate(e.target.value)} />
            </Field>
          </div>
          <div style={{ flex: 1, minWidth: 160 }}>
            <Field label="Notering (valfri)">
              <Input value={note} onChange={(e) => setNote(e.target.value)} placeholder="t.ex. bankgiro, swish" />
            </Field>
          </div>
          <Button type="submit" disabled={register.isPending}>Registrera</Button>
        </form>
      )}
      {error && <ErrorText>{error}</ErrorText>}
      <ul style={{ listStyle: "none", padding: 0, margin: canPay ? `${tokens.space.md} 0 0` : 0 }}>
        {payments.data?.map((p) => (
          <li key={p.id} style={{ display: "flex", justifyContent: "space-between", gap: tokens.space.md, padding: tokens.space.sm, borderTop: tokens.line.perforated, fontSize: tokens.font.size.sm }}>
            <span>
              <strong style={{ fontVariantNumeric: "tabular-nums" }}>{kr(p.amount)}</strong>
              {p.note && <span style={{ color: tokens.color.textMuted }}> · {p.note}</span>}
            </span>
            <span style={{ color: tokens.color.textMuted, whiteSpace: "nowrap" }}>{p.paidDate}</span>
          </li>
        ))}
      </ul>
    </Card>
  );
}

/** Delkreditering: välj antal per rad, eller kreditera hela fakturan (spec 012). */
function CreditCard({ inv }: { inv: InvoiceDto }) {
  const qc = useQueryClient();
  const [quantities, setQuantities] = useState<Record<number, string>>({});
  const [error, setError] = useState<string | null>(null);

  const credit = useMutation({
    mutationFn: (lines: { index: number; quantity: number }[] | null) =>
      api.post<InvoiceDto>(`/api/invoices/${inv.id}/credit`, lines ? { lines } : {}),
    onSuccess: () => {
      setQuantities({});
      setError(null);
      qc.invalidateQueries({ queryKey: ["invoice", inv.id] });
      qc.invalidateQueries({ queryKey: ["invoices"] });
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : "Kunde inte kreditera."),
  });

  const selected = inv.lines
    .map((_, i) => ({ index: i, quantity: Number(quantities[i] ?? 0) }))
    .filter((s) => s.quantity > 0);

  return (
    <Card>
      <h2 style={{ marginTop: 0, fontSize: tokens.font.size.lg }}>Kreditera</h2>
      <p style={{ color: tokens.color.textMuted, fontSize: tokens.font.size.sm, marginTop: `-${tokens.space.sm}` }}>
        Ange antal att kreditera per rad, eller kreditera hela fakturan.
      </p>
      {inv.lines.map((l, i) => (
        <div key={i} style={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: tokens.space.md, padding: tokens.space.sm, borderTop: tokens.line.perforated }}>
          <span>{l.description} <span style={{ color: tokens.color.textMuted, fontSize: tokens.font.size.sm }}>({l.quantity}{l.unit ? ` ${l.unit}` : ""})</span></span>
          <Input
            type="number"
            min="0"
            max={l.quantity}
            step="0.01"
            value={quantities[i] ?? ""}
            onChange={(e) => setQuantities({ ...quantities, [i]: e.target.value })}
            placeholder="0"
            style={{ width: 90, textAlign: "right" }}
            aria-label={`Antal att kreditera: ${l.description}`}
          />
        </div>
      ))}
      <div style={{ display: "flex", gap: tokens.space.sm, marginTop: tokens.space.md }}>
        <Button onClick={() => credit.mutate(selected)} disabled={credit.isPending || selected.length === 0}>
          Kreditera valda rader
        </Button>
        <Button
          onClick={() => window.confirm("Kreditera hela fakturan?") && credit.mutate(null)}
          disabled={credit.isPending}
          style={{ background: tokens.color.surfaceAlt, borderColor: tokens.color.surfaceAlt }}
        >
          Kreditera allt
        </Button>
      </div>
      {error && <ErrorText>{error}</ErrorText>}
    </Card>
  );
}
