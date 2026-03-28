import { requestApi } from "@/services/api-core";
import type { ApiResponse, PagedRequest } from "@/types/api";
import type {
  AppDepartmentListItem,
  AppOrganizationAssignMembersRequest,
  AppOrganizationCreateMemberUserRequest,
  AppOrganizationCreateDepartmentRequest,
  AppOrganizationCreatePositionRequest,
  AppOrganizationCreateProjectRequest,
  AppOrganizationCreateRoleRequest,
  AppOrganizationUpdateDepartmentRequest,
  AppOrganizationUpdateMemberRolesRequest,
  AppOrganizationUpdatePositionRequest,
  AppOrganizationUpdateProjectRequest,
  AppOrganizationUpdateRoleRequest,
  AppOrganizationWorkspaceResponse
} from "@/types/platform-v2";

function buildOrgBasePath(appId: string) {
  return `/api/v2/tenant-app-instances/${appId}/organization`;
}

export async function getAppOrganizationWorkspace(appId: string, request: PagedRequest) {
  const query = new URLSearchParams({
    pageIndex: request.pageIndex.toString(),
    pageSize: request.pageSize.toString()
  });
  if (request.keyword) {
    query.set("keyword", request.keyword);
  }

  const response = await requestApi<ApiResponse<AppOrganizationWorkspaceResponse>>(
    `${buildOrgBasePath(appId)}/workspace?${query.toString()}`
  );
  if (!response.data) {
    throw new Error(response.message || "查询组织工作区失败");
  }

  return response.data;
}

export async function addOrganizationMembers(appId: string, request: AppOrganizationAssignMembersRequest) {
  const response = await requestApi<ApiResponse<object>>(`${buildOrgBasePath(appId)}/members`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "添加成员失败");
  }
}

export async function createOrganizationMemberUser(appId: string, request: AppOrganizationCreateMemberUserRequest) {
  const response = await requestApi<ApiResponse<{ userId: string }>>(`${buildOrgBasePath(appId)}/members/users`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "创建用户失败");
  }
}

export async function updateOrganizationMemberRoles(appId: string, userId: string, request: AppOrganizationUpdateMemberRolesRequest) {
  const response = await requestApi<ApiResponse<object>>(`${buildOrgBasePath(appId)}/members/${userId}/roles`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新成员角色失败");
  }
}

export async function removeOrganizationMember(appId: string, userId: string) {
  const response = await requestApi<ApiResponse<object>>(`${buildOrgBasePath(appId)}/members/${userId}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "移除成员失败");
  }
}

export async function createOrganizationRole(appId: string, request: AppOrganizationCreateRoleRequest) {
  const response = await requestApi<ApiResponse<{ roleId: string }>>(`${buildOrgBasePath(appId)}/roles`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "创建角色失败");
  }
}

export async function updateOrganizationRole(appId: string, roleId: string, request: AppOrganizationUpdateRoleRequest) {
  const response = await requestApi<ApiResponse<object>>(`${buildOrgBasePath(appId)}/roles/${roleId}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新角色失败");
  }
}

export async function deleteOrganizationRole(appId: string, roleId: string) {
  const response = await requestApi<ApiResponse<object>>(`${buildOrgBasePath(appId)}/roles/${roleId}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除角色失败");
  }
}

export async function createOrganizationDepartment(appId: string, request: AppOrganizationCreateDepartmentRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`${buildOrgBasePath(appId)}/departments`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "创建部门失败");
  }
}

export async function updateOrganizationDepartment(appId: string, id: string, request: AppOrganizationUpdateDepartmentRequest) {
  const response = await requestApi<ApiResponse<object>>(`${buildOrgBasePath(appId)}/departments/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新部门失败");
  }
}

export async function deleteOrganizationDepartment(appId: string, id: string) {
  const response = await requestApi<ApiResponse<object>>(`${buildOrgBasePath(appId)}/departments/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除部门失败");
  }
}

export async function createOrganizationPosition(appId: string, request: AppOrganizationCreatePositionRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`${buildOrgBasePath(appId)}/positions`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "创建职位失败");
  }
}

export async function updateOrganizationPosition(appId: string, id: string, request: AppOrganizationUpdatePositionRequest) {
  const response = await requestApi<ApiResponse<object>>(`${buildOrgBasePath(appId)}/positions/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新职位失败");
  }
}

export async function deleteOrganizationPosition(appId: string, id: string) {
  const response = await requestApi<ApiResponse<object>>(`${buildOrgBasePath(appId)}/positions/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除职位失败");
  }
}

export async function createOrganizationProject(appId: string, request: AppOrganizationCreateProjectRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`${buildOrgBasePath(appId)}/projects`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "创建项目失败");
  }
}

export async function updateOrganizationProject(appId: string, id: string, request: AppOrganizationUpdateProjectRequest) {
  const response = await requestApi<ApiResponse<object>>(`${buildOrgBasePath(appId)}/projects/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新项目失败");
  }
}

export async function deleteOrganizationProject(appId: string, id: string) {
  const response = await requestApi<ApiResponse<object>>(`${buildOrgBasePath(appId)}/projects/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除项目失败");
  }
}

export function getDepartmentParentLabel(items: AppDepartmentListItem[], parentId?: string) {
  if (!parentId) {
    return "-";
  }

  const matched = items.find((item) => item.id === parentId);
  return matched?.name ?? parentId;
}
