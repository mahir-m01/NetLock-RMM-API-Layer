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
} from "./types";

const BASE_URL =
  process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5290";

const API_KEY_STORAGE_KEY = "controlit_api_key";

function getApiKey(): string {
  if (typeof window === "undefined") return "";
  return localStorage.getItem(API_KEY_STORAGE_KEY) ?? "";
}

export function saveApiKey(key: string): void {
  if (typeof window === "undefined") return;
  localStorage.setItem(API_KEY_STORAGE_KEY, key);
}

export function readApiKey(): string {
  return getApiKey();
}

async function request<T>(
  path: string,
  options: RequestInit = {},
  skipApiKey = false
): Promise<T> {
  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    ...(options.headers as Record<string, string> | undefined),
  };

  if (!skipApiKey) {
    const key = getApiKey();
    if (key) {
      headers["x-api-key"] = key;
    }
  }

  const res = await fetch(`${BASE_URL}${path}`, {
    ...options,
    headers,
  });

  if (!res.ok) {
    const text = await res.text().catch(() => res.statusText);
    throw new ApiError(res.status, text);
  }

  // Some endpoints may return empty body
  const contentType = res.headers.get("content-type") ?? "";
  if (contentType.includes("application/json")) {
    return res.json() as Promise<T>;
  }
  return {} as T;
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

// ─── Endpoints ───────────────────────────────────────────────────────────────

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
