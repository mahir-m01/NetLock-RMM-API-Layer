"use client";

import { useState } from "react";
import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { getDevices, getNetworkSummary, getTenants } from "@/lib/api";
import type { Device } from "@/lib/types";
import { useDashboardStream, type StreamStatus } from "./use-dashboard-stream";
import { mergeDeviceNetbirdFields, NetbirdStatus } from "@/components/network/netbird-status";
import {
  Monitor,
  Wifi,
  WifiOff,
  Building2,
  Zap,
  Terminal,
  ClipboardList,
  Network,
  Server,
} from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { useAuth } from "@/components/providers/auth-provider";

// ─── KPI stat card ────────────────────────────────────────────────────────────

interface StatCardProps {
  title: string;
  value: number | undefined;
  icon: React.ElementType;
  loading: boolean;
  accent?: "blue" | "red" | "default";
}

function StatCard({ title, value, icon: Icon, loading, accent = "default" }: StatCardProps) {
  const accentClass =
    accent === "blue"
      ? "text-blue-400"
      : accent === "red" && (value ?? 0) > 0
      ? "text-red-400"
      : "text-foreground";

  const iconClass =
    accent === "blue"
      ? "text-blue-400"
      : accent === "red" && (value ?? 0) > 0
      ? "text-red-400"
      : "text-muted-foreground";

  return (
    <Card className="border-border bg-card">
      <CardHeader className="flex flex-row items-center justify-between pb-2">
        <CardTitle className="text-xs font-semibold text-foreground/70 uppercase tracking-wide">
          {title}
        </CardTitle>
        <Icon className={`h-4 w-4 ${iconClass}`} aria-hidden="true" />
      </CardHeader>
      <CardContent>
        {loading ? (
          <Skeleton className="h-8 w-24 bg-muted" />
        ) : (
          <p className={`text-3xl font-bold ${accentClass}`}>
            {value?.toLocaleString() ?? "—"}
          </p>
        )}
      </CardContent>
    </Card>
  );
}

// ─── CPU/RAM usage bar ────────────────────────────────────────────────────────

function UsageBar({ value }: { value: number | null | undefined }) {
  if (value === undefined || value === null) {
    return <span className="text-xs text-muted-foreground">—</span>;
  }
  const pct = Math.min(100, Math.max(0, value));
  const colorClass =
    pct > 90
      ? "bg-red-500"
      : pct > 70
      ? "bg-amber-500"
      : "bg-blue-500";
  return (
    <div className="flex items-center gap-2">
      <div className="h-1.5 w-16 rounded-full bg-muted overflow-hidden">
        <div className={`h-full rounded-full ${colorClass}`} style={{ width: `${pct}%` }} />
      </div>
      <span className="text-xs text-muted-foreground">{pct}%</span>
    </div>
  );
}

function getStreamBadgeClass(status: StreamStatus): string {
  if (status === "connected") return "bg-blue-500/20 text-blue-400 border-blue-500/30";
  if (status === "reconnecting") return "bg-amber-500/20 text-amber-400 border-amber-500/30";
  return "bg-red-500/20 text-red-400 border-red-500/30";
}

// ─── Main page ────────────────────────────────────────────────────────────────

export default function DashboardPage() {
  const { user } = useAuth();
  const isSuperAdmin = user?.role === "SuperAdmin";
  const isElevated = user?.role === "SuperAdmin" || user?.role === "CpAdmin";
  const [dashNetworkTenantId, setDashNetworkTenantId] = useState<number | undefined>(undefined);
  const { liveState, streamStatus, streamError } = useDashboardStream(
    Boolean(user),
    dashNetworkTenantId,
    !isElevated
  );
  const stats = liveState.stats;
  const devicesData = liveState.devicesData;
  const streamNetworkSummary =
    !isElevated || liveState.networkSummaryTenantId === dashNetworkTenantId
      ? liveState.networkSummary
      : undefined;
  const streamIsStale = streamStatus !== "connected";
  const statsLoading = stats === undefined && streamStatus !== "offline";
  const networkQueryEnabled = !isElevated || dashNetworkTenantId !== undefined;

  const { data: networkTenants } = useQuery({
    queryKey: ["tenants"],
    queryFn: getTenants,
    enabled: isElevated,
  });

  const {
    data: queriedNetworkSummary,
    isLoading: networkQueryLoading,
    isError: networkQueryError,
  } = useQuery({
    queryKey: ["network-summary", dashNetworkTenantId ?? "tenant-scope"],
    queryFn: () => getNetworkSummary(dashNetworkTenantId),
    enabled: networkQueryEnabled,
    refetchInterval: 30_000,
  });

  const {
    data: queriedDevicesData,
    isLoading: queriedDevicesLoading,
  } = useQuery({
    queryKey: ["devices-recent"],
    queryFn: () => getDevices(1, 10),
    enabled: Boolean(user),
    refetchInterval: 30_000,
  });

  const networkSummary = streamNetworkSummary ?? queriedNetworkSummary;
  const networkLoading = networkQueryEnabled && networkSummary === undefined && networkQueryLoading;
  const recentDevicesData = devicesData
    ? {
        ...devicesData,
        items: devicesData.items.map((device) =>
          mergeDeviceNetbirdFields(device, queriedDevicesData?.items)
        ),
      }
    : queriedDevicesData;
  const devicesLoading =
    recentDevicesData === undefined && streamStatus !== "offline" && queriedDevicesLoading;

  const offlineDevices =
    stats !== undefined
      ? (stats.totalDevices ?? 0) - (stats.onlineDevices ?? 0)
      : undefined;

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
        <Badge className={`${getStreamBadgeClass(streamStatus)} text-xs w-fit`}>
          Push stream: {streamStatus}
        </Badge>
        {liveState.lastEventAt && (
          <span className="text-xs text-muted-foreground">
            Last event {new Date(liveState.lastEventAt).toLocaleTimeString()}
          </span>
        )}
      </div>

      {streamIsStale && (
        <div className="rounded-lg border border-amber-500/30 bg-amber-500/10 px-4 py-3 text-sm text-amber-300">
          Live stream {streamStatus}. Dashboard may show stale data until push reconnects.
          {streamError ? ` ${streamError}` : ""}
        </div>
      )}

      {liveState.degradedReason && (
        <div className="rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-400">
          {liveState.degradedReason}
        </div>
      )}

      {/* Row 1 — KPI cards */}
      <div className="grid gap-3 grid-cols-2 sm:grid-cols-3 lg:grid-cols-5">
        <StatCard
          title="Total Devices"
          value={stats?.totalDevices}
          icon={Monitor}
          loading={statsLoading}
        />
        <StatCard
          title="Online"
          value={stats?.onlineDevices}
          icon={Wifi}
          loading={statsLoading}
          accent="blue"
        />
        <StatCard
          title="Offline"
          value={offlineDevices}
          icon={WifiOff}
          loading={statsLoading}
          accent="red"
        />
        <StatCard
          title="NetLock Events"
          value={stats?.totalEvents}
          icon={Zap}
          loading={statsLoading}
        />
        <StatCard
          title="Total Tenants"
          value={stats?.totalTenants}
          icon={Building2}
          loading={statsLoading}
        />
      </div>

      {/* Row 2 — Recent devices + right panels */}
      <div className="grid gap-4 lg:grid-cols-3">
        {/* Left: Recent devices table (2/3 width) */}
        <div className="lg:col-span-2">
          <Card className="border-border bg-card">
            <CardHeader className="flex flex-row items-center justify-between pb-3">
              <CardTitle className="text-sm font-semibold text-foreground">
                Recent Devices
              </CardTitle>
              <Link
                href="/devices"
                className="text-xs text-muted-foreground hover:text-foreground transition-colors"
              >
                View all &rarr;
              </Link>
            </CardHeader>
            <CardContent className="p-0">
              <div className="overflow-x-auto">
                <table className="w-full text-xs">
                  <thead>
                    <tr className="border-b border-border">
                      <th className="px-4 py-2 text-left font-medium text-muted-foreground">Device</th>
                      <th className="px-4 py-2 text-left font-medium text-muted-foreground">Platform</th>
                      <th className="px-4 py-2 text-left font-medium text-muted-foreground">OS</th>
                      <th className="px-4 py-2 text-left font-medium text-muted-foreground">CPU</th>
                      <th className="px-4 py-2 text-left font-medium text-muted-foreground">RAM</th>
                      <th className="px-4 py-2 text-left font-medium text-muted-foreground">Status</th>
                      <th className="px-4 py-2 text-left font-medium text-muted-foreground">NetBird</th>
                      <th className="px-4 py-2 text-left font-medium text-muted-foreground">Last Seen</th>
                    </tr>
                  </thead>
                  <tbody>
                    {devicesLoading
                      ? Array.from({ length: 5 }).map((_, i) => (
                          <tr key={i} className="border-b border-border">
                            {Array.from({ length: 8 }).map((_, j) => (
                              <td key={j} className="px-4 py-2">
                                <Skeleton className="h-3 w-full bg-muted" />
                              </td>
                            ))}
                          </tr>
                        ))
                      : recentDevicesData?.items.map((device: Device) => (
                          <tr
                            key={device.id}
                            className="border-b border-border last:border-0 hover:bg-muted/50"
                          >
                            <td className="px-4 py-2 font-medium text-foreground max-w-[120px] truncate">
                              {device.deviceName}
                            </td>
                            <td className="px-4 py-2 text-muted-foreground">{device.platform}</td>
                            <td className="px-4 py-2 text-muted-foreground max-w-[100px] truncate">
                              {device.operatingSystem ?? "—"}
                            </td>
                            <td className="px-4 py-2">
                              <UsageBar value={device.cpuUsage} />
                            </td>
                            <td className="px-4 py-2">
                              <UsageBar value={device.ramUsage} />
                            </td>
                            <td className="px-4 py-2">
                              <Badge
                                variant={device.isOnline ? "default" : "secondary"}
                                className={
                                  device.isOnline
                                    ? "bg-blue-500/20 text-blue-400 border-blue-500/30 text-xs"
                                    : "bg-red-500/20 text-red-400 border-red-500/30 text-xs"
                                }
                              >
                                {device.isOnline ? "Online" : "Offline"}
                              </Badge>
                            </td>
                            <td className="px-4 py-2">
                              <NetbirdStatus device={device} />
                            </td>
                            <td className="px-4 py-2 text-muted-foreground">
                              {device.lastAccess
                                ? new Date(device.lastAccess).toLocaleDateString()
                                : "—"}
                            </td>
                          </tr>
                        ))}
                    {!devicesLoading && recentDevicesData?.items.length === 0 && (
                      <tr>
                        <td colSpan={8} className="px-4 py-8 text-center text-muted-foreground">
                          No devices found.
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>
            </CardContent>
          </Card>
        </div>

        {/* Right: Compact dashboard actions */}
        <div className="space-y-4">
          <Card className="border-border bg-card self-start">
            <CardHeader className="py-3">
              <CardTitle className="text-sm font-semibold text-foreground">
                Quick Actions
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-1.5 pt-0">
              <Button
                asChild
                variant="outline"
                size="sm"
                className="h-8 w-full justify-start border-border text-foreground hover:bg-muted text-xs"
              >
                <Link href="/commands">
                  <Terminal className="mr-2 h-4 w-4" />
                  Execute Command
                </Link>
              </Button>
              <Button
                asChild
                variant="outline"
                size="sm"
                className="h-8 w-full justify-start border-border text-foreground hover:bg-muted text-xs"
              >
                <Link href="/audit">
                  <ClipboardList className="mr-2 h-4 w-4" />
                  View Audit Log
                </Link>
              </Button>
              <Button
                asChild
                variant="outline"
                size="sm"
                className="h-8 w-full justify-start border-border text-foreground hover:bg-muted text-xs"
              >
                <Link href="/devices">
                  <Monitor className="mr-2 h-4 w-4" />
                  View All Devices
                </Link>
              </Button>
              {isSuperAdmin && (
                <Button
                  asChild
                  variant="outline"
                  size="sm"
                  className="h-8 w-full justify-start border-border text-foreground hover:bg-muted text-xs"
                >
                  <Link href="/admin/system">
                    <Server className="mr-2 h-4 w-4" />
                    System Health
                  </Link>
                </Button>
              )}
            </CardContent>
          </Card>

          <Card className="border-border bg-card self-start" data-testid="dashboard-network-card">
            <CardHeader className="flex flex-row items-center justify-between py-3">
              <div className="flex items-center gap-2">
                <Network className="h-4 w-4 text-blue-400" aria-hidden="true" />
                <CardTitle className="text-sm font-medium text-foreground">Netbird Network</CardTitle>
              </div>
              <Badge className="bg-blue-500/20 text-blue-400 border-blue-500/30 text-xs">Live</Badge>
            </CardHeader>
            <CardContent className="pt-0">
              {isElevated && (
                <div className="mb-2">
                  <Select
                    value={dashNetworkTenantId !== undefined ? String(dashNetworkTenantId) : ""}
                    onValueChange={(v) => setDashNetworkTenantId(Number(v))}
                  >
                    <SelectTrigger className="h-7 text-xs w-full bg-background border-border">
                      <SelectValue placeholder="Select tenant..." />
                    </SelectTrigger>
                    <SelectContent>
                      {networkTenants?.map((t) => (
                        <SelectItem key={t.id} value={String(t.id)}>
                          {t.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              )}
              {isElevated && dashNetworkTenantId === undefined ? (
                <p className="text-xs text-muted-foreground">Select tenant to view network summary.</p>
              ) : networkLoading ? (
                <Skeleton className="h-8 w-full bg-muted" />
              ) : networkQueryError ? (
                <p className="text-xs text-amber-300">Network summary unavailable.</p>
              ) : networkSummary ? (
                <div className="grid grid-cols-3 gap-2 text-xs">
                  <div>
                    <p className="text-muted-foreground">Peers</p>
                    <p className="font-medium text-foreground">
                      {networkSummary.tenantConnectedPeers}/{networkSummary.tenantPeers}
                    </p>
                  </div>
                  <div>
                    <p className="text-muted-foreground">Keys</p>
                    <p className="font-medium text-foreground">{networkSummary.setupKeysActive}</p>
                  </div>
                  <div>
                    <p className="text-muted-foreground">Routes</p>
                    <p className="font-medium text-foreground">{networkSummary.routeCount}</p>
                  </div>
                </div>
              ) : (
                <p className="text-xs text-muted-foreground">No network data available.</p>
              )}
              <Button
                asChild
                variant="outline"
                size="sm"
                className="h-7 w-full border-border text-foreground hover:bg-muted text-xs mt-3"
              >
                <Link href="/network">Manage Network</Link>
              </Button>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}
