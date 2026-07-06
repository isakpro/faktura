import { useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "../auth/AuthContext";
import { api, ApiError } from "../api/client";
import type { BillingDto, DashboardDto, InvitationDto, MemberDto, ReminderSettingsDto } from "../api/types";
import { Nav } from "../components/Nav";
import { RevenueChart } from "../components/RevenueChart";
import { ActivityCard } from "../components/ActivityCard";
import { Badge, Button, Card, ErrorText, Field, Input } from "../components/ui";
import { tokens } from "../theme/tokens";

const kr = (n: number) => `${n.toLocaleString("sv-SE")} kr`;

function StatTile({ label, value, emphasize }: { label: string; value: number; emphasize?: boolean }) {
  return (
    <Card style={{ flex: 1, minWidth: 170, padding: tokens.space.md }}>
      <div style={{ color: tokens.color.textMuted, fontSize: tokens.font.size.sm, textTransform: "uppercase", letterSpacing: "0.08em" }}>
        {label}
      </div>
      <div
        style={{
          fontSize: tokens.font.size.xl,
          fontWeight: 700,
          fontVariantNumeric: "tabular-nums",
          color: emphasize && value > 0 ? tokens.color.accent : tokens.color.text,
        }}
      >
        {kr(value)}
      </div>
    </Card>
  );
}

export function Dashboard() {
  const { user, organization } = useAuth();
  const qc = useQueryClient();
  const canManage = user?.role === "Owner" || user?.role === "Admin";
  const isOwner = user?.role === "Owner";

  const members = useQuery({ queryKey: ["members"], queryFn: () => api.get<MemberDto[]>("/api/members") });
  const invitations = useQuery({
    queryKey: ["invitations"],
    queryFn: () => api.get<InvitationDto[]>("/api/invitations"),
    enabled: canManage,
  });
  const billing = useQuery({
    queryKey: ["billing"],
    queryFn: () => api.get<BillingDto>("/api/billing"),
    enabled: isOwner,
  });
  const dashboard = useQuery({ queryKey: ["dashboard"], queryFn: () => api.get<DashboardDto>("/api/dashboard") });

  const [inviteEmail, setInviteEmail] = useState("");
  const [inviteRole, setInviteRole] = useState("Member");
  const [inviteError, setInviteError] = useState<string | null>(null);
  const [inviteLink, setInviteLink] = useState<string | null>(null);

  const invite = useMutation({
    mutationFn: (body: { email: string; role: string }) =>
      api.post<{ invitation: InvitationDto; token: string }>("/api/invitations", body),
    onSuccess: (res) => {
      setInviteError(null);
      setInviteEmail("");
      setInviteLink(`${window.location.origin}/accept/${res.token}`);
      qc.invalidateQueries({ queryKey: ["invitations"] });
    },
    onError: (err) => setInviteError(err instanceof ApiError ? err.message : "Kunde inte bjuda in."),
  });

  const checkout = useMutation({
    mutationFn: () => api.post<{ checkoutUrl: string }>("/api/billing/checkout", { returnUrl: window.location.href }),
    onSuccess: (res) => {
      window.location.href = res.checkoutUrl;
    },
  });

  const removeMember = useMutation({
    mutationFn: (id: string) => api.del(`/api/members/${id}`),
    onSuccess: () => {
      setInviteError(null);
      qc.invalidateQueries({ queryKey: ["members"] });
    },
    onError: (err) => setInviteError(err instanceof ApiError ? err.message : "Kunde inte ta bort medlemmen."),
  });

  function onInvite(e: FormEvent) {
    e.preventDefault();
    invite.mutate({ email: inviteEmail, role: inviteRole });
  }

  return (
    <div style={{ maxWidth: 780, margin: "0 auto", padding: tokens.space.md, display: "grid", gap: tokens.space.lg }}>
      <Nav />
      <span style={{ color: tokens.color.textMuted, fontSize: tokens.font.size.sm, marginTop: `-${tokens.space.md}` }}>
        {user?.email} · {user?.role} · Plan: {organization?.plan}
      </span>

      {dashboard.data && (
        <>
          <div style={{ display: "flex", gap: tokens.space.md, flexWrap: "wrap" }}>
            <StatTile label="Utestående" value={dashboard.data.outstanding} />
            <StatTile label="Förfallet" value={dashboard.data.overdue} emphasize />
            <StatTile label="Betalt i år" value={dashboard.data.paidThisYear} />
          </div>

          <Card>
            <h2 style={{ marginTop: 0, fontSize: tokens.font.size.lg }}>Omsättning per månad</h2>
            <RevenueChart data={dashboard.data.monthlyRevenue} />
          </Card>

          {dashboard.data.recentInvoices.length > 0 && (
            <Card>
              <h2 style={{ marginTop: 0, fontSize: tokens.font.size.lg }}>Senaste fakturor</h2>
              <ul style={{ listStyle: "none", padding: 0, margin: 0 }}>
                {dashboard.data.recentInvoices.map((inv) => (
                  <li key={inv.id} style={{ display: "flex", justifyContent: "space-between", alignItems: "center", padding: tokens.space.sm, borderTop: tokens.line.perforated }}>
                    <span style={{ fontWeight: 600 }}>Faktura {inv.number ?? "—"}</span>
                    <span style={{ display: "flex", alignItems: "center", gap: tokens.space.md }}>
                      <Badge status={inv.status} />
                      <span style={{ fontVariantNumeric: "tabular-nums" }}>{kr(inv.gross)}</span>
                    </span>
                  </li>
                ))}
              </ul>
            </Card>
          )}
        </>
      )}

      <Card>
        <h2 style={{ marginTop: 0, fontSize: tokens.font.size.lg }}>Medlemmar</h2>
        {members.isLoading && <p>Laddar…</p>}
        <ul style={{ listStyle: "none", padding: 0, margin: 0 }}>
          {members.data?.map((m) => (
            <li key={m.id} style={{ display: "flex", justifyContent: "space-between", alignItems: "center", padding: tokens.space.sm, borderBottom: `1px solid ${tokens.color.border}` }}>
              <span>{m.email}</span>
              <span style={{ display: "flex", alignItems: "center", gap: tokens.space.sm }}>
                <span style={{ color: tokens.color.textMuted }}>{m.role}</span>
                {canManage && m.id !== user?.id && (
                  <Button
                    onClick={() => window.confirm(`Ta bort ${m.email}?`) && removeMember.mutate(m.id)}
                    style={{ background: tokens.color.surfaceAlt }}
                  >
                    Ta bort
                  </Button>
                )}
              </span>
            </li>
          ))}
        </ul>
      </Card>

      {canManage && (
        <Card>
          <h2 style={{ marginTop: 0, fontSize: tokens.font.size.lg }}>Bjud in medlem</h2>
          <form onSubmit={onInvite} style={{ display: "flex", gap: tokens.space.sm, alignItems: "end", flexWrap: "wrap" }}>
            <div style={{ flex: 1, minWidth: 200 }}>
              <Field label="E-post">
                <Input type="email" value={inviteEmail} onChange={(e) => setInviteEmail(e.target.value)} required />
              </Field>
            </div>
            <Field label="Roll">
              <select
                value={inviteRole}
                onChange={(e) => setInviteRole(e.target.value)}
                style={{ background: tokens.color.bg, color: tokens.color.text, border: `1px solid ${tokens.color.border}`, borderRadius: tokens.radius.sm, padding: tokens.space.sm }}
              >
                <option>Member</option>
                <option>Admin</option>
              </select>
            </Field>
            <Button type="submit" disabled={invite.isPending}>Bjud in</Button>
          </form>
          {inviteError && <ErrorText>{inviteError}</ErrorText>}
          {inviteLink && (
            <p style={{ color: tokens.color.success, fontSize: tokens.font.size.sm, wordBreak: "break-all" }}>
              Inbjudningslänk (dela med mottagaren): {inviteLink}
            </p>
          )}
          <ul style={{ listStyle: "none", padding: 0, marginTop: tokens.space.md }}>
            {invitations.data?.filter((i) => i.status === "Pending").map((i) => (
              <li key={i.id} style={{ color: tokens.color.textMuted, fontSize: tokens.font.size.sm }}>
                {i.email} · {i.role} · {i.status}
              </li>
            ))}
          </ul>
        </Card>
      )}

      {isOwner && (
        <Card>
          <h2 style={{ marginTop: 0, fontSize: tokens.font.size.lg }}>Plan & fakturering</h2>
          <p style={{ color: tokens.color.textMuted }}>
            Nuvarande plan: <strong style={{ color: tokens.color.text }}>{billing.data?.plan}</strong>
            {" · "}Platser: {billing.data?.seatLimit}
          </p>
          {billing.data?.plan !== "Pro" && (
            <Button onClick={() => checkout.mutate()} disabled={checkout.isPending}>
              Uppgradera till Pro (Stripe testläge)
            </Button>
          )}
        </Card>
      )}

      {canManage && <ReminderSettingsCard />}

      {canManage && <ProfileCard />}

      {canManage && <ActivityCard />}
    </div>
  );
}

function ReminderSettingsCard() {
  const qc = useQueryClient();
  const settings = useQuery({
    queryKey: ["reminder-settings"],
    queryFn: () => api.get<ReminderSettingsDto>("/api/reminder-settings"),
  });

  const [enabled, setEnabled] = useState<boolean | null>(null);
  const [days, setDays] = useState<number | null>(null);
  const autoEnabled = enabled ?? settings.data?.autoEnabled ?? false;
  const daysAfterDue = days ?? settings.data?.daysAfterDue ?? 7;

  const save = useMutation({
    mutationFn: () =>
      api.put<ReminderSettingsDto>("/api/reminder-settings", { autoEnabled, daysAfterDue }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["reminder-settings"] }),
  });

  return (
    <Card>
      <h2 style={{ marginTop: 0, fontSize: tokens.font.size.lg }}>Betalningspåminnelser</h2>
      <div style={{ display: "flex", alignItems: "center", gap: tokens.space.md, flexWrap: "wrap" }}>
        <label style={{ display: "flex", alignItems: "center", gap: tokens.space.sm }}>
          <input type="checkbox" checked={autoEnabled} onChange={(e) => setEnabled(e.target.checked)} />
          Skicka automatiskt
        </label>
        <label style={{ display: "flex", alignItems: "center", gap: tokens.space.sm }}>
          dagar efter förfall:
          <Input
            type="number"
            value={daysAfterDue}
            onChange={(e) => setDays(Number(e.target.value))}
            style={{ width: 80 }}
          />
        </label>
        <Button onClick={() => save.mutate()} disabled={save.isPending}>Spara</Button>
        {save.isSuccess && <span style={{ color: tokens.color.success, fontSize: tokens.font.size.sm }}>Sparat.</span>}
      </div>
    </Card>
  );
}

function ProfileCard() {
  const qc = useQueryClient();
  const profile = useQuery({
    queryKey: ["organization-profile"],
    queryFn: () => api.get<Record<string, string | boolean | null>>("/api/organization-profile"),
  });

  const [form, setForm] = useState<Record<string, string | boolean | null> | null>(null);
  const current = form ?? profile.data ?? {};
  const set = (key: string, value: string | boolean) =>
    setForm({ ...(form ?? profile.data ?? {}), [key]: value });
  const str = (key: string) => String(current[key] ?? "");

  const save = useMutation({
    mutationFn: () => api.put("/api/organization-profile", {
      orgNumber: str("orgNumber") || null,
      addressLine: str("addressLine") || null,
      postalCode: str("postalCode") || null,
      city: str("city") || null,
      bankgiro: str("bankgiro") || null,
      plusgiro: str("plusgiro") || null,
      fSkatt: Boolean(current["fSkatt"]),
    }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["organization-profile"] }),
  });

  const fields: Array<[string, string, number]> = [
    ["orgNumber", "Org.nr", 130],
    ["addressLine", "Adress", 200],
    ["postalCode", "Postnr", 90],
    ["city", "Ort", 130],
    ["bankgiro", "Bankgiro", 120],
    ["plusgiro", "Plusgiro", 120],
  ];

  return (
    <Card>
      <h2 style={{ marginTop: 0, fontSize: tokens.font.size.lg }}>Fakturaprofil</h2>
      <p style={{ color: tokens.color.textMuted, fontSize: tokens.font.size.sm, marginTop: `-${tokens.space.sm}` }}>
        Säljaruppgifterna som visas på fakturans PDF.
      </p>
      <div style={{ display: "flex", gap: tokens.space.sm, alignItems: "end", flexWrap: "wrap" }}>
        {fields.map(([key, label, width]) => (
          <div key={key} style={{ width }}>
            <Field label={label}>
              <Input value={str(key)} onChange={(e) => set(key, e.target.value)} />
            </Field>
          </div>
        ))}
        <label style={{ display: "flex", alignItems: "center", gap: tokens.space.sm, marginBottom: tokens.space.md }}>
          <input type="checkbox" checked={Boolean(current["fSkatt"])} onChange={(e) => set("fSkatt", e.target.checked)} />
          Godkänd för F-skatt
        </label>
        <div style={{ marginBottom: tokens.space.md }}>
          <Button onClick={() => save.mutate()} disabled={save.isPending}>Spara</Button>
        </div>
        {save.isSuccess && <span style={{ color: tokens.color.success, fontSize: tokens.font.size.sm, marginBottom: tokens.space.md }}>Sparat.</span>}
      </div>
    </Card>
  );
}
