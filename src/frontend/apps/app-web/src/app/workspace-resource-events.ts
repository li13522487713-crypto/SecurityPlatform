export type WorkspaceCreatedResourceType = "app" | "agent";

export interface WorkspaceResourceCreatedEventDetail {
  workspaceId: string;
  resourceType: WorkspaceCreatedResourceType;
  resourceId: string;
  resourceName: string;
}

const WORKSPACE_RESOURCE_CREATED_EVENT = "atlas:workspace-resource-created";
const PENDING_CREATED_RESOURCES_KEY = "atlas_pending_created_workspace_resources";

function readPendingCreatedResources(): WorkspaceResourceCreatedEventDetail[] {
  if (typeof window === "undefined") {
    return [];
  }
  try {
    const raw = window.sessionStorage.getItem(PENDING_CREATED_RESOURCES_KEY);
    if (!raw) {
      return [];
    }
    const parsed = JSON.parse(raw);
    return Array.isArray(parsed) ? parsed as WorkspaceResourceCreatedEventDetail[] : [];
  } catch {
    return [];
  }
}

function writePendingCreatedResources(items: WorkspaceResourceCreatedEventDetail[]): void {
  if (typeof window === "undefined") {
    return;
  }
  try {
    window.sessionStorage.setItem(PENDING_CREATED_RESOURCES_KEY, JSON.stringify(items));
  } catch {
    // ignore storage failure
  }
}

export function notifyWorkspaceResourceCreated(detail: WorkspaceResourceCreatedEventDetail): void {
  if (typeof window === "undefined") {
    return;
  }
  const pending = readPendingCreatedResources();
  pending.push(detail);
  writePendingCreatedResources(pending);
  window.dispatchEvent(new CustomEvent<WorkspaceResourceCreatedEventDetail>(WORKSPACE_RESOURCE_CREATED_EVENT, { detail }));
}

export function consumeWorkspaceResourceCreated(workspaceId: string): WorkspaceResourceCreatedEventDetail[] {
  const pending = readPendingCreatedResources();
  if (pending.length === 0) {
    return [];
  }
  const matched = pending.filter(item => item.workspaceId === workspaceId);
  const rest = pending.filter(item => item.workspaceId !== workspaceId);
  writePendingCreatedResources(rest);
  return matched;
}

export function subscribeWorkspaceResourceCreated(
  handler: (detail: WorkspaceResourceCreatedEventDetail) => void
): () => void {
  if (typeof window === "undefined") {
    return () => undefined;
  }
  const listener = (event: Event) => {
    const customEvent = event as CustomEvent<WorkspaceResourceCreatedEventDetail>;
    if (customEvent.detail) {
      handler(customEvent.detail);
    }
  };
  window.addEventListener(WORKSPACE_RESOURCE_CREATED_EVENT, listener as EventListener);
  return () => window.removeEventListener(WORKSPACE_RESOURCE_CREATED_EVENT, listener as EventListener);
}
