import { selectWorkspacePath, workspaceHomePath } from "../app-paths";

export function resolveWorkspaceEntryTarget(workspaceIds: string[], lastWorkspaceId: string | null): string {
  if (lastWorkspaceId && workspaceIds.includes(lastWorkspaceId)) {
    return workspaceHomePath(lastWorkspaceId);
  }

  if (workspaceIds.length === 1) {
    return workspaceHomePath(workspaceIds[0]);
  }

  return selectWorkspacePath();
}
