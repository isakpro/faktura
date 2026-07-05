import { afterEach, describe, expect, it, vi } from "vitest";
import { api, ApiError, setAccessToken } from "./client";

function jsonResponse(status: number, body?: unknown) {
  const text = body === undefined ? "" : JSON.stringify(body);
  return {
    ok: status >= 200 && status < 300,
    status,
    json: async () => JSON.parse(text),
    text: async () => text,
  };
}

describe("api client", () => {
  afterEach(() => {
    setAccessToken(null);
    vi.unstubAllGlobals();
  });

  it("skickar Authorization-header när token är satt", async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(200, { ok: true }));
    vi.stubGlobal("fetch", fetchMock);
    setAccessToken("token-123");

    await api.get("/api/me");

    const [, init] = fetchMock.mock.calls[0];
    expect((init.headers as Record<string, string>).Authorization).toBe("Bearer token-123");
  });

  it("utelämnar Authorization utan token", async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(200, {}));
    vi.stubGlobal("fetch", fetchMock);

    await api.get("/api/health");

    const [, init] = fetchMock.mock.calls[0];
    expect((init.headers as Record<string, string>).Authorization).toBeUndefined();
  });

  it("kastar ApiError med problem+json-detalj vid fel", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(
      jsonResponse(409, { title: "E-post upptagen", detail: "E-postadressen är redan registrerad." })));

    const error = await api.post("/api/auth/register", {}).catch((e) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(409);
    expect((error as ApiError).message).toBe("E-postadressen är redan registrerad.");
  });

  it("returnerar undefined för 204", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(jsonResponse(204)));

    await expect(api.del("/api/members/x")).resolves.toBeUndefined();
  });
});
