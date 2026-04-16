"use client";

import { use, useState } from "react";
import { useQuery, useMutation } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { getDevice, executeCommand } from "@/lib/api";
import type { Shell } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetDescription,
} from "@/components/ui/sheet";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { ArrowLeft, Terminal } from "lucide-react";
import { cn } from "@/lib/utils";

interface PageProps {
  params: Promise<{ id: string }>;
}

function StatusBadge({ status }: { status: string }) {
  const isOnline = status?.toLowerCase() === "online";
  return (
    <Badge
      className={
        isOnline
          ? "bg-green-500/20 text-green-400 border-green-500/30"
          : "bg-red-500/20 text-red-400 border-red-500/30"
      }
    >
      {isOnline ? "Online" : "Offline"}
    </Badge>
  );
}

function DetailRow({
  label,
  value,
}: {
  label: string;
  value: string | undefined;
}) {
  return (
    <div className="flex flex-col gap-0.5 py-3 border-b border-[rgba(107,148,193,0.18)] last:border-0">
      <span className="text-xs text-[#85AFDD]">{label}</span>
      <span className="text-sm text-[#E9F1FF] font-mono break-all">
        {value ?? "—"}
      </span>
    </div>
  );
}

function CommandSheet({
  deviceId,
  open,
  onOpenChange,
}: {
  deviceId: string;
  open: boolean;
  onOpenChange: (v: boolean) => void;
}) {
  const [command, setCommand] = useState("");
  const [shell, setShell] = useState<Shell>("bash");
  const [timeout, setTimeout] = useState(30);
  const [elapsed, setElapsed] = useState(0);
  const [timerRef, setTimerRef] = useState<ReturnType<typeof setInterval> | null>(null);

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

  const isSuccess = mutation.isSuccess;
  const isError = mutation.isError;
  const output =
    mutation.data?.output ?? mutation.data?.error ?? "";
  const status = mutation.data?.exitCode;

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="border-l border-[rgba(107,148,193,0.18)] bg-[#003257] text-[#E9F1FF] w-full sm:max-w-lg overflow-y-auto">
        <SheetHeader className="mb-4">
          <SheetTitle className="text-[#E9F1FF] flex items-center gap-2">
            <Terminal className="h-4 w-4 text-[#A1CAFA]" />
            Execute Command
          </SheetTitle>
          <SheetDescription className="text-[#85AFDD]">
            Run a command on device{" "}
            <span className="font-mono text-[#A1CAFA]">{deviceId}</span>.
          </SheetDescription>
        </SheetHeader>

        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-2">
            <label className="text-xs text-[#85AFDD]" htmlFor="cmd-input">
              Command
            </label>
            <textarea
              id="cmd-input"
              value={command}
              onChange={(e) => setCommand(e.target.value)}
              rows={4}
              placeholder="e.g. ls -la / or Get-Process"
              className="w-full rounded-md border border-[rgba(107,148,193,0.18)] bg-[#1B4972] px-3 py-2 font-mono text-sm text-[#E9F1FF] placeholder:text-[#85AFDD] focus:outline-none focus:ring-2 focus:ring-[#A1CAFA] resize-none"
              required
            />
          </div>

          <div className="flex gap-3">
            <div className="flex-1 space-y-2">
              <label className="text-xs text-[#85AFDD]">Shell</label>
              <Select
                value={shell}
                onValueChange={(v) => setShell(v as Shell)}
              >
                <SelectTrigger className="border-[rgba(107,148,193,0.18)] bg-[#1B4972] text-[#E9F1FF]">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent className="border-[rgba(107,148,193,0.18)] bg-[#003257] text-[#E9F1FF]">
                  <SelectItem value="bash" className="focus:bg-[#1B4972]">bash</SelectItem>
                  <SelectItem value="powershell" className="focus:bg-[#1B4972]">powershell</SelectItem>
                  <SelectItem value="cmd" className="focus:bg-[#1B4972]">cmd</SelectItem>
                </SelectContent>
              </Select>
            </div>

            <div className="flex-1 space-y-2">
              <label className="text-xs text-[#85AFDD]" htmlFor="timeout-input">
                Timeout: {timeout}s
              </label>
              <Input
                id="timeout-input"
                type="range"
                min={5}
                max={120}
                value={timeout}
                onChange={(e) => setTimeout(Number(e.target.value))}
                className="h-9 border-[rgba(107,148,193,0.18)] bg-[#1B4972] accent-[#A1CAFA]"
              />
            </div>
          </div>

          <Button
            type="submit"
            disabled={mutation.isPending || !command.trim()}
            className="w-full bg-[#A1CAFA] text-[#001D35] font-semibold hover:bg-[#D0E4FF] disabled:opacity-50"
          >
            {mutation.isPending ? (
              <span className="flex items-center gap-2">
                <span className="h-3.5 w-3.5 animate-spin rounded-full border-2 border-[#001D35] border-t-transparent" />
                Running... {elapsed}s
              </span>
            ) : (
              "Execute"
            )}
          </Button>
        </form>

        {(isSuccess || isError) && (
          <div className="mt-6 space-y-2">
            <div className="flex items-center justify-between">
              <span className="text-xs text-[#85AFDD]">Output</span>
              {status !== undefined && (
                <Badge
                  className={
                    status === 0
                      ? "bg-green-500/20 text-green-400 border-green-500/30"
                      : "bg-red-500/20 text-red-400 border-red-500/30"
                  }
                >
                  exit {status}
                </Badge>
              )}
            </div>
            <pre
              className={cn(
                "min-h-20 max-h-72 overflow-y-auto whitespace-pre-wrap break-all rounded-lg border px-4 py-3 font-mono text-xs",
                isError || (status !== undefined && status !== 0)
                  ? "border-red-500/30 bg-red-500/10 text-red-300"
                  : "border-green-500/30 bg-green-500/10 text-green-300"
              )}
            >
              {isError
                ? (mutation.error as Error)?.message ?? "Unknown error"
                : output || "(no output)"}
            </pre>
          </div>
        )}
      </SheetContent>
    </Sheet>
  );
}

export default function DeviceDetailPage({ params }: PageProps) {
  const { id } = use(params);
  const router = useRouter();
  const [sheetOpen, setSheetOpen] = useState(false);

  const { data: device, isLoading, isError } = useQuery({
    queryKey: ["device", id],
    queryFn: () => getDevice(id),
  });

  return (
    <div className="space-y-4">
      <div className="flex items-center gap-3">
        <Button
          variant="ghost"
          size="icon"
          className="h-8 w-8 text-[#85AFDD] hover:bg-[#003257] hover:text-[#E9F1FF]"
          onClick={() => router.back()}
          aria-label="Go back"
        >
          <ArrowLeft className="h-4 w-4" />
        </Button>
        <div>
          {isLoading ? (
            <Skeleton className="h-5 w-40 bg-[#1B4972]" />
          ) : (
            <h2 className="text-base font-semibold text-[#E9F1FF]">
              {device?.deviceName ?? id}
            </h2>
          )}
        </div>
        {device && (
          <div className="ml-auto flex items-center gap-2">
            <StatusBadge status={device.status} />
            <Button
              onClick={() => setSheetOpen(true)}
              className="bg-[#A1CAFA] text-[#001D35] font-semibold hover:bg-[#D0E4FF]"
              size="sm"
            >
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
        <Card className="border-[rgba(107,148,193,0.18)] bg-[#003257]">
          <CardContent className="space-y-3 pt-6">
            {Array.from({ length: 8 }).map((_, i) => (
              <Skeleton key={i} className="h-10 w-full bg-[#1B4972]" />
            ))}
          </CardContent>
        </Card>
      ) : device ? (
        <Card className="border-[rgba(107,148,193,0.18)] bg-[#003257]">
          <CardHeader className="pb-2">
            <CardTitle className="text-sm font-medium text-[#85AFDD]">
              Device Information
            </CardTitle>
          </CardHeader>
          <CardContent>
            <DetailRow label="Device ID" value={device.id} />
            <DetailRow label="Device Name" value={device.deviceName} />
            <DetailRow label="Platform" value={device.platform} />
            <DetailRow label="OS Version" value={device.osVersion as string | undefined} />
            <DetailRow label="IP Address" value={device.ipAddress as string | undefined} />
            <DetailRow label="MAC Address" value={device.macAddress as string | undefined} />
            <DetailRow label="Tenant ID" value={device.tenantId} />
            <DetailRow label="Agent Version" value={device.agentVersion as string | undefined} />
            <DetailRow label="Last Seen" value={device.lastSeen as string | undefined} />
            <DetailRow label="Created At" value={device.createdAt as string | undefined} />
          </CardContent>
        </Card>
      ) : null}

      <CommandSheet
        deviceId={id}
        open={sheetOpen}
        onOpenChange={setSheetOpen}
      />
    </div>
  );
}
