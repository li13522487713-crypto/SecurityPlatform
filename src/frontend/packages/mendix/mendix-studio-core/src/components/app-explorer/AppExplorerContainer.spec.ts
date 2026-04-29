import { describe, expect, it } from "vitest";

import { resolveExplorerCreateContext } from "./app-explorer-create-context";

const modules = [
  { moduleId: "sales", name: "Sales", qualifiedName: "Sales" },
  { moduleId: "inventory", name: "Inventory", qualifiedName: "Inventory" }
];

describe("resolveExplorerCreateContext", () => {
  it("Microflows 分组通过 key 解析并锁定真实模块", () => {
    const context = resolveExplorerCreateContext({
      node: { key: "microflows:sales", kind: "folder" },
      modules,
      appId: "app-1",
      workspaceId: "workspace-1"
    });

    expect(context).toMatchObject({
      appId: "app-1",
      workspaceId: "workspace-1",
      moduleId: "sales",
      moduleName: "Sales",
      sourceNodeKey: "microflows:sales"
    });
  });

  it("动态文件夹继承 folderId/folderPath 和模块上下文", () => {
    const context = resolveExplorerCreateContext({
      node: { key: "folder:orders", kind: "folder", moduleId: "sales", folderId: "orders", folderPath: "Sales/Orders" },
      modules,
      fallbackModuleId: "inventory"
    });

    expect(context).toMatchObject({
      moduleId: "sales",
      moduleName: "Sales",
      folderId: "orders",
      folderPath: "Sales/Orders"
    });
  });

  it("缺少显式模块时只使用有效 fallback，避免锁死空模块", () => {
    const validFallback = resolveExplorerCreateContext({
      node: { key: "microflows", kind: "folder" },
      modules,
      fallbackModuleId: "inventory"
    });
    const invalidFallback = resolveExplorerCreateContext({
      node: { key: "microflows", kind: "folder" },
      modules,
      fallbackModuleId: "stale"
    });

    expect(validFallback.moduleId).toBe("inventory");
    expect(invalidFallback.moduleId).toBeUndefined();
  });
});
