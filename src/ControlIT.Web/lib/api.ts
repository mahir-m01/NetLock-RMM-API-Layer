import { setAccessToken, getAccessToken, clearTokens } from "./auth";
import type {
  HealthResponse,
  DashboardStats,
  DevicesResponse,
  Device,
  EventsResponse,
  Tenant,
  AuditLog,
  ExecuteCommandRequest,
  ExecuteCommandResponse,
  DeviceFilters,
  LoginRequest,
  LoginResponse,
  UserSummary,
  CreateUserRequest,
  CreateUserResponse,
  ChangePasswordRequest,
} from "./types";

const BASE_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5290";

let _onUnauthenticated: (() => void) | null = null;

export function registerUnauthenticatedCallback(cb: () => void): void {
  _onUnauthenticated = cb;
}

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string
  ) {
    super(message);
    this.name = "ApiError";
  }
}

async function doFetch(path: string, options: RequestInit = {}): Promise<Response> {
  const token = getAccessToken();
  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    ...(options.headers as Record<string, string> | undefined),
  };
  if (token) headers["Authorization"] = `Bearer ${token}`;

  return fetch(`${BASE_URL}${path}`, {
    ...options,
    headers,
    credentials: "include", // always include — needed for refresh cookie
  });
}

async function tryRefresh(): Promise<boolean> {
  try {
    const res = await fetch(`${BASE_URL}/auth/refresh`, {
      method: "POST",
      credentials: "include",
      headers: { "Content-Type": "application/json" },
    });
    if (!res.ok) return false;
    const data = (await res.json()) as LoginResponse;
    setAccessToken(data.accessToken);
    return true;
  } catch {
    return false;
  }
}

async function request<T>(path: string, options: RequestInit = {}, skipAuth = false): Promise<T> {
  let res = await doFetch(path, options);

  // Attempt silent refresh on 401 (unless this IS the refresh/login call)
  if (res.status === 401 && !skipAuth) {
    const refreshed = await tryRefresh();
    if (refreshed) {
      res = await doFetch(path, options); // retry once with new token
    } else {
      clearTokens();
      _onUnauthenticated?.();
      throw new ApiError(401, "Session expired. Please log in again.");
    }
  }

  if (!res.ok) {
    const text = await res.text().catch(() => res.statusText);
    throw new ApiError(res.status, text);
  }

  const contentType = res.headers.get("content-type") ?? "";
  if (contentType.includes("application/json")) {
    return res.json() as Promise<T>;
  }
  return {} as T;
}

// ─── Auth ────────────────────────────────────────────────────────────────────

export async function loginUser(req: LoginRequest): Promise<LoginResponse> {
  const res = await fetch(`${BASE_URL}/auth/login`, {
    method: "POST",
    credentials: "include",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(req),
  });
  if (!res.ok) {
    const text = await res.text().catch(() => res.statusText);
    throw new ApiError(res.status, text);
  }
  const data = (await res.json()) as LoginResponse;
  setAccessToken(data.accessToken);
  return data;
}

export async function logoutUser(): Promise<void> {
  await request<void>("/auth/logout", { method: "POST" }).catch(() => {});
  clearTokens();
}

export async function refreshTokenFromCookie(): Promise<LoginResponse | null> {
  try {
    const res = await fetch(`${BASE_URL}/auth/refresh`, {
      method: "POST",
      credentials: "include",
      headers: { "Content-Type": "application/json" },
    });
    if (!res.ok) return null;
    const data = (await res.json()) as LoginResponse;
    setAccessToken(data.accessToken);
    return data;
  } catch {
    return null;
  }
}

export async function changePassword(req: ChangePasswordRequest): Promise<void> {
  await request<void>("/auth/change-password", {
    method: "POST",
    body: JSON.stringify(req),
  });
}

// ─── Users ───────────────────────────────────────────────────────────────────

export async function getUsers(): Promise<UserSummary[]> {
  return request<UserSummary[]>("/users");
}

export async function createUser(req: CreateUserRequest): Promise<CreateUserResponse> {
  return request<CreateUserResponse>("/users", {
    method: "POST",
    body: JSON.stringify(req),
  });
}

export async function patchUser(
  id: number,
  updates: Partial<{ isActive: boolean; role: string; tenantId: number | null }>
): Promise<UserSummary> {
  return request<UserSummary>(`/users/${id}`, {
    method: "PATCH",
    body: JSON.stringify(updates),
  });
}

// ─── Existing endpoints ───────────────────────────────────────────────────────

export async function getHealth(): Promise<HealthResponse> {
  return request<HealthResponse>("/health", {}, true);
}

export async function getDashboard(): Promise<DashboardStats> {
  return request<DashboardStats>("/dashboard");
}

export async function getDevices(
  page: number,
  pageSize: number,
  filters?: DeviceFilters
): Promise<DevicesResponse> {
  const params = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
  });
  if (filters?.platform) params.set("platform", filters.platform);
  if (filters?.search) params.set("search", filters.search);
  return request<DevicesResponse>(`/devices?${params.toString()}`);
}

export async function getDevice(id: string): Promise<Device> {
  return request<Device>(`/devices/${id}`);
}

export async function getEvents(
  page: number,
  pageSize: number
): Promise<EventsResponse> {
  const params = new URLSearchParams({
    page: String(page),
    pageSize: String(pageSize),
  });
  return request<EventsResponse>(`/events?${params.toString()}`);
}

export async function getTenants(): Promise<Tenant[]> {
  return request<Tenant[]>("/tenants");
}

export async function getAuditLogs(
  limit: number,
  offset: number,
  from?: string,
  to?: string
): Promise<AuditLog[]> {
  const params = new URLSearchParams({
    limit: String(limit),
    offset: String(offset),
  });
  if (from) params.set("from", from);
  if (to) params.set("to", to);
  return request<AuditLog[]>(`/audit/logs?${params.toString()}`);
}

export async function executeCommand(
  payload: ExecuteCommandRequest
): Promise<ExecuteCommandResponse> {
  return request<ExecuteCommandResponse>("/commands/execute", {
    method: "POST",
    body: JSON.stringify(payload),
  });
}
