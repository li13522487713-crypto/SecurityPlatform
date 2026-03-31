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
