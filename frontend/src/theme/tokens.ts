// Delade design-tokens. Ingen hårdkodad styling i komponenter — referera dessa (constitution).
export const tokens = {
  color: {
    bg: "#0f172a",
    surface: "#1e293b",
    surfaceAlt: "#334155",
    border: "#475569",
    text: "#f1f5f9",
    textMuted: "#94a3b8",
    primary: "#6366f1",
    primaryText: "#ffffff",
    danger: "#ef4444",
    success: "#22c55e",
  },
  radius: { sm: "6px", md: "10px", lg: "16px" },
  space: { xs: "4px", sm: "8px", md: "16px", lg: "24px", xl: "40px" },
  font: {
    family: "system-ui, -apple-system, Segoe UI, Roboto, sans-serif",
    size: { sm: "13px", md: "15px", lg: "20px", xl: "28px" },
  },
} as const;
