/**
 * Workflow V2 API（与 /api/v2/workflows 契约对齐）
 */
import { requestApi, API_BASE } from '@/services/api-core'
import { getAccessToken, getTenantId, getAntiforgeryToken } from '@/utils/auth'
import { getCurrentAppIdFromStorage } from '@/utils/app-context'
import { workflowNodeTypeToValue, workflowNodeValueToKey, normalizeNodeTypeKey } from '@/types/workflow-v2'
import type {
  CanvasSchema,
  ConnectionSchema,
  ExecutionCancelledEvent,
  ExecutionCompleteEvent,
  ExecutionFailedEvent,
  ExecutionInterruptedEvent,
  ExecutionStartEvent,
  NodeSchema,
  WorkflowCanvasPayload,
  WorkflowCreateRequest,
  WorkflowDetailResponse,
  WorkflowListItem,
  WorkflowVersionItem,
  WorkflowPublishRequest,
  WorkflowSaveRequest,
  WorkflowUpdateMetaRequest,
  NodeTypeMetadata,
  WorkflowRunRequest,
  WorkflowRunResponse,
  WorkflowProcessResponse,
  NodeExecutionDetailResponse,
  WorkflowResumeRequest,
  NodeDebugRequest,
  NodeDebugResponse,
  NodeStartEvent,
  NodeCompleteEvent,
  NodeOutputEvent,
  NodeFailedEvent,
} from '@/types/workflow-v2'
import type { ApiResponse, PagedResult } from '@/types/api'

const BASE = '/api/v2/workflows'
const EXEC_BASE = '/api/v2/workflows/executions'
const DEFAULT_NODE_WIDTH = 160
const DEFAULT_NODE_HEIGHT = 60

// ============ SSE 回调接口 ============

export interface StreamCallbacks {
  onExecutionStarted?: (ev: ExecutionStartEvent) => void
  onNodeStarted?: (ev: NodeStartEvent) => void
  onNodeOutput?: (ev: NodeOutputEvent) => void
  onNodeCompleted?: (ev: NodeCompleteEvent) => void
  onNodeFailed?: (ev: NodeFailedEvent) => void
  onLlmOutput?: (content: string) => void
  onExecutionCompleted?: (ev: ExecutionCompleteEvent) => void
  onExecutionFailed?: (ev: ExecutionFailedEvent) => void
  onExecutionCancelled?: (ev: ExecutionCancelledEvent) => void
  onExecutionInterrupted?: (ev: ExecutionInterruptedEvent) => void
  onError?: (err: Event | Error) => void
}

export interface StreamRunHandle {
  abort: () => void
  done: Promise<void>
}

// ============ 工作流 V2 API 对象 ============

export const workflowV2Api = {
  create(req: WorkflowCreateRequest): Promise<ApiResponse<{ id: string }>> {
    return requestApi<ApiResponse<{ id: string }>>(BASE, { method: 'POST', body: JSON.stringify(req) })
  },

  list(pageIndex = 1, pageSize = 20, keyword?: string): Promise<ApiResponse<PagedResult<WorkflowListItem>>> {
    const params = new URLSearchParams({ pageIndex: String(pageIndex), pageSize: String(pageSize) })
    if (keyword) params.set('keyword', keyword)
    return requestApi<ApiResponse<PagedResult<WorkflowListItem>>>(`${BASE}?${params}`)
  },

  getDetail(id: number): Promise<ApiResponse<WorkflowDetailResponse>> {
    return requestApi<ApiResponse<WorkflowDetailResponse>>(`${BASE}/${id}`).then(res => {
      if (res.data?.canvasJson) {
        return {
          ...res,
          data: {
            ...res.data,
            canvasJson: toEditorCanvasJson(res.data.canvasJson),
          },
        }
      }

      return res
    })
  },

  saveDraft(id: number, req: WorkflowSaveRequest): Promise<ApiResponse<boolean>> {
    return requestApi<ApiResponse<boolean>>(`${BASE}/${id}/draft`, {
      method: 'PUT',
      body: JSON.stringify({
        canvasJson: toBackendCanvasJson(req.canvasJson),
        commitId: req.commitId ?? null,
      }),
    })
  },

  updateMeta(id: number, req: WorkflowUpdateMetaRequest): Promise<ApiResponse<boolean>> {
    return requestApi<ApiResponse<boolean>>(`${BASE}/${id}/meta`, { method: 'PUT', body: JSON.stringify(req) })
  },

  publish(id: number, req: WorkflowPublishRequest): Promise<ApiResponse<{ id: string }>> {
    return requestApi<ApiResponse<{ id: string }>>(`${BASE}/${id}/publish`, {
      method: 'POST',
      body: JSON.stringify(req),
    })
  },

  copy(id: number): Promise<ApiResponse<{ id: string }>> {
    return requestApi<ApiResponse<{ id: string }>>(`${BASE}/${id}/copy`, { method: 'POST' })
  },

  delete(id: number): Promise<ApiResponse<boolean>> {
    return requestApi<ApiResponse<boolean>>(`${BASE}/${id}`, { method: 'DELETE' })
  },

  getVersions(id: number): Promise<ApiResponse<WorkflowVersionItem[]>> {
    return requestApi<ApiResponse<WorkflowVersionItem[]>>(`${BASE}/${id}/versions`)
  },

  getNodeTypes(): Promise<ApiResponse<NodeTypeMetadata[]>> {
    return requestApi<ApiResponse<NodeTypeMetadata[]>>(`${BASE}/node-types`)
  },

  runSync(id: number, req: WorkflowRunRequest): Promise<ApiResponse<WorkflowRunResponse>> {
    const payload = buildRunPayload(req)
    return requestApi<ApiResponse<WorkflowRunResponse>>(`${BASE}/${id}/run`, {
      method: 'POST',
      body: JSON.stringify(payload),
    })
  },

  runStream(id: number, req: WorkflowRunRequest, callbacks: StreamCallbacks): StreamRunHandle {
    const abortController = new AbortController()
    const payload = buildRunPayload(req)

    const done = (async () => {
      try {
        const response = await fetch(buildAbsoluteApiUrl(`${BASE}/${id}/stream`), {
          method: 'POST',
          headers: buildStreamHeaders(),
          body: JSON.stringify(payload),
          credentials: 'include',
          signal: abortController.signal,
        })

        if (!response.ok || !response.body) {
          throw new Error(`流式执行失败: HTTP ${response.status}`)
        }

        const reader = response.body.getReader()
        const decoder = new TextDecoder()
        let buffer = ''
        let currentEvent = ''
        let currentData: string[] = []

        const flush = () => {
          if (!currentEvent) {
            currentData = []
            return
          }

          const rawData = currentData.join('\n')
          handleStreamEvent(currentEvent, rawData, callbacks)
          currentEvent = ''
          currentData = []
        }

        while (true) {
          const { value, done: streamDone } = await reader.read()
          if (streamDone) {
            flush()
            break
          }

          buffer += decoder.decode(value, { stream: true })
          const lines = buffer.split(/\r?\n/)
          buffer = lines.pop() ?? ''

          for (const line of lines) {
            if (line.length === 0) {
              flush()
              continue
            }

            if (line.startsWith('event:')) {
              currentEvent = line.slice('event:'.length).trim()
              continue
            }

            if (line.startsWith('data:')) {
              currentData.push(line.slice('data:'.length).trim())
            }
          }
        }
      } catch (error) {
        if (!abortController.signal.aborted) {
          callbacks.onError?.(error as Error)
        }
      }
    })()

    return {
      abort: () => abortController.abort(),
      done,
    }
  },

  cancel(executionId: number): Promise<ApiResponse<boolean>> {
    return requestApi<ApiResponse<boolean>>(`${EXEC_BASE}/${executionId}/cancel`, { method: 'POST' })
  },

  getProcess(executionId: number): Promise<ApiResponse<WorkflowProcessResponse>> {
    return requestApi<ApiResponse<WorkflowProcessResponse>>(`${EXEC_BASE}/${executionId}/process`)
  },

  getNodeDetail(executionId: number, nodeKey: string): Promise<ApiResponse<NodeExecutionDetailResponse>> {
    return requestApi<ApiResponse<NodeExecutionDetailResponse>>(
      `${EXEC_BASE}/${executionId}/nodes/${encodeURIComponent(nodeKey)}`,
    )
  },

  resume(executionId: number, req: WorkflowResumeRequest): Promise<ApiResponse<boolean>> {
    void req
    return requestApi<ApiResponse<boolean>>(`${EXEC_BASE}/${executionId}/resume`, {
      method: 'POST',
    })
  },

  debugNode(workflowId: number, nodeKey: string, req: NodeDebugRequest): Promise<ApiResponse<NodeDebugResponse>> {
    const payload = {
      nodeKey,
      inputsJson: req.inputsJson ?? JSON.stringify(req.inputs ?? {}),
    }
    return requestApi<ApiResponse<NodeDebugResponse>>(`${BASE}/${workflowId}/debug-node`, {
      method: 'POST',
      body: JSON.stringify(payload),
    })
  },
}

// ============ 向后兼容的独立导出 ============

export function createWorkflow(req: WorkflowCreateRequest): Promise<ApiResponse<number>> {
  return workflowV2Api.create(req).then(res => ({
    ...res,
    data: res.data?.id ? Number(res.data.id) : undefined,
  }))
}

export function listWorkflows(pageIndex = 1, pageSize = 20, keyword?: string): Promise<ApiResponse<PagedResult<WorkflowListItem>>> {
  return workflowV2Api.list(pageIndex, pageSize, keyword)
}

export function getWorkflowCanvas(id: number): Promise<ApiResponse<WorkflowDetailResponse>> {
  return workflowV2Api.getDetail(id)
}

export function saveWorkflowDraft(id: number, req: WorkflowSaveRequest): Promise<ApiResponse<boolean>> {
  return workflowV2Api.saveDraft(id, req)
}

export function updateWorkflowMeta(id: number, req: WorkflowUpdateMetaRequest): Promise<ApiResponse<boolean>> {
  return workflowV2Api.updateMeta(id, req)
}

export function publishWorkflow(id: number, req: WorkflowPublishRequest): Promise<ApiResponse<{ id: string }>> {
  return workflowV2Api.publish(id, req)
}

export function copyWorkflow(id: number): Promise<ApiResponse<number>> {
  return workflowV2Api.copy(id).then(res => ({
    ...res,
    data: res.data?.id ? Number(res.data.id) : undefined,
  }))
}

export function deleteWorkflow(id: number): Promise<ApiResponse<boolean>> {
  return workflowV2Api.delete(id)
}

export function listWorkflowVersions(id: number): Promise<ApiResponse<WorkflowVersionItem[]>> {
  return workflowV2Api.getVersions(id)
}

export function getNodeTypes(): Promise<ApiResponse<NodeTypeMetadata[]>> {
  return workflowV2Api.getNodeTypes()
}

export function syncRunWorkflow(id: number, req: WorkflowRunRequest): Promise<ApiResponse<WorkflowRunResponse>> {
  return workflowV2Api.runSync(id, req)
}

export function asyncRunWorkflow(id: number, req: WorkflowRunRequest): Promise<ApiResponse<number>> {
  return workflowV2Api.runSync(id, req).then(res => ({
    ...res,
    data: res.data?.executionId ? Number(res.data.executionId) : undefined,
  }))
}

export function cancelExecution(executionId: number): Promise<ApiResponse<boolean>> {
  return workflowV2Api.cancel(executionId)
}

export function getExecutionProcess(executionId: number): Promise<ApiResponse<WorkflowProcessResponse>> {
  return workflowV2Api.getProcess(executionId)
}

export function getNodeExecutionDetail(executionId: number, nodeKey: string): Promise<ApiResponse<NodeExecutionDetailResponse>> {
  return workflowV2Api.getNodeDetail(executionId, nodeKey)
}

export function resumeExecution(executionId: number, req: WorkflowResumeRequest): Promise<ApiResponse<boolean>> {
  return workflowV2Api.resume(executionId, req)
}

export function debugNode(workflowId: number, nodeKey: string, req: NodeDebugRequest): Promise<ApiResponse<NodeDebugResponse>> {
  return workflowV2Api.debugNode(workflowId, nodeKey, req)
}

function buildRunPayload(req: WorkflowRunRequest): { inputsJson?: string } {
  if (req.inputsJson) {
    return { inputsJson: req.inputsJson }
  }

  if (req.inputs) {
    return { inputsJson: JSON.stringify(req.inputs) }
  }

  return {}
}

function handleStreamEvent(eventName: string, dataText: string, callbacks: StreamCallbacks) {
  switch (eventName) {
    case 'execution_start':
      callbacks.onExecutionStarted?.(safeJsonParse<ExecutionStartEvent>(dataText))
      break
    case 'node_start':
      callbacks.onNodeStarted?.(safeJsonParse<NodeStartEvent>(dataText))
      break
    case 'node_complete':
      callbacks.onNodeCompleted?.(safeJsonParse<NodeCompleteEvent>(dataText))
      break
    case 'node_output':
      callbacks.onNodeOutput?.(safeJsonParse<NodeOutputEvent>(dataText))
      break
    case 'node_failed':
      callbacks.onNodeFailed?.(safeJsonParse<NodeFailedEvent>(dataText))
      break
    case 'llm_output':
      callbacks.onLlmOutput?.(dataText)
      break
    case 'execution_complete':
      callbacks.onExecutionCompleted?.(safeJsonParse<ExecutionCompleteEvent>(dataText))
      break
    case 'execution_failed':
      callbacks.onExecutionFailed?.(safeJsonParse<ExecutionFailedEvent>(dataText))
      break
    case 'execution_cancelled':
      callbacks.onExecutionCancelled?.(safeJsonParse<ExecutionCancelledEvent>(dataText))
      break
    case 'execution_interrupted':
      callbacks.onExecutionInterrupted?.(safeJsonParse<ExecutionInterruptedEvent>(dataText))
      break
    default:
      break
  }
}

function safeJsonParse<T>(raw: string): T {
  if (!raw) {
    return {} as T
  }

  try {
    return JSON.parse(raw) as T
  } catch {
    return {} as T
  }
}

function toBackendCanvasJson(editorCanvasJson: string): string {
  let parsed: unknown
  try {
    parsed = JSON.parse(editorCanvasJson)
  } catch {
    return editorCanvasJson
  }

  if (!parsed || typeof parsed !== 'object') {
    return editorCanvasJson
  }

  const maybeBackend = parsed as WorkflowCanvasPayload
  if (Array.isArray(maybeBackend.nodes) && maybeBackend.nodes.every(node => typeof node.type === 'number')) {
    return JSON.stringify(maybeBackend)
  }

  const editorCanvas = parsed as CanvasSchema
  const nodes = (editorCanvas.nodes ?? []).map(node => {
    const editorNode = node as NodeSchema
    const normalizedType = normalizeNodeTypeKey(String(editorNode.type))
    const config = normalizeConfigPayload(editorNode.configs)

    if (editorNode.inputMappings && Object.keys(editorNode.inputMappings).length > 0) {
      config.inputMappings = editorNode.inputMappings
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
        height: editorNode.layout?.height ?? DEFAULT_NODE_HEIGHT,
      },
    }
  })

  const connections = (editorCanvas.connections ?? []).map(connection => {
    const editorConnection = connection as ConnectionSchema
    return {
      sourceNodeKey: editorConnection.fromNode,
      sourcePort: editorConnection.fromPort ?? 'output',
      targetNodeKey: editorConnection.toNode,
      targetPort: editorConnection.toPort ?? 'input',
      condition: editorConnection.condition ?? null,
    }
  })

  return JSON.stringify({ nodes, connections })
}

function toEditorCanvasJson(backendCanvasJson: string): string {
  let parsed: unknown
  try {
    parsed = JSON.parse(backendCanvasJson)
  } catch {
    return backendCanvasJson
  }

  if (!parsed || typeof parsed !== 'object') {
    return backendCanvasJson
  }

  const payload = parsed as WorkflowCanvasPayload
  if (!Array.isArray(payload.nodes) || !Array.isArray(payload.connections)) {
    return backendCanvasJson
  }

  const nodes = payload.nodes.map(node => {
    const config = normalizeConfigPayload(node.config ?? {})
    const inputMappings = normalizeInputMappings(config.inputMappings)
    delete config.inputMappings

    return {
      key: node.key,
      type: workflowNodeValueToKey(node.type),
      title: node.label ?? node.key,
      layout: {
        x: node.layout?.x ?? 0,
        y: node.layout?.y ?? 0,
        width: node.layout?.width ?? DEFAULT_NODE_WIDTH,
        height: node.layout?.height ?? DEFAULT_NODE_HEIGHT,
      },
      configs: config,
      inputMappings,
    }
  })

  const connections = payload.connections.map(connection => ({
    fromNode: connection.sourceNodeKey,
    fromPort: connection.sourcePort ?? 'output',
    toNode: connection.targetNodeKey,
    toPort: connection.targetPort ?? 'input',
    condition: connection.condition ?? null,
  }))

  return JSON.stringify({ nodes, connections })
}

function normalizeConfigPayload(config: unknown): Record<string, unknown> {
  if (!config || typeof config !== 'object' || Array.isArray(config)) {
    return {}
  }

  return { ...(config as Record<string, unknown>) }
}

function normalizeInputMappings(raw: unknown): Record<string, string> {
  if (!raw || typeof raw !== 'object' || Array.isArray(raw)) {
    return {}
  }

  const result: Record<string, string> = {}
  for (const [key, value] of Object.entries(raw as Record<string, unknown>)) {
    if (typeof value === 'string') {
      result[key] = value
    }
  }

  return result
}

function buildStreamHeaders(): Headers {
  const headers = new Headers({
    'Content-Type': 'application/json',
    Accept: 'text/event-stream',
  })

  const token = getAccessToken()
  if (token) {
    headers.set('Authorization', `Bearer ${token}`)
  }

  const tenantId = getTenantId()
  if (tenantId) {
    headers.set('X-Tenant-Id', tenantId)
  }

  const csrfToken = getAntiforgeryToken()
  if (csrfToken) {
    headers.set('X-CSRF-TOKEN', csrfToken)
  }

  const appId = resolveCurrentAppId()
  if (appId) {
    headers.set('X-App-Id', appId)
    headers.set('X-App-Workspace', '1')
  }

  return headers
}

function buildAbsoluteApiUrl(path: string): string {
  if (path.startsWith('http://') || path.startsWith('https://')) {
    return path
  }

  if (API_BASE.startsWith('http://') || API_BASE.startsWith('https://')) {
    const normalizedPath = path.startsWith('/') ? path : `/${path}`
    return new URL(normalizedPath, API_BASE).toString()
  }

  return path
}

function resolveCurrentAppId(): string | null {
  if (typeof window !== 'undefined') {
    const match = window.location.pathname.match(/^\/apps\/([^/]+)/)
    if (match?.[1]) {
      return decodeURIComponent(match[1])
    }
  }

  return getCurrentAppIdFromStorage()
}
