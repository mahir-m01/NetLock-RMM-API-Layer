"use client";

import { useQuery } from "@tanstack/react-query";
import { getDashboard } from "@/lib/api";
import { Monitor, Wifi, Building2, Zap } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";

interface StatCardProps {
  title: string;
  value: number | undefined;
  icon: React.ElementType;
  loading: boolean;
}

function StatCard({ title, value, icon: Icon, loading }: StatCardProps) {
  return (
    <Card className="border-[rgba(107,148,193,0.18)] bg-[#003257]">
      <CardHeader className="flex flex-row items-center justify-between pb-2">
        <CardTitle className="text-sm font-medium text-[#85AFDD]">
          {title}
        </CardTitle>
        <Icon className="h-4 w-4 text-[#6B94C1]" aria-hidden="true" />
      </CardHeader>
      <CardContent>
        {loading ? (
          <Skeleton className="h-8 w-24 bg-[#1B4972]" />
        ) : (
          <p className="text-3xl font-bold text-[#A1CAFA]">
            {value?.toLocaleString() ?? "—"}
          </p>
        )}
      </CardContent>
    </Card>
  );
}

export default function DashboardPage() {
  const { data, isLoading, isError } = useQuery({
    queryKey: ["dashboard"],
    queryFn: getDashboard,
    refetchInterval: 30_000,
  });

  return (
    <div className="space-y-6">
      {isError && (
        <div className="rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-400">
          Failed to load dashboard data. Check your API key and connection.
        </div>
      )}

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <StatCard
          title="Total Devices"
          value={data?.totalDevices}
          icon={Monitor}
          loading={isLoading}
        />
        <StatCard
          title="Online Devices"
          value={data?.onlineDevices}
          icon={Wifi}
          loading={isLoading}
        />
        <StatCard
          title="Total Tenants"
          value={data?.totalTenants}
          icon={Building2}
          loading={isLoading}
        />
        <StatCard
          title="Total Events"
          value={data?.totalEvents}
          icon={Zap}
          loading={isLoading}
        />
      </div>
    </div>
  );
}
