import { beforeEach, describe, expect, it, vi } from "vitest";
import { getStudioWorkspaceOverview } from "./studio-workspace-overview";

const { resolveAppInstanceId, getOrganizationWorkspace } = vi.hoisted(() => ({
  resolveAppInstanceId: vi.fn<(appKey: string) => Promise<string | null>>(),
  getOrganizationWorkspace: vi.fn()
}));

vi.mock("./app-instance-context", () => ({
  resolveAppInstanceId
}));

vi.mock("./api-org-management", () => ({
  getOrganizationWorkspace
}));

describe("studio-workspace-overview", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("returns organization overview data when the app instance resolves successfully", async () => {
    resolveAppInstanceId.mockResolvedValue("101");
    getOrganizationWorkspace.mockResolvedValue({
      appId: "101",
      members: { total: 3 },
      roles: [{ id: "1" }, { id: "2" }],
      departments: [{ id: "10" }],
      positions: [{ id: "20" }, { id: "21" }],
      projects: [{ id: "30" }],
      roleGovernance: { uncoveredMembers: 1 }
    });

    await expect(getStudioWorkspaceOverview("app-alpha")).resolves.toEqual({
      appId: "101",
      memberCount: 3,
      roleCount: 2,
      departmentCount: 1,
      positionCount: 2,
      projectCount: 1,
      uncoveredMemberCount: 1,
      applications: []
    });
    expect(getOrganizationWorkspace).toHaveBeenCalledWith("101", { pageIndex: 1, pageSize: 8 });
  });

  it("returns a stable zero overview when no app instance is available", async () => {
    resolveAppInstanceId.mockResolvedValue(null);

    await expect(getStudioWorkspaceOverview("app-alpha")).resolves.toEqual({
      appId: "",
      memberCount: 0,
      roleCount: 0,
      departmentCount: 0,
      positionCount: 0,
      projectCount: 0,
      uncoveredMemberCount: 0,
      applications: []
    });
    expect(getOrganizationWorkspace).not.toHaveBeenCalled();
  });

  it("degrades to a zero overview when organization workspace loading fails", async () => {
    resolveAppInstanceId.mockResolvedValue("101");
    getOrganizationWorkspace.mockRejectedValue(new Error("应用实例不存在。"));

    await expect(getStudioWorkspaceOverview("app-alpha")).resolves.toEqual({
      appId: "101",
      memberCount: 0,
      roleCount: 0,
      departmentCount: 0,
      positionCount: 0,
      projectCount: 0,
      uncoveredMemberCount: 0,
      applications: []
    });
  });
});
