import { beforeEach, describe, expect, it, vi } from "vitest";
import {
  copyWorkspaceIdeResourceToWorkspace,
  deleteWorkspaceIdeResource,
  duplicateWorkspaceIdeResource,
  getWorkspaceIdeResources,
  migrateWorkspaceIdeResource
} from "./api-workspace-ide";

const { requestApiMock } = vi.hoisted(() => ({
  requestApiMock: vi.fn()
}));

vi.mock("./api-core", () => ({
  requestApi: requestApiMock
}));

describe("api-workspace-ide", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("拼接 resources 查询参数时包含 folderId/status/workspaceId", async () => {
    requestApiMock.mockResolvedValue({
      success: true,
      data: {
        items: [],
        total: 0,
        pageIndex: 2,
        pageSize: 30
      }
    });

    await getWorkspaceIdeResources({
      pageIndex: 2,
      pageSize: 30,
      keyword: "flow",
      resourceType: "app",
      favoriteOnly: true,
      folderId: "1001",
      status: "draft",
      workspaceId: "2001"
    });

    expect(requestApiMock).toHaveBeenCalledTimes(1);
    const [url] = requestApiMock.mock.calls[0] as [string];
    const parsed = new URL(`http://localhost${url}`);
    expect(parsed.pathname).toBe("/workspace-ide/resources");
    expect(parsed.searchParams.get("pageIndex")).toBe("2");
    expect(parsed.searchParams.get("pageSize")).toBe("30");
    expect(parsed.searchParams.get("keyword")).toBe("flow");
    expect(parsed.searchParams.get("resourceType")).toBe("app");
    expect(parsed.searchParams.get("favoriteOnly")).toBe("true");
    expect(parsed.searchParams.get("folderId")).toBe("1001");
    expect(parsed.searchParams.get("status")).toBe("draft");
    expect(parsed.searchParams.get("workspaceId")).toBe("2001");
  });

  it("创建副本调用 duplicate 接口", async () => {
    requestApiMock.mockResolvedValue({
      success: true,
      data: { resourceType: "app", resourceId: "99", action: "duplicate" }
    });

    await duplicateWorkspaceIdeResource("app", "99", {
      workspaceId: "2001",
      folderId: "1001"
    });

    expect(requestApiMock).toHaveBeenCalledWith(
      "/workspace-ide/resources/app/99/duplicate",
      expect.objectContaining({
        method: "POST",
        body: JSON.stringify({ workspaceId: "2001", folderId: "1001" })
      })
    );
  });

  it("迁移与复制到其它空间分别命中正确接口", async () => {
    requestApiMock.mockResolvedValue({
      success: true,
      data: { resourceType: "agent", resourceId: "7", action: "migrate" }
    });

    await migrateWorkspaceIdeResource("agent", "7", {
      sourceWorkspaceId: "2001",
      targetWorkspaceId: "2002"
    });

    await copyWorkspaceIdeResourceToWorkspace("agent", "7", {
      sourceWorkspaceId: "2001",
      targetWorkspaceId: "2002"
    });

    expect(requestApiMock).toHaveBeenNthCalledWith(
      1,
      "/workspace-ide/resources/agent/7/migrate",
      expect.objectContaining({
        method: "POST"
      })
    );
    expect(requestApiMock).toHaveBeenNthCalledWith(
      2,
      "/workspace-ide/resources/agent/7/copy-to-workspace",
      expect.objectContaining({
        method: "POST"
      })
    );
  });

  it("删除资源走 DELETE", async () => {
    requestApiMock.mockResolvedValue({ success: true, data: {} });

    await deleteWorkspaceIdeResource("app", "101");

    expect(requestApiMock).toHaveBeenCalledWith(
      "/workspace-ide/resources/app/101",
      expect.objectContaining({
        method: "DELETE"
      })
    );
  });
});
