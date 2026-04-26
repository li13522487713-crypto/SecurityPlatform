import type { MicroflowSchemaMigration, MicroflowSchemaMigrationResult } from "./migration-types";

/**
 * 应用层/CLI 在加载旧 JSON 时执行迁移的签名（概念契约）。
 * 不兼容迁移应失败并返回 `MICROFLOW_SCHEMA_INVALID` 或等效业务错误，而非静默损坏。
 */
export type MicroflowSchemaMigrationHandler = (input: {
  fromVersion: string;
  toVersion: string;
  schema: unknown;
}) => Promise<MicroflowSchemaMigrationResult> | MicroflowSchemaMigrationResult;

export type { MicroflowSchemaMigration, MicroflowSchemaMigrationResult };
