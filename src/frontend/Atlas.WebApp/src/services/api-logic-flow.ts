import { requestApi } from './api-core'
import type { ApiResponse, PagedRequest } from '@/types/api'

// ─── Function Definitions ────────────────────────────────

export interface FunctionDefinitionCreateRequest {
  name: string
  displayName?: string
  description?: string
  category: number
  parametersJson: string
  returnType: number
  bodyExpression?: string
  sortOrder?: number
}

export interface FunctionDefinitionUpdateRequest extends FunctionDefinitionCreateRequest {
  id: number
  isEnabled: boolean
}

export interface FunctionDefinitionResponse {
  id: number
  name: string
  displayName: string | null
  description: string | null
  category: number
  parametersJson: string
  returnType: number
  bodyExpression: string | null
  isBuiltin: boolean
  isEnabled: boolean
  sortOrder: number
  createdAt: string
  updatedAt: string | null
  createdBy: string | null
  updatedBy: string | null
}

export interface FunctionDefinitionListItem {
  id: number
  name: string
  displayName: string | null
  category: number
  returnType: number
  isBuiltin: boolean
  isEnabled: boolean
  sortOrder: number
}

interface PagedData<T> {
  items: T[]
  total: number
  pageIndex: number
  pageSize: number
}

export const getFunctionDefinitionsPaged = (params: PagedRequest & { keyword?: string; category?: number }) =>
  requestApi<ApiResponse<PagedData<FunctionDefinitionListItem>>>(
    `/function-definitions?pageIndex=${params.pageIndex}&pageSize=${params.pageSize}${params.keyword ? `&keyword=${encodeURIComponent(params.keyword)}` : ''}${params.category != null ? `&category=${params.category}` : ''}`,
  )

export const getFunctionDefinitionsAll = () =>
  requestApi<ApiResponse<FunctionDefinitionListItem[]>>('/function-definitions/all')

export const getFunctionDefinitionById = (id: number) =>
  requestApi<ApiResponse<FunctionDefinitionResponse>>(`/function-definitions/${id}`)

export const createFunctionDefinition = (body: FunctionDefinitionCreateRequest) =>
  requestApi<ApiResponse<number>>('/function-definitions', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })

export const updateFunctionDefinition = (id: number, body: FunctionDefinitionUpdateRequest) =>
  requestApi<ApiResponse<void>>(`/function-definitions/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })

export const deleteFunctionDefinition = (id: number) =>
  requestApi<ApiResponse<void>>(`/function-definitions/${id}`, {
    method: 'DELETE',
  })

// ─── Decision Tables ─────────────────────────────────────

export interface DecisionTableCreateRequest {
  name: string
  displayName?: string
  description?: string
  hitPolicy: number
  inputColumnsJson: string
  outputColumnsJson: string
  rowsJson: string
  sortOrder?: number
}

export interface DecisionTableUpdateRequest extends DecisionTableCreateRequest {
  id: number
  isEnabled: boolean
}

export interface DecisionTableResponse {
  id: number
  name: string
  displayName: string | null
  description: string | null
  hitPolicy: number
  inputColumnsJson: string
  outputColumnsJson: string
  rowsJson: string
  isEnabled: boolean
  sortOrder: number
  createdAt: string
  updatedAt: string | null
}

export interface DecisionTableListItem {
  id: number
  name: string
  displayName: string | null
  hitPolicy: number
  isEnabled: boolean
  sortOrder: number
}

export interface DecisionTableExecuteRequest {
  tableId: number
  input: Record<string, unknown>
}

export interface DecisionTableExecuteResponse {
  isMatched: boolean
  matchedOutputs: Record<string, unknown>[]
}

export const getDecisionTablesPaged = (params: PagedRequest & { keyword?: string }) =>
  requestApi<ApiResponse<PagedData<DecisionTableListItem>>>(
    `/decision-tables?pageIndex=${params.pageIndex}&pageSize=${params.pageSize}${params.keyword ? `&keyword=${encodeURIComponent(params.keyword)}` : ''}`,
  )

export const getDecisionTableById = (id: number) =>
  requestApi<ApiResponse<DecisionTableResponse>>(`/decision-tables/${id}`)

export const createDecisionTable = (body: DecisionTableCreateRequest) =>
  requestApi<ApiResponse<number>>('/decision-tables', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })

export const updateDecisionTable = (id: number, body: DecisionTableUpdateRequest) =>
  requestApi<ApiResponse<void>>(`/decision-tables/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })

export const deleteDecisionTable = (id: number) =>
  requestApi<ApiResponse<void>>(`/decision-tables/${id}`, {
    method: 'DELETE',
  })

export const executeDecisionTable = (body: DecisionTableExecuteRequest) =>
  requestApi<ApiResponse<DecisionTableExecuteResponse>>('/decision-tables/execute', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })

// ─── Rule Chains ─────────────────────────────────────────

export interface RuleChainCreateRequest {
  name: string
  displayName?: string
  description?: string
  stepsJson: string
  defaultOutputExpression?: string
  sortOrder?: number
}

export interface RuleChainUpdateRequest extends RuleChainCreateRequest {
  id: number
  isEnabled: boolean
}

export interface RuleChainResponse {
  id: number
  name: string
  displayName: string | null
  description: string | null
  stepsJson: string
  defaultOutputExpression: string | null
  isEnabled: boolean
  sortOrder: number
  createdAt: string
  updatedAt: string | null
}

export interface RuleChainListItem {
  id: number
  name: string
  displayName: string | null
  isEnabled: boolean
  sortOrder: number
}

export interface RuleChainExecuteRequest {
  chainId: number
  input: Record<string, unknown>
}

export interface RuleChainExecuteResponse {
  isMatched: boolean
  output: unknown
  matchedStepIndex: number | null
}

export const getRuleChainsPaged = (params: PagedRequest & { keyword?: string }) =>
  requestApi<ApiResponse<PagedData<RuleChainListItem>>>(
    `/rule-chains?pageIndex=${params.pageIndex}&pageSize=${params.pageSize}${params.keyword ? `&keyword=${encodeURIComponent(params.keyword)}` : ''}`,
  )

export const getRuleChainById = (id: number) =>
  requestApi<ApiResponse<RuleChainResponse>>(`/rule-chains/${id}`)

export const createRuleChain = (body: RuleChainCreateRequest) =>
  requestApi<ApiResponse<number>>('/rule-chains', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })

export const updateRuleChain = (id: number, body: RuleChainUpdateRequest) =>
  requestApi<ApiResponse<void>>(`/rule-chains/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })

export const deleteRuleChain = (id: number) =>
  requestApi<ApiResponse<void>>(`/rule-chains/${id}`, {
    method: 'DELETE',
  })

export const executeRuleChain = (body: RuleChainExecuteRequest) =>
  requestApi<ApiResponse<RuleChainExecuteResponse>>('/rule-chains/execute', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })

// ─── Node Types ──────────────────────────────────────────

export enum NodeCategory {
  Trigger = 0,
  DataRead = 1,
  DataTransform = 2,
  ControlFlow = 3,
  Transaction = 4,
  SystemIntegration = 5,
}

export interface PortDefinition {
  portKey: string
  displayName: string
  direction: number
  portType: number
  dataType: number
  isRequired: boolean
  maxConnections: number
  description?: string
}

export interface NodeCapability {
  supportsRetry: boolean
  supportsTimeout: boolean
  supportsCompensation: boolean
  supportsParallelExecution: boolean
  supportsBatching: boolean
  supportsConditionalBranching: boolean
  supportsSubFlow: boolean
  supportsBreakpoint: boolean
  maxInputPorts: number
  maxOutputPorts: number
  requiredPermissions?: string[]
}

export interface NodeUiMetadata {
  shape: string
  icon?: string
  color?: string
  backgroundColor?: string
  width?: number
  height?: number
  portPositions?: { portKey: string; side: string; offset: number }[]
}

export interface NodeConfigSchema {
  basic?: { fields?: { fieldKey: string; displayName: string; fieldType: string; required: boolean; defaultValue?: string; placeholder?: string; options?: string[] }[] }
  binding?: { inputBindings?: { portKey: string; expression?: string; staticValue?: string }[]; outputBindings?: { portKey: string; expression?: string; staticValue?: string }[] }
  advanced?: { maxRetries?: number; timeoutSeconds?: number; maxParallelism?: number; enableCache?: boolean }
  error?: { errorStrategy?: string; fallbackNodeKey?: string; enableErrorPort?: boolean }
  debug?: { enableBreakpoint?: boolean; logInput?: boolean; logOutput?: boolean; mockDataJson?: string }
}

export interface NodeRegistryItem {
  typeKey: string
  category: NodeCategory
  displayName: string
  description: string | null
  ports: PortDefinition[]
  capabilities: NodeCapability
  uiMetadata: NodeUiMetadata
}

export interface NodeCategoryInfo {
  category: NodeCategory
  displayName: string
  count: number
}

export interface NodeTypeListItem {
  id: string
  typeKey: string
  category: NodeCategory
  displayName: string
  description: string | null
  version: string
  isBuiltIn: boolean
  isActive: boolean
  createdAt: string
}

export interface NodeTypeDetailResponse extends NodeTypeListItem {
  ports: PortDefinition[]
  configSchema: NodeConfigSchema | null
  capabilities: NodeCapability | null
  uiMetadata: NodeUiMetadata | null
  updatedAt: string | null
}

export const getNodeTypeRegistry = (category?: NodeCategory) =>
  requestApi<ApiResponse<NodeRegistryItem[]>>(
    `/node-types/registry${category != null ? `?category=${category}` : ''}`,
  )

export const getNodeTypeCategories = () =>
  requestApi<ApiResponse<NodeCategoryInfo[]>>('/node-types/categories')

export const getNodeTypesPaged = (params: PagedRequest & { keyword?: string; category?: number; isBuiltIn?: boolean }) =>
  requestApi<ApiResponse<PagedData<NodeTypeListItem>>>(
    `/node-types?pageIndex=${params.pageIndex}&pageSize=${params.pageSize}${params.keyword ? `&keyword=${encodeURIComponent(params.keyword)}` : ''}${params.category != null ? `&category=${params.category}` : ''}${params.isBuiltIn != null ? `&isBuiltIn=${params.isBuiltIn}` : ''}`,
  )

export const getNodeTypeById = (id: string) =>
  requestApi<ApiResponse<NodeTypeDetailResponse>>(`/node-types/${id}`)

export const getNodeTypeByKey = (typeKey: string) =>
  requestApi<ApiResponse<NodeTypeDetailResponse>>(`/node-types/by-key/${typeKey}`)

// ─── Logic Flows ──────────────────────────────────────────
// Types and API functions for logic flow definitions and executions

export enum LogicFlowStatus {
  Draft = 0,
  Published = 1,
  Archived = 2,
  Disabled = 3,
}

export enum FlowExecutionStatus {
  Pending = 0,
  Running = 1,
  Completed = 2,
  Failed = 3,
  Cancelled = 4,
  TimedOut = 5,
  Paused = 6,
  Compensating = 7,
  Compensated = 8,
}

export enum LogicFlowTriggerType {
  Manual = 0,
  Scheduled = 1,
  EventDriven = 2,
  ApiCall = 3,
  DataChange = 4,
}

export interface LogicFlowCreatePayload {
  flow: {
    name: string
    displayName?: string
    description?: string
    version?: string
    triggerType: number
    triggerConfigJson?: string
    inputSchemaJson?: string
    outputSchemaJson?: string
    maxRetries?: number
    timeoutSeconds?: number
  }
  nodes: FlowNodeBindingPayload[]
  edges: FlowEdgePayload[]
}

export interface LogicFlowUpdatePayload {
  flow: {
    name: string
    displayName?: string
    description?: string
    version?: string
    triggerType: number
    triggerConfigJson?: string
    inputSchemaJson?: string
    outputSchemaJson?: string
    maxRetries?: number
    timeoutSeconds?: number
    isEnabled: boolean
  }
  nodes: FlowNodeBindingPayload[]
  edges: FlowEdgePayload[]
}

export interface FlowNodeBindingPayload {
  nodeTypeKey: string
  nodeInstanceKey: string
  displayName?: string
  configJson?: string
  positionX: number
  positionY: number
  sortOrder?: number
}

export interface FlowEdgePayload {
  sourceNodeKey: string
  sourcePortKey: string
  targetNodeKey: string
  targetPortKey: string
  conditionExpression?: string
  priority?: number
  label?: string
  edgeStyle?: string
}

export interface LogicFlowResponse {
  id: string
  name: string
  displayName: string
  description?: string | null
  version: string
  status: LogicFlowStatus
  triggerType: number
  triggerConfigJson: string
  inputSchemaJson: string
  outputSchemaJson: string
  maxRetries: number
  timeoutSeconds: number
  isEnabled: boolean
  snapshotId?: string | null
  createdAt: string
  updatedAt?: string | null
  createdBy: string
  updatedBy: string
}

export interface LogicFlowListItem {
  id: string
  name: string
  displayName: string
  version: string
  status: LogicFlowStatus
  triggerType: number
  isEnabled: boolean
  createdAt: string
}

export interface FlowNodeBindingDto {
  id: string
  flowDefinitionId: string
  nodeTypeKey: string
  nodeInstanceKey: string
  displayName: string
  configJson: string
  positionX: number
  positionY: number
  sortOrder: number
  isEnabled: boolean
}

export interface FlowEdgeDto {
  id: string
  flowDefinitionId: string
  sourceNodeKey: string
  sourcePortKey: string
  targetNodeKey: string
  targetPortKey: string
  conditionExpression?: string | null
  priority: number
  label?: string | null
  edgeStyle?: string | null
}

export interface LogicFlowDetailResponse extends LogicFlowResponse {
  nodes: FlowNodeBindingDto[]
  edges: FlowEdgeDto[]
}

export interface FlowExecutionResponse {
  id: string
  flowDefinitionId: number
  version: string
  status: FlowExecutionStatus
  triggerType: LogicFlowTriggerType
  inputDataJson: string
  outputDataJson: string
  errorMessage?: string | null
  startedAt?: string | null
  completedAt?: string | null
  createdAt: string
  durationMs?: number | null
  currentNodeKey?: string | null
  retryCount: number
  maxRetries: number
  snapshotId?: string | null
  correlationId?: string | null
  createdBy: string
  parentExecutionId?: string | null
}

export interface FlowExecutionListItem {
  id: string
  flowDefinitionId: number
  version: string
  status: FlowExecutionStatus
  triggerType: LogicFlowTriggerType
  startedAt?: string | null
  completedAt?: string | null
  durationMs?: number | null
  createdBy: string
}

export enum LogicFlowNodeRunStatus {
  Pending = 0,
  Running = 1,
  Completed = 2,
  Failed = 3,
  Skipped = 4,
  TimedOut = 5,
  Compensating = 6,
  Compensated = 7,
  WaitingForRetry = 8,
}

export interface NodeRunResponse {
  id: string
  flowExecutionId: number
  nodeKey: string
  nodeTypeKey: string
  status: LogicFlowNodeRunStatus
  inputDataJson: string
  outputDataJson: string
  errorMessage?: string | null
  retryCount: number
  maxRetries: number
  startedAt?: string | null
  completedAt?: string | null
  durationMs?: number | null
  compensationDataJson?: string | null
  isCompensated: boolean
}

export interface FlowExecutionTriggerPayload {
  flowDefinitionId: number
  inputDataJson?: string
  correlationId?: string
}

export const getLogicFlowsPaged = (params: PagedRequest & { status?: LogicFlowStatus }) =>
  requestApi<ApiResponse<PagedData<LogicFlowListItem>>>(
    `/logic-flows?pageIndex=${params.pageIndex}&pageSize=${params.pageSize}${params.keyword ? `&keyword=${encodeURIComponent(params.keyword)}` : ''}${params.status != null ? `&status=${params.status}` : ''}`,
  )

export const getLogicFlowById = (id: string | number) =>
  requestApi<ApiResponse<LogicFlowDetailResponse>>(`/logic-flows/${id}`)

export const createLogicFlow = (body: LogicFlowCreatePayload) =>
  requestApi<ApiResponse<{ id: string }>>('/logic-flows', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })

export const updateLogicFlow = (id: string | number, body: LogicFlowUpdatePayload) =>
  requestApi<ApiResponse<void>>(`/logic-flows/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })

export const publishLogicFlow = (id: string | number) =>
  requestApi<ApiResponse<void>>(`/logic-flows/${id}/publish`, { method: 'POST' })

export const archiveLogicFlow = (id: string | number) =>
  requestApi<ApiResponse<void>>(`/logic-flows/${id}/archive`, { method: 'POST' })

export const deleteLogicFlow = (id: string | number) =>
  requestApi<ApiResponse<void>>(`/logic-flows/${id}`, { method: 'DELETE' })

export const getFlowExecutionsPaged = (
  params: PagedRequest & { flowDefinitionId?: number; status?: FlowExecutionStatus },
) =>
  requestApi<ApiResponse<PagedData<FlowExecutionListItem>>>(
    `/flow-executions?pageIndex=${params.pageIndex}&pageSize=${params.pageSize}${params.flowDefinitionId != null ? `&flowDefinitionId=${params.flowDefinitionId}` : ''}${params.status != null ? `&status=${params.status}` : ''}`,
  )

export const getFlowExecutionById = (id: string | number) =>
  requestApi<ApiResponse<FlowExecutionResponse>>(`/flow-executions/${id}`)

export const getNodeRuns = (executionId: string | number) =>
  requestApi<ApiResponse<NodeRunResponse[]>>(`/flow-executions/${executionId}/node-runs`)

export const triggerFlowExecution = (body: FlowExecutionTriggerPayload) =>
  requestApi<ApiResponse<{ executionId: string }>>('/flow-executions/trigger', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })

export const cancelExecution = (id: string | number) =>
  requestApi<ApiResponse<void>>(`/flow-executions/${id}/cancel`, { method: 'POST' })

export const pauseExecution = (id: string | number) =>
  requestApi<ApiResponse<void>>(`/flow-executions/${id}/pause`, { method: 'POST' })

export const resumeExecution = (id: string | number) =>
  requestApi<ApiResponse<void>>(`/flow-executions/${id}/resume`, { method: 'POST' })

export const retryExecution = (id: string | number) =>
  requestApi<ApiResponse<{ executionId: string }>>(`/flow-executions/${id}/retry`, { method: 'POST' })

// ─── Governance (T10) ─────────────────────────────────────

export interface QuotaInfoDto {
  resourceType: string
  limit: number
  used: number
  remaining: number
}

export interface CanaryReleaseInfoDto {
  featureKey: string
  rolloutPercentage: number
  isActive: boolean
  activatedAt?: string | null
}

export interface VersionFreezeInfoDto {
  resourceType: string
  resourceId: number
  reason: string
  frozenBy: string
  frozenAt: string
}

export const getQuotaInfo = (resourceType?: string) =>
  requestApi<ApiResponse<QuotaInfoDto | QuotaInfoDto[]>>(
    `/governance/quotas${resourceType ? `?resourceType=${encodeURIComponent(resourceType)}` : ''}`,
  )

export const consumeQuota = (resourceType: string, amount: number) =>
  requestApi<ApiResponse<{ success: boolean }>>(`/governance/quotas/${encodeURIComponent(resourceType)}/consume`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ amount }),
  })

export const getCanaryReleases = () => requestApi<ApiResponse<CanaryReleaseInfoDto[]>>('/governance/canary-releases')

export const setCanaryRollout = (featureKey: string, rolloutPercentage: number) =>
  requestApi<ApiResponse<null>>(`/governance/canary-releases/${encodeURIComponent(featureKey)}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ rolloutPercentage }),
  })

export const getVersionFreezes = (resourceType?: string, resourceId?: number) => {
  const p = new URLSearchParams()
  if (resourceType) p.set('resourceType', resourceType)
  if (resourceId != null) p.set('resourceId', String(resourceId))
  const q = p.toString()
  return requestApi<ApiResponse<VersionFreezeInfoDto[]>>(`/governance/version-freezes${q ? `?${q}` : ''}`)
}

export const freezeVersion = (body: { resourceType: string; resourceId: number; reason: string }) =>
  requestApi<ApiResponse<null>>('/governance/version-freezes', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })

export const unfreezeVersion = (resourceType: string, resourceId: number) =>
  requestApi<ApiResponse<null>>(`/governance/version-freezes/${encodeURIComponent(resourceType)}/${resourceId}`, {
    method: 'DELETE',
  })

// ─── Logic-flow SPI plugin registry ───────────────────────

export interface PluginInfoDto {
  pluginType: string
  key: string
  displayName: string
}

export const getPlugins = () => requestApi<ApiResponse<PluginInfoDto[]>>('/logic-flow/plugins')

export const getNodePlugins = () => requestApi<ApiResponse<PluginInfoDto[]>>('/logic-flow/plugins/nodes')

export const getFunctionPlugins = () => requestApi<ApiResponse<PluginInfoDto[]>>('/logic-flow/plugins/functions')

export const getDataSourcePlugins = () => requestApi<ApiResponse<PluginInfoDto[]>>('/logic-flow/plugins/data-sources')

export const getTemplatePlugins = () => requestApi<ApiResponse<PluginInfoDto[]>>('/logic-flow/plugins/templates')
