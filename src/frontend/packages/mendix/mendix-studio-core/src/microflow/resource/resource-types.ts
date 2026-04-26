import type {
  MicroflowAuthoringSchema,
  MicroflowConcurrencyConfig,
  MicroflowDataType,
  MicroflowExposureConfig,
  MicroflowParameter,
  MicroflowSchema,
  MicroflowSecurityConfig
} from "@atlas/microflow";

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
  schema: MicroflowSchema;
  permissions?: MicroflowResourcePermissions;
}

export interface MicroflowResourceQuery {
  keyword?: string;
  status?: MicroflowResourceStatus[];
  favoriteOnly?: boolean;
  ownerId?: string;
  moduleId?: string;
  tags?: string[];
  updatedFrom?: string;
  updatedTo?: string;
  sortBy?: "updatedAt" | "createdAt" | "name" | "version" | "referenceCount";
  sortOrder?: "asc" | "desc";
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
  template?: "blank" | "orderProcessing" | "approval" | "restErrorHandling" | "loopProcessing";
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
}

export type { MicroflowReference, MicroflowVersionSummary };
