"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { executeBatchCommand, getDevices } from "@/lib/api";
import type { BatchCommandResult, Device, DeviceFilters, Shell } from "@/lib/types";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { cn } from "@/lib/utils";
import { Check, Search, Terminal, X } from "lucide-react";

const MAX_BATCH_TARGETS = 25;
const DEVICE_PAGE_SIZE = 25;
const PLATFORMS = ["All", "Linux", "Windows", "macOS"];

function getResultStatus(result: BatchCommandResult): "ok" | "failed" {
  if (result.status !== "SUCCESS") return "failed";
  return "ok";
}

function getNetbirdIp(device: Device): string | null {
  return device.netbirdIp ?? device.netBirdIp ?? null;
}

function DeviceStatusBadge({ isOnline }: { isOnline: boolean }) {
  return (
    <Badge
      variant={isOnline ? "default" : "secondary"}
      className={
        isOnline
          ? "border-blue-500/30 bg-blue-500/20 text-blue-400"
          : "border-red-500/30 bg-red-500/20 text-red-400"
      }
    >
      {isOnline ? "Online" : "Offline"}
    </Badge>
  );
}

export default function CommandsPage() {
  const [command, setCommand] = useState("");
  const [shell, setShell] = useState<Shell>("bash");
  const [timeoutSeconds, setTimeoutSeconds] = useState(30);
  const [search, setSearch] = useState("");
  const [platform, setPlatform] = useState("All");
  const [selectedDevices, setSelectedDevices] = useState<Record<number, Device>>({});
  const [inspectedDeviceId, setInspectedDeviceId] = useState<string>("");
  const [elapsed, setElapsed] = useState(0);
  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const filters: DeviceFilters = {
    platform: platform !== "All" ? platform : undefined,
    search: search || undefined,
  };
  const deviceQuery = useQuery({
    queryKey: ["command-devices", filters],
    queryFn: () => getDevices(1, DEVICE_PAGE_SIZE, filters),
  });

  const visibleDevices = deviceQuery.data?.items ?? [];
  const selectedDeviceList = useMemo(
    () => Object.values(selectedDevices).sort((a, b) => a.id - b.id),
    [selectedDevices]
  );
  const deviceIds = selectedDeviceList.map((device) => device.id);
  const selectedCount = selectedDeviceList.length;
  const canSelectMore = selectedCount < MAX_BATCH_TARGETS;

  useEffect(() => {
    return () => {
      if (timerRef.current) clearInterval(timerRef.current);
    };
  }, []);

  const mutation = useMutation({
    mutationFn: () =>
      executeBatchCommand({
        deviceIds,
        command,
        shell,
        timeoutSeconds,
      }),
    onMutate: () => {
      setElapsed(0);
      setInspectedDeviceId("");
      timerRef.current = setInterval(() => setElapsed((s) => s + 1), 1000);
    },
    onSettled: () => {
      if (timerRef.current) {
        clearInterval(timerRef.current);
        timerRef.current = null;
      }
    },
  });

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (deviceIds.length === 0 || !command.trim()) return;
    mutation.mutate();
  }

  function handleToggleDevice(device: Device) {
    setSelectedDevices((current) => {
      if (current[device.id]) {
        const next = { ...current };
        delete next[device.id];
        return next;
      }
      if (Object.keys(current).length >= MAX_BATCH_TARGETS) return current;
      return { ...current, [device.id]: device };
    });
  }

  function handleSelectVisible() {
    setSelectedDevices((current) => {
      const next = { ...current };
      for (const device of visibleDevices) {
        if (Object.keys(next).length >= MAX_BATCH_TARGETS) break;
        next[device.id] = device;
      }
      return next;
    });
  }

  const apiError = mutation.error as Error | null;
  const apiErrorStatus =
    apiError && "status" in apiError ? (apiError as { status: number }).status : null;
  const isBadGateway = apiErrorStatus === 503 || apiErrorStatus === 504;
  const results = mutation.data?.results ?? [];
  const inspectedResult =
    results.find((result) => String(result.deviceId) === inspectedDeviceId) ?? results[0];
  const cannotSubmit =
    mutation.isPending ||
    !command.trim() ||
    deviceIds.length === 0;

  return (
    <div className="mx-auto max-w-6xl space-y-4">
      <Card className="border-border bg-card">
        <CardHeader className="pb-3">
          <CardTitle className="flex items-center gap-2 text-base text-foreground">
            <Terminal className="h-4 w-4 text-muted-foreground" />
            Batch Command Dispatch
          </CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} className="space-y-4">
            <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_320px]">
              <div className="space-y-2">
                <label
                  className="text-xs font-medium text-muted-foreground"
                  htmlFor="device-search-input"
                >
                  Target devices
                </label>
                <div className="flex flex-col gap-2 sm:flex-row">
                  <div className="relative min-w-0 flex-1">
                    <Search className="pointer-events-none absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" />
                    <Input
                      id="device-search-input"
                      value={search}
                      onChange={(e) => setSearch(e.target.value)}
                      placeholder="Search devices"
                      className="border-border bg-muted pl-9 text-foreground placeholder:text-muted-foreground"
                    />
                  </div>
                  <Select value={platform} onValueChange={setPlatform}>
                    <SelectTrigger className="w-full border-border bg-muted text-foreground sm:w-36">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent className="border-border bg-card text-foreground">
                      {PLATFORMS.map((item) => (
                        <SelectItem key={item} value={item} className="focus:bg-muted">
                          {item}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
                  <p className="text-xs text-muted-foreground">
                    {selectedCount}/{MAX_BATCH_TARGETS} selected
                  </p>
                  <div className="flex gap-2">
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      onClick={handleSelectVisible}
                      disabled={visibleDevices.length === 0 || !canSelectMore}
                      className="h-8 border-border bg-card text-xs"
                    >
                      Select visible
                    </Button>
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      onClick={() => setSelectedDevices({})}
                      disabled={selectedCount === 0}
                      className="h-8 text-xs text-muted-foreground hover:text-foreground"
                    >
                      Clear
                    </Button>
                  </div>
                </div>
                {selectedCount >= MAX_BATCH_TARGETS && (
                  <p className="text-xs text-amber-400">
                    Maximum {MAX_BATCH_TARGETS} devices per batch.
                  </p>
                )}
              </div>

              <div className="space-y-2">
                <label className="text-xs font-medium text-muted-foreground">
                  Shell
                </label>
                <Select value={shell} onValueChange={(v) => setShell(v as Shell)}>
                  <SelectTrigger className="border-border bg-muted text-foreground">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent className="border-border bg-card text-foreground">
                    <SelectItem value="bash" className="focus:bg-muted">
                      bash
                    </SelectItem>
                    <SelectItem value="powershell" className="focus:bg-muted">
                      powershell
                    </SelectItem>
                    <SelectItem value="cmd" className="focus:bg-muted">
                      cmd
                    </SelectItem>
                  </SelectContent>
                </Select>
              </div>
            </div>

            <div className="overflow-x-auto rounded-lg border border-border">
              <Table>
                <TableHeader>
                  <TableRow className="border-border hover:bg-transparent">
                    <TableHead className="w-12 text-muted-foreground">Pick</TableHead>
                    <TableHead className="min-w-44 text-muted-foreground">Device</TableHead>
                    <TableHead className="text-muted-foreground">Platform</TableHead>
                    <TableHead className="text-muted-foreground">Status</TableHead>
                    <TableHead className="min-w-32 text-muted-foreground">NetBird IP</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {deviceQuery.isLoading ? (
                    Array.from({ length: 5 }).map((_, index) => (
                      <TableRow key={index} className="border-border">
                        {Array.from({ length: 5 }).map((__, cellIndex) => (
                          <TableCell key={cellIndex}>
                            <Skeleton className="h-4 w-full bg-muted" />
                          </TableCell>
                        ))}
                      </TableRow>
                    ))
                  ) : deviceQuery.isError ? (
                    <TableRow className="border-border">
                      <TableCell colSpan={5} className="py-8 text-center text-red-400">
                        Failed to load devices.
                      </TableCell>
                    </TableRow>
                  ) : visibleDevices.length === 0 ? (
                    <TableRow className="border-border">
                      <TableCell colSpan={5} className="py-8 text-center text-muted-foreground">
                        No devices found.
                      </TableCell>
                    </TableRow>
                  ) : (
                    visibleDevices.map((device) => {
                      const isSelected = Boolean(selectedDevices[device.id]);
                      const isDisabled = !isSelected && !canSelectMore;
                      return (
                        <TableRow key={device.id} className="border-border hover:bg-muted">
                          <TableCell>
                            <Button
                              type="button"
                              variant={isSelected ? "default" : "outline"}
                              size="icon"
                              disabled={isDisabled}
                              onClick={() => handleToggleDevice(device)}
                              aria-label={`${isSelected ? "Remove" : "Select"} ${device.deviceName}`}
                              className="h-7 w-7"
                            >
                              {isSelected && <Check className="h-3.5 w-3.5" />}
                            </Button>
                          </TableCell>
                          <TableCell className="font-medium text-foreground">
                            <div className="min-w-0">
                              <p className="truncate">{device.deviceName}</p>
                              <p className="font-mono text-xs text-muted-foreground">
                                ID {device.id}
                              </p>
                            </div>
                          </TableCell>
                          <TableCell className="text-muted-foreground">
                            {device.platform || "-"}
                          </TableCell>
                          <TableCell>
                            <DeviceStatusBadge isOnline={device.isOnline} />
                          </TableCell>
                          <TableCell className="font-mono text-xs text-muted-foreground">
                            {getNetbirdIp(device) ?? "-"}
                          </TableCell>
                        </TableRow>
                      );
                    })
                  )}
                </TableBody>
              </Table>
            </div>

            <div className="space-y-2">
              <div className="flex items-center justify-between gap-2">
                <p className="text-xs font-medium text-muted-foreground">
                  Selected targets
                </p>
                <span className="text-xs text-muted-foreground">
                  {selectedCount} device{selectedCount === 1 ? "" : "s"}
                </span>
              </div>
              {selectedDeviceList.length === 0 ? (
                <div className="rounded-lg border border-dashed border-border px-3 py-4 text-sm text-muted-foreground">
                  Pick devices above before running batch command.
                </div>
              ) : (
                <div className="flex max-h-36 flex-wrap gap-2 overflow-y-auto rounded-lg border border-border p-2">
                  {selectedDeviceList.map((device) => (
                    <div
                      key={device.id}
                      className="flex max-w-full items-center gap-2 rounded-md border border-border bg-muted px-2 py-1 text-xs text-foreground"
                    >
                      <span className="max-w-40 truncate font-medium">{device.deviceName}</span>
                      <span className="text-muted-foreground">{device.platform || "-"}</span>
                      <span
                        className={cn(
                          "h-2 w-2 rounded-full",
                          device.isOnline ? "bg-blue-400" : "bg-red-400"
                        )}
                        aria-label={device.isOnline ? "Online" : "Offline"}
                      />
                      {getNetbirdIp(device) && (
                        <span className="font-mono text-muted-foreground">
                          {getNetbirdIp(device)}
                        </span>
                      )}
                      <Button
                        type="button"
                        variant="ghost"
                        size="icon"
                        onClick={() => handleToggleDevice(device)}
                        aria-label={`Remove ${device.deviceName}`}
                        className="h-5 w-5 text-muted-foreground hover:text-foreground"
                      >
                        <X className="h-3 w-3" />
                      </Button>
                    </div>
                  ))}
                </div>
              )}
            </div>

            <div className="space-y-2">
              <label
                className="text-xs font-medium text-muted-foreground"
                htmlFor="command-textarea"
              >
                Command
              </label>
              <textarea
                id="command-textarea"
                value={command}
                onChange={(e) => setCommand(e.target.value)}
                rows={4}
                placeholder="e.g. uptime"
                className="w-full resize-y rounded-md border border-border bg-muted px-3 py-2 font-mono text-sm text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring"
                required
              />
            </div>

            <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
              <div className="flex-1 space-y-2">
                <label
                  className="text-xs font-medium text-muted-foreground"
                  htmlFor="timeout-range"
                >
                  Timeout: {timeoutSeconds}s
                </label>
                <div className="flex h-9 items-center gap-2">
                  <span className="text-xs text-muted-foreground">5s</span>
                  <input
                    id="timeout-range"
                    type="range"
                    min={5}
                    max={120}
                    value={timeoutSeconds}
                    onChange={(e) => setTimeoutSeconds(Number(e.target.value))}
                    className="flex-1"
                  />
                  <span className="text-xs text-muted-foreground">120s</span>
                </div>
              </div>

              <Button
                type="submit"
                disabled={cannotSubmit}
                className="h-9 min-w-40"
              >
                {mutation.isPending ? (
                  <span className="flex items-center gap-2">
                    <span className="h-4 w-4 animate-spin rounded-full border-2 border-background border-t-transparent" />
                    Running {elapsed}s
                  </span>
                ) : (
                  "Run Batch"
                )}
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>

      {(mutation.isSuccess || mutation.isError) && (
        <Card className="border-border bg-card">
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">
              Batch Results
            </CardTitle>
            {mutation.isSuccess && (
              <span className="text-xs text-muted-foreground">
                {results.length} device{results.length === 1 ? "" : "s"}
              </span>
            )}
          </CardHeader>
          <CardContent>
            {isBadGateway ? (
              <div className="rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-4 text-sm text-red-400">
                <p className="mb-1 font-semibold">
                  {apiErrorStatus === 503
                    ? "Service Unavailable (503)"
                    : "Gateway Timeout (504)"}
                </p>
                <p>One or more agents did not respond before timeout.</p>
              </div>
            ) : mutation.isError ? (
              <div className="rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-4 text-sm text-red-400">
                <p className="mb-1 font-semibold">Batch command failed</p>
                <p>{apiError?.message ?? "Unknown API error."}</p>
              </div>
            ) : results.length === 0 ? (
              <div className="rounded-lg border border-border px-4 py-8 text-center text-sm text-muted-foreground">
                No per-device results returned.
              </div>
            ) : (
              <div className="space-y-4">
                <div className="grid gap-3 md:grid-cols-[280px_minmax(0,1fr)]">
                  <Select
                    value={String(inspectedResult?.deviceId ?? "")}
                    onValueChange={setInspectedDeviceId}
                  >
                    <SelectTrigger className="border-border bg-muted text-foreground">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent className="border-border bg-card text-foreground">
                      {results.map((result) => (
                        <SelectItem
                          key={result.deviceId}
                          value={String(result.deviceId)}
                          className="focus:bg-muted"
                        >
                          Device {result.deviceId} - {result.status}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <div className="rounded-lg border border-border bg-muted p-3">
                    <div className="mb-2 flex flex-wrap items-center gap-2 text-xs">
                      <span className="font-mono text-foreground">
                        Device {inspectedResult.deviceId}
                      </span>
                      <span
                        className={cn(
                          "rounded-full px-2 py-0.5 font-medium",
                          getResultStatus(inspectedResult) === "ok"
                            ? "bg-green-500/20 text-green-400"
                            : "bg-red-500/20 text-red-400"
                        )}
                      >
                        {inspectedResult.status}
                      </span>
                      {inspectedResult.executedAt && (
                        <span className="text-muted-foreground">
                          {inspectedResult.executedAt}
                        </span>
                      )}
                    </div>
                    <pre className="max-h-80 overflow-auto whitespace-pre-wrap break-words font-mono text-xs text-muted-foreground">
                      {inspectedResult.output ||
                        inspectedResult.message ||
                        "(no output)"}
                    </pre>
                  </div>
                </div>

                <div className="overflow-x-auto rounded-lg border border-border">
                  <Table>
                    <TableHeader>
                      <TableRow className="border-border hover:bg-transparent">
                        <TableHead className="text-muted-foreground">Device</TableHead>
                        <TableHead className="text-muted-foreground">Status</TableHead>
                        <TableHead className="text-muted-foreground">Result</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {results.map((result) => {
                        const status = getResultStatus(result);
                        return (
                          <TableRow
                            key={result.deviceId}
                            className="border-border hover:bg-muted"
                          >
                            <TableCell className="font-mono text-xs text-foreground">
                              {result.deviceId}
                            </TableCell>
                            <TableCell>
                              <span
                                className={cn(
                                  "rounded-full px-2 py-0.5 text-xs font-medium",
                                  status === "ok"
                                    ? "bg-green-500/20 text-green-400"
                                    : "bg-red-500/20 text-red-400"
                                )}
                              >
                                {status}
                              </span>
                            </TableCell>
                            <TableCell className="font-mono text-xs text-muted-foreground">
                              {result.status}
                            </TableCell>
                          </TableRow>
                        );
                      })}
                    </TableBody>
                  </Table>
                </div>
              </div>
            )}
          </CardContent>
        </Card>
      )}
    </div>
  );
}
