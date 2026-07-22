import { useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, ApiError, BASE_URL } from "../api/client";
import type { ApiKeyDto, CreatedApiKeyDto, CreatedWebhookDto, WebhookEndpointDto } from "../api/types";
import { Layout } from "../components/Layout";
import { Button, Card, ErrorText, Field, Input } from "../components/ui";
import { tokens } from "../theme/tokens";

const SCOPES = ["invoices:read", "customers:read", "customers:write"];

/** Publikt API + webhooks (spec 016): nyckelhantering och mottagar-URL:er. */
export function Developer() {
  return (
    <Layout>
      <Card>
        <h2 style={{ marginTop: 0, fontSize: tokens.font.size.lg }}>Publikt API</h2>
        <p style={{ color: tokens.color.textMuted, fontSize: tokens.font.size.sm, marginTop: `-${tokens.space.sm}` }}>
          Skicka nyckeln i headern <code>X-Api-Key</code> mot <code>{BASE_URL}/api/v1/…</code> —
          t.ex. <code>GET /api/v1/invoices</code> eller <code>GET /api/v1/customers</code>.
        </p>
      </Card>
      <div className="card-grid">
        <ApiKeysCard />
        <WebhooksCard />
      </div>
    </Layout>
  );
}

function ApiKeysCard() {
  const qc = useQueryClient();
  const keys = useQuery({ queryKey: ["api-keys"], queryFn: () => api.get<ApiKeyDto[]>("/api/api-keys") });

  const [name, setName] = useState("");
  const [scopes, setScopes] = useState<string[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [revealed, setRevealed] = useState<CreatedApiKeyDto | null>(null);

  const create = useMutation({
    mutationFn: () => api.post<CreatedApiKeyDto>("/api/api-keys", { name, scopes }),
    onSuccess: (key) => {
      setRevealed(key);
      setName("");
      setScopes([]);
      setError(null);
      qc.invalidateQueries({ queryKey: ["api-keys"] });
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : "Kunde inte skapa nyckeln."),
  });

  const revoke = useMutation({
    mutationFn: (id: string) => api.del(`/api/api-keys/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["api-keys"] }),
  });

  function toggleScope(scope: string) {
    setScopes((s) => (s.includes(scope) ? s.filter((x) => x !== scope) : [...s, scope]));
  }

  function onSubmit(e: FormEvent) {
    e.preventDefault();
    create.mutate();
  }

  return (
    <Card>
      <h2 style={{ marginTop: 0, fontSize: tokens.font.size.lg }}>API-nycklar</h2>
      <form onSubmit={onSubmit} style={{ display: "flex", gap: tokens.space.sm, alignItems: "end", flexWrap: "wrap" }}>
        <div style={{ flex: 1, minWidth: 180 }}>
          <Field label="Namn">
            <Input value={name} onChange={(e) => setName(e.target.value)} placeholder="t.ex. Bokföringsintegration" required />
          </Field>
        </div>
        <div>
          <span style={{ display: "block", color: tokens.color.textMuted, fontSize: tokens.font.size.sm, marginBottom: tokens.space.xs, textTransform: "uppercase", letterSpacing: "0.08em" }}>
            Scopes
          </span>
          <div style={{ display: "flex", gap: tokens.space.sm, marginBottom: tokens.space.md }}>
            {SCOPES.map((scope) => (
              <label key={scope} style={{ display: "flex", alignItems: "center", gap: "4px", fontSize: tokens.font.size.sm }}>
                <input type="checkbox" checked={scopes.includes(scope)} onChange={() => toggleScope(scope)} />
                {scope}
              </label>
            ))}
          </div>
        </div>
        <Button type="submit" disabled={create.isPending || scopes.length === 0}>Skapa nyckel</Button>
      </form>
      {error && <ErrorText>{error}</ErrorText>}
      {revealed && (
        <div style={{ background: tokens.color.bg, border: `1px dashed ${tokens.color.border}`, borderRadius: tokens.radius.md, padding: tokens.space.md, marginTop: tokens.space.md }}>
          <p style={{ margin: 0, fontSize: tokens.font.size.sm, color: tokens.color.accent, fontWeight: 700 }}>
            Visas bara nu — kopiera nyckeln, den går inte att se igen:
          </p>
          <code style={{ display: "block", marginTop: tokens.space.xs, wordBreak: "break-all", fontVariantNumeric: "tabular-nums" }}>
            {revealed.key}
          </code>
        </div>
      )}
      <ul style={{ listStyle: "none", padding: 0, marginTop: tokens.space.md }}>
        {keys.data?.map((k) => (
          <li key={k.id} style={{ display: "flex", justifyContent: "space-between", alignItems: "center", padding: tokens.space.sm, borderTop: tokens.line.perforated }}>
            <span>
              <strong>{k.name}</strong>{" "}
              <span style={{ color: tokens.color.textMuted, fontSize: tokens.font.size.sm }}>
                {k.prefix}… · {k.scopes.join(", ")}
              </span>
            </span>
            <Button onClick={() => revoke.mutate(k.id)} style={{ background: tokens.color.surfaceAlt, borderColor: tokens.color.surfaceAlt }}>
              Återkalla
            </Button>
          </li>
        ))}
      </ul>
    </Card>
  );
}

function WebhooksCard() {
  const qc = useQueryClient();
  const endpoints = useQuery({ queryKey: ["webhooks"], queryFn: () => api.get<WebhookEndpointDto[]>("/api/webhooks") });

  const [url, setUrl] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [revealed, setRevealed] = useState<CreatedWebhookDto | null>(null);

  const create = useMutation({
    mutationFn: () => api.post<CreatedWebhookDto>("/api/webhooks", { url }),
    onSuccess: (endpoint) => {
      setRevealed(endpoint);
      setUrl("");
      setError(null);
      qc.invalidateQueries({ queryKey: ["webhooks"] });
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : "Kunde inte lägga till mottagaren."),
  });

  const remove = useMutation({
    mutationFn: (id: string) => api.del(`/api/webhooks/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["webhooks"] }),
  });

  function onSubmit(e: FormEvent) {
    e.preventDefault();
    create.mutate();
  }

  return (
    <Card>
      <h2 style={{ marginTop: 0, fontSize: tokens.font.size.lg }}>Webhooks</h2>
      <p style={{ color: tokens.color.textMuted, fontSize: tokens.font.size.sm, marginTop: `-${tokens.space.sm}` }}>
        Vi POST:ar <code>invoice.sent</code>, <code>invoice.paid</code> och <code>invoice.credited</code> till
        mottagar-URL:en, signerat med HMAC-SHA256 i headern <code>X-Faktura-Signature</code>.
      </p>
      <form onSubmit={onSubmit} style={{ display: "flex", gap: tokens.space.sm, alignItems: "end" }}>
        <div style={{ flex: 1 }}>
          <Field label="Mottagar-URL">
            <Input type="url" value={url} onChange={(e) => setUrl(e.target.value)} placeholder="https://exempel.se/hooks/faktura" required />
          </Field>
        </div>
        <Button type="submit" disabled={create.isPending}>Lägg till</Button>
      </form>
      {error && <ErrorText>{error}</ErrorText>}
      {revealed && (
        <div style={{ background: tokens.color.bg, border: `1px dashed ${tokens.color.border}`, borderRadius: tokens.radius.md, padding: tokens.space.md, marginTop: tokens.space.md }}>
          <p style={{ margin: 0, fontSize: tokens.font.size.sm, color: tokens.color.accent, fontWeight: 700 }}>
            Signeringshemlighet — visas bara nu:
          </p>
          <code style={{ display: "block", marginTop: tokens.space.xs, wordBreak: "break-all" }}>{revealed.secret}</code>
        </div>
      )}
      <ul style={{ listStyle: "none", padding: 0, marginTop: tokens.space.md }}>
        {endpoints.data?.map((e) => (
          <li key={e.id} style={{ display: "flex", justifyContent: "space-between", alignItems: "center", padding: tokens.space.sm, borderTop: tokens.line.perforated }}>
            <span style={{ wordBreak: "break-all" }}>{e.url}</span>
            <Button onClick={() => remove.mutate(e.id)} style={{ background: tokens.color.surfaceAlt, borderColor: tokens.color.surfaceAlt, flexShrink: 0, marginLeft: tokens.space.sm }}>
              Ta bort
            </Button>
          </li>
        ))}
      </ul>
    </Card>
  );
}
