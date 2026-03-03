import type {
  ApiResponse,
  AuthTokenResult,
  AuthProfile,
  ChangePasswordRequest,
  PagedRequest,
  PagedResult,
  ApprovalFlowDefinitionListItem,
  ApprovalFlowDefinitionResponse,
  ApprovalFlowDefinitionCreateRequest,
  ApprovalFlowDefinitionUpdateRequest,
  ApprovalFlowPublishRequest,
  ApprovalFlowValidationResult,
  ApprovalStartRequest,
  ApprovalTaskResponse,
  ApprovalTaskDecideRequest,
  ApprovalInstanceListItem,
  ApprovalInstanceResponse,
  ApprovalHistoryEventResponse,
  StepTypeMetadata,
  RegisterWorkflowDefinitionRequest,
  ExecutionPointerResponse,
  WorkflowInstanceResponse,
  WorkflowInstanceListItem,
  VisualizationOverview,
  VisualizationProcessSummary,
  VisualizationProcessDetail,
  VisualizationInstanceSummary,
  VisualizationInstanceDetail,
  PublishVisualizationRequest,
  ValidateVisualizationRequest,
  VisualizationValidationResult,
  VisualizationPublishResult,
  SaveVisualizationProcessRequest,
  SaveVisualizationProcessResult,
  VisualizationMetricsResponse,
  AuditListItem,
  UserListItem,
  UserDetail,
  UserCreateRequest,
  UserUpdateRequest,
  UserAssignRolesRequest,
  UserAssignDepartmentsRequest,
  UserAssignPositionsRequest,
  DepartmentListItem,
  DepartmentCreateRequest,
  DepartmentUpdateRequest,
  RoleListItem,
  RoleDetail,
  RoleCreateRequest,
  RoleUpdateRequest,
  RoleAssignPermissionsRequest,
  RoleAssignMenusRequest,
  RoleQueryRequest,
  PermissionListItem,
  PermissionCreateRequest,
  PermissionUpdateRequest,
  PermissionQueryRequest,
  MenuListItem,
  MenuCreateRequest,
  MenuUpdateRequest,
  MenuQueryRequest,
  RouterVo,
  RegisterRequest,
  PositionListItem,
  PositionDetail,
  PositionCreateRequest,
  PositionUpdateRequest,
  AppConfigListItem,
  AppConfigDetail,
  AppConfigUpdateRequest,
  ProjectListItem,
  ProjectDetail,
  ProjectCreateRequest,
  ProjectUpdateRequest,
  ProjectAssignUsersRequest,
  ProjectAssignDepartmentsRequest,
  ProjectAssignPositionsRequest,
  TenantDataSourceDto,
  TenantDataSourceCreateRequest,
  TenantDataSourceUpdateRequest,
  TenantDataSourceTestConnectionRequest,
  TenantDataSourceTestConnectionResult,
  TableViewConfig,
  TableViewListItem,
  TableViewDetail,
  TableViewCreateRequest,
  TableViewUpdateRequest,
  TableViewConfigUpdateRequest,
  TableViewDuplicateRequest,
  FileUploadResult,
  FileRecordDto
} from "@/types/api";
import type {
  FlowDefinition,
  FlowSaveRequest,
  FlowSaveResponse,
  FlowLoadResponse,
  FlowPublishResponse,
  FlowValidationResult
} from "@/types/workflow";
// Core API infrastructure is in api-core.ts
// Domain API functions are below
import { requestApi, toQuery, persistTokenResult, type RequestOptions } from "@/services/api-core";
import { getRefreshToken } from "@/utils/auth";
export type { RequestOptions } from "@/services/api-core";
export { requestApi } from "@/services/api-core";

export interface AssetListItem {
  id: string;
  name: string;
}

export interface AlertListItem {
  id: string;
  title: string;
  createdAt: string;
}

export interface CaptchaResult {
  captchaKey: string;
  captchaImage: string;
}

export async function getCaptcha(tenantId: string): Promise<CaptchaResult> {
  const response = await requestApi<ApiResponse<CaptchaResult>>("/auth/captcha", {
    headers: { "X-Tenant-Id": tenantId }
  }, { disableAutoRefresh: true });
  if (!response.data) throw new Error("获取验证码失败");
  return response.data;
}

export async function createToken(
  tenantId: string,
  username: string,
  password: string,
  requestOptions?: RequestOptions,
  extra?: {
    totpCode?: string;
    captchaKey?: string;
    captchaCode?: string;
    rememberMe?: boolean;
  }
) {
  const response = await requestApi<ApiResponse<AuthTokenResult>>("/auth/token", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-Tenant-Id": tenantId
    },
    body: JSON.stringify({
      username,
      password,
      totpCode: extra?.totpCode,
      captchaKey: extra?.captchaKey,
      captchaCode: extra?.captchaCode,
      rememberMe: extra?.rememberMe ?? false
    })
  }, { ...requestOptions, disableAutoRefresh: true });

  if (!response.data) {
    throw new Error(response.message || "登录失败");
  }

  return response.data;
}

export async function refreshToken(): Promise<AuthTokenResult> {
  const refreshTokenValue = getRefreshToken();
  if (!refreshTokenValue) {
    throw new Error("缺少刷新令牌");
  }

  const response = await requestApi<ApiResponse<AuthTokenResult>>("/auth/refresh", {
    method: "POST",
    headers: {
      "Content-Type": "application/json"
    },
    body: JSON.stringify({ refreshToken: refreshTokenValue })
  }, { disableAutoRefresh: true });
  if (!response.data) {
    throw new Error(response.message || "刷新失败");
  }

  persistTokenResult(response.data);
  return response.data;
}

export async function getCurrentUser(): Promise<AuthProfile> {
  const response = await requestApi<ApiResponse<AuthProfile>>("/auth/me");
  if (!response.data) {
    throw new Error(response.message || "获取用户信息失败");
  }

  return response.data;
}

export async function getRouters(): Promise<RouterVo[]> {
  const response = await requestApi<ApiResponse<RouterVo[]>>("/auth/routers");
  if (!response.data) {
    throw new Error(response.message || "获取路由失败");
  }
  return response.data;
}

export async function register(
  tenantId: string,
  request: RegisterRequest,
  requestOptions?: RequestOptions
): Promise<{ id: string }> {
  const response = await requestApi<ApiResponse<{ id: string }>>("/auth/register", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      "X-Tenant-Id": tenantId
    },
    body: JSON.stringify(request)
  }, { ...requestOptions, disableAutoRefresh: true });
  if (!response.data) {
    throw new Error(response.message || "注册失败");
  }
  return response.data;
}

export async function logout(): Promise<void> {
  const response = await requestApi<ApiResponse<{ success: boolean }>>("/auth/logout", {
    method: "POST"
  });
  if (!response.success) {
    throw new Error(response.message || "退出失败");
  }
}

export async function changePassword(request: ChangePasswordRequest): Promise<void> {
  const response = await requestApi<ApiResponse<{ success: boolean }>>("/auth/password", {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "修改密码失败");
  }
}

export async function uploadFile(file: File): Promise<FileUploadResult> {
  const formData = new FormData();
  formData.append("file", file);
  const response = await requestApi<ApiResponse<FileUploadResult>>("/files", {
    method: "POST",
    body: formData
  });
  if (!response.data) {
    throw new Error(response.message || "上传失败");
  }
  return response.data;
}

export async function getFileInfo(id: string | number): Promise<FileRecordDto> {
  const response = await requestApi<ApiResponse<FileRecordDto>>(`/files/${id}/info`);
  if (!response.data) {
    throw new Error(response.message || "文件不存在");
  }
  return response.data;
}

export async function deleteFile(id: string | number): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/files/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除失败");
  }
}

export async function getAssetsPaged(pagedRequest: PagedRequest) {
  const query = toQuery(pagedRequest);
  const response = await requestApi<ApiResponse<PagedResult<AssetListItem>>>(`/assets?${query}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }

  return response.data;
}

export async function getAuditsPaged(pagedRequest: PagedRequest) {
  const query = toQuery(pagedRequest);
  const response = await requestApi<ApiResponse<PagedResult<AuditListItem>>>(`/audit?${query}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }

  return response.data;
}

export async function getUsersPaged(pagedRequest: PagedRequest) {
  const query = toQuery(pagedRequest);
  const response = await requestApi<ApiResponse<PagedResult<UserListItem>>>(`/users?${query}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }

  return response.data;
}

export async function getUserDetail(id: string) {
  const response = await requestApi<ApiResponse<UserDetail>>(`/users/${id}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }

  return response.data;
}

export async function createUser(request: UserCreateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>("/users", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "创建失败");
  }
}

export async function updateUser(id: string, request: UserUpdateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/users/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新失败");
  }
}

export async function updateUserRoles(id: string, request: UserAssignRolesRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/users/${id}/roles`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新角色失败");
  }
}

export async function updateUserDepartments(id: string, request: UserAssignDepartmentsRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/users/${id}/departments`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新部门失败");
  }
}

export async function updateUserPositions(id: string, request: UserAssignPositionsRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/users/${id}/positions`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新职位失败");
  }
}

export async function deleteUser(id: string) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/users/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除失败");
  }
}

export async function getDepartmentsPaged(pagedRequest: PagedRequest) {
  const query = toQuery(pagedRequest);
  const response = await requestApi<ApiResponse<PagedResult<DepartmentListItem>>>(`/departments?${query}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }

  return response.data;
}

export async function getDepartmentsAll() {
  const response = await requestApi<ApiResponse<DepartmentListItem[]>>("/departments/all");
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }

  return response.data;
}

export async function createDepartment(request: DepartmentCreateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>("/departments", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "创建失败");
  }
}

export async function updateDepartment(id: string, request: DepartmentUpdateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/departments/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新失败");
  }
}

export async function deleteDepartment(id: string) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/departments/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除失败");
  }
}

export async function getRolesPaged(pagedRequest: RoleQueryRequest) {
  const query = toQuery(pagedRequest, {
    isSystem: typeof pagedRequest.isSystem === "boolean" ? String(pagedRequest.isSystem) : undefined
  });
  const response = await requestApi<ApiResponse<PagedResult<RoleListItem>>>(`/roles?${query}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }

  return response.data;
}

export async function getRoleDetail(id: string) {
  const response = await requestApi<ApiResponse<RoleDetail>>(`/roles/${id}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }

  return response.data;
}

export async function createRole(request: RoleCreateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>("/roles", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "创建失败");
  }
}

export async function updateRole(id: string, request: RoleUpdateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/roles/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新失败");
  }
}

export async function deleteRole(id: string) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/roles/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除失败");
  }
}

export async function updateRolePermissions(id: string, request: RoleAssignPermissionsRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/roles/${id}/permissions`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新权限失败");
  }
}

export async function updateRoleMenus(id: string, request: RoleAssignMenusRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/roles/${id}/menus`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新菜单失败");
  }
}

export async function setRoleDataScope(id: string, dataScope: number) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/roles/${id}/data-scope`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ dataScope })
  });
  if (!response.success) {
    throw new Error(response.message || "更新数据权限失败");
  }
}

export async function getPermissionsPaged(pagedRequest: PermissionQueryRequest) {
  const query = toQuery(pagedRequest, {
    type: pagedRequest.type
  });
  const response = await requestApi<ApiResponse<PagedResult<PermissionListItem>>>(`/permissions?${query}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }

  return response.data;
}

export async function createPermission(request: PermissionCreateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>("/permissions", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "创建失败");
  }
}

export async function updatePermission(id: string, request: PermissionUpdateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/permissions/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新失败");
  }
}

export async function getMenusPaged(pagedRequest: MenuQueryRequest) {
  const query = toQuery(pagedRequest, {
    isHidden: typeof pagedRequest.isHidden === "boolean" ? String(pagedRequest.isHidden) : undefined
  });
  const response = await requestApi<ApiResponse<PagedResult<MenuListItem>>>(`/menus?${query}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }

  return response.data;
}

export async function getMenusAll() {
  const response = await requestApi<ApiResponse<MenuListItem[]>>("/menus/all");
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }

  return response.data;
}

export async function createMenu(request: MenuCreateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>("/menus", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "创建失败");
  }
}

export async function updateMenu(id: string, request: MenuUpdateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/menus/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新失败");
  }
}

export async function getPositionsPaged(pagedRequest: PagedRequest) {
  const query = toQuery(pagedRequest);
  const response = await requestApi<ApiResponse<PagedResult<PositionListItem>>>(`/positions?${query}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }

  return response.data;
}

export async function getPositionsAll() {
  const response = await requestApi<ApiResponse<PositionListItem[]>>("/positions/all");
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }

  return response.data;
}

export async function getPositionDetail(id: string) {
  const response = await requestApi<ApiResponse<PositionDetail>>(`/positions/${id}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }

  return response.data;
}

export async function createPosition(request: PositionCreateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>("/positions", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "创建失败");
  }
}

export async function updatePosition(id: string, request: PositionUpdateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/positions/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新失败");
  }
}

export async function deletePosition(id: string) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/positions/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除失败");
  }
}

export async function getAlertsPaged(pagedRequest: PagedRequest) {
  const query = toQuery(pagedRequest);
  const response = await requestApi<ApiResponse<PagedResult<AlertListItem>>>(`/alert?${query}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }

  return response.data;
}

// toQuery is imported from api-core.ts

function toApprovalFlowRequest(req: FlowSaveRequest): ApprovalFlowDefinitionCreateRequest {
  return {
    name: req.definition.name || req.definition.code || "未命名流程",
    definitionJson: JSON.stringify(req.definition),
    description: req.definition.remark || undefined
  };
}

function parseFlowDefinition(definitionJson: string): FlowDefinition {
  return JSON.parse(definitionJson) as FlowDefinition;
}

// 审批流 API
export async function getApprovalFlowsPaged(pagedRequest: PagedRequest) {
  const query = toQuery(pagedRequest);
  const response = await requestApi<ApiResponse<PagedResult<ApprovalFlowDefinitionListItem>>>(
    `/approval/flows?${query}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function getApprovalFlowById(id: string) {
  const response = await requestApi<ApiResponse<ApprovalFlowDefinitionResponse>>(`/approval/flows/${id}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function createApprovalFlow(request: ApprovalFlowDefinitionCreateRequest) {
  const response = await requestApi<ApiResponse<ApprovalFlowDefinitionResponse>>("/approval/flows", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "创建失败");
  }
  return response.data;
}

export async function updateApprovalFlow(id: string, request: ApprovalFlowDefinitionUpdateRequest) {
  const response = await requestApi<ApiResponse<ApprovalFlowDefinitionResponse>>(`/approval/flows/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "更新失败");
  }
  return response.data;
}

export async function deleteApprovalFlow(id: string) {
  const response = await requestApi<ApiResponse<void>>(`/approval/flows/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除失败");
  }
}

export async function publishApprovalFlow(id: string, request?: ApprovalFlowPublishRequest) {
  const response = await requestApi<ApiResponse<void>>(`/approval/flows/${id}/publication`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request || {})
  });
  if (!response.success) {
    throw new Error(response.message || "发布失败");
  }
}

export async function validateApprovalFlow(request: ApprovalFlowDefinitionCreateRequest): Promise<ApprovalFlowValidationResult> {
  const response = await requestApi<ApiResponse<ApprovalFlowValidationResult>>("/approval/flows/validation", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "校验失败");
  }
  return response.data;
}

export async function disableApprovalFlow(id: string) {
  const response = await requestApi<ApiResponse<void>>(`/approval/flows/${id}/deactivation`, {
    method: "POST"
  });
  if (!response.success) {
    throw new Error(response.message || "停用失败");
  }
}

export async function startApprovalInstance(request: ApprovalStartRequest) {
  const response = await requestApi<ApiResponse<ApprovalInstanceResponse>>("/approval/instances", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "发起失败");
  }
  return response.data;
}

export async function getMyInstancesPaged(pagedRequest: PagedRequest, status?: number) {
  const params = new URLSearchParams(toQuery(pagedRequest));
  if (status !== undefined) {
    params.append("status", status.toString());
  }
  const response = await requestApi<ApiResponse<PagedResult<ApprovalInstanceListItem>>>(
    `/approval/instances/my?${params.toString()}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function getApprovalInstanceById(id: string) {
  const response = await requestApi<ApiResponse<ApprovalInstanceResponse>>(`/approval/instances/${id}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function getApprovalInstanceHistory(id: string, pagedRequest: PagedRequest) {
  const query = toQuery(pagedRequest);
  const response = await requestApi<ApiResponse<PagedResult<ApprovalHistoryEventResponse>>>(
    `/approval/instances/${id}/history?${query}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function cancelApprovalInstance(id: string) {
  const response = await requestApi<ApiResponse<void>>(`/approval/instances/${id}/cancellation`, {
    method: "POST"
  });
  if (!response.success) {
    throw new Error(response.message || "取消失败");
  }
}

export async function getMyTasksPaged(pagedRequest: PagedRequest, status?: number) {
  const params = new URLSearchParams(toQuery(pagedRequest));
  if (status !== undefined) {
    params.append("status", status.toString());
  }
  const response = await requestApi<ApiResponse<PagedResult<ApprovalTaskResponse>>>(
    `/approval/tasks/my?${params.toString()}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function getApprovalTaskById(id: string) {
  const response = await requestApi<ApiResponse<ApprovalTaskResponse>>(`/approval/tasks/${id}`);
  if (!response.data) {
    throw new Error(response.message || "任务不存在");
  }
  return response.data;
}

export async function getApprovalTasksByInstance(instanceId: string, pagedRequest: PagedRequest) {
  const query = toQuery(pagedRequest);
  const response = await requestApi<ApiResponse<PagedResult<ApprovalTaskResponse>>>(
    `/approval/instances/${instanceId}/tasks?${query}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function decideApprovalTask(request: ApprovalTaskDecideRequest) {
  const response = await requestApi<ApiResponse<void>>(`/approval/tasks/${request.taskId}/decision`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "操作失败");
  }
}

export async function delegateTask(taskId: string, delegateeUserId: string, comment?: string) {
  const response = await requestApi<ApiResponse<string>>(`/approval/tasks/${taskId}/delegation?delegateeUserId=${delegateeUserId}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(comment || "")
  });
  if (!response.success) {
    throw new Error(response.message || "委派失败");
  }
}

export async function transferTask(instanceId: string, taskId: string, targetAssigneeValue: string, comment?: string) {
  const request = {
    operationType: 21, // Transfer
    targetAssigneeValue,
    comment
  };
  const response = await requestApi<ApiResponse<string>>(`/approval/instances/${instanceId}/operations?taskId=${taskId}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "转办失败");
  }
}

export async function resolveTask(taskId: string, comment?: string) {
  const response = await requestApi<ApiResponse<string>>(`/approval/tasks/${taskId}/resolution`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(comment || "")
  });
  if (!response.success) {
    throw new Error(response.message || "归还失败");
  }
}

export async function claimTask(taskId: string) {
  const response = await requestApi<ApiResponse<string>>(`/approval/tasks/${taskId}/claim`, {
    method: "POST"
  });
  if (!response.success) {
    throw new Error(response.message || "认领失败");
  }
}

export async function urgeTask(taskId: string, message?: string) {
  const response = await requestApi<ApiResponse<string>>(`/approval/tasks/${taskId}/urge`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(message || "")
  });
  if (!response.success) {
    throw new Error(response.message || "催办失败");
  }
}

export async function communicateTask(taskId: string, recipientUserId: string, content: string) {
  const response = await requestApi<ApiResponse<string>>(`/approval/tasks/${taskId}/communication?recipientUserId=${recipientUserId}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(content)
  });
  if (!response.success) {
    throw new Error(response.message || "沟通失败");
  }
}

export async function getCommunications(taskId: string) {
  const response = await requestApi<ApiResponse<ApprovalCommunicationMessage[]>>(`/approval/tasks/${taskId}/communications`);
  if (!response.data) {
    throw new Error(response.message || "获取沟通记录失败");
  }
  return response.data;
}

export interface ApprovalCommunicationMessage {
  id: string;
  senderUserId: string;
  senderName?: string;
  content: string;
  createdAt: string;
}

export async function jumpTask(instanceId: string, targetNodeId: string, taskId?: string) {
  const request = {
    operationType: 36, // Jump
    targetNodeId
  };
  const query = taskId ? `?taskId=${encodeURIComponent(taskId)}` : "";
  const response = await requestApi<ApiResponse<string>>(`/approval/instances/${instanceId}/operations${query}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "跳转失败");
  }
}

export async function reclaimTask(instanceId: string, taskId: string) {
  const request = {
    operationType: 37 // Reclaim
  };
  const response = await requestApi<ApiResponse<string>>(`/approval/instances/${instanceId}/operations?taskId=${taskId}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "拿回失败");
  }
}

export async function getTaskPool(pagedRequest: PagedRequest) {
  const query = toQuery(pagedRequest);
  const response = await requestApi<ApiResponse<PagedResult<ApprovalTaskResponse>>>(`/approval/tasks/pool?${query}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function batchTransferTasks(fromUserId: string, toUserId: string) {
  const response = await requestApi<ApiResponse<string>>(`/approval/tasks/batch-transfer?fromUserId=${fromUserId}&toUserId=${toUserId}`, {
    method: "POST"
  });
  if (!response.success) {
    throw new Error(response.message || "转办失败");
  }
}

export async function suspendInstance(instanceId: string) {
  const response = await requestApi<ApiResponse<string>>(`/approval/instances/${instanceId}/suspension`, {
    method: "POST"
  });
  if (!response.success) {
    throw new Error(response.message || "挂起失败");
  }
}

export async function activateInstance(instanceId: string) {
  const response = await requestApi<ApiResponse<string>>(`/approval/instances/${instanceId}/activation`, {
    method: "POST"
  });
  if (!response.success) {
    throw new Error(response.message || "激活失败");
  }
}

export async function terminateInstance(instanceId: string, comment?: string) {
  const response = await requestApi<ApiResponse<string>>(`/approval/instances/${instanceId}/termination`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(comment || "")
  });
  if (!response.success) {
    throw new Error(response.message || "终止失败");
  }
}

export async function saveDraft(request: ApprovalStartRequest) {
  const response = await requestApi<ApiResponse<ApprovalInstanceResponse>>("/approval/instances/draft", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "保存草稿失败");
  }
  return response.data;
}

export async function submitDraft(instanceId: string) {
  const response = await requestApi<ApiResponse<ApprovalInstanceResponse>>(`/approval/instances/${instanceId}/submission`, {
    method: "POST"
  });
  if (!response.data) {
    throw new Error(response.message || "提交草稿失败");
  }
  return response.data;
}

export async function markTaskViewed(taskId: string) {
  const response = await requestApi<ApiResponse<string>>(`/approval/tasks/${taskId}/viewed`, {
    method: "POST"
  });
  if (!response.success) {
    throw new Error(response.message || "操作失败");
  }
}

// WorkflowCore 相关 API

export async function getWorkflowStepTypes(): Promise<StepTypeMetadata[]> {
  const response = await requestApi<ApiResponse<StepTypeMetadata[]>>("/workflows/step-types");
  if (!response.data) {
    throw new Error(response.message || "获取步骤类型失败");
  }
  return response.data;
}

export async function registerWorkflow(request: RegisterWorkflowDefinitionRequest) {
  const response = await requestApi<ApiResponse<{ success: boolean; workflowId: string; version: number }>>(
    "/workflows/definitions",
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.success) {
    throw new Error(response.message || "注册工作流失败");
  }
  return response.data;
}

export async function startWorkflow(data: { workflowId: string; version?: number; data?: any; reference?: string }) {
  const response = await requestApi<ApiResponse<{ instanceId: string }>>("/workflows/instances", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(data)
  });
  if (!response.data) {
    throw new Error(response.message || "启动工作流失败");
  }
  return response.data.instanceId;
}

export async function getWorkflowInstance(instanceId: string): Promise<WorkflowInstanceResponse> {
  const response = await requestApi<ApiResponse<WorkflowInstanceResponse>>(`/workflows/instances/${instanceId}`);
  if (!response.data) {
    throw new Error(response.message || "获取工作流实例失败");
  }
  return response.data;
}

export async function getWorkflowInstances(pagedRequest: PagedRequest): Promise<PagedResult<WorkflowInstanceListItem>> {
  const query = toQuery(pagedRequest);
  const response = await requestApi<ApiResponse<PagedResult<WorkflowInstanceListItem>>>(
    `/workflows/instances?${query}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询工作流实例失败");
  }
  return response.data;
}

export async function getExecutionPointers(instanceId: string): Promise<ExecutionPointerResponse[]> {
  const response = await requestApi<ApiResponse<ExecutionPointerResponse[]>>(
    `/workflows/instances/${instanceId}/pointers`
  );
  if (!response.data) {
    throw new Error(response.message || "获取执行指针失败");
  }
  return response.data;
}

export async function suspendWorkflow(instanceId: string) {
  const response = await requestApi<ApiResponse<{ success: boolean }>>(
    `/workflows/instances/${instanceId}/suspend`,
    { method: "POST" }
  );
  if (!response.success) {
    throw new Error(response.message || "挂起工作流失败");
  }
}

export async function resumeWorkflow(instanceId: string) {
  const response = await requestApi<ApiResponse<{ success: boolean }>>(
    `/workflows/instances/${instanceId}/resume`,
    { method: "POST" }
  );
  if (!response.success) {
    throw new Error(response.message || "恢复工作流失败");
  }
}

export async function terminateWorkflow(instanceId: string) {
  const response = await requestApi<ApiResponse<{ success: boolean }>>(
    `/workflows/instances/${instanceId}/terminate`,
    { method: "POST" }
  );
  if (!response.success) {
    throw new Error(response.message || "终止工作流失败");
  }
}

// Visualization module
export async function getVisualizationOverview(params?: {
  department?: string;
  flowType?: string;
  from?: string;
  to?: string;
}): Promise<VisualizationOverview> {
  const query = params
    ? new URLSearchParams(
        Object.entries(params).reduce((acc, [k, v]) => {
          if (v) acc[k] = v;
          return acc;
        }, {} as Record<string, string>)
      ).toString()
    : "";
  const response = await requestApi<ApiResponse<VisualizationOverview>>(
    `/visualization/overview${query ? `?${query}` : ""}`
  );
  if (!response.data) {
    throw new Error(response.message || "获取概览失败");
  }
  return response.data;
}

export async function getVisualizationProcesses(
  pagedRequest: PagedRequest
): Promise<PagedResult<VisualizationProcessSummary>> {
  const query = toQuery(pagedRequest);
  const response = await requestApi<ApiResponse<PagedResult<VisualizationProcessSummary>>>(
    `/visualization/processes?${query}`
  );
  if (!response.data) {
    throw new Error(response.message || "获取流程列表失败");
  }
  return response.data;
}

export async function getVisualizationInstances(
  pagedRequest: PagedRequest,
  params?: { processId?: string; status?: string }
): Promise<PagedResult<VisualizationInstanceSummary>> {
  const queryParams = new URLSearchParams(toQuery(pagedRequest));
  if (params?.processId) {
    queryParams.append("processId", params.processId);
  }
  if (params?.status) {
    queryParams.append("status", params.status);
  }
  const query = queryParams.toString();
  const response = await requestApi<ApiResponse<PagedResult<VisualizationInstanceSummary>>>(
    `/visualization/instances?${query}`
  );
  if (!response.data) {
    throw new Error(response.message || "获取实例列表失败");
  }
  return response.data;
}

export async function validateVisualizationProcess(
  request: ValidateVisualizationRequest
): Promise<VisualizationValidationResult> {
  const response = await requestApi<ApiResponse<VisualizationValidationResult>>(
    "/visualization/processes/validation",
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.data) {
    throw new Error(response.message || "校验失败");
  }
  return response.data;
}

export async function publishVisualizationProcess(
  request: PublishVisualizationRequest
): Promise<VisualizationPublishResult> {
  const response = await requestApi<ApiResponse<VisualizationPublishResult>>(
    `/visualization/processes/${request.processId}/publication`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.data) {
    throw new Error(response.message || "发布失败");
  }
  return response.data;
}

export async function saveVisualizationProcess(
  request: SaveVisualizationProcessRequest
): Promise<SaveVisualizationProcessResult> {
  const isUpdate = Boolean(request.processId);
  const path = isUpdate
    ? `/visualization/processes/${request.processId}`
    : "/visualization/processes";
  const response = await requestApi<ApiResponse<SaveVisualizationProcessResult>>(path, {
    method: isUpdate ? "PUT" : "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "保存失败");
  }
  return response.data;
}

export async function getVisualizationProcessDetail(id: string): Promise<VisualizationProcessDetail> {
  const response = await requestApi<ApiResponse<VisualizationProcessDetail>>(`/visualization/processes/${id}`);
  if (!response.data) {
    throw new Error(response.message || "获取流程详情失败");
  }
  return response.data;
}

export async function getVisualizationInstanceDetail(
  id: string
): Promise<VisualizationInstanceDetail> {
  const response = await requestApi<ApiResponse<VisualizationInstanceDetail>>(
    `/visualization/instances/${id}`
  );
  if (!response.data) {
    throw new Error(response.message || "获取实例详情失败");
  }
  return response.data;
}

export async function getVisualizationMetrics(params?: {
  department?: string;
  flowType?: string;
  from?: string;
  to?: string;
}): Promise<VisualizationMetricsResponse> {
  const query = params
    ? new URLSearchParams(
        Object.entries(params).reduce((acc, [k, v]) => {
          if (v) acc[k] = v;
          return acc;
        }, {} as Record<string, string>)
      ).toString()
    : "";
  const response = await requestApi<ApiResponse<VisualizationMetricsResponse>>(
    `/visualization/metrics${query ? `?${query}` : ""}`
  );
  if (!response.data) {
    throw new Error(response.message || "获取指标失败");
  }
  return response.data;
}

export async function getVisualizationAudit(
  pagedRequest: PagedRequest
): Promise<PagedResult<AuditListItem>> {
  const query = toQuery(pagedRequest);
  const response = await requestApi<ApiResponse<PagedResult<AuditListItem>>>(`/visualization/audit?${query}`);
  if (!response.data) {
    throw new Error(response.message || "获取审计记录失败");
  }
  return response.data;
}

// ---------- Workflow Designer (Visualization) ----------
export async function loadFlowDefinition(id: string): Promise<FlowDefinition> {
  const response = await requestApi<ApiResponse<ApprovalFlowDefinitionResponse>>(`/approval/flows/${id}`);
  if (!response.data?.definitionJson) {
    throw new Error(response.message || "加载流程失败");
  }
  return parseFlowDefinition(response.data.definitionJson);
}

export async function saveFlowDefinition(req: FlowSaveRequest): Promise<FlowSaveResponse> {
  const payload = toApprovalFlowRequest(req);
  const response = await requestApi<ApiResponse<ApprovalFlowDefinitionResponse>>("/approval/flows", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
  });
  if (!response.data) {
    throw new Error(response.message || "保存流程失败");
  }
  return { id: response.data.id, version: response.data.version };
}

export async function updateFlowDefinition(id: string, req: FlowSaveRequest): Promise<void> {
  const payload = toApprovalFlowRequest(req);
  const response = await requestApi<ApiResponse<ApprovalFlowDefinitionResponse>>(`/approval/flows/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
  });
  if (!response.data) {
    throw new Error(response.message || "更新流程失败");
  }
}

export async function publishFlowDefinition(id: string): Promise<FlowPublishResponse> {
  const response = await requestApi<ApiResponse<FlowPublishResponse>>(`/approval/flows/${id}/publication`, {
    method: "POST"
  });
  if (!response.data) {
    throw new Error(response.message || "发布流程失败");
  }
  return response.data;
}

export async function validateFlowDefinition(req: FlowSaveRequest): Promise<FlowValidationResult> {
  const payload = toApprovalFlowRequest(req);
  const response = await requestApi<ApiResponse<FlowValidationResult>>("/approval/flows/validation", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
  });
  if (!response.data) {
    throw new Error(response.message || "校验失败");
  }
  return response.data;
}

export async function previewFlowDefinition(id: string): Promise<FlowDefinition> {
  const response = await requestApi<ApiResponse<FlowLoadResponse>>(`/approval/flows/${id}/preview`, {
    method: "POST"
  });
  if (!response.data?.definition) {
    throw new Error(response.message || "预览失败");
  }
  return response.data.definition;
}

export async function getAppConfigsPaged(pagedRequest: PagedRequest) {
  const query = toQuery(pagedRequest);
  const response = await requestApi<ApiResponse<PagedResult<AppConfigListItem>>>(`/apps?${query}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }

  return response.data;
}

export async function getAppConfigDetail(id: string) {
  const response = await requestApi<ApiResponse<AppConfigDetail>>(`/apps/${id}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function updateAppConfig(id: string, request: AppConfigUpdateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/apps/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新失败");
  }
}

export async function getCurrentAppConfig(): Promise<AppConfigDetail | null> {
  const response = await requestApi<ApiResponse<AppConfigDetail>>("/apps/current");
  return response.data ?? null;
}

export async function getProjectsPaged(pagedRequest: PagedRequest) {
  const query = toQuery(pagedRequest);
  const response = await requestApi<ApiResponse<PagedResult<ProjectListItem>>>(`/projects?${query}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function getProjectDetail(id: string) {
  const response = await requestApi<ApiResponse<ProjectDetail>>(`/projects/${id}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function createProject(request: ProjectCreateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>("/projects", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "创建失败");
  }
}

export async function updateProject(id: string, request: ProjectUpdateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/projects/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新失败");
  }
}

export async function deleteProject(id: string) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/projects/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除失败");
  }
}

export async function updateProjectUsers(id: string, request: ProjectAssignUsersRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/projects/${id}/users`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新项目人员失败");
  }
}

export async function updateProjectDepartments(id: string, request: ProjectAssignDepartmentsRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/projects/${id}/departments`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新项目部门失败");
  }
}

export async function updateProjectPositions(id: string, request: ProjectAssignPositionsRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/projects/${id}/positions`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新项目岗位失败");
  }
}

export async function getMyProjects() {
  const response = await requestApi<ApiResponse<ProjectListItem[]>>("/projects/my");
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function getTenantDataSources(): Promise<TenantDataSourceDto[]> {
  const response = await requestApi<ApiResponse<TenantDataSourceDto[]>>("/tenant-datasources");
  if (!response.data) {
    throw new Error(response.message || "查询数据源失败");
  }
  return response.data;
}

export async function createTenantDataSource(request: TenantDataSourceCreateRequest): Promise<{ id: string }> {
  const response = await requestApi<ApiResponse<{ id: string }>>("/tenant-datasources", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "创建数据源失败");
  }
  return response.data;
}

export async function updateTenantDataSource(
  id: string,
  request: TenantDataSourceUpdateRequest
): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/tenant-datasources/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新数据源失败");
  }
}

export async function deleteTenantDataSource(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/tenant-datasources/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除数据源失败");
  }
}

export async function testTenantDataSourceConnection(
  request: TenantDataSourceTestConnectionRequest
): Promise<TenantDataSourceTestConnectionResult> {
  const response = await requestApi<ApiResponse<TenantDataSourceTestConnectionResult>>("/tenant-datasources/test", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "连接测试失败");
  }
  return response.data;
}

// ---------- Table Views (Personal) ----------
export async function getTableViewsPaged(tableKey: string, pagedRequest: PagedRequest) {
  const queryParams = new URLSearchParams(toQuery(pagedRequest));
  queryParams.append("tableKey", tableKey);
  const response = await requestApi<ApiResponse<PagedResult<TableViewListItem>>>(`/table-views?${queryParams}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function getDefaultTableView(tableKey: string): Promise<TableViewDetail | null> {
  const response = await requestApi<ApiResponse<TableViewDetail | null>>(
    `/table-views/default?tableKey=${encodeURIComponent(tableKey)}`
  );
  return response.data ?? null;
}

export async function getDefaultTableViewConfig(tableKey: string): Promise<TableViewConfig | null> {
  const response = await requestApi<ApiResponse<TableViewConfig | null>>(
    `/table-views/default-config?tableKey=${encodeURIComponent(tableKey)}`
  );
  return response.data ?? null;
}

export async function getTableViewDetail(id: string): Promise<TableViewDetail> {
  const response = await requestApi<ApiResponse<TableViewDetail>>(`/table-views/${id}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function createTableView(request: TableViewCreateRequest): Promise<{ id: string }> {
  const response = await requestApi<ApiResponse<{ id: string }>>("/table-views", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "创建失败");
  }
  return response.data;
}

export async function updateTableView(id: string, request: TableViewUpdateRequest): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/table-views/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新失败");
  }
}

export async function updateTableViewConfig(id: string, request: TableViewConfigUpdateRequest): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/table-views/${id}/config`, {
    method: "PATCH",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新失败");
  }
}

export async function duplicateTableView(id: string, request: TableViewDuplicateRequest): Promise<{ id: string }> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/table-views/${id}/duplicate`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "复制失败");
  }
  return response.data;
}

export async function setDefaultTableView(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/table-views/${id}/set-default`, {
    method: "POST"
  });
  if (!response.success) {
    throw new Error(response.message || "设置失败");
  }
}

export async function deleteTableView(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/table-views/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除失败");
  }
}

// Core infrastructure (requestApi, error handling, token management) is in api-core.ts
