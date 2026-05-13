import type { MicroflowCaseValue } from "../schema";

export type MicroflowRuntimeRunStatus = "idle" | "starting" | "running" | "completed" | "failed" | "cancelled";
export type RuntimeNodeStatus = "idle" | "queued" | "running" | "succeeded" | "failed" | "skipped" | "paused";
export type RuntimeFlowStatus = "idle" | "visited" | "running" | "selectedCase" | "skipped" | "failed" | "errorHandlerVisited";

export interface RuntimeValueSummary {
  name: string;
  type?: string;
  preview?: string;
}

export interface RuntimeVariableDeltaSummary {
  kind: "added" | "changed" | "removed";
  name: string;
  type?: string;
  afterPreview?: string;
}

export interface MicroflowRuntimeWsEvent {
  eventId?: string;
  runId: string;
  sequence: number;
  type: string;
  timestamp: string;
  objectId?: string;
  flowId?: string;
  payload?: unknown;
}

export interface RuntimeNodeOverlay {
  objectId: string;
  status: RuntimeNodeStatus;
  inputSummary?: RuntimeValueSummary[];
  outputSummary?: RuntimeValueSummary[];
  variableDeltaSummary?: RuntimeVariableDeltaSummary[];
  conditionExpression?: string;
  evaluatedValuePreview?: string;
  selectedCaseLabel?: string;
  selectedCaseValue?: MicroflowCaseValue;
  loopIteration?: {
    index?: number;
    total?: number;
    iteratorName?: string;
    iteratorValuePreview?: string;
    control?: "continue" | "break";
  };
  gatewaySummary?: {
    totalBranches?: number;
    completedBranches?: number;
    skippedBranches?: number;
    failedBranches?: number;
    mergeResultPreview?: string;
  };
  durationMs?: number;
  error?: {
    code?: string;
    message?: string;
  };
  startedAt?: string;
  endedAt?: string;
}

export interface RuntimeFlowOverlay {
  flowId: string;
  status: RuntimeFlowStatus;
  selectedCaseValue?: MicroflowCaseValue;
  visitedAt?: string;
}

export interface MicroflowRuntimeOverlayState {
  runId?: string;
  currentObjectId?: string;
  status: MicroflowRuntimeRunStatus;
  nodeOverlays: Record<string, RuntimeNodeOverlay>;
  flowOverlays: Record<string, RuntimeFlowOverlay>;
  events: MicroflowRuntimeWsEvent[];
  lastSequence?: number;
  result?: unknown;
}

export interface MicroflowRuntimeOverlaySnapshot {
  runId: string;
  status: string;
  currentObjectId?: string;
  lastSequence: number;
  result?: unknown;
  nodeOverlays: Record<string, RuntimeNodeOverlay>;
  flowOverlays: Record<string, RuntimeFlowOverlay>;
  events?: MicroflowRuntimeWsEvent[];
}

const MAX_RUNTIME_EVENTS = 2400;

export function createRuntimeOverlayState(runId?: string): MicroflowRuntimeOverlayState {
  return {
    runId,
    currentObjectId: undefined,
    status: runId ? "starting" : "idle",
    nodeOverlays: {},
    flowOverlays: {},
    events: [],
    lastSequence: undefined,
    result: undefined,
  };
}

export function applyRuntimeOverlaySnapshot(
  previous: MicroflowRuntimeOverlayState | undefined,
  snapshot: MicroflowRuntimeOverlaySnapshot | undefined,
): MicroflowRuntimeOverlayState {
  if (!snapshot) {
    return previous ?? createRuntimeOverlayState();
  }
  return {
    runId: snapshot.runId,
    currentObjectId: snapshot.currentObjectId,
    status: normalizeRunStatus(snapshot.status),
    nodeOverlays: snapshot.nodeOverlays ?? {},
    flowOverlays: snapshot.flowOverlays ?? {},
    events: (snapshot.events ?? []).slice(-MAX_RUNTIME_EVENTS),
    lastSequence: snapshot.lastSequence,
    result: snapshot.result ?? previous?.result,
  };
}

export function applyRuntimeOverlayEvent(
  previous: MicroflowRuntimeOverlayState | undefined,
  event: MicroflowRuntimeWsEvent,
): MicroflowRuntimeOverlayState {
  const base = previous ?? createRuntimeOverlayState(event.runId);
  if (typeof event.sequence === "number" && typeof base.lastSequence === "number" && event.sequence <= base.lastSequence) {
    return base;
  }

  const state: MicroflowRuntimeOverlayState = {
    ...base,
    runId: event.runId || base.runId,
    currentObjectId: event.objectId ?? base.currentObjectId,
    events: [...base.events, event].slice(-MAX_RUNTIME_EVENTS),
    lastSequence: Math.max(base.lastSequence ?? 0, event.sequence ?? 0),
    nodeOverlays: { ...base.nodeOverlays },
    flowOverlays: { ...base.flowOverlays },
  };

  const payload = asRecord(event.payload);
  switch (event.type) {
    case "run.started":
      state.status = "running";
      break;
    case "run.completed":
      state.status = "completed";
      state.result = payload?.result;
      patchNode(state, state.currentObjectId, node => {
        const resultPreview = summarizePreview(payload?.result);
        return {
          ...node,
          status: node.status === "failed" ? "failed" : "succeeded",
          durationMs: asNumber(payload?.durationMs) ?? node.durationMs,
          outputSummary: mergeResultOutput(node.outputSummary, resultPreview),
        };
      });
      break;
    case "run.failed":
      state.status = "failed";
      patchNode(state, state.currentObjectId, node => ({
        ...node,
        status: "failed",
      }));
      break;
    case "node.started":
      patchNode(state, event.objectId, node => ({
        ...node,
        status: "running",
        startedAt: event.timestamp,
      }));
      break;
    case "node.inputResolved":
      patchNode(state, event.objectId, node => ({
        ...node,
        inputSummary: normalizeValueSummaries(payload?.inputSummary),
      }));
      break;
    case "node.outputProduced":
      patchNode(state, event.objectId, node => ({
        ...node,
        outputSummary: normalizeValueSummaries(payload?.outputSummary),
        variableDeltaSummary: normalizeDeltaSummaries(payload?.variableDeltaSummary),
      }));
      break;
    case "node.completed":
      patchNode(state, event.objectId, node => ({
        ...node,
        status: payload?.status === "skipped" ? "skipped" : "succeeded",
        durationMs: asNumber(payload?.durationMs),
        endedAt: event.timestamp,
      }));
      break;
    case "node.failed":
      patchNode(state, event.objectId, node => ({
        ...node,
        status: "failed",
        durationMs: asNumber(payload?.durationMs),
        endedAt: event.timestamp,
        error: {
          code: asString(asRecord(payload?.error)?.code),
          message: asString(asRecord(payload?.error)?.message) ?? asString(payload?.message),
        },
      }));
      break;
    case "edge.visited":
      patchFlow(state, event.flowId, flow => ({
        ...flow,
        status: "visited",
        visitedAt: event.timestamp,
      }));
      break;
    case "edge.running":
      patchFlow(state, event.flowId, flow => ({
        ...flow,
        status: "running",
        visitedAt: event.timestamp,
      }));
      break;
    case "edge.failed":
      patchFlow(state, event.flowId, flow => ({
        ...flow,
        status: "failed",
        visitedAt: event.timestamp,
      }));
      break;
    case "edge.errorHandlerVisited":
      patchFlow(state, event.flowId, flow => ({
        ...flow,
        status: "errorHandlerVisited",
        visitedAt: event.timestamp,
      }));
      break;
    case "branch.selected":
      patchNode(state, event.objectId, node => ({
        ...node,
        conditionExpression: asString(payload?.conditionExpression) ?? node.conditionExpression,
        evaluatedValuePreview: summarizePreview(payload?.evaluatedValue) ?? node.evaluatedValuePreview,
        selectedCaseLabel: asString(payload?.selectedCaseLabel) ?? node.selectedCaseLabel,
        selectedCaseValue: asCaseValue(payload?.selectedCaseValue) ?? node.selectedCaseValue,
      }));
      patchFlow(state, event.flowId, flow => ({
        ...flow,
        status: "selectedCase",
        selectedCaseValue: asCaseValue(payload?.selectedCaseValue),
        visitedAt: event.timestamp,
      }));
      for (const skippedFlowId of asStringArray(payload?.skippedFlowIds)) {
        patchFlow(state, skippedFlowId, flow => ({
          ...flow,
          status: "skipped",
          visitedAt: event.timestamp,
        }));
      }
      break;
    case "branch.skipped":
      patchFlow(state, event.flowId, flow => ({
        ...flow,
        status: "skipped",
        visitedAt: event.timestamp,
      }));
      break;
    case "loop.iteration.started":
    case "loop.iteration.completed":
      patchNode(state, event.objectId, node => ({
        ...node,
        loopIteration: {
          index: asNumber(payload?.index),
          total: asNumber(payload?.total),
          iteratorName: asString(payload?.iteratorName),
          iteratorValuePreview: asString(payload?.iteratorValuePreview),
          control: node.loopIteration?.control,
        },
      }));
      break;
    case "loop.break":
      patchNode(state, event.objectId, node => ({
        ...node,
        loopIteration: {
          ...node.loopIteration,
          control: "break",
        },
      }));
      break;
    case "loop.continue":
      patchNode(state, event.objectId, node => ({
        ...node,
        loopIteration: {
          ...node.loopIteration,
          control: "continue",
        },
      }));
      break;
    case "gateway.branch.completed":
      patchFlow(state, event.flowId, flow => ({
        ...flow,
        status: normalizeFlowStatus(asString(payload?.status), asBoolean(payload?.selected)),
        visitedAt: event.timestamp,
      }));
      break;
    case "gateway.branch.started":
      patchFlow(state, event.flowId, flow => ({
        ...flow,
        status: "running",
        visitedAt: event.timestamp,
      }));
      break;
    case "gateway.merge.completed":
      patchNode(state, event.objectId, node => ({
        ...node,
        gatewaySummary: {
          totalBranches: asNumber(payload?.totalBranches),
          completedBranches: asNumber(payload?.completedBranches),
          skippedBranches: asNumber(payload?.skippedBranches),
          failedBranches: asNumber(payload?.failedBranches),
          mergeResultPreview: asString(payload?.mergeResultPreview),
        },
      }));
      break;
    case "gateway.merge.failed":
      patchNode(state, event.objectId, node => ({
        ...node,
        status: "failed",
        gatewaySummary: {
          totalBranches: asNumber(payload?.totalBranches),
          completedBranches: asNumber(payload?.completedBranches),
          skippedBranches: asNumber(payload?.skippedBranches),
          failedBranches: asNumber(payload?.failedBranches),
          mergeResultPreview: asString(payload?.mergeResultPreview),
        },
        error: {
          code: asString(payload?.code) ?? "MF_GATEWAY_MERGE_FAILED",
          message: asString(payload?.message) ?? "Gateway merge failed.",
        },
      }));
      break;
    case "variable.delta":
      patchNode(state, event.objectId, node => ({
        ...node,
        variableDeltaSummary: normalizeDeltaSummaries(payload?.variableDeltaSummary),
      }));
      break;
    case "heartbeat":
    default:
      break;
  }

  return state;
}

function patchNode(
  state: MicroflowRuntimeOverlayState,
  objectId: string | undefined,
  updater: (current: RuntimeNodeOverlay) => RuntimeNodeOverlay,
): void {
  if (!objectId) {
    return;
  }
  const current = state.nodeOverlays[objectId] ?? { objectId, status: "idle" as RuntimeNodeStatus };
  state.nodeOverlays[objectId] = updater(current);
}

function patchFlow(
  state: MicroflowRuntimeOverlayState,
  flowId: string | undefined,
  updater: (current: RuntimeFlowOverlay) => RuntimeFlowOverlay,
): void {
  if (!flowId) {
    return;
  }
  const current = state.flowOverlays[flowId] ?? { flowId, status: "idle" as RuntimeFlowStatus };
  state.flowOverlays[flowId] = updater(current);
}

function normalizeRunStatus(status: string | undefined): MicroflowRuntimeRunStatus {
  const normalized = (status ?? "").toLowerCase();
  if (normalized === "running" || normalized === "starting" || normalized === "queued") {
    return "running";
  }
  if (normalized === "completed" || normalized === "succeeded" || normalized === "success") {
    return "completed";
  }
  if (normalized === "cancelled") {
    return "cancelled";
  }
  if (normalized === "failed" || normalized === "error") {
    return "failed";
  }
  return "idle";
}

function normalizeFlowStatus(status: string | undefined, selected?: boolean): RuntimeFlowStatus {
  if (selected) {
    return "selectedCase";
  }
  const normalized = (status ?? "").toLowerCase();
  if (normalized === "skipped") {
    return "skipped";
  }
  if (normalized === "failed") {
    return "failed";
  }
  if (normalized === "running") {
    return "running";
  }
  if (normalized === "errorhandlervisited") {
    return "errorHandlerVisited";
  }
  return "visited";
}

function normalizeValueSummaries(value: unknown): RuntimeValueSummary[] | undefined {
  if (!Array.isArray(value)) {
    return undefined;
  }
  return value
    .map(item => {
      const row = asRecord(item);
      const name = asString(row?.name);
      if (!name) {
        return undefined;
      }
      return {
        name,
        type: asString(row?.type),
        preview: asString(row?.preview),
      } satisfies RuntimeValueSummary;
    })
    .filter(Boolean) as RuntimeValueSummary[];
}

function normalizeDeltaSummaries(value: unknown): RuntimeVariableDeltaSummary[] | undefined {
  if (!Array.isArray(value)) {
    return undefined;
  }
  return value
    .map(item => {
      const row = asRecord(item);
      const name = asString(row?.name);
      const kind = asString(row?.kind);
      if (!name || (kind !== "added" && kind !== "changed" && kind !== "removed")) {
        return undefined;
      }
      return {
        name,
        kind,
        type: asString(row?.type),
        afterPreview: asString(row?.afterPreview),
      } satisfies RuntimeVariableDeltaSummary;
    })
    .filter(Boolean) as RuntimeVariableDeltaSummary[];
}

function asRecord(value: unknown): Record<string, unknown> | undefined {
  if (!value || typeof value !== "object") {
    return undefined;
  }
  return value as Record<string, unknown>;
}

function asString(value: unknown): string | undefined {
  return typeof value === "string" ? value : undefined;
}

function asNumber(value: unknown): number | undefined {
  if (typeof value === "number" && Number.isFinite(value)) {
    return value;
  }
  if (typeof value === "string" && value.trim()) {
    const parsed = Number(value);
    if (Number.isFinite(parsed)) {
      return parsed;
    }
  }
  return undefined;
}

function asBoolean(value: unknown): boolean | undefined {
  return typeof value === "boolean" ? value : undefined;
}

function asStringArray(value: unknown): string[] {
  if (!Array.isArray(value)) {
    return [];
  }
  return value.filter(item => typeof item === "string");
}

function asCaseValue(value: unknown): MicroflowCaseValue | undefined {
  if (!value || typeof value !== "object") {
    return undefined;
  }
  return value as MicroflowCaseValue;
}

function mergeResultOutput(outputSummary: RuntimeValueSummary[] | undefined, resultPreview: string | undefined): RuntimeValueSummary[] | undefined {
  if (!resultPreview) {
    return outputSummary;
  }
  const list = Array.isArray(outputSummary) ? [...outputSummary] : [];
  const existing = list.findIndex(item => item.name === "result");
  const resultItem: RuntimeValueSummary = { name: "result", preview: resultPreview };
  if (existing >= 0) {
    list[existing] = { ...list[existing], ...resultItem };
    return list;
  }
  return [...list, resultItem];
}

function summarizePreview(value: unknown): string | undefined {
  if (value === null || value === undefined) {
    return undefined;
  }
  if (typeof value === "string") {
    return value.length > 96 ? `${value.slice(0, 93)}...` : value;
  }
  if (typeof value === "number" || typeof value === "boolean") {
    return String(value);
  }
  try {
    const raw = JSON.stringify(value);
    if (!raw) {
      return undefined;
    }
    return raw.length > 96 ? `${raw.slice(0, 93)}...` : raw;
  } catch {
    return String(value);
  }
}
