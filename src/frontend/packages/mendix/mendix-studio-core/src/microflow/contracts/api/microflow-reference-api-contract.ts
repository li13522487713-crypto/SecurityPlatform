import type { MicroflowPublishImpactAnalysis } from "../../publish/microflow-publish-types";
import type { MicroflowImpactLevel, MicroflowReference } from "../../references/microflow-reference-types";

import type { MicroflowApiResponse } from "./api-envelope";

/**
 * GET /api/microflows/{id}/references
 */
export interface GetMicroflowReferencesRequest {
  includeInactive?: boolean;
  sourceType?: NonNullable<MicroflowReference["sourceType"]>[];
  impactLevel?: MicroflowImpactLevel[];
}

export type GetMicroflowReferencesResponse = MicroflowApiResponse<MicroflowReference[]>;

/**
 * GET /api/microflows/{id}/impact
 */
export interface AnalyzeMicroflowImpactRequest {
  version?: string;
  includeBreakingChanges?: boolean;
  includeReferences?: boolean;
}

export type AnalyzeMicroflowImpactResponse = MicroflowApiResponse<MicroflowPublishImpactAnalysis>;
