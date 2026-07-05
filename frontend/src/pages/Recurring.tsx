import { useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, ApiError } from "../api/client";
import type { CustomerDto, RecurringInvoiceDto } from "../api/types";
import { Nav } from "../components/Nav";
import { Badge, Button, Card, ErrorText, Field, Input } from "../components/ui";
import { tokens } from "../theme/tokens";

const kr = (n: number) => `${n.toLocaleString("sv-SE")} kr`;
const INTERVALS = [
  { value: "monthly", label: "Månadsvis" },
  { value: "quarterly", label: "Kvartalsvis" },
  { value: "yearly", label: "Årsvis" },
];

const selectStyle = {
  background: tokens.color.surface,
  color: tokens.color.text,
  border: `1px solid ${tokens.color.border}`,
  borderRadius: tokens.radius.sm,
  padding: tokens.space.sm,
};

export function Recurring() {
  const qc = useQueryClient();
  const invalidate = () => qc.invalidateQueries({ queryKey: ["recurring"] });
  const customers = useQuery({ queryKey: ["customers"], queryFn: () => api.get<CustomerDto[]>("/api/customers") });
  const recurring = useQuery({ queryKey: ["recurring"], queryFn: () => api.get<RecurringInvoiceDto[]>("/api/recurring-invoices") });

  const [customerId, setCustomerId] = useState("");
  const [description, setDescription] = useState("");
  const [price, setPrice] = useState(0);
  const [interval, setInterval] = useState("monthly");
  const [startDate, setStartDate] = useState(new Date().toISOString().slice(0, 10));
  const [error, setError] = useState<string | null>(null);

  const create = useMutation({
    mutationFn: () =>
      api.post<RecurringInvoiceDto>("/api/recurring-invoices", {
        customerId,
        lines: [{ description, quantity: 1, unitPriceExclVat: price, vatRate: 25 }],
        interval,
        startDate,
        endDate: null,
      }),
    onSuccess: () => {
      setDescription("");
      setPrice(0);
      setError(null);
      invalidate();
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : "Kunde inte spara."),
  });

  const toggle = useMutation({
    mutationFn: ({ id, action }: { id: string; action: "pause" | "resume" }) =>
      api.post(`/api/recurring-invoices/${id}/${action}`),
    onSuccess: invalidate,
  });

  const customerName = (id: string) => customers.data?.find((c) => c.id === id)?.name ?? id;

  function onSubmit(e: FormEvent) {
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
        <h2 style={{ marginTop: 0, fontSize: tokens.font.size.lg }}>Nytt abonnemang</h2>
        <p style={{ color: tokens.color.textMuted, fontSize: tokens.font.size.sm, marginTop: `-${tokens.space.sm}` }}>
          Fakturan genereras, skickas och mejlas automatiskt varje period.
        </p>
        <form onSubmit={onSubmit} style={{ display: "flex", gap: tokens.space.sm, alignItems: "end", flexWrap: "wrap" }}>
          <div style={{ flex: 2, minWidth: 150 }}>
            <Field label="Kund">
              <select value={customerId} onChange={(e) => setCustomerId(e.target.value)} style={{ ...selectStyle, width: "100%" }}>
                <option value="">— välj —</option>
                {customers.data?.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
              </select>
            </Field>
          </div>
          <div style={{ flex: 3, minWidth: 160 }}>
            <Field label="Beskrivning"><Input value={description} onChange={(e) => setDescription(e.target.value)} required /></Field>
          </div>
          <div style={{ width: 120 }}>
            <Field label="Pris exkl. moms">
              <Input type="number" step="0.01" value={price} onChange={(e) => setPrice(Number(e.target.value))} />
            </Field>
          </div>
          <div style={{ width: 130 }}>
            <Field label="Intervall">
              <select value={interval} onChange={(e) => setInterval(e.target.value)} style={{ ...selectStyle, width: "100%" }}>
                {INTERVALS.map((i) => <option key={i.value} value={i.value}>{i.label}</option>)}
              </select>
            </Field>
          </div>
          <div style={{ width: 150 }}>
            <Field label="Startdatum">
              <Input type="date" value={startDate} onChange={(e) => setStartDate(e.target.value)} />
            </Field>
          </div>
          <div style={{ marginBottom: tokens.space.md }}>
            <Button type="submit" disabled={create.isPending}>Starta</Button>
          </div>
        </form>
        {error && <ErrorText>{error}</ErrorText>}
      </Card>

      <Card>
        <h2 style={{ marginTop: 0, fontSize: tokens.font.size.lg }}>Abonnemang</h2>
        {recurring.isLoading && <p>Laddar…</p>}
        <table style={{ width: "100%", borderCollapse: "collapse" }}>
          <thead>
            <tr style={{ color: tokens.color.textMuted, textAlign: "left", fontSize: tokens.font.size.sm, textTransform: "uppercase", letterSpacing: "0.08em" }}>
              <th style={{ padding: tokens.space.sm }}>Kund</th>
              <th>Intervall</th>
              <th>Nästa körning</th>
              <th style={{ textAlign: "right" }}>Belopp</th>
              <th>Status</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {recurring.data?.map((r) => (
              <tr key={r.id} style={{ borderTop: tokens.line.perforated }}>
                <td style={{ padding: tokens.space.sm, fontWeight: 600 }}>{customerName(r.customerId)}</td>
                <td>{INTERVALS.find((i) => i.value.toLowerCase() === r.interval.toLowerCase())?.label ?? r.interval}</td>
                <td>{r.nextRunDate}</td>
                <td style={{ textAlign: "right" }}>{kr(r.gross)}</td>
                <td><Badge status={r.status} /></td>
                <td style={{ textAlign: "right" }}>
                  <Button
                    onClick={() => toggle.mutate({ id: r.id, action: r.status === "Active" ? "pause" : "resume" })}
                    style={{ background: tokens.color.surfaceAlt, borderColor: tokens.color.surfaceAlt }}
                  >
                    {r.status === "Active" ? "Pausa" : "Återuppta"}
                  </Button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </Card>
    </div>
  );
}
