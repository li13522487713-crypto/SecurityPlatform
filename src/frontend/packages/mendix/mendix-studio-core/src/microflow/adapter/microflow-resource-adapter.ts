import type { MicroflowAuthoringSchema, MicroflowSchema } from "@atlas/microflow";

import type { MicroflowPublishImpactAnalysis, MicroflowPublishInput, MicroflowPublishResult } from "../publish/microflow-publish-types";
import type { MicroflowReference } from "../references/microflow-reference-types";
import type {
  MicroflowCreateInput,
  MicroflowDuplicateInput,
  MicroflowResource,
  MicroflowResourceListResult,
  MicroflowResourcePatch,
  MicroflowResourceQuery
} from "../resource/resource-types";
import type { MicroflowVersionDetail, MicroflowVersionDiff, MicroflowVersionSummary } from "../versions/microflow-version-types";
import type { GetMicroflowReferencesRequest } from "../contracts/api/microflow-reference-api-contract";

/**
 * 与 `PUT /api/microflows/{id}/schema` 的 `SaveMicroflowSchemaRequest` 对齐的本地选项。
 */
export interface SaveMicroflowSchemaOptions {
  baseVersion?: string;
  saveReason?: string;
}

export interface MicroflowResourceAdapter {
  listMicroflows(query?: MicroflowResourceQuery): Promise<MicroflowResourceListResult>;
  getMicroflow(id: string): Promise<MicroflowResource | undefined>;
  createMicroflow(input: MicroflowCreateInput): Promise<MicroflowResource>;
  updateMicroflow(id: string, patch: MicroflowResourcePatch): Promise<MicroflowResource>;
  saveMicroflowSchema(id: string, schema: MicroflowAuthoringSchema | MicroflowSchema, options?: SaveMicroflowSchemaOptions): Promise<MicroflowResource>;
  duplicateMicroflow(id: string, input?: MicroflowDuplicateInput): Promise<MicroflowResource>;
  renameMicroflow(id: string, name: string, displayName?: string): Promise<MicroflowResource>;
  toggleFavorite(id: string, favorite: boolean): Promise<MicroflowResource>;
  archiveMicroflow(id: string): Promise<MicroflowResource>;
  restoreMicroflow(id: string): Promise<MicroflowResource>;
  deleteMicroflow(id: string): Promise<void>;
  publishMicroflow(id: string, input: MicroflowPublishInput): Promise<MicroflowPublishResult>;
  getMicroflowReferences(id: string, query?: GetMicroflowReferencesRequest): Promise<MicroflowReference[]>;
  getMicroflowVersions(id: string): Promise<MicroflowVersionSummary[]>;
  getMicroflowVersionDetail(id: string, versionId: string): Promise<MicroflowVersionDetail | undefined>;
  rollbackMicroflowVersion(id: string, versionId: string): Promise<MicroflowResource>;
  duplicateMicroflowVersion(id: string, versionId: string, input?: MicroflowDuplicateInput): Promise<MicroflowResource>;
  analyzeMicroflowPublishImpact(id: string, input: MicroflowPublishInput): Promise<MicroflowPublishImpactAnalysis>;
  compareMicroflowVersion(id: string, versionId: string): Promise<MicroflowVersionDiff>;
}
