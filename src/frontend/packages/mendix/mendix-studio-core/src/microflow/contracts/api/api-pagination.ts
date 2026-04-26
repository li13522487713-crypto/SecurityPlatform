/**
 * 分页与排序的通用 Query 片段（与 `ListMicroflowsRequest` 组合使用）。
 * `pageIndex`：**1-based**，与 `MicroflowApiPageResult.pageIndex` 一致。
 */
export interface MicroflowPageQuery {
  pageIndex?: number;
  pageSize?: number;
}

/**
 * 排序。`sortBy` 的合法取值在 `docs/microflow/contracts/backend-api-contract.md` 中列明；
 * 资源列表推荐：`name` | `updatedAt` | `createdAt` | `version` | `referenceCount`。
 */
export interface MicroflowSortQuery {
  sortBy?: string;
  sortOrder?: "asc" | "desc";
}
