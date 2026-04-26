import type { MicroflowAuthoringSchema } from "@atlas/microflow";

/**
 * 描述一条 Authoring 结构迁移（`schemaVersion` 轴），与 `migrationVersion` / 行级 `MicroflowSchemaMigrationRow` 配合。
 */
export interface MicroflowSchemaMigration {
  fromVersion: string;
  toVersion: string;
  description: string;
  migrate: "frontend" | "backend" | "both";
}

export interface MicroflowSchemaMigrationResult {
  schema: MicroflowAuthoringSchema;
  warnings: string[];
  changed: boolean;
}
