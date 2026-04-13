import { requestApi } from "@/services/api-core";
import type { ApiResponse } from "@atlas/shared-react-core/types";

export interface WorkflowWorkbenchExecutionDto {
  executionId: string;
  status?: string;
  outputsJson?: string;
  errorMessage?: string;
}

export interface WorkflowWorkbenchTraceStepDto {
  nodeKey: string;
  status?: string;
  nodeType?: string;
  durationMs?: number;
  errorMessage?: string;
}

export interface WorkflowWorkbenchTraceDto {
  executionId: string;
  status?: string;
  startedAt?: string;
  completedAt?: string;
  durationMs?: number;
  steps: WorkflowWorkbenchTraceStepDto[];
}

export interface WorkflowWorkbenchExecuteResultDto {
  execution: WorkflowWorkbenchExecutionDto;
  trace?: WorkflowWorkbenchTraceDto;
}

export async function executeWorkflowTask(
  workflowId: string,
  request: {
    incident: string;
    source?: "draft" | "published";
  }
): Promise<WorkflowWorkbenchExecuteResultDto> {
  const response = await requestApi<ApiResponse<WorkflowWorkbenchExecuteResultDto>>(
    `/workflow-playground/${encodeURIComponent(workflowId)}/execute`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(request)
    }
  );

  if (!response.success || !response.data) {
    throw new Error(response.message || "Failed to execute workflow task");
  }

  return response.data;
}
