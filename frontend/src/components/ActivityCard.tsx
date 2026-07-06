import { useQuery } from "@tanstack/react-query";
import { api } from "../api/client";
import { Card } from "./ui";
import { tokens } from "../theme/tokens";

interface AuditEntryDto {
  actorEmail: string;
  method: string;
  path: string;
  statusCode: number;
  occurredAt: string;
}

/** Översätter metod + sökväg till en läsbar svensk åtgärd. */
function describeAction(e: AuditEntryDto): string {
  const p = e.path;
  const is = (pattern: RegExp) => pattern.test(p);

  if (is(/^\/api\/invoices\/[^/]+\/send$/)) return "Skickade faktura";
  if (is(/^\/api\/invoices\/[^/]+\/mark-paid$/)) return "Markerade faktura betald";
  if (is(/^\/api\/invoices\/[^/]+\/credit$/)) return "Skapade kreditfaktura";
  if (is(/^\/api\/invoices\/[^/]+\/email$/)) return "Mejlade faktura";
  if (is(/^\/api\/invoices\/[^/]+\/remind$/)) return "Skickade betalningspåminnelse";
  if (is(/^\/api\/invoices\/[^/]+$/)) return "Ändrade fakturautkast";
  if (is(/^\/api\/invoices$/)) return "Skapade fakturautkast";
  if (is(/^\/api\/customers\/[^/]+\/archive$/)) return "Arkiverade kund";
  if (is(/^\/api\/customers\/[^/]+$/)) return "Ändrade kund";
  if (is(/^\/api\/customers$/)) return "Skapade kund";
  if (is(/^\/api\/articles\/[^/]+\/archive$/)) return "Arkiverade artikel";
  if (is(/^\/api\/articles\/[^/]+$/)) return "Ändrade artikel";
  if (is(/^\/api\/articles$/)) return "Skapade artikel";
  if (is(/^\/api\/recurring-invoices\/[^/]+\/pause$/)) return "Pausade abonnemang";
  if (is(/^\/api\/recurring-invoices\/[^/]+\/resume$/)) return "Återupptog abonnemang";
  if (is(/^\/api\/recurring-invoices/)) return e.method === "POST" ? "Startade abonnemang" : "Ändrade abonnemang";
  if (is(/^\/api\/invitations\/[^/]+$/) && e.method === "DELETE") return "Återkallade inbjudan";
  if (is(/^\/api\/invitations$/)) return "Bjöd in användare";
  if (is(/^\/api\/members\/[^/]+\/role$/)) return "Ändrade roll";
  if (is(/^\/api\/members\/[^/]+$/) && e.method === "DELETE") return "Tog bort medlem";
  if (is(/^\/api\/reminder-settings$/)) return "Ändrade påminnelseinställningar";
  if (is(/^\/api\/billing\/checkout$/)) return "Startade Pro-uppgradering";
  return `${e.method} ${p}`;
}

const time = (iso: string) =>
  new Date(iso).toLocaleString("sv-SE", { month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" });

/** Aktivitetslogg (Owner/Admin): vem gjorde vad när — append-only från API:ts audit-middleware. */
export function ActivityCard() {
  const audit = useQuery({ queryKey: ["audit"], queryFn: () => api.get<AuditEntryDto[]>("/api/audit") });

  if (!audit.data || audit.data.length === 0) return null;

  return (
    <Card>
      <h2 style={{ marginTop: 0, fontSize: tokens.font.size.lg }}>Aktivitet</h2>
      <ul style={{ listStyle: "none", padding: 0, margin: 0 }}>
        {audit.data.slice(0, 10).map((e, i) => (
          <li
            key={i}
            style={{
              display: "flex",
              justifyContent: "space-between",
              gap: tokens.space.md,
              padding: tokens.space.sm,
              borderTop: tokens.line.perforated,
              fontSize: tokens.font.size.sm,
            }}
          >
            <span>
              <strong>{describeAction(e)}</strong>
              {e.statusCode >= 400 && <span style={{ color: tokens.color.danger }}> (nekades)</span>}
            </span>
            <span style={{ color: tokens.color.textMuted, whiteSpace: "nowrap" }}>
              {e.actorEmail} · {time(e.occurredAt)}
            </span>
          </li>
        ))}
      </ul>
    </Card>
  );
}
