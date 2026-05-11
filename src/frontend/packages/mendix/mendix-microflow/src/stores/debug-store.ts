export type DebugConnectionStatus = "disconnected" | "connecting" | "reconnecting" | "connected" | "error";

export const DEBUG_WS_EVENTS = {
  NODE_ENTER: "node-enter",
  NODE_EXIT: "node-exit",
  EDGE_TAKEN: "edge-taken",
  BREAKPOINT_HIT: "breakpoint",
  PAUSED: "paused",
  LOOP_ITER: "loop-iter",
  ERROR: "error",
  COMPLETE: "complete",
  STACK_PUSH: "stack-push",
  STACK_POP: "stack-pop",
  STACK_TOP: "stack-top",
  VAR_SNAPSHOT: "vars",
  SESSION: "session",
  SESSION_STATUS: "session-status",
  STATE_SYNC: "state-sync",
  VARIABLE_DETAILS: "variable-details",
  PING: "ping",
  PONG: "pong",
} as const;

export type DebugWsEventType = typeof DEBUG_WS_EVENTS[keyof typeof DEBUG_WS_EVENTS] | string;
export type DebugCommand =
  | "continue"
  | "pause"
  | "step-over"
  | "step-into"
  | "step-out"
  | "run-to-node"
  | "run-to-cursor"
  | "stop"
  | "set-breakpoint"
  | "remove-breakpoint"
  | "toggle-breakpoint"
  | "get-variable-details"
  | "set-variable"
  | "ping"
  | "pong";

export interface DebugBreakpoint {
  id: string;
  nodeId: string;
  condition?: string;
  scope?: "node" | "flow" | "expression" | "error" | "gatewayBranch";
  enabled?: boolean;
  hitTarget?: number;
  logOnly?: boolean;
}

export interface DebugVariableSnapshot {
  name: string;
  value?: string;
  type?: string;
  typeHint?: string;
}

export interface DebugLoopIteration {
  flowId?: string;
  nodeId?: string;
  iterationIndex?: number;
  totalIterations?: number;
}

export interface DebugCallStackFrame {
  runId: string;
  microflowId: string;
  depth: number;
  callerNodeId?: string;
  callerActionId?: string;
}

export interface DebugWsNodeHighlight {
  currentNodeId?: string;
  currentFlowId?: string;
  currentBranchId?: string;
  currentSafePoint?: string;
}

export interface DebugStateSyncSnapshot {
  nodeStatuses?: Record<string, string>;
  executedEdgeIds?: string[];
  variables?: DebugVariableSnapshot[];
  breakpoints?: Array<{ nodeId?: string; condition?: string; enabled?: boolean }>;
  callStack?: DebugCallStackFrame[];
}

export interface DebugWsEvent {
  type: DebugWsEventType;
  id?: string;
  timestamp?: number;
  nodeId?: string;
  flowId?: string;
  branchId?: string;
  safePoint?: string;
  message?: string;
  sessionId?: string;
  runId?: string;
  iteration?: DebugLoopIteration;
  breakpoint?: {
    nodeId: string;
    scope?: string;
    condition?: string;
    enabled?: boolean;
  };
  callStack?: DebugCallStackFrame[];
  variables?: DebugVariableSnapshot[];
  command?: string;
  error?: {
    message: string;
    stack?: string;
  };
  data?: unknown;
}

export interface DebugSnapshot {
  status: DebugConnectionStatus;
  sessionId?: string;
  lastError?: string;
  breakpoints: DebugBreakpoint[];
  conditionalBreakpoints: DebugBreakpoint[];
  pendingCommands: DebugQueuedCommand[];
  nodeState: DebugWsNodeHighlight;
  nodeStatuses: Record<string, string>;
  executedEdgeIds: string[];
  loopIteration?: DebugLoopIteration;
  callStack: DebugCallStackFrame[];
  variables: DebugVariableSnapshot[];
  activeError?: string;
  activeErrorStack?: string;
  paused?: boolean;
}

export interface DebugQueuedCommand {
  command: DebugCommand;
  payload?: Record<string, unknown>;
}

export interface DebugStoreListener {
  (snapshot: DebugSnapshot): void;
}

function commandKey(command: DebugCommand, target: string = ""): string {
  return `${command}:${target}`;
}

function deriveQueueTarget(payload: Record<string, unknown> | undefined): string {
  if (!payload) {
    return "";
  }
  const direct = payload.targetId;
  if (typeof direct === "string") {
    return direct;
  }
  const nodeId = payload.nodeId;
  if (typeof nodeId === "string") {
    return nodeId;
  }
  const breakpoint = payload.breakpoint;
  if (breakpoint && typeof breakpoint === "object" && "nodeId" in breakpoint) {
    const candidate = (breakpoint as { nodeId?: unknown }).nodeId;
    return typeof candidate === "string" ? candidate : "";
  }
  return "";
}

function normalizeVariables(value: unknown): DebugVariableSnapshot[] {
  if (!Array.isArray(value)) {
    return [];
  }
  return value
    .filter(item => typeof item === "object" && item !== null)
    .map(item => {
      const data = item as Record<string, unknown>;
      return {
        name: typeof data.name === "string" ? data.name : "",
        value: typeof data.value === "string" ? data.value : typeof data.valuePreview === "string" ? data.valuePreview : undefined,
        type: typeof data.type === "string" ? data.type : undefined,
        typeHint: typeof data.typeHint === "string" ? data.typeHint : undefined,
      };
    })
    .filter(item => item.name.length > 0);
}

function normalizeCallStack(value: unknown): DebugCallStackFrame[] {
  if (!Array.isArray(value)) {
    return [];
  }
  return value
    .filter(item => typeof item === "object" && item !== null)
    .map(item => {
      const data = item as Record<string, unknown>;
      return {
        runId: typeof data.runId === "string" ? data.runId : "",
        microflowId: typeof data.microflowId === "string" ? data.microflowId : "",
        depth: typeof data.depth === "number" ? data.depth : 0,
        callerNodeId: typeof data.callerNodeId === "string" ? data.callerNodeId : undefined,
        callerActionId: typeof data.callerActionId === "string" ? data.callerActionId : undefined,
      };
    });
}

function normalizeBreakpoints(value: unknown): DebugBreakpoint[] {
  if (!Array.isArray(value)) {
    return [];
  }
  const normalized: DebugBreakpoint[] = [];
  for (const raw of value) {
    if (typeof raw !== "object" || raw === null) {
      continue;
    }
    const data = raw as Record<string, unknown>;
    const nodeId = typeof data.nodeId === "string" ? data.nodeId : "";
    if (!nodeId) {
      continue;
    }
    normalized.push({
      id: `bp:${nodeId}`,
      nodeId,
      condition: typeof data.condition === "string" ? data.condition : undefined,
      enabled: typeof data.enabled === "boolean" ? data.enabled : true,
      scope: "node",
    });
  }
  return normalized;
}

export class DebugStore {
  private state: DebugSnapshot = {
    status: "disconnected",
    sessionId: undefined,
    lastError: undefined,
    breakpoints: [],
    conditionalBreakpoints: [],
    pendingCommands: [],
    nodeState: {},
    nodeStatuses: {},
    executedEdgeIds: [],
    loopIteration: undefined,
    callStack: [],
    variables: [],
    activeError: undefined,
    activeErrorStack: undefined,
    paused: false,
  };
  private readonly breakpointMap = new Map<string, DebugBreakpoint>();
  private readonly conditionalMap = new Map<string, DebugBreakpoint>();
  private readonly listeners = new Set<DebugStoreListener>();
  private commandSignatureSet = new Set<string>();

  getSnapshot(): DebugSnapshot {
    return {
      ...this.state,
      breakpoints: [...this.state.breakpoints],
      conditionalBreakpoints: [...this.state.conditionalBreakpoints],
      pendingCommands: [...this.state.pendingCommands],
      callStack: [...this.state.callStack],
      variables: [...this.state.variables],
      nodeStatuses: { ...this.state.nodeStatuses },
      executedEdgeIds: [...this.state.executedEdgeIds],
      loopIteration: this.state.loopIteration ? { ...this.state.loopIteration } : undefined,
      nodeState: { ...this.state.nodeState },
    };
  }

  get breakpointItems(): DebugBreakpoint[] {
    return [...this.breakpointMap.values()];
  }

  get conditionalBreakpointItems(): DebugBreakpoint[] {
    return [...this.conditionalMap.values()];
  }

  subscribe(listener: DebugStoreListener): () => void {
    this.listeners.add(listener);
    listener(this.getSnapshot());
    return () => {
      this.listeners.delete(listener);
    };
  }

  setStatus(status: DebugConnectionStatus): void {
    if (this.state.status === status) {
      return;
    }
    this.state.status = status;
    this.emit();
  }

  setSession(sessionId?: string): void {
    this.state.sessionId = sessionId;
    this.emit();
  }

  setError(message?: string): void {
    this.state.lastError = message;
    this.state.activeError = message;
    this.state.activeErrorStack = undefined;
    this.emit();
  }

  clearError(): void {
    this.state.lastError = undefined;
    this.state.activeError = undefined;
    this.state.activeErrorStack = undefined;
    this.emit();
  }

  upsertBreakpoint(breakpoint: DebugBreakpoint): void {
    this.breakpointMap.set(breakpoint.id, breakpoint);
    this.state.breakpoints = [...this.breakpointMap.values()];
    this.emit();
  }

  removeBreakpoint(id: string): void {
    this.breakpointMap.delete(id);
    this.state.breakpoints = [...this.breakpointMap.values()];
    this.emit();
  }

  upsertConditionalBreakpoint(breakpoint: DebugBreakpoint): void {
    this.conditionalMap.set(breakpoint.id, breakpoint);
    this.state.conditionalBreakpoints = [...this.conditionalMap.values()];
    this.emit();
  }

  removeConditionalBreakpoint(id: string): void {
    this.conditionalMap.delete(id);
    this.state.conditionalBreakpoints = [...this.conditionalMap.values()];
    this.emit();
  }

  clearBreakpoints(): void {
    this.breakpointMap.clear();
    this.conditionalMap.clear();
    this.state.breakpoints = [];
    this.state.conditionalBreakpoints = [];
    this.emit();
  }

  queueCommand(command: DebugCommand, payload?: Record<string, unknown>): void {
    const targetId = deriveQueueTarget(payload);
    const key = commandKey(command, targetId);
    if (this.commandSignatureSet.has(key)) {
      return;
    }
    this.state.pendingCommands.push({ command, payload });
    this.commandSignatureSet.add(key);
    if (this.state.pendingCommands.length > 50) {
      this.state.pendingCommands = this.state.pendingCommands.slice(-50);
    }
    this.emit();
  }

  popCommands(): DebugQueuedCommand[] {
    const next = [...this.state.pendingCommands];
    this.state.pendingCommands = [];
    this.commandSignatureSet = new Set();
    this.emit();
    return next;
  }

  getBreakpointsToRegister(): DebugBreakpoint[] {
    return [...this.breakpointMap.values(), ...this.conditionalMap.values()];
  }

  clearRuntimeState(): void {
    this.state.nodeState = {};
    this.state.nodeStatuses = {};
    this.state.executedEdgeIds = [];
    this.state.loopIteration = undefined;
    this.state.callStack = [];
    this.state.variables = [];
    this.state.activeError = undefined;
    this.state.activeErrorStack = undefined;
    this.state.paused = false;
    this.emit();
  }

  restoreState(snapshot: DebugStateSyncSnapshot): void {
    if (snapshot.nodeStatuses) {
      this.state.nodeStatuses = { ...snapshot.nodeStatuses };
    }
    if (snapshot.executedEdgeIds) {
      this.state.executedEdgeIds = [...snapshot.executedEdgeIds];
    }
    if (snapshot.variables) {
      this.state.variables = snapshot.variables.map(item => ({ ...item }));
    }
    if (snapshot.breakpoints) {
      const parsed = normalizeBreakpoints(snapshot.breakpoints);
      this.breakpointMap.clear();
      this.conditionalMap.clear();
      for (const item of parsed) {
        if (item.condition) {
          this.conditionalMap.set(item.id, item);
        } else {
          this.breakpointMap.set(item.id, item);
        }
      }
      this.state.breakpoints = [...this.breakpointMap.values()];
      this.state.conditionalBreakpoints = [...this.conditionalMap.values()];
    }
    if (snapshot.callStack) {
      this.state.callStack = snapshot.callStack.map(item => ({ ...item }));
    }
    this.emit();
  }

  handleEvent(event: DebugWsEvent): void {
    if (event.type === DEBUG_WS_EVENTS.STATE_SYNC) {
      const payload = (event.data ?? {}) as Record<string, unknown>;
      this.restoreState({
        nodeStatuses: typeof payload.nodeStatuses === "object" && payload.nodeStatuses ? payload.nodeStatuses as Record<string, string> : {},
        executedEdgeIds: Array.isArray(payload.executedEdgeIds) ? payload.executedEdgeIds.filter(item => typeof item === "string") as string[] : [],
        variables: normalizeVariables(payload.variables),
        breakpoints: normalizeBreakpoints(payload.breakpoints),
        callStack: normalizeCallStack(payload.callStack),
      });
      return;
    }

    if (event.type === DEBUG_WS_EVENTS.NODE_ENTER) {
      this.state.nodeState.currentNodeId = event.nodeId;
      this.state.nodeState.currentFlowId = event.flowId;
      this.state.nodeState.currentBranchId = event.branchId;
      this.state.nodeState.currentSafePoint = event.safePoint;
      if (event.nodeId) {
        this.state.nodeStatuses[event.nodeId] = "running";
      }
      this.state.paused = false;
      this.emit();
      return;
    }
    if (event.type === DEBUG_WS_EVENTS.NODE_EXIT) {
      this.state.nodeState.currentSafePoint = event.safePoint ?? this.state.nodeState.currentSafePoint;
      this.state.nodeState.currentNodeId = event.nodeId ?? this.state.nodeState.currentNodeId;
      if (event.nodeId) {
        this.state.nodeStatuses[event.nodeId] = "success";
      }
      this.emit();
      return;
    }
    if (event.type === DEBUG_WS_EVENTS.EDGE_TAKEN) {
      this.state.nodeState.currentBranchId = event.branchId;
      if (event.flowId && !this.state.executedEdgeIds.includes(event.flowId)) {
        this.state.executedEdgeIds.push(event.flowId);
      }
      this.emit();
      return;
    }
    if (event.type === DEBUG_WS_EVENTS.BREAKPOINT_HIT || event.type === DEBUG_WS_EVENTS.PAUSED) {
      this.state.paused = true;
      this.state.nodeState.currentNodeId = event.nodeId ?? this.state.nodeState.currentNodeId;
      this.state.nodeState.currentSafePoint = event.safePoint ?? this.state.nodeState.currentSafePoint;
      if (event.variables) {
        this.state.variables = event.variables.map(item => ({ ...item }));
      }
      if (event.callStack) {
        this.state.callStack = event.callStack.map(item => ({ ...item }));
      }
      this.state.lastError = undefined;
      this.emit();
      return;
    }
    if (event.type === DEBUG_WS_EVENTS.LOOP_ITER && event.iteration) {
      this.state.loopIteration = { ...event.iteration };
      this.emit();
      return;
    }
    if (event.type === DEBUG_WS_EVENTS.ERROR) {
      const message = event.error?.message ?? event.message ?? "Debug error";
      this.state.activeError = message;
      this.state.activeErrorStack = event.error?.stack ?? undefined;
      this.state.lastError = message;
      this.state.nodeState.currentNodeId = event.nodeId ?? this.state.nodeState.currentNodeId;
      this.state.nodeState.currentSafePoint = event.safePoint ?? this.state.nodeState.currentSafePoint;
      if (event.nodeId) {
        this.state.nodeStatuses[event.nodeId] = "error";
      }
      this.state.paused = true;
      this.emit();
      return;
    }
    if (event.type === DEBUG_WS_EVENTS.COMPLETE) {
      this.state.loopIteration = undefined;
      this.state.nodeState.currentNodeId = undefined;
      this.state.nodeState.currentFlowId = undefined;
      this.state.nodeState.currentBranchId = undefined;
      this.state.nodeState.currentSafePoint = undefined;
      this.state.paused = false;
      this.emit();
      return;
    }
    if (event.type === DEBUG_WS_EVENTS.STACK_PUSH || event.type === DEBUG_WS_EVENTS.STACK_POP || event.type === DEBUG_WS_EVENTS.STACK_TOP) {
      this.state.callStack = [...(event.callStack ?? [])];
      this.emit();
      return;
    }
    if (event.type === DEBUG_WS_EVENTS.VAR_SNAPSHOT && event.variables) {
      this.state.variables = event.variables.map(item => ({ ...item }));
      this.emit();
      return;
    }
    if ((event.type === DEBUG_WS_EVENTS.SESSION || event.type === DEBUG_WS_EVENTS.SESSION_STATUS) && event.sessionId) {
      this.state.sessionId = event.sessionId;
      this.emit();
    }
  }

  resetForSession(sessionId?: string): void {
    this.state = {
      ...this.state,
      status: "disconnected",
      sessionId,
      breakpoints: [...this.breakpointMap.values()],
      conditionalBreakpoints: [...this.conditionalMap.values()],
      pendingCommands: [],
      lastError: undefined,
      activeError: undefined,
      activeErrorStack: undefined,
      paused: false,
    };
    this.state.breakpoints = [...this.breakpointMap.values()];
    this.state.conditionalBreakpoints = [...this.conditionalMap.values()];
    this.state.callStack = [];
    this.state.loopIteration = undefined;
    this.state.variables = [];
    this.state.nodeState = {};
    this.state.nodeStatuses = {};
    this.state.executedEdgeIds = [];
    this.commandSignatureSet = new Set();
    this.emit();
  }

  private emit(): void {
    const snapshot = this.getSnapshot();
    for (const listener of this.listeners) {
      listener(snapshot);
    }
  }
}

export function createDebugStore(): DebugStore {
  return new DebugStore();
}
