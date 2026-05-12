import type { MicroflowDebugSessionDto } from "../debug/step-debug-api";
import { resolveDeepestDebugMicroflowId, resolveDeepestDebugWsMicroflowId } from "../debug/debug-session-routing";
import type { DebugCallStackFrame, DebugWsNodeHighlight } from "../stores/debug-store";

export interface DebugRouteIntent {
  routeKey: string;
  targetMicroflowId: string;
}

export type DebugCallStackClickIntent =
  | { kind: "focus-node"; nodeId: string }
  | { kind: "open-microflow"; microflowId: string };

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

export function resolveCallStackFrameClickIntent(input: {
  activeMicroflowId: string;
  activeNodeId?: string;
  frame: {
    microflowId?: string;
    callerNodeId?: string;
  };
  visibleObjectIds: Iterable<string>;
}): DebugCallStackClickIntent | undefined {
  const callerNodeId = typeof input.frame.callerNodeId === "string" ? input.frame.callerNodeId.trim() : "";
  if (callerNodeId) {
    const visibleObjectIds = new Set(input.visibleObjectIds);
    if (visibleObjectIds.has(callerNodeId)) {
      return { kind: "focus-node", nodeId: callerNodeId };
    }
  }
  const microflowId = typeof input.frame.microflowId === "string" ? input.frame.microflowId.trim() : "";
  const activeNodeId = typeof input.activeNodeId === "string" ? input.activeNodeId.trim() : "";
  if (microflowId && microflowId === input.activeMicroflowId && activeNodeId) {
    const visibleObjectIds = new Set(input.visibleObjectIds);
    if (visibleObjectIds.has(activeNodeId)) {
      return { kind: "focus-node", nodeId: activeNodeId };
    }
  }
  if (microflowId && microflowId !== input.activeMicroflowId) {
    return { kind: "open-microflow", microflowId };
  }
  return undefined;
}
