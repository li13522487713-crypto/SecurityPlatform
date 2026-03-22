import type { ApiResponse, PagedRequest, PagedResult } from "@/types/api";
import type {
  FormDefinitionListItem,
  FormDefinitionDetail,
  FormDefinitionCreateRequest,
  FormDefinitionUpdateRequest,
  FormDefinitionVersionListItem,
  FormDefinitionVersionDetail,
  LowCodeAppListItem,
  LowCodeAppDetail,
  LowCodeAppVersionListItem,
  LowCodeAppCreateRequest,
  LowCodeAppUpdateRequest,
  LowCodeAppEntityAliasItem,
  LowCodeAppEntityAliasesUpdateRequest,
  LowCodeAppDataSourceInfo,
  LowCodeEnvironmentListItem,
  LowCodeEnvironmentDetail,
  LowCodeEnvironmentCreateRequest,
  LowCodeEnvironmentUpdateRequest,
  LowCodeAppExportPackage,
  LowCodeAppImportRequest,
  LowCodeAppImportResult,
  LowCodePageDetail,
  LowCodePageRuntimeSchema,
  LowCodePageVersionListItem,
  LowCodePageTreeNode,
  LowCodePageCreateRequest,
  LowCodePageUpdateRequest
} from "@/types/lowcode";
import { requestApi, requestApiBlob } from "@/services/api-core";

// ─── 表单定义 API ───

export async function getFormDefinitionsPaged(
  params: PagedRequest & { category?: string }
): Promise<PagedResult<FormDefinitionListItem>> {
  const query = new URLSearchParams({
    PageIndex: params.pageIndex.toString(),
    PageSize: params.pageSize.toString(),
    Keyword: params.keyword ?? ""
  });
  if (params.category) {
    query.set("category", params.category);
  }
  const response = await requestApi<ApiResponse<PagedResult<FormDefinitionListItem>>>(
    `/form-definitions?${query.toString()}`
  );
  if (!response.data) throw new Error(response.message || "查询失败");
  return response.data;
}

export async function getFormDefinitionDetail(id: string): Promise<FormDefinitionDetail> {
  const response = await requestApi<ApiResponse<FormDefinitionDetail>>(
    `/form-definitions/${id}`
  );
  if (!response.data) throw new Error(response.message || "查询失败");
  return response.data;
}

export async function createFormDefinition(
  request: FormDefinitionCreateRequest
): Promise<{ id: string }> {
  const response = await requestApi<ApiResponse<{ id: string }>>("/form-definitions", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) throw new Error(response.message || "创建失败");
  return response.data;
}

export async function updateFormDefinition(
  id: string,
  request: FormDefinitionUpdateRequest
): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/form-definitions/${id}`,
    {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.success) throw new Error(response.message || "更新失败");
}

export async function updateFormDefinitionSchema(
  id: string,
  schemaJson: string
): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/form-definitions/${id}/schema`,
    {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ schemaJson })
    }
  );
  if (!response.success) throw new Error(response.message || "保存 Schema 失败");
}

export async function publishFormDefinition(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/form-definitions/${id}/publish`,
    { method: "POST" }
  );
  if (!response.success) throw new Error(response.message || "发布失败");
}

export async function disableFormDefinition(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/form-definitions/${id}/disable`,
    { method: "POST" }
  );
  if (!response.success) throw new Error(response.message || "停用失败");
}

export async function enableFormDefinition(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/form-definitions/${id}/enable`,
    { method: "POST" }
  );
  if (!response.success) throw new Error(response.message || "启用失败");
}

export async function deleteFormDefinition(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/form-definitions/${id}`,
    { method: "DELETE" }
  );
  if (!response.success) throw new Error(response.message || "删除失败");
}

export async function getFormDefinitionVersions(id: string): Promise<FormDefinitionVersionListItem[]> {
  const response = await requestApi<ApiResponse<FormDefinitionVersionListItem[]>>(
    `/form-definitions/${id}/versions`
  );
  if (!response.data) throw new Error(response.message || "查询版本历史失败");
  return response.data;
}

export async function getFormDefinitionVersionDetail(id: string, versionId: string): Promise<FormDefinitionVersionDetail> {
  const response = await requestApi<ApiResponse<FormDefinitionVersionDetail>>(
    `/form-definitions/${id}/versions/${versionId}`
  );
  if (!response.data) throw new Error(response.message || "查询版本详情失败");
  return response.data;
}

export async function rollbackFormDefinitionVersion(id: string, versionId: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/form-definitions/${id}/rollback/${versionId}`,
    { method: "POST" }
  );
  if (!response.success) throw new Error(response.message || "回滚失败");
}

// ─── 低代码应用 API ───

export async function getLowCodeAppsPaged(
  params: PagedRequest & { category?: string }
): Promise<PagedResult<LowCodeAppListItem>> {
  const query = new URLSearchParams({
    PageIndex: params.pageIndex.toString(),
    PageSize: params.pageSize.toString(),
    Keyword: params.keyword ?? ""
  });
  if (params.category) {
    query.set("category", params.category);
  }
  const response = await requestApi<ApiResponse<PagedResult<LowCodeAppListItem>>>(
    `/lowcode-apps?${query.toString()}`
  );
  if (!response.data) throw new Error(response.message || "查询失败");
  return response.data;
}

export async function getLowCodeAppDetail(id: string): Promise<LowCodeAppDetail> {
  const response = await requestApi<ApiResponse<LowCodeAppDetail>>(
    `/lowcode-apps/${id}`
  );
  if (!response.data) throw new Error(response.message || "查询失败");
  return response.data;
}

export async function getLowCodePageDetail(pageId: string): Promise<LowCodePageDetail> {
  const response = await requestApi<ApiResponse<LowCodePageDetail>>(
    `/lowcode-apps/pages/${pageId}`
  );
  if (!response.data) throw new Error(response.message || "查询失败");
  return response.data;
}

export async function getLowCodeRuntimePageSchema(
  pageId: string,
  mode: "draft" | "published" = "draft",
  environmentCode?: string
): Promise<LowCodePageRuntimeSchema> {
  const query = new URLSearchParams({ mode });
  if (environmentCode) {
    query.set("environmentCode", environmentCode);
  }
  const response = await requestApi<ApiResponse<LowCodePageRuntimeSchema>>(
    `/lowcode-apps/pages/${pageId}/runtime?${query.toString()}`
  );
  if (!response.data) throw new Error(response.message || "查询失败");
  return response.data;
}

export async function getLowCodeRuntimePageSchemaByKey(
  appKey: string,
  pageKey: string,
  environmentCode?: string
): Promise<LowCodePageRuntimeSchema> {
  const query = new URLSearchParams();
  if (environmentCode) {
    query.set("environmentCode", environmentCode);
  }
  const queryText = query.toString();
  const response = await requestApi<ApiResponse<LowCodePageRuntimeSchema>>(
    `/runtime/apps/${encodeURIComponent(appKey)}/pages/${encodeURIComponent(pageKey)}/schema${
      queryText ? `?${queryText}` : ""
    }`
  );
  if (!response.data) throw new Error(response.message || "查询失败");
  return response.data;
}

export async function getLowCodePageVersions(pageId: string): Promise<LowCodePageVersionListItem[]> {
  const response = await requestApi<ApiResponse<LowCodePageVersionListItem[]>>(
    `/lowcode-apps/pages/${pageId}/versions`
  );
  if (!response.data) throw new Error(response.message || "查询失败");
  return response.data;
}

export async function getLowCodePageTree(appId: string): Promise<LowCodePageTreeNode[]> {
  const response = await requestApi<ApiResponse<LowCodePageTreeNode[]>>(
    `/lowcode-apps/${appId}/pages/tree`
  );
  if (!response.data) throw new Error(response.message || "查询失败");
  return response.data;
}

export async function getLowCodeAppByKey(appKey: string): Promise<LowCodeAppDetail> {
  const response = await requestApi<ApiResponse<LowCodeAppDetail>>(
    `/lowcode-apps/by-key/${encodeURIComponent(appKey)}`
  );
  if (!response.data) throw new Error(response.message || "查询失败");
  return response.data;
}

export async function createLowCodeApp(
  request: LowCodeAppCreateRequest
): Promise<{ id: string }> {
  const response = await requestApi<ApiResponse<{ id: string }>>("/lowcode-apps", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) throw new Error(response.message || "创建失败");
  return response.data;
}

export async function updateLowCodeApp(
  id: string,
  request: LowCodeAppUpdateRequest
): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/lowcode-apps/${id}`,
    {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.success) throw new Error(response.message || "更新失败");
}

export async function getLowCodeAppEntityAliases(id: string): Promise<LowCodeAppEntityAliasItem[]> {
  const response = await requestApi<ApiResponse<LowCodeAppEntityAliasItem[]>>(
    `/lowcode-apps/${id}/entity-aliases`
  );
  return response.data ?? [];
}

export async function updateLowCodeAppEntityAliases(
  id: string,
  request: LowCodeAppEntityAliasesUpdateRequest
): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/lowcode-apps/${id}/entity-aliases`,
    {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.success) throw new Error(response.message || "更新实体别名失败");
}

export async function getLowCodeAppDataSourceInfo(id: string): Promise<LowCodeAppDataSourceInfo | null> {
  const response = await requestApi<ApiResponse<LowCodeAppDataSourceInfo | null>>(
    `/lowcode-apps/${id}/datasource`
  );
  return response.data ?? null;
}

export async function testLowCodeAppDataSource(id: string): Promise<{ success: boolean; errorMessage?: string | null }> {
  const response = await requestApi<ApiResponse<{ success: boolean; errorMessage?: string | null }>>(
    `/lowcode-apps/${id}/datasource/test`,
    { method: "POST" }
  );
  if (!response.data) {
    throw new Error(response.message || "测试数据源失败");
  }
  return response.data;
}

export async function publishLowCodeApp(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/lowcode-apps/${id}/publish`,
    { method: "POST" }
  );
  if (!response.success) throw new Error(response.message || "发布失败");
}

export async function disableLowCodeApp(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(
    `/lowcode-apps/${id}/disable`,
    { method: "POST" }
  );
  if (!response.success) throw new Error(response.message || "停用失败");
}

export async function enableLowCodeApp(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(
    `/lowcode-apps/${id}/enable`,
    { method: "POST" }
  );
  if (!response.success) throw new Error(response.message || "启用失败");
}

export async function archiveLowCodeApp(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(
    `/lowcode-apps/${id}/archive`,
    { method: "POST" }
  );
  if (!response.success) throw new Error(response.message || "归档失败");
}

export async function updateLowCodeAppMenuConfig(id: string, menuConfigJson: string): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(
    `/lowcode-apps/${id}/menu-config`,
    {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ menuConfigJson })
    }
  );
  if (!response.success) throw new Error(response.message || "保存菜单配置失败");
}

export async function getLowCodeAppVersionsPaged(
  appId: string,
  params: PagedRequest
): Promise<PagedResult<LowCodeAppVersionListItem>> {
  const query = new URLSearchParams({
    PageIndex: params.pageIndex.toString(),
    PageSize: params.pageSize.toString()
  });
  if (params.keyword) {
    query.set("Keyword", params.keyword);
  }
  const response = await requestApi<ApiResponse<PagedResult<LowCodeAppVersionListItem>>>(
    `/lowcode-apps/${appId}/versions?${query.toString()}`
  );
  if (!response.data) throw new Error(response.message || "查询版本失败");
  return response.data;
}

export async function rollbackLowCodeAppVersion(appId: string, versionId: string): Promise<number> {
  const response = await requestApi<ApiResponse<{ id: string; version: number }>>(
    `/lowcode-apps/${appId}/versions/${versionId}/rollback`,
    { method: "POST" }
  );
  if (!response.success || !response.data) throw new Error(response.message || "回滚失败");
  return response.data.version;
}

export async function deleteLowCodeApp(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/lowcode-apps/${id}`,
    { method: "DELETE" }
  );
  if (!response.success) throw new Error(response.message || "删除失败");
}

export async function exportLowCodeApp(id: string): Promise<Blob> {
  return requestApiBlob(`/lowcode-apps/${id}/export`);
}

export async function importLowCodeApp(request: LowCodeAppImportRequest): Promise<LowCodeAppImportResult> {
  const response = await requestApi<ApiResponse<LowCodeAppImportResult>>("/lowcode-apps/import", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) throw new Error(response.message || "导入失败");
  return response.data;
}

export function parseLowCodeAppExportPackage(raw: string): LowCodeAppExportPackage {
  const parsed = JSON.parse(raw) as unknown;
  if (typeof parsed !== "object" || parsed === null) {
    throw new Error("导入文件格式不正确");
  }
  const pkg = parsed as Partial<LowCodeAppExportPackage>;
  if (!pkg.appKey || !pkg.name || !Array.isArray(pkg.pages) || !Array.isArray(pkg.pageVersions)) {
    throw new Error("导入文件缺少必要字段");
  }
  return pkg as LowCodeAppExportPackage;
}

// ─── 低代码页面 API ───

export async function createLowCodePage(
  appId: string,
  request: LowCodePageCreateRequest
): Promise<{ id: string }> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/lowcode-apps/${appId}/pages`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.data) throw new Error(response.message || "创建失败");
  return response.data;
}

export async function updateLowCodePage(
  pageId: string,
  request: LowCodePageUpdateRequest
): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/lowcode-apps/pages/${pageId}`,
    {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.success) throw new Error(response.message || "更新失败");
}

export async function updateLowCodePageSchema(
  pageId: string,
  schemaJson: string
): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/lowcode-apps/pages/${pageId}/schema`,
    {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ schemaJson })
    }
  );
  if (!response.success) throw new Error(response.message || "保存 Schema 失败");
}

export async function publishLowCodePage(pageId: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/lowcode-apps/pages/${pageId}/publish`,
    { method: "POST" }
  );
  if (!response.success) throw new Error(response.message || "发布失败");
}

export async function rollbackLowCodePage(pageId: string, versionId: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string; versionId: string }>>(
    `/lowcode-apps/pages/${pageId}/rollback/${versionId}`,
    { method: "POST" }
  );
  if (!response.success) throw new Error(response.message || "回滚失败");
}

export async function getLowCodeEnvironments(appId: string): Promise<LowCodeEnvironmentListItem[]> {
  const response = await requestApi<ApiResponse<LowCodeEnvironmentListItem[]>>(
    `/lowcode-apps/${appId}/environments`
  );
  if (!response.data) throw new Error(response.message || "查询失败");
  return response.data;
}

export async function getLowCodeEnvironmentDetail(id: string): Promise<LowCodeEnvironmentDetail> {
  const response = await requestApi<ApiResponse<LowCodeEnvironmentDetail>>(
    `/lowcode-apps/environments/${id}`
  );
  if (!response.data) throw new Error(response.message || "查询失败");
  return response.data;
}

export async function createLowCodeEnvironment(
  appId: string,
  request: LowCodeEnvironmentCreateRequest
): Promise<{ id: string }> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/lowcode-apps/${appId}/environments`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.data) throw new Error(response.message || "创建失败");
  return response.data;
}

export async function updateLowCodeEnvironment(
  id: string,
  request: LowCodeEnvironmentUpdateRequest
): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/lowcode-apps/environments/${id}`,
    {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );
  if (!response.success) throw new Error(response.message || "更新失败");
}

export async function deleteLowCodeEnvironment(id: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/lowcode-apps/environments/${id}`,
    { method: "DELETE" }
  );
  if (!response.success) throw new Error(response.message || "删除失败");
}

export async function deleteLowCodePage(pageId: string): Promise<void> {
  const response = await requestApi<ApiResponse<{ id: string }>>(
    `/lowcode-apps/pages/${pageId}`,
    { method: "DELETE" }
  );
  if (!response.success) throw new Error(response.message || "删除失败");
}

export interface MicroflowStep {
  type: "api_call" | "condition" | "set_variable" | "notification";
  name?: string;
  config: Record<string, string>;
}

export interface MicroflowExecutionResult {
  success: boolean;
  stepsExecuted: number;
  outputData?: Record<string, unknown>;
}

export async function executeMicroflow(
  steps: MicroflowStep[],
  inputData?: Record<string, unknown>
): Promise<MicroflowExecutionResult> {
  const microflowJson = JSON.stringify({ steps });
  const response = await requestApi<ApiResponse<MicroflowExecutionResult>>(
    "/lowcode-actions/execute-microflow",
    {
      method: "POST",
      body: JSON.stringify({ microflowJson, inputData }),
    }
  );
  if (!response.success || !response.data) {
    throw new Error(response.message || "Microflow execution failed");
  }
  return response.data;
}
