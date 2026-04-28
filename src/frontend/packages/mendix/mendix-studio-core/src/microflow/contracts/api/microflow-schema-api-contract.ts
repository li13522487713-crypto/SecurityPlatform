import type { MicroflowAuthoringSchema } from "@atlas/microflow";

import type { MicroflowResource } from "../../resource/resource-types";

import type { MicroflowApiResponse } from "./api-envelope";

/**
 * GET /api/microflows/{id}/schema
 * 仅持久化/传输 `MicroflowAuthoringSchema`（JSON），**不**包含 FlowGram WorkflowJSON。
 */
export interface GetMicroflowSchemaResponse {
  resourceId: string;
  schema: MicroflowAuthoringSchema;
  /** 结构版本，对应 Authoring 模型演进。 */
  schemaVersion: string;
  /** 后端侧存储迁移脚本版本。 */
  migrationVersion?: string;
  updatedAt: string;
  updatedBy?: string;
}

export type GetMicroflowSchemaApiResponse = MicroflowApiResponse<GetMicroflowSchemaResponse>;

/**
 * PUT /api/microflows/{id}/schema
 * `baseVersion`：乐观锁（对最近一次加载的 `schemaVersion` 或 ETag）；
 * 与 `MICROFLOW_VERSION_CONFLICT` 搭配。
 */
export interface SaveMicroflowSchemaRequest {
  schema: MicroflowAuthoringSchema;
  baseVersion?: string;
  schemaId?: string;
  version?: string;
  saveReason?: string;
  clientRequestId?: string;
  force?: boolean;
}

export interface SaveMicroflowSchemaResponse {
  resource: MicroflowResource;
  schemaVersion: string;
  updatedAt: string;
  /** 与资源层 `MicroflowResource.publishStatus` 对齐。 */
  changedAfterPublish: boolean;
}

export type SaveMicroflowSchemaApiResponse = MicroflowApiResponse<SaveMicroflowSchemaResponse>;

/**
 * POST /api/microflows/{id}/schema/migrate
 * 入参 `schema` 可为未知版本 JSON；成功则回当前 `MicroflowAuthoringSchema`。
 */
export interface MigrateMicroflowSchemaRequest {
  fromVersion: string;
  toVersion: string;
  schema: unknown;
}

export interface MigrateMicroflowSchemaResponse {
  schema: MicroflowAuthoringSchema;
  warnings: string[];
}

export type MigrateMicroflowSchemaApiResponse = MicroflowApiResponse<MigrateMicroflowSchemaResponse>;
