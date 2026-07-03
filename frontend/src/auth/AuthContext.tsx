import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import { api, setAccessToken } from "../api/client";
import type { AuthResponse, MeResponse, OrganizationDto, UserDto } from "../api/types";

interface AuthState {
  status: "loading" | "authed" | "anon";
  user: UserDto | null;
  organization: OrganizationDto | null;
  login: (email: string, password: string) => Promise<void>;
  register: (organizationName: string, email: string, password: string) => Promise<void>;
  acceptInvitation: (token: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  refreshOrg: () => Promise<void>;
}

const AuthContext = createContext<AuthState | null>(null);

const ACCESS = "faktura.access";
const REFRESH = "faktura.refresh";

export function AuthProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<AuthState["status"]>("loading");
  const [user, setUser] = useState<UserDto | null>(null);
  const [organization, setOrganization] = useState<OrganizationDto | null>(null);

  function persist(auth: AuthResponse) {
    localStorage.setItem(ACCESS, auth.accessToken);
    localStorage.setItem(REFRESH, auth.refreshToken);
    setAccessToken(auth.accessToken);
    setUser(auth.user);
    setOrganization(auth.organization);
    setStatus("authed");
  }

  function clear() {
    localStorage.removeItem(ACCESS);
    localStorage.removeItem(REFRESH);
    setAccessToken(null);
    setUser(null);
    setOrganization(null);
    setStatus("anon");
  }

  useEffect(() => {
    const token = localStorage.getItem(ACCESS);
    if (!token) {
      setStatus("anon");
      return;
    }
    setAccessToken(token);
    api
      .get<MeResponse>("/api/me")
      .then((me) => {
        setUser(me.user);
        setOrganization(me.organization);
        setStatus("authed");
      })
      .catch(() => clear());
  }, []);

  const login = async (email: string, password: string) =>
    persist(await api.post<AuthResponse>("/api/auth/login", { email, password }));

  const register = async (organizationName: string, email: string, password: string) =>
    persist(await api.post<AuthResponse>("/api/auth/register", { organizationName, email, password }));

  const acceptInvitation = async (token: string, password: string) =>
    persist(await api.post<AuthResponse>(`/api/invitations/${token}/accept`, { password }));

  const logout = async () => {
    const refreshToken = localStorage.getItem(REFRESH);
    try {
      await api.post("/api/auth/logout", { refreshToken });
    } catch {
      // best-effort
    }
    clear();
  };

  const refreshOrg = async () => {
    const me = await api.get<MeResponse>("/api/me");
    setUser(me.user);
    setOrganization(me.organization);
  };

  return (
    <AuthContext.Provider value={{ status, user, organization, login, register, acceptInvitation, logout, refreshOrg }}>
      {children}
    </AuthContext.Provider>
  );
}

// eslint-disable-next-line react-refresh/only-export-components
export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}
