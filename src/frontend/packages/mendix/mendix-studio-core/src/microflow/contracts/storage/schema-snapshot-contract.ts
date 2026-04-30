import type { MicroflowDesignSchema } from "@atlas/microflow";

/**
 * 逻辑「版本/草稿」与物理行映射：一个 `MicroflowSchemaSnapshotRow` 对应某次保存前的设计态可还原点。
 * `schema` 为新版 `MicroflowDesignSchema`。
 */
export interface MicroflowSchemaSnapshotContract {
  id: string;
  resourceId: string;
  schema: MicroflowDesignSchema;
  schemaVersion: string;
  migrationVersion?: string;
  reason?: string;
  createdAt: string;
}
