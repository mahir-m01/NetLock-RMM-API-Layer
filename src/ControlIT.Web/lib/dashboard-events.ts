import type { DashboardStats, Device, DevicesResponse, NetworkSummary } from "./types";

export const PUSH_EVENT_VERSION = 1;
export const RECENT_DEVICE_LIMIT = 10;

export type PushEventType =
  | "device.online"
  | "device.offline"
  | "device.updated"
  | "command.status"
  | "netbird.peer.updated"
  | "system.health.updated";

export interface ControlItPushEvent {
  version: typeof PUSH_EVENT_VERSION;
  type: PushEventType;
  emittedAt: string;
  tenantId?: number | null;
  payload: unknown;
}

export interface DashboardLiveState {
  stats?: Partial<DashboardStats>;
  devicesData?: DevicesResponse;
  networkSummary?: NetworkSummary;
  networkSummaryTenantId?: number | null;
  degradedReason?: string;
  lastEventAt?: string;
}

const PUSH_EVENT_TYPES = new Set<PushEventType>([
  "device.online",
  "device.offline",
  "device.updated",
  "command.status",
  "netbird.peer.updated",
  "system.health.updated",
]);

const SECRET_FIELD = /(accesskey|access_key|token|secret|password|setupkey|setup_key|apikey|api_key)$/i;

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function getNumber(value: unknown): number | undefined {
  return typeof value === "number" && Number.isFinite(value) ? value : undefined;
}

function getString(value: unknown): string | undefined {
  return typeof value === "string" ? value : undefined;
}

function stripSecrets(value: unknown): unknown {
  if (Array.isArray(value)) return value.map(stripSecrets);
  if (!isRecord(value)) return value;

  return Object.fromEntries(
    Object.entries(value)
      .filter(([key]) => !SECRET_FIELD.test(key))
      .map(([key, nested]) => [key, stripSecrets(nested)])
  );
}

function coerceDevice(raw: unknown): Device | null {
  if (!isRecord(raw)) return null;

  const id = getNumber(raw.id) ?? getNumber(raw.deviceId);
  const tenantId = getNumber(raw.tenantId);
  const deviceName = getString(raw.deviceName) ?? getString(raw.name);
  if (id === undefined || tenantId === undefined || !deviceName) return null;

  return {
    id,
    tenantId,
    deviceName,
    platform: getString(raw.platform) ?? "unknown",
    operatingSystem: getString(raw.operatingSystem) ?? getString(raw.os) ?? "unknown",
    ipAddressInternal: getString(raw.ipAddressInternal) ?? "",
    ipAddressExternal: getString(raw.ipAddressExternal) ?? "",
    agentVersion: getString(raw.agentVersion) ?? "",
    cpu: getString(raw.cpu) ?? "",
    ram: getString(raw.ram) ?? "",
    cpuUsage: getNumber(raw.cpuUsage) ?? null,
    ramUsage: getNumber(raw.ramUsage) ?? null,
    isOnline: typeof raw.isOnline === "boolean" ? raw.isOnline : false,
    lastAccess: getString(raw.lastAccess) ?? new Date().toISOString(),
    netbirdIp: getString(raw.netbirdIp) ?? getString(raw.netBirdIp) ?? null,
    netbirdPeerId: getString(raw.netbirdPeerId) ?? getString(raw.netBirdPeerId) ?? null,
  };
}

function extractDevice(payload: unknown): Device | null {
  if (!isRecord(payload)) return null;
  return coerceDevice(payload.device) ?? coerceDevice(payload);
}

function extractStats(payload: unknown): Partial<DashboardStats> | undefined {
  if (!isRecord(payload)) return undefined;
  const source = isRecord(payload.dashboard)
    ? payload.dashboard
    : isRecord(payload.stats)
      ? payload.stats
      : payload;

  const stats: Partial<DashboardStats> = {};
  const totalDevices = getNumber(source.totalDevices);
  const onlineDevices = getNumber(source.onlineDevices);
  const totalTenants = getNumber(source.totalTenants);
  const totalEvents = getNumber(source.totalEvents);
  const criticalAlerts = getNumber(source.criticalAlerts);

  if (totalDevices !== undefined) stats.totalDevices = totalDevices;
  if (onlineDevices !== undefined) stats.onlineDevices = onlineDevices;
  if (totalTenants !== undefined) stats.totalTenants = totalTenants;
  if (totalEvents !== undefined) stats.totalEvents = totalEvents;
  if (criticalAlerts !== undefined) stats.criticalAlerts = criticalAlerts;

  return Object.keys(stats).length > 0 ? stats : undefined;
}

function extractNetworkSummary(payload: unknown): NetworkSummary | undefined {
  if (!isRecord(payload)) return undefined;
  const source = isRecord(payload.networkSummary) ? payload.networkSummary : payload;
  const tenantPeers = getNumber(source.tenantPeers);
  const tenantConnectedPeers = getNumber(source.tenantConnectedPeers);
  const setupKeysActive = getNumber(source.setupKeysActive);
  const routeCount = getNumber(source.routeCount);

  if (
    tenantPeers === undefined ||
    tenantConnectedPeers === undefined ||
    setupKeysActive === undefined ||
    routeCount === undefined
  ) {
    return undefined;
  }

  return {
    totalPeers: getNumber(source.totalPeers) ?? tenantPeers,
    connectedPeers: getNumber(source.connectedPeers) ?? tenantConnectedPeers,
    tenantPeers,
    tenantConnectedPeers,
    setupKeysActive,
    routeCount,
  };
}

function upsertDevice(
  current: DevicesResponse | undefined,
  device: Device,
  onlineOverride?: boolean
): DevicesResponse {
  const existing = current?.items ?? [];
  const nextDevice = { ...device, isOnline: onlineOverride ?? device.isOnline };
  const withoutCurrent = existing.filter((item) => item.id !== nextDevice.id);
  const items = [nextDevice, ...withoutCurrent].slice(0, RECENT_DEVICE_LIMIT);

  return {
    items,
    totalCount: Math.max(current?.totalCount ?? items.length, items.length),
    page: current?.page ?? 1,
    pageSize: current?.pageSize ?? RECENT_DEVICE_LIMIT,
    totalPages: current?.totalPages ?? 1,
  };
}

function updateKnownDevice(
  current: DevicesResponse | undefined,
  payload: unknown,
  isOnline: boolean
): DevicesResponse | undefined {
  if (!isRecord(payload)) return current;
  const id = getNumber(payload.deviceId) ?? getNumber(payload.id);
  if (id === undefined || !current) return current;

  return {
    ...current,
    items: current.items.map((device) =>
      device.id === id
        ? { ...device, isOnline, lastAccess: getString(payload.lastAccess) ?? device.lastAccess }
        : device
    ),
  };
}

function mergeStatsWithDeviceEvent(
  stats: Partial<DashboardStats> | undefined,
  currentDevices: DevicesResponse | undefined
): Partial<DashboardStats> | undefined {
  if (!stats || !currentDevices) return stats;
  return {
    ...stats,
    onlineDevices: currentDevices.items.filter((device) => device.isOnline).length,
    totalDevices: Math.max(stats.totalDevices ?? 0, currentDevices.totalCount),
  };
}

export function parseSseData(chunk: string): unknown[] {
  return chunk
    .split(/\n\n+/)
    .map((frame) =>
      frame
        .split(/\r?\n/)
        .filter((line) => line.startsWith("data:"))
        .map((line) => line.slice(5).trimStart())
        .join("\n")
    )
    .filter(Boolean)
    .map((data) => JSON.parse(data) as unknown);
}

export function normalizePushEvent(raw: unknown): ControlItPushEvent | null {
  if (!isRecord(raw)) return null;

  if (
    raw.version === PUSH_EVENT_VERSION &&
    typeof raw.type === "string" &&
    PUSH_EVENT_TYPES.has(raw.type as PushEventType)
  ) {
    return {
      version: PUSH_EVENT_VERSION,
      type: raw.type as PushEventType,
      emittedAt: getString(raw.emittedAt) ?? new Date().toISOString(),
      tenantId: getNumber(raw.tenantId) ?? null,
      payload: stripSecrets(raw.payload),
    };
  }

  if (typeof raw.syncedAt === "string") {
    return {
      version: PUSH_EVENT_VERSION,
      type: "system.health.updated",
      emittedAt: raw.syncedAt,
      tenantId: null,
      payload: stripSecrets({
        dashboard: {
          onlineDevices: raw.onlineDevices,
          totalDevices: raw.totalDevices,
        },
      }),
    };
  }

  return null;
}

export function applyDashboardEvent(
  state: DashboardLiveState,
  event: ControlItPushEvent,
  selectedTenantId?: number,
  acceptTenantScopedStats = true
): DashboardLiveState {
  const payload = event.payload;
  const next: DashboardLiveState = {
    ...state,
    lastEventAt: event.emittedAt,
  };

  const canApplyStats = event.tenantId === null || event.tenantId === undefined || acceptTenantScopedStats;
  const stats = canApplyStats ? extractStats(payload) : undefined;
  if (stats) next.stats = { ...next.stats, ...stats };

  const networkSummary = extractNetworkSummary(payload);
  const tenantMatches =
    selectedTenantId === undefined ||
    event.tenantId === null ||
    event.tenantId === selectedTenantId;
  if (networkSummary && tenantMatches) {
    next.networkSummary = networkSummary;
    next.networkSummaryTenantId = event.tenantId ?? selectedTenantId ?? null;
  }

  if (event.type === "device.updated") {
    const device = extractDevice(payload);
    if (device) next.devicesData = upsertDevice(next.devicesData, device);
  }

  if (event.type === "device.online" || event.type === "device.offline") {
    const device = extractDevice(payload);
    const isOnline = event.type === "device.online";
    next.devicesData = device
      ? upsertDevice(next.devicesData, device, isOnline)
      : updateKnownDevice(next.devicesData, payload, isOnline);
    next.stats = mergeStatsWithDeviceEvent(next.stats, next.devicesData);
  }

  if (event.type === "system.health.updated" && isRecord(payload)) {
    const status = getString(payload.status);
    next.degradedReason =
      status && status !== "healthy"
        ? getString(payload.detail) ?? getString(payload.reason) ?? "Live stream degraded."
        : undefined;
  }

  return next;
}
