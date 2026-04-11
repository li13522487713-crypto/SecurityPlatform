import type { ApiResponse, PagedResult } from "@atlas/shared-core";
import { getAccessToken, getAntiforgeryToken, getTenantId } from "@atlas/shared-core";
import {
  normalizeNodeTypeKey,
  workflowNodeTypeToValue,
  workflowNodeValueToKey,
  type CanvasSchema,
  type ConnectionSchema,
  type ExecutionCancelledEvent,
  type ExecutionCompleteEvent,
  type ExecutionFailedEvent,
  type ExecutionInterruptedEvent,
  type ExecutionStartEvent,
  type NodeCompleteEvent,
  type NodeDebugRequest,
  type NodeDebugResponse,
  type NodeExecutionDetailResponse,
  type NodeFailedEvent,
  type NodeOutputEvent,
  type NodeSchema,
  type NodeStartEvent,
  type NodeTemplateMetadata,
  type NodeTypeMetadata,
  type WorkflowCanvasPayload,
  type WorkflowCreateRequest,
  type WorkflowDetailResponse,
  type WorkflowExecutionCheckpointResponse,
  type WorkflowExecutionDebugViewResponse,
  type WorkflowListItem,
  type WorkflowProcessResponse,
  type WorkflowPublishRequest,
  type WorkflowResumeRequest,
  type WorkflowRunRequest,
  type WorkflowRunResponse,
  type WorkflowSaveRequest,
  type WorkflowUpdateMetaRequest,
  type WorkflowVersionItem
} from "../types";

type IdLike = string | number;
const DEFAULT_NODE_WIDTH = 160;
const DEFAULT_NODE_HEIGHT = 60;
const BASE = "/api/v2/workflows";
const EXEC_BASE = "/api/v2/workflows/executions";

export interface StreamCallbacks {
  onExecutionStarted?: (ev: ExecutionStartEvent) => void;
  onNodeStarted?: (ev: NodeStartEvent) => void;
  onNodeOutput?: (ev: NodeOutputEvent) => void;
  onNodeCompleted?: (ev: NodeCompleteEvent) => void;
  onNodeFailed?: (ev: NodeFailedEvent) => void;
  onLlmOutput?: (content: string) => void;
  onExecutionCompleted?: (ev: ExecutionCompleteEvent) => void;
  onExecutionFailed?: (ev: ExecutionFailedEvent) => void;
  onExecutionCancelled?: (ev: ExecutionCancelledEvent) => void;
  onExecutionInterrupted?: (ev: ExecutionInterruptedEvent) => void;
  onError?: (err: Event | Error) => void;
}

export interface StreamRunHandle {
  abort: () => void;
  done: Promise<void>;
}

export interface WorkflowApiFactoryOptions {
  requestFn: <T>(path: string, init?: RequestInit) => Promise<T>;
  resolveAbsoluteUrl?: (path: string) => string;
  resolveAppId?: () => string | null;
}

export function createWorkflowV2Api(options: WorkflowApiFactoryOptions) {
  const requestFn = options.requestFn;

  return {
    create(req: WorkflowCreateRequest): Promise<ApiResponse<{ id: string }>> {
      return requestFn<ApiResponse<{ id: string }>>(BASE, { method: "POST", body: JSON.stringify(req) });
    },
    list(pageIndex = 1, pageSize = 20, keyword?: string): Promise<ApiResponse<PagedResult<WorkflowListItem>>> {
      const params = new URLSearchParams({ pageIndex: String(pageIndex), pageSize: String(pageSize) });
      if (keyword) {
        params.set("keyword", keyword);
      }
      return requestFn<ApiResponse<PagedResult<WorkflowListItem>>>(`${BASE}?${params.toString()}`);
    },
    listPublished(pageIndex = 1, pageSize = 20, keyword?: string): Promise<ApiResponse<PagedResult<WorkflowListItem>>> {
      const params = new URLSearchParams({ pageIndex: String(pageIndex), pageSize: String(pageSize) });
      if (keyword) {
        params.set("keyword", keyword);
      }
      return requestFn<ApiResponse<PagedResult<WorkflowListItem>>>(`${BASE}/published?${params.toString()}`);
    },
    getDetail(id: IdLike): Promise<ApiResponse<WorkflowDetailResponse>> {
      return requestFn<ApiResponse<WorkflowDetailResponse>>(`${BASE}/${id}`).then((res) => {
        if (res.data?.canvasJson) {
          return { ...res, data: { ...res.data, canvasJson: toEditorCanvasJson(res.data.canvasJson) } };
        }
        return res;
      });
    },
    saveDraft(id: IdLike, req: WorkflowSaveRequest): Promise<ApiResponse<boolean>> {
      return requestFn<ApiResponse<boolean>>(`${BASE}/${id}/draft`, {
        method: "PUT",
        body: JSON.stringify({
          canvasJson: toBackendCanvasJson(req.canvasJson),
          commitId: req.commitId ?? null
        })
      });
    },
    updateMeta(id: IdLike, req: WorkflowUpdateMetaRequest): Promise<ApiResponse<boolean>> {
      return requestFn<ApiResponse<boolean>>(`${BASE}/${id}/meta`, { method: "PUT", body: JSON.stringify(req) });
    },
    publish(id: IdLike, req: WorkflowPublishRequest): Promise<ApiResponse<{ id: string }>> {
      return requestFn<ApiResponse<{ id: string }>>(`${BASE}/${id}/publish`, {
        method: "POST",
        body: JSON.stringify(req)
      });
    },
    copy(id: IdLike): Promise<ApiResponse<{ id: string }>> {
      return requestFn<ApiResponse<{ id: string }>>(`${BASE}/${id}/copy`, { method: "POST" });
    },
    delete(id: IdLike): Promise<ApiResponse<boolean>> {
      return requestFn<ApiResponse<boolean>>(`${BASE}/${id}`, { method: "DELETE" });
    },
    getVersions(id: IdLike): Promise<ApiResponse<WorkflowVersionItem[]>> {
      return requestFn<ApiResponse<WorkflowVersionItem[]>>(`${BASE}/${id}/versions`);
    },
    getNodeTypes(): Promise<ApiResponse<NodeTypeMetadata[]>> {
      return requestFn<ApiResponse<NodeTypeMetadata[]>>(`${BASE}/node-types`);
    },
    getNodeTemplates(): Promise<ApiResponse<NodeTemplateMetadata[]>> {
      return requestFn<ApiResponse<NodeTemplateMetadata[]>>(`${BASE}/node-templates`);
    },
    runSync(id: IdLike, req: WorkflowRunRequest): Promise<ApiResponse<WorkflowRunResponse>> {
      return requestFn<ApiResponse<WorkflowRunResponse>>(`${BASE}/${id}/run`, {
        method: "POST",
        body: JSON.stringify(buildRunPayload(req))
      });
    },
    runStream(id: IdLike, req: WorkflowRunRequest, callbacks: StreamCallbacks): StreamRunHandle {
      return createStreamRunHandle(`${BASE}/${id}/stream`, buildRunPayload(req), callbacks, options);
    },
    cancel(executionId: IdLike): Promise<ApiResponse<boolean>> {
      return requestFn<ApiResponse<boolean>>(`${EXEC_BASE}/${executionId}/cancel`, { method: "POST" });
    },
    getProcess(executionId: IdLike): Promise<ApiResponse<WorkflowProcessResponse>> {
      return requestFn<ApiResponse<WorkflowProcessResponse>>(`${EXEC_BASE}/${executionId}/process`);
    },
    getCheckpoint(executionId: IdLike): Promise<ApiResponse<WorkflowExecutionCheckpointResponse>> {
      return requestFn<ApiResponse<WorkflowExecutionCheckpointResponse>>(`${EXEC_BASE}/${executionId}/checkpoint`);
    },
    getNodeDetail(executionId: IdLike, nodeKey: string): Promise<ApiResponse<NodeExecutionDetailResponse>> {
      return requestFn<ApiResponse<NodeExecutionDetailResponse>>(
        `${EXEC_BASE}/${executionId}/nodes/${encodeURIComponent(nodeKey)}`
      );
    },
    resume(executionId: IdLike, req: WorkflowResumeRequest): Promise<ApiResponse<boolean>> {
      return requestFn<ApiResponse<boolean>>(`${EXEC_BASE}/${executionId}/resume`, {
        method: "POST",
        body: JSON.stringify({
          inputsJson: req.inputsJson,
          data: req.data
        })
      });
    },
    streamResume(executionId: IdLike, callbacks: StreamCallbacks): StreamRunHandle {
      return createStreamRunHandle(`${EXEC_BASE}/${executionId}/stream-resume`, undefined, callbacks, options);
    },
    recover(executionId: IdLike): Promise<ApiResponse<WorkflowRunResponse>> {
      return requestFn<ApiResponse<WorkflowRunResponse>>(`${EXEC_BASE}/${executionId}/recover`, { method: "POST" });
    },
    getDebugView(executionId: IdLike): Promise<ApiResponse<WorkflowExecutionDebugViewResponse>> {
      return requestFn<ApiResponse<WorkflowExecutionDebugViewResponse>>(`${EXEC_BASE}/${executionId}/debug-view`);
    },
    debugNode(workflowId: IdLike, nodeKey: string, req: NodeDebugRequest): Promise<ApiResponse<NodeDebugResponse>> {
      const payload = {
        nodeKey,
        inputsJson: req.inputsJson ?? JSON.stringify(req.inputs ?? {})
      };
      return requestFn<ApiResponse<NodeDebugResponse>>(`${BASE}/${workflowId}/debug-node`, {
        method: "POST",
        body: JSON.stringify(payload)
      });
    }
  };
}

export type WorkflowTestRunClient = Pick<
  ReturnType<typeof createWorkflowV2Api>,
  "runSync" | "getProcess" | "runStream" | "resume"
>;

function createStreamRunHandle(
  path: string,
  payload: object | undefined,
  callbacks: StreamCallbacks,
  options: WorkflowApiFactoryOptions
): StreamRunHandle {
  const abortController = new AbortController();
  const done = (async () => {
    try {
      const response = await fetch(resolveAbsoluteUrl(path, options), {
        method: "POST",
        headers: buildStreamHeaders(options.resolveAppId),
        body: payload ? JSON.stringify(payload) : undefined,
        credentials: "include",
        signal: abortController.signal
      });

      if (!response.ok || !response.body) {
        throw new Error(`Stream request failed: HTTP ${response.status}`);
      }

      const reader = response.body.getReader();
      const decoder = new TextDecoder();
      let buffer = "";
      let currentEvent = "";
      let currentData: string[] = [];

      const flush = () => {
        if (!currentEvent) {
          currentData = [];
          return;
        }
        handleStreamEvent(currentEvent, currentData.join("\n"), callbacks);
        currentEvent = "";
        currentData = [];
      };

      while (true) {
        const { value, done: streamDone } = await reader.read();
        if (streamDone) {
          flush();
          break;
        }
        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split(/\r?\n/);
        buffer = lines.pop() ?? "";
        for (const line of lines) {
          if (line.length === 0) {
            flush();
            continue;
          }
          if (line.startsWith("event:")) {
            currentEvent = line.slice("event:".length).trim();
            continue;
          }
          if (line.startsWith("data:")) {
            currentData.push(line.slice("data:".length).trim());
          }
        }
      }
    } catch (error) {
      if (!abortController.signal.aborted) {
        callbacks.onError?.(error as Error);
      }
    }
  })();

  return {
    abort: () => abortController.abort(),
    done
  };
}

function resolveAbsoluteUrl(path: string, options: WorkflowApiFactoryOptions): string {
  if (options.resolveAbsoluteUrl) {
    return options.resolveAbsoluteUrl(path);
  }
  return path;
}

function buildStreamHeaders(resolveAppId?: () => string | null): Headers {
  const headers = new Headers({
    "Content-Type": "application/json",
    Accept: "text/event-stream"
  });
  headers.set("Idempotency-Key", generateIdempotencyKey());
  const token = getAccessToken();
  if (token) {
    headers.set("Authorization", `Bearer ${token}`);
  }
  const tenantId = getTenantId();
  if (tenantId) {
    headers.set("X-Tenant-Id", tenantId);
  }
  const csrfToken = getAntiforgeryToken();
  if (csrfToken) {
    headers.set("X-CSRF-TOKEN", csrfToken);
  }
  const appId = resolveAppId?.();
  if (appId) {
    headers.set("X-App-Id", appId);
    headers.set("X-App-Workspace", "1");
  }
  return headers;
}

function generateIdempotencyKey(): string {
  if (typeof crypto !== "undefined" && typeof crypto.randomUUID === "function") {
    return crypto.randomUUID();
  }

  return `idem-${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

function buildRunPayload(req: WorkflowRunRequest): { inputsJson?: string } {
  if (req.inputsJson) {
    return { inputsJson: req.inputsJson };
  }
  if (req.inputs) {
    return { inputsJson: JSON.stringify(req.inputs) };
  }
  return {};
}

function handleStreamEvent(eventName: string, dataText: string, callbacks: StreamCallbacks) {
  switch (eventName) {
    case "execution_start":
    case "execution_resume_start":
      callbacks.onExecutionStarted?.(safeJsonParse<ExecutionStartEvent>(dataText));
      break;
    case "node_start":
      callbacks.onNodeStarted?.(safeJsonParse<NodeStartEvent>(dataText));
      break;
    case "node_complete":
      callbacks.onNodeCompleted?.(safeJsonParse<NodeCompleteEvent>(dataText));
      break;
    case "node_output":
      callbacks.onNodeOutput?.(safeJsonParse<NodeOutputEvent>(dataText));
      break;
    case "node_failed":
      callbacks.onNodeFailed?.(safeJsonParse<NodeFailedEvent>(dataText));
      break;
    case "llm_output":
      callbacks.onLlmOutput?.(dataText);
      break;
    case "execution_complete":
      callbacks.onExecutionCompleted?.(safeJsonParse<ExecutionCompleteEvent>(dataText));
      break;
    case "execution_failed":
      callbacks.onExecutionFailed?.(safeJsonParse<ExecutionFailedEvent>(dataText));
      break;
    case "execution_cancelled":
      callbacks.onExecutionCancelled?.(safeJsonParse<ExecutionCancelledEvent>(dataText));
      break;
    case "execution_interrupted":
      callbacks.onExecutionInterrupted?.(safeJsonParse<ExecutionInterruptedEvent>(dataText));
      break;
    default:
      break;
  }
}

function safeJsonParse<T>(raw: string): T {
  if (!raw) {
    return {} as T;
  }
  try {
    return JSON.parse(raw) as T;
  } catch {
    return {} as T;
  }
}

function toBackendCanvasJson(editorCanvasJson: string): string {
  let parsed: unknown;
  try {
    parsed = JSON.parse(editorCanvasJson);
  } catch {
    return editorCanvasJson;
  }
  if (!parsed || typeof parsed !== "object") {
    return editorCanvasJson;
  }
  const maybeBackend = parsed as WorkflowCanvasPayload;
  if (Array.isArray(maybeBackend.nodes) && maybeBackend.nodes.every((node) => typeof node.type === "number")) {
    return JSON.stringify(maybeBackend);
  }

  const editorCanvas = parsed as CanvasSchema;
  const nodes = (editorCanvas.nodes ?? []).map((node) => {
    const editorNode = node as NodeSchema;
    const normalizedType = normalizeNodeTypeKey(String(editorNode.type));
    const config = normalizeConfigPayload(editorNode.configs);
    if (editorNode.inputMappings && Object.keys(editorNode.inputMappings).length > 0) {
      config.inputMappings = editorNode.inputMappings;
    }
    return {
      key: editorNode.key,
      type: workflowNodeTypeToValue(normalizedType),
      label: editorNode.title ?? normalizedType,
      config,
      layout: {
        x: editorNode.layout?.x ?? 0,
        y: editorNode.layout?.y ?? 0,
        width: editorNode.layout?.width ?? DEFAULT_NODE_WIDTH,
        height: editorNode.layout?.height ?? DEFAULT_NODE_HEIGHT
      }
    };
  });

  const connections = (editorCanvas.connections ?? []).map((connection) => {
    const editorConnection = connection as ConnectionSchema;
    return {
      sourceNodeKey: editorConnection.fromNode,
      sourcePort: editorConnection.fromPort ?? "output",
      targetNodeKey: editorConnection.toNode,
      targetPort: editorConnection.toPort ?? "input",
      condition: editorConnection.condition ?? null
    };
  });
  return JSON.stringify({ nodes, connections });
}

function toEditorCanvasJson(backendCanvasJson: string): string {
  let parsed: unknown;
  try {
    parsed = JSON.parse(backendCanvasJson);
  } catch {
    return backendCanvasJson;
  }
  if (!parsed || typeof parsed !== "object") {
    return backendCanvasJson;
  }
  const payload = parsed as WorkflowCanvasPayload;
  if (!Array.isArray(payload.nodes) || !Array.isArray(payload.connections)) {
    return backendCanvasJson;
  }
  const nodes = payload.nodes.map((node) => {
    const config = normalizeConfigPayload(node.config ?? {});
    const inputMappings = normalizeInputMappings(config.inputMappings);
    delete config.inputMappings;
    return {
      key: node.key,
      type: workflowNodeValueToKey(node.type),
      title: node.label ?? node.key,
      layout: {
        x: node.layout?.x ?? 0,
        y: node.layout?.y ?? 0,
        width: node.layout?.width ?? DEFAULT_NODE_WIDTH,
        height: node.layout?.height ?? DEFAULT_NODE_HEIGHT
      },
      configs: config,
      inputMappings
    };
  });
  const connections = payload.connections.map((connection) => ({
    fromNode: connection.sourceNodeKey,
    fromPort: connection.sourcePort ?? "output",
    toNode: connection.targetNodeKey,
    toPort: connection.targetPort ?? "input",
    condition: connection.condition ?? null
  }));
  return JSON.stringify({ nodes, connections });
}

function normalizeConfigPayload(config: unknown): Record<string, unknown> {
  if (!config || typeof config !== "object" || Array.isArray(config)) {
    return {};
  }
  return { ...(config as Record<string, unknown>) };
}

function normalizeInputMappings(raw: unknown): Record<string, string> {
  if (!raw || typeof raw !== "object" || Array.isArray(raw)) {
    return {};
  }
  const result: Record<string, string> = {};
  for (const [key, value] of Object.entries(raw as Record<string, unknown>)) {
    if (typeof value === "string") {
      result[key] = value;
    }
  }
  return result;
}

