import {
  applyDashboardEvent,
  normalizePushEvent,
  parseSseData,
  type DashboardLiveState,
} from "@/lib/dashboard-events";

describe("dashboard push events", () => {
  it("parses typed SSE frames", () => {
    const events = parseSseData(
      'data: {"version":1,"type":"system.health.updated","emittedAt":"2026-05-03T00:00:00Z","payload":{"dashboard":{"totalDevices":1}}}\n\n'
    );

    expect(events).toHaveLength(1);
    expect(normalizePushEvent(events[0])?.type).toBe("system.health.updated");
  });

  it("supports current legacy sync summary without polling", () => {
    const event = normalizePushEvent({
      syncedAt: "2026-05-03T00:00:00Z",
      onlineDevices: 1,
      totalDevices: 2,
    });

    expect(event?.type).toBe("system.health.updated");
    const state = applyDashboardEvent({}, event!);
    expect(state.stats?.onlineDevices).toBe(1);
    expect(state.stats?.totalDevices).toBe(2);
  });

  it("redacts secret fields from typed payloads", () => {
    const event = normalizePushEvent({
      version: 1,
      type: "device.updated",
      emittedAt: "2026-05-03T00:00:00Z",
      payload: {
        device: {
          id: 7,
          tenantId: 1,
          deviceName: "debian",
          accessKey: "secret-value",
          setupKey: "secret-value",
        },
      },
    });

    expect(JSON.stringify(event)).not.toContain("secret-value");
  });

  it("updates known device status from online events", () => {
    const initial: DashboardLiveState = {
      stats: { totalDevices: 1, onlineDevices: 0 },
      devicesData: {
        items: [
          {
            id: 7,
            tenantId: 1,
            deviceName: "debian",
            platform: "linux",
            operatingSystem: "debian",
            ipAddressInternal: "",
            ipAddressExternal: "",
            agentVersion: "",
            cpu: "",
            ram: "",
            cpuUsage: null,
            ramUsage: null,
            isOnline: false,
            lastAccess: "2026-05-03T00:00:00Z",
          },
        ],
        totalCount: 1,
        page: 1,
        pageSize: 10,
        totalPages: 1,
      },
    };

    const event = normalizePushEvent({
      version: 1,
      type: "device.online",
      emittedAt: "2026-05-03T00:01:00Z",
      payload: { deviceId: 7 },
    });

    const next = applyDashboardEvent(initial, event!);
    expect(next.devicesData?.items[0].isOnline).toBe(true);
    expect(next.stats?.onlineDevices).toBe(1);
  });

  it("can ignore tenant-scoped dashboard counts for elevated all-tenant views", () => {
    const initial: DashboardLiveState = {
      stats: { totalDevices: 10, onlineDevices: 7 },
    };

    const event = normalizePushEvent({
      version: 1,
      type: "device.online",
      emittedAt: "2026-05-03T00:01:00Z",
      tenantId: 3,
      payload: {
        dashboard: { totalDevices: 1, onlineDevices: 1 },
        deviceId: 7,
      },
    });

    const next = applyDashboardEvent(initial, event!, undefined, false);
    expect(next.stats?.totalDevices).toBe(10);
    expect(next.stats?.onlineDevices).toBe(7);
  });
});
