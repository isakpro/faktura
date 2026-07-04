import { useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, ApiError } from "../api/client";
import type { CustomerDto } from "../api/types";
import { Nav } from "../components/Nav";
import { Button, Card, ErrorText, Field, Input } from "../components/ui";
import { tokens } from "../theme/tokens";

export function Customers() {
  const qc = useQueryClient();
  const customers = useQuery({ queryKey: ["customers"], queryFn: () => api.get<CustomerDto[]>("/api/customers") });

  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [terms, setTerms] = useState(30);
  const [error, setError] = useState<string | null>(null);

  const create = useMutation({
    mutationFn: () => api.post<CustomerDto>("/api/customers", { name, email, paymentTermsDays: terms }),
    onSuccess: () => {
      setName("");
      setEmail("");
      setError(null);
      qc.invalidateQueries({ queryKey: ["customers"] });
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : "Kunde inte spara."),
  });

  function onSubmit(e: FormEvent) {
    e.preventDefault();
    create.mutate();
  }

  return (
    <div style={{ maxWidth: 780, margin: "0 auto", padding: tokens.space.md }}>
      <Nav />
      <Card style={{ marginBottom: tokens.space.lg }}>
        <h2 style={{ marginTop: 0, fontSize: tokens.font.size.lg }}>Ny kund</h2>
        <form onSubmit={onSubmit} style={{ display: "flex", gap: tokens.space.sm, alignItems: "end", flexWrap: "wrap" }}>
          <div style={{ flex: 2, minWidth: 180 }}>
            <Field label="Namn"><Input value={name} onChange={(e) => setName(e.target.value)} required /></Field>
          </div>
          <div style={{ flex: 2, minWidth: 180 }}>
            <Field label="E-post"><Input type="email" value={email} onChange={(e) => setEmail(e.target.value)} /></Field>
          </div>
          <div style={{ width: 120 }}>
            <Field label="Betaln.villkor (dgr)">
              <Input type="number" value={terms} onChange={(e) => setTerms(Number(e.target.value))} />
            </Field>
          </div>
          <Button type="submit" disabled={create.isPending}>Spara</Button>
        </form>
        {error && <ErrorText>{error}</ErrorText>}
      </Card>

      <Card>
        <h2 style={{ marginTop: 0, fontSize: tokens.font.size.lg }}>Kunder</h2>
        {customers.isLoading && <p>Laddar…</p>}
        <ul style={{ listStyle: "none", padding: 0, margin: 0 }}>
          {customers.data?.map((c) => (
            <li key={c.id} style={{ display: "flex", justifyContent: "space-between", padding: tokens.space.sm, borderBottom: `1px solid ${tokens.color.border}` }}>
              <span>{c.name}</span>
              <span style={{ color: tokens.color.textMuted }}>{c.email} · {c.paymentTermsDays} dgr</span>
            </li>
          ))}
        </ul>
      </Card>
    </div>
  );
}
