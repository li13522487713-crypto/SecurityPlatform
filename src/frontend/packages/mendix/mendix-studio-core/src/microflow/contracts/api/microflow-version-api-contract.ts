import type { MicroflowDuplicateInput } from "../../resource/resource-types";
import type { MicroflowResource } from "../../resource/resource-types";
import type { MicroflowVersionDetail, MicroflowVersionDiff, MicroflowVersionSummary } from "../../versions/microflow-version-types";

import type { MicroflowApiResponse } from "./api-envelope";

/** GET /api/microflows/{id}/versions */
export type ListMicroflowVersionsResponse = MicroflowApiResponse<MicroflowVersionSummary[]>;

export type GetMicroflowVersionDetailResponse = MicroflowApiResponse<MicroflowVersionDetail>;

export interface RollbackMicroflowVersionRequest {
  reason?: string;
}

export type RollbackMicroflowVersionResponse = MicroflowApiResponse<MicroflowResource>;

export interface DuplicateMicroflowVersionRequest {
  name?: string;
  displayName?: string;
  moduleId?: string;
  moduleName?: string;
  tags?: string[];
}

export type DuplicateMicroflowVersionResponse = MicroflowApiResponse<MicroflowResource>;

/** 与 `MicroflowDuplicateInput` 对齐。 */
export type DuplicateMicroflowVersionRequestPayload = MicroflowDuplicateInput;

export type CompareMicroflowVersionWithCurrentResponse = MicroflowApiResponse<MicroflowVersionDiff>;
