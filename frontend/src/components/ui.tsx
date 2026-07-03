import type { ButtonHTMLAttributes, CSSProperties, InputHTMLAttributes, ReactNode } from "react";
import { tokens } from "../theme/tokens";

export function Card({ children, style }: { children: ReactNode; style?: CSSProperties }) {
  return (
    <div
      style={{
        background: tokens.color.surface,
        border: `1px solid ${tokens.color.border}`,
        borderRadius: tokens.radius.lg,
        padding: tokens.space.lg,
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
        border: "none",
        borderRadius: tokens.radius.md,
        padding: `${tokens.space.sm} ${tokens.space.md}`,
        fontSize: tokens.font.size.md,
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
        background: tokens.color.bg,
        color: tokens.color.text,
        border: `1px solid ${tokens.color.border}`,
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
      <span style={{ display: "block", color: tokens.color.textMuted, fontSize: tokens.font.size.sm, marginBottom: tokens.space.xs }}>
        {label}
      </span>
      {children}
    </label>
  );
}

export function ErrorText({ children }: { children: ReactNode }) {
  return <p style={{ color: tokens.color.danger, fontSize: tokens.font.size.sm }}>{children}</p>;
}
