export const BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5080").replace(/\/$/, "");

let accessToken: string | null = null;
export function setAccessToken(token: string | null) {
  accessToken = token;
}
export function getAccessToken() {
  return accessToken;
}

export class ApiError extends Error {
  status: number;
  constructor(status: number, message: string) {
    super(message);
    this.status = status;
  }
}

async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  const headers: Record<string, string> = { "Content-Type": "application/json" };
  if (accessToken) headers.Authorization = `Bearer ${accessToken}`;

  const res = await fetch(`${BASE_URL}${path}`, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
  });

  if (!res.ok) {
    let message = `Fel (${res.status})`;
    try {
      const problem = await res.json();
      if (problem?.title || problem?.detail) message = problem.detail ?? problem.title;
    } catch {
      // non-JSON error body
    }
    throw new ApiError(res.status, message);
  }

  if (res.status === 204) return undefined as T;
  const text = await res.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

export const api = {
  get: <T>(path: string) => request<T>("GET", path),
  post: <T>(path: string, body?: unknown) => request<T>("POST", path, body),
  put: <T>(path: string, body?: unknown) => request<T>("PUT", path, body),
  del: <T>(path: string) => request<T>("DELETE", path),
};

/** Öppnar en autentiserad binär resurs (t.ex. PDF) i en ny flik. */
export async function openAuthed(path: string): Promise<void> {
  const headers: Record<string, string> = {};
  if (accessToken) headers.Authorization = `Bearer ${accessToken}`;
  const res = await fetch(`${BASE_URL}${path}`, { headers });
  if (!res.ok) throw new ApiError(res.status, `Kunde inte hämta (${res.status})`);
  const url = URL.createObjectURL(await res.blob());
  window.open(url, "_blank");
}
