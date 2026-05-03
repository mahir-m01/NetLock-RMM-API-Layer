"use client";

import { useEffect, useRef, useState } from "react";
import { API_BASE_URL, refreshTokenFromCookie } from "@/lib/api";
import { getAccessToken } from "@/lib/auth";
import {
  applyDashboardEvent,
  normalizePushEvent,
  parseSseData,
  type DashboardLiveState,
} from "@/lib/dashboard-events";

export type StreamStatus = "connected" | "reconnecting" | "offline";

interface DashboardStreamResult {
  liveState: DashboardLiveState;
  streamStatus: StreamStatus;
  streamError: string | null;
}

const INITIAL_BACKOFF_MS = 1_000;
const MAX_BACKOFF_MS = 30_000;
const OFFLINE_AFTER_ATTEMPTS = 5;

function getBackoffMs(attempt: number): number {
  return Math.min(MAX_BACKOFF_MS, INITIAL_BACKOFF_MS * 2 ** Math.max(0, attempt - 1));
}

export function useDashboardStream(
  enabled: boolean,
  selectedTenantId?: number,
  acceptTenantScopedStats = true
): DashboardStreamResult {
  const [liveState, setLiveState] = useState<DashboardLiveState>({});
  const [streamStatus, setStreamStatus] = useState<StreamStatus>("offline");
  const [streamError, setStreamError] = useState<string | null>(null);
  const selectedTenantIdRef = useRef<number | undefined>(selectedTenantId);

  useEffect(() => {
    selectedTenantIdRef.current = selectedTenantId;
  }, [selectedTenantId]);

  useEffect(() => {
    if (!enabled) return;

    let cancelled = false;
    let retryTimer: ReturnType<typeof setTimeout> | null = null;
    let controller: AbortController | null = null;

    const connect = async (attempt: number): Promise<void> => {
      controller?.abort();
      controller = new AbortController();
      setStreamStatus(attempt >= OFFLINE_AFTER_ATTEMPTS ? "offline" : "reconnecting");

      try {
        const token = getAccessToken();
        if (!token) throw new Error("No access token available for live stream.");

        const response = await fetch(`${API_BASE_URL}/sync/stream`, {
          method: "GET",
          credentials: "include",
          headers: {
            Accept: "text/event-stream",
            Authorization: `Bearer ${token}`,
          },
          signal: controller.signal,
        });

        if (response.status === 401) {
          const refreshed = await refreshTokenFromCookie();
          if (refreshed && !cancelled) return connect(attempt + 1);
        }

        if (!response.ok || !response.body) {
          throw new Error(`Live stream failed with HTTP ${response.status}.`);
        }

        setStreamStatus("connected");
        setStreamError(null);

        const reader = response.body.getReader();
        const decoder = new TextDecoder();
        let buffer = "";

        while (!cancelled) {
          const { value, done } = await reader.read();
          if (done) break;

          buffer += decoder.decode(value, { stream: true });
          const frames = buffer.split(/\n\n+/);
          buffer = frames.pop() ?? "";

          for (const frame of frames) {
            for (const rawEvent of parseSseData(`${frame}\n\n`)) {
              const event = normalizePushEvent(rawEvent);
              if (!event) continue;

              setLiveState((current) =>
                applyDashboardEvent(current, event, selectedTenantIdRef.current, acceptTenantScopedStats)
              );
            }
          }
        }

        throw new Error("Live stream disconnected.");
      } catch (error) {
        if (cancelled || (error instanceof DOMException && error.name === "AbortError")) return;

        setStreamError(error instanceof Error ? error.message : "Live stream unavailable.");
        setStreamStatus(attempt >= OFFLINE_AFTER_ATTEMPTS ? "offline" : "reconnecting");
        retryTimer = setTimeout(() => {
          void connect(attempt + 1);
        }, getBackoffMs(attempt + 1));
      }
    };

    void connect(0);

    return () => {
      cancelled = true;
      controller?.abort();
      if (retryTimer) clearTimeout(retryTimer);
    };
  }, [enabled, acceptTenantScopedStats]);

  return { liveState, streamStatus, streamError };
}
