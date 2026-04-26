import type { MicroflowAuthoringSchema } from "@atlas/microflow";

/**
 * 逻辑「版本/草稿」与物理行映射：一个 `MicroflowSchemaSnapshotRow` 对应某次保存/发布前的可还原点。
 * `schema` 为 Authoring 模型；禁止写入 FlowGram。
 */
export interface MicroflowSchemaSnapshotContract {
  id: string;
  resourceId: string;
  schema: MicroflowAuthoringSchema;
  schemaVersion: string;
  migrationVersion?: string;
  reason?: string;
  createdAt: string;
}
