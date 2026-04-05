import type { DynamicFieldType } from "@/types/dynamic-tables";

export interface DataFlowNode {
  id: string;
  type: string;
  label?: string;
}

export interface DataFlowEdge {
  id: string;
  sourceNodeId: string;
  targetNodeId: string;
}

export interface OutputFieldMapping {
  targetFieldKey: string;
  targetLabel: string;
  targetType: DynamicFieldType;
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
}

export interface DynamicViewHistoryItem {
  version: number;
  status: string;
  createdAt: string;
  checksum: string;
  comment?: string | null;
}

export interface DynamicViewSqlPreviewResult {
  sql: string;
  warnings: string[];
  fullyPushdown: boolean;
}

export interface DynamicTransformJobDto {
  id: string;
  jobKey: string;
  name: string;
  status: string;
  enabled: boolean;
  cronExpression?: string | null;
  lastRunAt?: string | null;
  lastRunStatus?: string | null;
}
