import type { ApiResponse } from "@atlas/shared-core";
import { requestApi, resolveAppHostPrefix } from "./api-core";

export type AiAssistantFunctionType = "form" | "sql" | "workflow";

export interface AiAssistantGenerateResponse {
  result: string;
  explanation: string;
}

const endpointMap: Record<AiAssistantFunctionType, string> = {
  form: "/api/v1/ai/generate-form",
  sql: "/api/v1/ai/generate-sql",
  workflow: "/api/v1/ai/suggest-workflow",
};

export async function generateByAiAssistant(
  appKey: string,
  type: AiAssistantFunctionType,
  description: string
): Promise<AiAssistantGenerateResponse | null> {
  const base = resolveAppHostPrefix(appKey);
  const response = await requestApi<ApiResponse<AiAssistantGenerateResponse>>(
    `${base}${endpointMap[type]}`,
    {
      method: "POST",
      body: JSON.stringify({ description }),
    }
  );
  if (!response.success) throw new Error(response.message || "Request failed");
  return response.data ?? null;
}
