"use client";

import { useQuery } from "@tanstack/react-query";
import { getSystemHealth } from "@/lib/api";
import { useAuth } from "@/components/providers/auth-provider";
import { useRouter } from "next/navigation";
import { useEffect } from "react";
import type { ComponentHealth } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Database,
  Radio,
  Network,
  Server,
  RefreshCw,
  CheckCircle2,
  AlertTriangle,
  XCircle,
} from "lucide-react";
import { Button } from "@/components/ui/button";

// ─── Status helpers ───────────────────────────────────────────────────────────

function statusIcon(status: string) {
  if (status === "healthy")
    return <CheckCircle2 className="h-4 w-4 text-blue-400" />;
  if (status === "degraded")
    return <AlertTriangle className="h-4 w-4 text-amber-400" />;
  return <XCircle className="h-4 w-4 text-red-400" />;
}

function statusBadge(status: string) {
  const cls =
    status === "healthy"
      ? "bg-blue-500/20 text-blue-400 border-blue-500/30"
      : status === "degraded"
      ? "bg-amber-500/20 text-amber-400 border-amber-500/30"
      : "bg-red-500/20 text-red-400 border-red-500/30";
  const label = status.charAt(0).toUpperCase() + status.slice(1);
  return <Badge className={`text-xs ${cls}`}>{label}</Badge>;
}

// ─── Component row ────────────────────────────────────────────────────────────

function ComponentRow({
  icon: Icon,
  label,
  data,
  loading,
}: {
  icon: React.ElementType;
  label: string;
  data: ComponentHealth | undefined;
  loading: boolean;
}) {
  return (
    <div className="flex items-start gap-4 py-4 border-b border-border last:border-0">
      <div className="flex items-center gap-2 w-44 shrink-0">
        <Icon className="h-4 w-4 text-muted-foreground shrink-0" />
        <span className="text-sm font-medium text-foreground">{label}</span>
      </div>

      {loading || !data ? (
        <Skeleton className="h-5 w-48 bg-muted" />
      ) : (
        <div className="flex flex-1 items-center gap-3 flex-wrap">
          <div className="flex items-center gap-1.5">
            {statusIcon(data.status)}
            {statusBadge(data.status)}
          </div>
          {data.latencyMs !== undefined && (
            <span className="text-xs text-muted-foreground font-mono">
              {data.latencyMs}ms
            </span>
          )}
          {data.detail && (
            <span className="text-xs text-muted-foreground font-mono break-all">
              {data.detail}
            </span>
          )}
        </div>
      )}
    </div>
  );
}

// ─── Info row ─────────────────────────────────────────────────────────────────

function InfoRow({ label, value }: { label: string; value: string | number | undefined }) {
  return (
    <div className="flex items-center py-3 border-b border-border last:border-0">
      <span className="w-44 shrink-0 text-xs text-muted-foreground">{label}</span>
      <span className="text-sm font-mono text-foreground">{value ?? "—"}</span>
    </div>
  );
}

// ─── Page ─────────────────────────────────────────────────────────────────────

export default function SystemHealthPage() {
  const { user } = useAuth();
  const router = useRouter();

  // Guard: only SuperAdmin may view this page
  useEffect(() => {
    if (user && user.role !== "SuperAdmin") router.replace("/dashboard");
  }, [user, router]);

  const { data, isLoading, isError, refetch, isFetching, dataUpdatedAt } = useQuery({
    queryKey: ["system-health"],
    queryFn: getSystemHealth,
    refetchInterval: 30_000,
  });

  if (user && user.role !== "SuperAdmin") return null;

  const lastChecked = dataUpdatedAt
    ? new Date(dataUpdatedAt).toLocaleTimeString()
    : null;

  return (
    <div className="space-y-6 max-w-3xl">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-base font-semibold text-foreground">System Health</h1>
          <p className="text-xs text-muted-foreground mt-0.5">
            Live diagnostics — SuperAdmin only
            {lastChecked && <span className="ml-2">· Last checked {lastChecked}</span>}
          </p>
        </div>
        <Button
          variant="outline"
          size="sm"
          className="border-border text-foreground hover:bg-muted"
          onClick={() => void refetch()}
          disabled={isFetching}
        >
          <RefreshCw className={`h-3.5 w-3.5 mr-2 ${isFetching ? "animate-spin" : ""}`} />
          Refresh
        </Button>
      </div>

      {isError && (
        <div className="rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-400">
          Failed to load system health. Make sure you are SuperAdmin and the API is reachable.
        </div>
      )}

      {/* Overall status */}
      {(isLoading || data) && (
        <Card className="border-border bg-card">
          <CardHeader className="pb-2">
            <div className="flex items-center gap-2">
              <Server className="h-4 w-4 text-muted-foreground" />
              <CardTitle className="text-sm font-medium text-foreground">Overall Status</CardTitle>
              {!isLoading && data && statusBadge(data.status)}
            </div>
          </CardHeader>
          <CardContent>
            {isLoading ? (
              <Skeleton className="h-5 w-32 bg-muted" />
            ) : (
              <p className="text-xs text-muted-foreground">
                {data?.status === "healthy"
                  ? "All core systems operational."
                  : data?.status === "degraded"
                  ? "Core systems operational — one or more optional components are down."
                  : "One or more critical systems are unreachable."}
              </p>
            )}
          </CardContent>
        </Card>
      )}

      {/* Components */}
      <Card className="border-border bg-card">
        <CardHeader className="pb-0">
          <CardTitle className="text-sm font-medium text-foreground">Components</CardTitle>
        </CardHeader>
        <CardContent className="pt-0">
          <ComponentRow
            icon={Database}
            label="MySQL Database"
            data={data?.mysql}
            loading={isLoading}
          />
          <ComponentRow
            icon={Radio}
            label="NetLock SignalR Hub"
            data={data?.signalR}
            loading={isLoading}
          />
          <ComponentRow
            icon={Network}
            label="NetBird Mesh"
            data={data?.netBird}
            loading={isLoading}
          />
        </CardContent>
      </Card>

      {/* API info */}
      <Card className="border-border bg-card">
        <CardHeader className="pb-0">
          <CardTitle className="text-sm font-medium text-foreground">API Process</CardTitle>
        </CardHeader>
        <CardContent className="pt-0">
          <InfoRow label="Version" value={data?.api.version} />
          <InfoRow label="Environment" value={data?.api.environment} />
          <InfoRow label="Uptime" value={data?.api.uptime} />
          <InfoRow label="Connected Devices" value={data?.api.connectedDevices} />
          <InfoRow
            label="Checked At"
            value={data?.checkedAt ? new Date(data.checkedAt).toLocaleString() : undefined}
          />
        </CardContent>
      </Card>
    </div>
  );
}
