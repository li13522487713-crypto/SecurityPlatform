import type { MicroflowMetadataCatalog } from "@atlas/microflow";

/**
 * 全量/分区缓存的元数据目录；`catalogJson` 为 `MicroflowMetadataCatalog` 形状 JSON。
 */
export interface MicroflowMetadataCacheContract {
  workspaceId: string;
  catalogVersion: string;
  updatedAt: string;
  catalog: MicroflowMetadataCatalog;
}
