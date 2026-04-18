"use client"

import { usePathname } from "next/navigation"
import { Geist, Geist_Mono } from "next/font/google"
import "./globals.css"
import { Providers } from "@/components/providers"
import { AppSidebar } from "@/components/layout/app-sidebar"
import { SiteHeader } from "@/components/layout/site-header"
import { AuthGate } from "@/components/shared/auth-gate"
import { SidebarProvider, SidebarInset } from "@/components/ui/sidebar"

const geistSans = Geist({
  variable: "--font-geist-sans",
  subsets: ["latin"],
})

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin"],
})

function AppShell({ children }: { children: React.ReactNode }) {
  const pathname = usePathname()
  const isLoginPage = pathname === "/login"

  if (isLoginPage) {
    return (
      <AuthGate>
        {children}
      </AuthGate>
    )
  }

  return (
    <AuthGate>
      <SidebarProvider
        style={
          {
            "--sidebar-width": "calc(var(--spacing) * 72)",
            "--header-height": "calc(var(--spacing) * 12)",
          } as React.CSSProperties
        }
        className="h-screen overflow-hidden"
      >
        <AppSidebar variant="inset" />
        <SidebarInset className="flex flex-col h-screen overflow-hidden">
          <SiteHeader />
          <div className="flex-1 overflow-y-auto">
            <div className="flex flex-col gap-2 p-4 pb-8">
              {children}
            </div>
          </div>
        </SidebarInset>
      </SidebarProvider>
    </AuthGate>
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
      <body className="h-screen overflow-hidden bg-background text-foreground">
        <Providers>
          <AppShell>{children}</AppShell>
        </Providers>
      </body>
    </html>
  )
}
