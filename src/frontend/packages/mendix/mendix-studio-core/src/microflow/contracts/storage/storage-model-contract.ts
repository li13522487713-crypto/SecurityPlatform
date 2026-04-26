import type {
  MicroflowMetadataCacheRow,
  MicroflowPublishSnapshotRow,
  MicroflowReferenceRow,
  MicroflowResourceRow,
  MicroflowRunLogRow,
  MicroflowRunSessionRow,
  MicroflowRunTraceFrameRow,
  MicroflowSchemaMigrationRow,
  MicroflowSchemaSnapshotRow,
  MicroflowVersionRow
} from "./storage-types";

/**
 * 将各表行聚合为只读关系视图，供后端设计参考（非 ORM 实体）。
 */
export interface MicroflowStorageModel {
  resource: MicroflowResourceRow;
  currentDraftSnapshot: MicroflowSchemaSnapshotRow;
  publishedSnapshots: MicroflowPublishSnapshotRow[];
  versions: MicroflowVersionRow[];
  references: MicroflowReferenceRow[];
  runs: MicroflowRunSessionRow[];
  traceFrames: MicroflowRunTraceFrameRow[];
  runLogs: MicroflowRunLogRow[];
  metadataCache?: MicroflowMetadataCacheRow;
  appliedMigrations: MicroflowSchemaMigrationRow[];
}
