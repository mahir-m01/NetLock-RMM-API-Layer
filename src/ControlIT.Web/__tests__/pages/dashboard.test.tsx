import React from "react";
import { render, screen } from "@testing-library/react";
import "@testing-library/jest-dom";
import { useQuery } from "@tanstack/react-query";
import DashboardPage from "@/app/dashboard/page";

jest.mock("next/navigation", () => ({
  useRouter: () => ({ push: jest.fn() }),
  usePathname: () => "/dashboard",
}));

jest.mock("@tanstack/react-query", () => ({
  useQuery: jest.fn(),
  useMutation: jest.fn(),
  QueryClient: jest.fn(),
  QueryClientProvider: ({ children }: { children: React.ReactNode }) => children,
}));

const mockUseQuery = useQuery as jest.Mock;

describe("DashboardPage", () => {
  it("renders skeleton cards when loading", () => {
    mockUseQuery.mockReturnValue({ data: undefined, isLoading: true, isError: false });
    render(<DashboardPage />);
    // Skeleton elements render via aria role or data-slot
    const skeletons = document.querySelectorAll("[data-slot='skeleton']");
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it("renders all 4 stat values when data is loaded", () => {
    mockUseQuery.mockReturnValue({
      data: { totalDevices: 5, onlineDevices: 2, totalTenants: 3, totalEvents: 100 },
      isLoading: false,
      isError: false,
    });
    render(<DashboardPage />);
    expect(screen.getByText("5")).toBeInTheDocument();
    expect(screen.getByText("2")).toBeInTheDocument();
    expect(screen.getByText("3")).toBeInTheDocument();
    expect(screen.getByText("100")).toBeInTheDocument();
  });

  it("renders error message when query fails", () => {
    mockUseQuery.mockReturnValue({ data: undefined, isLoading: false, isError: true });
    render(<DashboardPage />);
    expect(
      screen.getByText(/Failed to load dashboard data/i)
    ).toBeInTheDocument();
  });
});
