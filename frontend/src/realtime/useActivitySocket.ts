import { useEffect } from "react";
import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { useQueryClient } from "@tanstack/react-query";
import { BASE_URL, getAccessToken } from "../api/client";

/**
 * Kopplar upp mot realtidskanalen (spec 017) när användaren är inloggad. Vid mottagen
 * aktivitet invalideras de frågor som visar den, så TanStack Query hämtar färsk data —
 * ingen manuell state-hantering av själva händelsen behövs.
 */
export function useActivitySocket(enabled: boolean) {
  const queryClient = useQueryClient();

  useEffect(() => {
    if (!enabled) return;

    const connection = new HubConnectionBuilder()
      // Autentisering sker via bearer-token (accessTokenFactory), inga cookies —
      // withCredentials: false undviker att CORS kräver Access-Control-Allow-Credentials.
      .withUrl(`${BASE_URL}/hubs/activity`, { accessTokenFactory: () => getAccessToken() ?? "", withCredentials: false })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on("activity", () => {
      queryClient.invalidateQueries({ queryKey: ["audit"] });
      queryClient.invalidateQueries({ queryKey: ["dashboard"] });
    });

    connection.start().catch(() => {
      // Best-effort: ingen realtid är inte kritiskt — sidorna funkar ändå via vanlig hämtning.
    });

    return () => {
      connection.stop();
    };
  }, [enabled, queryClient]);
}
