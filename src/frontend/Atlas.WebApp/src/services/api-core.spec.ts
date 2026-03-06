import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const authState = {
  accessToken: "access-token",
  refreshToken: null as string | null,
  tenantId: "00000000-0000-0000-0000-000000000001",
  projectId: null as string | null,
  projectScopeEnabled: false,
  antiforgeryToken: "csrf-token"
};

vi.mock("ant-design-vue", () => ({
  message: {
    warning: vi.fn(),
    open: vi.fn(),
    error: vi.fn()
  }
}));

vi.mock("@/router", () => ({
  default: {
    currentRoute: { value: { name: "home" } },
    push: vi.fn()
  }
}));

vi.mock("@/utils/clientContext", () => ({
  getClientContextHeaders: () => ({})
}));

vi.mock("@/utils/auth", () => ({
  clearAuthStorage: vi.fn(),
  getAccessToken: () => authState.accessToken,
  getRefreshToken: () => authState.refreshToken,
  setAccessToken: vi.fn(),
  setRefreshToken: vi.fn(),
  getTenantId: () => authState.tenantId,
  getProjectId: () => authState.projectId,
  getProjectScopeEnabled: () => authState.projectScopeEnabled,
  getAntiforgeryToken: () => authState.antiforgeryToken,
  setAntiforgeryToken: vi.fn(),
  clearAntiforgeryToken: vi.fn()
}));

import { requestApi } from "@/services/api-core";

describe("requestApi 写请求防重复", () => {
  beforeEach(() => {
    authState.accessToken = "access-token";
    authState.refreshToken = null;
    authState.tenantId = "00000000-0000-0000-0000-000000000001";
    authState.projectId = null;
    authState.projectScopeEnabled = false;
    authState.antiforgeryToken = "csrf-token";
    vi.restoreAllMocks();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("相同写请求并发调用时只发送一次网络请求", async () => {
    let resolveFetch!: (value: Response) => void;
    const pendingResponse = new Promise<Response>((resolve) => {
      resolveFetch = resolve;
    });
    const fetchMock = vi.fn(() => pendingResponse);
    vi.stubGlobal("fetch", fetchMock);

    const requestInit: RequestInit = {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ name: "demo-project" })
    };

    const promise1 = requestApi("/projects", requestInit);
    const promise2 = requestApi("/projects", requestInit);
    await Promise.resolve();

    expect(fetchMock).toHaveBeenCalledTimes(1);
    resolveFetch(
      new Response(
        JSON.stringify({
          success: true,
          code: "SUCCESS",
          message: "OK",
          traceId: "trace-1",
          data: { id: "1001" }
        }),
        {
          status: 200,
          headers: { "Content-Type": "application/json" }
        }
      )
    );

    const [result1, result2] = await Promise.all([promise1, promise2]);
    expect(result1).toEqual(result2);
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it("写请求 payload 不同会分别发送", async () => {
    const fetchMock = vi.fn(() =>
      Promise.resolve(
        new Response(
          JSON.stringify({
            success: true,
            code: "SUCCESS",
            message: "OK",
            traceId: "trace-2",
            data: { id: "1002" }
          }),
          {
            status: 200,
            headers: { "Content-Type": "application/json" }
          }
        )
      )
    );
    vi.stubGlobal("fetch", fetchMock);

    await requestApi("/projects", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ name: "project-a" })
    });
    await requestApi("/projects", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ name: "project-b" })
    });

    expect(fetchMock).toHaveBeenCalledTimes(2);
  });
});
