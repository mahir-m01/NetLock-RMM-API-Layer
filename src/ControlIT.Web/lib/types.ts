// ─── Health ──────────────────────────────────────────────────────────────────

export interface HealthResponse {
  status: string;
}

// ─── Dashboard ───────────────────────────────────────────────────────────────

export interface DashboardStats {
  totalDevices: number;
  onlineDevices: number;
  totalTenants: number;
  totalEvents: number;
}

// ─── Devices ─────────────────────────────────────────────────────────────────

export type DeviceStatus = "online" | "offline" | string;

export interface Device {
  id: number;
  deviceName: string;
  platform: string;
  status: DeviceStatus;
  tenantId?: number;
  ipAddress?: string;
  macAddress?: string;
  osVersion?: string;
  agentVersion?: string;
  lastSeen?: string;
  createdAt?: string;
  [key: string]: unknown;
}

export interface DevicesResponse {
  items: Device[];
  totalCount: number;
  page: number;
  pageSize: number;
}

// ─── Events ──────────────────────────────────────────────────────────────────

export interface DeviceEvent {
  id: number;
  timestamp: string;
  eventType: string;
  deviceId?: number;
  deviceName?: string;
  description?: string;
  [key: string]: unknown;
}

export interface EventsResponse {
  items: DeviceEvent[];
  totalCount: number;
  page: number;
  pageSize: number;
}

// ─── Tenants ─────────────────────────────────────────────────────────────────

export interface Tenant {
  id: number;
  name: string;
  createdAt?: string;
  deviceCount?: number;
  [key: string]: unknown;
}

// ─── Audit Logs ──────────────────────────────────────────────────────────────

export interface AuditLog {
  id: number;
  timestamp: string;
  action: string;
  deviceId?: number;
  deviceName?: string;
  tenantId?: number;
  tenantName?: string;
  status?: string;
  [key: string]: unknown;
}

// ─── Commands ────────────────────────────────────────────────────────────────

export type Shell = "bash" | "powershell" | "cmd";

export interface ExecuteCommandRequest {
  deviceId: string;
  command: string;
  shell: Shell;
  timeoutSeconds: number;
}

export interface ExecuteCommandResponse {
  output?: string;
  exitCode?: number;
  error?: string;
  executedAt?: string;
  [key: string]: unknown;
}

// ─── Pagination ───────────────────────────────────────────────────────────────

export interface PaginationParams {
  page: number;
  pageSize: number;
}

export interface DeviceFilters {
  platform?: string;
  search?: string;
}
