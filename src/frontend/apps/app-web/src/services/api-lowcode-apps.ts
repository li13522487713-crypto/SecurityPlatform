import type { ApiResponse, PagedResult } from "@atlas/shared-react-core/types";
import { requestApi } from "./api-core";

/** 解析 ASP.NET Core ValidationProblemDetails 或后端自定义 errors 对象，拼接所有字段错误为可读字符串。 */
function _extractValidationErrors(body: unknown): string | null {
  if (!body || typeof body !== "object") return null;
  const obj = body as Record<string, unknown>;
  // ASP.NET Core ProblemDetails: { errors: { FieldName: ["msg1", "msg2"] } }
  if (obj["errors"] && typeof obj["errors"] === "object") {
    const errors = obj["errors"] as Record<string, string[]>;
    const messages = Object.values(errors).flat().filter(Boolean);
    if (messages.length > 0) return messages.join("; ");
  }
  // 后端自定义 ApiResponse: { message: "..." }
  if (typeof obj["message"] === "string" && obj["message"]) return obj["message"];
  return null;
}

export interface LowcodeAppListItemDto {
  id: string;
  code: string;
  displayName: string;
  description?: string;
  schemaVersion: string;
  targetTypes: string;
  defaultLocale: string;
  status: string;
  currentVersionId?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface LowcodeAppListQuery {
  keyword?: string;
  status?: string;
  pageIndex?: number;
  pageSize?: number;
  workspaceId?: string;
}

export interface LowcodeAppCreateRequest {
  code: string;
  displayName: string;
  description?: string;
  targetTypes: string;
  defaultLocale?: string;
  workspaceId?: string;
  theme?: {
    primaryColor?: string;
    borderRadius?: number;
    darkMode?: "never" | "always" | "auto";
    cssVariables?: Record<string, string>;
  } | null;
}

interface LowcodeAppCreateResponseData {
  id: string;
}

export async function getLowcodeAppsPaged(query?: LowcodeAppListQuery): Promise<PagedResult<LowcodeAppListItemDto>> {
  const params = new URLSearchParams({
    pageIndex: String(query?.pageIndex ?? 1),
    pageSize: String(query?.pageSize ?? 20)
  });

  if (query?.keyword) {
    params.set("keyword", query.keyword);
  }

  if (query?.status) {
    params.set("status", query.status);
  }

  if (query?.workspaceId) {
    params.set("workspaceId", query.workspaceId);
  }

  const response = await requestApi<ApiResponse<PagedResult<LowcodeAppListItemDto>>>(`/lowcode/apps?${params.toString()}`);
  if (!response.data) {
    throw new Error(response.message || "获取低代码应用列表失败");
  }
  return response.data;
}

export async function createLowcodeApp(request: LowcodeAppCreateRequest): Promise<string> {
  const response = await requestApi<ApiResponse<LowcodeAppCreateResponseData>>("/lowcode/apps", {
    method: "POST",
    body: JSON.stringify(request)
  });

  const appId = response.data?.id?.trim();
  if (!appId) {
    throw new Error(response.message || "创建低代码应用失败");
  }
  return appId;
}

export async function deleteLowcodeApp(appId: string): Promise<void> {
  const response = await requestApi<ApiResponse<object>>(`/lowcode/apps/${encodeURIComponent(appId)}`, {
    method: "DELETE"
  });

  if (!response.success) {
    throw new Error(response.message || "删除低代码应用失败");
  }
}
