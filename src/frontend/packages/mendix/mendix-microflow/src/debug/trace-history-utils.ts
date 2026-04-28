import type { MicroflowRunSession, MicroflowTraceFrame } from "./trace-types";
import type { MicroflowRunHistoryItem, MicroflowRunHistoryStatus } from "../runtime-adapter/types";

export interface MicroflowExecutionPathItem {
  frame: MicroflowTraceFrame;
  runId: string;
  microflowId: string;
  microflowPath: string[];
  callDepth: number;
  sequence: number;
}

export function normalizeRunHistoryStatus(rawStatus: string, errorCode?: string): MicroflowRunHistoryStatus {
  if (rawStatus === "success") {
    return "success";
  }
  if (rawStatus === "cancelled") {
    return "cancelled";
  }
  if (errorCode && errorCode.toUpperCase().includes("UNSUPPORTED")) {
    return "unsupported";
  }
  return "failed";
}

export function buildRunHistoryItemFromSession(microflowId: string, session: MicroflowRunSession): MicroflowRunHistoryItem {
  const completedAt = session.endedAt;
  const durationMs = completedAt
    ? Math.max(0, new Date(completedAt).getTime() - new Date(session.startedAt).getTime())
    : 0;
  const status = normalizeRunHistoryStatus(session.status, session.error?.code);
  return {
    runId: session.id,
    microflowId,
    status,
    durationMs,
    startedAt: session.startedAt,
    completedAt,
    errorMessage: session.error?.message,
    summary: status === "success" ? "Run succeeded" : status === "unsupported" ? "Run failed on unsupported action" : status === "cancelled" ? "Run cancelled" : "Run failed",
  };
}

export function buildExecutionPath(session?: MicroflowRunSession): MicroflowExecutionPathItem[] {
  if (!session) {
    return [];
  }
  const result: MicroflowExecutionPathItem[] = [];
  const walk = (current: MicroflowRunSession, path: string[], depth: number) => {
    const nextPath = [...path, current.resourceId || current.schemaId || "unknown"];
    current.trace.forEach((frame, index) => {
      result.push({
        frame,
        runId: current.id,
        microflowId: frame.microflowId || current.resourceId || current.schemaId,
        microflowPath: nextPath,
        callDepth: frame.callDepth ?? depth,
        sequence: index + 1,
      });
    });
    current.childRuns?.forEach(child => walk(child, nextPath, depth + 1));
  };
  walk(session, [], session.callDepth ?? 0);
  return result;
}

export function filterNodeResultsByMicroflowId(session: MicroflowRunSession | undefined, microflowId: string): MicroflowTraceFrame[] {
  if (!session) {
    return [];
  }
  return buildExecutionPath(session)
    .filter(item => !item.frame.microflowId || item.frame.microflowId === microflowId)
    .map(item => item.frame);
}
