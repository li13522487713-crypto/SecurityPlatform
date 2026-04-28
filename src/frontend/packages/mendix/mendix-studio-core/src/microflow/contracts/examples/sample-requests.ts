import type { ListMicroflowsRequest } from "../api/microflow-resource-api-contract";
import type { SaveMicroflowSchemaRequest } from "../api/microflow-schema-api-contract";
import type { ValidateMicroflowRequest } from "../api/microflow-validation-api-contract";
import type { PublishMicroflowApiRequest } from "../api/microflow-publish-api-contract";

export const sampleListMicroflowsRequest: ListMicroflowsRequest = {
  pageIndex: 1,
  pageSize: 20,
  sortBy: "updatedAt",
  sortOrder: "desc",
  status: ["draft", "published"],
  tags: ["order", "sales"]
};

export const sampleSaveSchemaRequest: SaveMicroflowSchemaRequest = {
  baseVersion: "1.0.0",
  saveReason: "manual-save",
  schema: { id: "mf-1" } as import("@atlas/microflow").MicroflowAuthoringSchema
};

export const sampleValidateRequest: ValidateMicroflowRequest = {
  mode: "publish",
  includeWarnings: true,
  includeInfo: true,
  schema: { id: "mf-1" } as import("@atlas/microflow").MicroflowAuthoringSchema
};

export const samplePublishRequest: PublishMicroflowApiRequest = {
  version: "1.2.0",
  description: "release",
  confirmBreakingChanges: true
};
