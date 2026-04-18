// Access token lives only in memory — never localStorage or sessionStorage.
// The refresh_token httpOnly cookie is managed by the browser automatically.
let _accessToken: string | null = null;

export function setAccessToken(token: string | null): void {
  _accessToken = token;
}

export function getAccessToken(): string | null {
  return _accessToken;
}

export function clearTokens(): void {
  _accessToken = null;
}

// Decode the JWT payload without verifying signature (trust the server already verified it).
export function decodeToken(token: string): Record<string, unknown> | null {
  try {
    const parts = token.split(".");
    if (parts.length !== 3) return null;
    const payload = atob(parts[1].replace(/-/g, "+").replace(/_/g, "/"));
    return JSON.parse(payload);
  } catch {
    return null;
  }
}

export function isTokenExpired(token: string): boolean {
  const payload = decodeToken(token);
  if (!payload || typeof payload["exp"] !== "number") return true;
  // 30s buffer matches API ClockSkew
  return Date.now() / 1000 > (payload["exp"] as number) - 30;
}
