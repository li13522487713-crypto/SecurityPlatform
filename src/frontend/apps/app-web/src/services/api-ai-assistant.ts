import type { ApiResponse } from "@atlas/shared-core";
import { requestApi } from "./api-core";

export type AiAssistantFunctionType = "form" | "sql" | "workflow";

export interface AiAssistantGenerateResponse {
  result: string;
  explanation: string;
}

const endpointMap: Record<AiAssistantFunctionType, string> = {
  form: "/ai/generate-form",
  sql: "/ai/generate-sql",
  workflow: "/ai/suggest-workflow",
};

export async function generateByAiAssistant(
  type: AiAssistantFunctionType,
  description: string
): Promise<AiAssistantGenerateResponse | null> {
  const response = await requestApi<ApiResponse<AiAssistantGenerateResponse>>(endpointMap[type], {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ description }),
  });

  if (!response.success) {
    throw new Error(response.message || "Request failed");
  }

  return response.data ?? null;
}
