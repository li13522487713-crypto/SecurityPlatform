import type { MicroflowTraceFrame } from "../debug/trace-types";

export function composeTraceFramesForRuntimePreview(input: {
  baseFrames: MicroflowTraceFrame[];
  wsCurrentNodeId?: string;
  wsCurrentBranchId?: string;
  sessionId?: string;
}): MicroflowTraceFrame[] {
  const wsCurrentNodeId = String(input.wsCurrentNodeId ?? "").trim();
  const wsCurrentBranchId = String(input.wsCurrentBranchId ?? "").trim();
  if (!wsCurrentNodeId) {
    return input.baseFrames;
  }
  const alreadyRunning = input.baseFrames.some(frame => frame.objectId === wsCurrentNodeId && frame.status === "running");
  if (alreadyRunning) {
    return input.baseFrames;
  }
  const syntheticRunningFrame: MicroflowTraceFrame = {
    id: `ws-live-${wsCurrentNodeId}`,
    runId: input.sessionId ?? "ws-live",
    objectId: wsCurrentNodeId,
    status: "running",
    startedAt: "1970-01-01T00:00:00.000Z",
    durationMs: 0,
    output: wsCurrentBranchId
      ? {
          branchTrace: [{
            flowId: wsCurrentBranchId,
            branchId: wsCurrentBranchId,
            selected: true,
            status: "completed",
          }],
        }
      : undefined,
  };
  return [...input.baseFrames, syntheticRunningFrame];
}
