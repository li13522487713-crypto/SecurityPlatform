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
  status?: string | readonly string[];
}

export interface GetPageRefsRequest extends GetMicroflowMetadataRequest {
  keyword?: string;
}

export interface GetWorkflowRefsRequest extends GetMicroflowMetadataRequest {
  keyword?: string;
}

/** 简要信息，与 app-web DatabaseCenterSourceSummary 保持字段对齐 */
export interface MicroflowDatabaseSourceSummary {
  id: string;
  name: string;
  sourceKind: string;
  driverCode: string;
  environment?: string | null;
  status?: string | null;
  readOnly?: boolean | null;
  defaultSchemaName?: string | null;
}

export interface MicroflowDatabaseColumnSummary {
  name: string;
  dataType: string;
  nullable: boolean;
  primaryKey: boolean;
}

export interface MicroflowDatabaseObjectSummary {
  id: string;
  name: string;
  objectType: string;
  columns?: MicroflowDatabaseColumnSummary[];
}

export interface MicroflowDatabaseSchemaStructure {
  sourceId: string;
  schemaName: string;
  objects: MicroflowDatabaseObjectSummary[];
  columnsByObject: Record<string, MicroflowDatabaseColumnSummary[]>;
}

export interface MicroflowDatabaseSqlResult {
  columns: Array<{ name: string; dataType?: string | null }>;
  rows: Record<string, unknown>[];
  elapsedMs?: number | null;
  truncated?: boolean;
}

export interface GetDatabaseSourcesRequest {
  workspaceId?: string;
  keyword?: string;
}

/**
 * 元数据唯一异步加载入口（不依赖 React / app-web）。
 * 生产环境通过 {@link createHttpMicroflowMetadataAdapter} 或业务 Adapter 注入；
 * Provider 不会在缺失 adapter 或请求失败时回落 mock metadata。
 */
export interface MicroflowMetadataAdapter {
  getMetadataCatalog(request?: GetMicroflowMetadataRequest): Promise<MicroflowMetadataCatalog>;
  refreshMetadataCatalog?(request?: GetMicroflowMetadataRequest): Promise<MicroflowMetadataCatalog>;
  getEntity?(qualifiedName: string): Promise<MetadataEntity | undefined>;
  getEnumeration?(qualifiedName: string): Promise<MetadataEnumeration | undefined>;
  getMicroflowRefs?(request?: GetMicroflowRefsRequest): Promise<MetadataMicroflowRef[]>;
  getPageRefs?(request?: GetPageRefsRequest): Promise<MetadataPageRef[]>;
  getWorkflowRefs?(request?: GetWorkflowRefsRequest): Promise<MetadataWorkflowRef[]>;
  /** 列出工作区下所有数据库连接（可选，未实现时表单回退到手动输入） */
  getDatabaseSources?(request?: GetDatabaseSourcesRequest): Promise<MicroflowDatabaseSourceSummary[]>;
  /** 获取某个数据源的 schema 结构（表/列/外键）*/
  getDatabaseSchemaStructure?(sourceId: string, schemaName?: string): Promise<MicroflowDatabaseSchemaStructure>;
  /** 预览 SQL 执行结果（只读） */
  previewDatabaseSql?(sourceId: string, sql: string, schemaName?: string): Promise<MicroflowDatabaseSqlResult>;
}

/**
 * 与 {@link createMockMicroflowMetadataAdapter} 默认 catalog 相同。
 * 仅用于测试、契约验收、以及暂时无法注入 {@link MicroflowMetadataAdapter} 的同步模式；编辑器与宿主应通过 Provider / Adapter 获取元数据。
 */
export function getDefaultMockMetadataCatalog(): MicroflowMetadataCatalog {
  return mockMicroflowMetadataCatalog;
}

/**
 * Development/Test only.
 * Do not import this factory from production runtime paths.
 */
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
 * Local development/offline debug only.
 * 第一版：与 mock 等价；后续可接 localStorage / IndexedDB 覆盖。
 */
export function createLocalMicroflowMetadataAdapter(): MicroflowMetadataAdapter {
  return createMockMicroflowMetadataAdapter();
}
