import type { MicroflowDesignSchema } from "@atlas/microflow";

import type { MicroflowValidationSummary } from "../../versions/microflow-version-types";

/**
 * 发布快照：**不可变**。创建后只读；`schema` 内同样为新版 `MicroflowDesignSchema`，
 * 用于版本详情与发布历史回显。
 */
export interface MicroflowPublishSnapshotStorageContract {
  id: string;
  resourceId: string;
  version: string;
  schema: MicroflowDesignSchema;
  validationSummary: MicroflowValidationSummary;
  schemaHash?: string;
  publishedAt: string;
  publishedBy?: string;
  description?: string;
}
