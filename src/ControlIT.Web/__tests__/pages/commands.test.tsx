import React from "react";
import { render, screen } from "@testing-library/react";
import "@testing-library/jest-dom";
import { useMutation } from "@tanstack/react-query";
import CommandsPage from "@/app/commands/page";

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

const mockUseMutation = useMutation as jest.Mock;

describe("CommandsPage", () => {
  beforeEach(() => {
    mockUseMutation.mockReturnValue({
      mutate: jest.fn(),
      isPending: false,
      isSuccess: false,
      isError: false,
      data: undefined,
      error: null,
    });
  });

  it("renders the device IDs input field", () => {
    render(<CommandsPage />);
    expect(screen.getByLabelText(/Device IDs/i)).toBeInTheDocument();
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
});
