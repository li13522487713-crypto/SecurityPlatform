export type MicroflowDebugCommand =
  | "continue"
  | "pause"
  | "stepOver"
  | "stepInto"
  | "stepOut"
  | "runToNode"
  | "runToCursor"
  | "cancel"
  | "stop";

export type MicroflowDebugBreakpointScopeDto = "node" | "flow" | "expression" | "errorHandler" | "gatewayBranch" | 0 | 1 | 2 | 3 | 4;
export type MicroflowDebugBreakpointSuspendPolicyDto = "all" | "thread" | "branchOnly" | 0 | 1 | 2;

export interface MicroflowDebugBreakpointDto {
  id: string;
  microflowObjectId: string;
  scope: MicroflowDebugBreakpointScopeDto;
  stale: boolean;
  enabled?: boolean;
  hitCount?: number;
  hitTarget?: number;
  suspendPolicy?: MicroflowDebugBreakpointSuspendPolicyDto;
}

export interface MicroflowDebugConditionalBreakpointDto {
  id: string;
  microflowObjectId: string;
  conditionExpression: string;
  hitTarget: number;
  suspendPolicy: MicroflowDebugBreakpointSuspendPolicyDto;
  logOnly: boolean;
  stale: boolean;
  enabled?: boolean;
  hitCount?: number;
  scope?: MicroflowDebugBreakpointScopeDto;
}

export interface MicroflowDebugSessionDto {
  id: string;
  microflowId: string;
  runId?: string;
  boundEngineRunId?: string;
  currentNodeObjectId?: string;
  pausePhase?: string;
  currentSafePoint?: MicroflowDebugSafePointDto;
  status: string;
  trace?: MicroflowDebugTraceEventDto[];
  state?: string;
  availableCommands?: MicroflowDebugCommand[];
  breakpoints?: MicroflowDebugBreakpointDto[];
  conditionalBreakpoints?: MicroflowDebugConditionalBreakpointDto[];
  callStack?: MicroflowDebugCallStackFrameDto[];
  lastUpdatedAt?: string;
}

export interface MicroflowDebugSafePointDto {
  nodeObjectId: string;
  nodeKind: string;
  phase: string;
  incomingFlowId?: string;
  outgoingFlowId?: string;
  branchId?: string;
  splitInstanceId?: string;
  loopIterationId?: string;
  loopIterationIndex?: number;
  callStackFrameId?: string;
  callDepth: number;
  semanticKind: string;
  arrivedAt: string;
}

export interface MicroflowDebugTraceEventDto {
  id: string;
  kind: string;
  message: string;
  runId?: string;
  nodeObjectId?: string;
  flowId?: string;
  branchId?: string;
  createdAt: string;
}

export interface MicroflowDebugVariableSnapshotDto {
  name: string;
  type: string;
  valuePreview?: string;
  redactionApplied?: boolean;
  scopeKind?: string;
  objectId?: string;
  flowId?: string;
  branchId?: string;
}

export interface MicroflowDebugCallStackFrameDto {
  id: string;
  microflowId: string;
  parentRunId?: string;
  runId: string;
  callerObjectId?: string;
  callerActionId?: string;
  depth: number;
  status: string;
}

export interface MicroflowDebugWatchExpressionDto {
  expression: string;
  type?: string;
  valuePreview?: string;
  error?: string;
  durationMs?: number;
}

export interface MicroflowDebugTimelineEventDto {
  id: string;
  sessionId: string;
  runId?: string;
  objectId?: string;
  flowId?: string;
  branchId?: string;
  phase?: string;
  occurredAt: string;
  summary?: string;
}

export interface UpdateDebugSuspendPolicyRequestDto {
  policy: "all" | "branchOnly";
}

export interface UpdateDebugSuspendPolicyResponseDto {
  sessionId: string;
  policy: "all" | "branchOnly";
}

export interface MutateDebugVariableRequestDto {
  name: string;
  value: unknown;
}

export interface MutateDebugVariableResponseDto {
  sessionId: string;
  name: string;
  valuePreview?: string;
  mutated: boolean;
}

export interface MicroflowDebugCommandTargetDto {
  nodeObjectId?: string;
  flowId?: string;
}

export interface MicroflowDebugCommandRequestDto {
  command: MicroflowDebugCommand;
  targetNodeObjectId?: string;
  targetFlowId?: string;
}

export interface MicroflowStepDebugApiClientOptions {
  baseUrl?: string;
  fetcher?: typeof fetch;
  headers?: HeadersInit;
}

type WsClientMessage = {
  type: string;
  data?: Record<string, unknown>;
};

type WsServerMessage = {
  type: string;
  id?: string;
  timestamp?: number;
  data?: Record<string, unknown>;
  [key: string]: unknown;
};

interface WsRuntime {
  sessionId: string;
  microflowId: string;
  ws: WebSocket;
  status: string;
  session: MicroflowDebugSessionDto;
  breakpointsById: Map<string, MicroflowDebugBreakpointDto>;
  nodeBreakpointIdByNode: Map<string, string>;
  variables: MicroflowDebugVariableSnapshotDto[];
  timeline: MicroflowDebugTimelineEventDto[];
  trace: MicroflowDebugTraceEventDto[];
  waitingVariableDetails: Map<string, (message: WsServerMessage) => void>;
}

function nowIso(): string {
  return new Date().toISOString();
}

function createId(prefix: string): string {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    return `${prefix}-${crypto.randomUUID()}`;
  }
  return `${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
}

function mapCommand(command: MicroflowDebugCommand, target?: MicroflowDebugCommandTargetDto): WsClientMessage {
  switch (command) {
    case "stepOver":
      return { type: "step-over" };
    case "stepInto":
      return { type: "step-into" };
    case "stepOut":
      return { type: "step-out" };
    case "continue":
      return { type: "continue" };
    case "pause":
      return { type: "pause" };
    case "runToNode":
      return { type: "run-to-node", data: target?.nodeObjectId ? { nodeId: target.nodeObjectId } : {} };
    case "runToCursor":
      return { type: "run-to-cursor", data: target?.flowId ? { flowId: target.flowId } : {} };
    case "cancel":
    case "stop":
      return { type: "stop" };
    default:
      return { type: "pause" };
  }
}

function resolveWsBaseUrl(baseUrl?: string): string {
  if (typeof window !== "undefined") {
    const protocol = window.location.protocol === "https:" ? "wss:" : "ws:";
    return `${protocol}//${window.location.host}`;
  }
  if (!baseUrl) {
    return "ws://localhost";
  }
  if (baseUrl.startsWith("http://")) {
    return `ws://${baseUrl.slice("http://".length)}`;
  }
  if (baseUrl.startsWith("https://")) {
    return `wss://${baseUrl.slice("https://".length)}`;
  }
  return baseUrl;
}

export class MicroflowStepDebugApiClient {
  private readonly baseUrl: string;
  private readonly runtimes = new Map<string, WsRuntime>();

  constructor(options: MicroflowStepDebugApiClientOptions = {}) {
    this.baseUrl = resolveWsBaseUrl(options.baseUrl);
  }

  async createSession(microflowId: string): Promise<MicroflowDebugSessionDto> {
    const sessionId = createId("dbg");
    const runtime = await this.openRuntime(microflowId, sessionId);
    return { ...runtime.session };
  }

  async getSession(sessionId: string): Promise<MicroflowDebugSessionDto> {
    const runtime = this.requireRuntime(sessionId);
    return { ...runtime.session };
  }

  async sendCommand(sessionId: string, command: MicroflowDebugCommand, target?: MicroflowDebugCommandTargetDto): Promise<MicroflowDebugSessionDto> {
    const runtime = this.requireRuntime(sessionId);
    this.send(runtime, mapCommand(command, target));
    runtime.session.lastUpdatedAt = nowIso();
    return { ...runtime.session };
  }

  async upsertBreakpoint(sessionId: string, breakpoint: MicroflowDebugBreakpointDto): Promise<MicroflowDebugSessionDto> {
    const runtime = this.requireRuntime(sessionId);
    runtime.breakpointsById.set(breakpoint.id, breakpoint);
    runtime.nodeBreakpointIdByNode.set(breakpoint.microflowObjectId, breakpoint.id);
    runtime.session.breakpoints = [...runtime.breakpointsById.values()];
    this.send(runtime, {
      type: "set-breakpoint",
      data: {
        nodeId: breakpoint.microflowObjectId,
        enabled: breakpoint.enabled ?? true,
      },
    });
    return { ...runtime.session };
  }

  async removeBreakpoint(sessionId: string, breakpointId: string): Promise<MicroflowDebugSessionDto> {
    const runtime = this.requireRuntime(sessionId);
    const breakpoint = runtime.breakpointsById.get(breakpointId);
    if (breakpoint) {
      runtime.nodeBreakpointIdByNode.delete(breakpoint.microflowObjectId);
      this.send(runtime, {
        type: "remove-breakpoint",
        data: { nodeId: breakpoint.microflowObjectId },
      });
    }
    runtime.breakpointsById.delete(breakpointId);
    runtime.session.breakpoints = [...runtime.breakpointsById.values()];
    return { ...runtime.session };
  }

  async listVariables(sessionId: string): Promise<MicroflowDebugVariableSnapshotDto[]> {
    return [...this.requireRuntime(sessionId).variables];
  }

  async evaluate(sessionId: string, expression: string): Promise<MicroflowDebugWatchExpressionDto> {
    const runtime = this.requireRuntime(sessionId);
    const startedAt = Date.now();
    const variableName = expression.trim().replace(/^\$/, "");
    const hit = runtime.variables.find(item => item.name === variableName || item.name === `$${variableName}`);
    if (!hit) {
      return {
        expression,
        error: "Watch expression cannot be evaluated from the current paused snapshot.",
        durationMs: Date.now() - startedAt,
      };
    }

    return {
      expression,
      type: hit.type,
      valuePreview: hit.valuePreview,
      durationMs: Date.now() - startedAt,
    };
  }

  async trace(sessionId: string): Promise<MicroflowDebugTraceEventDto[]> {
    return [...this.requireRuntime(sessionId).trace];
  }

  async updateSuspendPolicy(sessionId: string, policy: UpdateDebugSuspendPolicyRequestDto["policy"]): Promise<UpdateDebugSuspendPolicyResponseDto> {
    const runtime = this.requireRuntime(sessionId);
    return {
      sessionId: runtime.sessionId,
      policy,
    };
  }

  async getTimeline(sessionId: string): Promise<MicroflowDebugTimelineEventDto[]> {
    return [...this.requireRuntime(sessionId).timeline].sort((a, b) => b.occurredAt.localeCompare(a.occurredAt));
  }

  async mutateVariable(sessionId: string, payload: MutateDebugVariableRequestDto): Promise<MutateDebugVariableResponseDto> {
    const runtime = this.requireRuntime(sessionId);
    this.send(runtime, {
      type: "set-variable",
      data: {
        variableName: payload.name,
        value: payload.value,
      },
    });

    const normalized = payload.name.startsWith("$") ? payload.name : `$${payload.name}`;
    const index = runtime.variables.findIndex(item => item.name === payload.name || item.name === normalized);
    const preview = String(payload.value);
    if (index >= 0) {
      runtime.variables[index] = {
        ...runtime.variables[index],
        valuePreview: preview,
      };
    }

    return {
      sessionId: runtime.sessionId,
      name: payload.name,
      valuePreview: preview,
      mutated: true,
    };
  }

  async deleteSession(sessionId: string): Promise<boolean> {
    const runtime = this.runtimes.get(sessionId);
    if (!runtime) {
      return true;
    }
    runtime.ws.close();
    this.runtimes.delete(sessionId);
    return true;
  }

  private requireRuntime(sessionId: string): WsRuntime {
    const runtime = this.runtimes.get(sessionId);
    if (!runtime) {
      throw new Error(`MICROFLOW_DEBUG_SESSION_NOT_FOUND: ${sessionId}`);
    }
    return runtime;
  }

  private async openRuntime(microflowId: string, sessionId: string): Promise<WsRuntime> {
    const url = `${this.baseUrl}/api/debug/microflow/${encodeURIComponent(microflowId)}?sessionId=${encodeURIComponent(sessionId)}`;
    const ws = new WebSocket(url);

    const runtime: WsRuntime = {
      sessionId,
      microflowId,
      ws,
      status: "initialized",
      session: {
        id: sessionId,
        microflowId,
        status: "initialized",
        state: "initialized",
        breakpoints: [],
        conditionalBreakpoints: [],
        callStack: [],
        trace: [],
      },
      breakpointsById: new Map(),
      nodeBreakpointIdByNode: new Map(),
      variables: [],
      timeline: [],
      trace: [],
      waitingVariableDetails: new Map(),
    };

    this.runtimes.set(sessionId, runtime);

    await new Promise<void>((resolve, reject) => {
      ws.onopen = () => {
        this.send(runtime, { type: "hello", data: { sessionId } });
        resolve();
      };
      ws.onerror = () => {
        reject(new Error("MICROFLOW_DEBUG_SOCKET_OPEN_FAILED"));
      };
      ws.onmessage = event => {
        const message = JSON.parse(event.data) as WsServerMessage;
        this.handleServerMessage(runtime, message);
      };
      ws.onclose = () => {
        runtime.status = "completed";
        runtime.session.status = "completed";
      };
    });

    return runtime;
  }

  private send(runtime: WsRuntime, message: WsClientMessage): void {
    runtime.ws.send(JSON.stringify(message));
  }

  private handleServerMessage(runtime: WsRuntime, message: WsServerMessage): void {
    if (message.type === "ping") {
      const sequence = typeof message.data?.sequence === "number" ? message.data.sequence : Date.now();
      this.send(runtime, {
        type: "pong",
        data: { sequence },
      });
      return;
    }

    if (message.type === "pong") {
      return;
    }

    const timestamp = new Date(typeof message.timestamp === "number" ? message.timestamp : Date.now()).toISOString();

    if (message.type === "session-status") {
      const status = typeof message.data?.status === "string" ? message.data.status : "running";
      runtime.status = status;
      runtime.session.status = status;
      runtime.session.state = status;
      runtime.session.lastUpdatedAt = timestamp;
    }

    if (message.type === "state-sync") {
      const variables = Array.isArray(message.data?.variables) ? message.data?.variables : [];
      runtime.variables = variables.map(item => {
        const row = item as Record<string, unknown>;
        return {
          name: typeof row.name === "string" ? row.name : "",
          type: typeof row.type === "string" ? row.type : "unknown",
          valuePreview: typeof row.valuePreview === "string" ? row.valuePreview : typeof row.value === "string" ? row.value : undefined,
        } satisfies MicroflowDebugVariableSnapshotDto;
      }).filter(item => item.name.length > 0);

      const callStack = Array.isArray(message.data?.callStack) ? message.data?.callStack : [];
      runtime.session.callStack = callStack.map((item, index) => {
        const row = item as Record<string, unknown>;
        return {
          id: typeof row.id === "string" ? row.id : `${runtime.sessionId}-${index}`,
          microflowId: typeof row.microflowId === "string" ? row.microflowId : runtime.microflowId,
          runId: typeof row.runId === "string" ? row.runId : runtime.session.runId ?? runtime.sessionId,
          callerObjectId: typeof row.callerObjectId === "string" ? row.callerObjectId : undefined,
          callerActionId: typeof row.callerActionId === "string" ? row.callerActionId : undefined,
          depth: typeof row.depth === "number" ? row.depth : index,
          status: typeof row.status === "string" ? row.status : "active",
        } satisfies MicroflowDebugCallStackFrameDto;
      });

      runtime.session.lastUpdatedAt = timestamp;
    }

    if (message.type === "node-enter" || message.type === "node-exit" || message.type === "edge-taken" || message.type === "breakpoint" || message.type === "paused" || message.type === "error" || message.type === "complete") {
      const data = (message.data ?? {}) as Record<string, unknown>;
      const nodeId = typeof data.nodeId === "string" ? data.nodeId : undefined;
      const flowId = typeof data.flowId === "string" ? data.flowId : undefined;
      const branchId = typeof data.branchId === "string" ? data.branchId : undefined;
      runtime.session.currentNodeObjectId = nodeId ?? runtime.session.currentNodeObjectId;

      const traceItem: MicroflowDebugTraceEventDto = {
        id: typeof message.id === "string" ? message.id : createId("trace"),
        kind: message.type,
        message: JSON.stringify(data),
        runId: runtime.session.runId,
        nodeObjectId: nodeId,
        flowId,
        branchId,
        createdAt: timestamp,
      };
      runtime.trace = [...runtime.trace, traceItem].slice(-500);
      runtime.session.trace = runtime.trace;
      runtime.timeline = [
        {
          id: traceItem.id,
          sessionId: runtime.sessionId,
          runId: traceItem.runId,
          objectId: traceItem.nodeObjectId,
          flowId: traceItem.flowId,
          branchId: traceItem.branchId,
          phase: traceItem.kind,
          occurredAt: traceItem.createdAt,
          summary: traceItem.message,
        },
        ...runtime.timeline,
      ].slice(0, 500);

      if (Array.isArray(data.variables)) {
        runtime.variables = data.variables
          .map(item => {
            const row = item as Record<string, unknown>;
            return {
              name: typeof row.name === "string" ? row.name : "",
              type: typeof row.type === "string" ? row.type : "unknown",
              valuePreview: typeof row.valuePreview === "string" ? row.valuePreview : typeof row.value === "string" ? row.value : undefined,
            };
          })
          .filter(item => item.name.length > 0);
      }

      runtime.session.lastUpdatedAt = timestamp;
    }

    if (message.type === "variable-details") {
      const requestId = typeof message.data?.requestId === "string" ? message.data.requestId : "";
      if (requestId.length > 0) {
        runtime.waitingVariableDetails.get(requestId)?.(message);
      }
    }
  }
}
