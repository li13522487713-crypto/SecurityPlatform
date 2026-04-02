export interface SchemaPublishSnapshotListItem {
  id: number;
  tableKey: string;
  version: number;
  publishNote?: string | null;
  publishedBy: number;
  publishedAt: string;
  migrationTaskId?: number | null;
}

export interface SchemaPublishSnapshotDetail {
  id: number;
  tableId: number;
  tableKey: string;
  version: number;
  snapshotJson: string;
  publishNote?: string | null;
  publishedBy: number;
  publishedAt: string;
  migrationTaskId?: number | null;
}

export interface SchemaPublishSnapshotCreateRequest {
  tableKey: string;
  publishNote?: string | null;
}

export interface SchemaSnapshotDiffResult {
  fromVersion: number;
  toVersion: number;
  tableKey: string;
  fieldChanges: SchemaFieldDiff[];
  indexChanges: SchemaIndexDiff[];
}

export interface SchemaFieldDiff {
  fieldName: string;
  changeType: "Added" | "Modified" | "Removed";
  oldDefinition?: string | null;
  newDefinition?: string | null;
}

export interface SchemaIndexDiff {
  indexName: string;
  changeType: "Added" | "Modified" | "Removed";
  oldDefinition?: string | null;
  newDefinition?: string | null;
}

export interface SchemaCompatibilityCheckRequest {
  tableKey: string;
  addFields?: import("./dynamic-tables").DynamicFieldDefinition[] | null;
  updateFields?: import("./dynamic-tables").DynamicFieldUpdateDefinition[] | null;
  removeFields?: string[] | null;
  addIndexes?: import("./dynamic-tables").DynamicIndexDefinition[] | null;
  removeIndexes?: string[] | null;
}

export interface SchemaCompatibilityResult {
  isCompatible: boolean;
  issues: CompatibilityIssue[];
  highRiskWarnings: HighRiskWarning[];
}

export interface CompatibilityIssue {
  category: string;
  severity: "Error" | "Warning";
  objectName: string;
  description: string;
  suggestedAction?: string | null;
}

export interface HighRiskWarning {
  warningType: string;
  objectName: string;
  description: string;
  riskLevel: "Critical" | "High" | "Medium";
}

export interface DdlPreviewResult {
  tableKey: string;
  upScript: string;
  downHint?: string | null;
  warnings: string[];
  capabilityWarnings: DdlCapabilityWarning[];
}

export interface DdlCapabilityWarning {
  feature: string;
  dbType: string;
  description: string;
}

export interface DependencyGraphResult {
  tableKey: string;
  dependencies: DependencyEdge[];
  totalDependencyCount: number;
}

export interface DependencyEdge {
  sourceType: string;
  sourceKey: string;
  targetType: string;
  targetKey: string;
  relationDescription: string;
}

export interface SchemaImpactList {
  tableKey: string;
  impactedViews: SchemaImpactItem[];
  impactedFunctions: SchemaImpactItem[];
  impactedFlows: SchemaImpactItem[];
  totalCount: number;
}

export interface SchemaImpactItem {
  resourceType: string;
  resourceId: string;
  resourceName: string;
  impactDescription: string;
  navigationPath?: string | null;
}
