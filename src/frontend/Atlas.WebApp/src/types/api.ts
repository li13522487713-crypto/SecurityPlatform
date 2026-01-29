export interface ApiResponse<T> {
  success: boolean;
  code: string;
  message: string;
  traceId: string;
  data?: T;
}

export interface PagedRequest {
  pageIndex: number;
  pageSize: number;
  keyword?: string;
  sortBy?: string;
  sortDesc?: boolean;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  pageIndex: number;
  pageSize: number;
}

// 审批流相关类型
export const ApprovalFlowStatus = {
  Draft: 0,
  Published: 1,
  Disabled: 2
} as const;
export type ApprovalFlowStatus = typeof ApprovalFlowStatus[keyof typeof ApprovalFlowStatus];

export const ApprovalInstanceStatus = {
  Running: 0,
  Completed: 1,
  Rejected: 2,
  Canceled: 3
} as const;
export type ApprovalInstanceStatus = typeof ApprovalInstanceStatus[keyof typeof ApprovalInstanceStatus];

export const ApprovalTaskStatus = {
  Pending: 0,
  Approved: 1,
  Rejected: 2,
  Canceled: 3
} as const;
export type ApprovalTaskStatus = typeof ApprovalTaskStatus[keyof typeof ApprovalTaskStatus];

export const AssigneeType = {
  User: 0,
  Role: 1,
  DepartmentLeader: 2
} as const;
export type AssigneeType = typeof AssigneeType[keyof typeof AssigneeType];

export interface ApprovalFlowDefinitionListItem {
  id: string;
  name: string;
  version: number;
  status: ApprovalFlowStatus;
  publishedAt?: string;
}

export interface ApprovalFlowDefinitionResponse {
  id: string;
  name: string;
  definitionJson: string;
  version: number;
  status: ApprovalFlowStatus;
  publishedAt?: string;
  publishedByUserId?: string;
}

export interface ApprovalFlowDefinitionCreateRequest {
  name: string;
  definitionJson: string;
}

export interface ApprovalFlowDefinitionUpdateRequest {
  name: string;
  definitionJson: string;
}

export interface ApprovalFlowPublishRequest {
  remark?: string;
}

export interface ApprovalStartRequest {
  definitionId: string;
  businessKey: string;
  dataJson?: string;
}

export interface ApprovalTaskResponse {
  id: string;
  instanceId: string;
  nodeId: string;
  title: string;
  assigneeType: AssigneeType;
  assigneeValue: string;
  status: ApprovalTaskStatus;
  decisionByUserId?: string;
  decisionAt?: string;
  comment?: string;
  createdAt: string;
}

export interface ApprovalTaskDecideRequest {
  taskId: string;
  approved: boolean;
  comment?: string;
}

export interface ApprovalInstanceListItem {
  id: string;
  definitionId: string;
  flowName: string;
  businessKey: string;
  initiatorUserId: string;
  status: ApprovalInstanceStatus;
  startedAt: string;
  endedAt?: string;
}

export interface ApprovalInstanceResponse {
  id: string;
  definitionId: string;
  businessKey: string;
  initiatorUserId: string;
  dataJson?: string;
  status: ApprovalInstanceStatus;
  startedAt: string;
  endedAt?: string;
}

export interface ApprovalHistoryEventResponse {
  id: string;
  eventType: string;
  fromNode?: string;
  toNode?: string;
  payloadJson?: string;
  actorUserId?: string;
  occurredAt: string;
}

// WorkflowCore 相关类型
export interface StepParameter {
  name: string;
  type: string;
  required: boolean;
  defaultValue?: string;
  description: string;
}

export interface StepTypeMetadata {
  type: string;
  label: string;
  category: string;
  color: string;
  icon: string;
  parameters: StepParameter[];
}

export interface RegisterWorkflowDefinitionRequest {
  workflowId: string;
  version: number;
  definitionJson: string;
  dataType?: string;
}

export interface ExecutionPointerResponse {
  id: string;
  stepId: number;
  stepName: string;
  active: boolean;
  startTime?: string;
  endTime?: string;
  status: string;
  retryCount: number;
  errorMessage?: string;
  sleepUntil?: string;
  eventName?: string;
  eventKey?: string;
}

export interface WorkflowInstanceResponse {
  id: string;
  workflowDefinitionId: string;
  version: number;
  status: string;
  data?: any;
  createTime: string;
  completeTime?: string;
  reference?: string;
}

export interface WorkflowInstanceListItem {
  id: string;
  workflowDefinitionId: string;
  version: number;
  status: string;
  createTime: string;
  completeTime?: string;
}