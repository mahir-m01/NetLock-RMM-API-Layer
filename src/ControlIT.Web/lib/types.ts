// ─── Auth ────────────────────────────────────────────────────────────────────

export type Role = "SuperAdmin" | "CpAdmin" | "ClientAdmin" | "Technician";

export interface AuthUser {
  id: number;
  email: string;
  role: Role;
  tenantId: number | null;
  mustChangePassword: boolean;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  expiresIn: number;
  user: AuthUser;
}

export interface UserSummary {
  id: number;
  email: string;
  role: Role;
  tenantId: number | null;
  isActive: boolean;
  mustChangePassword: boolean;
  createdAt: string;
  lastLoginAt: string | null;
}

export interface CreateUserRequest {
  email: string;
  role: Role;
  tenantId: number | null;
  assignedClients: number[] | null;
}

export interface CreateUserResponse {
  id: number;
  email: string;
  role: Role;
  generatedPassword: string;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

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
  criticalAlerts: number;
}

// ─── Devices ─────────────────────────────────────────────────────────────────

export interface Device {
  id: number;
  deviceName: string;
  platform: string;
  operatingSystem?: string;
  ipAddressInternal?: string;
  cpuUsage?: number;
  ramUsage?: number;
  isOnline: boolean;
  lastAccess?: string;
  tenantId?: number;
  [key: string]: unknown;
}

export interface DevicesResponse {
  items: Device[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
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
  guid: string;
  name: string;
  locations: unknown[];
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
