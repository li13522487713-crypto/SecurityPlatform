import type { ApiResponse } from "@atlas/shared-core";
import { requestApi } from "@/services/api-core";

export async function createTemplate(request: {
  name: string;
  category: number;
  schemaJson: string;
  description: string;
  tags: string;
  version: string;
}): Promise<{ id: string }> {
  const response = await requestApi<ApiResponse<{ id: string }>>("/templates", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  if (!response.data) {
    throw new Error(response.message || "保存模板失败");
  }
  return response.data;
}
