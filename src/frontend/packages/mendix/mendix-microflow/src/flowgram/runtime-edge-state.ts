import type { MicroflowTraceFrame } from "../debug/trace-types";
import { extractGatewayBranchTrace } from "../debug/trace-types";
import type { FlowGramMicroflowEdgeData } from "./FlowGramMicroflowTypes";

function runtimeLineStateFromTraceStatus(status: MicroflowTraceFrame["status"] | undefined): FlowGramMicroflowEdgeData["runtimeState"] {
  if (!status) {
    return "idle";
  }
  if (status === "success") {
    return "visited";
  }
  if (status === "failed") {
    return "failed";
  }
  if (status === "skipped") {
    return "skipped";
  }
  if (status === "running") {
    return "running";
  }
  return "idle";
}

function runtimeLineStateFromBranchTraceStatus(status: ReturnType<typeof extractGatewayBranchTrace>[number]["status"]): FlowGramMicroflowEdgeData["runtimeState"] {
  if (status === "failed") {
    return "failed";
  }
  if (status === "skipped") {
    return "skipped";
  }
  return "visited";
}

const runtimeLineStatePriority: Record<Exclude<FlowGramMicroflowEdgeData["runtimeState"], undefined>, number> = {
  idle: 0,
  visited: 1,
  running: 2,
  skipped: 3,
  failed: 4,
  errorHandlerVisited: 4,
  selectedCase: 5,
};

function mergeRuntimeLineState(
  current: FlowGramMicroflowEdgeData["runtimeState"] | undefined,
  next: FlowGramMicroflowEdgeData["runtimeState"],
): FlowGramMicroflowEdgeData["runtimeState"] {
  const currentState = current ?? "idle";
  const nextPriority = runtimeLineStatePriority[next ?? "idle"];
  const currentPriority = runtimeLineStatePriority[currentState];
  return nextPriority >= currentPriority ? next : currentState;
}

export function deriveEdgeRuntimeStateByFlowId(runtimeTrace: MicroflowTraceFrame[]): Map<string, FlowGramMicroflowEdgeData["runtimeState"]> {
  const edgeRuntimeByFlowId = new Map<string, FlowGramMicroflowEdgeData["runtimeState"]>();
  const latestRunId = runtimeTrace[runtimeTrace.length - 1]?.runId;
  const scopedTrace = latestRunId
    ? runtimeTrace.filter(frame => frame.runId === latestRunId)
    : runtimeTrace;
  const assign = (edgeId: string | undefined, state: FlowGramMicroflowEdgeData["runtimeState"] | undefined) => {
    if (!edgeId || !state) {
      return;
    }
    const key = String(edgeId);
    edgeRuntimeByFlowId.set(key, mergeRuntimeLineState(edgeRuntimeByFlowId.get(key), state));
  };
  for (const frame of scopedTrace) {
    const baseState = runtimeLineStateFromTraceStatus(frame.status);
    assign(frame.incomingFlowId, baseState);
    assign(frame.outgoingFlowId, baseState);
    assign(frame.incomingEdgeId, baseState);
    assign(frame.outgoingEdgeId, baseState);
    for (const branch of extractGatewayBranchTrace(frame)) {
      const branchState: FlowGramMicroflowEdgeData["runtimeState"] = branch.selected
        ? "selectedCase"
        : runtimeLineStateFromBranchTraceStatus(branch.status);
      assign(branch.flowId, branchState);
      assign(branch.branchId, branchState);
    }
  }
  return edgeRuntimeByFlowId;
}
