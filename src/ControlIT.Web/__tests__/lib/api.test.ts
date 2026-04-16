import "@testing-library/jest-dom";
import { getDevices } from "@/lib/api";

// Mock fetch globally
const mockFetch = jest.fn();
global.fetch = mockFetch;

// Mock localStorage
const localStorageMock = (() => {
  let store: Record<string, string> = {};
  return {
    getItem: (key: string) => store[key] ?? null,
    setItem: (key: string, value: string) => {
      store[key] = value;
    },
    removeItem: (key: string) => {
      delete store[key];
    },
    clear: () => {
      store = {};
    },
  };
})();

Object.defineProperty(global, "localStorage", { value: localStorageMock });

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
    localStorage.clear();
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

  it("includes x-api-key header when key is in localStorage", async () => {
    localStorage.setItem("controlit_api_key", "test-key-abc");
    mockFetch.mockResolvedValueOnce(
      makeFetchResponse({ items: [], totalCount: 0, page: 1, pageSize: 10 })
    );

    await getDevices(1, 10);

    const [, init] = mockFetch.mock.calls[0] as [string, RequestInit];
    const headers = init.headers as Record<string, string>;
    expect(headers["x-api-key"]).toBe("test-key-abc");
  });

  it("does not include x-api-key header when no key is stored", async () => {
    mockFetch.mockResolvedValueOnce(
      makeFetchResponse({ items: [], totalCount: 0, page: 1, pageSize: 10 })
    );

    await getDevices(1, 10);

    const [, init] = mockFetch.mock.calls[0] as [string, RequestInit];
    const headers = init.headers as Record<string, string>;
    expect(headers["x-api-key"]).toBeUndefined();
  });
});

describe("getHealth", () => {
  beforeEach(() => {
    mockFetch.mockReset();
    localStorage.setItem("controlit_api_key", "should-not-be-sent");
  });

  it("does NOT include x-api-key header for /health requests", async () => {
    const { getHealth } = await import("@/lib/api");
    mockFetch.mockResolvedValueOnce(makeFetchResponse({ status: "ok" }));

    await getHealth();

    const [, init] = mockFetch.mock.calls[0] as [string, RequestInit];
    const headers = init.headers as Record<string, string>;
    expect(headers["x-api-key"]).toBeUndefined();
  });
});
