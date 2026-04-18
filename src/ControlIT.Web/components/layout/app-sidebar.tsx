"use client"

import * as React from "react"
import Link from "next/link"
import { usePathname } from "next/navigation"
import {
  LayoutDashboard,
  Monitor,
  Activity,
  Building2,
  ClipboardList,
  Terminal,
  Users,
  LogOut,
  KeyRound,
  Server,
} from "lucide-react"
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarGroup,
  SidebarGroupContent,
} from "@/components/ui/sidebar"
import { Button } from "@/components/ui/button"
import { useAuth } from "@/components/providers/auth-provider"

const BASE_NAV_ITEMS = [
  { title: "Dashboard", url: "/dashboard", icon: LayoutDashboard },
  { title: "Devices", url: "/devices", icon: Monitor },
  { title: "Events", url: "/events", icon: Activity },
  { title: "Tenants", url: "/tenants", icon: Building2 },
  { title: "Audit Logs", url: "/audit", icon: ClipboardList },
  { title: "Commands", url: "/commands", icon: Terminal },
]

const ADMIN_NAV_ITEM = { title: "Users", url: "/admin/users", icon: Users }
const SUPERADMIN_NAV_ITEM = { title: "System Health", url: "/admin/system", icon: Server }

function ResizeHandle() {
  const handleMouseDown = (e: React.MouseEvent) => {
    e.preventDefault();
    const startX = e.clientX;
    const startWidth = parseInt(
      getComputedStyle(document.documentElement)
        .getPropertyValue("--sidebar-width") || "288"
    );

    const onMouseMove = (e: MouseEvent) => {
      const newWidth = Math.min(400, Math.max(180, startWidth + e.clientX - startX));
      document.documentElement.style.setProperty(
        "--sidebar-width", `${newWidth}px`
      );
    };

    const onMouseUp = () => {
      document.removeEventListener("mousemove", onMouseMove);
      document.removeEventListener("mouseup", onMouseUp);
    };

    document.addEventListener("mousemove", onMouseMove);
    document.addEventListener("mouseup", onMouseUp);
  };

  return (
    <div
      onMouseDown={handleMouseDown}
      className="absolute right-0 top-0 h-full w-1 cursor-col-resize hover:bg-sidebar-primary/50 transition-colors"
      aria-hidden="true"
    />
  );
}

export function AppSidebar(props: React.ComponentProps<typeof Sidebar>) {
  const pathname = usePathname()
  const { user, logout } = useAuth()

  const isAdminRole = user?.role === "SuperAdmin" || user?.role === "CpAdmin"
  const isSuperAdmin = user?.role === "SuperAdmin"
  const navItems = [
    ...BASE_NAV_ITEMS,
    ...(isAdminRole ? [ADMIN_NAV_ITEM] : []),
    ...(isSuperAdmin ? [SUPERADMIN_NAV_ITEM] : []),
  ]

  return (
    <Sidebar collapsible="offcanvas" {...props} style={{ position: "relative" }}>
      <SidebarHeader>
        <SidebarMenu>
          <SidebarMenuItem>
            <SidebarMenuButton
              asChild
              className="data-[slot=sidebar-menu-button]:!p-1.5"
            >
              <Link href="/dashboard">
                <span className="text-base font-semibold">ControlIT</span>
              </Link>
            </SidebarMenuButton>
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarHeader>

      <SidebarContent>
        <SidebarGroup>
          <SidebarGroupContent>
            <SidebarMenu>
              {navItems.map((item) => {
                const isActive =
                  pathname === item.url || pathname.startsWith(item.url + "/")
                return (
                  <SidebarMenuItem key={item.title}>
                    <SidebarMenuButton asChild isActive={isActive}>
                      <Link href={item.url}>
                        <item.icon />
                        <span>{item.title}</span>
                      </Link>
                    </SidebarMenuButton>
                  </SidebarMenuItem>
                )
              })}
            </SidebarMenu>
          </SidebarGroupContent>
        </SidebarGroup>
      </SidebarContent>

      <SidebarFooter>
        <div className="px-2 py-2 space-y-1">
          {user && (
            <p className="truncate text-xs text-muted-foreground px-2 pb-1" title={user.email}>
              {user.email}
            </p>
          )}
          <SidebarMenu>
            <SidebarMenuItem>
              <SidebarMenuButton asChild>
                <Link href="/account/change-password">
                  <KeyRound />
                  <span>Change Password</span>
                </Link>
              </SidebarMenuButton>
            </SidebarMenuItem>
            <SidebarMenuItem>
              <SidebarMenuButton
                onClick={() => void logout()}
                className="text-destructive hover:text-destructive"
              >
                <LogOut />
                <span>Log out</span>
              </SidebarMenuButton>
            </SidebarMenuItem>
          </SidebarMenu>
        </div>
      </SidebarFooter>
      <ResizeHandle />
    </Sidebar>
  )
}
