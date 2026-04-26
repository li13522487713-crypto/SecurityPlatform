/**
 * 与 `storage-model-contract.md` 中表/列一一对应的后端建表用 TypeScript 草案（仅契约，不生成 ORM）。
 * 主键/外键/索引以 Markdown 为准。
 */

export interface MicroflowResourceRow {
  id: string;
  workspaceId: string;
  moduleId: string;
  name: string;
  displayName: string;
  description: string | null;
  /** JSON 数组字符串。 */
  tagsJson: string;
  ownerId: string | null;
  ownerName: string | null;
  status: string;
  publishStatus: string;
  version: string;
  latestPublishedVersion: string | null;
  favorite: boolean;
  archived: boolean;
  referenceCount: number;
  lastRunStatus: string | null;
  lastRunAt: string | null;
  createdBy: string | null;
  createdAt: string;
  updatedBy: string | null;
  updatedAt: string;
  /** 租户/工作区可映射到分区键。 */
  tenantId?: string;
}

export interface MicroflowSchemaSnapshotRow {
  id: string;
  resourceId: string;
  schemaVersion: string;
  migrationVersion: string | null;
  /** 仅 `MicroflowAuthoringSchema` JSON，无 FlowGram。 */
  schemaJson: string;
  schemaHash: string | null;
  createdBy: string | null;
  createdAt: string;
  reason: string | null;
}

export interface MicroflowVersionRow {
  id: string;
  resourceId: string;
  version: string;
  status: string;
  schemaSnapshotId: string;
  description: string | null;
  validationSummaryJson: string | null;
  referenceCount: number;
  isLatestPublished: boolean;
  createdBy: string | null;
  createdAt: string;
}

export interface MicroflowPublishSnapshotRow {
  id: string;
  resourceId: string;
  version: string;
  schemaSnapshotId: string;
  schemaJson: string;
  validationSummaryJson: string | null;
  impactAnalysisJson: string | null;
  publishedBy: string | null;
  publishedAt: string;
  description: string | null;
  schemaHash: string | null;
}

export interface MicroflowReferenceRow {
  id: string;
  targetMicroflowId: string;
  sourceType: string;
  sourceId: string | null;
  sourceName: string;
  sourcePath: string | null;
  sourceVersion: string | null;
  referencedVersion: string | null;
  referenceKind: string;
  impactLevel: string;
  description: string | null;
  active: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface MicroflowRunSessionRow {
  id: string;
  resourceId: string;
  schemaSnapshotId: string | null;
  status: string;
  inputJson: string;
  outputJson: string | null;
  errorJson: string | null;
  startedAt: string;
  endedAt: string | null;
  createdBy: string | null;
}

export interface MicroflowRunTraceFrameRow {
  id: string;
  runId: string;
  objectId: string;
  actionId: string | null;
  collectionId: string | null;
  incomingFlowId: string | null;
  outgoingFlowId: string | null;
  selectedCaseValueJson: string | null;
  loopIterationJson: string | null;
  status: string;
  startedAt: string;
  endedAt: string | null;
  durationMs: number;
  inputJson: string | null;
  outputJson: string | null;
  errorJson: string | null;
  variablesSnapshotJson: string | null;
}

export interface MicroflowRunLogRow {
  id: string;
  runId: string;
  timestamp: string;
  level: string;
  objectId: string | null;
  actionId: string | null;
  message: string;
}

export interface MicroflowMetadataCacheRow {
  id: string;
  workspaceId: string;
  catalogVersion: string;
  catalogJson: string;
  updatedAt: string;
}

export interface MicroflowSchemaMigrationRow {
  id: string;
  fromVersion: string;
  toVersion: string;
  description: string;
  appliedAt: string;
}
