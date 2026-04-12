import type { PagedResult } from "@atlas/shared-react-core/types";
import type { WorkflowApiClient } from "@atlas/workflow-core-react";

export type WorkflowResourceMode = "workflow" | "chatflow";
export type WorkflowCreateSource = "blank" | "template" | "duplicate";
export type WorkflowStatusFilter = "all" | "draft" | "published";

export interface WorkflowTemplateSummary {
  id: string;
  title: string;
  description: string;
  mode: WorkflowResourceMode;
  createSource: WorkflowCreateSource;
  accentColor?: string;
  badge?: string;
}

export interface WorkflowListQuery {
  pageIndex?: number;
  pageSize?: number;
  keyword?: string;
  mode?: WorkflowResourceMode;
  status?: WorkflowStatusFilter;
}

export interface WorkflowListItem {
  id: string;
  name: string;
  description?: string;
  code?: string;
  updatedAt?: string;
  createdAt?: string;
  publishedAt?: string;
  mode?: 0 | 1;
  status?: 0 | 1 | 2;
  latestVersionNumber?: number;
}

export interface WorkflowCreateRequest {
  name: string;
  description?: string;
  mode: WorkflowResourceMode;
  createSource: WorkflowCreateSource;
  templateId?: string;
}

export interface WorkflowModuleApi {
  listWorkflows: (query?: WorkflowListQuery) => Promise<PagedResult<WorkflowListItem>>;
  listTemplates: (mode: WorkflowResourceMode) => Promise<WorkflowTemplateSummary[]>;
  createWorkflow: (request: WorkflowCreateRequest) => Promise<string>;
  duplicateWorkflow: (id: string) => Promise<string>;
  deleteWorkflow: (id: string) => Promise<void>;
  getVersions: (id: string) => Promise<Array<{ id: string; versionNumber: number; publishedAt?: string }>>;
  apiClient: WorkflowApiClient;
}

export interface WorkflowPageProps {
  api: WorkflowModuleApi;
  locale: "zh-CN" | "en-US";
}
