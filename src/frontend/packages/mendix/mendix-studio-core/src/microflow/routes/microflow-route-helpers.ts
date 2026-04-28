export function createMicroflowEditorPath(resourceId: string): string {
  return `/microflow/${encodeURIComponent(resourceId)}/editor`;
}

export function createWorkspaceMicroflowEditorPath(workspaceId: string, resourceId: string): string {
  return `/space/${encodeURIComponent(workspaceId)}/microflows/${encodeURIComponent(resourceId)}`;
}

export function createMicroflowLibraryPath(workspaceId?: string): string {
  return workspaceId ? `/space/${encodeURIComponent(workspaceId)}/library?tab=microflow` : "/microflow";
}
