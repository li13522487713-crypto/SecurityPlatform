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
