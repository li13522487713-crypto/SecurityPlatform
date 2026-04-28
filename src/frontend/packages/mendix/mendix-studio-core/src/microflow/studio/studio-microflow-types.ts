/**
 * Studio 视图层专用的微流类型定义。
 *
 * 这些类型是从后端 MicroflowResource 映射而来的展示层模型，
 * 不修改后端 DTO、不修改 MicroflowResource 结构、不修改 schema 结构。
 * 仅用于 Studio 组件层和 store 层的渲染与索引。
 */

export interface StudioMicroflowDefinitionView {
  id: string;
  moduleId: string;
  moduleName?: string;
  name: string;
  displayName: string;
  qualifiedName: string;
  description?: string;
  status: "draft" | "published" | "archived";
  publishStatus?: "neverPublished" | "published" | "changedAfterPublish";
  schemaId: string;
  version: string;
  latestPublishedVersion?: string;
  referenceCount: number;
  favorite: boolean;
  archived: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface StudioMicroflowDraftView {
  microflowId: string;
  schemaId: string;
  version: string;
}

export interface StudioMicroflowReferenceView {
  id: string;
  targetMicroflowId: string;
  sourceType: string;
  sourceId?: string;
  sourceName?: string;
  sourcePath?: string;
  referenceKind: string;
  active: boolean;
}
