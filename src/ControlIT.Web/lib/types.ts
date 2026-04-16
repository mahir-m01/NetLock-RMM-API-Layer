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
  id: string;
  deviceName: string;
  platform: string;
  status: DeviceStatus;
  tenantId?: string;
  ipAddress?: string;
  macAddress?: string;
  osVersion?: string;
  agentVersion?: string;
  lastSeen?: string;
  createdAt?: string;
  [key: string]: unknown;
}

export interface DevicesResponse {
  data: Device[];
  total: number;
  page: number;
  pageSize: number;
}

// ─── Events ──────────────────────────────────────────────────────────────────

export interface DeviceEvent {
  id: string;
  timestamp: string;
  eventType: string;
  deviceId?: string;
  deviceName?: string;
  description?: string;
  [key: string]: unknown;
}

export interface EventsResponse {
  data: DeviceEvent[];
  total: number;
  page: number;
  pageSize: number;
}

// ─── Tenants ─────────────────────────────────────────────────────────────────

export interface Tenant {
  id: string;
  name: string;
  createdAt?: string;
  deviceCount?: number;
  [key: string]: unknown;
}

export interface TenantsResponse {
  data: Tenant[];
  total?: number;
}

// ─── Audit Logs ──────────────────────────────────────────────────────────────

export interface AuditLog {
  id: string;
  timestamp: string;
  action: string;
  deviceId?: string;
  deviceName?: string;
  tenantId?: string;
  tenantName?: string;
  status?: string;
  [key: string]: unknown;
}

export interface AuditLogsResponse {
  data: AuditLog[];
  total: number;
  limit: number;
  offset: number;
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
