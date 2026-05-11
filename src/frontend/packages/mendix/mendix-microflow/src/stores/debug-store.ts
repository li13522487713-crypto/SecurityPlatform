export type DebugConnectionStatus = "disconnected" | "connecting" | "connected" | "error";

export const DEBUG_WS_EVENTS = {
  NODE_ENTER: "node-enter",
  NODE_EXIT: "node-exit",
  EDGE_TAKEN: "edge-taken",
  BREAKPOINT_HIT: "breakpoint",
  LOOP_ITER: "loop-iter",
  ERROR: "error",
  COMPLETE: "complete",
  STACK_PUSH: "stack-push",
  STACK_POP: "stack-pop",
  STACK_TOP: "stack-top",
  VAR_SNAPSHOT: "vars",
  SESSION: "session",
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

export interface DebugWsEvent {
  type: DebugWsEventType;
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
  };
  callStack?: DebugCallStackFrame[];
  variables?: DebugVariableSnapshot[];
  command?: string;
  error?: {
    message: string;
    stack?: string;
  };
}

export interface DebugSnapshot {
  status: DebugConnectionStatus;
  sessionId?: string;
  lastError?: string;
  breakpoints: DebugBreakpoint[];
  conditionalBreakpoints: DebugBreakpoint[];
  pendingCommands: DebugQueuedCommand[];
  nodeState: DebugWsNodeHighlight;
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

export class DebugStore {
  private state: DebugSnapshot = {
    status: "disconnected",
    sessionId: undefined,
    lastError: undefined,
    breakpoints: [],
    conditionalBreakpoints: [],
    pendingCommands: [],
    nodeState: {},
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
    if (this.state.pendingCommands.length > 20) {
      this.state.pendingCommands = this.state.pendingCommands.slice(-20);
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
    this.state.loopIteration = undefined;
    this.state.callStack = [];
    this.state.variables = [];
    this.state.activeError = undefined;
    this.state.activeErrorStack = undefined;
    this.state.paused = false;
    this.emit();
  }

  handleEvent(event: DebugWsEvent): void {
    if (event.type === DEBUG_WS_EVENTS.NODE_ENTER) {
      this.state.nodeState.currentNodeId = event.nodeId;
      this.state.nodeState.currentFlowId = event.flowId;
      this.state.nodeState.currentBranchId = event.branchId;
      this.state.nodeState.currentSafePoint = event.safePoint;
      this.state.paused = false;
      this.emit();
      return;
    }
    if (event.type === DEBUG_WS_EVENTS.NODE_EXIT) {
      this.state.nodeState.currentSafePoint = event.safePoint ?? this.state.nodeState.currentSafePoint;
      this.state.nodeState.currentNodeId = event.nodeId ?? this.state.nodeState.currentNodeId;
      this.emit();
      return;
    }
    if (event.type === DEBUG_WS_EVENTS.EDGE_TAKEN) {
      this.state.nodeState.currentBranchId = event.branchId;
      this.emit();
      return;
    }
    if (event.type === DEBUG_WS_EVENTS.BREAKPOINT_HIT) {
      this.state.paused = true;
      this.state.nodeState.currentNodeId = event.nodeId ?? this.state.nodeState.currentNodeId;
      this.state.nodeState.currentSafePoint = event.safePoint ?? this.state.nodeState.currentSafePoint;
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
    if (event.type === DEBUG_WS_EVENTS.SESSION && event.sessionId) {
      this.state.sessionId = event.sessionId;
      this.emit();
      return;
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
