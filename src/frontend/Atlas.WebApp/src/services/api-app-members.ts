import { requestApi } from "@/services/api-core";
import type { ApiResponse, PagedRequest, PagedResult } from "@/types/api";
import type {
  AppDepartmentCreateRequest,
  AppDepartmentDetail,
  AppDepartmentListItem,
  AppDepartmentUpdateRequest,
  AppPageListItem,
  AppPositionCreateRequest,
  AppPositionDetail,
  AppPositionListItem,
  AppPositionUpdateRequest,
  AppProjectCreateRequest,
  AppProjectDetail,
  AppProjectListItem,
  AppProjectUpdateRequest,
  AppRoleAssignmentDetail,
  AppRoleDataScopeRequest,
  AppRoleFieldPermissionGroup,
  AppRoleFieldPermissionsRequest,
  AppRolePagesRequest,
  TenantAppMemberAssignRequest,
  TenantAppMemberDetail,
  TenantAppMemberListItem,
  TenantAppMemberUpdateRolesRequest,
  TenantAppRoleAssignPermissionsRequest,
  TenantAppRoleCreateRequest,
  TenantAppRoleDetail,
  TenantAppRoleGovernanceOverview,
  TenantAppRoleListItem,
  TenantAppRoleUpdateRequest
} from "@/types/platform-v2";
import type { PermissionCreateRequest, PermissionListItem, PermissionUpdateRequest } from "@/types/api";

function buildMemberBasePath(appId: string) {
  return `/api/v2/tenant-app-instances/${appId}/members`;
}

function buildRoleBasePath(appId: string) {
  return `/api/v2/tenant-app-instances/${appId}/roles`;
}

export async function getTenantAppMembersPaged(
  appId: string,
  params: PagedRequest
): Promise<PagedResult<TenantAppMemberListItem>> {
  const query = new URLSearchParams({
    pageIndex: params.pageIndex.toString(),
    pageSize: params.pageSize.toString()
  });
  if (params.keyword) {
    query.set("keyword", params.keyword);
  }

  const response = await requestApi<ApiResponse<PagedResult<TenantAppMemberListItem>>>(
    `${buildMemberBasePath(appId)}?${query.toString()}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询应用成员失败");
  }

  return response.data;
}

export async function getTenantAppMemberDetail(appId: string, userId: string): Promise<TenantAppMemberDetail> {
  const response = await requestApi<ApiResponse<TenantAppMemberDetail>>(
    `${buildMemberBasePath(appId)}/${userId}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询应用成员详情失败");
  }

  return response.data;
}

export async function addTenantAppMembers(appId: string, request: TenantAppMemberAssignRequest): Promise<void> {
  const response = await requestApi<ApiResponse<{ appId: string }>>(buildMemberBasePath(appId), {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "添加应用成员失败");
  }
}

export async function updateTenantAppMemberRoles(
  appId: string,
  userId: string,
  request: TenantAppMemberUpdateRolesRequest
): Promise<void> {
  const response = await requestApi<ApiResponse<{ appId: string; userId: string }>>(
    `${buildMemberBasePath(appId)}/${userId}/roles`,
    {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.success) {
    throw new Error(response.message || "更新应用成员角色失败");
  }
}

export async function removeTenantAppMember(appId: string, userId: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ appId: string; userId: string }>>(
    `${buildMemberBasePath(appId)}/${userId}`,
    {
      method: "DELETE"
    }
  );
  if (!response.success) {
    throw new Error(response.message || "移除应用成员失败");
  }
}

export async function getTenantAppRolesPaged(
  appId: string,
  params: PagedRequest
): Promise<PagedResult<TenantAppRoleListItem>> {
  const query = new URLSearchParams({
    pageIndex: params.pageIndex.toString(),
    pageSize: params.pageSize.toString()
  });
  if (params.keyword) {
    query.set("keyword", params.keyword);
  }

  const response = await requestApi<ApiResponse<PagedResult<TenantAppRoleListItem>>>(
    `${buildRoleBasePath(appId)}?${query.toString()}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询应用角色失败");
  }

  return response.data;
}

export async function getTenantAppRoleGovernanceOverview(appId: string): Promise<TenantAppRoleGovernanceOverview> {
  const response = await requestApi<ApiResponse<TenantAppRoleGovernanceOverview>>(
    `${buildRoleBasePath(appId)}/governance-overview`
  );
  if (!response.data) {
    throw new Error(response.message || "查询应用角色治理概览失败");
  }

  return response.data;
}

export async function getTenantAppRoleDetail(appId: string, roleId: string): Promise<TenantAppRoleDetail> {
  const response = await requestApi<ApiResponse<TenantAppRoleDetail>>(
    `${buildRoleBasePath(appId)}/${roleId}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询应用角色详情失败");
  }

  return response.data;
}

export async function createTenantAppRole(appId: string, request: TenantAppRoleCreateRequest): Promise<{ roleId: string }> {
  const response = await requestApi<ApiResponse<{ appId: string; roleId: string }>>(buildRoleBasePath(appId), {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "创建应用角色失败");
  }

  return { roleId: response.data.roleId };
}

export async function updateTenantAppRole(
  appId: string,
  roleId: string,
  request: TenantAppRoleUpdateRequest
): Promise<void> {
  const response = await requestApi<ApiResponse<{ appId: string; roleId: string }>>(
    `${buildRoleBasePath(appId)}/${roleId}`,
    {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.success) {
    throw new Error(response.message || "更新应用角色失败");
  }
}

export async function updateTenantAppRolePermissions(
  appId: string,
  roleId: string,
  request: TenantAppRoleAssignPermissionsRequest
): Promise<void> {
  const response = await requestApi<ApiResponse<{ appId: string; roleId: string }>>(
    `${buildRoleBasePath(appId)}/${roleId}/permissions`,
    {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.success) {
    throw new Error(response.message || "更新应用角色权限失败");
  }
}

export async function deleteTenantAppRole(appId: string, roleId: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ appId: string; roleId: string }>>(
    `${buildRoleBasePath(appId)}/${roleId}`,
    {
      method: "DELETE"
    }
  );
  if (!response.success) {
    throw new Error(response.message || "删除应用角色失败");
  }
}

// ===== 应用角色数据范围 API =====

export async function getAppRoleDataScope(appId: string, roleId: string): Promise<AppRoleAssignmentDetail> {
  const response = await requestApi<ApiResponse<AppRoleAssignmentDetail>>(
    `${buildRoleBasePath(appId)}/${roleId}/data-scope`
  );
  if (!response.data) {
    throw new Error(response.message || "查询应用角色数据范围失败");
  }
  return response.data;
}

export async function setAppRoleDataScope(
  appId: string,
  roleId: string,
  request: AppRoleDataScopeRequest
): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(
    `${buildRoleBasePath(appId)}/${roleId}/data-scope`,
    {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.success) {
    throw new Error(response.message || "设置应用角色数据范围失败");
  }
}

// ===== 应用级部门管理 API =====

function buildDeptBasePath(appId: string) {
  return `/api/v2/tenant-app-instances/${appId}/departments`;
}

export async function getAppDepartmentAll(appId: string): Promise<AppDepartmentListItem[]> {
  const response = await requestApi<ApiResponse<AppDepartmentListItem[]>>(`${buildDeptBasePath(appId)}/all`);
  if (!response.data) throw new Error(response.message || "查询应用部门失败");
  return response.data;
}

export async function getAppDepartmentsPaged(appId: string, params: PagedRequest): Promise<PagedResult<AppDepartmentListItem>> {
  const query = new URLSearchParams({ pageIndex: params.pageIndex.toString(), pageSize: params.pageSize.toString() });
  if (params.keyword) query.set("keyword", params.keyword);
  const response = await requestApi<ApiResponse<PagedResult<AppDepartmentListItem>>>(`${buildDeptBasePath(appId)}?${query}`);
  if (!response.data) throw new Error(response.message || "查询应用部门失败");
  return response.data;
}

export async function getAppDepartmentById(appId: string, id: string): Promise<AppDepartmentDetail> {
  const response = await requestApi<ApiResponse<AppDepartmentDetail>>(`${buildDeptBasePath(appId)}/${id}`);
  if (!response.data) throw new Error(response.message || "查询应用部门详情失败");
  return response.data;
}

export async function createAppDepartment(appId: string, request: AppDepartmentCreateRequest): Promise<{ id: string }> {
  const response = await requestApi<ApiResponse<{ id: string }>>(buildDeptBasePath(appId), {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) throw new Error(response.message || "创建应用部门失败");
  return response.data;
}

export async function updateAppDepartment(appId: string, id: string, request: AppDepartmentUpdateRequest): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(`${buildDeptBasePath(appId)}/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) throw new Error(response.message || "更新应用部门失败");
}

export async function deleteAppDepartment(appId: string, id: string): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(`${buildDeptBasePath(appId)}/${id}`, { method: "DELETE" });
  if (!response.success) throw new Error(response.message || "删除应用部门失败");
}

// ===== 应用级职位管理 API =====

function buildPositionBasePath(appId: string) {
  return `/api/v2/tenant-app-instances/${appId}/positions`;
}

export async function getAppPositionsPaged(appId: string, params: PagedRequest): Promise<PagedResult<AppPositionListItem>> {
  const query = new URLSearchParams({ pageIndex: params.pageIndex.toString(), pageSize: params.pageSize.toString() });
  if (params.keyword) query.set("keyword", params.keyword);
  const response = await requestApi<ApiResponse<PagedResult<AppPositionListItem>>>(`${buildPositionBasePath(appId)}?${query}`);
  if (!response.data) throw new Error(response.message || "查询应用职位失败");
  return response.data;
}

export async function createAppPosition(appId: string, request: AppPositionCreateRequest): Promise<{ id: string }> {
  const response = await requestApi<ApiResponse<{ id: string }>>(buildPositionBasePath(appId), {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) throw new Error(response.message || "创建应用职位失败");
  return response.data;
}

export async function updateAppPosition(appId: string, id: string, request: AppPositionUpdateRequest): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(`${buildPositionBasePath(appId)}/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) throw new Error(response.message || "更新应用职位失败");
}

export async function deleteAppPosition(appId: string, id: string): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(`${buildPositionBasePath(appId)}/${id}`, { method: "DELETE" });
  if (!response.success) throw new Error(response.message || "删除应用职位失败");
}

// ===== 应用级项目管理 API =====

function buildProjectBasePath(appId: string) {
  return `/api/v2/tenant-app-instances/${appId}/projects`;
}

export async function getAppProjectsPaged(appId: string, params: PagedRequest): Promise<PagedResult<AppProjectListItem>> {
  const query = new URLSearchParams({ pageIndex: params.pageIndex.toString(), pageSize: params.pageSize.toString() });
  if (params.keyword) query.set("keyword", params.keyword);
  const response = await requestApi<ApiResponse<PagedResult<AppProjectListItem>>>(`${buildProjectBasePath(appId)}?${query}`);
  if (!response.data) throw new Error(response.message || "查询应用项目失败");
  return response.data;
}

export async function createAppProject(appId: string, request: AppProjectCreateRequest): Promise<{ id: string }> {
  const response = await requestApi<ApiResponse<{ id: string }>>(buildProjectBasePath(appId), {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) throw new Error(response.message || "创建应用项目失败");
  return response.data;
}

export async function updateAppProject(appId: string, id: string, request: AppProjectUpdateRequest): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(`${buildProjectBasePath(appId)}/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) throw new Error(response.message || "更新应用项目失败");
}

export async function deleteAppProject(appId: string, id: string): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(`${buildProjectBasePath(appId)}/${id}`, { method: "DELETE" });
  if (!response.success) throw new Error(response.message || "删除应用项目失败");
}

// ===== 应用级功能权限 CRUD =====

function buildAppPermBasePath(appId: string) {
  return `/api/v2/tenant-app-instances/${appId}/permissions`;
}

export async function getAppPermissionsPaged(appId: string, params: PagedRequest & { type?: string }): Promise<PagedResult<PermissionListItem>> {
  const query = new URLSearchParams({ pageIndex: params.pageIndex.toString(), pageSize: params.pageSize.toString() });
  if (params.keyword) query.set("keyword", params.keyword);
  if (params.type) query.set("type", params.type);
  const response = await requestApi<ApiResponse<PagedResult<PermissionListItem>>>(`${buildAppPermBasePath(appId)}?${query}`);
  if (!response.data) throw new Error(response.message || "查询应用权限失败");
  return response.data;
}

export async function createAppPermission(appId: string, request: PermissionCreateRequest): Promise<{ id: string }> {
  const response = await requestApi<ApiResponse<{ id: string }>>(buildAppPermBasePath(appId), {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) throw new Error(response.message || "创建应用权限失败");
  return response.data;
}

export async function updateAppPermission(appId: string, id: string, request: PermissionUpdateRequest): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(`${buildAppPermBasePath(appId)}/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) throw new Error(response.message || "更新应用权限失败");
}

export async function deleteAppPermission(appId: string, id: string): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(`${buildAppPermBasePath(appId)}/${id}`, { method: "DELETE" });
  if (!response.success) throw new Error(response.message || "删除应用权限失败");
}

// ===== 应用角色页面分配 =====

export async function getAppRoleAvailablePages(appId: string): Promise<AppPageListItem[]> {
  const response = await requestApi<ApiResponse<AppPageListItem[]>>(
    `/api/v2/tenant-app-instances/${appId}/roles/available-pages`
  );
  if (!response.data) throw new Error(response.message || "获取应用页面列表失败");
  return response.data;
}

export async function getAppRolePages(appId: string, roleId: string): Promise<number[]> {
  const response = await requestApi<ApiResponse<number[]>>(
    `${buildRoleBasePath(appId)}/${roleId}/pages`
  );
  if (!response.data) throw new Error(response.message || "获取角色页面分配失败");
  return response.data;
}

export async function setAppRolePages(appId: string, roleId: string, request: AppRolePagesRequest): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(
    `${buildRoleBasePath(appId)}/${roleId}/pages`,
    {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.success) throw new Error(response.message || "设置角色页面分配失败");
}

// ===== 应用角色字段权限 =====

export async function getAppRoleFieldPermissions(appId: string, roleId: string): Promise<AppRoleFieldPermissionGroup[]> {
  const response = await requestApi<ApiResponse<AppRoleFieldPermissionGroup[]>>(
    `${buildRoleBasePath(appId)}/${roleId}/field-permissions`
  );
  if (!response.data) throw new Error(response.message || "获取角色字段权限失败");
  return response.data;
}

export async function setAppRoleFieldPermissions(appId: string, roleId: string, request: AppRoleFieldPermissionsRequest): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(
    `${buildRoleBasePath(appId)}/${roleId}/field-permissions`,
    {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.success) throw new Error(response.message || "设置角色字段权限失败");
}

// ===== 应用可用动态表（用于字段权限配置）=====

export interface AppAvailableDynamicTableItem {
  tableKey: string;
  displayName: string;
}

export async function getAppAvailableDynamicTables(
  appId: string,
  keyword?: string
): Promise<AppAvailableDynamicTableItem[]> {
  const params = new URLSearchParams();
  if (keyword?.trim()) params.set("keyword", keyword.trim());
  const query = params.toString() ? `?${params.toString()}` : "";
  const response = await requestApi<ApiResponse<AppAvailableDynamicTableItem[]>>(
    `/api/v2/tenant-app-instances/${appId}/roles/available-dynamic-tables${query}`
  );
  if (!response.data) throw new Error(response.message || "获取应用动态表失败");
  return response.data;
}
