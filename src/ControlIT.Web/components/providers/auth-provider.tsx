"use client";

import React, { createContext, useCallback, useContext, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import {
  loginUser,
  logoutUser,
  refreshTokenFromCookie,
  registerUnauthenticatedCallback,
} from "@/lib/api";
import type { AuthUser, LoginRequest } from "@/lib/types";

interface AuthContextValue {
  user: AuthUser | null;
  isLoading: boolean;
  login: (req: LoginRequest) => Promise<AuthUser>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const router = useRouter();

  const handleUnauthenticated = useCallback(() => {
    setUser(null);
    router.push("/login");
  }, [router]);

  useEffect(() => {
    registerUnauthenticatedCallback(handleUnauthenticated);
  }, [handleUnauthenticated]);

  // On mount: try to restore session from httpOnly refresh cookie
  useEffect(() => {
    refreshTokenFromCookie()
      .then((data) => {
        if (data) setUser(data.user);
      })
      .finally(() => setIsLoading(false));
  }, []);

  const login = useCallback(async (req: LoginRequest): Promise<AuthUser> => {
    const data = await loginUser(req);
    setUser(data.user);
    return data.user;
  }, []);

  const logout = useCallback(async () => {
    await logoutUser();
    setUser(null);
    router.push("/login");
  }, [router]);

  return (
    <AuthContext.Provider value={{ user, isLoading, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth must be used within AuthProvider");
  return ctx;
}
