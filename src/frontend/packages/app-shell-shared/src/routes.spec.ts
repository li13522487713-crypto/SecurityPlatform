import { describe, expect, it } from "vitest";
import {
  orgWorkspacesPath,
  orgWorkspaceHomePath,
  orgWorkspaceDashboardPath,
  orgWorkspaceDevelopPath,
  orgWorkspaceChatPath,
  orgWorkspaceModelConfigsPath,
  orgWorkspaceAssistantToolsPath,
  orgWorkspacePublishCenterPath,
  orgWorkspaceAppDetailPath,
  orgWorkspaceAgentDetailPath,
  orgWorkspaceManagePath,
  orgWorkspaceSettingsPath,
  orgWorkspaceDataPath,
  orgWorkspaceVariablesPath,
  orgWorkspaceWorkflowsPath,
  orgWorkspaceChatflowsPath,
  orgWorkspaceAppWorkflowPath,
  signPath
} from "./routes";

describe("organization workspace routes", () => {
  it("builds sign path with redirect", () => {
    expect(signPath("/workspaces")).toBe("/sign?redirect=%2Fworkspaces");
  });

  it("builds workspace list and workspace paths", () => {
    expect(orgWorkspacesPath("tenant-1")).toBe("/space");
    expect(orgWorkspaceHomePath("tenant-1", "100")).toBe("/space/100/home");
    expect(orgWorkspaceDashboardPath("tenant-1", "100")).toBe("/space/100/home");
    expect(orgWorkspaceDevelopPath("tenant-1", "100")).toBe("/space/100/develop");
    expect(orgWorkspaceChatPath("tenant-1", "100")).toBe("/space/100/develop/chat");
    expect(orgWorkspaceModelConfigsPath("tenant-1", "100")).toBe("/space/100/develop/model-configs");
    expect(orgWorkspaceAssistantToolsPath("tenant-1", "100")).toBe("/space/100/develop/assistant-tools");
    expect(orgWorkspacePublishCenterPath("tenant-1", "100")).toBe("/space/100/develop/publish-center");
    expect(orgWorkspaceDataPath("tenant-1", "100")).toBe("/space/100/library/data");
    expect(orgWorkspaceVariablesPath("tenant-1", "100")).toBe("/space/100/library/variables");
    expect(orgWorkspaceManagePath("tenant-1", "100")).toBe("/space/100/manage");
    expect(orgWorkspaceManagePath("tenant-1", "100", "users")).toBe("/space/100/manage/users");
    expect(orgWorkspaceSettingsPath("tenant-1", "100")).toBe("/space/100/settings");
    expect(orgWorkspaceSettingsPath("tenant-1", "100", "members")).toBe("/space/100/settings/members");
  });

  it("builds deep resource paths", () => {
    expect(orgWorkspaceAppDetailPath("tenant-1", "100", "200")).toBe("/space/100/apps/200");
    expect(orgWorkspaceAgentDetailPath("tenant-1", "100", "300")).toBe("/space/100/agents/300");
    expect(orgWorkspaceWorkflowsPath("tenant-1", "100")).toBe("/space/100/workflows");
    expect(orgWorkspaceChatflowsPath("tenant-1", "100")).toBe("/space/100/chatflows");
    expect(orgWorkspaceAppWorkflowPath("tenant-1", "100", "200", "400")).toBe("/space/100/apps/200/workflows/400");
  });
});

import {
  LOWCODE_APP_KEY,
  LOWCODE_ROUTES,
  lowcodeAppListPath,
  lowcodeAppPreviewPath,
  lowcodeAppPublishPath,
  lowcodeAppStudioPath,
  lowcodeAppVersionsPath,
  lowcodeFaqPath,
  lowcodeTemplatesPath
} from "./routes";
import { buildWorkspaceSwitchPath } from "./workspace-routes";

describe("lowcode routes (M07 C07-11)", () => {
  it("uses lowcode app key", () => {
    expect(LOWCODE_APP_KEY).toBe("lowcode");
    expect(lowcodeAppListPath()).toBe("/apps/lowcode");
  });

  it("builds studio / preview / publish / versions paths", () => {
    expect(lowcodeAppStudioPath("100")).toBe("/apps/lowcode/100/studio");
    expect(lowcodeAppPreviewPath("100")).toBe("/apps/lowcode/100/preview");
    expect(lowcodeAppPreviewPath("100", { v: "1" })).toBe("/apps/lowcode/100/preview?v=1");
    expect(lowcodeAppPublishPath("100")).toBe("/apps/lowcode/100/publish");
    expect(lowcodeAppVersionsPath("100")).toBe("/apps/lowcode/100/versions");
  });

  it("supports faq + templates with query", () => {
    expect(lowcodeFaqPath()).toBe("/apps/lowcode/faq");
    expect(lowcodeFaqPath({ q: "foo" })).toBe("/apps/lowcode/faq?q=foo");
    expect(lowcodeTemplatesPath({ kind: "page" })).toBe("/apps/lowcode/templates?kind=page");
  });

  it("LOWCODE_ROUTES catalog provides all helpers", () => {
    expect(LOWCODE_ROUTES.studio("9")).toBe("/apps/lowcode/9/studio");
    expect(LOWCODE_ROUTES.preview("9")).toBe("/apps/lowcode/9/preview");
    expect(LOWCODE_ROUTES.publish("9")).toBe("/apps/lowcode/9/publish");
    expect(LOWCODE_ROUTES.versions("9")).toBe("/apps/lowcode/9/versions");
    expect(LOWCODE_ROUTES.faq()).toBe("/apps/lowcode/faq");
    expect(LOWCODE_ROUTES.templates()).toBe("/apps/lowcode/templates");
  });
});

describe("workspace switch path", () => {
  it("保留工作区菜单路径与查询参数", () => {
    expect(buildWorkspaceSwitchPath("/space/100/resources/knowledge?keyword=demo", "200"))
      .toBe("/space/200/resources/knowledge?keyword=demo");
  });

  it("详情型路径会自动降级到菜单根路径", () => {
    expect(buildWorkspaceSwitchPath("/space/100/projects/folder/f-1", "200"))
      .toBe("/space/200/projects");
    expect(buildWorkspaceSwitchPath("/space/100/tasks/t-1?tab=detail", "200"))
      .toBe("/space/200/tasks");
    expect(buildWorkspaceSwitchPath("/space/100/evaluations/e-1", "200"))
      .toBe("/space/200/evaluations");
  });

  it("非工作区路径保持不变", () => {
    expect(buildWorkspaceSwitchPath("/market/templates?keyword=test", "200"))
      .toBe("/market/templates?keyword=test");
  });
});
