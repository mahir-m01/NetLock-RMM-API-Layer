import React from "react";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import "@testing-library/jest-dom";
import { useMutation, useQuery } from "@tanstack/react-query";
import CommandsPage from "@/app/commands/page";
import { executeBatchCommand } from "@/lib/api";

jest.mock("next/navigation", () => ({
  useRouter: () => ({ push: jest.fn() }),
  usePathname: () => "/commands",
}));

jest.mock("@tanstack/react-query", () => ({
  useQuery: jest.fn(),
  useMutation: jest.fn(),
  QueryClient: jest.fn(),
  QueryClientProvider: ({ children }: { children: React.ReactNode }) => children,
}));

jest.mock("@/lib/api", () => ({
  executeBatchCommand: jest.fn(),
  getDevices: jest.fn(),
}));

const mockUseMutation = useMutation as jest.Mock;
const mockUseQuery = useQuery as jest.Mock;
const mockExecuteBatchCommand = executeBatchCommand as jest.Mock;

const devicesResponse = {
  items: [
    {
      id: 27,
      tenantId: 1,
      deviceName: "linux-runner",
      platform: "Linux",
      operatingSystem: "Ubuntu",
      ipAddressInternal: "10.0.0.27",
      ipAddressExternal: "203.0.113.27",
      agentVersion: "1.0.0",
      cpu: "x64",
      ram: "8GB",
      cpuUsage: null,
      ramUsage: null,
      isOnline: true,
      lastAccess: "2026-05-04T00:00:00Z",
      netbirdIp: "100.64.0.27",
    },
  ],
  totalCount: 1,
  page: 1,
  pageSize: 25,
  totalPages: 1,
};

describe("CommandsPage", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockUseQuery.mockReturnValue({
      data: devicesResponse,
      isLoading: false,
      isError: false,
    });
    mockExecuteBatchCommand.mockResolvedValue({ results: [] });
    mockUseMutation.mockImplementation((options) => ({
      mutate: jest.fn(() => options.mutationFn()),
      isPending: false,
      isSuccess: false,
      isError: false,
      data: undefined,
      error: null,
    }));
  });

  it("renders the device picker search field", () => {
    render(<CommandsPage />);
    expect(screen.getByLabelText(/Target devices/i)).toBeInTheDocument();
  });

  it("renders device rows with status and NetBird IP", () => {
    render(<CommandsPage />);
    expect(screen.getByText("linux-runner")).toBeInTheDocument();
    expect(screen.getByText("Online")).toBeInTheDocument();
    expect(screen.getByText("100.64.0.27")).toBeInTheDocument();
  });

  it("renders the command textarea", () => {
    render(<CommandsPage />);
    expect(screen.getByLabelText(/Command/i)).toBeInTheDocument();
  });

  it("renders the shell select trigger", () => {
    render(<CommandsPage />);
    expect(screen.getByText("Shell")).toBeInTheDocument();
  });

  it("renders the submit button", () => {
    render(<CommandsPage />);
    expect(
      screen.getByRole("button", { name: /Run Batch/i })
    ).toBeInTheDocument();
  });

  it("does not crash on initial render", () => {
    expect(() => render(<CommandsPage />)).not.toThrow();
  });

  it("submits selected device IDs to executeBatchCommand", async () => {
    render(<CommandsPage />);

    fireEvent.click(screen.getByRole("button", { name: /Select linux-runner/i }));
    fireEvent.change(screen.getByLabelText(/Command/i), {
      target: { value: "uptime" },
    });
    fireEvent.click(screen.getByRole("button", { name: /Run Batch/i }));

    await waitFor(() => {
      expect(mockExecuteBatchCommand).toHaveBeenCalledWith({
        deviceIds: [27],
        command: "uptime",
        shell: "bash",
        timeoutSeconds: 30,
      });
    });
  });
});
