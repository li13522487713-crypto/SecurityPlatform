// Coze 风格工作流引擎 v2 类型定义
// 与后端 Atlas.Application.Workflow.Models.V2 保持同步

// ============ 枚举 ============

export type WorkflowMode = 0 | 3 // Standard = 0, ChatFlow = 3

export type WorkflowLifecycleStatus = 0 | 1 | 2 // Draft=0, Published=1, Disabled=2

export type NodeType =
  | 'Entry'
  | 'Exit'
  | 'LLM'
  | 'If'
  | 'Loop'
  | 'Break'
  | 'Continue'
  | 'Batch'
  | 'SubWorkflow'
  | 'IntentDetector'
  | 'KnowledgeRetriever'
  | 'KnowledgeIndexer'
  | 'KnowledgeDeleter'
  | 'CodeRunner'
  | 'HttpRequester'
  | 'PluginApi'
  | 'DatabaseQuery'
  | 'DatabaseInsert'
  | 'DatabaseUpdate'
  | 'DatabaseDelete'
  | 'AssignVariable'
  | 'VariableAggregator'
  | 'JsonSerialization'
  | 'JsonDeserialization'
  | 'TextProcessor'
  | 'MessageList'
  | 'CreateMessage'
  | 'ConversationList'
  | 'QuestionAnswer'
  | 'OutputEmitter'

export type ExecutionStatus = 0 | 1 | 2 | 3 | 4 | 5
// Pending=0, Running=1, Success=2, Failed=3, Cancelled=4, Interrupted=5

export type InterruptType = 0 | 1 | 2 // None=0, Question=1, InputReceiver=2

// ============ Canvas Schema ============

export interface NodeLayout {
  x: number
  y: number
  width?: number
  height?: number
}

export interface NodeSchema {
  key: string
  type: NodeType
  title: string
  layout?: NodeLayout
  configs: Record<string, unknown>
  inputMappings: Record<string, string>
}

export interface ConnectionSchema {
  fromNode: string
  fromPort?: string
  toNode: string
  toPort?: string
}

export interface CanvasSchema {
  nodes: NodeSchema[]
  connections: ConnectionSchema[]
}

// ============ API Request/Response ============

export interface WorkflowCreateRequest {
  name: string
  description: string
  mode: WorkflowMode
}

export interface WorkflowSaveRequest {
  canvasJson: string
}

export interface WorkflowPublishRequest {
  changeLog: string
}

export interface WorkflowUpdateMetaRequest {
  name: string
  description: string
}

export interface WorkflowListItem {
  id: number
  name: string
  description: string
  mode: WorkflowMode
  status: WorkflowLifecycleStatus
  latestVersion: string | null
  createdAt: string
  updatedAt: string
}

export interface WorkflowDetailResponse {
  id: number
  name: string
  description: string
  mode: WorkflowMode
  status: WorkflowLifecycleStatus
  latestVersion: string | null
  createdAt: string
  updatedAt: string
  canvasJson: string | null
  commitId: string | null
}

export interface WorkflowVersionItem {
  id: number
  version: string
  commitId: string
  changeLog: string
  publishedAt: string
  nodeCount?: number
}

export interface NodeTypeMetadata {
  type: number
  name: string
  description: string
  category: string
  icon: string
}

// ============ 执行相关 ============

export interface WorkflowRunRequest {
  inputs: Record<string, unknown>
  version?: string
  mode?: number
}

export interface WorkflowRunResponse {
  executionId: number
  status: ExecutionStatus
  outputs: Record<string, unknown>
  errorMessage?: string
  costMs: number
}

export interface NodeExecutionItem {
  id: number
  nodeKey: string
  nodeType: number
  nodeTitle: string
  status: ExecutionStatus
  costMs: number
  tokensUsed?: number
  iterationIndex: number
  startedAt: string
  completedAt?: string
}

export interface WorkflowProcessResponse {
  executionId: number
  status: ExecutionStatus
  costMs: number
  errorMessage?: string
  nodes: NodeExecutionItem[]
  nodeExecutions: NodeExecutionItem[]  // alias for nodes (backend may return either)
}

export interface NodeExecutionDetailResponse {
  id: number
  nodeKey: string
  nodeType: number
  nodeTitle: string
  status: ExecutionStatus
  inputJson?: string
  outputJson?: string
  errorMessage?: string
  costMs: number
  tokensUsed?: number
  startedAt: string
  completedAt?: string
}

export interface WorkflowResumeRequest {
  data: Record<string, unknown>
}

export interface NodeDebugRequest {
  nodeKey: string
  inputs: Record<string, unknown>
}

export interface NodeDebugResponse {
  nodeKey: string
  status: ExecutionStatus
  inputs: Record<string, unknown>
  outputs: Record<string, unknown>
  errorMessage?: string
  costMs: number
  tokensUsed?: number
}

// ============ SSE 事件 ============

export interface NodeStartEvent {
  executionId: number
  nodeKey: string
  nodeType: string
  nodeTitle: string
}

export interface NodeCompleteEvent {
  executionId: number
  nodeKey: string
  status: string
  costMs: number
  output: Record<string, unknown>
  tokensUsed?: number
}

export interface NodeErrorEvent {
  executionId: number
  nodeKey: string
  errorMessage: string
  costMs: number
}

export interface WorkflowDoneEvent {
  executionId: number
  status: string
  totalCostMs: number
  outputs: Record<string, unknown>
}

export interface WorkflowInterruptEvent {
  executionId: number
  interruptType: string
  nodeKey: string
  promptText: string
}

export type SseEventData =
  | NodeStartEvent
  | NodeCompleteEvent
  | NodeErrorEvent
  | WorkflowDoneEvent
  | WorkflowInterruptEvent

// ============ 前端画布节点状态 ============

export type NodeExecutionStateType = 'idle' | 'running' | 'success' | 'failed' | 'interrupted'

export interface NodeExecutionState {
  nodeKey: string
  state: NodeExecutionStateType
  costMs?: number
  tokensUsed?: number
  errorMessage?: string
}
