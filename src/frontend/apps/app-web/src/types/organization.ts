import type { PagedResult } from "@atlas/shared-core";

export interface TenantAppMemberListItem {
  userId: string;
  username: string;
  displayName: string;
  email: string | null;
  phoneNumber: string | null;
  isActive: boolean;
  joinedAt: string;
  roleIds: string[];
  roleNames: string[];
  departmentIds: string[];
  departmentNames: string[];
  positionIds: string[];
  positionNames: string[];
  projectIds: string[];
  projectNames: string[];
}

export interface TenantAppRoleGovernanceItem {
  roleId: string;
  roleCode: string;
  roleName: string;
  isSystem: boolean;
  memberCount: number;
  permissionCount: number;
  hasPermissionCoverage: boolean;
}

export interface TenantAppRoleGovernanceOverview {
  appId: string;
  totalRoles: number;
  systemRoleCount: number;
  customRoleCount: number;
  totalMembers: number;
  coveredMembers: number;
  uncoveredMembers: number;
  permissionCoverageRate: number;
  roles: TenantAppRoleGovernanceItem[];
}

export interface TenantAppRoleListItem {
  id: string;
  code: string;
  name: string;
  description: string | null;
  isSystem: boolean;
  memberCount: number;
  permissionCodes: string[];
}

export interface AppDepartmentListItem {
  id: string;
  name: string;
  code: string;
  parentId: string | null;
  sortOrder: number;
}

export interface AppPositionListItem {
  id: string;
  name: string;
  code: string;
  description: string | null;
  isActive: boolean;
  sortOrder: number;
}

export interface AppPositionDetail {
  id: string;
  appId: string;
  name: string;
  code: string;
  description: string | null;
  isActive: boolean;
  sortOrder: number;
}

export interface TenantAppMemberDetail {
  userId: string;
  username: string;
  displayName: string;
  email: string | null;
  phoneNumber: string | null;
  isActive: boolean;
  joinedAt: string;
  roleIds: string[];
  roleNames: string[];
  departmentIds: string[];
  departmentNames: string[];
  positionIds: string[];
  positionNames: string[];
  projectIds: string[];
  projectNames: string[];
}

export interface TenantAppRoleDetail {
  id: string;
  code: string;
  name: string;
  description: string | null;
  isSystem: boolean;
  createdAt: string;
  updatedAt: string;
  memberCount: number;
  permissionCodes: string[];
}

export interface AppDepartmentDetail {
  id: string;
  appId: string;
  name: string;
  code: string;
  parentId: string | null;
  sortOrder: number;
  memberCount: number;
  managerName: string | null;
}

export interface AppProjectListItem {
  id: string;
  code: string;
  name: string;
  description: string | null;
  isActive: boolean;
}

export interface AppOrganizationWorkspaceResponse {
  appId: string;
  members: PagedResult<TenantAppMemberListItem>;
  roleGovernance: TenantAppRoleGovernanceOverview;
  roles: TenantAppRoleListItem[];
  departments: AppDepartmentListItem[];
  positions: AppPositionListItem[];
  projects: AppProjectListItem[];
}

export interface AppOrganizationAssignMembersRequest {
  userIds: string[];
  roleIds: string[];
  departmentIds?: string[];
  positionIds?: string[];
  projectIds?: string[];
}

export interface AppOrganizationCreateMemberUserRequest {
  username: string;
  password: string;
  displayName: string;
  email?: string;
  phoneNumber?: string;
  isActive: boolean;
  roleIds: string[];
  departmentIds?: string[];
  positionIds?: string[];
  projectIds?: string[];
}

export interface AppOrganizationUpdateMemberRolesRequest {
  roleIds: string[];
  departmentIds?: string[];
  positionIds?: string[];
  projectIds?: string[];
}

export interface AppOrganizationResetMemberPasswordRequest {
  newPassword: string;
}

export interface AppOrganizationUpdateMemberProfileRequest {
  displayName: string;
  email?: string;
  phoneNumber?: string;
  isActive: boolean;
}

export interface AppOrganizationCreateRoleRequest {
  code: string;
  name: string;
  description?: string;
  permissionCodes?: string[];
}

export interface AppOrganizationUpdateRoleRequest {
  name: string;
  description?: string;
}

export interface AppOrganizationCreateDepartmentRequest {
  name: string;
  code: string;
  parentId?: string;
  sortOrder: number;
}

export interface AppOrganizationUpdateDepartmentRequest {
  name: string;
  code: string;
  parentId?: string;
  sortOrder: number;
}

export interface AppOrganizationCreatePositionRequest {
  name: string;
  code: string;
  description?: string;
  isActive: boolean;
  sortOrder: number;
}

export interface AppOrganizationUpdatePositionRequest {
  name: string;
  description?: string;
  isActive: boolean;
  sortOrder: number;
}

export interface AppOrganizationCreateProjectRequest {
  code: string;
  name: string;
  description?: string;
  isActive: boolean;
}

export interface AppOrganizationUpdateProjectRequest {
  name: string;
  description?: string;
  isActive: boolean;
}
