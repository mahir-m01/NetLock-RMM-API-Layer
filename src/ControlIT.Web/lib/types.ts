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
  tenantId: number;
  deviceName: string;
  platform: string;
  operatingSystem: string;
  ipAddressInternal: string;
  ipAddressExternal: string;
  agentVersion: string;
  cpu: string;
  ram: string;
  cpuUsage: number | null;
  ramUsage: number | null;
  isOnline: boolean;
  lastAccess: string;
  netbirdIp?: string | null;
  netBirdIp?: string | null;
  netbirdPeerId?: string | null;
  netBirdPeerId?: string | null;
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
  event: string;
  severity: string;
  deviceName?: string;
  description?: string;
}

export interface EventsResponse {
  items: DeviceEvent[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
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
  tenantId: number;
  actorEmail: string;
  action: string;
  resourceType: string;
  resourceId: string | null;
  ipAddress: string | null;
  result: string;
  errorMessage: string | null;
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

export interface BatchCommandRequest {
  deviceIds: number[];
  command: string;
  shell: Shell;
  timeoutSeconds: number;
}

export interface BatchCommandResult {
  deviceId: number;
  status: "SUCCESS" | "TIMEOUT" | "FAILURE" | string;
  message: string;
  output?: string;
  executedAt?: string;
}

export interface BatchCommandResponse {
  requestedCount: number;
  successCount: number;
  failureCount: number;
  results: BatchCommandResult[];
}

// ─── System Health ───────────────────────────────────────────────────────────

export interface ComponentHealth {
  status: "healthy" | "degraded" | "unhealthy";
  latencyMs?: number;
  detail?: string;
}

export interface SystemHealthResponse {
  status: "healthy" | "degraded" | "unhealthy";
  checkedAt: string;
  mysql: ComponentHealth;
  signalR: ComponentHealth;
  netBird: ComponentHealth;
  api: {
    version: string;
    environment: string;
    uptime: string;
    connectedDevices: number;
  };
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

// ─── Netbird Network ────────────────────────────────────────────────────────

export interface NetbirdPeer {
  id: string;
  name: string;
  ip: string;
  os: string;
  connected: boolean;
  lastSeen: string;
  hostname: string;
  dnsLabel: string;
  version: string;
  countryCode: string;
  cityName: string;
  connectionIp: string;
  sshEnabled: boolean;
  loginExpired: boolean;
  accessiblePeersCount: number;
  groups: { id: string; name: string }[];
  createdAt: string;
}

export interface NetbirdGroup {
  id: string;
  name: string;
  peersCount: number;
  resourcesCount: number;
  issued: string;
  peers: { id: string; name: string }[];
}

export interface NetbirdSetupKey {
  id: string;
  name: string;
  key: string;
  type: "one-off" | "reusable";
  valid: boolean;
  revoked: boolean;
  usedTimes: number;
  usageLimit: number;
  expires: string;
  autoGroups: string[];
  ephemeral: boolean;
  state: string;
}

// Returned only by POST /network/setup-keys. key is raw and shown once; never from list endpoints.
export type NetbirdSetupKeyCreateResponse = NetbirdSetupKey;

export interface NetbirdPolicy {
  id: string;
  name: string;
  description: string;
  enabled: boolean;
  rules: {
    name: string;
    action: string;
    bidirectional: boolean;
    protocol: string;
    sources: string[];
    destinations: string[];
    ports: string[];
  }[];
}

export interface NetbirdRoute {
  id: string;
  description: string;
  networkId: string;
  network: string;
  networkType: string;
  enabled: boolean;
  peer: string;
  metric: number;
  masquerade: boolean;
  groups: string[];
}

export interface NetworkSummary {
  totalPeers: number;
  connectedPeers: number;
  tenantPeers: number;
  tenantConnectedPeers: number;
  setupKeysActive: number;
  routeCount: number;
}

export interface CreateSetupKeyApiRequest {
  name: string;
  type: "one-off" | "reusable";
  expiresInDays: number;
  usageLimit: number;
  ephemeral: boolean;
}

export type TenantNetbirdGroupMode = "external" | "read_only";

export interface BindTenantGroupRequest {
  groupId: string;
  mode: TenantNetbirdGroupMode;
}
