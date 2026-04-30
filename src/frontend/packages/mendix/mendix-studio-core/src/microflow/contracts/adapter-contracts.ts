import type {
  MicroflowMetadataCatalog,
  MetadataEntity,
  MetadataEnumeration,
  MetadataMicroflowRef,
  MetadataPageRef,
  MetadataWorkflowRef
} from "@atlas/microflow/metadata";
import type { MicroflowTraceFrame } from "@atlas/microflow/debug";
import type {
  TestRunMicroflowRequest,
  TestRunMicroflowResponse,
  ValidateMicroflowRequest,
  ValidateMicroflowResponse
} from "@atlas/microflow/runtime-adapter";

import type { MicroflowResourceAdapter } from "../adapter/microflow-resource-adapter";

/**
 * 元数据由 MetadataCatalog 驱动选择器；真实后端应对齐这些查询语义。
 * 与 {@link @atlas/microflow} 中 getEntityByQualifiedName 等纯函数可组合使用。
 */
export interface MicroflowMetadataAdapter {
  getMetadataCatalog(): Promise<MicroflowMetadataCatalog> | MicroflowMetadataCatalog;
  refreshMetadataCatalog(): Promise<MicroflowMetadataCatalog> | MicroflowMetadataCatalog;
  getEntity(qualifiedName: string): Promise<MetadataEntity | undefined> | MetadataEntity | undefined;
  getEnumeration(qualifiedName: string): Promise<MetadataEnumeration | undefined> | MetadataEnumeration | undefined;
  getMicroflowRefs(keyword?: string): Promise<MetadataMicroflowRef[]> | MetadataMicroflowRef[];
  getPageRefs(keyword?: string): Promise<MetadataPageRef[]> | MetadataPageRef[];
  getWorkflowRefs(keyword?: string): Promise<MetadataWorkflowRef[]> | MetadataWorkflowRef[];
}

/**
 * 与本地/远端执行器通信；trace 既用于 DebugPanel 展示，也可能承载 runtime command 等前端消费信息。
 */
export interface MicroflowRuntimeAdapter {
  validateMicroflow(request: ValidateMicroflowRequest): Promise<ValidateMicroflowResponse>;
  testRunMicroflow(request: TestRunMicroflowRequest): Promise<TestRunMicroflowResponse>;
  cancelMicroflowRun(runId: string): Promise<void>;
  getMicroflowRunTrace(runId: string): Promise<MicroflowTraceFrame[]>;
}

/**
 * 资源层适配器以 studio-core 声明为准，见 {@link MicroflowResourceAdapter}。
 * 本符号仅为契约文档与 IDE 导航保留。
 */
export type { MicroflowResourceAdapter };
