import React from "react";
import { render, screen } from "@testing-library/react";
import "@testing-library/jest-dom";
import { useQuery } from "@tanstack/react-query";
import DevicesPage from "@/app/devices/page";

jest.mock("next/navigation", () => ({
  useRouter: () => ({ push: jest.fn() }),
  usePathname: () => "/devices",
}));

jest.mock("@tanstack/react-query", () => ({
  useQuery: jest.fn(),
  useMutation: jest.fn(),
  QueryClient: jest.fn(),
  QueryClientProvider: ({ children }: { children: React.ReactNode }) => children,
}));

const mockUseQuery = useQuery as jest.Mock;

describe("DevicesPage", () => {
  it("renders skeleton rows when loading and does not crash", () => {
    mockUseQuery.mockReturnValue({ data: undefined, isLoading: true, isError: false });
    render(<DevicesPage />);
    const skeletons = document.querySelectorAll("[data-slot='skeleton']");
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it("renders device name when data contains items", () => {
    mockUseQuery.mockReturnValue({
      data: {
        items: [
          { id: 27, deviceName: "test-device", platform: "Linux", isOnline: true, netbirdIp: "100.64.0.8" },
        ],
        totalCount: 1,
        page: 1,
        pageSize: 10,
      },
      isLoading: false,
      isError: false,
    });
    render(<DevicesPage />);
    expect(screen.getByText("test-device")).toBeInTheDocument();
    expect(screen.getByText("100.64.0.8")).toBeInTheDocument();
  });

  it("renders concise NetBird not linked state", () => {
    mockUseQuery.mockReturnValue({
      data: {
        items: [
          { id: 28, deviceName: "unlinked-device", platform: "Windows", isOnline: false, netbirdIp: null },
        ],
        totalCount: 1,
        page: 1,
        pageSize: 10,
      },
      isLoading: false,
      isError: false,
    });

    render(<DevicesPage />);

    expect(screen.getByText("NetBird not linked")).toBeInTheDocument();
  });

  it("renders 'No devices found' when items array is empty", () => {
    mockUseQuery.mockReturnValue({
      data: { items: [], totalCount: 0, page: 1, pageSize: 10 },
      isLoading: false,
      isError: false,
    });
    render(<DevicesPage />);
    expect(screen.getByText(/No devices found/i)).toBeInTheDocument();
  });
});
