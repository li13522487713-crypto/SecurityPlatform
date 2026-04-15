import { beforeEach, describe, expect, it, vi } from "vitest";

const {
  requestApi,
  getAppInstanceIdByAppKey,
  createWorkflowApiFromRequest
} = vi.hoisted(() => ({
  requestApi: vi.fn(),
  getAppInstanceIdByAppKey: vi.fn<(appKey: string) => Promise<string | null>>(),
  createWorkflowApiFromRequest: vi.fn(() => ({}))
}));

vi.mock("@atlas/workflow-core-react/api", () => ({
  createWorkflowApiFromRequest
}));

vi.mock("./api-core", () => ({
  API_BASE: "/api/v1",
  requestApi
}));

vi.mock("./api-lowcode-runtime", () => ({
  getAppInstanceIdByAppKey
}));

function installBrowserGlobals(): void {
  const storage = new Map<string, string>();
  const location = { pathname: "/" };
  const history = {
    pushState: (_state: unknown, _title: string, url?: string | URL | null) => {
      if (!url) {
        return;
      }

      const value = typeof url === "string" ? url : url.toString();
      location.pathname = new URL(value, "http://localhost").pathname;
    }
  };
  const localStorageMock = {
    getItem: (key: string) => storage.get(key) ?? null,
    setItem: (key: string, value: string) => {
      storage.set(key, value);
    },
    removeItem: (key: string) => {
      storage.delete(key);
    },
    clear: () => {
      storage.clear();
    }
  };

  vi.stubGlobal("localStorage", localStorageMock);
  vi.stubGlobal("window", { location, history });
}

describe("api-workflow", () => {
  beforeEach(async () => {
    vi.resetModules();
    installBrowserGlobals();
    localStorage.clear();
    vi.clearAllMocks();

    const { resetAppInstanceContextForTests } = await import("./app-instance-context");
    resetAppInstanceContextForTests();

    requestApi.mockResolvedValue({
      success: true,
      data: []
    });
    getAppInstanceIdByAppKey.mockImplementation(async (appKey) => appKey === "app-alpha" ? "101" : "202");
  });

  it("uses the current route app instance id in workflow headers after route changes", async () => {
    const { getWorkflowModelCatalog } = await import("./api-workflow");

    window.history.pushState({}, "", "/apps/app-alpha/work_flow/101/editor");
    await getWorkflowModelCatalog();

    window.history.pushState({}, "", "/apps/app-beta/work_flow/202/editor");
    await getWorkflowModelCatalog();

    const firstHeaders = requestApi.mock.calls[0]?.[1]?.headers as Headers;
    const secondHeaders = requestApi.mock.calls[1]?.[1]?.headers as Headers;

    expect(firstHeaders.get("X-App-Id")).toBe("101");
    expect(firstHeaders.get("X-App-Workspace")).toBe("1");
    expect(secondHeaders.get("X-App-Id")).toBe("202");
    expect(secondHeaders.get("X-App-Workspace")).toBe("1");
    expect(getAppInstanceIdByAppKey).toHaveBeenNthCalledWith(1, "app-alpha");
    expect(getAppInstanceIdByAppKey).toHaveBeenNthCalledWith(2, "app-beta");
  });

  it("falls back to configured appKey for workspace routes when building workflow headers", async () => {
    const { getWorkflowModelCatalog } = await import("./api-workflow");

    localStorage.setItem("atlas_app_last_appkey", "app-beta");
    localStorage.setItem("atlas_app_instance_ids", JSON.stringify({ "app-beta": "202" }));
    window.history.pushState({}, "", "/org/demo/workspaces/100/workflows/200");

    await getWorkflowModelCatalog();

    const headers = requestApi.mock.calls[0]?.[1]?.headers as Headers;
    expect(headers.get("X-App-Id")).toBe("202");
    expect(headers.get("X-App-Workspace")).toBe("1");
    expect(getAppInstanceIdByAppKey).not.toHaveBeenCalled();
  });
});
