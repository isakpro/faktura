import type { ButtonHTMLAttributes, CSSProperties, InputHTMLAttributes, ReactNode } from "react";
import { tokens } from "../theme/tokens";

/** Kvittoslip: papper med bläck-rubriklinje överst och mjuk pappersskugga. */
export function Card({ children, style }: { children: ReactNode; style?: CSSProperties }) {
  return (
    <div
      style={{
        background: tokens.color.surface,
        border: `1px solid ${tokens.color.border}`,
        borderTop: `3px solid ${tokens.color.primary}`,
        borderRadius: tokens.radius.lg,
        padding: tokens.space.lg,
        boxShadow: "0 1px 0 rgba(33, 27, 18, 0.08), 0 6px 16px rgba(33, 27, 18, 0.06)",
        ...style,
      }}
    >
      {children}
    </div>
  );
}

export function Button({ children, style, ...props }: ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <button
      {...props}
      style={{
        background: tokens.color.primary,
        color: tokens.color.primaryText,
        border: `1px solid ${tokens.color.primary}`,
        borderRadius: tokens.radius.md,
        padding: `${tokens.space.sm} ${tokens.space.md}`,
        fontSize: tokens.font.size.sm,
        fontWeight: 600,
        letterSpacing: "0.04em",
        textTransform: "uppercase",
        cursor: props.disabled ? "not-allowed" : "pointer",
        opacity: props.disabled ? 0.6 : 1,
        ...style,
      }}
    >
      {children}
    </button>
  );
}

export function Input(props: InputHTMLAttributes<HTMLInputElement>) {
  return (
    <input
      {...props}
      style={{
        background: tokens.color.surface,
        color: tokens.color.text,
        border: `1px solid ${tokens.color.border}`,
        borderBottom: `2px solid ${tokens.color.surfaceAlt}`,
        borderRadius: tokens.radius.sm,
        padding: tokens.space.sm,
        fontSize: tokens.font.size.md,
        width: "100%",
        boxSizing: "border-box",
        ...props.style,
      }}
    />
  );
}

export function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label style={{ display: "block", marginBottom: tokens.space.md }}>
      <span
        style={{
          display: "block",
          color: tokens.color.textMuted,
          fontSize: tokens.font.size.sm,
          textTransform: "uppercase",
          letterSpacing: "0.08em",
          marginBottom: tokens.space.xs,
        }}
      >
        {label}
      </span>
      {children}
    </label>
  );
}

export function ErrorText({ children }: { children: ReactNode }) {
  return <p style={{ color: tokens.color.danger, fontSize: tokens.font.size.sm }}>{children}</p>;
}

const badgeColors: Record<string, string> = {
  Draft: tokens.color.textMuted,
  Sent: tokens.color.primary,
  Overdue: tokens.color.accent,
  Paid: tokens.color.success,
  Credited: tokens.color.textMuted,
  CreditNote: tokens.color.textMuted,
  Active: tokens.color.success,
  Paused: tokens.color.textMuted,
  Archived: tokens.color.textMuted,
};

const badgeLabels: Record<string, string> = {
  Draft: "UTKAST",
  Sent: "SKICKAD",
  Overdue: "FÖRFALLEN",
  Paid: "BETALD",
  Credited: "KREDITERAD",
  CreditNote: "KREDIT",
  Active: "AKTIV",
  Paused: "PAUSAD",
  Archived: "ARKIVERAD",
};

/** Stämpel-lik statusmarkering: ram + versaler i stämpelfärg, aningen sned som en stämpel. */
export function Badge({ status }: { status: string }) {
  const color = badgeColors[status] ?? tokens.color.textMuted;
  return (
    <span
      style={{
        display: "inline-block",
        border: `1.5px solid ${color}`,
        borderRadius: tokens.radius.sm,
        color,
        padding: `1px ${tokens.space.sm}`,
        fontSize: "11px",
        fontWeight: 700,
        letterSpacing: "0.1em",
        transform: "rotate(-1deg)",
      }}
    >
      {badgeLabels[status] ?? status.toUpperCase()}
    </span>
  );
}
