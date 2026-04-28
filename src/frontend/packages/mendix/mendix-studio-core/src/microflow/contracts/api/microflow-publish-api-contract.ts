import type { MicroflowPublishResult } from "../../publish/microflow-publish-types";

import type { MicroflowApiResponse } from "./api-envelope";

/**
 * POST /api/microflows/{id}/publish
 */
export interface PublishMicroflowApiRequest {
  version: string;
  description?: string;
  confirmBreakingChanges?: boolean;
  force?: boolean;
}

export type PublishMicroflowApiResponse = MicroflowApiResponse<MicroflowPublishResult>;
