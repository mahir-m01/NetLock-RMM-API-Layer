"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { getEvents } from "@/lib/api";
import type { DeviceEvent } from "@/lib/types";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { ChevronLeft, ChevronRight } from "lucide-react";

const PAGE_SIZE = 25;

function SkeletonRows({ count }: { count: number }) {
  return (
    <>
      {Array.from({ length: count }).map((_, i) => (
        <TableRow key={i} className="border-[rgba(107,148,193,0.18)]">
          {Array.from({ length: 4 }).map((_, j) => (
            <TableCell key={j}>
              <Skeleton className="h-4 w-full bg-[#1B4972]" />
            </TableCell>
          ))}
        </TableRow>
      ))}
    </>
  );
}

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

export default function EventsPage() {
  const [page, setPage] = useState(1);

  const { data, isLoading, isError } = useQuery({
    queryKey: ["events", page],
    queryFn: () => getEvents(page, PAGE_SIZE),
  });

  const totalPages = data ? Math.ceil(data.total / PAGE_SIZE) : 1;

  return (
    <div className="space-y-4">
      {isError && (
        <div className="rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-400">
          Failed to load events. Check your API key and connection.
        </div>
      )}

      <div className="overflow-x-auto rounded-lg border border-[rgba(107,148,193,0.18)] bg-[#003257]">
        <Table>
          <TableHeader>
            <TableRow className="border-[rgba(107,148,193,0.18)] hover:bg-transparent">
              <TableHead className="text-[#85AFDD]">Timestamp</TableHead>
              <TableHead className="text-[#85AFDD]">Event Type</TableHead>
              <TableHead className="text-[#85AFDD]">Device</TableHead>
              <TableHead className="text-[#85AFDD]">Description</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading ? (
              <SkeletonRows count={10} />
            ) : data?.data.length === 0 ? (
              <TableRow className="border-[rgba(107,148,193,0.18)]">
                <TableCell
                  colSpan={4}
                  className="py-10 text-center text-[#85AFDD]"
                >
                  No events found.
                </TableCell>
              </TableRow>
            ) : (
              data?.data.map((event: DeviceEvent) => (
                <TableRow
                  key={event.id}
                  className="border-[rgba(107,148,193,0.18)] hover:bg-[#1B4972]"
                >
                  <TableCell className="font-mono text-xs text-[#85AFDD] whitespace-nowrap">
                    {formatTimestamp(event.timestamp)}
                  </TableCell>
                  <TableCell className="text-[#A1CAFA] font-medium">
                    {event.eventType}
                  </TableCell>
                  <TableCell className="text-[#E9F1FF]">
                    {event.deviceName ?? event.deviceId ?? "—"}
                  </TableCell>
                  <TableCell className="text-[#85AFDD] max-w-sm truncate">
                    {event.description ?? "—"}
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </div>

      {!isLoading && data && data.total > PAGE_SIZE && (
        <div className="flex items-center justify-between text-sm text-[#85AFDD]">
          <span>
            Page {page} of {totalPages} &mdash; {data.total} total
          </span>
          <div className="flex items-center gap-2">
            <Button
              variant="ghost"
              size="icon"
              className="h-8 w-8 text-[#85AFDD] hover:bg-[#003257] hover:text-[#E9F1FF] disabled:opacity-40"
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              disabled={page <= 1}
              aria-label="Previous page"
            >
              <ChevronLeft className="h-4 w-4" />
            </Button>
            <Button
              variant="ghost"
              size="icon"
              className="h-8 w-8 text-[#85AFDD] hover:bg-[#003257] hover:text-[#E9F1FF] disabled:opacity-40"
              onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
              disabled={page >= totalPages}
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
