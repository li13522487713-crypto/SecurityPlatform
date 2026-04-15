import { describe, expect, it } from "vitest";
import {
  orgWorkspacesPath,
  orgWorkspaceDevelopPath,
  orgWorkspaceAppDetailPath,
  orgWorkspaceAgentDetailPath,
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
    expect(orgWorkspaceDevelopPath("tenant-1", "100")).toBe("/org/tenant-1/workspaces/100/develop");
  });

  it("builds deep resource paths", () => {
    expect(orgWorkspaceAppDetailPath("tenant-1", "100", "200")).toBe("/org/tenant-1/workspaces/100/apps/200");
    expect(orgWorkspaceAgentDetailPath("tenant-1", "100", "300")).toBe("/org/tenant-1/workspaces/100/agents/300");
    expect(orgWorkspaceWorkflowsPath("tenant-1", "100")).toBe("/org/tenant-1/workspaces/100/workflows");
    expect(orgWorkspaceChatflowsPath("tenant-1", "100")).toBe("/org/tenant-1/workspaces/100/chatflows");
    expect(orgWorkspaceAppWorkflowPath("tenant-1", "100", "200", "400")).toBe("/org/tenant-1/workspaces/100/apps/200/workflows/400");
  });
});
