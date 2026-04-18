"use client";

import { useEffect } from "react";
import { usePathname, useRouter } from "next/navigation";
import { useAuth } from "@/components/providers/auth-provider";

interface AuthGateProps {
  children: React.ReactNode;
  /** If true, renders children unconditionally (used for public routes like /login) */
  public?: boolean;
}

export function AuthGate({ children, public: isPublic }: AuthGateProps) {
  const { user, isLoading } = useAuth();
  const router = useRouter();
  const pathname = usePathname();

  const isLoginPage = pathname === "/login";

  useEffect(() => {
    if (!isLoading && !user && !isLoginPage) {
      router.push("/login");
    }
  }, [user, isLoading, router, isLoginPage]);

  // Public routes (login page) — render unconditionally
  if (isPublic || isLoginPage) return <>{children}</>;

  // Protected routes — block until auth is confirmed
  if (isLoading || !user) return null;

  return <>{children}</>;
}
