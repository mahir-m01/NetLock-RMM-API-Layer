"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  LayoutDashboard,
  Monitor,
  Zap,
  Building2,
  ScrollText,
  Terminal,
} from "lucide-react";
import {
  Sidebar,
  SidebarContent,
  SidebarGroup,
  SidebarGroupContent,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarSeparator,
} from "@/components/ui/sidebar";
import { cn } from "@/lib/utils";

const NAV_ITEMS = [
  { label: "Dashboard", href: "/dashboard", icon: LayoutDashboard },
  { label: "Devices", href: "/devices", icon: Monitor },
  { label: "Events", href: "/events", icon: Zap },
  { label: "Tenants", href: "/tenants", icon: Building2 },
  { label: "Audit Logs", href: "/audit", icon: ScrollText },
  { label: "Commands", href: "/commands", icon: Terminal },
];

export function AppSidebar() {
  const pathname = usePathname();

  return (
    <Sidebar className="border-r border-[rgba(107,148,193,0.18)] bg-[#001D35]">
      <SidebarHeader className="px-5 py-4">
        <span className="text-lg font-bold tracking-tight text-[#A1CAFA]">
          ControlIT
        </span>
        <span className="text-xs text-[#85AFDD]">by Computer Port</span>
      </SidebarHeader>
      <SidebarSeparator className="bg-[rgba(107,148,193,0.18)]" />
      <SidebarContent className="pt-2">
        <SidebarGroup>
          <SidebarGroupContent>
            <SidebarMenu>
              {NAV_ITEMS.map(({ label, href, icon: Icon }) => {
                const active =
                  href === "/dashboard"
                    ? pathname === "/dashboard"
                    : pathname.startsWith(href);
                return (
                  <SidebarMenuItem key={href}>
                    <SidebarMenuButton
                      asChild
                      isActive={active}
                      className={cn(
                        "gap-3 rounded-lg px-3 py-2 text-sm transition-colors",
                        active
                          ? "bg-[#003257] text-[#A1CAFA] font-medium"
                          : "text-[#85AFDD] hover:bg-[#003257] hover:text-[#E9F1FF]"
                      )}
                    >
                      <Link href={href}>
                        <Icon className="h-4 w-4 shrink-0" />
                        <span>{label}</span>
                      </Link>
                    </SidebarMenuButton>
                  </SidebarMenuItem>
                );
              })}
            </SidebarMenu>
          </SidebarGroupContent>
        </SidebarGroup>
      </SidebarContent>
    </Sidebar>
  );
}
