import { workspaceHomePath } from "../app-paths";

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
      target: workspaceHomePath(lastWorkspaceId)
    };
  }

  if (workspaceIds.length > 0) {
    return {
      workspaceId: workspaceIds[0],
      target: workspaceHomePath(workspaceIds[0])
    };
  }

  return null;
}
