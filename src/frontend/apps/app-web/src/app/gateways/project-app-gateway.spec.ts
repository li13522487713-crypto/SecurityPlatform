import { beforeEach, describe, expect, it, vi } from "vitest";
import { createLowcodeProjectAppGateway } from "./project-app-gateway";

const { getLowcodeAppsPagedMock, createLowcodeAppMock, deleteLowcodeAppMock, navigateToLowcodeStudioMock } = vi.hoisted(() => ({
  getLowcodeAppsPagedMock: vi.fn(),
  createLowcodeAppMock: vi.fn(),
  deleteLowcodeAppMock: vi.fn(),
  navigateToLowcodeStudioMock: vi.fn()
}));

vi.mock("../../services/api-lowcode-apps", () => ({
  getLowcodeAppsPaged: getLowcodeAppsPagedMock,
  createLowcodeApp: createLowcodeAppMock,
  deleteLowcodeApp: deleteLowcodeAppMock
}));

vi.mock("../navigation/lowcode-studio-navigator", () => ({
  navigateToLowcodeStudio: navigateToLowcodeStudioMock
}));

describe("project-app-gateway", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("list 会把 lowcode dto 映射为项目应用卡片", async () => {
    getLowcodeAppsPagedMock.mockResolvedValue({
      items: [{
        id: "101",
        code: "demo-101",
        displayName: "Demo App",
        description: "desc",
        schemaVersion: "v1",
        targetTypes: "web",
        defaultLocale: "zh-CN",
        status: "draft",
        createdAt: "2026-04-21T08:00:00Z",
        updatedAt: "2026-04-21T08:10:00Z"
      }],
      total: 1,
      pageIndex: 1,
      pageSize: 20
    });

    const gateway = createLowcodeProjectAppGateway();
    const result = await gateway.list({ pageIndex: 1, pageSize: 20 });

    expect(getLowcodeAppsPagedMock).toHaveBeenCalledWith({ pageIndex: 1, pageSize: 20 });
    expect(result.items[0]).toEqual({
      id: "101",
      name: "Demo App",
      description: "desc",
      status: "draft",
      updatedAt: "2026-04-21T08:10:00Z"
    });
  });

  it("create 使用 lowcode create 接口并返回 appId", async () => {
    createLowcodeAppMock.mockResolvedValue("9001");
    const gateway = createLowcodeProjectAppGateway();
    const result = await gateway.create({ name: "测试工作流系统", description: "demo", locale: "zh-CN" });

    expect(createLowcodeAppMock).toHaveBeenCalledTimes(1);
    expect(createLowcodeAppMock.mock.calls[0][0]).toMatchObject({
      displayName: "测试工作流系统",
      description: "demo",
      targetTypes: "web",
      defaultLocale: "zh-CN"
    });
    expect(result).toEqual({ appId: "9001" });
  });

  it("capabilities 按 lowcode 能力矩阵输出（禁用旧动作）", () => {
    const gateway = createLowcodeProjectAppGateway({ canDelete: false });
    expect(gateway.getCapabilities()).toEqual({
      canFavorite: false,
      canDuplicate: false,
      canMove: false,
      canMigrate: false,
      canCopyToWorkspace: false,
      canDelete: false
    });
  });

  it("open 通过 lowcode studio 导航器跳转", () => {
    const gateway = createLowcodeProjectAppGateway();
    gateway.open("7788");
    expect(navigateToLowcodeStudioMock).toHaveBeenCalledWith("7788");
  });

  it("delete 透传 lowcode delete 接口", async () => {
    deleteLowcodeAppMock.mockResolvedValue(undefined);
    const gateway = createLowcodeProjectAppGateway();
    await gateway.delete("7788");
    expect(deleteLowcodeAppMock).toHaveBeenCalledWith("7788");
  });
});
