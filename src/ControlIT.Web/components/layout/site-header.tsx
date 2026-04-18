"use client"

import { usePathname } from "next/navigation"
import { useQuery } from "@tanstack/react-query"
import { getHealth } from "@/lib/api"
import { SidebarTrigger } from "@/components/ui/sidebar"
import { Separator } from "@/components/ui/separator"
import { cn } from "@/lib/utils"

const ROUTE_TITLES: Record<string, string> = {
  "/dashboard": "Dashboard",
  "/devices": "Devices",
  "/events": "Events",
  "/tenants": "Tenants",
  "/audit": "Audit Logs",
  "/commands": "Commands",
  "/admin/users": "Users",
  "/account/change-password": "Change Password",
}

function usePageTitle(pathname: string): string {
  for (const [prefix, title] of Object.entries(ROUTE_TITLES)) {
    if (pathname === prefix || pathname.startsWith(prefix + "/")) {
      return title
    }
  }
  return "ControlIT"
}

function HealthDot() {
  const { data, isError } = useQuery({
    queryKey: ["health"],
    queryFn: getHealth,
    refetchInterval: 30_000,
    retry: false,
  })

  const healthy = !isError && data !== undefined

  return (
    <div className="flex items-center gap-2">
      <span
        className={cn(
          "h-2 w-2 rounded-full",
          healthy ? "bg-green-400" : "bg-red-500"
        )}
        aria-label={healthy ? "API online" : "API offline"}
      />
      <span className="hidden text-xs text-muted-foreground sm:block">
        {healthy ? "API Online" : "API Offline"}
      </span>
    </div>
  )
}

export function SiteHeader() {
  const pathname = usePathname()
  const title = usePageTitle(pathname)

  return (
    <header className="flex h-(--header-height) shrink-0 items-center gap-2 border-b transition-[width,height] ease-linear group-has-data-[collapsible=icon]/sidebar-wrapper:h-(--header-height)">
      <div className="flex w-full items-center gap-1 px-4 lg:gap-2 lg:px-6">
        <SidebarTrigger className="-ml-1" />
        <Separator
          orientation="vertical"
          className="mx-2 data-[orientation=vertical]:h-4"
        />
        <h1 className="text-base font-medium">{title}</h1>
        <div className="ml-auto flex items-center gap-4">
          <HealthDot />
        </div>
      </div>
    </header>
  )
}
