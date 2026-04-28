import type { ApiResponse } from "@atlas/shared-react-core/types";
import { extractResourceId, requestApi } from "./api-core";

export interface VoiceAssetCreateRequest {
  name: string;
  description?: string;
  language?: string;
  gender?: string;
  previewUrl?: string | null;
}

/**
 * 创建自定义音色，对应 `POST /api/v1/voice-assets`。
 */
export async function createVoiceAsset(request: VoiceAssetCreateRequest): Promise<number> {
  const response = await requestApi<ApiResponse<{ id?: string | number; Id?: string | number }>>("/voice-assets", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(request)
  });
  const id = extractResourceId(response.data);
  if (!response.success || !id) {
    throw new Error(response.message || "创建音色失败");
  }
  return Number(id);
}
