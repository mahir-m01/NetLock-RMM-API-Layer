"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { getDashboard, getDevices } from "@/lib/api";
import type { Device } from "@/lib/types";
import {
  Monitor,
  Wifi,
  WifiOff,
  Building2,
  Zap,
  AlertTriangle,
  Terminal,
  ClipboardList,
  Users,
  Shield,
  Network,
  Server,
} from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
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
        <CardTitle className="text-xs font-medium text-muted-foreground uppercase tracking-wide">
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

// ─── Placeholder card ─────────────────────────────────────────────────────────

interface PlaceholderCardProps {
  icon: React.ElementType;
  title: string;
  description: string;
  phase: string;
}

function PlaceholderCard({ icon: Icon, title, description, phase }: PlaceholderCardProps) {
  return (
    <Card className="border-border bg-card opacity-60">
      <CardHeader className="flex flex-row items-center justify-between pb-2">
        <div className="flex items-center gap-2">
          <Icon className="h-4 w-4 text-muted-foreground" aria-hidden="true" />
          <CardTitle className="text-sm font-medium text-foreground">{title}</CardTitle>
        </div>
        <Badge className="bg-muted text-muted-foreground border-border text-xs">
          {phase}
        </Badge>
      </CardHeader>
      <CardContent>
        <p className="text-xs text-muted-foreground">{description}</p>
      </CardContent>
    </Card>
  );
}

// ─── Main page ────────────────────────────────────────────────────────────────

export default function DashboardPage() {
  const { user } = useAuth();
  const isSuperAdmin = user?.role === "SuperAdmin";
  const {
    data: stats,
    isLoading: statsLoading,
    isError: statsError,
  } = useQuery({
    queryKey: ["dashboard"],
    queryFn: getDashboard,
    refetchInterval: 30_000,
  });

  const {
    data: devicesData,
    isLoading: devicesLoading,
  } = useQuery({
    queryKey: ["devices-recent"],
    queryFn: () => getDevices(1, 10),
    refetchInterval: 30_000,
  });

  const offlineDevices =
    stats !== undefined
      ? (stats.totalDevices ?? 0) - (stats.onlineDevices ?? 0)
      : undefined;

  return (
    <div className="space-y-6">
      {statsError && (
        <div className="rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-400">
          Failed to load dashboard data. Check your API key and connection.
        </div>
      )}

      {/* Row 1 — KPI cards */}
      <div className="grid gap-3 grid-cols-2 sm:grid-cols-3 lg:grid-cols-6">
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
          title="Critical Alerts"
          value={stats?.criticalAlerts}
          icon={AlertTriangle}
          loading={statsLoading}
          accent="red"
        />
        <StatCard
          title="Total Events"
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
                      <th className="px-4 py-2 text-left font-medium text-muted-foreground">Last Seen</th>
                    </tr>
                  </thead>
                  <tbody>
                    {devicesLoading
                      ? Array.from({ length: 5 }).map((_, i) => (
                          <tr key={i} className="border-b border-border">
                            {Array.from({ length: 7 }).map((_, j) => (
                              <td key={j} className="px-4 py-2">
                                <Skeleton className="h-3 w-full bg-muted" />
                              </td>
                            ))}
                          </tr>
                        ))
                      : devicesData?.items.map((device: Device) => (
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
                            <td className="px-4 py-2 text-muted-foreground">
                              {device.lastAccess
                                ? new Date(device.lastAccess).toLocaleDateString()
                                : "—"}
                            </td>
                          </tr>
                        ))}
                    {!devicesLoading && devicesData?.items.length === 0 && (
                      <tr>
                        <td colSpan={7} className="px-4 py-8 text-center text-muted-foreground">
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

        {/* Right: Quick Actions (1/3 width) */}
        <Card className="border-border bg-card">
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-semibold text-foreground">
              Quick Actions
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-2">
            <Button
              asChild
              variant="outline"
              className="w-full justify-start border-border text-foreground hover:bg-muted text-sm"
            >
              <Link href="/commands">
                <Terminal className="mr-2 h-4 w-4" />
                Execute Command
              </Link>
            </Button>
            <Button
              asChild
              variant="outline"
              className="w-full justify-start border-border text-foreground hover:bg-muted text-sm"
            >
              <Link href="/audit">
                <ClipboardList className="mr-2 h-4 w-4" />
                View Audit Log
              </Link>
            </Button>
            <Button
              asChild
              variant="outline"
              className="w-full justify-start border-border text-foreground hover:bg-muted text-sm"
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
                className="w-full justify-start border-border text-foreground hover:bg-muted text-sm"
              >
                <Link href="/admin/system">
                  <Server className="mr-2 h-4 w-4" />
                  System Health
                </Link>
              </Button>
            )}
          </CardContent>
        </Card>
      </div>

      {/* Row 3 — Placeholder future panels */}
      <div className="grid gap-4 sm:grid-cols-3">
        <Card className="border-border bg-card">
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <div className="flex items-center gap-2">
              <Users className="h-4 w-4 text-blue-400" aria-hidden="true" />
              <CardTitle className="text-sm font-medium text-foreground">User Accounts</CardTitle>
            </div>
            <Badge className="bg-blue-500/20 text-blue-400 border-blue-500/30 text-xs">Live</Badge>
          </CardHeader>
          <CardContent>
            <p className="text-xs text-muted-foreground mb-3">JWT auth, 4-role RBAC, user management.</p>
            <Button asChild size="sm" variant="outline" className="w-full border-border text-foreground hover:bg-muted text-xs h-7">
              <Link href="/admin/users">Manage Users</Link>
            </Button>
          </CardContent>
        </Card>
        <PlaceholderCard
          icon={Shield}
          title="Wazuh Security"
          description="Security alerts and compliance — Phase 2"
          phase="Phase 2"
        />
        <PlaceholderCard
          icon={Network}
          title="Netbird Network"
          description="Mesh network topology — Phase 2"
          phase="Phase 2"
        />
      </div>
    </div>
  );
}
