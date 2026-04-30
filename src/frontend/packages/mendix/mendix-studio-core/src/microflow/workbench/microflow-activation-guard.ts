import type { StudioWorkbenchTab } from "../../store";

export interface ResolveMicroflowActivationInput {
  targetMicroflowId: string;
  activeWorkbenchTabId?: string;
  workbenchTabs: StudioWorkbenchTab[];
  dirtyByWorkbenchTabId: Record<string, boolean>;
}

export type ResolveMicroflowActivationResult =
  | { kind: "activate" }
  | { kind: "confirm-dirty"; activeTabId: string };

export function resolveMicroflowActivation(input: ResolveMicroflowActivationInput): ResolveMicroflowActivationResult {
  const activeTab = input.activeWorkbenchTabId
    ? input.workbenchTabs.find(tab => tab.id === input.activeWorkbenchTabId)
    : undefined;
  if (activeTab?.kind === "microflow" && activeTab.microflowId === input.targetMicroflowId) {
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
