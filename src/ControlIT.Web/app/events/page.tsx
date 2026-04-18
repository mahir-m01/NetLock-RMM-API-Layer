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

  const totalPages = data?.totalPages ?? 1;

  return (
    <div className="space-y-4">
      {isError && (
        <div className="rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-400">
          Failed to load events. Check your API key and connection.
        </div>
      )}

      <div className="overflow-x-auto rounded-lg border border-border bg-card">
        <Table>
          <TableHeader>
            <TableRow className="border-border hover:bg-transparent">
              <TableHead className="text-muted-foreground">Timestamp</TableHead>
              <TableHead className="text-muted-foreground">Event</TableHead>
              <TableHead className="text-muted-foreground">Severity</TableHead>
              <TableHead className="text-muted-foreground">Device</TableHead>
              <TableHead className="text-muted-foreground">Description</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading ? (
              <SkeletonRows count={10} />
            ) : data?.items.length === 0 ? (
              <TableRow className="border-border">
                <TableCell
                  colSpan={5}
                  className="py-10 text-center text-muted-foreground"
                >
                  No events found.
                </TableCell>
              </TableRow>
            ) : (
              data?.items.map((event: DeviceEvent) => (
                <TableRow
                  key={event.id}
                  className="border-border hover:bg-muted"
                >
                  <TableCell className="font-mono text-xs text-muted-foreground whitespace-nowrap">
                    {formatTimestamp(event.timestamp)}
                  </TableCell>
                  <TableCell className="text-foreground font-medium">
                    {event.event || "—"}
                  </TableCell>
                  <TableCell className="text-muted-foreground text-xs">
                    {event.severity || "—"}
                  </TableCell>
                  <TableCell className="text-foreground">
                    {event.deviceName ?? "—"}
                  </TableCell>
                  <TableCell className="text-muted-foreground max-w-sm truncate">
                    {event.description ?? "—"}
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </div>

      {!isLoading && data && data.totalCount > PAGE_SIZE && (
        <div className="flex items-center justify-between text-sm text-muted-foreground">
          <span>
            Page {page} of {totalPages} &mdash; {data.totalCount} total
          </span>
          <div className="flex items-center gap-2">
            <Button
              variant="ghost"
              size="icon"
              className="h-8 w-8 text-muted-foreground hover:bg-card hover:text-foreground disabled:opacity-40"
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              disabled={page <= 1}
              aria-label="Previous page"
            >
              <ChevronLeft className="h-4 w-4" />
            </Button>
            <Button
              variant="ghost"
              size="icon"
              className="h-8 w-8 text-muted-foreground hover:bg-card hover:text-foreground disabled:opacity-40"
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
