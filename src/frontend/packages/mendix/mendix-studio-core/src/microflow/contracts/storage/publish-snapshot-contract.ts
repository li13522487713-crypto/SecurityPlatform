import type { MicroflowAuthoringSchema } from "@atlas/microflow";

import type { MicroflowValidationSummary } from "../../versions/microflow-version-types";

/**
 * 发布快照：**不可变**。创建后只读；`schema` 内为 Authoring JSON。
 * 与 `MicroflowPublishedSnapshot`（资源域）同语义，此处强调存储策略。
 */
export interface MicroflowPublishSnapshotStorageContract {
  id: string;
  resourceId: string;
  version: string;
  schema: MicroflowAuthoringSchema;
  validationSummary: MicroflowValidationSummary;
  schemaHash?: string;
  publishedAt: string;
  publishedBy?: string;
  description?: string;
}
