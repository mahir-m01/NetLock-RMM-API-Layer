"use client"

import { usePathname } from "next/navigation"
import { SidebarTrigger } from "@/components/ui/sidebar"
import { Separator } from "@/components/ui/separator"

const ROUTE_TITLES: Record<string, string> = {
  "/dashboard": "Dashboard",
  "/devices": "Devices",
  "/events": "Events",
  "/tenants": "Tenants",
  "/audit": "Audit Logs",
  "/commands": "Commands",
  "/admin/users": "Users",
  "/admin/system": "System Health",
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

export function SiteHeader() {
  const pathname = usePathname()
  const title = usePageTitle(pathname)

  return (
    <header className="flex h-(--header-height) shrink-0 items-center gap-2 border-b border-border transition-[width,height] ease-linear group-has-data-[collapsible=icon]/sidebar-wrapper:h-(--header-height)">
      <div className="flex w-full items-center gap-1 px-4 lg:gap-2 lg:px-6">
        <SidebarTrigger className="-ml-1" />
        <Separator
          orientation="vertical"
          className="mx-2 data-[orientation=vertical]:h-4"
        />
        <h1 className="text-base font-medium text-foreground">{title}</h1>
      </div>
    </header>
  )
}
