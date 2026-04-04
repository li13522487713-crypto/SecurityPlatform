import type {
  ApiResponse,
  PagedRequest,
  PagedResult,
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
  DataSourceDriverDefinition,
  TenantDataSourceUpdateRequest,
  TenantDataSourceTestConnectionRequest,
  TenantDataSourceTestConnectionResult,
  SqlQueryRequest,
  SqlQueryResult,
  DataSourceSchemaResult,
} from "@atlas/shared-core";
import { requestApi, toQuery } from "./api-core";

export async function getAppConfigsPaged(pagedRequest: PagedRequest) {
  const query = toQuery(pagedRequest);
  const response = await requestApi<ApiResponse<PagedResult<AppConfigListItem>>>(`/apps?${query}`);
  if (!response.data) throw new Error(response.message || "查询失败");
  return response.data;
}

export async function getAppConfigDetail(id: string) {
  const response = await requestApi<ApiResponse<AppConfigDetail>>(`/apps/${id}`);
  if (!response.data) throw new Error(response.message || "查询失败");
  return response.data;
}

export async function updateAppConfig(id: string, request: AppConfigUpdateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/apps/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) throw new Error(response.message || "更新失败");
}

export async function getCurrentAppConfig(): Promise<AppConfigDetail | null> {
  const response = await requestApi<ApiResponse<AppConfigDetail>>("/apps/current");
  return response.data ?? null;
}

export async function getProjectsPaged(pagedRequest: PagedRequest) {
  const query = toQuery(pagedRequest);
  const response = await requestApi<ApiResponse<PagedResult<ProjectListItem>>>(`/projects?${query}`);
  if (!response.data) throw new Error(response.message || "查询失败");
  return response.data;
}

export async function getProjectDetail(id: string) {
  const response = await requestApi<ApiResponse<ProjectDetail>>(`/projects/${id}`);
  if (!response.data) throw new Error(response.message || "查询失败");
  return response.data;
}

export async function createProject(request: ProjectCreateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>("/projects", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) throw new Error(response.message || "创建失败");
}

export async function updateProject(id: string, request: ProjectUpdateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/projects/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) throw new Error(response.message || "更新失败");
}

export async function deleteProject(id: string) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/projects/${id}`, { method: "DELETE" });
  if (!response.success) throw new Error(response.message || "删除失败");
}

export async function updateProjectUsers(id: string, request: ProjectAssignUsersRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/projects/${id}/users`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) throw new Error(response.message || "更新项目人员失败");
}

export async function updateProjectDepartments(id: string, request: ProjectAssignDepartmentsRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/projects/${id}/departments`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) throw new Error(response.message || "更新项目部门失败");
}

export async function updateProjectPositions(id: string, request: ProjectAssignPositionsRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/projects/${id}/positions`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) throw new Error(response.message || "更新项目岗位失败");
}

export async function getMyProjects() {
  const response = await requestApi<ApiResponse<ProjectListItem[]>>("/projects/my");
  if (!response.data) throw new Error(response.message || "查询失败");
  return response.data;
}

export async function getMyProjectsPaged(pagedRequest: PagedRequest) {
  const query = toQuery(pagedRequest);
  const response = await requestApi<ApiResponse<PagedResult<ProjectListItem>>>(`/projects/my/paged?${query}`);
  if (!response.data) throw new Error(response.message || "查询失败");
  return response.data;
}

export async function getTenantDataSources(): Promise<TenantDataSourceDto[]> {
  const response = await requestApi<ApiResponse<TenantDataSourceDto[]>>("/tenant-datasources");
  if (!response.data) throw new Error(response.message || "查询数据源失败");
  return response.data;
}

export async function getTenantDataSourceDrivers(): Promise<DataSourceDriverDefinition[]> {
  const response = await requestApi<ApiResponse<DataSourceDriverDefinition[]>>("/tenant-datasources/drivers");
  if (!response.data) throw new Error(response.message || "查询数据源驱动失败");
  return response.data;
}

export async function createTenantDataSource(request: TenantDataSourceCreateRequest): Promise<{ id: string }> {
  const response = await requestApi<ApiResponse<{ id: string }>>("/tenant-datasources", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) throw new Error(response.message || "创建数据源失败");
  return response.data;
}

export async function updateTenantDataSource(id: string, request: TenantDataSourceUpdateRequest): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/tenant-datasources/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) throw new Error(response.message || "更新数据源失败");
}

export async function deleteTenantDataSource(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/tenant-datasources/${id}`, { method: "DELETE" });
  if (!response.success) throw new Error(response.message || "删除数据源失败");
}

export async function testTenantDataSourceConnection(
  request: TenantDataSourceTestConnectionRequest
): Promise<TenantDataSourceTestConnectionResult> {
  const response = await requestApi<ApiResponse<TenantDataSourceTestConnectionResult>>("/tenant-datasources/test", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) throw new Error(response.message || "连接测试失败");
  return response.data;
}

export async function testTenantDataSourceConnectionById(id: string): Promise<TenantDataSourceTestConnectionResult> {
  const response = await requestApi<ApiResponse<TenantDataSourceTestConnectionResult>>(`/tenant-datasources/${id}/test`, {
    method: "POST"
  });
  if (!response.data) throw new Error(response.message || "连接测试失败");
  return response.data;
}

export async function previewTenantDataSourceQuery(
  id: string,
  request: SqlQueryRequest
): Promise<SqlQueryResult> {
  const response = await requestApi<ApiResponse<SqlQueryResult>>(`/tenant-datasources/${id}/query`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) throw new Error(response.message || "执行查询失败");
  return response.data;
}

export async function getTenantDataSourceSchema(id: string): Promise<DataSourceSchemaResult> {
  const response = await requestApi<ApiResponse<DataSourceSchemaResult>>(`/tenant-datasources/${id}/schema`);
  if (!response.data) throw new Error(response.message || "获取表结构失败");
  return response.data;
}
