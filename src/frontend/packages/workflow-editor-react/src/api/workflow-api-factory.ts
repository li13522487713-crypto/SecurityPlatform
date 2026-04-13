import type { ApiResponse, PagedResult } from "@atlas/shared-react-core/types";
import { getAccessToken, getAntiforgeryToken, getTenantId } from "@atlas/shared-react-core/utils";
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
  type NodeBlockedEvent,
  type NodeSkippedEvent,
  type NodeOutputEvent,
  type EdgeStatusChangedEvent,
  type BranchDecisionEvent,
  type NodeSchema,
  type NodeStartEvent,
  type NodeTemplateMetadata,
  type NodeTypeMetadata,
  type RunTrace,
  type WorkflowCanvasPayload,
  type WorkflowCreateRequest,
  type WorkflowDetailQuery,
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
  type WorkflowValidateRequest,
  type WorkflowUpdateMetaRequest,
  type WorkflowVersionDiff,
  type WorkflowVersionRollbackResult,
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
  onNodeSkipped?: (ev: NodeSkippedEvent) => void;
  onNodeBlocked?: (ev: NodeBlockedEvent) => void;
  onEdgeStatusChanged?: (ev: EdgeStatusChangedEvent) => void;
  onBranchDecision?: (ev: BranchDecisionEvent) => void;
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
    getDetail(id: IdLike, query?: WorkflowDetailQuery): Promise<ApiResponse<WorkflowDetailResponse>> {
      return requestFn<ApiResponse<WorkflowDetailResponse>>(`${BASE}/${id}${buildDetailQuery(query)}`).then((res) => {
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
    getVersionDiff(id: IdLike, fromVersionId: IdLike, toVersionId: IdLike): Promise<ApiResponse<WorkflowVersionDiff>> {
      return requestFn<ApiResponse<WorkflowVersionDiff>>(`${BASE}/${id}/versions/${fromVersionId}/diff/${toVersionId}`);
    },
    rollbackVersion(id: IdLike, versionId: IdLike): Promise<ApiResponse<WorkflowVersionRollbackResult>> {
      return requestFn<ApiResponse<WorkflowVersionRollbackResult>>(
        `${BASE}/${id}/versions/${versionId}/rollback`,
        { method: "POST" }
      );
    },
    getNodeTypes(): Promise<ApiResponse<NodeTypeMetadata[]>> {
      return requestFn<ApiResponse<NodeTypeMetadata[]>>(`${BASE}/node-types`);
    },
    getNodeTemplates(): Promise<ApiResponse<NodeTemplateMetadata[]>> {
      return requestFn<ApiResponse<NodeTemplateMetadata[]>>(`${BASE}/node-templates`);
    },
    validate(id: IdLike, req: WorkflowValidateRequest): Promise<ApiResponse<{ isValid?: boolean; errors?: string[] }>> {
      const canvasJson = req.canvasJson ?? (req.canvas ? JSON.stringify(req.canvas) : undefined);
      return requestFn<ApiResponse<{ isValid?: boolean; errors?: string[] }>>(`${BASE}/${id}/validate`, {
        method: "POST",
        body: JSON.stringify({
          canvasJson: canvasJson ? toBackendCanvasJson(canvasJson) : undefined
        })
      });
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
    getTrace(executionId: IdLike): Promise<ApiResponse<RunTrace>> {
      return requestFn<ApiResponse<RunTrace>>(`${EXEC_BASE}/${executionId}/trace`);
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
          data: req.data,
          variableOverrides: req.variableOverrides
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
        inputsJson: req.inputsJson ?? JSON.stringify(req.inputs ?? {}),
        source: req.source,
        versionId: req.versionId
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

function buildRunPayload(req: WorkflowRunRequest): { inputsJson?: string; source?: "published" | "draft" } {
  const payload: { inputsJson?: string; source?: "published" | "draft" } = {};
  if (req.inputsJson) {
    payload.inputsJson = req.inputsJson;
  } else if (req.inputs) {
    payload.inputsJson = JSON.stringify(req.inputs);
  }
  if (req.source) {
    payload.source = req.source;
  }
  return payload;
}

function buildDetailQuery(query?: WorkflowDetailQuery): string {
  if (!query) {
    return "";
  }

  const params = new URLSearchParams();
  if (query.source) {
    params.set("source", query.source);
  }
  if (query.versionId) {
    params.set("versionId", query.versionId);
  }
  const queryText = params.toString();
  return queryText ? `?${queryText}` : "";
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
    case "node_skipped":
      callbacks.onNodeSkipped?.(safeJsonParse<NodeSkippedEvent>(dataText));
      break;
    case "node_blocked":
      callbacks.onNodeBlocked?.(safeJsonParse<NodeBlockedEvent>(dataText));
      break;
    case "edge_status_changed":
      callbacks.onEdgeStatusChanged?.(safeJsonParse<EdgeStatusChangedEvent>(dataText));
      break;
    case "branch_decision":
      callbacks.onBranchDecision?.(safeJsonParse<BranchDecisionEvent>(dataText));
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

  if (isEditorCanvasPayload(parsed)) {
    return JSON.stringify(normalizeEditorCanvasPayload(parsed));
  }

  if (isLegacyBackendCanvasPayload(parsed)) {
    return editorCanvasJson;
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
      },
      childCanvas: editorNode.childCanvas ? JSON.parse(toBackendCanvasJson(JSON.stringify(editorNode.childCanvas))) : undefined,
      inputTypes: editorNode.inputTypes,
      outputTypes: editorNode.outputTypes,
      inputSources: editorNode.inputSources,
      outputSources: editorNode.outputSources,
      ports: editorNode.ports,
      version: editorNode.version,
      debugMeta: editorNode.debugMeta
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
  return JSON.stringify({
    nodes,
    connections,
    schemaVersion: editorCanvas.schemaVersion,
    viewport: editorCanvas.viewport,
    globals: editorCanvas.globals
  });
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

  if (isEditorCanvasPayload(parsed)) {
    return JSON.stringify(normalizeEditorCanvasPayload(parsed));
  }

  if (!isLegacyBackendCanvasPayload(parsed)) {
    return backendCanvasJson;
  }

  const payload = parsed as WorkflowCanvasPayload;
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
      inputMappings,
      childCanvas: node.childCanvas ? JSON.parse(toEditorCanvasJson(JSON.stringify(node.childCanvas))) : undefined,
      inputTypes: node.inputTypes,
      outputTypes: node.outputTypes,
      inputSources: node.inputSources,
      outputSources: node.outputSources,
      ports: node.ports,
      version: node.version,
      debugMeta: node.debugMeta
    };
  });
  const connections = payload.connections.map((connection) => ({
    fromNode: connection.sourceNodeKey,
    fromPort: connection.sourcePort ?? "output",
    toNode: connection.targetNodeKey,
    toPort: connection.targetPort ?? "input",
    condition: connection.condition ?? null
  }));
  return JSON.stringify({
    nodes: normalizeEditorCanvasPayload({ nodes, connections, schemaVersion: payload.schemaVersion, viewport: payload.viewport, globals: payload.globals }).nodes,
    connections,
    schemaVersion: payload.schemaVersion,
    viewport: payload.viewport,
    globals: payload.globals
  });
}

function normalizeConfigPayload(config: unknown): Record<string, unknown> {
  if (!config || typeof config !== "object" || Array.isArray(config)) {
    return {};
  }
  return { ...(config as Record<string, unknown>) };
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value) && typeof value === "object" && !Array.isArray(value);
}

function isEditorCanvasPayload(value: unknown): value is CanvasSchema {
  if (!isRecord(value) || !Array.isArray(value.nodes) || !Array.isArray(value.connections)) {
    return false;
  }

  const sampleNode = value.nodes.find(isRecord);
  const sampleConnection = value.connections.find(isRecord);

  return Boolean(
    sampleNode &&
      ("title" in sampleNode || "configs" in sampleNode || "inputMappings" in sampleNode || "childCanvas" in sampleNode)
  ) || Boolean(sampleConnection && ("fromNode" in sampleConnection || "toNode" in sampleConnection));
}

function isLegacyBackendCanvasPayload(value: unknown): value is WorkflowCanvasPayload {
  if (!isRecord(value) || !Array.isArray(value.nodes) || !Array.isArray(value.connections)) {
    return false;
  }

  const sampleNode = value.nodes.find(isRecord);
  const sampleConnection = value.connections.find(isRecord);

  return Boolean(
    sampleNode &&
      ("label" in sampleNode || "config" in sampleNode || typeof sampleNode.type === "number")
  ) || Boolean(sampleConnection && ("sourceNodeKey" in sampleConnection || "targetNodeKey" in sampleConnection));
}

function normalizeEditorCanvasPayload(canvas: CanvasSchema): CanvasSchema {
  return {
    ...canvas,
    nodes: (canvas.nodes ?? []).map((node) => ({
      ...node,
      configs: normalizeEditorNodeConfig(String(node.type), normalizeConfigPayload(node.configs)),
      childCanvas: node.childCanvas ? normalizeEditorCanvasPayload(node.childCanvas) : undefined
    }))
  };
}

function normalizeEditorNodeConfig(type: string, config: Record<string, unknown>): Record<string, unknown> {
  const next = { ...config };
  const nestedEntry = isRecord(config.entry) ? config.entry : null;
  const nestedLlm = isRecord(config.llm) ? config.llm : null;
  const nestedExit = isRecord(config.exit) ? config.exit : null;

  if (type === "Entry") {
    const entryVariable = firstNonEmptyString(next.entryVariable, next.variable, nestedEntry?.entryVariable, nestedEntry?.variable);
    const entryDescription = firstNonEmptyString(next.entryDescription, next.description, nestedEntry?.entryDescription, nestedEntry?.description);
    const entryAutoSaveHistory = firstBoolean(next.entryAutoSaveHistory, next.autoSaveHistory, nestedEntry?.entryAutoSaveHistory, nestedEntry?.autoSaveHistory);

    if (entryVariable) {
      next.entryVariable = entryVariable;
    }
    if (entryDescription) {
      next.entryDescription = entryDescription;
    }
    if (typeof entryAutoSaveHistory === "boolean") {
      next.entryAutoSaveHistory = entryAutoSaveHistory;
    }
  }

  if (type === "Llm") {
    const provider = firstNonEmptyString(next.provider, nestedLlm?.provider);
    const model = firstNonEmptyString(next.model, nestedLlm?.model);
    const prompt = firstNonEmptyString(next.prompt, next.userPrompt, nestedLlm?.prompt, nestedLlm?.userPrompt);
    const outputKey = firstNonEmptyString(next.outputKey, nestedLlm?.outputKey, "result");
    const systemPrompt = firstNonEmptyString(next.systemPrompt, nestedLlm?.systemPrompt);

    if (provider) {
      next.provider = provider;
    }
    if (model) {
      next.model = model;
    }
    if (prompt) {
      next.prompt = prompt;
    }
    if (outputKey) {
      next.outputKey = outputKey;
    }
    if (systemPrompt) {
      next.systemPrompt = systemPrompt;
    }
  }

  if (type === "Exit") {
    const exitTerminateMode = firstNonEmptyString(next.exitTerminateMode, next.terminateMode, nestedExit?.exitTerminateMode, nestedExit?.terminateMode);
    const exitTemplate = firstNonEmptyString(next.exitTemplate, next.template, nestedExit?.exitTemplate, nestedExit?.template);
    const exitStreaming = firstBoolean(next.exitStreaming, next.streaming, nestedExit?.exitStreaming, nestedExit?.streaming);

    if (exitTerminateMode) {
      next.exitTerminateMode = exitTerminateMode;
    }
    if (exitTemplate) {
      next.exitTemplate = exitTemplate;
    }
    if (typeof exitStreaming === "boolean") {
      next.exitStreaming = exitStreaming;
    }
  }

  return next;
}

function firstNonEmptyString(...values: unknown[]): string | undefined {
  for (const value of values) {
    if (typeof value === "string" && value.trim().length > 0) {
      return value.trim();
    }
  }
  return undefined;
}

function firstBoolean(...values: unknown[]): boolean | undefined {
  for (const value of values) {
    if (typeof value === "boolean") {
      return value;
    }
  }
  return undefined;
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

