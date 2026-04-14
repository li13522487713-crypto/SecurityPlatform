import { beforeEach, describe, expect, it, vi } from "vitest";
import { setAppInstanceIdToStorage } from "../utils/app-context";
import {
  getCachedAppInstanceId,
  resetAppInstanceContextForTests,
  resolveAppInstanceId
} from "./app-instance-context";

const { getAppInstanceIdByAppKey } = vi.hoisted(() => ({
  getAppInstanceIdByAppKey: vi.fn<(appKey: string) => Promise<string | null>>()
}));

vi.mock("./api-lowcode-runtime", () => ({
  getAppInstanceIdByAppKey
}));

function installBrowserGlobals(): void {
  const storage = new Map<string, string>();
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

  const windowMock = {
    location: { pathname: "/apps/app-alpha" }
  };

  vi.stubGlobal("localStorage", localStorageMock);
  vi.stubGlobal("window", windowMock);
}

describe("app-instance-context", () => {
  beforeEach(() => {
    installBrowserGlobals();
    localStorage.clear();
    resetAppInstanceContextForTests();
    vi.clearAllMocks();
  });

  it("hits the cache for the same appKey", async () => {
    getAppInstanceIdByAppKey.mockImplementation(async () => "101");

    await expect(resolveAppInstanceId("app-alpha")).resolves.toBe("101");
    await expect(resolveAppInstanceId("app-alpha")).resolves.toBe("101");

    expect(getCachedAppInstanceId("app-alpha")).toBe("101");
    expect(getAppInstanceIdByAppKey).toHaveBeenCalledTimes(1);
    expect(getAppInstanceIdByAppKey).toHaveBeenCalledWith("app-alpha");
  });

  it("isolates cached instances by appKey", async () => {
    getAppInstanceIdByAppKey.mockImplementation(async (appKey) => appKey === "app-alpha" ? "101" : "202");

    await expect(resolveAppInstanceId("app-alpha")).resolves.toBe("101");
    await expect(resolveAppInstanceId("app-beta")).resolves.toBe("202");

    expect(getCachedAppInstanceId("app-alpha")).toBe("101");
    expect(getCachedAppInstanceId("app-beta")).toBe("202");
    expect(getAppInstanceIdByAppKey).toHaveBeenCalledTimes(2);
  });

  it("falls back to source resolution after cache invalidation", async () => {
    getAppInstanceIdByAppKey
      .mockResolvedValueOnce("101")
      .mockResolvedValueOnce("102");

    await expect(resolveAppInstanceId("app-alpha")).resolves.toBe("101");

    setAppInstanceIdToStorage("app-alpha", null);

    await expect(resolveAppInstanceId("app-alpha")).resolves.toBe("102");
    expect(getAppInstanceIdByAppKey).toHaveBeenCalledTimes(2);
  });

  it("deduplicates concurrent resolutions for the same appKey", async () => {
    let resolveRequest: ((value: string | null) => void) | null = null;
    getAppInstanceIdByAppKey.mockImplementation(
      () =>
        new Promise((resolve) => {
          resolveRequest = resolve;
        })
    );

    const pendingA = resolveAppInstanceId("app-alpha");
    const pendingB = resolveAppInstanceId("app-alpha");

    expect(getAppInstanceIdByAppKey).toHaveBeenCalledTimes(1);

    resolveRequest?.("101");

    await expect(Promise.all([pendingA, pendingB])).resolves.toEqual([
      "101",
      "101"
    ]);
  });
});
