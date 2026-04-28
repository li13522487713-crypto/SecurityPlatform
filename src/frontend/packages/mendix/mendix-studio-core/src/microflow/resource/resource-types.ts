import type {
  MicroflowAuthoringSchema,
  MicroflowConcurrencyConfig,
  MicroflowDataType,
  MicroflowExposureConfig,
  MicroflowParameter,
  MicroflowSecurityConfig
} from "@atlas/microflow";

import type { MicroflowPageQuery } from "../contracts/api/api-pagination";
import type { MicroflowReference } from "../references/microflow-reference-types";
import type { MicroflowVersionSummary } from "../versions/microflow-version-types";

export type MicroflowResourceStatus = "draft" | "published" | "archived";
export type MicroflowPublishStatus = "neverPublished" | "published" | "changedAfterPublish";
export type MicroflowLastRunStatus = "success" | "failed" | "neverRun";
export type MicroflowResourceView = "card" | "table";

export interface MicroflowResourcePermissions {
  canEdit: boolean;
  canDelete: boolean;
  canPublish: boolean;
  canArchive: boolean;
  canDuplicate: boolean;
}

export interface MicroflowResource {
  id: string;
  schemaId: string;
  workspaceId?: string;
  moduleId: string;
  moduleName?: string;
  name: string;
  displayName: string;
  qualifiedName?: string;
  description?: string;
  tags: string[];
  ownerId?: string;
  ownerName?: string;
  createdBy?: string;
  createdAt: string;
  updatedBy?: string;
  updatedAt: string;
  version: string;
  latestPublishedVersion?: string;
  status: MicroflowResourceStatus;
  publishStatus?: MicroflowPublishStatus;
  favorite: boolean;
  archived: boolean;
  referenceCount: number;
  lastRunStatus?: MicroflowLastRunStatus;
  lastRunAt?: string;
  schema: MicroflowAuthoringSchema;
  permissions?: MicroflowResourcePermissions;
}

export interface MicroflowResourceQuery extends MicroflowPageQuery {
  /** 多选为 OR 语义。 */
  workspaceId?: string;
  keyword?: string;
  status?: MicroflowResourceStatus[];
  /** 多选为 OR 语义。 */
  publishStatus?: MicroflowPublishStatus[];
  favoriteOnly?: boolean;
  ownerId?: string;
  moduleId?: string;
  /**
   * 多选为 **OR** 语义：至少匹配其一（与 `ListMicroflowsRequest` / 后端 API 一致）。
   */
  tags?: string[];
  updatedFrom?: string;
  updatedTo?: string;
  sortBy?: "updatedAt" | "createdAt" | "name" | "version" | "referenceCount";
  sortOrder?: "asc" | "desc";
  /** 仅资源库 UI 使用；不进入无 `view` 的 list API。 */
  view?: MicroflowResourceView;
}

export interface MicroflowCreateInput {
  name: string;
  displayName?: string;
  description?: string;
  moduleId: string;
  moduleName?: string;
  tags: string[];
  parameters: MicroflowParameter[];
  returnType: MicroflowDataType;
  returnVariableName?: string;
  security?: Partial<MicroflowSecurityConfig>;
  concurrency?: Partial<MicroflowConcurrencyConfig>;
  exposure?: Partial<MicroflowExposureConfig>;
  template?: "blank" | "orderProcessing" | "approval" | "restErrorHandling" | "loopProcessing" | "objectTypeDecision" | "listProcessing";
}

export interface MicroflowResourcePatch {
  name?: string;
  displayName?: string;
  description?: string;
  tags?: string[];
  moduleId?: string;
  moduleName?: string;
  status?: MicroflowResourceStatus;
  publishStatus?: MicroflowPublishStatus;
  favorite?: boolean;
  archived?: boolean;
  schema?: MicroflowAuthoringSchema;
  permissions?: MicroflowResourcePermissions;
}

export interface MicroflowDuplicateInput {
  name?: string;
  displayName?: string;
  moduleId?: string;
  moduleName?: string;
  tags?: string[];
}

export interface MicroflowResourceListResult {
  items: MicroflowResource[];
  total: number;
  /** 与 `MicroflowApiPageResult` 对齐；未分页时由适配器回退为 1 页全量。 */
  pageIndex?: number;
  pageSize?: number;
  hasMore?: boolean;
}

export type { MicroflowReference, MicroflowVersionSummary };
