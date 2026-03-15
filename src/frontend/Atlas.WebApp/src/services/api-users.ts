// 用户/组织/权限/菜单/职位/告警模块 API
import type {
  ApiResponse,
  PagedRequest,
  PagedResult,
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
  PositionUpdateRequest
} from "@/types/api";
import type { AlertListItem } from "@/services/api-auth";
import { requestApi, toQuery } from "@/services/api-core";

export async function getAssetsPaged(pagedRequest: PagedRequest) {
  const query = toQuery(pagedRequest);
  const response = await requestApi<ApiResponse<PagedResult<{ id: string; name: string }>>>(`/assets?${query}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function getAuditsPaged(
  pagedRequest: PagedRequest,
  extra?: { action?: string; result?: string }
) {
  const query = toQuery(pagedRequest, {
    action: extra?.action,
    result: extra?.result
  });
  const response = await requestApi<ApiResponse<PagedResult<AuditListItem>>>(`/audit?${query}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }

  return response.data;
}

export interface ClientErrorReportRequest {
  message: string;
  stack?: string;
  url?: string;
  component?: string;
  level?: string;
}

export async function reportClientError(request: ClientErrorReportRequest): Promise<void> {
  const response = await requestApi<ApiResponse<{ success: boolean }>>("/audit/client-errors", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  }, { suppressErrorMessage: true });
  if (!response.success) {
    throw new Error(response.message || "上报失败");
  }
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

export async function setRoleDataScope(id: string, dataScope: number, deptIds?: string[]) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/roles/${id}/data-scope`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ dataScope, deptIds })
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
