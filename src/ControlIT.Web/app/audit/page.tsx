"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { getAuditLogs } from "@/lib/api";
import type { AuditLog } from "@/lib/types";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { ChevronLeft, ChevronRight } from "lucide-react";

const PAGE_LIMIT = 25;

function formatTimestamp(ts: string) {
  try {
    return new Intl.DateTimeFormat("en-GB", {
      dateStyle: "medium",
      timeStyle: "short",
    }).format(new Date(ts));
  } catch {
    return ts;
  }
}

function StatusBadge({ status }: { status: string | undefined }) {
  if (!status) return <span className="text-muted-foreground">—</span>;
  const isSuccess =
    status.toLowerCase() === "success" || status.toLowerCase() === "ok";
  return (
    <Badge
      className={
        isSuccess
          ? "bg-green-500/20 text-green-400 border-green-500/30"
          : "bg-red-500/20 text-red-400 border-red-500/30"
      }
    >
      {status}
    </Badge>
  );
}

function SkeletonRows({ count }: { count: number }) {
  return (
    <>
      {Array.from({ length: count }).map((_, i) => (
        <TableRow key={i} className="border-border">
          {Array.from({ length: 5 }).map((_, j) => (
            <TableCell key={j}>
              <Skeleton className="h-4 w-full bg-muted" />
            </TableCell>
          ))}
        </TableRow>
      ))}
    </>
  );
}

export default function AuditPage() {
  const [offset, setOffset] = useState(0);
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");

  const { data, isLoading, isError } = useQuery({
    queryKey: ["audit-logs", offset, from, to],
    queryFn: () =>
      getAuditLogs(
        PAGE_LIMIT,
        offset,
        from || undefined,
        to || undefined
      ),
  });

  // data is AuditLog[] — length drives pagination UI
  const itemCount = data?.length ?? 0;
  const page = Math.floor(offset / PAGE_LIMIT) + 1;
  const hasNextPage = itemCount === PAGE_LIMIT;

  function handleFromChange(e: React.ChangeEvent<HTMLInputElement>) {
    setFrom(e.target.value);
    setOffset(0);
  }

  function handleToChange(e: React.ChangeEvent<HTMLInputElement>) {
    setTo(e.target.value);
    setOffset(0);
  }

  return (
    <div className="space-y-4">
      {/* Date range filters */}
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
        <div className="flex items-center gap-2">
          <label className="text-xs text-muted-foreground whitespace-nowrap">
            From
          </label>
          <Input
            type="datetime-local"
            value={from}
            onChange={handleFromChange}
            className="border-border bg-muted text-foreground [color-scheme:dark]"
          />
        </div>
        <div className="flex items-center gap-2">
          <label className="text-xs text-muted-foreground whitespace-nowrap">
            To
          </label>
          <Input
            type="datetime-local"
            value={to}
            onChange={handleToChange}
            className="border-border bg-muted text-foreground [color-scheme:dark]"
          />
        </div>
        {(from || to) && (
          <Button
            variant="ghost"
            size="sm"
            className="text-muted-foreground hover:text-foreground hover:bg-card"
            onClick={() => {
              setFrom("");
              setTo("");
              setOffset(0);
            }}
          >
            Clear filters
          </Button>
        )}
      </div>

      {isError && (
        <div className="rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-400">
          Failed to load audit logs. Check your API key and connection.
        </div>
      )}

      <div className="overflow-x-auto rounded-lg border border-border bg-card">
        <Table>
          <TableHeader>
            <TableRow className="border-border hover:bg-transparent">
              <TableHead className="text-muted-foreground">Timestamp</TableHead>
              <TableHead className="text-muted-foreground">Action</TableHead>
              <TableHead className="text-muted-foreground">Device</TableHead>
              <TableHead className="text-muted-foreground">Tenant</TableHead>
              <TableHead className="text-muted-foreground">Status</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading ? (
              <SkeletonRows count={10} />
            ) : data?.length === 0 ? (
              <TableRow className="border-border">
                <TableCell
                  colSpan={5}
                  className="py-10 text-center text-muted-foreground"
                >
                  No audit logs found.
                </TableCell>
              </TableRow>
            ) : (
              data?.map((log: AuditLog) => (
                <TableRow
                  key={log.id}
                  className="border-border hover:bg-muted"
                >
                  <TableCell className="font-mono text-xs text-muted-foreground whitespace-nowrap">
                    {formatTimestamp(log.timestamp)}
                  </TableCell>
                  <TableCell className="text-foreground font-medium">
                    {log.action}
                  </TableCell>
                  <TableCell className="text-foreground">
                    {log.deviceName ?? log.deviceId ?? "—"}
                  </TableCell>
                  <TableCell className="text-foreground">
                    {log.tenantName ?? log.tenantId ?? "—"}
                  </TableCell>
                  <TableCell>
                    <StatusBadge status={log.status as string | undefined} />
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </div>

      {!isLoading && (offset > 0 || hasNextPage) && (
        <div className="flex items-center justify-between text-sm text-muted-foreground">
          <span>Page {page}</span>
          <div className="flex items-center gap-2">
            <Button
              variant="ghost"
              size="icon"
              className="h-8 w-8 text-muted-foreground hover:bg-card hover:text-foreground disabled:opacity-40"
              onClick={() => setOffset((o) => Math.max(0, o - PAGE_LIMIT))}
              disabled={offset === 0}
              aria-label="Previous page"
            >
              <ChevronLeft className="h-4 w-4" />
            </Button>
            <Button
              variant="ghost"
              size="icon"
              className="h-8 w-8 text-muted-foreground hover:bg-card hover:text-foreground disabled:opacity-40"
              onClick={() => setOffset((o) => o + PAGE_LIMIT)}
              disabled={!hasNextPage}
              aria-label="Next page"
            >
              <ChevronRight className="h-4 w-4" />
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
