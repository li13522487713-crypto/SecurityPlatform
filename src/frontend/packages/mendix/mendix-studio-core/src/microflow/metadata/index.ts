/**
 * 微流元数据统一出口：Adapter / Provider / Hooks 以 @atlas/microflow 为实装；
 * HTTP Adapter 仅在本包定义（依赖契约 Envelope）。
 */
export {
  type GetMicroflowMetadataRequest,
  type GetMicroflowRefsRequest,
  type GetPageRefsRequest,
  type GetWorkflowRefsRequest,
  type MicroflowMetadataAdapter,
  createLocalMicroflowMetadataAdapter,
  createMockMicroflowMetadataAdapter,
  MicroflowMetadataProvider,
  useMicroflowMetadata,
  useMicroflowMetadataCatalog,
  useAssociationCatalog,
  useEnumerationCatalog,
  useEntityCatalog,
  useMetadataStatus,
  useMicroflowRefCatalog,
  usePageCatalog,
  useWorkflowCatalog,
  EMPTY_MICROFLOW_METADATA_CATALOG,
  getDefaultMockMetadataCatalog,
  resolveStoredEntityQualifiedName,
} from "@atlas/microflow/metadata";

export type {
  MetadataAssociation,
  MetadataAttribute,
  MetadataConnector,
  MetadataEntity,
  MetadataEnumeration,
  MetadataMicroflowRef,
  MetadataPageRef,
  MetadataWorkflowRef,
  MicroflowMetadataCatalog,
} from "@atlas/microflow/metadata";

export { createHttpMicroflowMetadataAdapter, type HttpMicroflowMetadataAdapterOptions } from "./http-metadata-adapter";
