"use client"

import { useState } from "react"
import { Geist, Geist_Mono } from "next/font/google"
import "./globals.css"
import { Providers } from "@/components/providers"
import { AppSidebar } from "@/components/layout/app-sidebar"
import { SiteHeader } from "@/components/layout/site-header"
import { ApiKeyGate } from "@/components/shared/api-key-gate"
import { SidebarProvider, SidebarInset } from "@/components/ui/sidebar"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
} from "@/components/ui/dialog"
import { saveApiKey, readApiKey } from "@/lib/api"

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
})

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
})

function RootLayoutInner({ children }: { children: React.ReactNode }) {
  const [settingsOpen, setSettingsOpen] = useState(false)
  const [keyValue, setKeyValue] = useState("")

  function handleSettingsOpen() {
    setKeyValue(readApiKey())
    setSettingsOpen(true)
  }

  function handleSave() {
    const trimmed = keyValue.trim()
    if (trimmed) {
      saveApiKey(trimmed)
    }
    setSettingsOpen(false)
  }

  return (
    <>
      <SidebarProvider
        style={
          {
            "--sidebar-width": "calc(var(--spacing) * 72)",
            "--header-height": "calc(var(--spacing) * 12)",
          } as React.CSSProperties
        }
      >
        <AppSidebar variant="inset" onSettingsClick={handleSettingsOpen} />
        <SidebarInset>
          <SiteHeader onSettingsClick={handleSettingsOpen} />
          <div className="flex flex-1 flex-col">
            <div className="flex flex-1 flex-col gap-2 p-4">
              {children}
            </div>
          </div>
        </SidebarInset>
      </SidebarProvider>

      <Dialog open={settingsOpen} onOpenChange={setSettingsOpen}>
        <DialogContent className="bg-card border-border text-foreground">
          <DialogHeader>
            <DialogTitle>API Key</DialogTitle>
            <DialogDescription className="text-muted-foreground">
              Update the API key used for all requests. Stored in localStorage.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4 pt-2">
            <Input
              type="password"
              value={keyValue}
              onChange={(e) => setKeyValue(e.target.value)}
              placeholder="Enter API key"
              className="bg-input border-border text-foreground placeholder:text-muted-foreground"
              autoComplete="off"
            />
            <div className="flex justify-end gap-2">
              <Button
                variant="ghost"
                onClick={() => setSettingsOpen(false)}
                className="text-muted-foreground hover:text-foreground"
              >
                Cancel
              </Button>
              <Button onClick={handleSave}>Save</Button>
            </div>
          </div>
        </DialogContent>
      </Dialog>
    </>
  )
}

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode
}>) {
  return (
    <html
      lang="en"
      className={`${geistSans.variable} ${geistMono.variable} h-full antialiased dark`}
    >
      <body className="min-h-full bg-background text-foreground">
        <Providers>
          <ApiKeyGate>
            <RootLayoutInner>{children}</RootLayoutInner>
          </ApiKeyGate>
        </Providers>
      </body>
    </html>
  )
}
