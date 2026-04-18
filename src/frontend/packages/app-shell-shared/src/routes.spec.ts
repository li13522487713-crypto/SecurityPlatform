import { describe, expect, it } from "vitest";
import {
  orgWorkspacesPath,
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
    expect(signPath("/org/demo/workspaces")).toBe("/sign?redirect=%2Forg%2Fdemo%2Fworkspaces");
  });

  it("builds workspace list and develop paths", () => {
    expect(orgWorkspacesPath("tenant-1")).toBe("/org/tenant-1/workspaces");
    expect(orgWorkspaceDashboardPath("tenant-1", "100")).toBe("/org/tenant-1/workspaces/100/dashboard");
    expect(orgWorkspaceDevelopPath("tenant-1", "100")).toBe("/org/tenant-1/workspaces/100/develop");
    expect(orgWorkspaceChatPath("tenant-1", "100")).toBe("/org/tenant-1/workspaces/100/develop/chat");
    expect(orgWorkspaceModelConfigsPath("tenant-1", "100")).toBe("/org/tenant-1/workspaces/100/develop/model-configs");
    expect(orgWorkspaceAssistantToolsPath("tenant-1", "100")).toBe("/org/tenant-1/workspaces/100/develop/assistant-tools");
    expect(orgWorkspacePublishCenterPath("tenant-1", "100")).toBe("/org/tenant-1/workspaces/100/develop/publish-center");
    expect(orgWorkspaceDataPath("tenant-1", "100")).toBe("/org/tenant-1/workspaces/100/library/data");
    expect(orgWorkspaceVariablesPath("tenant-1", "100")).toBe("/org/tenant-1/workspaces/100/library/variables");
    expect(orgWorkspaceManagePath("tenant-1", "100")).toBe("/org/tenant-1/workspaces/100/manage");
    expect(orgWorkspaceManagePath("tenant-1", "100", "users")).toBe("/org/tenant-1/workspaces/100/manage/users");
    expect(orgWorkspaceSettingsPath("tenant-1", "100")).toBe("/org/tenant-1/workspaces/100/settings");
    expect(orgWorkspaceSettingsPath("tenant-1", "100", "members")).toBe("/org/tenant-1/workspaces/100/settings/members");
  });

  it("builds deep resource paths", () => {
    expect(orgWorkspaceAppDetailPath("tenant-1", "100", "200")).toBe("/org/tenant-1/workspaces/100/apps/200");
    expect(orgWorkspaceAgentDetailPath("tenant-1", "100", "300")).toBe("/org/tenant-1/workspaces/100/agents/300");
    expect(orgWorkspaceWorkflowsPath("tenant-1", "100")).toBe("/org/tenant-1/workspaces/100/workflows");
    expect(orgWorkspaceChatflowsPath("tenant-1", "100")).toBe("/org/tenant-1/workspaces/100/chatflows");
    expect(orgWorkspaceAppWorkflowPath("tenant-1", "100", "200", "400")).toBe("/org/tenant-1/workspaces/100/apps/200/workflows/400");
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
