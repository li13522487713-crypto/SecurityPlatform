export type { MicroflowVersionSummary } from "../resource/resource-types";
import type { MicroflowAuthoringSchema } from "@atlas/microflow";

export interface MicroflowValidationSummary {
  errorCount: number;
  warningCount: number;
  infoCount: number;
}

export interface MicroflowBreakingChange {
  id: string;
  severity: "low" | "medium" | "high";
  code:
    | "PARAMETER_REMOVED"
    | "PARAMETER_TYPE_CHANGED"
    | "RETURN_TYPE_CHANGED"
    | "PUBLISHED_NODE_REMOVED"
    | "CALLED_MICROFLOW_MISSING"
    | "ENTITY_REFERENCE_MISSING"
    | "EXPOSED_URL_CHANGED";
  message: string;
  fieldPath?: string;
  before?: string;
  after?: string;
}

export interface MicroflowVersionDiff {
  addedParameters: string[];
  removedParameters: string[];
  changedParameters: Array<{
    name: string;
    beforeType: string;
    afterType: string;
  }>;
  returnTypeChanged?: {
    beforeType: string;
    afterType: string;
  };
  addedObjects: string[];
  removedObjects: string[];
  changedObjects: string[];
  addedFlows: string[];
  removedFlows: string[];
  breakingChanges: MicroflowBreakingChange[];
}

export interface MicroflowVersionSummary {
  id: string;
  resourceId: string;
  version: string;
  status: "draft" | "published" | "archived" | "rolledBack";
  createdAt: string;
  createdBy?: string;
  description?: string;
  schemaSnapshotId: string;
  validationSummary?: MicroflowValidationSummary;
  referenceCount?: number;
  isLatestPublished?: boolean;
}

export interface MicroflowPublishedSnapshot {
  id: string;
  resourceId: string;
  version: string;
  schema: MicroflowAuthoringSchema;
  publishedAt: string;
  publishedBy?: string;
  description?: string;
  validationSummary: MicroflowValidationSummary;
  schemaHash?: string;
}

export interface MicroflowVersionDetail extends MicroflowVersionSummary {
  snapshot: MicroflowPublishedSnapshot;
  diffFromCurrent?: MicroflowVersionDiff;
}
