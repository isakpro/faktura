import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen, waitFor } from "@testing-library/react";
import { fireEvent } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import App from "./App";
import { AuthProvider } from "./auth/AuthContext";
import { setAccessToken } from "./api/client";

const me = {
  user: { id: "u1", email: "owner@acme.se", role: "Owner" },
  organization: { id: "t1", name: "Acme AB", plan: "Free", subscriptionStatus: "None", seatLimit: 2 },
};

const authResponse = {
  accessToken: "access-1",
  refreshToken: "refresh-1",
  expiresAt: new Date(Date.now() + 900_000).toISOString(),
  ...me,
};

/** Fetch-mock som routar på URL — tomma listor/standardsvar för Dashboard-frågorna. */
function stubFetchRoutes() {
  vi.stubGlobal("fetch", vi.fn(async (url: string) => {
    const path = String(url);
    const body =
      path.includes("/api/auth/login") ? authResponse :
      path.includes("/api/members") ? [] :
      path.endsWith("/api/me") ? me :
      path.includes("/api/dashboard")
        ? { outstanding: 0, overdue: 0, paidThisYear: 0, monthlyRevenue: [], recentInvoices: [] } :
      path.includes("/api/invitations") ? [] :
      path.includes("/api/billing") ? { plan: "Free", subscriptionStatus: "None", seatLimit: 2 } :
      path.includes("/api/reminder-settings") ? { autoEnabled: false, daysAfterDue: 7 } :
      {};
    const text = JSON.stringify(body);
    return { ok: true, status: 200, json: async () => JSON.parse(text), text: async () => text };
  }));
}

function renderApp() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={["/"]}>
        <AuthProvider>
          <App />
        </AuthProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe("App auth-flöde", () => {
  beforeEach(() => stubFetchRoutes());

  afterEach(() => {
    cleanup();
    localStorage.clear();
    setAccessToken(null);
    vi.unstubAllGlobals();
  });

  it("skyddad route: anonym besökare skickas till inloggningen", async () => {
    localStorage.clear();

    renderApp();

    expect(await screen.findByRole("heading", { name: "Logga in" })).toBeTruthy();
  });

  it("inloggning: formuläret loggar in och visar organisationens dashboard", async () => {
    renderApp();
    await screen.findByRole("heading", { name: "Logga in" });

    const inputs = document.querySelectorAll("input");
    fireEvent.change(inputs[0], { target: { value: "owner@acme.se" } });
    fireEvent.change(inputs[1], { target: { value: "password1" } });
    fireEvent.submit(document.querySelector("form")!);

    expect(await screen.findByText("Acme AB")).toBeTruthy();
    await waitFor(() => expect(localStorage.getItem("faktura.access")).toBe("access-1"));
    expect(localStorage.getItem("faktura.refresh")).toBe("refresh-1");
  });

  it("bootstrap: sparad token ger inloggat läge via /api/me", async () => {
    localStorage.setItem("faktura.access", "stored-token");

    renderApp();

    expect(await screen.findByText("Acme AB")).toBeTruthy();
    expect(screen.queryByRole("heading", { name: "Logga in" })).toBeNull();
  });
});
