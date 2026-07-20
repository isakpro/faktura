import { useParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { api, BASE_URL } from "../api/client";
import type { PublicInvoiceDto } from "../api/types";
import { Badge, Button } from "../components/ui";
import { tokens } from "../theme/tokens";

const kr = (n: number) => `${n.toLocaleString("sv-SE", { minimumFractionDigits: 2 })} kr`;

/**
 * Kundportalen (spec 013): publik fakturavy via kapabilitets-token — ingen inloggning.
 * Designad som ett fysiskt dokument på skrivbordet: ett ark i Huvudboken-tema med
 * brevhuvud, perforerad betalningstalong och stämpel.
 */
export function PublicInvoice() {
  const { token } = useParams<{ token: string }>();
  const invoice = useQuery({
    queryKey: ["public-invoice", token],
    queryFn: () => api.get<PublicInvoiceDto>(`/api/public/invoices/${token}`),
    retry: false,
  });

  const inv = invoice.data;

  return (
    <div style={{ minHeight: "100vh", background: tokens.color.bg, padding: `${tokens.space.xl} ${tokens.space.md}` }}>
      <div style={{ maxWidth: 700, margin: "0 auto" }}>
        {invoice.isLoading && <p style={{ textAlign: "center", color: tokens.color.textMuted }}>Hämtar fakturan…</p>}

        {invoice.isError && (
          <div style={{ textAlign: "center", color: tokens.color.textMuted, marginTop: "20vh" }}>
            <div style={{ fontFamily: tokens.font.display, fontSize: tokens.font.size.xl, color: tokens.color.text }}>
              Dokumentet hittades inte
            </div>
            <p>Länken kan vara felaktig. Kontakta avsändaren för en ny länk.</p>
          </div>
        )}

        {inv && (
          <div
            style={{
              background: tokens.color.surface,
              border: `1px solid ${tokens.color.border}`,
              borderTop: `6px double ${tokens.color.primary}`,
              boxShadow: "0 12px 30px rgba(33, 27, 18, 0.18)",
              padding: tokens.space.xl,
            }}
          >
            {/* Brevhuvud */}
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "start", gap: tokens.space.md, flexWrap: "wrap" }}>
              <div>
                <div style={{ fontFamily: tokens.font.display, fontSize: tokens.font.size.xl, fontWeight: 700 }}>
                  {inv.seller.name}
                </div>
                <div style={{ color: tokens.color.textMuted, fontSize: tokens.font.size.sm }}>
                  {inv.seller.addressLine && <div>{inv.seller.addressLine}</div>}
                  {(inv.seller.postalCode || inv.seller.city) && <div>{inv.seller.postalCode} {inv.seller.city}</div>}
                  {inv.seller.orgNumber && <div>Org.nr {inv.seller.orgNumber}</div>}
                </div>
              </div>
              <div style={{ textAlign: "right" }}>
                <div style={{ fontFamily: tokens.font.display, fontSize: tokens.font.size.lg, fontWeight: 700 }}>
                  {inv.type === "CreditNote" ? "KREDITFAKTURA" : "FAKTURA"} {inv.number}
                </div>
                <div style={{ color: tokens.color.textMuted, fontSize: tokens.font.size.sm }}>
                  {inv.invoiceDate && <div>Fakturadatum {inv.invoiceDate}</div>}
                  {inv.dueDate && <div>Förfaller {inv.dueDate}</div>}
                </div>
                <div style={{ marginTop: tokens.space.sm }}><Badge status={inv.status} /></div>
              </div>
            </div>

            <div style={{ color: tokens.color.textMuted, fontSize: tokens.font.size.sm, marginTop: tokens.space.lg }}>
              Till: <strong style={{ color: tokens.color.text }}>{inv.customerName}</strong>
            </div>

            {/* Rader */}
            <table style={{ width: "100%", borderCollapse: "collapse", marginTop: tokens.space.md }}>
              <thead>
                <tr style={{ color: tokens.color.textMuted, textAlign: "left", fontSize: tokens.font.size.sm, textTransform: "uppercase", letterSpacing: "0.08em" }}>
                  <th style={{ padding: tokens.space.sm }}>Beskrivning</th>
                  <th style={{ textAlign: "right" }}>Antal</th>
                  <th style={{ textAlign: "right" }}>À-pris</th>
                  <th style={{ textAlign: "right" }}>Moms</th>
                  <th style={{ textAlign: "right" }}>Netto</th>
                </tr>
              </thead>
              <tbody style={{ fontVariantNumeric: "tabular-nums" }}>
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

            {/* Summor */}
            <div style={{ marginTop: tokens.space.lg, marginLeft: "auto", width: "fit-content", textAlign: "right", fontVariantNumeric: "tabular-nums" }}>
              <div style={{ color: tokens.color.textMuted }}>Netto {kr(inv.totals.net)}</div>
              {inv.totals.vatByRate.map((v) => (
                <div key={v.rate} style={{ color: tokens.color.textMuted }}>Moms {v.rate}% {kr(v.vat)}</div>
              ))}
              <div style={{ fontSize: tokens.font.size.lg, fontWeight: 700, borderTop: `2px solid ${tokens.color.primary}`, marginTop: tokens.space.xs, paddingTop: tokens.space.xs }}>
                Att betala {kr(inv.totals.gross)}
              </div>
              {inv.paidAmount > 0 && (
                <>
                  <div style={{ color: tokens.color.textMuted }}>Betalt −{kr(inv.paidAmount)}</div>
                  <div style={{ fontWeight: 700, color: inv.remainingAmount > 0 ? tokens.color.accent : tokens.color.success }}>
                    Kvar {kr(inv.remainingAmount)}
                  </div>
                </>
              )}
            </div>

            {/* Betalningstalong */}
            {inv.type === "Invoice" && inv.remainingAmount > 0 && (
              <div style={{ borderTop: tokens.line.perforated, marginTop: tokens.space.lg, paddingTop: tokens.space.md, display: "flex", gap: tokens.space.xl, flexWrap: "wrap" }}>
                {inv.ocrNumber && (
                  <div>
                    <div style={{ color: tokens.color.textMuted, fontSize: tokens.font.size.sm, textTransform: "uppercase", letterSpacing: "0.08em" }}>OCR</div>
                    <div style={{ fontWeight: 700, fontVariantNumeric: "tabular-nums" }}>{inv.ocrNumber}</div>
                  </div>
                )}
                {inv.seller.bankgiro && (
                  <div>
                    <div style={{ color: tokens.color.textMuted, fontSize: tokens.font.size.sm, textTransform: "uppercase", letterSpacing: "0.08em" }}>Bankgiro</div>
                    <div style={{ fontWeight: 700, fontVariantNumeric: "tabular-nums" }}>{inv.seller.bankgiro}</div>
                  </div>
                )}
                {inv.seller.plusgiro && (
                  <div>
                    <div style={{ color: tokens.color.textMuted, fontSize: tokens.font.size.sm, textTransform: "uppercase", letterSpacing: "0.08em" }}>Plusgiro</div>
                    <div style={{ fontWeight: 700, fontVariantNumeric: "tabular-nums" }}>{inv.seller.plusgiro}</div>
                  </div>
                )}
                <div>
                  <div style={{ color: tokens.color.textMuted, fontSize: tokens.font.size.sm, textTransform: "uppercase", letterSpacing: "0.08em" }}>Belopp</div>
                  <div style={{ fontWeight: 700, fontVariantNumeric: "tabular-nums" }}>{kr(inv.remainingAmount)}</div>
                </div>
              </div>
            )}

            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginTop: tokens.space.xl, gap: tokens.space.md, flexWrap: "wrap" }}>
              <span style={{ color: tokens.color.textMuted, fontSize: tokens.font.size.sm }}>
                {inv.seller.fSkatt && "Godkänd för F-skatt · "}Dokument levererat via Faktura.
              </span>
              <Button onClick={() => window.open(`${BASE_URL}/api/public/invoices/${token}/pdf`, "_blank")}>
                Ladda ner PDF
              </Button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
