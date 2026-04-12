import type { PagedResult } from "@atlas/shared-core/types";
import type { WorkflowApiClient } from "@atlas/workflow-editor-react";

export interface WorkflowListItem {
  id: string;
  name: string;
  code?: string;
  updatedAt?: string;
}

export interface WorkflowModuleApi {
  listWorkflows: () => Promise<PagedResult<WorkflowListItem>>;
  createWorkflow: () => Promise<string>;
  apiClient: WorkflowApiClient;
}

export interface WorkflowPageProps {
  api: WorkflowModuleApi;
  locale: "zh-CN" | "en-US";
}
