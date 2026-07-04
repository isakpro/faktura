import { useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "../auth/AuthContext";
import { api, ApiError } from "../api/client";
import type { BillingDto, InvitationDto, MemberDto } from "../api/types";
import { Nav } from "../components/Nav";
import { Button, Card, ErrorText, Field, Input } from "../components/ui";
import { tokens } from "../theme/tokens";

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
    </div>
  );
}
