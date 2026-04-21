import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  createLowcodeApp,
  deleteLowcodeApp,
  getLowcodeAppsPaged
} from "./api-lowcode-apps";

const { requestApiMock } = vi.hoisted(() => ({
  requestApiMock: vi.fn()
}));

vi.mock("./api-core", () => ({
  requestApi: requestApiMock
}));

describe("api-lowcode-apps", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("列表请求会拼接 keyword/status/page 参数", async () => {
    requestApiMock.mockResolvedValue({
      success: true,
      data: {
        items: [],
        total: 0,
        pageIndex: 2,
        pageSize: 30
      }
    });

    await getLowcodeAppsPaged({
      pageIndex: 2,
      pageSize: 30,
      keyword: "demo",
      status: "draft"
    });

    expect(requestApiMock).toHaveBeenCalledTimes(1);
    const [url] = requestApiMock.mock.calls[0] as [string];
    const parsed = new URL(`http://localhost${url}`);
    expect(parsed.pathname).toBe("/lowcode/apps");
    expect(parsed.searchParams.get("pageIndex")).toBe("2");
    expect(parsed.searchParams.get("pageSize")).toBe("30");
    expect(parsed.searchParams.get("keyword")).toBe("demo");
    expect(parsed.searchParams.get("status")).toBe("draft");
  });

  it("创建接口返回 data.id", async () => {
    requestApiMock.mockResolvedValue({
      success: true,
      data: { id: "12345" }
    });

    const appId = await createLowcodeApp({
      code: "demo",
      displayName: "Demo",
      targetTypes: "web"
    });

    expect(appId).toBe("12345");
    expect(requestApiMock).toHaveBeenCalledWith(
      "/lowcode/apps",
      expect.objectContaining({ method: "POST" })
    );
  });

  it("删除接口走 DELETE", async () => {
    requestApiMock.mockResolvedValue({ success: true, data: {} });
    await deleteLowcodeApp("12345");
    expect(requestApiMock).toHaveBeenCalledWith(
      "/lowcode/apps/12345",
      expect.objectContaining({ method: "DELETE" })
    );
  });
});
