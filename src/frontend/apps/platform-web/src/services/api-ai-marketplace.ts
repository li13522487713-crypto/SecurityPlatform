import { requestApi, toQuery } from "@/services/api-core";
import type { ApiResponse, PagedRequest, PagedResult } from "@atlas/shared-core";

export type AiMarketplaceProductType = 1 | 2 | 3 | 4 | 5;
export type AiMarketplaceProductStatus = 0 | 1 | 2;

export interface AiProductCategoryItem {
  id: number;
  name: string;
  code: string;
  description?: string;
  sortOrder: number;
  isEnabled: boolean;
}

export interface AiMarketplaceProductListItem {
  id: number;
  categoryId: number;
  categoryName: string;
  name: string;
  summary?: string;
  icon?: string;
  productType: AiMarketplaceProductType;
  status: AiMarketplaceProductStatus;
  version: string;
  downloadCount: number;
  favoriteCount: number;
  isFavorited: boolean;
  publishedAt?: string;
  createdAt: string;
  updatedAt?: string;
}

export interface AiMarketplaceProductDetail extends AiMarketplaceProductListItem {
  description?: string;
  tags: string[];
  sourceResourceId?: number;
  publisherUserId: number;
}

export interface AiProductCategoryCreateRequest {
  name: string;
  code: string;
  description?: string;
  sortOrder: number;
}

export interface AiProductCategoryUpdateRequest extends AiProductCategoryCreateRequest {}

export interface AiMarketplaceProductCreateRequest {
  categoryId: number;
  name: string;
  summary?: string;
  description?: string;
  icon?: string;
  tags: string[];
  productType: AiMarketplaceProductType;
  sourceResourceId?: number;
}

export interface AiMarketplaceProductUpdateRequest extends AiMarketplaceProductCreateRequest {}

export interface AiMarketplaceProductPublishRequest {
  version: string;
}

export interface AiMarketplaceProductFilters {
  keyword?: string;
  categoryId?: number;
  productType?: AiMarketplaceProductType;
  status?: AiMarketplaceProductStatus;
}

export async function getAiMarketplaceCategories() {
  const response = await requestApi<ApiResponse<AiProductCategoryItem[]>>("/ai-marketplace/categories");
  if (!response.data) {
    throw new Error(response.message || "加载市场分类失败");
  }

  return response.data;
}

export async function createAiMarketplaceCategory(request: AiProductCategoryCreateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>("/ai-marketplace/categories", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success || !response.data) {
    throw new Error(response.message || "创建市场分类失败");
  }

  return Number(response.data.id);
}

export async function updateAiMarketplaceCategory(id: number, request: AiProductCategoryUpdateRequest) {
  const response = await requestApi<ApiResponse<object>>(`/ai-marketplace/categories/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新市场分类失败");
  }
}

export async function deleteAiMarketplaceCategory(id: number) {
  const response = await requestApi<ApiResponse<object>>(`/ai-marketplace/categories/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除市场分类失败");
  }
}

export async function getAiMarketplaceProductsPaged(request: PagedRequest, filters?: AiMarketplaceProductFilters) {
  const query = toQuery(request, {
    keyword: filters?.keyword,
    categoryId: filters?.categoryId !== undefined ? String(filters.categoryId) : undefined,
    productType: filters?.productType !== undefined ? String(filters.productType) : undefined,
    status: filters?.status !== undefined ? String(filters.status) : undefined
  });
  const response = await requestApi<ApiResponse<PagedResult<AiMarketplaceProductListItem>>>(`/ai-marketplace/products?${query}`);
  if (!response.data) {
    throw new Error(response.message || "加载市场商品失败");
  }

  return response.data;
}

export async function getAiMarketplaceProductById(id: number) {
  const response = await requestApi<ApiResponse<AiMarketplaceProductDetail>>(`/ai-marketplace/products/${id}`);
  if (!response.data) {
    throw new Error(response.message || "加载市场商品详情失败");
  }

  return response.data;
}

export async function createAiMarketplaceProduct(request: AiMarketplaceProductCreateRequest) {
  const response = await requestApi<ApiResponse<{ id: string }>>("/ai-marketplace/products", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success || !response.data) {
    throw new Error(response.message || "创建市场商品失败");
  }

  return Number(response.data.id);
}

export async function updateAiMarketplaceProduct(id: number, request: AiMarketplaceProductUpdateRequest) {
  const response = await requestApi<ApiResponse<object>>(`/ai-marketplace/products/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "更新市场商品失败");
  }
}

export async function deleteAiMarketplaceProduct(id: number) {
  const response = await requestApi<ApiResponse<object>>(`/ai-marketplace/products/${id}`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "删除市场商品失败");
  }
}

export async function publishAiMarketplaceProduct(id: number, request: AiMarketplaceProductPublishRequest) {
  const response = await requestApi<ApiResponse<object>>(`/ai-marketplace/products/${id}/publish`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.success) {
    throw new Error(response.message || "发布市场商品失败");
  }
}

export async function favoriteAiMarketplaceProduct(id: number) {
  const response = await requestApi<ApiResponse<object>>(`/ai-marketplace/products/${id}/favorite`, {
    method: "POST"
  });
  if (!response.success) {
    throw new Error(response.message || "收藏市场商品失败");
  }
}

export async function unfavoriteAiMarketplaceProduct(id: number) {
  const response = await requestApi<ApiResponse<object>>(`/ai-marketplace/products/${id}/favorite`, {
    method: "DELETE"
  });
  if (!response.success) {
    throw new Error(response.message || "取消收藏失败");
  }
}

export async function markAiMarketplaceProductDownloaded(id: number) {
  const response = await requestApi<ApiResponse<object>>(`/ai-marketplace/products/${id}/download`, {
    method: "POST"
  });
  if (!response.success) {
    throw new Error(response.message || "记录下载失败");
  }
}
