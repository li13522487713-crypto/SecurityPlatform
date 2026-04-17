import type { PagedRequest } from "./api-base";

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
  confirmPassword: string;
}

export interface UserProfileDetail {
  displayName: string;
  email?: string;
  phoneNumber?: string;
}

export interface UserProfileUpdateRequest {
  displayName: string;
  email?: string;
  phoneNumber?: string;
}

export interface RoleQueryRequest extends PagedRequest {
  isSystem?: boolean;
}

export interface PermissionQueryRequest extends PagedRequest {
  type?: string;
}

export interface MenuQueryRequest extends PagedRequest {
  isHidden?: boolean;
}

export interface FileRecordDto {
  id: number;
  originalName: string;
  contentType: string;
  sizeBytes: number;
  uploadedById: number;
  uploadedByName: string;
  uploadedAt: string;
  versionNumber: number;
  isLatestVersion: boolean;
  previousVersionId?: number | null;
}

export interface FileUploadResult {
  id: number;
  originalName: string;
  contentType: string;
  sizeBytes: number;
  uploadedAt: string;
  versionNumber: number;
  isLatestVersion: boolean;
  previousVersionId?: number | null;
}

export interface FileVersionHistoryItemDto {
  id: number;
  originalName: string;
  contentType: string;
  sizeBytes: number;
  versionNumber: number;
  isLatestVersion: boolean;
  previousVersionId?: number | null;
  uploadedAt: string;
  uploadedById: number;
  uploadedByName: string;
}

export interface AttachmentBindingDto {
  id: number;
  fileRecordId: number;
  entityType: string;
  entityId: number;
  fieldKey?: string | null;
  isPrimary: boolean;
  createdAt: string;
  file?: FileRecordDto | null;
}

export interface AttachmentBindRequest {
  fileRecordId: number;
  entityType: string;
  entityId: number;
  fieldKey?: string | null;
  isPrimary?: boolean;
}

export interface AttachmentUnbindRequest {
  fileRecordId: number;
  entityType: string;
  entityId: number;
  fieldKey?: string | null;
}

export interface FileChunkUploadInitRequest {
  originalName: string;
  contentType: string;
  totalSizeBytes: number;
  totalParts: number;
}

export interface FileChunkUploadInitResult {
  sessionId: number;
  partSizeBytes: number;
  expiresAt: string;
}

export interface FileChunkUploadCompleteRequest {
  originalName: string;
  contentType: string;
}

export interface FileUploadSessionProgressDto {
  sessionId: number;
  originalName: string;
  contentType: string;
  totalSizeBytes: number;
  uploadedSizeBytes: number;
  totalParts: number;
  uploadedPartCount: number;
  completed: boolean;
  expiresAt: string;
  createdAt: string;
  updatedAt?: string;
}

export interface FileSignedUrlResult {
  url: string;
  expiresAt: string;
}

export interface FileInstantCheckResult {
  exists: boolean;
  fileId?: number;
  originalName?: string;
  contentType?: string;
  sizeBytes?: number;
}

export interface FileTusCreateResult {
  sessionId: number;
  location: string;
  uploadOffset: number;
  uploadLength: number;
  expiresAt: string;
}

export interface FileTusStatusResult {
  sessionId: number;
  uploadOffset: number;
  uploadLength: number;
  completed: boolean;
  expiresAt: string;
}

// ==================== 表格视图（个人）====================

export type TableViewDensity = "compact" | "default" | "comfortable";

export interface TableViewColumnConfig {
  key: string;
  dataIndex?: string;
  title?: string;
  visible?: boolean;
  order?: number;
  align?: "left" | "center" | "right";
  resizable?: boolean;
  ellipsis?: boolean;
  wrap?: boolean;
  tooltip?: boolean;
  width?: string | number;
  minWidth?: number;
  maxWidth?: number;
  pinned?: "left" | "right" | false;
  colSpan?: number;
  rowSpan?: number;
  children?: TableViewColumnConfig[];
  conditionalFormats?: {
    operator:
      | "equal"
      | "notEqual"
      | "greaterThan"
      | "lessThan"
      | "between"
      | "contains";
    value: unknown;
    value2?: unknown;
    color?: string;
    backgroundColor?: string;
  }[];
}

export interface TableViewPagination {
  pageSize: number;
}

export interface TableViewSort {
  key: string;
  order: string;
  priority: number;
}

export interface TableViewFilter {
  key: string;
  operator: string;
  value?: string | number | boolean | string[] | number[];
}

export interface TableViewGroupBy {
  key: string;
  collapsedKeys?: string[];
}

export interface TableViewAggregation {
  key: string;
  op: string;
}

export interface TableViewQueryPanel {
  open: boolean;
  autoSearch: boolean;
  savedFilterId?: string;
}

export interface TableViewQueryCondition {
  field: string;
  operator: string;
  value?: string | number | boolean | string[] | number[];
}

export interface TableViewQueryGroup {
  logic: "AND" | "OR";
  conditions?: TableViewQueryCondition[];
  groups?: TableViewQueryGroup[];
}

export interface MergeCellRule {
  columnKey: string;
  dependsOn?: string[];
}

export interface TableViewConfig {
  columns: TableViewColumnConfig[];
  density?: TableViewDensity;
  bordered?: boolean;
  stripe?: boolean;
  scroll?: {
    x?: number | string;
    y?: number | string;
  };
  virtual?: boolean;
  itemSize?: number;
  pagination?: TableViewPagination;
  sort?: TableViewSort[];
  filters?: TableViewFilter[];
  groupBy?: TableViewGroupBy;
  aggregations?: TableViewAggregation[];
  queryPanel?: TableViewQueryPanel;
  queryModel?: TableViewQueryGroup;
  mergeCells?: MergeCellRule[];
}

export interface TableViewListItem {
  id: string;
  name: string;
  tableKey: string;
  configVersion: number;
  isDefault: boolean;
  updatedAt: string;
  lastUsedAt?: string;
}

export interface TableViewDetail {
  id: string;
  name: string;
  tableKey: string;
  configVersion: number;
  isDefault: boolean;
  config: TableViewConfig;
  updatedAt: string;
  lastUsedAt?: string;
}

export interface TableViewCreateRequest {
  tableKey: string;
  name: string;
  config: TableViewConfig;
  configVersion?: number;
}

export interface TableViewUpdateRequest {
  name: string;
  config: TableViewConfig;
  configVersion?: number;
}

export interface TableViewConfigUpdateRequest {
  config: TableViewConfig;
  configVersion?: number;
}

export interface TableViewDuplicateRequest {
  name: string;
}

// ==================== 审批流 ====================

export const ApprovalFlowStatus = {
  Draft: 0,
  Published: 1,
  Disabled: 2,
} as const;
export type ApprovalFlowStatus =
  (typeof ApprovalFlowStatus)[keyof typeof ApprovalFlowStatus];

export const ApprovalInstanceStatus = {
  Destroy: -3,
  Suspended: -2,
  Draft: -1,
  Running: 0,
  Completed: 1,
  Rejected: 2,
  Canceled: 3,
  TimedOut: 4,
  Terminated: 5,
  AutoApproved: 6,
  AutoRejected: 7,
  AiProcessing: 8,
  AiManualReview: 9,
} as const;
export type ApprovalInstanceStatus =
  (typeof ApprovalInstanceStatus)[keyof typeof ApprovalInstanceStatus];

export const ApprovalHistoryEventType = {
  InstanceStarted: "InstanceStarted",
  TaskCreated: "TaskCreated",
  TaskApproved: "TaskApproved",
  TaskRejected: "TaskRejected",
  NodeAdvanced: "NodeAdvanced",
  InstanceCompleted: "InstanceCompleted",
  InstanceRejected: "InstanceRejected",
  InstanceCanceled: "InstanceCanceled",
  TaskTransferred: "TaskTransferred",
  DrawBackAgree: "DrawBackAgree",
  ProcessDrawBack: "ProcessDrawBack",
  BackToAnyNode: "BackToAnyNode",
  BackToModify: "BackToModify",
  AssigneeAdded: "AssigneeAdded",
  AssigneeRemoved: "AssigneeRemoved",
  AssigneeChanged: "AssigneeChanged",
  TaskForwarded: "TaskForwarded",
  TaskUndertaken: "TaskUndertaken",
  ProcessMoveAhead: "ProcessMoveAhead",
  RecoverToHistory: "RecoverToHistory",
  DraftSaved: "DraftSaved",
  TaskDelegated: "TaskDelegated",
  TaskDelegateReturned: "TaskDelegateReturned",
  TaskClaimed: "TaskClaimed",
  TaskJumped: "TaskJumped",
  TaskReclaimed: "TaskReclaimed",
  TaskResumed: "TaskResumed",
  InstanceSuspended: "InstanceSuspended",
  InstanceActivated: "InstanceActivated",
  InstanceTerminated: "InstanceTerminated",
  TaskUrged: "TaskUrged",
  TaskCommunicated: "TaskCommunicated",
} as const;
export type ApprovalHistoryEventType =
  (typeof ApprovalHistoryEventType)[keyof typeof ApprovalHistoryEventType];

export const ApprovalTaskStatus = {
  Pending: 0,
  Approved: 1,
  Rejected: 2,
  Canceled: 3,
  Waiting: 4,
  Delegated: 5,
} as const;
export type ApprovalTaskStatus =
  (typeof ApprovalTaskStatus)[keyof typeof ApprovalTaskStatus];

export const AssigneeType = {
  User: 0,
  Role: 1,
  DepartmentLeader: 2,
  Loop: 3,
  Level: 4,
  DirectLeader: 5,
  StartUser: 6,
  Hrbp: 7,
  Customize: 8,
  BusinessTable: 9,
  OutSideAccess: 10,
} as const;
export type AssigneeType =
  (typeof AssigneeType)[keyof typeof AssigneeType];

export const ApprovalMode = {
  OrSign: 0,
  AndSign: 1,
  SequenceSign: 2,
} as const;
export type ApprovalMode =
  (typeof ApprovalMode)[keyof typeof ApprovalMode];

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

export interface ApprovalFlowValidationResult {
  isValid: boolean;
  errors: string[];
  warnings: string[];
  details?: ApprovalFlowValidationIssue[];
}

export interface ApprovalFlowValidationIssue {
  code: string;
  message: string;
  severity: "error" | "warning";
  nodeId?: string;
  edgeId?: string;
}

export interface ApprovalFlowCopyRequest {
  name?: string;
}

export interface ApprovalFlowImportRequest {
  name: string;
  definitionJson: string;
  description?: string;
  category?: string;
  visibilityScopeJson?: string;
  isQuickEntry?: boolean;
}

export interface ApprovalFlowExportResponse {
  id: string;
  name: string;
  version: number;
  definitionJson: string;
  description?: string;
  category?: string;
  visibilityScopeJson?: string;
  isQuickEntry: boolean;
  exportedAt: string;
}

export interface ApprovalFlowDifferenceItem {
  path: string;
  sourceValue: string;
  targetValue: string;
  changeType: string;
}

export interface ApprovalFlowCompareResponse {
  sourceFlowId: string;
  sourceVersion: number;
  targetVersion: number;
  isSame: boolean;
  summary: string;
  differences: ApprovalFlowDifferenceItem[];
}

export interface ApprovalFlowVersionListItem {
  id: string;
  definitionId: string;
  snapshotVersion: number;
  name: string;
  description?: string;
  category?: string;
  createdBy: number;
  createdAt: string;
}

export interface ApprovalFlowVersionDetail
  extends ApprovalFlowVersionListItem {
  definitionJson: string;
  visibilityScopeJson?: string;
}

export interface ApprovalStartRequest {
  definitionId: number | string;
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
  flowName?: string;
  currentNodeName?: string;
  slaRemainingMinutes?: number;
  expectedCompleteTime?: string;
}

export interface ApprovalTaskDecideRequest {
  taskId: number | string;
  approved: boolean;
  comment?: string;
}

export interface ApprovalInstanceListItem {
  id: number | string;
  definitionId: number | string;
  flowName: string;
  businessKey: string;
  initiatorUserId: number | string;
  status: ApprovalInstanceStatus;
  startedAt: string;
  endedAt?: string;
  currentNodeName?: string;
  slaRemainingMinutes?: number;
}

export interface ApprovalInstanceResponse {
  id: number | string;
  definitionId: number | string;
  businessKey: string;
  initiatorUserId: number | string;
  dataJson?: string;
  status: ApprovalInstanceStatus;
  startedAt: string;
  endedAt?: string;
  flowName?: string;
  currentNodeName?: string;
  slaRemainingMinutes?: number;
  expectedCompleteTime?: string;
}

export interface ApprovalHistoryEventResponse {
  id: number | string;
  eventType: ApprovalHistoryEventType;
  fromNode?: string;
  toNode?: string;
  payloadJson?: string;
  actorUserId?: number | string;
  occurredAt: string;
}

export interface ApprovalOperationRequest {
  operationType: number;
  comment?: string;
  targetNodeId?: string;
  targetAssigneeValue?: string;
  additionalAssigneeValues?: string[];
  idempotencyKey?: string;
}

export interface ApprovalCopyRecordResponse {
  id: number | string;
  instanceId: number | string;
  nodeId: string;
  recipientUserId: number | string;
  isRead: boolean;
  createdAt: string;
  readAt?: string;
}

// ==================== 审计 ====================

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

// ==================== 用户管理 ====================

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
  roleIds?: number[];
  departmentIds?: number[];
  positionIds?: number[];
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

// ==================== 部门管理 ====================

export interface DepartmentListItem {
  id: string;
  name: string;
  code: string;
  parentId?: number;
  sortOrder: number;
}

export interface DepartmentCreateRequest {
  name: string;
  code: string;
  parentId?: number;
  sortOrder: number;
}

export interface DepartmentUpdateRequest {
  name: string;
  code: string;
  parentId?: number;
  sortOrder: number;
}

// ==================== 角色管理 ====================

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
  dataScope: number;
  deptIds: number[];
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

// ==================== 权限管理 ====================

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

// ==================== 菜单管理 ====================

export interface MenuListItem {
  id: string;
  name: string;
  path: string;
  parentId?: number | null;
  sortOrder: number;
  menuType: "M" | "C" | "F" | "L";
  component?: string | null;
  icon?: string | null;
  perms?: string | null;
  query?: string | null;
  isFrame: boolean;
  isCache: boolean;
  visible: "0" | "1";
  status: "0" | "1";
  permissionCode?: string | null;
  isHidden: boolean;
}

export interface MenuCreateRequest {
  name: string;
  path: string;
  parentId?: number | null;
  sortOrder: number;
  menuType: "M" | "C" | "F" | "L";
  component?: string | null;
  icon?: string | null;
  perms?: string | null;
  query?: string | null;
  isFrame: boolean;
  isCache: boolean;
  visible: "0" | "1";
  status: "0" | "1";
  permissionCode?: string | null;
  isHidden: boolean;
}

export interface MenuUpdateRequest {
  name: string;
  path: string;
  parentId?: number | null;
  sortOrder: number;
  menuType: "M" | "C" | "F" | "L";
  component?: string | null;
  icon?: string | null;
  perms?: string | null;
  query?: string | null;
  isFrame: boolean;
  isCache: boolean;
  visible: "0" | "1";
  status: "0" | "1";
  permissionCode?: string | null;
  isHidden: boolean;
}

export interface RouterMeta {
  title: string;
  titleKey?: string;
  icon?: string;
  noCache?: boolean;
  link?: string;
  permi?: string;
}

export interface RouterVo {
  alwaysShow?: boolean;
  hidden?: boolean;
  name: string;
  path: string;
  redirect?: string;
  query?: string;
  component?: string;
  meta?: RouterMeta;
  children?: RouterVo[];
}

// ==================== 注册 ====================

export interface RegisterRequest {
  username: string;
  password: string;
  confirmPassword: string;
  captchaKey?: string;
  captchaCode?: string;
}

// ==================== 应用配置 ====================

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

// ==================== 项目管理 ====================

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

// ==================== 租户数据源 ====================

export interface TenantDataSourceDto {
  id: string;
  tenantIdValue: string;
  name: string;
  dbType: string;
  appId?: string;
  maxPoolSize?: number;
  connectionTimeoutSeconds?: number;
  lastTestSuccess?: boolean;
  lastTestedAt?: string;
  lastTestMessage?: string;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string;
}

export interface TenantDataSourceCreateRequest {
  tenantIdValue: string;
  name: string;
  connectionString: string;
  dbType: string;
  mode?: "raw" | "visual";
  visualConfig?: Record<string, string>;
  maxPoolSize?: number;
  connectionTimeoutSeconds?: number;
}

export interface TenantDataSourceUpdateRequest {
  name: string;
  connectionString?: string;
  dbType: string;
  mode?: "raw" | "visual";
  visualConfig?: Record<string, string>;
  maxPoolSize?: number;
  connectionTimeoutSeconds?: number;
}

export interface TenantDataSourceTestConnectionRequest {
  connectionString: string;
  dbType: string;
  mode?: "raw" | "visual";
  visualConfig?: Record<string, string>;
}

export interface TenantDataSourceTestConnectionResult {
  success: boolean;
  errorMessage?: string;
  latencyMs?: number;
}

export interface DataSourceDriverFieldDefinition {
  key: string;
  label: string;
  inputType: string;
  required: boolean;
  secret: boolean;
  multiline: boolean;
  placeholder?: string;
  defaultValue?: string;
}

export interface DataSourceDriverDefinition {
  code: string;
  displayName: string;
  supportsVisual: boolean;
  connectionStringExample: string;
  fields: DataSourceDriverFieldDefinition[];
}

// ==================== 职位管理 ====================

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

// ==================== WorkflowCore ====================

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
  supported: boolean;
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
  data?: unknown;
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

// ==================== 可视化中心 ====================

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

// ==================== License ====================

export type LicenseEdition = "Trial" | "Pro" | "Enterprise";
export type LicenseStatusCode = "None" | "Active" | "Expired" | "Invalid";

export interface LicenseStatus {
  status: LicenseStatusCode;
  edition: LicenseEdition;
  isPermanent: boolean;
  issuedAt: string | null;
  expiresAt: string | null;
  remainingDays: number | null;
  machineBound: boolean;
  machineMatched: boolean;
  features: Record<string, boolean>;
  limits: Record<string, number>;
  tenantId?: string | null;
  tenantName?: string | null;
}

export interface LicenseActivateRequest {
  licenseContent: string;
}

export interface LicenseActivateResult {
  message: string;
  edition: LicenseEdition;
  isPermanent: boolean;
  expiresAt: string | null;
  remainingDays: number | null;
}

export interface LicenseFingerprintResult {
  fingerprint: string;
}

// ==================== 数据库连接器与预览 ====================

export interface SqlQueryRequest {
  sql: string;
}

export interface SqlQueryResultColumn {
  field: string;
  title: string;
  type: string;
}

export interface SqlQueryResult {
  success: boolean;
  errorMessage?: string;
  columns: SqlQueryResultColumn[];
  data: Record<string, unknown>[];
  executionTimeMs: number;
}

export interface DataSourceColumnInfo {
  name: string;
  dataType: string;
  isNullable: boolean;
  isPrimaryKey: boolean;
}

export interface DataSourceTableInfo {
  name: string;
  columns: DataSourceColumnInfo[];
}

export interface DataSourceSchemaResult {
  success: boolean;
  errorMessage?: string;
  tables: DataSourceTableInfo[];
}

// ==================== 统一元数据关联查询 ====================

export interface FormDefinitionRef {
  id: string;
  name: string;
  description?: string | null;
  category?: string | null;
  status: string;
}

export interface RuntimePageRef {
  id: string;
  pageKey: string;
  name: string;
  appId: string;
}

export interface ApprovalFlowRef {
  id: string;
  name: string;
  status: string;
}

export interface EntityReferenceResult {
  tableKey: string;
  formDefinitions: FormDefinitionRef[];
  runtimePages: RuntimePageRef[];
  boundApprovalFlow?: ApprovalFlowRef | null;
}

// ==================== Agent Team ====================

export type AgentTeamStatus =
  | "Draft"
  | "Ready"
  | "Published"
  | "Disabled"
  | "Archived";
export type AgentTeamPublishStatus =
  | "Unpublished"
  | "PendingApproval"
  | "Published";
export type AgentTeamRiskLevel = "Low" | "Medium" | "High";

export interface AgentTeamListItemDto {
  id: number;
  teamName: string;
  description?: string;
  owner: string;
  status: AgentTeamStatus;
  publishStatus: AgentTeamPublishStatus;
  publishedVersionId?: number;
  riskLevel: AgentTeamRiskLevel;
  version: number;
  updatedAt: string;
}

export interface AgentTeamVersionDto {
  id: number;
  teamId: number;
  versionNo: string;
  publishStatus: AgentTeamPublishStatus;
  publishedBy?: string;
  publishedAt?: string;
  rollbackFromVersionId?: number;
}
