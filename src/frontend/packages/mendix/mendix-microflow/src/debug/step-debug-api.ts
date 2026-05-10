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

export interface MicroflowDebugApiEnvelope<T> {
  success: boolean;
  data?: T;
  error?: {
    code: string;
    message: string;
    httpStatus?: number;
    traceId?: string;
  };
  traceId?: string;
}

export interface MicroflowStepDebugApiClientOptions {
  baseUrl?: string;
  fetcher?: typeof fetch;
  headers?: HeadersInit;
}

export class MicroflowStepDebugApiClient {
  private readonly baseUrl: string;
  private readonly fetcher: typeof fetch;
  private readonly headers?: HeadersInit;

  constructor(options: MicroflowStepDebugApiClientOptions = {}) {
    this.baseUrl = options.baseUrl ?? "";
    this.fetcher = options.fetcher ?? fetch;
    this.headers = options.headers;
  }

  createSession(microflowId: string): Promise<MicroflowDebugSessionDto> {
    return this.request<MicroflowDebugSessionDto>(`/api/v1/microflows/${encodeURIComponent(microflowId)}/debug-sessions`, {
      method: "POST",
    });
  }

  getSession(sessionId: string): Promise<MicroflowDebugSessionDto> {
    return this.request<MicroflowDebugSessionDto>(`/api/v1/microflows/debug-sessions/${encodeURIComponent(sessionId)}`);
  }

  sendCommand(sessionId: string, command: MicroflowDebugCommand, target?: MicroflowDebugCommandTargetDto): Promise<MicroflowDebugSessionDto> {
    return this.request<MicroflowDebugSessionDto>(`/api/v1/microflows/debug-sessions/${encodeURIComponent(sessionId)}/commands`, {
      method: "POST",
      body: JSON.stringify({
        command,
        targetNodeObjectId: target?.nodeObjectId,
        targetFlowId: target?.flowId,
      } satisfies MicroflowDebugCommandRequestDto),
    });
  }

  upsertBreakpoint(sessionId: string, breakpoint: MicroflowDebugBreakpointDto): Promise<MicroflowDebugSessionDto> {
    return this.request<MicroflowDebugSessionDto>(`/api/v1/microflows/debug-sessions/${encodeURIComponent(sessionId)}/breakpoints`, {
      method: "POST",
      body: JSON.stringify(breakpoint),
    });
  }

  removeBreakpoint(sessionId: string, breakpointId: string): Promise<MicroflowDebugSessionDto> {
    return this.request<MicroflowDebugSessionDto>(`/api/v1/microflows/debug-sessions/${encodeURIComponent(sessionId)}/breakpoints/${encodeURIComponent(breakpointId)}`, {
      method: "DELETE",
    });
  }

  listVariables(sessionId: string): Promise<MicroflowDebugVariableSnapshotDto[]> {
    return this.request<MicroflowDebugVariableSnapshotDto[]>(`/api/v1/microflows/debug-sessions/${encodeURIComponent(sessionId)}/variables`);
  }

  evaluate(sessionId: string, expression: string): Promise<MicroflowDebugWatchExpressionDto> {
    return this.request<MicroflowDebugWatchExpressionDto>(`/api/v1/microflows/debug-sessions/${encodeURIComponent(sessionId)}/evaluate`, {
      method: "POST",
      body: JSON.stringify({ expression }),
    });
  }

  trace(sessionId: string): Promise<MicroflowDebugTraceEventDto[]> {
    return this.request<MicroflowDebugTraceEventDto[]>(`/api/v1/microflows/debug-sessions/${encodeURIComponent(sessionId)}/trace`);
  }

  updateSuspendPolicy(sessionId: string, policy: UpdateDebugSuspendPolicyRequestDto["policy"]): Promise<UpdateDebugSuspendPolicyResponseDto> {
    return this.request<UpdateDebugSuspendPolicyResponseDto>(`/api/v1/microflows/debug-sessions/${encodeURIComponent(sessionId)}/suspend-policy`, {
      method: "POST",
      body: JSON.stringify({ policy }),
    });
  }

  getTimeline(sessionId: string): Promise<MicroflowDebugTimelineEventDto[]> {
    return this.request<MicroflowDebugTimelineEventDto[]>(`/api/v1/microflows/debug-sessions/${encodeURIComponent(sessionId)}/timeline`);
  }

  mutateVariable(sessionId: string, payload: MutateDebugVariableRequestDto): Promise<MutateDebugVariableResponseDto> {
    return this.request<MutateDebugVariableResponseDto>(`/api/v1/microflows/debug-sessions/${encodeURIComponent(sessionId)}/variables:mutate`, {
      method: "POST",
      body: JSON.stringify(payload),
    });
  }

  async deleteSession(sessionId: string): Promise<boolean> {
    return this.request<boolean>(`/api/v1/microflows/debug-sessions/${encodeURIComponent(sessionId)}`, {
      method: "DELETE",
    });
  }

  private async request<T>(path: string, init: RequestInit = {}): Promise<T> {
    const response = await this.fetcher(`${this.baseUrl}${path}`, {
      ...init,
      headers: {
        "content-type": "application/json",
        ...this.headers,
        ...init.headers,
      },
    });
    const envelope = (await response.json()) as MicroflowDebugApiEnvelope<T>;
    if (!response.ok || !envelope.success) {
      const code = envelope.error?.code ?? `HTTP_${response.status}`;
      const message = envelope.error?.message ?? response.statusText;
      throw new Error(`${code}: ${message}`);
    }

    return envelope.data as T;
  }
}
