import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-core";
import type {
  TenantAppMemberListItem,
  TenantAppRoleListItem,
  AppDepartmentListItem,
  AppPositionListItem,
  AppOrganizationCreateRoleRequest,
  AppOrganizationUpdateRoleRequest,
  AppOrganizationCreateDepartmentRequest,
  AppOrganizationUpdateDepartmentRequest,
  AppOrganizationCreatePositionRequest,
  AppOrganizationUpdatePositionRequest,
  AppOrganizationCreateMemberUserRequest,
  AppOrganizationUpdateMemberProfileRequest,
  AppOrganizationAssignMembersRequest,
  AppOrganizationUpdateMemberRolesRequest,
  AppOrganizationResetMemberPasswordRequest,
} from "@/types/organization";
import { requestApi, toQuery } from "./api-core";

const V2_BASE = "/api/v2/tenant-app-instances";

function orgBase(appId: string): string {
  return `${V2_BASE}/${encodeURIComponent(appId)}/organization`;
}

function rolesBase(appId: string): string {
  return `${V2_BASE}/${encodeURIComponent(appId)}/roles`;
}

function deptsBase(appId: string): string {
  return `${V2_BASE}/${encodeURIComponent(appId)}/departments`;
}

function positionsBase(appId: string): string {
  return `${V2_BASE}/${encodeURIComponent(appId)}/positions`;
}

function membersBase(appId: string): string {
  return `${V2_BASE}/${encodeURIComponent(appId)}/members`;
}

// ========== Members (Users) ==========

export interface MemberQueryRequest extends PagedRequest {
  roleId?: string;
}

export async function getMembersPaged(
  appId: string,
  params: MemberQueryRequest
): Promise<PagedResult<TenantAppMemberListItem>> {
  const extra: Record<string, string | undefined> = {};
  if (params.roleId) extra["roleId"] = params.roleId;
  if (params.departmentId != null) extra["departmentId"] = String(params.departmentId);
  const qs = toQuery(params, extra);
  const resp = await requestApi<ApiResponse<PagedResult<TenantAppMemberListItem>>>(
    `${membersBase(appId)}?${qs}`
  );
  if (!resp.data) throw new Error(resp.message ?? "Failed to query members");
  return resp.data;
}

export async function getMemberDetail(
  appId: string,
  userId: string
): Promise<TenantAppMemberListItem> {
  const resp = await requestApi<ApiResponse<TenantAppMemberListItem>>(
    `${membersBase(appId)}/${encodeURIComponent(userId)}`
  );
  if (!resp.data) throw new Error(resp.message ?? "Failed to query member detail");
  return resp.data;
}

export async function addMembers(
  appId: string,
  req: AppOrganizationAssignMembersRequest
): Promise<void> {
  await requestApi<ApiResponse<unknown>>(`${orgBase(appId)}/members`, {
    method: "POST",
    body: JSON.stringify(req)
  });
}

export async function createMemberUser(
  appId: string,
  req: AppOrganizationCreateMemberUserRequest
): Promise<void> {
  await requestApi<ApiResponse<unknown>>(`${orgBase(appId)}/members/users`, {
    method: "POST",
    body: JSON.stringify(req)
  });
}

export async function updateMemberRoles(
  appId: string,
  userId: string,
  req: AppOrganizationUpdateMemberRolesRequest
): Promise<void> {
  await requestApi<ApiResponse<unknown>>(
    `${orgBase(appId)}/members/${encodeURIComponent(userId)}/roles`,
    { method: "PUT", body: JSON.stringify(req) }
  );
}

export async function updateMemberProfile(
  appId: string,
  userId: string,
  req: AppOrganizationUpdateMemberProfileRequest
): Promise<void> {
  await requestApi<ApiResponse<unknown>>(
    `${orgBase(appId)}/members/${encodeURIComponent(userId)}/profile`,
    { method: "PUT", body: JSON.stringify(req) }
  );
}

export async function resetMemberPassword(
  appId: string,
  userId: string,
  req: AppOrganizationResetMemberPasswordRequest
): Promise<void> {
  await requestApi<ApiResponse<unknown>>(
    `${orgBase(appId)}/members/${encodeURIComponent(userId)}/reset-password`,
    { method: "POST", body: JSON.stringify(req) }
  );
}

export async function removeMember(appId: string, userId: string): Promise<void> {
  await requestApi<ApiResponse<unknown>>(
    `${orgBase(appId)}/members/${encodeURIComponent(userId)}`,
    { method: "DELETE" }
  );
}

// ========== Roles ==========

export interface RoleQueryRequest extends PagedRequest {
  isSystem?: boolean;
}

export async function getRolesPaged(
  appId: string,
  params: RoleQueryRequest
): Promise<PagedResult<TenantAppRoleListItem>> {
  const extra: Record<string, string | undefined> = {};
  if (typeof params.isSystem === "boolean") extra["isSystem"] = String(params.isSystem);
  const qs = toQuery(params, extra);
  const resp = await requestApi<ApiResponse<PagedResult<TenantAppRoleListItem>>>(
    `${rolesBase(appId)}?${qs}`
  );
  if (!resp.data) throw new Error(resp.message ?? "Failed to query roles");
  return resp.data;
}

export async function createRole(appId: string, req: AppOrganizationCreateRoleRequest): Promise<void> {
  await requestApi<ApiResponse<unknown>>(`${orgBase(appId)}/roles`, {
    method: "POST",
    body: JSON.stringify(req)
  });
}

export async function updateRole(appId: string, roleId: string, req: AppOrganizationUpdateRoleRequest): Promise<void> {
  await requestApi<ApiResponse<unknown>>(
    `${orgBase(appId)}/roles/${encodeURIComponent(roleId)}`,
    { method: "PUT", body: JSON.stringify(req) }
  );
}

export async function deleteRole(appId: string, roleId: string): Promise<void> {
  await requestApi<ApiResponse<unknown>>(
    `${orgBase(appId)}/roles/${encodeURIComponent(roleId)}`,
    { method: "DELETE" }
  );
}

// ========== Departments ==========

export async function getDepartmentsPaged(
  appId: string,
  params: PagedRequest
): Promise<PagedResult<AppDepartmentListItem>> {
  const qs = toQuery(params);
  const resp = await requestApi<ApiResponse<PagedResult<AppDepartmentListItem>>>(
    `${deptsBase(appId)}?${qs}`
  );
  if (!resp.data) throw new Error(resp.message ?? "Failed to query departments");
  return resp.data;
}

export async function getDepartmentsAll(appId: string): Promise<AppDepartmentListItem[]> {
  const resp = await requestApi<ApiResponse<AppDepartmentListItem[]>>(
    `${deptsBase(appId)}/all`
  );
  if (!resp.data) throw new Error(resp.message ?? "Failed to query departments");
  return resp.data;
}

export async function createDepartment(
  appId: string,
  req: AppOrganizationCreateDepartmentRequest
): Promise<void> {
  await requestApi<ApiResponse<unknown>>(`${orgBase(appId)}/departments`, {
    method: "POST",
    body: JSON.stringify(req)
  });
}

export async function updateDepartment(
  appId: string,
  id: string,
  req: AppOrganizationUpdateDepartmentRequest
): Promise<void> {
  await requestApi<ApiResponse<unknown>>(
    `${orgBase(appId)}/departments/${encodeURIComponent(id)}`,
    { method: "PUT", body: JSON.stringify(req) }
  );
}

export async function deleteDepartment(appId: string, id: string): Promise<void> {
  await requestApi<ApiResponse<unknown>>(
    `${orgBase(appId)}/departments/${encodeURIComponent(id)}`,
    { method: "DELETE" }
  );
}

// ========== Positions ==========

export async function getPositionsPaged(
  appId: string,
  params: PagedRequest
): Promise<PagedResult<AppPositionListItem>> {
  const qs = toQuery(params);
  const resp = await requestApi<ApiResponse<PagedResult<AppPositionListItem>>>(
    `${positionsBase(appId)}?${qs}`
  );
  if (!resp.data) throw new Error(resp.message ?? "Failed to query positions");
  return resp.data;
}

export async function getPositionDetail(
  appId: string,
  id: string
): Promise<AppPositionListItem> {
  const resp = await requestApi<ApiResponse<AppPositionListItem>>(
    `${positionsBase(appId)}/${encodeURIComponent(id)}`
  );
  if (!resp.data) throw new Error(resp.message ?? "Failed to query position detail");
  return resp.data;
}

export async function createPosition(
  appId: string,
  req: AppOrganizationCreatePositionRequest
): Promise<void> {
  await requestApi<ApiResponse<unknown>>(`${orgBase(appId)}/positions`, {
    method: "POST",
    body: JSON.stringify(req)
  });
}

export async function updatePosition(
  appId: string,
  id: string,
  req: AppOrganizationUpdatePositionRequest
): Promise<void> {
  await requestApi<ApiResponse<unknown>>(
    `${orgBase(appId)}/positions/${encodeURIComponent(id)}`,
    { method: "PUT", body: JSON.stringify(req) }
  );
}

export async function deletePosition(appId: string, id: string): Promise<void> {
  await requestApi<ApiResponse<unknown>>(
    `${orgBase(appId)}/positions/${encodeURIComponent(id)}`,
    { method: "DELETE" }
  );
}
