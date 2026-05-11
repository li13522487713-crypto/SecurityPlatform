import type { MicroflowDebugSessionDto } from "../debug/step-debug-api";
import { resolveDeepestDebugMicroflowId, resolveDeepestDebugWsMicroflowId } from "../debug/debug-session-routing";
import type { DebugCallStackFrame, DebugWsNodeHighlight } from "../stores/debug-store";

export interface DebugRouteIntent {
  routeKey: string;
  targetMicroflowId: string;
}

export function buildWsDebugRouteIntent(input: {
  debugSessionId?: string;
  activeMicroflowId: string;
  callStack: DebugCallStackFrame[];
  nodeState: Pick<DebugWsNodeHighlight, "currentNodeId" | "currentSafePoint">;
}): DebugRouteIntent | undefined {
  const targetMicroflowId = resolveDeepestDebugWsMicroflowId(input.callStack);
  if (!targetMicroflowId || targetMicroflowId === input.activeMicroflowId) {
    return undefined;
  }
  return {
    targetMicroflowId,
    routeKey: [
      "ws",
      input.debugSessionId ?? "",
      input.activeMicroflowId,
      targetMicroflowId,
      input.nodeState.currentNodeId ?? "",
      input.nodeState.currentSafePoint ?? "",
      String(input.callStack.length),
    ].join("|"),
  };
}

export function buildSessionDebugRouteIntent(input: {
  session: MicroflowDebugSessionDto | undefined;
  sourceMicroflowId: string;
}): DebugRouteIntent | undefined {
  const session = input.session;
  if (!session) {
    return undefined;
  }
  const targetMicroflowId = resolveDeepestDebugMicroflowId(session, input.sourceMicroflowId);
  if (!targetMicroflowId || targetMicroflowId === input.sourceMicroflowId) {
    return undefined;
  }
  return {
    targetMicroflowId,
    routeKey: [
      session.id,
      input.sourceMicroflowId,
      targetMicroflowId,
      session.currentSafePoint?.nodeObjectId ?? session.currentNodeObjectId ?? "",
      session.currentSafePoint?.phase ?? session.pausePhase ?? "",
      session.lastUpdatedAt ?? "",
    ].join("|"),
  };
}
