import type { MicroflowAuthoringSchema, MicroflowMetadataCatalog, MicroflowRunSession } from "@atlas/microflow";

import type { MicroflowReference } from "../../references/microflow-reference-types";
import type { MicroflowResource } from "../../resource/resource-types";
import type { MicroflowPublishedSnapshot, MicroflowVersionSummary } from "../../versions/microflow-version-types";

export interface MicroflowSchemaSnapshotContract {
  resourceId: string;
  schema: MicroflowAuthoringSchema;
  schemaVersion: string;
  updatedAt: string;
  updatedBy?: string;
}

export interface MicroflowContractMockStore {
  resources: Map<string, MicroflowResource>;
  schemaSnapshots: Map<string, MicroflowSchemaSnapshotContract>;
  versions: Map<string, MicroflowVersionSummary[]>;
  publishSnapshots: Map<string, MicroflowPublishedSnapshot>;
  references: Map<string, MicroflowReference[]>;
  runSessions: Map<string, MicroflowRunSession>;
  metadataCatalog: MicroflowMetadataCatalog;
}

export interface MicroflowContractMockSeedOptions {
  workspaceId?: string;
  currentUser?: {
    id: string;
    name: string;
  };
}

export type MicroflowMockErrorScenario =
  | "unauthorized"
  | "forbidden"
  | "not-found"
  | "version-conflict"
  | "validation-failed"
  | "publish-blocked"
  | "reference-blocked"
  | "service-unavailable"
  | "network";
