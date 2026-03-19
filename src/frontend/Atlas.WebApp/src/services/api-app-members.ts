import { requestApi } from "@/services/api-core";
import type { ApiResponse, PagedRequest, PagedResult } from "@/types/api";
import type {
  TenantAppMemberAssignRequest,
  TenantAppMemberDetail,
  TenantAppMemberListItem,
  TenantAppMemberUpdateRolesRequest,
  TenantAppRoleAssignPermissionsRequest,
  TenantAppRoleCreateRequest,
  TenantAppRoleDetail,
  TenantAppRoleListItem,
  TenantAppRoleUpdateRequest
} from "@/types/platform-v2";

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
