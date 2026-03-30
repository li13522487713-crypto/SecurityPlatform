// 系统管理模块 API：应用配置、项目、数据源、表格视图
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
  TableViewConfig,
  TableViewListItem,
  TableViewDetail,
  TableViewCreateRequest,
  TableViewUpdateRequest,
  TableViewConfigUpdateRequest,
  TableViewDuplicateRequest,
  SqlQueryRequest,
  SqlQueryResult,
  DataSourceSchemaResult
} from "@/types/api";
import { requestApi, toQuery } from "@/services/api-core";

export async function getAppConfigsPaged(pagedRequest: PagedRequest) {
  const query = toQuery(pagedRequest);
  const response = await requestApi<ApiResponse<PagedResult<AppConfigListItem>>>(`/apps?${query}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }

  return response.data;
}

export async function getAppConfigDetail(id: string) {
  const response = await requestApi<ApiResponse<AppConfigDetail>>(`/apps/${id}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function updateAppConfig(id: string, request: AppConfigUpdateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/apps/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新失败");
  }
}

export async function getCurrentAppConfig(): Promise<AppConfigDetail | null> {
  const response = await requestApi<ApiResponse<AppConfigDetail>>("/apps/current");
  return response.data ?? null;
}

export async function getProjectsPaged(pagedRequest: PagedRequest) {
  const query = new URLSearchParams({
    PageIndex: String(pagedRequest.pageIndex),
    PageSize: String(pagedRequest.pageSize),
    Keyword: pagedRequest.keyword ?? "",
    SortBy: pagedRequest.sortBy ?? "",
    SortDesc: String(Boolean(pagedRequest.sortDesc))
  });
  const response = await requestApi<ApiResponse<PagedResult<ProjectListItem>>>(`/projects?${query.toString()}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function getProjectDetail(id: string) {
  const response = await requestApi<ApiResponse<ProjectDetail>>(`/projects/${id}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function createProject(request: ProjectCreateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>("/projects", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "创建失败");
  }
}

export async function updateProject(id: string, request: ProjectUpdateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/projects/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新失败");
  }
}

export async function deleteProject(id: string) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/projects/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除失败");
  }
}

export async function updateProjectUsers(id: string, request: ProjectAssignUsersRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/projects/${id}/users`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新项目人员失败");
  }
}

export async function updateProjectDepartments(id: string, request: ProjectAssignDepartmentsRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/projects/${id}/departments`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新项目部门失败");
  }
}

export async function updateProjectPositions(id: string, request: ProjectAssignPositionsRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/projects/${id}/positions`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新项目岗位失败");
  }
}

export async function getMyProjects() {
  const response = await requestApi<ApiResponse<ProjectListItem[]>>("/projects/my");
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function getMyProjectsPaged(pagedRequest: PagedRequest) {
  const query = new URLSearchParams({
    PageIndex: String(pagedRequest.pageIndex),
    PageSize: String(pagedRequest.pageSize),
    Keyword: pagedRequest.keyword ?? "",
    SortBy: pagedRequest.sortBy ?? "",
    SortDesc: String(Boolean(pagedRequest.sortDesc))
  });
  const response = await requestApi<ApiResponse<PagedResult<ProjectListItem>>>(`/projects/my/paged?${query.toString()}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function getTenantDataSources(): Promise<TenantDataSourceDto[]> {
  const response = await requestApi<ApiResponse<TenantDataSourceDto[]>>("/tenant-datasources");
  if (!response.data) {
    throw new Error(response.message || "查询数据源失败");
  }
  return response.data;
}

export async function getTenantDataSourceDrivers(): Promise<DataSourceDriverDefinition[]> {
  const response = await requestApi<ApiResponse<DataSourceDriverDefinition[]>>("/tenant-datasources/drivers");
  if (!response.data) {
    throw new Error(response.message || "查询数据源驱动失败");
  }
  return response.data;
}

export async function createTenantDataSource(request: TenantDataSourceCreateRequest): Promise<{ id: string }> {
  const response = await requestApi<ApiResponse<{ id: string }>>("/tenant-datasources", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "创建数据源失败");
  }
  return response.data;
}

export async function updateTenantDataSource(
  id: string,
  request: TenantDataSourceUpdateRequest
): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/tenant-datasources/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新数据源失败");
  }
}

export async function deleteTenantDataSource(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/tenant-datasources/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除数据源失败");
  }
}

export async function testTenantDataSourceConnection(
  request: TenantDataSourceTestConnectionRequest
): Promise<TenantDataSourceTestConnectionResult> {
  const response = await requestApi<ApiResponse<TenantDataSourceTestConnectionResult>>("/tenant-datasources/test", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "连接测试失败");
  }
  return response.data;
}

export async function testTenantDataSourceConnectionById(id: string): Promise<TenantDataSourceTestConnectionResult> {
  const response = await requestApi<ApiResponse<TenantDataSourceTestConnectionResult>>(`/tenant-datasources/${id}/test`, {
    method: "POST"
  });
  if (!response.data) {
    throw new Error(response.message || "连接测试失败");
  }
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
  if (!response.data) {
    throw new Error(response.message || "执行查询失败");
  }
  return response.data;
}

export async function getTenantDataSourceSchema(
  id: string
): Promise<DataSourceSchemaResult> {
  const response = await requestApi<ApiResponse<DataSourceSchemaResult>>(`/tenant-datasources/${id}/schema`);
  if (!response.data) {
    throw new Error(response.message || "获取表结构失败");
  }
  return response.data;
}

// ---------- Table Views (Personal) ----------
export async function getTableViewsPaged(tableKey: string, pagedRequest: PagedRequest) {
  const queryParams = new URLSearchParams(toQuery(pagedRequest));
  queryParams.append("tableKey", tableKey);
  const response = await requestApi<ApiResponse<PagedResult<TableViewListItem>>>(`/table-views?${queryParams}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function getDefaultTableView(tableKey: string): Promise<TableViewDetail | null> {
  const response = await requestApi<ApiResponse<TableViewDetail | null>>(
    `/table-views/default?tableKey=${encodeURIComponent(tableKey)}`
  );
  return response.data ?? null;
}

export async function getDefaultTableViewConfig(tableKey: string): Promise<TableViewConfig | null> {
  const response = await requestApi<ApiResponse<TableViewConfig | null>>(
    `/table-views/default-config?tableKey=${encodeURIComponent(tableKey)}`
  );
  return response.data ?? null;
}

export async function getTableViewDetail(id: string): Promise<TableViewDetail> {
  const response = await requestApi<ApiResponse<TableViewDetail>>(`/table-views/${id}`);
  if (!response.data) {
    throw new Error(response.message || "查询失败");
  }
  return response.data;
}

export async function createTableView(request: TableViewCreateRequest): Promise<{ id: string }> {
  const response = await requestApi<ApiResponse<{ id: string }>>("/table-views", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "创建失败");
  }
  return response.data;
}

export async function updateTableView(id: string, request: TableViewUpdateRequest): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/table-views/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新失败");
  }
}

export async function updateTableViewConfig(id: string, request: TableViewConfigUpdateRequest): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/table-views/${id}/config`, {
    method: "PATCH",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新失败");
  }
}

export async function duplicateTableView(id: string, request: TableViewDuplicateRequest): Promise<{ id: string }> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/table-views/${id}/duplicate`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "复制失败");
  }
  return response.data;
}

export async function setDefaultTableView(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/table-views/${id}/set-default`, {
    method: "POST"
  });
  if (!response.success) {
    throw new Error(response.message || "设置失败");
  }
}

export async function deleteTableView(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(`/table-views/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除失败");
  }
}

// ---- 统一元数据关联查询 ----

export async function getEntityReferences(tableKey: string): Promise<import('@/types/api').EntityReferenceResult> {
  const response = await requestApi<ApiResponse<import('@/types/api').EntityReferenceResult>>(
    `/metadata/entities/${encodeURIComponent(tableKey)}/references`
  );
  if (!response.data) {
    throw new Error(response.message || "查询元数据引用失败");
  }
  return response.data;
}
