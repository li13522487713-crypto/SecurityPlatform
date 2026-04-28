import type { MicroflowRunSession, MicroflowTraceFrameStatus } from "./trace-types";

export interface MicroflowRuntimeHighlightState {
  nodes: Record<string, {
    status: "idle" | MicroflowTraceFrameStatus;
    lastFrameId?: string;
    durationMs?: number;
    errorMessage?: string;
  }>;
  flows: Record<string, {
    visited: boolean;
    skipped?: boolean;
    errorHandlerVisited?: boolean;
    selectedCase?: boolean;
  }>;
  activeObjectId?: string;
  activeFlowId?: string;
}

export function buildRuntimeHighlightState(runSession?: MicroflowRunSession, activeFrameId?: string): MicroflowRuntimeHighlightState {
  const state: MicroflowRuntimeHighlightState = { nodes: {}, flows: {} };
  if (!runSession) {
    return state;
  }
  const visitedBranchSources = new Map<string, Set<string>>();
  const activeFrame = activeFrameId ? runSession.trace.find(frame => frame.id === activeFrameId) : undefined;
  for (const frame of runSession.trace) {
    const current = state.nodes[frame.objectId];
    const shouldReplace = !current || frame.status === "failed" || current.status !== "failed";
    if (shouldReplace) {
      state.nodes[frame.objectId] = {
        status: frame.status,
        lastFrameId: frame.id,
        durationMs: frame.durationMs,
        errorMessage: frame.error?.message,
      };
    }
    if (frame.outgoingFlowId) {
      const flowState = state.flows[frame.outgoingFlowId] ?? { visited: false };
      state.flows[frame.outgoingFlowId] = {
        ...flowState,
        visited: true,
        errorHandlerVisited: Boolean(frame.errorHandlerVisited ?? frame.error) || flowState.errorHandlerVisited,
        selectedCase: Boolean(frame.selectedCaseValue) || flowState.selectedCase,
      };
      const sourceVisited = visitedBranchSources.get(frame.objectId) ?? new Set<string>();
      sourceVisited.add(frame.outgoingFlowId);
      visitedBranchSources.set(frame.objectId, sourceVisited);
    }
  }
  for (const frame of runSession.trace) {
    if (!frame.outgoingFlowId || !frame.selectedCaseValue) {
      continue;
    }
    for (const candidate of runSession.trace.filter(item => item.objectId === frame.objectId)) {
      if (candidate.outgoingFlowId && candidate.outgoingFlowId !== frame.outgoingFlowId) {
        state.flows[candidate.outgoingFlowId] = { ...(state.flows[candidate.outgoingFlowId] ?? { visited: false }), skipped: true };
      }
    }
  }
  state.activeObjectId = activeFrame?.objectId;
  state.activeFlowId = activeFrame?.outgoingFlowId ?? activeFrame?.incomingFlowId;
  return state;
}
