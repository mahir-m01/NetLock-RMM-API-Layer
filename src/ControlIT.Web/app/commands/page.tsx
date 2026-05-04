"use client";

import { useEffect, useRef, useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { executeBatchCommand } from "@/lib/api";
import type { BatchCommandResult, Shell } from "@/lib/types";
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
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { cn } from "@/lib/utils";
import { Terminal } from "lucide-react";

const DEVICE_ID_SPLIT_PATTERN = /[\s,]+/;
const MAX_BATCH_TARGETS = 25;

function parseDeviceIds(value: string): { ids: number[]; invalid: string[] } {
  const invalid: string[] = [];
  const ids = new Set<number>();

  value
    .split(DEVICE_ID_SPLIT_PATTERN)
    .map((deviceId) => deviceId.trim())
    .filter(Boolean)
    .forEach((deviceId) => {
      if (!/^\d+$/.test(deviceId)) {
        invalid.push(deviceId);
        return;
      }

      const parsed = Number(deviceId);
      if (!Number.isSafeInteger(parsed) || parsed <= 0) {
        invalid.push(deviceId);
        return;
      }

      ids.add(parsed);
    });

  return { ids: Array.from(ids), invalid };
}

function getResultStatus(result: BatchCommandResult): "ok" | "failed" {
  if (result.status !== "SUCCESS") return "failed";
  return "ok";
}

export default function CommandsPage() {
  const [deviceIdsValue, setDeviceIdsValue] = useState("");
  const [command, setCommand] = useState("");
  const [shell, setShell] = useState<Shell>("bash");
  const [timeoutSeconds, setTimeoutSeconds] = useState(30);
  const [elapsed, setElapsed] = useState(0);
  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const { ids: deviceIds, invalid: invalidDeviceIds } = parseDeviceIds(deviceIdsValue);
  const exceedsBatchLimit = deviceIds.length > MAX_BATCH_TARGETS;

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

  const apiError = mutation.error as Error | null;
  const apiErrorStatus =
    apiError && "status" in apiError ? (apiError as { status: number }).status : null;
  const isBadGateway = apiErrorStatus === 503 || apiErrorStatus === 504;
  const results = mutation.data?.results ?? [];
  const cannotSubmit =
    mutation.isPending ||
    !command.trim() ||
    deviceIds.length === 0 ||
    invalidDeviceIds.length > 0 ||
    exceedsBatchLimit;

  return (
    <div className="mx-auto max-w-4xl space-y-4">
      <Card className="border-border bg-card">
        <CardHeader className="pb-3">
          <CardTitle className="flex items-center gap-2 text-base text-foreground">
            <Terminal className="h-4 w-4 text-muted-foreground" />
            Batch Command Dispatch
          </CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} className="space-y-4">
            <div className="grid gap-4 md:grid-cols-[1fr_180px]">
              <div className="space-y-2">
                <label
                  className="text-xs font-medium text-muted-foreground"
                  htmlFor="device-ids-input"
                >
                  Device IDs
                </label>
                <Input
                  id="device-ids-input"
                  value={deviceIdsValue}
                  onChange={(e) => setDeviceIdsValue(e.target.value)}
                  placeholder="27, 28, 31"
                  className="border-border bg-muted font-mono text-foreground placeholder:text-muted-foreground"
                  required
                />
                <p className="text-xs text-muted-foreground">
                  {deviceIds.length} target{deviceIds.length === 1 ? "" : "s"} selected
                </p>
                {invalidDeviceIds.length > 0 && (
                  <p className="text-xs text-red-400">
                    Invalid IDs: {invalidDeviceIds.slice(0, 3).join(", ")}
                  </p>
                )}
                {exceedsBatchLimit && (
                  <p className="text-xs text-red-400">
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
            ) : (
              <div className="overflow-x-auto rounded-lg border border-border">
                <Table>
                  <TableHeader>
                    <TableRow className="border-border hover:bg-transparent">
                      <TableHead className="text-muted-foreground">Device</TableHead>
                      <TableHead className="text-muted-foreground">Status</TableHead>
                      <TableHead className="text-muted-foreground">Result</TableHead>
                      <TableHead className="text-muted-foreground">Output</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {results.map((result) => {
                      const status = getResultStatus(result);
                      const output = result.output || result.message || "(no output)";
                      return (
                        <TableRow key={result.deviceId} className="border-border hover:bg-muted">
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
                          <TableCell className="max-w-xl">
                            <pre className="max-h-28 overflow-y-auto whitespace-pre-wrap break-all font-mono text-xs text-muted-foreground">
                              {output}
                            </pre>
                          </TableCell>
                        </TableRow>
                      );
                    })}
                    {results.length === 0 && (
                      <TableRow className="border-border">
                        <TableCell colSpan={4} className="py-8 text-center text-muted-foreground">
                          No per-device results returned.
                        </TableCell>
                      </TableRow>
                    )}
                  </TableBody>
                </Table>
              </div>
            )}
          </CardContent>
        </Card>
      )}
    </div>
  );
}
