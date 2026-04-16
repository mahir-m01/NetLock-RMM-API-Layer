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
  if (!status) return <span className="text-[#85AFDD]">—</span>;
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
        <TableRow key={i} className="border-[rgba(107,148,193,0.18)]">
          {Array.from({ length: 5 }).map((_, j) => (
            <TableCell key={j}>
              <Skeleton className="h-4 w-full bg-[#1B4972]" />
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

  const total = data?.total ?? 0;
  const page = Math.floor(offset / PAGE_LIMIT) + 1;
  const totalPages = Math.max(1, Math.ceil(total / PAGE_LIMIT));

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
          <label className="text-xs text-[#85AFDD] whitespace-nowrap">
            From
          </label>
          <Input
            type="datetime-local"
            value={from}
            onChange={handleFromChange}
            className="border-[rgba(107,148,193,0.18)] bg-[#1B4972] text-[#E9F1FF] [color-scheme:dark]"
          />
        </div>
        <div className="flex items-center gap-2">
          <label className="text-xs text-[#85AFDD] whitespace-nowrap">
            To
          </label>
          <Input
            type="datetime-local"
            value={to}
            onChange={handleToChange}
            className="border-[rgba(107,148,193,0.18)] bg-[#1B4972] text-[#E9F1FF] [color-scheme:dark]"
          />
        </div>
        {(from || to) && (
          <Button
            variant="ghost"
            size="sm"
            className="text-[#85AFDD] hover:text-[#E9F1FF] hover:bg-[#003257]"
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

      <div className="overflow-x-auto rounded-lg border border-[rgba(107,148,193,0.18)] bg-[#003257]">
        <Table>
          <TableHeader>
            <TableRow className="border-[rgba(107,148,193,0.18)] hover:bg-transparent">
              <TableHead className="text-[#85AFDD]">Timestamp</TableHead>
              <TableHead className="text-[#85AFDD]">Action</TableHead>
              <TableHead className="text-[#85AFDD]">Device</TableHead>
              <TableHead className="text-[#85AFDD]">Tenant</TableHead>
              <TableHead className="text-[#85AFDD]">Status</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading ? (
              <SkeletonRows count={10} />
            ) : data?.data.length === 0 ? (
              <TableRow className="border-[rgba(107,148,193,0.18)]">
                <TableCell
                  colSpan={5}
                  className="py-10 text-center text-[#85AFDD]"
                >
                  No audit logs found.
                </TableCell>
              </TableRow>
            ) : (
              data?.data.map((log: AuditLog) => (
                <TableRow
                  key={log.id}
                  className="border-[rgba(107,148,193,0.18)] hover:bg-[#1B4972]"
                >
                  <TableCell className="font-mono text-xs text-[#85AFDD] whitespace-nowrap">
                    {formatTimestamp(log.timestamp)}
                  </TableCell>
                  <TableCell className="text-[#A1CAFA] font-medium">
                    {log.action}
                  </TableCell>
                  <TableCell className="text-[#E9F1FF]">
                    {log.deviceName ?? log.deviceId ?? "—"}
                  </TableCell>
                  <TableCell className="text-[#E9F1FF]">
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

      {!isLoading && total > PAGE_LIMIT && (
        <div className="flex items-center justify-between text-sm text-[#85AFDD]">
          <span>
            Page {page} of {totalPages} &mdash; {total} total
          </span>
          <div className="flex items-center gap-2">
            <Button
              variant="ghost"
              size="icon"
              className="h-8 w-8 text-[#85AFDD] hover:bg-[#003257] hover:text-[#E9F1FF] disabled:opacity-40"
              onClick={() => setOffset((o) => Math.max(0, o - PAGE_LIMIT))}
              disabled={offset === 0}
              aria-label="Previous page"
            >
              <ChevronLeft className="h-4 w-4" />
            </Button>
            <Button
              variant="ghost"
              size="icon"
              className="h-8 w-8 text-[#85AFDD] hover:bg-[#003257] hover:text-[#E9F1FF] disabled:opacity-40"
              onClick={() => setOffset((o) => o + PAGE_LIMIT)}
              disabled={offset + PAGE_LIMIT >= total}
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
