import type { ApiResponse, PagedRequest } from "@atlas/shared-core";
import type {
  AppOrganizationWorkspaceResponse,
  AppOrganizationAssignMembersRequest,
  AppOrganizationCreateMemberUserRequest,
  AppOrganizationUpdateMemberRolesRequest,
  AppOrganizationResetMemberPasswordRequest,
  AppOrganizationUpdateMemberProfileRequest,
  AppOrganizationCreateRoleRequest,
  AppOrganizationUpdateRoleRequest,
  AppOrganizationCreateDepartmentRequest,
  AppOrganizationUpdateDepartmentRequest,
  AppOrganizationCreatePositionRequest,
  AppOrganizationUpdatePositionRequest,
  AppOrganizationCreateProjectRequest,
  AppOrganizationUpdateProjectRequest
} from "@/types/organization";
import { requestApi, toQuery } from "./api-core";

const V2_BASE = "/api/v2/tenant-app-instances";

function orgBase(appId: string): string {
  return `${V2_BASE}/${encodeURIComponent(appId)}/organization`;
}

function rolesV2Base(appId: string): string {
  return `${V2_BASE}/${encodeURIComponent(appId)}/roles`;
}

export async function getOrganizationWorkspace(
  appId: string,
  params: PagedRequest,
  roleId?: string
): Promise<AppOrganizationWorkspaceResponse> {
  const extra: Record<string, string | undefined> = {};
  if (roleId) extra["roleId"] = roleId;
  const qs = toQuery(params, extra);
  const resp = await requestApi<ApiResponse<AppOrganizationWorkspaceResponse>>(
    `${orgBase(appId)}/workspace?${qs}`
  );
  if (!resp.data) throw new Error(resp.message ?? "Failed to load workspace");
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

export async function removeMember(appId: string, userId: string): Promise<void> {
  await requestApi<ApiResponse<unknown>>(
    `${orgBase(appId)}/members/${encodeURIComponent(userId)}`,
    { method: "DELETE" }
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

export async function createRole(
  appId: string,
  req: AppOrganizationCreateRoleRequest
): Promise<void> {
  await requestApi<ApiResponse<unknown>>(`${orgBase(appId)}/roles`, {
    method: "POST",
    body: JSON.stringify(req)
  });
}

export async function updateRole(
  appId: string,
  roleId: string,
  req: AppOrganizationUpdateRoleRequest
): Promise<void> {
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

export async function createProject(
  appId: string,
  req: AppOrganizationCreateProjectRequest
): Promise<void> {
  await requestApi<ApiResponse<unknown>>(`${orgBase(appId)}/projects`, {
    method: "POST",
    body: JSON.stringify(req)
  });
}

export async function updateProject(
  appId: string,
  id: string,
  req: AppOrganizationUpdateProjectRequest
): Promise<void> {
  await requestApi<ApiResponse<unknown>>(
    `${orgBase(appId)}/projects/${encodeURIComponent(id)}`,
    { method: "PUT", body: JSON.stringify(req) }
  );
}

export async function deleteProject(appId: string, id: string): Promise<void> {
  await requestApi<ApiResponse<unknown>>(
    `${orgBase(appId)}/projects/${encodeURIComponent(id)}`,
    { method: "DELETE" }
  );
}

// ===== Permissions =====

export interface PermissionListItem {
  id: string;
  name: string;
  code: string;
  type: string;
  description: string | null;
}

export interface RoleDetailWithPermissions {
  id: string;
  code: string;
  name: string;
  description: string | null;
  isSystem: boolean;
  permissionCodes: string[];
}

export async function getAppPermissions(appId: string): Promise<PermissionListItem[]> {
  const qs = new URLSearchParams({ PageIndex: "1", PageSize: "500" });
  const resp = await requestApi<ApiResponse<{ items: PermissionListItem[] }>>(
    `${V2_BASE}/${encodeURIComponent(appId)}/permissions?${qs.toString()}`
  );
  return resp.data?.items ?? [];
}

export async function getRoleDetail(appId: string, roleId: string): Promise<RoleDetailWithPermissions> {
  const resp = await requestApi<ApiResponse<RoleDetailWithPermissions>>(
    `${V2_BASE}/${encodeURIComponent(appId)}/roles/${encodeURIComponent(roleId)}`
  );
  if (!resp.data) throw new Error(resp.message ?? "Failed to fetch role detail");
  return resp.data;
}

export async function updateRolePermissions(appId: string, roleId: string, permissionCodes: string[]): Promise<void> {
  await requestApi<ApiResponse<unknown>>(
    `${V2_BASE}/${encodeURIComponent(appId)}/roles/${encodeURIComponent(roleId)}/permissions`,
    { method: "PUT", body: JSON.stringify({ permissionCodes }) }
  );
}

// ===== App Roles: Data Scope / Pages / Field Permissions (TenantAppRolesV2Controller) =====

export interface AppRoleAssignmentDetail {
  roleId: string;
  roleCode: string;
  roleName: string;
  dataScope: number;
  deptIds: string[];
}

export interface AppPageListItem {
  id: string;
  pageKey: string;
  name: string;
  description: string | null;
  routePath: string | null;
  parentPageId: number | null;
  sortOrder: number;
  isPublished: boolean;
}

export interface AppRoleFieldPermissionItemDto {
  fieldName: string;
  canView: boolean;
  canEdit: boolean;
}

export interface AppRoleFieldPermissionGroupDto {
  tableKey: string;
  fields: AppRoleFieldPermissionItemDto[];
}

export interface AppAvailableDynamicTableItem {
  tableKey: string;
  displayName: string;
}

export interface AppDynamicFieldItem {
  name: string;
  displayName: string | null;
}

export async function getAppRoleDataScope(appId: string, roleId: string): Promise<AppRoleAssignmentDetail> {
  const resp = await requestApi<ApiResponse<AppRoleAssignmentDetail>>(
    `${rolesV2Base(appId)}/${encodeURIComponent(roleId)}/data-scope`
  );
  if (!resp.data) throw new Error(resp.message ?? "Failed to load data scope");
  return resp.data;
}

export async function updateAppRoleDataScope(
  appId: string,
  roleId: string,
  body: { dataScope: number; deptIds?: number[] }
): Promise<void> {
  await requestApi<ApiResponse<unknown>>(
    `${rolesV2Base(appId)}/${encodeURIComponent(roleId)}/data-scope`,
    { method: "PUT", body: JSON.stringify(body) }
  );
}

export async function getAvailableAppPages(appId: string): Promise<AppPageListItem[]> {
  const resp = await requestApi<ApiResponse<AppPageListItem[]>>(
    `${rolesV2Base(appId)}/available-pages`
  );
  return resp.data ?? [];
}

export async function getRolePageIds(appId: string, roleId: string): Promise<number[]> {
  const resp = await requestApi<ApiResponse<number[]>>(
    `${rolesV2Base(appId)}/${encodeURIComponent(roleId)}/pages`
  );
  return resp.data ?? [];
}

export async function updateRolePages(appId: string, roleId: string, pageIds: number[]): Promise<void> {
  await requestApi<ApiResponse<unknown>>(
    `${rolesV2Base(appId)}/${encodeURIComponent(roleId)}/pages`,
    { method: "PUT", body: JSON.stringify({ pageIds }) }
  );
}

export async function getAvailableDynamicTables(
  appId: string,
  keyword?: string
): Promise<AppAvailableDynamicTableItem[]> {
  const qs = keyword?.trim() ? `?keyword=${encodeURIComponent(keyword.trim())}` : "";
  const resp = await requestApi<ApiResponse<AppAvailableDynamicTableItem[]>>(
    `${rolesV2Base(appId)}/available-dynamic-tables${qs}`
  );
  return resp.data ?? [];
}

export async function getRoleFieldPermissions(
  appId: string,
  roleId: string
): Promise<AppRoleFieldPermissionGroupDto[]> {
  const resp = await requestApi<ApiResponse<AppRoleFieldPermissionGroupDto[]>>(
    `${rolesV2Base(appId)}/${encodeURIComponent(roleId)}/field-permissions`
  );
  return resp.data ?? [];
}

export async function updateRoleFieldPermissions(
  appId: string,
  roleId: string,
  groups: AppRoleFieldPermissionGroupDto[]
): Promise<void> {
  await requestApi<ApiResponse<unknown>>(
    `${rolesV2Base(appId)}/${encodeURIComponent(roleId)}/field-permissions`,
    { method: "PUT", body: JSON.stringify({ groups }) }
  );
}

/** Resolve dynamic table fields within app context (requires app JWT's app_id) */
export async function getAppDynamicTableFields(tableKey: string): Promise<AppDynamicFieldItem[]> {
  const resp = await requestApi<ApiResponse<AppDynamicFieldItem[]>>(
    `/dynamic-tables/${encodeURIComponent(tableKey)}/fields`
  );
  return resp.data ?? [];
}
