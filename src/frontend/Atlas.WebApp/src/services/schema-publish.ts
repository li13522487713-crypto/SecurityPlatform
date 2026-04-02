import type { ApiResponse, PagedRequest, PagedResult } from "@/types/api";
import type {
  SchemaPublishSnapshotListItem,
  SchemaPublishSnapshotDetail,
  SchemaPublishSnapshotCreateRequest,
  SchemaSnapshotDiffResult,
  SchemaCompatibilityCheckRequest,
  SchemaCompatibilityResult,
  DdlPreviewResult,
  DependencyGraphResult,
  SchemaImpactList,
} from "@/types/schema-publish";
import { requestApi } from "@/services/api-core";

const BASE = "/schema";

export async function getSnapshots(
  tableKey?: string,
  paging?: PagedRequest,
): Promise<ApiResponse<PagedResult<SchemaPublishSnapshotListItem>>> {
  const params = new URLSearchParams();
  if (tableKey) params.set("tableKey", tableKey);
  if (paging?.pageIndex) params.set("pageIndex", String(paging.pageIndex));
  if (paging?.pageSize) params.set("pageSize", String(paging.pageSize));
  return requestApi<ApiResponse<PagedResult<SchemaPublishSnapshotListItem>>>(
    `${BASE}/snapshots?${params.toString()}`,
  );
}

export async function getSnapshotById(
  snapshotId: number,
): Promise<ApiResponse<SchemaPublishSnapshotDetail>> {
  return requestApi<ApiResponse<SchemaPublishSnapshotDetail>>(
    `${BASE}/snapshots/${snapshotId}`,
  );
}

export async function getLatestSnapshot(
  tableKey: string,
): Promise<ApiResponse<SchemaPublishSnapshotDetail | null>> {
  return requestApi<ApiResponse<SchemaPublishSnapshotDetail | null>>(
    `${BASE}/snapshots/latest?tableKey=${encodeURIComponent(tableKey)}`,
  );
}

export async function diffSnapshots(
  tableKey: string,
  fromVersion: number,
  toVersion: number,
): Promise<ApiResponse<SchemaSnapshotDiffResult | null>> {
  return requestApi<ApiResponse<SchemaSnapshotDiffResult | null>>(
    `${BASE}/snapshots/diff?tableKey=${encodeURIComponent(tableKey)}&fromVersion=${fromVersion}&toVersion=${toVersion}`,
  );
}

export async function createSnapshot(
  request: SchemaPublishSnapshotCreateRequest,
): Promise<ApiResponse<{ id: string; tableKey: string }>> {
  return requestApi<ApiResponse<{ id: string; tableKey: string }>>(
    `${BASE}/snapshots`,
    { method: "POST", body: JSON.stringify(request) },
  );
}

export async function checkCompatibility(
  request: SchemaCompatibilityCheckRequest,
): Promise<ApiResponse<SchemaCompatibilityResult>> {
  return requestApi<ApiResponse<SchemaCompatibilityResult>>(
    `${BASE}/compatibility-check`,
    { method: "POST", body: JSON.stringify(request) },
  );
}

export async function previewDdl(
  request: SchemaCompatibilityCheckRequest,
): Promise<ApiResponse<DdlPreviewResult>> {
  return requestApi<ApiResponse<DdlPreviewResult>>(
    `${BASE}/ddl-preview`,
    { method: "POST", body: JSON.stringify(request) },
  );
}

export async function getDependencies(
  tableKey: string,
): Promise<ApiResponse<DependencyGraphResult>> {
  return requestApi<ApiResponse<DependencyGraphResult>>(
    `${BASE}/dependencies/${encodeURIComponent(tableKey)}`,
  );
}

export async function getSchemaImpact(
  tableKey: string,
  removingFields?: string[],
): Promise<ApiResponse<SchemaImpactList>> {
  const params = removingFields?.length
    ? `?removingFields=${removingFields.join(",")}`
    : "";
  return requestApi<ApiResponse<SchemaImpactList>>(
    `${BASE}/impact/${encodeURIComponent(tableKey)}${params}`,
  );
}
