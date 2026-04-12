// Coze 风格工作流引擎 v2 类型定义（与后端 /api/v2/workflows 契约对齐）

// ============ 枚举与常量 ============

export type WorkflowMode = 0 | 1; // 0=Standard, 1=ChatFlow
export type WorkflowLifecycleStatus = 0 | 1 | 2; // 0=Draft, 1=Published, 2=Archived
export type ExecutionStatus = 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7; // Pending, Running, Completed, Failed, Cancelled, Interrupted, Skipped, Blocked
export type InterruptType = 0 | 1 | 2 | 3; // None, QuestionAnswer, ManualApproval, Timeout
export type EdgeExecutionStatus = 0 | 1 | 2 | 3; // Idle, Success, Skipped, Failed

export const WORKFLOW_SCHEMA_VERSION = 2;

export type WorkflowNodeTypeKey =
  | "Entry"
  | "Exit"
  | "Llm"
  | "Plugin"
  | "Agent"
  | "IntentDetector"
  | "QuestionAnswer"
  | "Selector"
  | "SubWorkflow"
  | "TextProcessor"
  | "Loop"
  | "Batch"
  | "Break"
  | "Continue"
  | "InputReceiver"
  | "OutputEmitter"
  | "AssignVariable"
  | "VariableAssignerWithinLoop"
  | "VariableAggregator"
  | "KnowledgeRetriever"
  | "KnowledgeIndexer"
  | "KnowledgeDeleter"
  | "Ltm"
  | "DatabaseQuery"
  | "DatabaseInsert"
  | "DatabaseUpdate"
  | "DatabaseDelete"
  | "DatabaseCustomSql"
  | "CreateConversation"
  | "ConversationList"
  | "ConversationUpdate"
  | "ConversationDelete"
  | "ConversationHistory"
  | "ClearConversationHistory"
  | "MessageList"
  | "CreateMessage"
  | "EditMessage"
  | "DeleteMessage"
  | "HttpRequester"
  | "CodeRunner"
  | "JsonSerialization"
  | "JsonDeserialization"
  | "Comment";

export const WORKFLOW_NODE_TYPE_VALUES: Record<WorkflowNodeTypeKey, number> = {
  Entry: 1,
  Exit: 2,
  Llm: 3,
  Plugin: 4,
  CodeRunner: 5,
  KnowledgeRetriever: 6,
  Selector: 8,
  SubWorkflow: 9,
  OutputEmitter: 13,
  TextProcessor: 15,
  QuestionAnswer: 18,
  Break: 19,
  VariableAssignerWithinLoop: 20,
  Loop: 21,
  IntentDetector: 22,
  KnowledgeIndexer: 27,
  Batch: 28,
  Continue: 29,
  InputReceiver: 30,
  Comment: 31,
  VariableAggregator: 32,
  ConversationList: 53,
  MessageList: 37,
  ClearConversationHistory: 38,
  CreateConversation: 39,
  AssignVariable: 40,
  DatabaseCustomSql: 41,
  ConversationUpdate: 51,
  ConversationDelete: 52,
  ConversationHistory: 54,
  CreateMessage: 55,
  EditMessage: 56,
  DeleteMessage: 57,
  DatabaseUpdate: 42,
  DatabaseQuery: 43,
  DatabaseDelete: 44,
  DatabaseInsert: 46,
  HttpRequester: 45,
  JsonSerialization: 58,
  JsonDeserialization: 59,
  Agent: 60,
  KnowledgeDeleter: 61,
  Ltm: 62
};

const NODE_ALIAS_MAP: Record<string, WorkflowNodeTypeKey> = {
  Start: "Entry",
  start: "Entry",
  End: "Exit",
  end: "Exit",
  LLM: "Llm",
  llm: "Llm",
  If: "Selector",
  if: "Selector",
  Api: "Plugin",
  api: "Plugin",
  Code: "CodeRunner",
  code: "CodeRunner",
  Http: "HttpRequester",
  http: "HttpRequester",
  ToJSON: "JsonSerialization",
  tojson: "JsonSerialization",
  FromJSON: "JsonDeserialization",
  fromjson: "JsonDeserialization",
  ENTRY: "Entry",
  entry: "Entry",
  EXIT: "Exit",
  exit: "Exit"
};

const NODE_VALUE_TO_KEY = Object.entries(WORKFLOW_NODE_TYPE_VALUES).reduce<Record<number, WorkflowNodeTypeKey>>(
  (acc, [key, value]) => {
    acc[value] = key as WorkflowNodeTypeKey;
    return acc;
  },
  {}
);

export function normalizeNodeTypeKey(value: string): WorkflowNodeTypeKey {
  const trimmed = value.trim();
  const numericValue = Number(trimmed);
  if (!Number.isNaN(numericValue) && Number.isFinite(numericValue)) {
    return workflowNodeValueToKey(numericValue);
  }

  if (trimmed in WORKFLOW_NODE_TYPE_VALUES) {
    return trimmed as WorkflowNodeTypeKey;
  }

  const alias = NODE_ALIAS_MAP[trimmed] ?? NODE_ALIAS_MAP[trimmed.toLowerCase()];
  if (alias) {
    return alias;
  }

  const canonicalMatch = (Object.keys(WORKFLOW_NODE_TYPE_VALUES) as WorkflowNodeTypeKey[]).find(
    (key) => key.toLowerCase() === trimmed.toLowerCase()
  );
  if (canonicalMatch) {
    return canonicalMatch;
  }

  return "TextProcessor";
}

export function workflowNodeTypeToValue(type: WorkflowNodeTypeKey): number {
  return WORKFLOW_NODE_TYPE_VALUES[type];
}

export function workflowNodeValueToKey(typeValue: number): WorkflowNodeTypeKey {
  return NODE_VALUE_TO_KEY[typeValue] ?? "TextProcessor";
}

// ============ 画布模型 ============

export interface NodeLayout {
  x: number;
  y: number;
  width: number;
  height: number;
}

export interface WorkflowViewport {
  x: number;
  y: number;
  zoom: number;
}

export type WorkflowPortDirection = "input" | "output";

export interface WorkflowNodePortSchema {
  key: string;
  name: string;
  direction: WorkflowPortDirection;
  dataType?: string;
  isRequired?: boolean;
  maxConnections?: number;
}

// 编辑器内部节点模型（字符串类型键）
export interface NodeSchema {
  key: string;
  type: WorkflowNodeTypeKey;
  title: string;
  layout: NodeLayout;
  configs: Record<string, unknown>;
  inputMappings: Record<string, string>;
  childCanvas?: CanvasSchema;
  inputTypes?: Record<string, string>;
  outputTypes?: Record<string, string>;
  inputSources?: Array<Record<string, unknown>>;
  outputSources?: Array<Record<string, unknown>>;
  ports?: WorkflowNodePortSchema[];
  version?: string;
  debugMeta?: Record<string, unknown>;
}

// 编辑器内部连线模型
export interface ConnectionSchema {
  fromNode: string;
  fromPort: string;
  toNode: string;
  toPort: string;
  condition: string | null;
}

export interface CanvasSchema {
  nodes: NodeSchema[];
  connections: ConnectionSchema[];
  schemaVersion?: number;
  viewport?: WorkflowViewport;
  globals?: Record<string, unknown>;
}

// 后端 Canvas 契约模型
export interface WorkflowCanvasNodePayload {
  key: string;
  type: number;
  label: string;
  config: Record<string, unknown>;
  layout: NodeLayout;
  childCanvas?: WorkflowCanvasPayload;
  inputTypes?: Record<string, string>;
  outputTypes?: Record<string, string>;
  inputSources?: Array<Record<string, unknown>>;
  outputSources?: Array<Record<string, unknown>>;
  ports?: WorkflowNodePortSchema[];
  version?: string;
  debugMeta?: Record<string, unknown>;
}

export interface WorkflowCanvasConnectionPayload {
  sourceNodeKey: string;
  sourcePort: string;
  targetNodeKey: string;
  targetPort: string;
  condition: string | null;
}

export interface WorkflowCanvasPayload {
  nodes: WorkflowCanvasNodePayload[];
  connections: WorkflowCanvasConnectionPayload[];
  schemaVersion?: number;
  viewport?: WorkflowViewport;
  globals?: Record<string, unknown>;
}

// ============ API Request/Response ============

export interface WorkflowCreateRequest {
  name: string;
  description?: string;
  mode: WorkflowMode;
}

export interface WorkflowSaveRequest {
  canvasJson: string;
  commitId?: string;
}

export interface WorkflowPublishRequest {
  changeLog?: string;
}

export interface WorkflowUpdateMetaRequest {
  name: string;
  description?: string;
}

export interface WorkflowListItem {
  id: string;
  name: string;
  description?: string;
  mode: WorkflowMode;
  status: WorkflowLifecycleStatus;
  latestVersionNumber: number;
  creatorId: string;
  createdAt: string;
  updatedAt: string;
  publishedAt?: string;
}

export interface WorkflowDetailResponse extends WorkflowListItem {
  canvasJson: string;
  commitId?: string;
}

export interface WorkflowDetailQuery {
  source?: "published" | "draft";
  versionId?: string;
}

export interface WorkflowVersionItem {
  id: string;
  workflowId: string;
  versionNumber: number;
  changeLog?: string;
  canvasJson: string;
  publishedAt: string;
  publishedByUserId: string;
}

export interface NodeTypeMetadata {
  key: WorkflowNodeTypeKey | string;
  name: string;
  category: string;
  description: string;
  ports?: WorkflowNodePortMetadata[];
  configSchemaJson?: string;
  uiMeta?: WorkflowNodeUiMetadata;
}

export type WorkflowNodePortDirection = "Input" | "Output" | 1 | 2;

export interface WorkflowNodePortMetadata {
  key: string;
  name: string;
  direction: WorkflowNodePortDirection;
  dataType: string;
  isRequired: boolean;
  maxConnections: number;
}

export interface WorkflowNodeUiMetadata {
  icon: string;
  color: string;
  supportsBatch: boolean;
  defaultWidth: number;
  defaultHeight: number;
}

export interface NodeTemplateMetadata {
  key: WorkflowNodeTypeKey | string;
  name: string;
  category: string;
  defaultConfig: Record<string, unknown>;
}

export interface WorkflowValidateRequest {
  canvasJson?: string;
  canvas?: CanvasSchema;
}

export interface WorkflowModelCatalogItem {
  provider: string;
  providerType: string;
  model: string;
  label: string;
  systemPrompt?: string;
  temperature?: number;
  maxTokens?: number;
  enableStreaming: boolean;
}

// ============ 执行相关 ============

export interface WorkflowRunRequest {
  inputs?: Record<string, unknown>;
  inputsJson?: string;
  source?: "published" | "draft";
}

export interface WorkflowRunResponse {
  executionId: string;
}

export interface NodeExecutionItem {
  id: string;
  executionId: string;
  nodeKey: string;
  nodeType: number;
  status: ExecutionStatus;
  inputsJson?: string;
  outputsJson?: string;
  errorMessage?: string;
  startedAt?: string;
  completedAt?: string;
  durationMs?: number;
}

export interface WorkflowProcessResponse {
  id: string;
  workflowId: string;
  versionNumber: number;
  status: ExecutionStatus;
  inputsJson?: string;
  outputsJson?: string;
  errorMessage?: string;
  startedAt: string;
  completedAt?: string;
  nodeExecutions: NodeExecutionItem[];
}

export type NodeExecutionDetailResponse = NodeExecutionItem;

export interface WorkflowExecutionCheckpointResponse {
  executionId: string;
  workflowId: string;
  status: ExecutionStatus;
  lastNodeKey?: string;
  startedAt: string;
  completedAt?: string;
  inputsJson?: string;
  outputsJson?: string;
  errorMessage?: string;
}

export interface WorkflowExecutionDebugViewResponse {
  execution: WorkflowProcessResponse;
  focusNode?: NodeExecutionItem;
  focusReason: string;
}

export interface WorkflowResumeRequest {
  inputsJson?: string;
  data?: Record<string, unknown>;
  variableOverrides?: Record<string, unknown>;
}

export interface NodeDebugRequest {
  nodeKey: string;
  inputs?: Record<string, unknown>;
  inputsJson?: string;
  source?: "published" | "draft";
  versionId?: string;
}

export interface NodeDebugResponse extends WorkflowRunResponse {
  status?: ExecutionStatus;
  outputsJson?: string;
  errorMessage?: string;
  debugNodeKey?: string;
  stepResult?: StepResult;
}

export interface StepResult {
  executionId: string;
  nodeKey: string;
  nodeType: string;
  status: ExecutionStatus;
  startedAt?: string;
  completedAt?: string;
  durationMs?: number;
  inputs?: Record<string, unknown>;
  outputs?: Record<string, unknown>;
  errorMessage?: string;
  branchDecision?: Record<string, unknown>;
}

export interface RunTrace {
  executionId: string;
  workflowId?: string;
  status: ExecutionStatus;
  startedAt?: string;
  completedAt?: string;
  durationMs?: number;
  steps: StepResult[];
  edgeStatuses?: EdgeRuntimeStatus[];
}

export interface EdgeRuntimeStatus {
  sourceNodeKey: string;
  sourcePort: string;
  targetNodeKey: string;
  targetPort: string;
  status: EdgeExecutionStatus;
  reason?: string;
}

export interface WorkflowVersionDiff {
  workflowId: string;
  fromVersionId: string;
  fromVersionNumber: number;
  toVersionId: string;
  toVersionNumber: number;
  addedNodeKeys: string[];
  removedNodeKeys: string[];
  modifiedNodeKeys: string[];
  addedConnectionCount: number;
  removedConnectionCount: number;
  hasChanges: boolean;
}

export interface WorkflowVersionRollbackResult {
  workflowId: string;
  rolledBackToVersionId: string;
  newVersionNumber: number;
}

// ============ SSE 事件 ============

export interface ExecutionStartEvent {
  executionId: string;
}

export interface NodeStartEvent {
  executionId: string;
  nodeKey: string;
  nodeType: string;
}

export interface NodeCompleteEvent {
  executionId: string;
  nodeKey: string;
  nodeType: string;
  durationMs: number;
}

export interface NodeOutputEvent {
  executionId: string;
  nodeKey: string;
  nodeType: string;
  outputs: Record<string, unknown>;
}

export interface NodeFailedEvent {
  executionId: string;
  nodeKey: string;
  nodeType: string;
  durationMs?: number;
  errorMessage: string;
  interruptType?: string;
}

export interface NodeSkippedEvent {
  executionId: string;
  nodeKey: string;
  nodeType: string;
  reason?: string;
}

export interface NodeBlockedEvent {
  executionId: string;
  nodeKey: string;
  nodeType: string;
  reason?: string;
}

export interface ExecutionCompleteEvent {
  executionId: string;
  outputsJson?: string;
}

export interface ExecutionFailedEvent {
  executionId: string;
  errorMessage: string;
}

export interface ExecutionCancelledEvent {
  executionId: string;
  errorMessage?: string;
}

export interface ExecutionInterruptedEvent {
  executionId: string;
  interruptType: string;
  nodeKey?: string;
  outputsJson?: string;
}

export interface EdgeStatusChangedEvent {
  executionId: string;
  edge: EdgeRuntimeStatus;
}

export interface BranchDecisionEvent {
  executionId: string;
  nodeKey: string;
  nodeType: string;
  selectedBranch: string;
  candidates?: string[];
}

export interface ExecutionMetricsEvent {
  executionId: string;
  totalDurationMs?: number;
  completedNodeCount?: number;
  skippedNodeCount?: number;
  failedNodeCount?: number;
}

export type SseEventData =
  | ExecutionStartEvent
  | NodeStartEvent
  | NodeCompleteEvent
  | NodeOutputEvent
  | NodeFailedEvent
  | NodeSkippedEvent
  | NodeBlockedEvent
  | ExecutionCompleteEvent
  | ExecutionFailedEvent
  | ExecutionCancelledEvent
  | ExecutionInterruptedEvent
  | EdgeStatusChangedEvent
  | BranchDecisionEvent
  | ExecutionMetricsEvent
  | string;

// ============ 前端画布节点状态 ============

export type NodeExecutionStateType = "idle" | "running" | "success" | "failed" | "interrupted";

export interface NodeExecutionState {
  nodeKey: string;
  state: NodeExecutionStateType;
  costMs?: number;
  errorMessage?: string;
}

