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

function ResultBadge({ result }: { result: string }) {
  const lower = result?.toLowerCase();
  const className =
    lower === "success"
      ? "bg-blue-500/20 text-blue-400 border-blue-500/30"
      : lower === "pending"
      ? "bg-yellow-500/20 text-yellow-400 border-yellow-500/30"
      : lower === "timeout"
      ? "bg-orange-500/20 text-orange-400 border-orange-500/30"
      : "bg-red-500/20 text-red-400 border-red-500/30";
  return <Badge className={className}>{result}</Badge>;
}

function ResourceCell({ type, id }: { type: string; id: string | null }) {
  if (!id) return <span className="text-muted-foreground">—</span>;
  return (
    <span className="font-mono text-xs">
      <span className="text-muted-foreground">{type} </span>
      <span className="text-foreground">#{id}</span>
    </span>
  );
}

function SkeletonRows({ count }: { count: number }) {
  return (
    <>
      {Array.from({ length: count }).map((_, i) => (
        <TableRow key={i} className="border-border">
          {Array.from({ length: 6 }).map((_, j) => (
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
    queryFn: () => getAuditLogs(PAGE_LIMIT, offset, from || undefined, to || undefined),
  });

  const itemCount = data?.length ?? 0;
  const page = Math.floor(offset / PAGE_LIMIT) + 1;
  const hasNextPage = itemCount === PAGE_LIMIT;

  return (
    <div className="space-y-4">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
        <div className="flex items-center gap-2">
          <label className="text-xs text-muted-foreground whitespace-nowrap">From</label>
          <Input
            type="datetime-local"
            value={from}
            onChange={(e) => { setFrom(e.target.value); setOffset(0); }}
            className="border-border bg-muted text-foreground [color-scheme:dark]"
          />
        </div>
        <div className="flex items-center gap-2">
          <label className="text-xs text-muted-foreground whitespace-nowrap">To</label>
          <Input
            type="datetime-local"
            value={to}
            onChange={(e) => { setTo(e.target.value); setOffset(0); }}
            className="border-border bg-muted text-foreground [color-scheme:dark]"
          />
        </div>
        {(from || to) && (
          <Button
            variant="ghost"
            size="sm"
            className="text-muted-foreground hover:text-foreground hover:bg-card"
            onClick={() => { setFrom(""); setTo(""); setOffset(0); }}
          >
            Clear filters
          </Button>
        )}
      </div>

      {isError && (
        <div className="rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-400">
          Failed to load audit logs.
        </div>
      )}

      <div className="overflow-x-auto rounded-lg border border-border bg-card">
        <Table>
          <TableHeader>
            <TableRow className="border-border hover:bg-transparent">
              <TableHead className="text-muted-foreground">Timestamp</TableHead>
              <TableHead className="text-muted-foreground">Action</TableHead>
              <TableHead className="text-muted-foreground">Actor</TableHead>
              <TableHead className="text-muted-foreground">Resource</TableHead>
              <TableHead className="text-muted-foreground">Tenant</TableHead>
              <TableHead className="text-muted-foreground">Result</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading ? (
              <SkeletonRows count={10} />
            ) : data?.length === 0 ? (
              <TableRow className="border-border">
                <TableCell colSpan={6} className="py-10 text-center text-muted-foreground">
                  No audit logs found.
                </TableCell>
              </TableRow>
            ) : (
              data?.map((log: AuditLog) => (
                <TableRow key={log.id} className="border-border hover:bg-muted/30">
                  <TableCell className="font-mono text-xs text-muted-foreground whitespace-nowrap">
                    {formatTimestamp(log.timestamp)}
                  </TableCell>
                  <TableCell className="text-foreground font-medium text-sm">
                    {log.action}
                  </TableCell>
                  <TableCell className="font-mono text-xs text-foreground">
                    {log.actorEmail || "—"}
                  </TableCell>
                  <TableCell>
                    <ResourceCell type={log.resourceType} id={log.resourceId} />
                  </TableCell>
                  <TableCell className="text-muted-foreground text-sm">
                    {log.tenantId === 0 ? (
                      <span className="text-xs italic text-muted-foreground">Global</span>
                    ) : (
                      log.tenantId
                    )}
                  </TableCell>
                  <TableCell>
                    <div className="flex flex-col gap-1">
                      <ResultBadge result={log.result} />
                      {log.errorMessage && (
                        <span className="text-xs text-red-400 font-mono break-all max-w-[200px]">
                          {log.errorMessage}
                        </span>
                      )}
                    </div>
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
