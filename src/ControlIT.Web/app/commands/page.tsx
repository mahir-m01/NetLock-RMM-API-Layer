"use client";

import { useState, useRef } from "react";
import { useMutation } from "@tanstack/react-query";
import { executeCommand } from "@/lib/api";
import type { Shell } from "@/lib/types";
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
import { cn } from "@/lib/utils";
import { Terminal } from "lucide-react";

export default function CommandsPage() {
  const [deviceId, setDeviceId] = useState("");
  const [command, setCommand] = useState("");
  const [shell, setShell] = useState<Shell>("bash");
  const [timeout, setTimeout] = useState(30);
  const [elapsed, setElapsed] = useState(0);
  const timerRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const mutation = useMutation({
    mutationFn: () =>
      executeCommand({
        deviceId: deviceId.trim(),
        command,
        shell,
        timeoutSeconds: timeout,
      }),
    onMutate: () => {
      setElapsed(0);
      timerRef.current = setInterval(
        () => setElapsed((s) => s + 1),
        1000
      );
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
    mutation.mutate();
  }

  const isSuccess = mutation.isSuccess;
  const isError = mutation.isError;
  const output = mutation.data?.output ?? mutation.data?.error ?? "";
  const exitCode = mutation.data?.exitCode;

  const apiError = mutation.error as Error | null;
  const apiErrorStatus =
    apiError && "status" in apiError ? (apiError as { status: number }).status : null;
  const isBadGateway =
    apiErrorStatus === 503 || apiErrorStatus === 504;

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <Card className="border-border bg-card">
        <CardHeader className="pb-4">
          <CardTitle className="flex items-center gap-2 text-base text-foreground">
            <Terminal className="h-4 w-4 text-muted-foreground" />
            Command Dispatch
          </CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} className="space-y-5">
            <div className="space-y-2">
              <label
                className="text-xs font-medium text-muted-foreground"
                htmlFor="device-id-input"
              >
                Device ID
              </label>
              <Input
                id="device-id-input"
                value={deviceId}
                onChange={(e) => setDeviceId(e.target.value)}
                placeholder="e.g. 27"
                className="border-border bg-muted text-foreground placeholder:text-muted-foreground font-mono"
                required
              />
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
                rows={5}
                placeholder="e.g. ls -la /tmp"
                className="w-full rounded-md border border-border bg-muted px-3 py-2 font-mono text-sm text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring resize-y"
                required
              />
            </div>

            <div className="flex gap-4">
              <div className="flex-1 space-y-2">
                <label className="text-xs font-medium text-muted-foreground">
                  Shell
                </label>
                <Select
                  value={shell}
                  onValueChange={(v) => setShell(v as Shell)}
                >
                  <SelectTrigger className="border-border bg-muted text-foreground">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent className="border-border bg-card text-foreground">
                    <SelectItem value="bash" className="focus:bg-muted">
                      bash
                    </SelectItem>
                    <SelectItem
                      value="powershell"
                      className="focus:bg-muted"
                    >
                      powershell
                    </SelectItem>
                    <SelectItem value="cmd" className="focus:bg-muted">
                      cmd
                    </SelectItem>
                  </SelectContent>
                </Select>
              </div>

              <div className="flex-1 space-y-2">
                <label
                  className="text-xs font-medium text-muted-foreground"
                  htmlFor="timeout-range"
                >
                  Timeout: {timeout}s
                </label>
                <div className="flex items-center gap-2 h-9">
                  <span className="text-xs text-muted-foreground">5s</span>
                  <input
                    id="timeout-range"
                    type="range"
                    min={5}
                    max={120}
                    value={timeout}
                    onChange={(e) => setTimeout(Number(e.target.value))}
                    className="flex-1"
                  />
                  <span className="text-xs text-muted-foreground">120s</span>
                </div>
              </div>
            </div>

            <Button
              type="submit"
              disabled={
                mutation.isPending ||
                !command.trim() ||
                !deviceId.trim()
              }
              className="w-full"
            >
              {mutation.isPending ? (
                <span className="flex items-center gap-2">
                  <span className="h-4 w-4 animate-spin rounded-full border-2 border-background border-t-transparent" />
                  Executing... {elapsed}s
                </span>
              ) : (
                "Execute Command"
              )}
            </Button>
          </form>
        </CardContent>
      </Card>

      {/* Result */}
      {(isSuccess || isError) && (
        <Card className="border-border bg-card">
          <CardHeader className="pb-2 flex flex-row items-center justify-between">
            <CardTitle className="text-sm font-medium text-muted-foreground">
              Output
            </CardTitle>
            {exitCode !== undefined && (
              <span
                className={cn(
                  "rounded-full px-2 py-0.5 text-xs font-mono font-medium",
                  exitCode === 0
                    ? "bg-green-500/20 text-green-400"
                    : "bg-red-500/20 text-red-400"
                )}
              >
                exit {exitCode}
              </span>
            )}
          </CardHeader>
          <CardContent>
            {isBadGateway ? (
              <div className="rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-4 text-sm text-red-400">
                <p className="font-semibold mb-1">
                  {apiErrorStatus === 503
                    ? "Service Unavailable (503)"
                    : "Gateway Timeout (504)"}
                </p>
                <p>
                  The agent did not respond in time. The device may be offline
                  or the command exceeded the timeout.
                </p>
              </div>
            ) : isError ? (
              <div className="rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-4 text-sm text-red-400">
                <p className="font-semibold mb-1">Command Failed</p>
                <p>{apiError?.message ?? "An unknown error occurred."}</p>
              </div>
            ) : (
              <pre
                className={cn(
                  "min-h-20 max-h-96 overflow-y-auto whitespace-pre-wrap break-all rounded-lg border px-4 py-3 font-mono text-xs",
                  exitCode !== undefined && exitCode !== 0
                    ? "border-red-500/30 bg-red-500/10 text-red-300"
                    : "border-green-500/30 bg-green-500/10 text-green-300"
                )}
              >
                {output || "(no output)"}
              </pre>
            )}
          </CardContent>
        </Card>
      )}
    </div>
  );
}
