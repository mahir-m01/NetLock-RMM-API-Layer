"use client";

// Deprecated — replaced by AuthGate (JWT-based auth). Kept as an empty re-export
// to avoid breaking any residual imports during the migration.
export function ApiKeyGate({ children }: { children: React.ReactNode }) {
  return <>{children}</>;
}
