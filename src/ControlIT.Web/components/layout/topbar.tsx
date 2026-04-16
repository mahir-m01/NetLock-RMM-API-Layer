"use client";

import { useEffect, useState } from "react";
import { usePathname } from "next/navigation";
import { Settings } from "lucide-react";
import { useQuery } from "@tanstack/react-query";
import { getHealth, saveApiKey, readApiKey } from "@/lib/api";
import { SidebarTrigger } from "@/components/ui/sidebar";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
} from "@/components/ui/dialog";
import { cn } from "@/lib/utils";

const ROUTE_TITLES: Record<string, string> = {
  "/dashboard": "Dashboard",
  "/devices": "Devices",
  "/events": "Events",
  "/tenants": "Tenants",
  "/audit": "Audit Logs",
  "/commands": "Commands",
};

function usePageTitle(pathname: string): string {
  for (const [prefix, title] of Object.entries(ROUTE_TITLES)) {
    if (pathname === prefix || pathname.startsWith(prefix + "/")) {
      return title;
    }
  }
  return "ControlIT";
}

function HealthDot() {
  const { data, isError } = useQuery({
    queryKey: ["health"],
    queryFn: getHealth,
    refetchInterval: 30_000,
    retry: false,
  });

  const healthy = !isError && data !== undefined;

  return (
    <div className="flex items-center gap-2">
      <span
        className={cn(
          "h-2 w-2 rounded-full",
          healthy ? "bg-green-400" : "bg-red-500"
        )}
        aria-label={healthy ? "API online" : "API offline"}
      />
      <span className="hidden text-xs text-[#85AFDD] sm:block">
        {healthy ? "API Online" : "API Offline"}
      </span>
    </div>
  );
}

export function Topbar() {
  const pathname = usePathname();
  const title = usePageTitle(pathname);
  const [open, setOpen] = useState(false);
  const [keyValue, setKeyValue] = useState("");

  useEffect(() => {
    if (open) {
      setKeyValue(readApiKey());
    }
  }, [open]);

  function handleSave() {
    const trimmed = keyValue.trim();
    if (trimmed) {
      saveApiKey(trimmed);
    }
    setOpen(false);
  }

  return (
    <header className="flex h-14 items-center justify-between border-b border-[rgba(107,148,193,0.18)] bg-[#001D35] px-4">
      <div className="flex items-center gap-3">
        <SidebarTrigger className="h-8 w-8 text-[#85AFDD] hover:bg-[#003257] hover:text-[#E9F1FF]" />
        <h1 className="text-sm font-semibold text-[#E9F1FF]">{title}</h1>
      </div>

      <div className="flex items-center gap-4">
        <HealthDot />
        <Button
          variant="ghost"
          size="icon"
          className="h-8 w-8 text-[#85AFDD] hover:bg-[#003257] hover:text-[#E9F1FF]"
          onClick={() => setOpen(true)}
          aria-label="API key settings"
        >
          <Settings className="h-4 w-4" />
        </Button>
      </div>

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="border-[rgba(107,148,193,0.18)] bg-[#003257] text-[#E9F1FF]">
          <DialogHeader>
            <DialogTitle className="text-[#E9F1FF]">API Key</DialogTitle>
            <DialogDescription className="text-[#85AFDD]">
              Update the API key used for all requests. Stored in localStorage.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4 pt-2">
            <Input
              type="password"
              value={keyValue}
              onChange={(e) => setKeyValue(e.target.value)}
              placeholder="Enter API key"
              className="border-[rgba(107,148,193,0.18)] bg-[#1B4972] text-[#E9F1FF] placeholder:text-[#85AFDD]"
              autoComplete="off"
            />
            <div className="flex justify-end gap-2">
              <Button
                variant="ghost"
                onClick={() => setOpen(false)}
                className="text-[#85AFDD] hover:bg-[#1B4972] hover:text-[#E9F1FF]"
              >
                Cancel
              </Button>
              <Button
                onClick={handleSave}
                className="bg-[#A1CAFA] text-[#001D35] font-semibold hover:bg-[#D0E4FF]"
              >
                Save
              </Button>
            </div>
          </div>
        </DialogContent>
      </Dialog>
    </header>
  );
}
