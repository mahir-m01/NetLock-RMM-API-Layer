"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { getDevices } from "@/lib/api";
import type { Device, DeviceFilters } from "@/lib/types";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { ChevronLeft, ChevronRight, ExternalLink } from "lucide-react";

const PAGE_SIZE = 20;

const PLATFORMS = ["All", "Windows", "Linux", "macOS"];


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

export default function DevicesPage() {
  const router = useRouter();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [platform, setPlatform] = useState("All");

  const filters: DeviceFilters = {
    search: search || undefined,
    platform: platform !== "All" ? platform : undefined,
  };

  const { data, isLoading, isError } = useQuery({
    queryKey: ["devices", page, filters],
    queryFn: () => getDevices(page, PAGE_SIZE, filters),
  });

  const totalPages = data?.totalPages ?? (data ? Math.ceil(data.totalCount / PAGE_SIZE) : 1);

  function handleSearch(e: React.ChangeEvent<HTMLInputElement>) {
    setSearch(e.target.value);
    setPage(1);
  }

  function handlePlatform(val: string) {
    setPlatform(val);
    setPage(1);
  }

  return (
    <div className="space-y-4">
      {/* Filters */}
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
        <Input
          placeholder="Search by name..."
          value={search}
          onChange={handleSearch}
          className="max-w-xs border-border bg-muted text-foreground placeholder:text-muted-foreground"
        />
        <Select value={platform} onValueChange={handlePlatform}>
          <SelectTrigger className="w-40 border-border bg-muted text-foreground">
            <SelectValue />
          </SelectTrigger>
          <SelectContent className="border-border bg-card text-foreground">
            {PLATFORMS.map((p) => (
              <SelectItem
                key={p}
                value={p}
                className="focus:bg-muted focus:text-foreground"
              >
                {p}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {isError && (
        <div className="rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-400">
          Failed to load devices. Check your API key and connection.
        </div>
      )}

      {/* Table */}
      <div className="overflow-x-auto rounded-lg border border-border bg-card">
        <Table>
          <TableHeader>
            <TableRow className="border-border hover:bg-transparent">
              <TableHead className="text-muted-foreground">ID</TableHead>
              <TableHead className="text-muted-foreground">Device Name</TableHead>
              <TableHead className="text-muted-foreground">Platform</TableHead>
              <TableHead className="text-muted-foreground">Status</TableHead>
              <TableHead className="text-muted-foreground">Actions</TableHead>
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
                  No devices found.
                </TableCell>
              </TableRow>
            ) : (
              data?.items.map((device: Device) => (
                <TableRow
                  key={device.id}
                  className="cursor-pointer border-border hover:bg-muted"
                  onClick={() => router.push(`/devices/${device.id}`)}
                >
                  <TableCell className="font-mono text-xs text-muted-foreground">
                    {device.id}
                  </TableCell>
                  <TableCell className="font-medium text-foreground">
                    {device.deviceName}
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {device.platform}
                  </TableCell>
                  <TableCell>
                    <Badge
                      variant={device.isOnline ? "default" : "secondary"}
                      className={device.isOnline
                        ? "bg-green-500/20 text-green-400 border-green-500/30"
                        : "bg-red-500/20 text-red-400 border-red-500/30"}
                    >
                      {device.isOnline ? "Online" : "Offline"}
                    </Badge>
                  </TableCell>
                  <TableCell>
                    <Button
                      variant="ghost"
                      size="icon"
                      className="h-7 w-7 text-muted-foreground hover:bg-card hover:text-foreground"
                      onClick={(e) => {
                        e.stopPropagation();
                        router.push(`/devices/${device.id}`);
                      }}
                      aria-label={`View ${device.deviceName}`}
                    >
                      <ExternalLink className="h-3.5 w-3.5" />
                    </Button>
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </div>

      {/* Pagination */}
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
