/**
 * Coze 风格工作流引擎 v2 API
 * 对应后端 /api/v2/workflows 路由
 */
import { requestApi } from '@/services/api-core'
import type {
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
  NodeErrorEvent,
  WorkflowDoneEvent,
  WorkflowInterruptEvent,
} from '@/types/workflow-v2'
import type { ApiResponse, PagedResult } from '@/types/api'

const BASE = '/api/v2/workflows'
const EXEC_BASE = '/api/v2/executions'

// ============ SSE 回调接口 ============

export interface StreamCallbacks {
  onNodeStarted?: (ev: NodeStartEvent) => void
  onNodeCompleted?: (ev: NodeCompleteEvent) => void
  onNodeError?: (ev: NodeErrorEvent) => void
  onWorkflowDone?: (ev: WorkflowDoneEvent) => void
  onWorkflowInterrupted?: (ev: WorkflowInterruptEvent) => void
  onError?: (err: Event | Error) => void
}

// ============ 工作流 V2 API 对象 ============

export const workflowV2Api = {
  create(req: WorkflowCreateRequest): Promise<ApiResponse<number>> {
    return requestApi<ApiResponse<number>>(BASE, { method: 'POST', body: JSON.stringify(req) })
  },

  list(pageIndex = 1, pageSize = 20, keyword?: string): Promise<ApiResponse<PagedResult<WorkflowListItem>>> {
    const params = new URLSearchParams({ pageIndex: String(pageIndex), pageSize: String(pageSize) })
    if (keyword) params.set('keyword', keyword)
    return requestApi<ApiResponse<PagedResult<WorkflowListItem>>>(`${BASE}?${params}`)
  },

  getDetail(id: number): Promise<ApiResponse<WorkflowDetailResponse>> {
    return requestApi<ApiResponse<WorkflowDetailResponse>>(`${BASE}/${id}/canvas`)
  },

  saveDraft(id: number, req: WorkflowSaveRequest): Promise<ApiResponse<boolean>> {
    return requestApi<ApiResponse<boolean>>(`${BASE}/${id}/draft`, { method: 'PUT', body: JSON.stringify(req) })
  },

  updateMeta(id: number, req: WorkflowUpdateMetaRequest): Promise<ApiResponse<boolean>> {
    return requestApi<ApiResponse<boolean>>(`${BASE}/${id}/meta`, { method: 'PATCH', body: JSON.stringify(req) })
  },

  publish(id: number, req: WorkflowPublishRequest): Promise<ApiResponse<WorkflowVersionItem>> {
    return requestApi<ApiResponse<WorkflowVersionItem>>(`${BASE}/${id}/publish`, {
      method: 'POST',
      body: JSON.stringify(req),
    })
  },

  copy(id: number): Promise<ApiResponse<number>> {
    return requestApi<ApiResponse<number>>(`${BASE}/${id}/copy`, { method: 'POST' })
  },

  delete(id: number): Promise<ApiResponse<boolean>> {
    return requestApi<ApiResponse<boolean>>(`${BASE}/${id}`, { method: 'DELETE' })
  },

  getVersions(id: number): Promise<ApiResponse<WorkflowVersionItem[]>> {
    return requestApi<ApiResponse<WorkflowVersionItem[]>>(`${BASE}/${id}/versions`)
  },

  getNodeTypes(): Promise<ApiResponse<NodeTypeMetadata[]>> {
    return requestApi<ApiResponse<NodeTypeMetadata[]>>('/api/v2/workflow-nodes/types')
  },

  runSync(id: number, req: WorkflowRunRequest): Promise<ApiResponse<WorkflowRunResponse>> {
    return requestApi<ApiResponse<WorkflowRunResponse>>(`${BASE}/${id}/run`, {
      method: 'POST',
      body: JSON.stringify(req),
    })
  },

  runAsync(id: number, req: WorkflowRunRequest): Promise<ApiResponse<number>> {
    return requestApi<ApiResponse<number>>(`${BASE}/${id}/async-run`, {
      method: 'POST',
      body: JSON.stringify(req),
    })
  },

  runStream(id: number, req: WorkflowRunRequest, callbacks: StreamCallbacks): EventSource {
    const token = localStorage.getItem('token') ?? ''
    const tenantId = localStorage.getItem('tenantId') ?? ''
    const params = new URLSearchParams({
      inputsJson: JSON.stringify(req.inputs),
    })
    if (req.version) params.set('version', req.version)
    if (token) params.set('_token', token)
    if (tenantId) params.set('_tenantId', tenantId)

    const es = new EventSource(`${BASE}/${id}/stream-run?${params}`)

    es.addEventListener('node_started', (e) => {
      try { callbacks.onNodeStarted?.(JSON.parse((e as MessageEvent).data) as NodeStartEvent) } catch { /* ignore */ }
    })
    es.addEventListener('node_completed', (e) => {
      try { callbacks.onNodeCompleted?.(JSON.parse((e as MessageEvent).data) as NodeCompleteEvent) } catch { /* ignore */ }
    })
    es.addEventListener('node_error', (e) => {
      try { callbacks.onNodeError?.(JSON.parse((e as MessageEvent).data) as NodeErrorEvent) } catch { /* ignore */ }
    })
    es.addEventListener('workflow_done', (e) => {
      try {
        callbacks.onWorkflowDone?.(JSON.parse((e as MessageEvent).data) as WorkflowDoneEvent)
      } catch { /* ignore */ }
      es.close()
    })
    es.addEventListener('workflow_interrupted', (e) => {
      try {
        callbacks.onWorkflowInterrupted?.(JSON.parse((e as MessageEvent).data) as WorkflowInterruptEvent)
      } catch { /* ignore */ }
    })
    es.onerror = (err) => {
      callbacks.onError?.(err)
      es.close()
    }

    return es
  },

  cancel(executionId: number): Promise<ApiResponse<boolean>> {
    return requestApi<ApiResponse<boolean>>(`${EXEC_BASE}/${executionId}`, { method: 'DELETE' })
  },

  getProcess(executionId: number): Promise<ApiResponse<WorkflowProcessResponse>> {
    return requestApi<ApiResponse<WorkflowProcessResponse>>(`${EXEC_BASE}/${executionId}/process`)
  },

  getNodeDetail(executionId: number, nodeKey: string): Promise<ApiResponse<NodeExecutionDetailResponse>> {
    return requestApi<ApiResponse<NodeExecutionDetailResponse>>(
      `${EXEC_BASE}/${executionId}/nodes/${encodeURIComponent(nodeKey)}/history`,
    )
  },

  resume(executionId: number, req: WorkflowResumeRequest): Promise<ApiResponse<boolean>> {
    return requestApi<ApiResponse<boolean>>(`${EXEC_BASE}/${executionId}/resume`, {
      method: 'POST',
      body: JSON.stringify(req),
    })
  },

  debugNode(workflowId: number, nodeKey: string, req: NodeDebugRequest): Promise<ApiResponse<NodeDebugResponse>> {
    return requestApi<ApiResponse<NodeDebugResponse>>(
      `${BASE}/${workflowId}/nodes/${encodeURIComponent(nodeKey)}/debug`,
      { method: 'POST', body: JSON.stringify(req) },
    )
  },
}

// ============ 向后兼容的独立导出 ============

export function createWorkflow(req: WorkflowCreateRequest): Promise<ApiResponse<number>> {
  return workflowV2Api.create(req)
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

export function publishWorkflow(id: number, req: WorkflowPublishRequest): Promise<ApiResponse<WorkflowVersionItem>> {
  return workflowV2Api.publish(id, req)
}

export function copyWorkflow(id: number): Promise<ApiResponse<number>> {
  return workflowV2Api.copy(id)
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
  return workflowV2Api.runAsync(id, req)
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
