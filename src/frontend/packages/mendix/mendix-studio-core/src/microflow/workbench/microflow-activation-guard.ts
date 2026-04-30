import type { StudioWorkbenchTab } from "../../store";
import { getResourceWorkbenchTabId, type StudioWorkbenchTabKind } from "../../store";

export interface WorkbenchResourceActivationTarget {
  kind: StudioWorkbenchTabKind;
  resourceId: string;
}

export interface ResolveMicroflowActivationInput {
  targetMicroflowId: string;
  activeWorkbenchTabId?: string;
  workbenchTabs: StudioWorkbenchTab[];
  dirtyByWorkbenchTabId: Record<string, boolean>;
}

export type ResolveMicroflowActivationResult =
  | { kind: "activate" }
  | { kind: "confirm-dirty"; activeTabId: string };

export interface ResolveWorkbenchResourceActivationInput {
  target: WorkbenchResourceActivationTarget;
  activeWorkbenchTabId?: string;
  workbenchTabs: StudioWorkbenchTab[];
  dirtyByWorkbenchTabId: Record<string, boolean>;
}

export function resolveMicroflowActivation(input: ResolveMicroflowActivationInput): ResolveMicroflowActivationResult {
  return resolveWorkbenchResourceActivation({
    target: { kind: "microflow", resourceId: input.targetMicroflowId },
    activeWorkbenchTabId: input.activeWorkbenchTabId,
    workbenchTabs: input.workbenchTabs,
    dirtyByWorkbenchTabId: input.dirtyByWorkbenchTabId,
  });
}

export function resolveWorkbenchResourceActivation(input: ResolveWorkbenchResourceActivationInput): ResolveMicroflowActivationResult {
  const targetTabId = getResourceWorkbenchTabId(input.target.kind, input.target.resourceId);
  if (input.activeWorkbenchTabId === targetTabId) {
    return { kind: "activate" };
  }

  const activeTab = input.activeWorkbenchTabId
    ? input.workbenchTabs.find(tab => tab.id === input.activeWorkbenchTabId)
    : undefined;
  const activeResourceId = activeTab?.kind === "microflow"
    ? activeTab.microflowId ?? activeTab.resourceId
    : activeTab?.resourceId;
  if (activeTab?.kind === input.target.kind && activeResourceId === input.target.resourceId) {
    return { kind: "activate" };
  }
  if (input.activeWorkbenchTabId && input.dirtyByWorkbenchTabId[input.activeWorkbenchTabId]) {
    return {
      kind: "confirm-dirty",
      activeTabId: input.activeWorkbenchTabId,
    };
  }
  return { kind: "activate" };
}
