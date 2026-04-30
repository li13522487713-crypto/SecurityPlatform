import type { MicroflowDesignSchema } from "@atlas/microflow";

/**
 * 描述一条新版设计态 schemaVersion 轴上的结构升级记录。
 * 旧设计态不再进入运行时迁移流程，命中旧格式时应由 API 直接返回 MICROFLOW_SCHEMA_INVALID。
 */
export interface MicroflowSchemaMigration {
  fromVersion: string;
  toVersion: string;
  description: string;
  migrate: "frontend" | "backend" | "both";
}

export interface MicroflowSchemaMigrationResult {
  schema: MicroflowDesignSchema;
  warnings: string[];
  changed: boolean;
}
