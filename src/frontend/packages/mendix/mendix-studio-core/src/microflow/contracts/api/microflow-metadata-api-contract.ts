import type {
  MetadataEntity,
  MetadataEnumeration,
  MicroflowMetadataCatalog,
  MetadataMicroflowRef
} from "@atlas/microflow";

import type { MicroflowApiResponse } from "./api-envelope";

/**
 * GET /api/microflow-metadata
 * `MicroflowMetadataCatalog` 在 @atlas/microflow 中已含 `version?`；响应中 **必须** 提供可缓存的 `updatedAt`。
 */
export interface GetMicroflowMetadataRequest {
  workspaceId?: string;
  moduleId?: string;
  includeSystem?: boolean;
  includeArchived?: boolean;
}

export type GetMicroflowMetadataResponseBody = MicroflowMetadataCatalog & {
  /** ISO-8601，用于 ETag/客户端缓存。 */
  updatedAt: string;
  /** 若 `catalog.version` 未设，可与此处并列提供同一语义。 */
  catalogVersion?: string;
};

export type GetMicroflowMetadataResponse = MicroflowApiResponse<GetMicroflowMetadataResponseBody>;

export type GetMetadataEntityResponse = MicroflowApiResponse<MetadataEntity>;
export type GetMetadataEnumerationResponse = MicroflowApiResponse<MetadataEnumeration>;

/**
 * GET /api/microflow-metadata/microflows
 * 可视为 catalog.microflows 的独立视图/过滤结果。
 */
export type ListMetadataMicroflowRefsResponse = MicroflowApiResponse<MetadataMicroflowRef[]>;
