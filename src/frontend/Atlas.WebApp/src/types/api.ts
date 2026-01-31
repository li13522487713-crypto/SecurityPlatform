export interface ApiResponse<T> {
  success: boolean;
  code: string;
  message: string;
  traceId: string;
  data?: T;
}

export type ClientType = "WebH5" | "Mobile" | "Backend";
export type ClientPlatform = "Web" | "Android" | "iOS";
export type ClientChannel = "Browser" | "App";
export type ClientAgent = "Chrome" | "Edge" | "Safari" | "Firefox" | "Other";

export interface ClientContext {
  clientType: ClientType;
  clientPlatform: ClientPlatform;
  clientChannel: ClientChannel;
  clientAgent: ClientAgent;
}

export interface AuthProfile {
  id: string;
  username: string;
  displayName: string;
  tenantId: string;
  roles: string[];
  permissions: string[];
  clientContext?: ClientContext;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
  confirmPassword: string;
}

export interface AuthTokenResult {
  accessToken: string;
  expiresAt: string;
  refreshToken: string;
  refreshExpiresAt: string;
  sessionId: number;
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
  category?: string;
  description?: string;
  isQuickEntry?: boolean;
}

export interface ApprovalFlowDefinitionResponse {
  id: string;
  name: string;
  definitionJson: string;
  version: number;
  status: ApprovalFlowStatus;
  publishedAt?: string;
  publishedByUserId?: string;
  category?: string;
  description?: string;
  visibilityScopeJson?: string;
  isQuickEntry?: boolean;
}

export interface ApprovalFlowDefinitionCreateRequest {
  name: string;
  definitionJson: string;
  description?: string;
  category?: string;
  visibilityScopeJson?: string;
  isQuickEntry?: boolean;
}

export interface ApprovalFlowDefinitionUpdateRequest {
  name: string;
  definitionJson: string;
  description?: string;
  category?: string;
  visibilityScopeJson?: string;
  isQuickEntry?: boolean;
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

export interface AuditListItem {
  id: string;
  actor: string;
  action: string;
  result: string;
  target: string;
  ipAddress?: string;
  userAgent?: string;
  clientType?: string;
  clientPlatform?: string;
  clientChannel?: string;
  clientAgent?: string;
  occurredAt: string;
}

export interface UserListItem {
  id: string;
  username: string;
  displayName: string;
  isActive: boolean;
  email?: string;
  phoneNumber?: string;
  lastLoginAt?: string;
}

export interface UserDetail {
  id: string;
  username: string;
  displayName: string;
  email?: string;
  phoneNumber?: string;
  isActive: boolean;
  isSystem: boolean;
  lastLoginAt?: string;
  roleIds: number[];
  departmentIds: number[];
  positionIds: number[];
}

export interface UserCreateRequest {
  username: string;
  password: string;
  displayName: string;
  email?: string;
  phoneNumber?: string;
  isActive: boolean;
  roleIds: number[];
  departmentIds: number[];
  positionIds: number[];
}

export interface UserUpdateRequest {
  displayName: string;
  email?: string;
  phoneNumber?: string;
  isActive: boolean;
}

export interface UserAssignRolesRequest {
  roleIds: number[];
}

export interface UserAssignDepartmentsRequest {
  departmentIds: number[];
}

export interface UserAssignPositionsRequest {
  positionIds: number[];
}

export interface DepartmentListItem {
  id: string;
  name: string;
  parentId?: number;
  sortOrder: number;
}

export interface DepartmentCreateRequest {
  name: string;
  parentId?: number;
  sortOrder: number;
}

export interface DepartmentUpdateRequest {
  name: string;
  parentId?: number;
  sortOrder: number;
}

export interface RoleListItem {
  id: string;
  name: string;
  code: string;
  description?: string;
  isSystem: boolean;
}

export interface RoleDetail {
  id: string;
  name: string;
  code: string;
  description?: string;
  isSystem: boolean;
  permissionIds: number[];
  menuIds: number[];
}

export interface RoleCreateRequest {
  name: string;
  code: string;
  description?: string;
}

export interface RoleUpdateRequest {
  name: string;
  description?: string;
}

export interface RoleAssignPermissionsRequest {
  permissionIds: number[];
}

export interface RoleAssignMenusRequest {
  menuIds: number[];
}

export interface PermissionListItem {
  id: string;
  name: string;
  code: string;
  type: string;
  description?: string;
}

export interface PermissionCreateRequest {
  name: string;
  code: string;
  type: string;
  description?: string;
}

export interface PermissionUpdateRequest {
  name: string;
  type: string;
  description?: string;
}

export interface MenuListItem {
  id: string;
  name: string;
  path: string;
  parentId?: number | null;
  sortOrder: number;
  component?: string | null;
  icon?: string | null;
  permissionCode?: string | null;
  isHidden: boolean;
}

export interface MenuCreateRequest {
  name: string;
  path: string;
  parentId?: number | null;
  sortOrder: number;
  component?: string | null;
  icon?: string | null;
  permissionCode?: string | null;
  isHidden: boolean;
}

export interface MenuUpdateRequest {
  name: string;
  path: string;
  parentId?: number | null;
  sortOrder: number;
  component?: string | null;
  icon?: string | null;
  permissionCode?: string | null;
  isHidden: boolean;
}

export interface AppConfigListItem {
  id: string;
  appId: string;
  name: string;
  isActive: boolean;
  enableProjectScope: boolean;
  description?: string;
  sortOrder: number;
}

export interface AppConfigDetail extends AppConfigListItem {}

export interface AppConfigUpdateRequest {
  name: string;
  isActive: boolean;
  enableProjectScope: boolean;
  description?: string;
  sortOrder: number;
}

export interface ProjectListItem {
  id: string;
  code: string;
  name: string;
  isActive: boolean;
  description?: string;
  sortOrder: number;
}

export interface ProjectDetail extends ProjectListItem {
  userIds: number[];
  departmentIds: number[];
  positionIds: number[];
}

export interface ProjectCreateRequest {
  code: string;
  name: string;
  isActive: boolean;
  description?: string;
  sortOrder: number;
}

export interface ProjectUpdateRequest {
  name: string;
  isActive: boolean;
  description?: string;
  sortOrder: number;
}

export interface ProjectAssignUsersRequest {
  userIds: number[];
}

export interface ProjectAssignDepartmentsRequest {
  departmentIds: number[];
}

export interface ProjectAssignPositionsRequest {
  positionIds: number[];
}

export interface PositionListItem {
  id: string;
  name: string;
  code: string;
  description?: string;
  isActive: boolean;
  isSystem: boolean;
  sortOrder: number;
}

export interface PositionDetail {
  id: string;
  name: string;
  code: string;
  description?: string;
  isActive: boolean;
  isSystem: boolean;
  sortOrder: number;
}

export interface PositionCreateRequest {
  name: string;
  code: string;
  description?: string;
  isActive: boolean;
  sortOrder: number;
}

export interface PositionUpdateRequest {
  name: string;
  description?: string;
  isActive: boolean;
  sortOrder: number;
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

// 可视化中心
export interface VisualizationOverview {
  totalProcesses: number;
  runningInstances: number;
  blockedNodes: number;
  alertsToday: number;
  riskHints: string[];
}

export interface VisualizationProcessSummary {
  id: string;
  name: string;
  version: number;
  status: string;
  publishedAt?: string;
}

export interface VisualizationProcessDetail {
  id: string;
  name: string;
  version: number;
  status: string;
  publishedAt?: string;
  definitionJson: string;
}

export interface VisualizationInstanceSummary {
  id: string;
  flowName: string;
  status: string;
  currentNode: string;
  startedAt: string;
  durationMinutes: number;
}

export interface NodeTrace {
  nodeId: string;
  name: string;
  status: string;
  durationMinutes: number;
  startedAt: string;
  endedAt?: string;
}

export interface VisualizationInstanceDetail {
  id: string;
  flowName: string;
  status: string;
  currentNode: string;
  startedAt: string;
  finishedAt?: string;
  trace: NodeTrace[];
  riskHints: string[];
}

export interface ValidateVisualizationRequest {
  definitionJson: string;
}

export interface PublishVisualizationRequest {
  processId: string;
  version: number;
  note?: string;
}

export interface SaveVisualizationProcessRequest {
  processId?: string;
  name: string;
  definitionJson: string;
}

export interface VisualizationValidationResult {
  passed: boolean;
  errors: string[];
}

export interface VisualizationPublishResult {
  processId: string;
  version: number;
  status: string;
}

export interface SaveVisualizationProcessResult {
  processId: string;
  version: number;
  status: string;
}

export interface VisualizationMetricsResponse {
  totalProcesses: number;
  draftProcesses: number;
  runningInstances: number;
  completedInstances: number;
  pendingTasks: number;
  overdueTasks: number;
  assetsTotal: number;
  alertsToday: number;
  auditEventsToday: number;
}
