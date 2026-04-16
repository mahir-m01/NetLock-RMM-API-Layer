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

  const totalPages = data ? Math.ceil(data.total / PAGE_SIZE) : 1;

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
          className="max-w-xs border-[rgba(107,148,193,0.18)] bg-[#1B4972] text-[#E9F1FF] placeholder:text-[#85AFDD]"
        />
        <Select value={platform} onValueChange={handlePlatform}>
          <SelectTrigger className="w-40 border-[rgba(107,148,193,0.18)] bg-[#1B4972] text-[#E9F1FF]">
            <SelectValue />
          </SelectTrigger>
          <SelectContent className="border-[rgba(107,148,193,0.18)] bg-[#003257] text-[#E9F1FF]">
            {PLATFORMS.map((p) => (
              <SelectItem
                key={p}
                value={p}
                className="focus:bg-[#1B4972] focus:text-[#E9F1FF]"
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
      <div className="overflow-x-auto rounded-lg border border-[rgba(107,148,193,0.18)] bg-[#003257]">
        <Table>
          <TableHeader>
            <TableRow className="border-[rgba(107,148,193,0.18)] hover:bg-transparent">
              <TableHead className="text-[#85AFDD]">ID</TableHead>
              <TableHead className="text-[#85AFDD]">Device Name</TableHead>
              <TableHead className="text-[#85AFDD]">Platform</TableHead>
              <TableHead className="text-[#85AFDD]">Status</TableHead>
              <TableHead className="text-[#85AFDD]">Actions</TableHead>
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
                  No devices found.
                </TableCell>
              </TableRow>
            ) : (
              data?.data.map((device: Device) => (
                <TableRow
                  key={device.id}
                  className="cursor-pointer border-[rgba(107,148,193,0.18)] hover:bg-[#1B4972]"
                  onClick={() => router.push(`/devices/${device.id}`)}
                >
                  <TableCell className="font-mono text-xs text-[#85AFDD]">
                    {device.id}
                  </TableCell>
                  <TableCell className="font-medium text-[#E9F1FF]">
                    {device.deviceName}
                  </TableCell>
                  <TableCell className="text-[#85AFDD]">
                    {device.platform}
                  </TableCell>
                  <TableCell>
                    <StatusBadge status={device.status} />
                  </TableCell>
                  <TableCell>
                    <Button
                      variant="ghost"
                      size="icon"
                      className="h-7 w-7 text-[#6B94C1] hover:bg-[#003257] hover:text-[#A1CAFA]"
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
