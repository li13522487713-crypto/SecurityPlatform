import type { DynamicFieldType, DynamicRecordListResult, DynamicRecordQueryRequest } from "@/types/dynamic-tables";

export type DataFlowNodeType =
  | "sourceTable"
  | "sourceView"
  | "join"
  | "select"
  | "filter"
  | "compute"
  | "cast"
  | "lookup"
  | "aggregate"
  | "union"
  | "sort"
  | "limit"
  | "outputView";

export interface DataFlowNode {
  id: string;
  type: DataFlowNodeType;
  label?: string;
  tableKey?: string;
  viewKey?: string;
  config?: Record<string, unknown>;
}

export interface DataFlowEdge {
  id: string;
  sourceNodeId: string;
  sourcePortId?: string;
  targetNodeId: string;
  targetPortId?: string;
}

export interface TransformOp {
  type: "trim" | "upper" | "lower" | "replace" | "concat" | "cast" | "expr" | "if" | "lookup";
  args?: Record<string, unknown>;
}

export interface OutputFieldMapping {
  targetFieldKey: string;
  targetLabel: string;
  targetType: DynamicFieldType;
  nullable?: boolean;
  source?: {
    nodeId: string;
    fieldKey: string;
  };
  pipeline: TransformOp[];
  onError?: "null" | "default" | "reject_row";
}

export interface DataViewDefinition {
  id?: string;
  appId: string;
  viewKey: string;
  name: string;
  description?: string;
  nodes: DataFlowNode[];
  edges: DataFlowEdge[];
  outputFields: OutputFieldMapping[];
  filters?: Array<{ field: string; operator: string; value?: unknown }>;
  groupBy?: string[];
  sorts?: Array<{ field: string; direction: "asc" | "desc" }>;
}

export interface DynamicViewHistoryItem {
  version: number;
  status: string;
  createdBy: number;
  createdAt: string;
  comment?: string | null;
  checksum: string;
}

export interface DynamicViewPublishResult {
  viewKey: string;
  version: number;
  publishedAt: string;
  checksum: string;
}

export interface DynamicViewSqlPreviewResult {
  sql: string;
  parameters: Array<{ name: string; value: unknown }>;
  warnings: string[];
  fullyPushdown: boolean;
}

export interface DeleteCheckBlocker {
  type: "form" | "page" | "approval" | "relation" | "view" | "etlJob";
  id: string;
  name: string;
  path?: string;
}

export interface DeleteCheckResult {
  canDelete: boolean;
  blockers: DeleteCheckBlocker[];
  warnings: string[];
}

export interface DynamicViewListItem {
  id: string;
  appId?: string | null;
  viewKey: string;
  name: string;
  description?: string | null;
  isPublished: boolean;
  publishedVersion: number;
  createdAt: string;
  updatedAt: string;
  createdBy: number;
  updatedBy: number;
}

export interface DynamicViewQueryRequest extends DynamicRecordQueryRequest {
  keyword?: string | null;
}

export interface DynamicViewPreviewRequest {
  definition: DataViewDefinition;
  limit?: number;
}

export type DynamicViewQueryResult = DynamicRecordListResult;

export interface DynamicTransformJobDto {
  id: string;
  appId?: string | null;
  jobKey: string;
  name: string;
  status: string;
  cronExpression?: string | null;
  enabled: boolean;
  lastRunAt?: string | null;
  lastRunStatus?: string | null;
  lastError?: string | null;
  sourceConfigJson: string;
  targetConfigJson: string;
  definitionJson: string;
  createdAt: string;
  updatedAt: string;
}

export interface DynamicTransformExecutionDto {
  id: string;
  jobKey: string;
  status: string;
  triggerType: "manual" | "schedule" | string;
  inputRows: number;
  outputRows: number;
  failedRows: number;
  durationMs: number;
  errorDetailJson?: string | null;
  startedBy: number;
  startedAt: string;
  endedAt?: string | null;
  message?: string | null;
}

export interface DynamicTransformJobUpdateRequest {
  name: string;
  definitionJson: string;
  cronExpression?: string | null;
  enabled: boolean;
  sourceConfigJson?: string;
  targetConfigJson?: string;
}

export interface DynamicExternalExtractPreviewResult {
  success: boolean;
  errorMessage?: string | null;
  columns: DynamicExternalExtractColumn[];
  rows: Array<Record<string, unknown>>;
}

export interface DynamicExternalExtractColumn {
  name: string;
  type: string;
}

export interface DynamicExternalExtractDataSource {
  id: string;
  name: string;
  dbType: string;
}

export interface DynamicExternalExtractSchemaTable {
  name: string;
  columns: DynamicExternalExtractColumn[];
}

export interface DynamicExternalExtractSchemaResult {
  dataSourceId: string;
  tables: DynamicExternalExtractSchemaTable[];
}

export interface DynamicPhysicalViewPublishResult {
  viewKey: string;
  publicationId: string;
  version: number;
  physicalViewName: string;
  dataSourceId?: number | null;
  status: string;
  publishedAt: string;
  success: boolean;
  message: string;
}

export interface DynamicPhysicalViewPublication {
  id: string;
  viewKey: string;
  version: number;
  physicalViewName: string;
  status: string;
  comment?: string | null;
  dataSourceId?: number | null;
  publishedBy: number;
  publishedAt: string;
}
