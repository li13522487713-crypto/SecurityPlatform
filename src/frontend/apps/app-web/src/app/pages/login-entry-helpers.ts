import { workspaceProjectsPath } from "../app-paths";

export interface WorkspaceEntryResolution {
  workspaceId: string;
  target: string;
}

export function resolveWorkspaceEntryTarget(
  workspaceIds: string[],
  lastWorkspaceId: string | null
): WorkspaceEntryResolution | null {
  if (lastWorkspaceId && workspaceIds.includes(lastWorkspaceId)) {
    return {
      workspaceId: lastWorkspaceId,
      target: workspaceProjectsPath(lastWorkspaceId)
    };
  }

  if (workspaceIds.length > 0) {
    return {
      workspaceId: workspaceIds[0],
      target: workspaceProjectsPath(workspaceIds[0])
    };
  }

  return null;
}
