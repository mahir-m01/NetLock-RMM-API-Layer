"use client";

import { useQuery } from "@tanstack/react-query";
import { getTenants } from "@/lib/api";
import type { Tenant } from "@/lib/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Building2 } from "lucide-react";

function TenantCard({ tenant }: { tenant: Tenant }) {
  return (
    <Card className="border-border bg-card">
      <CardHeader className="pb-2 flex flex-row items-center gap-3">
        <Building2 className="h-5 w-5 text-muted-foreground shrink-0" />
        <CardTitle className="text-base font-semibold text-foreground leading-tight">
          {tenant.name}
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-1">
        <p className="font-mono text-xs text-muted-foreground">
          ID: {tenant.id}
        </p>
        {(tenant as unknown as Record<string, unknown>)["deviceCount"] !== undefined && (
          <p className="text-xs text-muted-foreground">
            Devices:{" "}
            <span className="text-foreground font-medium">
              {(tenant as unknown as Record<string, unknown>)["deviceCount"] as number}
            </span>
          </p>
        )}
        {Boolean((tenant as unknown as Record<string, unknown>)["createdAt"]) && (
          <p className="text-xs text-muted-foreground">
            Created:{" "}
            <span className="text-foreground">
              {new Date((tenant as unknown as Record<string, unknown>)["createdAt"] as string).toLocaleDateString("en-GB", {
                dateStyle: "medium",
              })}
            </span>
          </p>
        )}
      </CardContent>
    </Card>
  );
}

function SkeletonCards({ count }: { count: number }) {
  return (
    <>
      {Array.from({ length: count }).map((_, i) => (
        <Card key={i} className="border-border bg-card">
          <CardHeader className="pb-2">
            <Skeleton className="h-5 w-32 bg-muted" />
          </CardHeader>
          <CardContent className="space-y-2">
            <Skeleton className="h-3 w-48 bg-muted" />
            <Skeleton className="h-3 w-24 bg-muted" />
          </CardContent>
        </Card>
      ))}
    </>
  );
}

export default function TenantsPage() {
  const { data, isLoading, isError } = useQuery({
    queryKey: ["tenants"],
    queryFn: getTenants,
  });

  return (
    <div className="space-y-4">
      {isError && (
        <div className="rounded-lg border border-red-500/30 bg-red-500/10 px-4 py-3 text-sm text-red-400">
          Failed to load tenants. Check your API key and connection.
        </div>
      )}

      {!isLoading && !isError && data?.length === 0 && (
        <div className="rounded-lg border border-border bg-card px-4 py-10 text-center text-sm text-muted-foreground">
          No tenants found.
        </div>
      )}

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {isLoading ? (
          <SkeletonCards count={6} />
        ) : (
          data?.map((tenant: Tenant) => (
            <TenantCard key={tenant.id} tenant={tenant} />
          ))
        )}
      </div>
    </div>
  );
}
