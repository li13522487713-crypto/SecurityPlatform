import type {
  MetadataEntity,
  MetadataEnumeration,
  MetadataMicroflowRef,
  MetadataPageRef,
  MetadataWorkflowRef,
  MicroflowMetadataCatalog,
} from "./metadata-catalog";
import { mockMicroflowMetadataCatalog } from "./mock-metadata";
import {
  getEntityByQualifiedName,
  getEnumerationByQualifiedName,
} from "./metadata-catalog";

export interface GetMicroflowMetadataRequest {
  workspaceId?: string;
  moduleId?: string;
  includeSystem?: boolean;
  includeArchived?: boolean;
}

export interface GetMicroflowRefsRequest extends GetMicroflowMetadataRequest {
  keyword?: string;
}

export interface GetPageRefsRequest extends GetMicroflowMetadataRequest {
  keyword?: string;
}

export interface GetWorkflowRefsRequest extends GetMicroflowMetadataRequest {
  keyword?: string;
}

/**
 * 元数据唯一异步加载入口（不依赖 React / app-web）。
 * 生产环境通过 {@link createHttpMicroflowMetadataAdapter} 或业务 Adapter 注入；
 * 本地开发默认 {@link createMockMicroflowMetadataAdapter}。
 */
export interface MicroflowMetadataAdapter {
  getMetadataCatalog(request?: GetMicroflowMetadataRequest): Promise<MicroflowMetadataCatalog>;
  refreshMetadataCatalog?(request?: GetMicroflowMetadataRequest): Promise<MicroflowMetadataCatalog>;
  getEntity?(qualifiedName: string): Promise<MetadataEntity | undefined>;
  getEnumeration?(qualifiedName: string): Promise<MetadataEnumeration | undefined>;
  getMicroflowRefs?(request?: GetMicroflowRefsRequest): Promise<MetadataMicroflowRef[]>;
  getPageRefs?(request?: GetPageRefsRequest): Promise<MetadataPageRef[]>;
  getWorkflowRefs?(request?: GetWorkflowRefsRequest): Promise<MetadataWorkflowRef[]>;
}

/**
 * 与 {@link createMockMicroflowMetadataAdapter} 默认 catalog 相同。
 * 仅用于测试、契约验收、以及暂时无法注入 {@link MicroflowMetadataAdapter} 的同步桥接；编辑器与宿主应通过 Provider / Adapter 获取元数据。
 */
export function getDefaultMockMetadataCatalog(): MicroflowMetadataCatalog {
  return mockMicroflowMetadataCatalog;
}

export function createMockMicroflowMetadataAdapter(
  catalog: MicroflowMetadataCatalog = mockMicroflowMetadataCatalog,
): MicroflowMetadataAdapter {
  return {
    async getMetadataCatalog() {
      return catalog;
    },
    async refreshMetadataCatalog() {
      return catalog;
    },
    async getEntity(qualifiedName: string) {
      return getEntityByQualifiedName(catalog, qualifiedName);
    },
    async getEnumeration(qualifiedName: string) {
      return getEnumerationByQualifiedName(catalog, qualifiedName);
    },
    async getMicroflowRefs() {
      return catalog.microflows;
    },
    async getPageRefs() {
      return catalog.pages;
    },
    async getWorkflowRefs() {
      return catalog.workflows;
    },
  };
}

/**
 * 第一版：与 mock 等价；后续可接 localStorage / IndexedDB 覆盖。
 */
export function createLocalMicroflowMetadataAdapter(): MicroflowMetadataAdapter {
  return createMockMicroflowMetadataAdapter();
}
