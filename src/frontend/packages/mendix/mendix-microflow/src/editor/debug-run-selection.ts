import type { MicroflowRunSession } from "../debug";

export interface DebugRunSelectionFocusIntent {
  runId: string;
  frameId?: string;
  objectId?: string;
}

export interface DebugRunSelectionOutcome {
  targetMicroflowId: string;
  focusIntent?: DebugRunSelectionFocusIntent;
}

export function resolveDebugRunSelectionOutcome(
  sourceMicroflowId: string,
  detail: MicroflowRunSession,
): DebugRunSelectionOutcome {
  const targetMicroflowId = detail.resourceId?.trim() || sourceMicroflowId;
  const firstTraceFrame = detail.trace[0];
  return {
    targetMicroflowId,
    focusIntent: firstTraceFrame
      ? {
          runId: detail.id,
          frameId: firstTraceFrame.id,
          objectId: firstTraceFrame.objectId,
        }
      : undefined,
  };
}
