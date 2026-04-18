import type { JsonObject } from '../shared/json';

/**
 * VersionArchive —— 应用版本归档（docx §10.6，M14 完整 diff/rollback 基于此）。
 */
export interface VersionArchive {
  id: string;
  appId: string;
  versionLabel: string;
  /** 完整 AppSchema 快照（不在前端反复反序列化，必要时按需 parse）。*/
  schemaSnapshotJson: string;
  /** 依赖资源版本快照 JSON（workflow / chatflow / knowledge / database / variable / plugin / prompt-template 版本）。*/
  resourceSnapshot: ResourceSnapshot;
  buildMetadata?: JsonObject;
  note?: string;
  createdByUserId: string;
  isSystemSnapshot: boolean;
  createdAt: string;
}

export interface ResourceSnapshot {
  workflows?: ResourceVersionRef[];
  chatflows?: ResourceVersionRef[];
  knowledge?: ResourceVersionRef[];
  databases?: ResourceVersionRef[];
  variables?: ResourceVersionRef[];
  plugins?: ResourceVersionRef[];
  promptTemplates?: ResourceVersionRef[];
}

export interface ResourceVersionRef {
  id: string;
  version: string;
  /** 留待 M14 / M18 扩展的元信息。*/
  metadata?: JsonObject;
}
