"use client";

import { use, useRef, useState } from "react";
import { useQuery, useMutation } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { getDevice, executeCommand } from "@/lib/api";
import type { Shell } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { ArrowLeft, Terminal, X, GripHorizontal } from "lucide-react";
import { cn } from "@/lib/utils";

interface PageProps {
  params: Promise<{ id: string }>;
}

function OnlineBadge({ isOnline }: { isOnline: boolean }) {
  return (
    <Badge
      className={
        isOnline
          ? "bg-blue-500/20 text-blue-400 border-blue-500/30"
          : "bg-red-500/20 text-red-400 border-red-500/30"
      }
    >
      {isOnline ? "Online" : "Offline"}
    </Badge>
  );
}

function DetailRow({ label, value, dim }: { label: string; value: string | number | null | undefined; dim?: boolean }) {
  const display =
    value !== undefined && value !== null && value !== ""
      ? String(value)
      : "—";
  return (
    <div className="flex flex-col gap-0.5 py-3 border-b border-border last:border-0">
      <span className="text-xs text-muted-foreground">{label}</span>
      <span className={`text-sm font-mono break-all ${dim ? "text-muted-foreground italic" : "text-foreground"}`}>{display}</span>
    </div>
  );
}

// ── Resize handle edges ────────────────────────────────────────────────────────
type ResizeEdge = "n" | "s" | "e" | "w" | "ne" | "nw" | "se" | "sw";


// ── Floating draggable + resizable command window ──────────────────────────────
function CommandWindow({
  deviceId,
  isOnline,
  onClose,
}: {
  deviceId: string;
  isOnline: boolean;
  onClose: () => void;
}) {
  const [command, setCommand] = useState("");
  const [shell, setShell] = useState<Shell>("bash");
  const [timeout, setTimeout] = useState(30);
  const [elapsed, setElapsed] = useState(0);
  const [timerRef, setTimerRef] = useState<ReturnType<typeof setInterval> | null>(null);

  const MIN_W = 360;
  const MIN_H = 300;

  // Position and size — null means "not yet set, use centered defaults"
  const [rect, setRect] = useState<{ x: number; y: number; w: number; h: number } | null>(null);
  const [interacting, setInteracting] = useState(false);

  const resolvedRect = rect ?? (typeof window !== "undefined"
    ? { x: window.innerWidth / 2 - 240, y: window.innerHeight / 2 - 260, w: 480, h: 520 }
    : { x: 200, y: 100, w: 480, h: 520 });

  // ── Drag title bar ───────────────────────────────────────────────────────────
  function onTitleMouseDown(e: React.MouseEvent) {
    e.preventDefault();
    const start = { mx: e.clientX, my: e.clientY, rx: resolvedRect.x, ry: resolvedRect.y };
    setInteracting(true);

    function onMove(me: MouseEvent) {
      setRect((r) => {
        const base = r ?? resolvedRect;
        return { ...base, x: start.rx + me.clientX - start.mx, y: start.ry + me.clientY - start.my };
      });
    }
    function onUp() {
      setInteracting(false);
      window.removeEventListener("mousemove", onMove);
      window.removeEventListener("mouseup", onUp);
    }
    window.addEventListener("mousemove", onMove);
    window.addEventListener("mouseup", onUp);
  }

  // ── Resize edges ─────────────────────────────────────────────────────────────
  function onResizeMouseDown(e: React.MouseEvent, edge: ResizeEdge) {
    e.preventDefault();
    e.stopPropagation();
    const start = {
      mx: e.clientX, my: e.clientY,
      rx: resolvedRect.x, ry: resolvedRect.y,
      rw: resolvedRect.w, rh: resolvedRect.h,
    };
    setInteracting(true);

    function onMove(me: MouseEvent) {
      const dx = me.clientX - start.mx;
      const dy = me.clientY - start.my;
      setRect(() => {
        let { rx: x, ry: y, rw: w, rh: h } = start;
        if (edge.includes("e")) w = Math.max(MIN_W, w + dx);
        if (edge.includes("s")) h = Math.max(MIN_H, h + dy);
        if (edge.includes("w")) { const nw = Math.max(MIN_W, w - dx); x += w - nw; w = nw; }
        if (edge.includes("n")) { const nh = Math.max(MIN_H, h - dy); y += h - nh; h = nh; }
        return { x, y, w, h };
      });
    }
    function onUp() {
      setInteracting(false);
      window.removeEventListener("mousemove", onMove);
      window.removeEventListener("mouseup", onUp);
    }
    window.addEventListener("mousemove", onMove);
    window.addEventListener("mouseup", onUp);
  }

  const mutation = useMutation({
    mutationFn: () =>
      executeCommand({ deviceId, command, shell, timeoutSeconds: timeout }),
    onMutate: () => {
      setElapsed(0);
      const ref = setInterval(() => setElapsed((s) => s + 1), 1000);
      setTimerRef(ref);
    },
    onSettled: () => {
      if (timerRef) clearInterval(timerRef);
    },
  });

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    mutation.mutate();
  }

  const output = mutation.data?.output ?? mutation.data?.error ?? "";
  const exitCode = mutation.data?.exitCode;

  const EDGE = 6; // hit-area thickness in px

  return (
    <div
      style={{
        left: resolvedRect.x,
        top: resolvedRect.y,
        width: resolvedRect.w,
        height: resolvedRect.h,
        userSelect: interacting ? "none" : "auto",
      }}
      className="fixed z-50 flex flex-col rounded-xl border border-[#1a1a1a] bg-black shadow-2xl shadow-black/60 overflow-hidden"
    >
      {/* ── Edge / corner resize handles ─────────────────────────────────── */}
      {/* N */ }
      <div onMouseDown={(e) => onResizeMouseDown(e, "n")}  style={{ position:"absolute", top:0,    left:EDGE,  right:EDGE,  height:EDGE, cursor:"n-resize",  zIndex:10 }} />
      {/* S */}
      <div onMouseDown={(e) => onResizeMouseDown(e, "s")}  style={{ position:"absolute", bottom:0, left:EDGE,  right:EDGE,  height:EDGE, cursor:"s-resize",  zIndex:10 }} />
      {/* W */}
      <div onMouseDown={(e) => onResizeMouseDown(e, "w")}  style={{ position:"absolute", top:EDGE, left:0,    bottom:EDGE, width:EDGE,  cursor:"w-resize",  zIndex:10 }} />
      {/* E */}
      <div onMouseDown={(e) => onResizeMouseDown(e, "e")}  style={{ position:"absolute", top:EDGE, right:0,   bottom:EDGE, width:EDGE,  cursor:"e-resize",  zIndex:10 }} />
      {/* NW */}
      <div onMouseDown={(e) => onResizeMouseDown(e, "nw")} style={{ position:"absolute", top:0,    left:0,    width:EDGE,  height:EDGE, cursor:"nw-resize", zIndex:11 }} />
      {/* NE */}
      <div onMouseDown={(e) => onResizeMouseDown(e, "ne")} style={{ position:"absolute", top:0,    right:0,   width:EDGE,  height:EDGE, cursor:"ne-resize", zIndex:11 }} />
      {/* SW */}
      <div onMouseDown={(e) => onResizeMouseDown(e, "sw")} style={{ position:"absolute", bottom:0, left:0,    width:EDGE,  height:EDGE, cursor:"sw-resize", zIndex:11 }} />
      {/* SE */}
      <div onMouseDown={(e) => onResizeMouseDown(e, "se")} style={{ position:"absolute", bottom:0, right:0,   width:EDGE,  height:EDGE, cursor:"se-resize", zIndex:11 }} />

      {/* Title bar — drag handle */}
      <div
        onMouseDown={onTitleMouseDown}
        className="flex shrink-0 items-center gap-2 px-4 py-3 border-b border-[#1a1a1a] cursor-grab active:cursor-grabbing select-none bg-[#0a0a0a]"
      >
        <GripHorizontal className="h-3.5 w-3.5 text-zinc-500 shrink-0" />
        <Terminal className="h-3.5 w-3.5 text-blue-400 shrink-0" />
        <span className="text-sm font-medium text-white flex-1">Execute Command</span>
        <span className="text-xs text-zinc-500 font-mono">device {deviceId}</span>
        <button
          onClick={onClose}
          className="ml-2 rounded p-0.5 text-zinc-500 hover:text-white hover:bg-white/10"
        >
          <X className="h-3.5 w-3.5" />
        </button>
      </div>

      {/* Scrollable body */}
      <div className="flex-1 overflow-y-auto p-4 space-y-4 bg-black">
        {!isOnline && (
          <div className="rounded-md border border-red-500/30 bg-red-500/10 px-3 py-2 text-xs text-red-400">
            Device is offline — commands will be rejected.
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-3">
          <div className="space-y-1.5">
            <label className="text-xs text-zinc-400" htmlFor="cmd-input">
              Command
            </label>
            <textarea
              id="cmd-input"
              value={command}
              onChange={(e) => setCommand(e.target.value)}
              rows={3}
              placeholder="e.g. ls -la / or Get-Process"
              className="w-full rounded-md border border-[#1a1a1a] bg-[#0a0a0a] px-3 py-2 font-mono text-sm text-white placeholder:text-zinc-600 focus:outline-none focus:ring-1 focus:ring-blue-500 resize-none"
              required
            />
          </div>

          <div className="flex gap-3">
            <div className="flex-1 space-y-1.5">
              <label className="text-xs text-zinc-400">Shell</label>
              <Select value={shell} onValueChange={(v) => setShell(v as Shell)}>
                <SelectTrigger className="border-[#1a1a1a] bg-[#0a0a0a] text-white h-9">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent className="border-[#1a1a1a] bg-black text-white">
                  <SelectItem value="bash">bash</SelectItem>
                  <SelectItem value="powershell">powershell</SelectItem>
                  <SelectItem value="cmd">cmd</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="flex-1 space-y-1.5">
              <label className="text-xs text-zinc-400">Timeout: {timeout}s</label>
              <Input
                type="range"
                min={5}
                max={120}
                value={timeout}
                onChange={(e) => setTimeout(Number(e.target.value))}
                className="h-9 border-[#1a1a1a] bg-[#0a0a0a] accent-blue-500"
              />
            </div>
          </div>

          <Button
            type="submit"
            disabled={mutation.isPending || !command.trim() || !isOnline}
            className="w-full bg-blue-600 hover:bg-blue-500 text-white border-0"
          >
            {mutation.isPending ? (
              <span className="flex items-center gap-2">
                <span className="h-3.5 w-3.5 animate-spin rounded-full border-2 border-white border-t-transparent" />
                Running... {elapsed}s
              </span>
            ) : (
              "Execute"
            )}
          </Button>
        </form>

        {(mutation.isSuccess || mutation.isError) && (
          <div className="space-y-1.5">
            <div className="flex items-center justify-between">
              <span className="text-xs text-zinc-400">Output</span>
              {exitCode !== undefined && (
                <Badge
                  className={
                    exitCode === 0
                      ? "bg-blue-500/20 text-blue-400 border-blue-500/30"
                      : "bg-red-500/20 text-red-400 border-red-500/30"
                  }
                >
                  exit {exitCode}
                </Badge>
              )}
            </div>
            <pre
              className={cn(
                "min-h-16 max-h-56 overflow-y-auto whitespace-pre-wrap break-all rounded-lg border px-3 py-2 font-mono text-xs",
                mutation.isError || (exitCode !== undefined && exitCode !== 0)
                  ? "border-red-500/30 bg-red-500/10 text-red-300"
                  : "border-green-500/30 bg-green-500/10 text-green-300"
              )}
            >
              {mutation.isError
                ? (mutation.error as Error)?.message ?? "Unknown error"
                : output || "(no output)"}
            </pre>
          </div>
        )}
      </div>
    </div>
  );
}

export default function DeviceDetailPage({ params }: PageProps) {
  const { id } = use(params);
  const router = useRouter();
  const [windowOpen, setWindowOpen] = useState(false);

  const { data: device, isLoading, isError } = useQuery({
    queryKey: ["device", id],
    queryFn: () => getDevice(id),
  });

  return (
    <div className="space-y-4">
      {/* Header row */}
      <div className="flex items-center gap-3">
        <Button
          variant="ghost"
          size="icon"
          className="h-8 w-8 text-muted-foreground hover:bg-card hover:text-foreground"
          onClick={() => router.back()}
          aria-label="Go back"
        >
          <ArrowLeft className="h-4 w-4" />
        </Button>
        <div>
          {isLoading ? (
            <Skeleton className="h-5 w-40 bg-muted" />
          ) : (
            <h2 className="text-base font-semibold text-foreground">
              {device?.deviceName ?? id}
            </h2>
          )}
        </div>
        {device && (
          <div className="ml-auto flex items-center gap-2">
            <OnlineBadge isOnline={device.isOnline} />
            <Button onClick={() => setWindowOpen(true)} size="sm" className="bg-blue-600 hover:bg-blue-500 text-white border-0">
              <Terminal className="mr-2 h-3.5 w-3.5" />
              Execute Command
            </Button>
          </div>
        )}
      </div>

      {isError && (
        <div className="rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-400">
          Failed to load device. It may not exist or you lack permission.
        </div>
      )}

      {isLoading ? (
        <Card className="border-border bg-card">
          <CardContent className="space-y-3 pt-6">
            {Array.from({ length: 8 }).map((_, i) => (
              <Skeleton key={i} className="h-10 w-full bg-muted" />
            ))}
          </CardContent>
        </Card>
      ) : device ? (
        <Card className="border-border bg-card">
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-muted-foreground">
              Device Information
            </CardTitle>
          </CardHeader>
          <CardContent>
            <DetailRow label="Device ID" value={device.id} />
            <DetailRow label="Device Name" value={device.deviceName} />
            <DetailRow label="Tenant ID" value={device.tenantId} />
            <DetailRow label="Platform" value={device.platform} />
            <DetailRow label="OS Version" value={device.operatingSystem} />
            <DetailRow label="Agent Version" value={device.agentVersion} />
            <DetailRow label="IP Address (Internal)" value={device.ipAddressInternal} />
            <DetailRow label="IP Address (External)" value={device.ipAddressExternal} />
            <DetailRow label="Netbird IP" value="Not configured" dim />
            <DetailRow label="CPU Usage" value={device.cpuUsage !== null ? `${device.cpuUsage?.toFixed(1)}%` : null} />
            <DetailRow label="RAM Usage" value={device.ramUsage !== null ? `${device.ramUsage?.toFixed(1)}%` : null} />
            <DetailRow
              label="Last Seen"
              value={device.lastAccess ? new Date(device.lastAccess).toLocaleString() : null}
            />
          </CardContent>
        </Card>
      ) : null}

      {windowOpen && device && (
        <CommandWindow
          deviceId={id}
          isOnline={device.isOnline}
          onClose={() => setWindowOpen(false)}
        />
      )}
    </div>
  );
}
