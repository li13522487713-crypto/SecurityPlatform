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

export interface MicroflowDebugSessionDto {
  id: string;
  microflowId: string;
  runId?: string;
  boundEngineRunId?: string;
  currentNodeObjectId?: string;
  pausePhase?: string;
  status: string;
  trace?: MicroflowDebugTraceEventDto[];
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

  sendCommand(sessionId: string, command: MicroflowDebugCommand, target?: { nodeObjectId?: string; flowId?: string }): Promise<MicroflowDebugSessionDto> {
    return this.request<MicroflowDebugSessionDto>(`/api/v1/microflows/debug-sessions/${encodeURIComponent(sessionId)}/commands`, {
      method: "POST",
      body: JSON.stringify({
        command,
        targetNodeObjectId: target?.nodeObjectId,
        targetFlowId: target?.flowId,
      }),
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
