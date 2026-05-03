"use client";

import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  getNetworkPeers,
  getSetupKeys,
  createSetupKey,
  deleteSetupKey,
  getNetworkRoutes,
  getNetworkPolicies,
  getNetworkGroups,
  bindTenantGroup,
  deletePeer,
  getTenants,
} from "@/lib/api";
import type { TenantNetbirdGroupMode } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { Input } from "@/components/ui/input";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Key, Route, Shield, Trash2, Plus, Wifi, Copy, X } from "lucide-react";
import { useAuth } from "@/components/providers/auth-provider";

function StatusBadge({ connected }: { connected: boolean }) {
  return (
    <Badge
      className={
        connected
          ? "bg-blue-500/20 text-blue-400 border-blue-500/30 text-xs"
          : "bg-red-500/20 text-red-400 border-red-500/30 text-xs"
      }
    >
      {connected ? "Online" : "Offline"}
    </Badge>
  );
}

function TableSkeleton({ cols, rows = 5 }: { cols: number; rows?: number }) {
  return (
    <>
      {Array.from({ length: rows }).map((_, i) => (
        <tr key={i} className="border-b border-border">
          {Array.from({ length: cols }).map((_, j) => (
            <td key={j} className="px-4 py-2">
              <Skeleton className="h-3 w-full bg-muted" />
            </td>
          ))}
        </tr>
      ))}
    </>
  );
}

function PeersTab() {
  const { user } = useAuth();
  const isAdmin = user?.role === "SuperAdmin" || user?.role === "CpAdmin";
  const queryClient = useQueryClient();
  const [error, setError] = useState<string | null>(null);
  const [selectedTenantId, setSelectedTenantId] = useState<number | undefined>(undefined);

  const { data: tenants } = useQuery({
    queryKey: ["tenants"],
    queryFn: getTenants,
    enabled: isAdmin,
  });

  const { data: peers, isLoading } = useQuery({
    queryKey: ["network-peers", selectedTenantId],
    queryFn: () => getNetworkPeers(selectedTenantId),
    refetchInterval: 30_000,
    enabled: !isAdmin || selectedTenantId !== undefined,
  });

  const removePeer = useMutation({
    mutationFn: (id: string) => deletePeer(id, selectedTenantId),
    onSuccess: () => {
      setError(null);
      queryClient.invalidateQueries({ queryKey: ["network-peers", selectedTenantId] });
      queryClient.invalidateQueries({ queryKey: ["network-summary", selectedTenantId] });
    },
    onError: (err: Error) => {
      setError(err.message || "Failed to delete peer");
    },
  });

  return (
    <Card className="border-border bg-card">
      <CardHeader className="pb-3">
        <CardTitle className="text-sm font-semibold text-foreground flex items-center gap-2">
          <Wifi className="h-4 w-4 text-blue-400" />
          Mesh Peers
        </CardTitle>
      </CardHeader>
      <CardContent className="p-0">
        {isAdmin && (
          <div className="px-4 pt-3 pb-2">
            <Select
              value={selectedTenantId !== undefined ? String(selectedTenantId) : ""}
              onValueChange={(v) => setSelectedTenantId(Number(v))}
            >
              <SelectTrigger className="h-8 text-xs w-[200px] bg-background border-border">
                <SelectValue placeholder="Select tenant..." />
              </SelectTrigger>
              <SelectContent>
                {tenants?.map((t) => (
                  <SelectItem key={t.id} value={String(t.id)}>
                    {t.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        )}
        {error && (
          <div className="rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-400 mb-4 mx-4 mt-2">
            {error}
          </div>
        )}
        {isAdmin && selectedTenantId === undefined ? (
          <div className="px-4 py-8 text-center text-muted-foreground text-xs">
            Select a tenant to view peers.
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-xs">
              <thead>
                <tr className="border-b border-border">
                  <th className="px-4 py-2 text-left font-medium text-muted-foreground">Name</th>
                  <th className="px-4 py-2 text-left font-medium text-muted-foreground">IP</th>
                  <th className="px-4 py-2 text-left font-medium text-muted-foreground">OS</th>
                  <th className="px-4 py-2 text-left font-medium text-muted-foreground">Status</th>
                  <th className="px-4 py-2 text-left font-medium text-muted-foreground">Groups</th>
                  <th className="px-4 py-2 text-left font-medium text-muted-foreground">Last Seen</th>
                  {isAdmin && <th className="px-4 py-2 text-left font-medium text-muted-foreground" />}
                </tr>
              </thead>
              <tbody>
                {isLoading ? (
                  <TableSkeleton cols={isAdmin ? 7 : 6} />
                ) : peers && peers.length > 0 ? (
                  peers.map((peer) => (
                    <tr key={peer.id} className="border-b border-border last:border-0 hover:bg-muted/50">
                      <td className="px-4 py-2 font-medium text-foreground">{peer.name}</td>
                      <td className="px-4 py-2 text-muted-foreground font-mono">{peer.ip}</td>
                      <td className="px-4 py-2 text-muted-foreground max-w-[120px] truncate">{peer.os}</td>
                      <td className="px-4 py-2"><StatusBadge connected={peer.connected} /></td>
                      <td className="px-4 py-2">
                        <div className="flex gap-1 flex-wrap">
                          {peer.groups.slice(0, 3).map((g) => (
                            <Badge key={g.id} variant="secondary" className="text-[10px] bg-muted text-muted-foreground">
                              {g.name}
                            </Badge>
                          ))}
                          {peer.groups.length > 3 && (
                            <Badge variant="secondary" className="text-[10px] bg-muted text-muted-foreground">
                              +{peer.groups.length - 3}
                            </Badge>
                          )}
                        </div>
                      </td>
                      <td className="px-4 py-2 text-muted-foreground">
                        {new Date(peer.lastSeen).toLocaleString()}
                      </td>
                      {isAdmin && (
                        <td className="px-4 py-2">
                          <Button
                            size="sm"
                            variant="ghost"
                            className="h-6 w-6 p-0 text-destructive hover:text-destructive"
                            onClick={() => {
                              if (window.confirm("Are you sure you want to delete this peer? This will remove it from the mesh network.")) {
                                removePeer.mutate(peer.id);
                              }
                            }}
                            disabled={removePeer.isPending}
                          >
                            <Trash2 className="h-3 w-3" />
                          </Button>
                        </td>
                      )}
                    </tr>
                  ))
                ) : (
                  <tr>
                    <td colSpan={isAdmin ? 7 : 6} className="px-4 py-8 text-center text-muted-foreground">
                      No peers found. Create a setup key and enrol devices.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        )}
      </CardContent>
    </Card>
  );
}

function SetupKeysTab({ isAdmin }: { isAdmin: boolean }) {
  const queryClient = useQueryClient();
  const [showForm, setShowForm] = useState(false);
  const [name, setName] = useState("");
  const [type, setType] = useState<"one-off" | "reusable">("reusable");
  const [expiresInDays, setExpiresInDays] = useState(30);
  const [usageLimit, setUsageLimit] = useState(0);
  const [bindGroupId, setBindGroupId] = useState<string | undefined>(undefined);
  const [bindMode, setBindMode] = useState<TenantNetbirdGroupMode>("external");
  const [error, setError] = useState<string | null>(null);
  const [oneTimeKey, setOneTimeKey] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);
  const [selectedTenantId, setSelectedTenantId] = useState<number | undefined>(undefined);

  const { data: tenants } = useQuery({
    queryKey: ["tenants"],
    queryFn: getTenants,
    enabled: isAdmin,
  });

  const {
    data: groups,
    isError: groupsUnavailable,
  } = useQuery({
    queryKey: ["network-groups"],
    queryFn: getNetworkGroups,
    enabled: isAdmin,
    retry: false,
  });

  const { data: keys, isLoading } = useQuery({
    queryKey: ["setup-keys", selectedTenantId],
    queryFn: () => getSetupKeys(selectedTenantId),
    enabled: !isAdmin || selectedTenantId !== undefined,
  });

  const bindGroup = useMutation({
    mutationFn: () =>
      bindTenantGroup(
        {
          groupId: bindGroupId ?? "",
          mode: bindMode,
        },
        selectedTenantId
      ),
    onSuccess: () => {
      setError(null);
      queryClient.invalidateQueries({ queryKey: ["network-peers", selectedTenantId] });
      queryClient.invalidateQueries({ queryKey: ["setup-keys", selectedTenantId] });
      queryClient.invalidateQueries({ queryKey: ["network-summary", selectedTenantId] });
    },
    onError: (err: Error) => {
      setError(err.message || "Failed to bind NetBird group");
    },
  });

  const create = useMutation({
    mutationFn: (req: Parameters<typeof createSetupKey>[0]) => createSetupKey(req, selectedTenantId),
    onSuccess: (data) => {
      setError(null);
      setOneTimeKey(data.key);
      setCopied(false);
      queryClient.invalidateQueries({ queryKey: ["setup-keys", selectedTenantId] });
      queryClient.invalidateQueries({ queryKey: ["network-summary", selectedTenantId] });
      setShowForm(false);
      setName("");
    },
    onError: (err: Error) => {
      setError(err.message || "Failed to create setup key");
    },
  });

  const remove = useMutation({
    mutationFn: (id: string) => deleteSetupKey(id, selectedTenantId),
    onSuccess: () => {
      setError(null);
      queryClient.invalidateQueries({ queryKey: ["setup-keys", selectedTenantId] });
      queryClient.invalidateQueries({ queryKey: ["network-summary", selectedTenantId] });
    },
    onError: (err: Error) => {
      setError(err.message || "Failed to delete setup key");
    },
  });

  return (
    <Card className="border-border bg-card">
      <CardHeader className="flex flex-row items-center justify-between pb-3">
        <CardTitle className="text-sm font-semibold text-foreground flex items-center gap-2">
          <Key className="h-4 w-4 text-blue-400" />
          Setup Keys
        </CardTitle>
        {isAdmin && (
          <Button
            size="sm"
            variant="outline"
            className="h-7 text-xs border-border"
            onClick={() => setShowForm(!showForm)}
            disabled={selectedTenantId === undefined}
          >
            <Plus className="h-3 w-3 mr-1" />
            Create Key
          </Button>
        )}
      </CardHeader>
      <CardContent className="space-y-4 p-0">
        {isAdmin && (
          <div className="px-4 pt-3 pb-2">
            <Select
              value={selectedTenantId !== undefined ? String(selectedTenantId) : ""}
              onValueChange={(v) => {
                setSelectedTenantId(Number(v));
                setOneTimeKey(null);
                setCopied(false);
                setError(null);
                setShowForm(false);
                setName("");
                setBindGroupId(undefined);
                setBindMode("external");
              }}
            >
              <SelectTrigger className="h-8 text-xs w-[200px] bg-background border-border">
                <SelectValue placeholder="Select tenant..." />
              </SelectTrigger>
              <SelectContent>
                {tenants?.map((t) => (
                  <SelectItem key={t.id} value={String(t.id)}>
                    {t.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        )}
        {error && (
          <div className="rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-400 mx-4 mt-2">
            {error}
          </div>
        )}
        {isAdmin && selectedTenantId !== undefined && (
          <div className="mx-4 rounded-lg border border-border bg-background/40 px-3 py-3">
            <div className="grid gap-2 sm:grid-cols-[1fr_140px_auto] sm:items-end">
              <div>
                <label className="text-xs text-muted-foreground mb-1 block">Use existing NetBird group</label>
                <Select
                  value={bindGroupId ?? ""}
                  onValueChange={(v) => setBindGroupId(v)}
                  disabled={!groups || groups.length === 0}
                >
                  <SelectTrigger className="h-8 text-xs bg-background border-border">
                    <SelectValue placeholder="Select group..." />
                  </SelectTrigger>
                  <SelectContent>
                    {groups?.map((group) => (
                      <SelectItem key={group.id} value={group.id}>
                        {group.name}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div>
                <label className="text-xs text-muted-foreground mb-1 block">Mode</label>
                <Select
                  value={bindMode}
                  onValueChange={(v) => setBindMode(v as TenantNetbirdGroupMode)}
                >
                  <SelectTrigger className="h-8 text-xs bg-background border-border">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="external">external</SelectItem>
                    <SelectItem value="read_only">read_only</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <Button
                size="sm"
                variant="outline"
                className="h-8 text-xs border-border"
                disabled={!bindGroupId || bindGroup.isPending}
                onClick={() => bindGroup.mutate()}
              >
                {bindGroup.isPending ? "Binding..." : "Bind"}
              </Button>
            </div>
            {groupsUnavailable && (
              <p className="mt-2 text-[11px] text-muted-foreground">
                Existing group endpoint unavailable.
              </p>
            )}
          </div>
        )}
        {oneTimeKey && (
          <div className="mx-4 mt-2 rounded-lg border border-amber-500/40 bg-amber-500/10 px-4 py-3 space-y-2">
            <div className="flex items-center justify-between">
              <p className="text-xs font-medium text-amber-400">
                Setup key created — copy it now, it will not be shown again.
              </p>
              <Button
                size="sm"
                variant="ghost"
                className="h-5 w-5 p-0 text-muted-foreground hover:text-foreground"
                onClick={() => setOneTimeKey(null)}
                aria-label="Dismiss"
              >
                <X className="h-3 w-3" />
              </Button>
            </div>
            <div className="flex items-center gap-2">
              <code className="flex-1 rounded bg-background/60 px-2 py-1 text-xs font-mono text-foreground break-all">
                {oneTimeKey}
              </code>
              <Button
                size="sm"
                variant="outline"
                className="h-7 text-xs border-border shrink-0"
                onClick={() => {
                  navigator.clipboard.writeText(oneTimeKey);
                  setCopied(true);
                  setTimeout(() => setCopied(false), 2000);
                }}
              >
                <Copy className="h-3 w-3 mr-1" />
                {copied ? "Copied!" : "Copy"}
              </Button>
            </div>
          </div>
        )}
        {isAdmin && selectedTenantId === undefined ? (
          <div className="px-4 py-8 text-center text-muted-foreground text-xs">
            Select a tenant to view setup keys.
          </div>
        ) : (
          <>
            {isAdmin && showForm && (
              <div className="px-4 pt-2 pb-4 border-b border-border space-y-3">
                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="text-xs text-muted-foreground mb-1 block">Name</label>
                    <Input
                      value={name}
                      onChange={(e) => setName(e.target.value)}
                      placeholder="e.g. office-devices"
                      className="h-8 text-xs bg-background border-border"
                    />
                  </div>
                  <div>
                    <label className="text-xs text-muted-foreground mb-1 block">Type</label>
                    <Select value={type} onValueChange={(v) => setType(v as "one-off" | "reusable")}>
                      <SelectTrigger className="h-8 text-xs bg-background border-border">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="reusable">Reusable</SelectItem>
                        <SelectItem value="one-off">One-off</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                  <div>
                    <label className="text-xs text-muted-foreground mb-1 block">Expires (days)</label>
                    <Select value={String(expiresInDays)} onValueChange={(v) => setExpiresInDays(Number(v))}>
                      <SelectTrigger className="h-8 text-xs bg-background border-border">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="7">7 days</SelectItem>
                        <SelectItem value="30">30 days</SelectItem>
                        <SelectItem value="90">90 days</SelectItem>
                        <SelectItem value="365">365 days</SelectItem>
                      </SelectContent>
                    </Select>
                  </div>
                  <div>
                    <label className="text-xs text-muted-foreground mb-1 block">Usage Limit (0=unlimited)</label>
                    <Input
                      type="number"
                      value={usageLimit}
                      onChange={(e) => setUsageLimit(Number(e.target.value))}
                      className="h-8 text-xs bg-background border-border"
                      min={0}
                    />
                  </div>
                </div>
                <Button
                  size="sm"
                  className="text-xs h-7"
                  disabled={!name || create.isPending}
                  onClick={() =>
                    create.mutate({
                      name,
                      type,
                      expiresInDays,
                      usageLimit,
                      ephemeral: false,
                    })
                  }
                >
                  {create.isPending ? "Creating..." : "Create"}
                </Button>
              </div>
            )}
            <div className="overflow-x-auto">
              <table className="w-full text-xs">
                <thead>
                  <tr className="border-b border-border">
                    <th className="px-4 py-2 text-left font-medium text-muted-foreground">Name</th>
                    <th className="px-4 py-2 text-left font-medium text-muted-foreground">Key</th>
                    <th className="px-4 py-2 text-left font-medium text-muted-foreground">Type</th>
                    <th className="px-4 py-2 text-left font-medium text-muted-foreground">State</th>
                    <th className="px-4 py-2 text-left font-medium text-muted-foreground">Used</th>
                    <th className="px-4 py-2 text-left font-medium text-muted-foreground">Expires</th>
                    {isAdmin && <th className="px-4 py-2 text-left font-medium text-muted-foreground" />}
                  </tr>
                </thead>
                <tbody>
                  {isLoading ? (
                    <TableSkeleton cols={isAdmin ? 7 : 6} />
                  ) : keys && keys.length > 0 ? (
                    keys.map((k) => (
                      <tr key={k.id} className="border-b border-border last:border-0 hover:bg-muted/50">
                        <td className="px-4 py-2 font-medium text-foreground">{k.name}</td>
                        <td className="px-4 py-2 text-muted-foreground font-mono">{k.key}</td>
                        <td className="px-4 py-2 text-muted-foreground">{k.type}</td>
                        <td className="px-4 py-2">
                          <Badge
                            className={
                              k.valid
                                ? "bg-blue-500/20 text-blue-400 border-blue-500/30 text-[10px]"
                                : "bg-red-500/20 text-red-400 border-red-500/30 text-[10px]"
                            }
                          >
                            {k.state}
                          </Badge>
                        </td>
                        <td className="px-4 py-2 text-muted-foreground">
                          {k.usedTimes}{k.usageLimit > 0 ? `/${k.usageLimit}` : ""}
                        </td>
                        <td className="px-4 py-2 text-muted-foreground">
                          {new Date(k.expires).toLocaleDateString()}
                        </td>
                        {isAdmin && (
                          <td className="px-4 py-2">
                            <Button
                              size="sm"
                              variant="ghost"
                              className="h-6 w-6 p-0 text-destructive hover:text-destructive"
                              onClick={() => {
                                if (window.confirm("Are you sure you want to revoke this setup key?")) {
                                  remove.mutate(k.id);
                                }
                              }}
                              disabled={remove.isPending}
                            >
                              <Trash2 className="h-3 w-3" />
                            </Button>
                          </td>
                        )}
                      </tr>
                    ))
                  ) : (
                    <tr>
                      <td colSpan={isAdmin ? 7 : 6} className="px-4 py-8 text-center text-muted-foreground">
                        No setup keys. Create one to enrol devices.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </>
        )}
      </CardContent>
    </Card>
  );
}

function RoutesTab() {
  const { data: routes, isLoading } = useQuery({
    queryKey: ["network-routes"],
    queryFn: getNetworkRoutes,
  });

  return (
    <Card className="border-border bg-card">
      <CardHeader className="pb-3">
        <CardTitle className="text-sm font-semibold text-foreground flex items-center gap-2">
          <Route className="h-4 w-4 text-blue-400" />
          Routes
        </CardTitle>
      </CardHeader>
      <CardContent className="p-0">
        <div className="overflow-x-auto">
          <table className="w-full text-xs">
            <thead>
              <tr className="border-b border-border">
                <th className="px-4 py-2 text-left font-medium text-muted-foreground">Description</th>
                <th className="px-4 py-2 text-left font-medium text-muted-foreground">Network</th>
                <th className="px-4 py-2 text-left font-medium text-muted-foreground">Type</th>
                <th className="px-4 py-2 text-left font-medium text-muted-foreground">Metric</th>
                <th className="px-4 py-2 text-left font-medium text-muted-foreground">Masquerade</th>
                <th className="px-4 py-2 text-left font-medium text-muted-foreground">Enabled</th>
              </tr>
            </thead>
            <tbody>
              {isLoading ? (
                <TableSkeleton cols={6} />
              ) : routes && routes.length > 0 ? (
                routes.map((r) => (
                  <tr key={r.id} className="border-b border-border last:border-0 hover:bg-muted/50">
                    <td className="px-4 py-2 font-medium text-foreground">{r.description}</td>
                    <td className="px-4 py-2 text-muted-foreground font-mono">{r.network}</td>
                    <td className="px-4 py-2 text-muted-foreground">{r.networkType}</td>
                    <td className="px-4 py-2 text-muted-foreground">{r.metric}</td>
                    <td className="px-4 py-2 text-muted-foreground">{r.masquerade ? "Yes" : "No"}</td>
                    <td className="px-4 py-2">
                      <Badge className={r.enabled ? "bg-blue-500/20 text-blue-400 border-blue-500/30 text-[10px]" : "bg-muted text-muted-foreground text-[10px]"}>
                        {r.enabled ? "Active" : "Disabled"}
                      </Badge>
                    </td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td colSpan={6} className="px-4 py-8 text-center text-muted-foreground">
                    No routes configured.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </CardContent>
    </Card>
  );
}

function PoliciesTab() {
  const { data: policies, isLoading } = useQuery({
    queryKey: ["network-policies"],
    queryFn: getNetworkPolicies,
  });

  return (
    <Card className="border-border bg-card">
      <CardHeader className="pb-3">
        <CardTitle className="text-sm font-semibold text-foreground flex items-center gap-2">
          <Shield className="h-4 w-4 text-blue-400" />
          Policies
        </CardTitle>
      </CardHeader>
      <CardContent className="p-0">
        <div className="overflow-x-auto">
          <table className="w-full text-xs">
            <thead>
              <tr className="border-b border-border">
                <th className="px-4 py-2 text-left font-medium text-muted-foreground">Name</th>
                <th className="px-4 py-2 text-left font-medium text-muted-foreground">Description</th>
                <th className="px-4 py-2 text-left font-medium text-muted-foreground">Rules</th>
                <th className="px-4 py-2 text-left font-medium text-muted-foreground">Enabled</th>
              </tr>
            </thead>
            <tbody>
              {isLoading ? (
                <TableSkeleton cols={4} />
              ) : policies && policies.length > 0 ? (
                policies.map((p) => (
                  <tr key={p.id} className="border-b border-border last:border-0 hover:bg-muted/50">
                    <td className="px-4 py-2 font-medium text-foreground">{p.name}</td>
                    <td className="px-4 py-2 text-muted-foreground max-w-[200px] truncate">{p.description}</td>
                    <td className="px-4 py-2 text-muted-foreground">{p.rules.length}</td>
                    <td className="px-4 py-2">
                      <Badge className={p.enabled ? "bg-blue-500/20 text-blue-400 border-blue-500/30 text-[10px]" : "bg-muted text-muted-foreground text-[10px]"}>
                        {p.enabled ? "Active" : "Disabled"}
                      </Badge>
                    </td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td colSpan={4} className="px-4 py-8 text-center text-muted-foreground">
                    No policies configured.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </CardContent>
    </Card>
  );
}

export default function NetworkPage() {
  const { user } = useAuth();
  const isAdmin = user?.role === "SuperAdmin" || user?.role === "CpAdmin";

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-lg font-semibold text-foreground">Network</h1>
        <p className="text-xs text-muted-foreground">Manage Netbird mesh VPN peers, keys, routes, and policies.</p>
      </div>

      <Tabs defaultValue="peers" className="space-y-4">
        <TabsList className="bg-muted border border-border">
          <TabsTrigger value="peers" className="text-xs data-[state=active]:bg-background">
            <Wifi className="h-3 w-3 mr-1" /> Peers
          </TabsTrigger>
          <TabsTrigger value="keys" className="text-xs data-[state=active]:bg-background">
            <Key className="h-3 w-3 mr-1" /> Setup Keys
          </TabsTrigger>
          {isAdmin && (
            <TabsTrigger value="routes" className="text-xs data-[state=active]:bg-background">
              <Route className="h-3 w-3 mr-1" /> Routes
            </TabsTrigger>
          )}
          {isAdmin && (
            <TabsTrigger value="policies" className="text-xs data-[state=active]:bg-background">
              <Shield className="h-3 w-3 mr-1" /> Policies
            </TabsTrigger>
          )}
        </TabsList>

        <TabsContent value="peers"><PeersTab /></TabsContent>
        <TabsContent value="keys"><SetupKeysTab isAdmin={isAdmin} /></TabsContent>
        {isAdmin && <TabsContent value="routes"><RoutesTab /></TabsContent>}
        {isAdmin && <TabsContent value="policies"><PoliciesTab /></TabsContent>}
      </Tabs>
    </div>
  );
}
