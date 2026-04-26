/**
 * 微流前端契约聚合出口：应用宿主应通过 `@atlas/mendix-studio-core` 引用，勿依赖内部子路径。
 * 实现类型与算法来自 `@atlas/microflow`；本包补充 Resource 模型、ResourceAdapter 与本文档化契约。
 */
export * from "./api";
export * from "./storage";
export * from "./migration";
export * from "./examples";
export * from "./runtime-dto-contract";
export * from "./adapter-contracts";
export * from "./sample-manifest";
export { verifyMicroflowContracts, type MicroflowContractVerificationResult } from "./verify-microflow-contracts";
export * from "./runtime-semantics";

export type { MicroflowAuthoringSchema, MicroflowSchema, MicroflowValidationIssue } from "@atlas/microflow/schema";
export type { MicroflowValidationIssue as ValidationIssue } from "@atlas/microflow/schema";

export type { MicroflowMetadataCatalog, MetadataEntity, MetadataEnumeration, MetadataMicroflowRef, MetadataPageRef, MetadataWorkflowRef } from "@atlas/microflow/metadata";

export type { MicroflowRunSession, MicroflowTraceFrame } from "@atlas/microflow/debug";
export type { MicroflowDebugState } from "@atlas/microflow/schema";
export type { TestRunMicroflowRequest, TestRunMicroflowResponse, ValidateMicroflowRequest, ValidateMicroflowResponse, MicroflowApiClient } from "@atlas/microflow/runtime-adapter";

export { buildVariableIndex } from "@atlas/microflow/variables";

export { microflowValidationCodes, validateMicroflowSchema } from "@atlas/microflow/validators";
export type { MicroflowValidationCode } from "@atlas/microflow/validators";
export { authoringToFlowGram } from "@atlas/microflow/flowgram/authoring-to-flowgram";
export { mockMicroflowMetadataCatalog } from "@atlas/microflow/metadata";
export { createLocalMicroflowApiClient, LocalMicroflowApiClient } from "@atlas/microflow/runtime-adapter";
