import { beforeEach, describe, expect, it, vi } from "vitest";
import { getModelConfigStats, getModelConfigsPaged } from "./api-model-config";

const { requestApiMock } = vi.hoisted(() => ({
  requestApiMock: vi.fn()
}));

vi.mock("./api-core", () => ({
  requestApi: requestApiMock,
  extractResourceId: vi.fn(),
  toQuery: vi.fn((request: Record<string, unknown>, extras?: Record<string, string | undefined>) => {
    const params = new URLSearchParams();
    for (const [key, value] of Object.entries(request)) {
      if (value !== undefined && value !== null) {
        params.set(key, String(value));
      }
    }
    if (extras) {
      for (const [key, value] of Object.entries(extras)) {
        if (value !== undefined && value !== null && value !== "") {
          params.set(key, value);
        }
      }
    }
    return params.toString();
  }),
  API_BASE: "/api/v1"
}));

vi.mock("@atlas/shared-react-core/utils", () => ({
  getAccessToken: vi.fn(),
  getTenantId: vi.fn()
}));

describe("api-model-config", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("分页请求会携带 workspaceId", async () => {
    requestApiMock.mockResolvedValue({
      success: true,
      data: {
        items: [],
        total: 0,
        pageIndex: 1,
        pageSize: 50
      }
    });

    await getModelConfigsPaged(
      { pageIndex: 1, pageSize: 50 },
      { keyword: "openai", workspaceId: "ws-2001" }
    );

    const [url] = requestApiMock.mock.calls[0] as [string];
    const parsed = new URL(`http://localhost${url}`);
    expect(parsed.pathname).toBe("/model-configs");
    expect(parsed.searchParams.get("keyword")).toBe("openai");
    expect(parsed.searchParams.get("workspaceId")).toBe("ws-2001");
  });

  it("统计请求会携带 workspaceId", async () => {
    requestApiMock.mockResolvedValue({
      success: true,
      data: {
        total: 1,
        enabled: 1,
        disabled: 0,
        embeddingCount: 1
      }
    });

    await getModelConfigStats("openai", "ws-2001");

    const [url] = requestApiMock.mock.calls[0] as [string];
    const parsed = new URL(`http://localhost${url}`);
    expect(parsed.pathname).toBe("/model-configs/stats");
    expect(parsed.searchParams.get("keyword")).toBe("openai");
    expect(parsed.searchParams.get("workspaceId")).toBe("ws-2001");
  });
});
