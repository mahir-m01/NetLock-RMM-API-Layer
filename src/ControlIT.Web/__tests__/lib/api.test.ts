import "@testing-library/jest-dom";
import { setAccessToken, clearTokens } from "@/lib/auth";
import { getDevices, getHealth, ApiError } from "@/lib/api";

// Mock fetch globally
const mockFetch = jest.fn();
global.fetch = mockFetch;

function makeFetchResponse(body: unknown, ok = true, status = 200) {
  return {
    ok,
    status,
    headers: {
      get: (name: string) =>
        name === "content-type" ? "application/json" : null,
    },
    json: () => Promise.resolve(body),
    text: () => Promise.resolve(String(body)),
    statusText: "OK",
  } as unknown as Response;
}

describe("getDevices", () => {
  beforeEach(() => {
    mockFetch.mockReset();
    clearTokens();
  });

  it("constructs the correct URL with page and pageSize params", async () => {
    mockFetch.mockResolvedValueOnce(
      makeFetchResponse({ items: [], totalCount: 0, page: 1, pageSize: 10 })
    );

    await getDevices(1, 10);

    const [url] = mockFetch.mock.calls[0] as [string, RequestInit];
    expect(url).toContain("page=1");
    expect(url).toContain("pageSize=10");
    expect(url).toContain("/devices");
  });

  it("includes Authorization Bearer header when access token is set", async () => {
    setAccessToken("test-access-token");
    mockFetch.mockResolvedValueOnce(
      makeFetchResponse({ items: [], totalCount: 0, page: 1, pageSize: 10 })
    );

    await getDevices(1, 10);

    const [, init] = mockFetch.mock.calls[0] as [string, RequestInit];
    const headers = init.headers as Record<string, string>;
    expect(headers["Authorization"]).toBe("Bearer test-access-token");
  });

  it("does not include Authorization header when no token is set", async () => {
    mockFetch.mockResolvedValueOnce(
      makeFetchResponse({ items: [], totalCount: 0, page: 1, pageSize: 10 })
    );

    // Simulate a 401 then a failed refresh so we don't hang
    mockFetch.mockResolvedValueOnce(makeFetchResponse({}, false, 401));
    mockFetch.mockResolvedValueOnce(makeFetchResponse({}, false, 401)); // refresh fails

    // With no token the first request will 401 → try refresh (also fails) → throw
    // For this test, just mock a successful response directly
    mockFetch.mockReset();
    mockFetch.mockResolvedValueOnce(
      makeFetchResponse({ items: [], totalCount: 0, page: 1, pageSize: 10 })
    );

    await getDevices(1, 10);

    const [, init] = mockFetch.mock.calls[0] as [string, RequestInit];
    const headers = init.headers as Record<string, string>;
    expect(headers["Authorization"]).toBeUndefined();
  });
});

describe("getHealth", () => {
  beforeEach(() => {
    mockFetch.mockReset();
    setAccessToken("should-still-be-sent-but-skipAuth-true");
  });

  afterEach(() => {
    clearTokens();
  });

  it("does NOT throw on a successful health response", async () => {
    mockFetch.mockResolvedValueOnce(makeFetchResponse({ status: "ok" }));

    const result = await getHealth();
    expect(result).toEqual({ status: "ok" });
  });
});

describe("ApiError", () => {
  it("exposes status and message", () => {
    const err = new ApiError(403, "Forbidden");
    expect(err.status).toBe(403);
    expect(err.message).toBe("Forbidden");
    expect(err.name).toBe("ApiError");
  });
});
