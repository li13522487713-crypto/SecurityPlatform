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

export interface MicroflowResourceAdapter {
  listMicroflows(query?: MicroflowResourceQuery): Promise<MicroflowResourceListResult>;
  getMicroflow(id: string): Promise<MicroflowResource | undefined>;
  createMicroflow(input: MicroflowCreateInput): Promise<MicroflowResource>;
  updateMicroflow(id: string, patch: MicroflowResourcePatch): Promise<MicroflowResource>;
  saveMicroflowSchema(id: string, schema: MicroflowAuthoringSchema | MicroflowSchema): Promise<MicroflowResource>;
  duplicateMicroflow(id: string, input?: MicroflowDuplicateInput): Promise<MicroflowResource>;
  renameMicroflow(id: string, name: string, displayName?: string): Promise<MicroflowResource>;
  toggleFavorite(id: string, favorite: boolean): Promise<MicroflowResource>;
  archiveMicroflow(id: string): Promise<MicroflowResource>;
  restoreMicroflow(id: string): Promise<MicroflowResource>;
  deleteMicroflow(id: string): Promise<void>;
  publishMicroflow(id: string, input: MicroflowPublishInput): Promise<MicroflowPublishResult>;
  getMicroflowReferences(id: string): Promise<MicroflowReference[]>;
  getMicroflowVersions(id: string): Promise<MicroflowVersionSummary[]>;
  getMicroflowVersionDetail(id: string, versionId: string): Promise<MicroflowVersionDetail | undefined>;
  rollbackMicroflowVersion(id: string, versionId: string): Promise<MicroflowResource>;
  duplicateMicroflowVersion(id: string, versionId: string, input?: MicroflowDuplicateInput): Promise<MicroflowResource>;
  analyzeMicroflowPublishImpact(id: string, input: MicroflowPublishInput): Promise<MicroflowPublishImpactAnalysis>;
  compareMicroflowVersion(id: string, versionId: string): Promise<MicroflowVersionDiff>;
}
