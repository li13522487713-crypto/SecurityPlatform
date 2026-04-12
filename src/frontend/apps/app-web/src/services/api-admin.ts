import type {
  ApiResponse,
  ChangePasswordRequest,
  DepartmentCreateRequest,
  DepartmentListItem,
  DepartmentUpdateRequest,
  PagedRequest,
  PagedResult,
  PositionCreateRequest,
  PositionDetail,
  PositionListItem,
  PositionUpdateRequest,
  RoleCreateRequest,
  RoleDetail,
  RoleListItem,
  RoleQueryRequest,
  RoleUpdateRequest,
  UserCreateRequest,
  UserDetail,
  UserListItem,
  UserProfileDetail,
  UserProfileUpdateRequest,
  UserUpdateRequest
} from "@atlas/shared-core/types";
import { requestApi, toQuery } from "./api-core";
import {
  changePassword,
  getProfileDetail,
  updateProfile
} from "./api-profile";

export interface AdminApprovalTaskItem {
  id: string;
  instanceId: string;
  flowName: string;
  title: string;
  currentNodeName: string;
  status: number;
  createdAt: string;
}

export interface AdminApprovalInstanceItem {
  id: string;
  flowName: string;
  title: string;
  status: number;
  createdAt: string;
  completedAt?: string;
}

export interface AdminApprovalCopyItem {
  id: string;
  instanceId: string;
  flowName: string;
  title: string;
  isRead: boolean;
  createdAt: string;
}

export async function getUsersPaged(request: PagedRequest): Promise<PagedResult<UserListItem>> {
  const response = await requestApi<ApiResponse<PagedResult<UserListItem>>>(`/users?${toQuery(request)}`);
  if (!response.data) {
    throw new Error(response.message || "查询用户失败");
  }

  return response.data;
}

export async function getUserDetail(id: string): Promise<UserDetail> {
  const response = await requestApi<ApiResponse<UserDetail>>(`/users/${encodeURIComponent(id)}`);
  if (!response.data) {
    throw new Error(response.message || "查询用户详情失败");
  }

  return response.data;
}

export async function createUser(request: UserCreateRequest): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>("/users", {
    method: "POST",
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "创建用户失败");
  }
}

export async function updateUser(id: string, request: UserUpdateRequest): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/users/${encodeURIComponent(id)}`, {
    method: "PUT",
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新用户失败");
  }
}

export async function deleteUser(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/users/${encodeURIComponent(id)}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除用户失败");
  }
}

export async function getRolesPaged(request: RoleQueryRequest): Promise<PagedResult<RoleListItem>> {
  const response = await requestApi<ApiResponse<PagedResult<RoleListItem>>>(`/roles?${toQuery(request, {
    isSystem: typeof request.isSystem === "boolean" ? String(request.isSystem) : undefined
  })}`);
  if (!response.data) {
    throw new Error(response.message || "查询角色失败");
  }

  return response.data;
}

export async function getRoleDetail(id: string): Promise<RoleDetail> {
  const response = await requestApi<ApiResponse<RoleDetail>>(`/roles/${encodeURIComponent(id)}`);
  if (!response.data) {
    throw new Error(response.message || "查询角色详情失败");
  }

  return response.data;
}

export async function createRole(request: RoleCreateRequest): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>("/roles", {
    method: "POST",
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "创建角色失败");
  }
}

export async function updateRole(id: string, request: RoleUpdateRequest): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/roles/${encodeURIComponent(id)}`, {
    method: "PUT",
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新角色失败");
  }
}

export async function deleteRole(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/roles/${encodeURIComponent(id)}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除角色失败");
  }
}

export async function getDepartmentsPaged(request: PagedRequest): Promise<PagedResult<DepartmentListItem>> {
  const response = await requestApi<ApiResponse<PagedResult<DepartmentListItem>>>(`/departments?${toQuery(request)}`);
  if (!response.data) {
    throw new Error(response.message || "查询部门失败");
  }

  return response.data;
}

export async function getDepartmentsAll(): Promise<DepartmentListItem[]> {
  const response = await requestApi<ApiResponse<DepartmentListItem[]>>("/departments/all");
  if (!response.data) {
    throw new Error(response.message || "查询部门失败");
  }

  return response.data;
}

export async function createDepartment(request: DepartmentCreateRequest): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>("/departments", {
    method: "POST",
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "创建部门失败");
  }
}

export async function updateDepartment(id: string, request: DepartmentUpdateRequest): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/departments/${encodeURIComponent(id)}`, {
    method: "PUT",
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新部门失败");
  }
}

export async function deleteDepartment(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/departments/${encodeURIComponent(id)}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除部门失败");
  }
}

export async function getPositionsPaged(request: PagedRequest): Promise<PagedResult<PositionListItem>> {
  const response = await requestApi<ApiResponse<PagedResult<PositionListItem>>>(`/positions?${toQuery(request)}`);
  if (!response.data) {
    throw new Error(response.message || "查询职位失败");
  }

  return response.data;
}

export async function getPositionsAll(): Promise<PositionListItem[]> {
  const response = await requestApi<ApiResponse<PositionListItem[]>>("/positions/all");
  if (!response.data) {
    throw new Error(response.message || "查询职位失败");
  }

  return response.data;
}

export async function getPositionDetail(id: string): Promise<PositionDetail> {
  const response = await requestApi<ApiResponse<PositionDetail>>(`/positions/${encodeURIComponent(id)}`);
  if (!response.data) {
    throw new Error(response.message || "查询职位详情失败");
  }

  return response.data;
}

export async function createPosition(request: PositionCreateRequest): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>("/positions", {
    method: "POST",
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "创建职位失败");
  }
}

export async function updatePosition(id: string, request: PositionUpdateRequest): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/positions/${encodeURIComponent(id)}`, {
    method: "PUT",
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新职位失败");
  }
}

export async function deletePosition(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/positions/${encodeURIComponent(id)}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除职位失败");
  }
}

export async function getProfile(): Promise<UserProfileDetail> {
  return getProfileDetail();
}

export async function saveProfile(request: UserProfileUpdateRequest): Promise<void> {
  await updateProfile(request);
}

export async function savePassword(request: ChangePasswordRequest): Promise<void> {
  await changePassword(request);
}
