import React from "react";
import { render, screen } from "@testing-library/react";
import "@testing-library/jest-dom";
import { useQuery } from "@tanstack/react-query";
import DashboardPage from "@/app/dashboard/page";
import { useDashboardStream } from "@/app/dashboard/use-dashboard-stream";

let mockUser: { role: string; tenantId?: number | null } | null = { role: "SuperAdmin", tenantId: null };

jest.mock("next/navigation", () => ({
  useRouter: () => ({ push: jest.fn() }),
  usePathname: () => "/dashboard",
}));

jest.mock("@/components/providers/auth-provider", () => ({
  useAuth: () => ({ user: mockUser, isLoading: false, login: jest.fn(), logout: jest.fn() }),
}));

jest.mock("@tanstack/react-query", () => ({
  useQuery: jest.fn(),
  useMutation: jest.fn(),
  QueryClient: jest.fn(),
  QueryClientProvider: ({ children }: { children: React.ReactNode }) => children,
}));

jest.mock("@/app/dashboard/use-dashboard-stream", () => ({
  useDashboardStream: jest.fn(),
}));

const mockUseQuery = useQuery as jest.Mock;
const mockUseDashboardStream = useDashboardStream as jest.Mock;

describe("DashboardPage", () => {
  beforeEach(() => {
    mockUser = { role: "SuperAdmin", tenantId: null };
    mockUseDashboardStream.mockReturnValue({
      liveState: {},
      streamStatus: "reconnecting",
      streamError: null,
    });
    mockUseQuery.mockReturnValue({ data: undefined, isLoading: false, isError: false });
  });

  it("renders skeleton cards when loading", () => {
    render(<DashboardPage />);
    // Skeleton elements render via aria role or data-slot
    const skeletons = document.querySelectorAll("[data-slot='skeleton']");
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it("renders all 4 stat values when data is loaded", () => {
    mockUseDashboardStream.mockReturnValue({
      liveState: {
        stats: { totalDevices: 10, onlineDevices: 7, totalTenants: 4, totalEvents: 200 },
        devicesData: { items: [], totalCount: 0, page: 1, pageSize: 10, totalPages: 0 },
      },
      streamStatus: "connected",
      streamError: null,
    });
    render(<DashboardPage />);
    expect(screen.getByText("10")).toBeInTheDocument();
    expect(screen.getByText("7")).toBeInTheDocument();
    expect(screen.getByText("4")).toBeInTheDocument();
    expect(screen.getByText("200")).toBeInTheDocument();
  });

  it("renders stale stream warning when push stream is down", () => {
    mockUseDashboardStream.mockReturnValue({
      liveState: {},
      streamStatus: "offline",
      streamError: "Live stream failed with HTTP 503.",
    });
    render(<DashboardPage />);
    expect(
      screen.getByText(/Dashboard may show stale data/i)
    ).toBeInTheDocument();
  });

  it("renders queried tenant network summary instead of an infinite spinner", () => {
    mockUser = { role: "ClientAdmin", tenantId: 3 };
    mockUseDashboardStream.mockReturnValue({
      liveState: {
        stats: { totalDevices: 1, onlineDevices: 1, totalTenants: 1, totalEvents: 0, criticalAlerts: 0 },
        devicesData: { items: [], totalCount: 0, page: 1, pageSize: 10, totalPages: 0 },
      },
      streamStatus: "reconnecting",
      streamError: null,
    });
    mockUseQuery.mockImplementation(({ queryKey }) => {
      if (queryKey[0] === "network-summary") {
        return {
          data: {
            totalPeers: 2,
            connectedPeers: 2,
            tenantPeers: 1,
            tenantConnectedPeers: 1,
            setupKeysActive: 1,
            routeCount: 0,
          },
          isLoading: false,
          isError: false,
        };
      }
      return { data: undefined, isLoading: false, isError: false };
    });

    render(<DashboardPage />);

    expect(screen.getByText("1/1")).toBeInTheDocument();
    expect(screen.queryByText(/Select tenant to view network summary/i)).not.toBeInTheDocument();
  });

  it("enriches recent devices with NetBird IP from device query", () => {
    mockUser = { role: "ClientAdmin", tenantId: 3 };
    mockUseDashboardStream.mockReturnValue({
      liveState: {
        stats: { totalDevices: 1, onlineDevices: 1, totalTenants: 1, totalEvents: 0, criticalAlerts: 0 },
        devicesData: {
          items: [
            {
              id: 7,
              tenantId: 3,
              deviceName: "agent-7",
              platform: "Linux",
              operatingSystem: "Ubuntu",
              ipAddressInternal: "",
              ipAddressExternal: "",
              agentVersion: "",
              cpu: "",
              ram: "",
              cpuUsage: null,
              ramUsage: null,
              isOnline: true,
              lastAccess: "2026-05-03T00:00:00Z",
            },
          ],
          totalCount: 1,
          page: 1,
          pageSize: 10,
          totalPages: 1,
        },
      },
      streamStatus: "connected",
      streamError: null,
    });
    mockUseQuery.mockImplementation(({ queryKey }) => {
      if (queryKey[0] === "devices-recent") {
        return {
          data: {
            items: [{ id: 7, netbirdIp: "100.64.0.8" }],
            totalCount: 1,
            page: 1,
            pageSize: 10,
            totalPages: 1,
          },
          isLoading: false,
          isError: false,
        };
      }
      return { data: undefined, isLoading: false, isError: false };
    });

    render(<DashboardPage />);

    expect(screen.getByText("100.64.0.8")).toBeInTheDocument();
    expect(screen.getByTestId("dashboard-network-card")).toBeInTheDocument();
  });
});
