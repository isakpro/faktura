import type { MonthlyRevenueDto } from "../api/types";
import { tokens } from "../theme/tokens";

const MONTHS = ["jan", "feb", "mar", "apr", "maj", "jun", "jul", "aug", "sep", "okt", "nov", "dec"];
const kr = (n: number) => `${n.toLocaleString("sv-SE")} kr`;

/**
 * Omsättning per månad — enkelserie-stapeldiagram i Huvudboken-stil (ren SVG, inga bibliotek).
 * En serie ⇒ ingen legend (rubriken namnger den). Bläckstaplar med rundade datatoppar,
 * 2px yt-gap mellan staplar, selektiv direktetikett på maxvärdet, hover-tooltip per stapel.
 */
export function RevenueChart({ data }: { data: MonthlyRevenueDto[] }) {
  if (data.length === 0) return null;

  const width = 660;
  const height = 150;
  const labelZone = 18;
  const topZone = 16; // plats för maxvärdets etikett
  const plotHeight = height - labelZone - topZone;
  const barGap = 2;
  const barWidth = width / data.length - barGap;
  const max = Math.max(...data.map((m) => m.gross), 1);
  const maxIndex = data.findIndex((m) => m.gross === max && max > 0);

  return (
    <svg
      viewBox={`0 0 ${width} ${height}`}
      style={{ width: "100%", height: "auto", display: "block" }}
      role="img"
      aria-label={`Omsättning per månad: ${data.map((m) => `${MONTHS[m.month - 1]} ${kr(m.gross)}`).join(", ")}`}
    >
      {data.map((m, i) => {
        const barHeight = max === 0 ? 0 : (m.gross / max) * plotHeight;
        const x = i * (barWidth + barGap);
        const y = topZone + plotHeight - barHeight;
        return (
          <g key={`${m.year}-${m.month}`}>
            {/* Rundad datatopp, rak baslinje: topphalvan rundas, botten täcks av en rak rect. */}
            {barHeight > 0 && (
              <>
                <rect x={x} y={y} width={barWidth} height={barHeight} rx={4} fill={tokens.color.primary} />
                {barHeight > 4 && (
                  <rect x={x} y={topZone + plotHeight - Math.min(4, barHeight)} width={barWidth} height={Math.min(4, barHeight)} fill={tokens.color.primary} />
                )}
              </>
            )}
            {/* Hover-tooltip (native) med större träffyta än stapeln. */}
            <rect x={x} y={0} width={barWidth + barGap} height={height - labelZone} fill="transparent">
              <title>{`${MONTHS[m.month - 1]} ${m.year} · ${kr(m.gross)}`}</title>
            </rect>
            {i === maxIndex && (
              <text
                x={x + barWidth / 2}
                y={y - 5}
                textAnchor="middle"
                fontSize="11"
                fontWeight="700"
                fill={tokens.color.text}
                style={{ fontVariantNumeric: "tabular-nums" }}
              >
                {kr(m.gross)}
              </text>
            )}
            <text
              x={x + barWidth / 2}
              y={height - 5}
              textAnchor="middle"
              fontSize="10"
              fill={tokens.color.textMuted}
            >
              {MONTHS[m.month - 1]}
            </text>
          </g>
        );
      })}
      {/* Baslinje — recessiv hårlinje. */}
      <line x1={0} x2={width} y1={topZone + plotHeight} y2={topZone + plotHeight} stroke={tokens.color.border} strokeWidth={1} />
    </svg>
  );
}
