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
  schema: { id: "mf-1", schemaVersion: "flowgram.microflow.v1", moduleId: "module-a", name: "mf-1", displayName: "mf-1", workflow: { nodes: [], edges: [] }, editor: { viewport: { x: 0, y: 0, zoom: 1 }, zoom: 1, selection: {} }, parameters: [], returnType: { kind: "void" }, variables: [], validation: { issues: [] }, audit: { version: "0.1.0", status: "draft" } } as import("@atlas/microflow").MicroflowDesignSchema
};

export const sampleValidateRequest: ValidateMicroflowRequest = {
  mode: "publish",
  includeWarnings: true,
  includeInfo: true,
  schema: { id: "mf-1", schemaVersion: "flowgram.microflow.v1", moduleId: "module-a", name: "mf-1", displayName: "mf-1", workflow: { nodes: [], edges: [] }, editor: { viewport: { x: 0, y: 0, zoom: 1 }, zoom: 1, selection: {} }, parameters: [], returnType: { kind: "void" }, variables: [], validation: { issues: [] }, audit: { version: "0.1.0", status: "draft" } } as import("@atlas/microflow").MicroflowDesignSchema
};

export const samplePublishRequest: PublishMicroflowApiRequest = {
  version: "1.2.0",
  description: "release",
  confirmBreakingChanges: true
};
