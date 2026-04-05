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
