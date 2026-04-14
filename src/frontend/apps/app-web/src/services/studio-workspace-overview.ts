import type { StudioWorkspaceOverview } from "@atlas/module-studio-react";
import { resolveAppInstanceId } from "./app-instance-context";
import { getOrganizationWorkspace } from "./api-org-management";

function createEmptyStudioWorkspaceOverview(appInstanceId: string | null): StudioWorkspaceOverview {
  return {
    appId: appInstanceId ?? "",
    memberCount: 0,
    roleCount: 0,
    departmentCount: 0,
    positionCount: 0,
    projectCount: 0,
    uncoveredMemberCount: 0,
    applications: []
  };
}

export async function getStudioWorkspaceOverview(appKey: string): Promise<StudioWorkspaceOverview> {
  const appInstanceId = await resolveAppInstanceId(appKey);
  if (!appInstanceId) {
    return createEmptyStudioWorkspaceOverview(null);
  }

  try {
    const workspace = await getOrganizationWorkspace(appInstanceId, { pageIndex: 1, pageSize: 8 });
    return {
      appId: workspace.appId,
      memberCount: workspace.members.total,
      roleCount: workspace.roles.length,
      departmentCount: workspace.departments.length,
      positionCount: workspace.positions.length,
      projectCount: workspace.projects.length,
      uncoveredMemberCount: workspace.roleGovernance.uncoveredMembers,
      applications: []
    };
  } catch {
    return createEmptyStudioWorkspaceOverview(appInstanceId);
  }
}
