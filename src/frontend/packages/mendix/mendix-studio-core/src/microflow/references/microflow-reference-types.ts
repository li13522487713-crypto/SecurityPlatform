export type { MicroflowReference } from "../resource/resource-types";
export type MicroflowReferenceSourceType =
  | "microflow"
  | "workflow"
  | "page"
  | "form"
  | "button"
  | "schedule"
  | "api"
  | "unknown";

export type MicroflowReferenceKind =
  | "callMicroflow"
  | "pageAction"
  | "workflowActivity"
  | "apiExposure"
  | "scheduledJob"
  | "unknown";

export type MicroflowImpactLevel = "none" | "low" | "medium" | "high";

export interface MicroflowReference {
  id: string;
  targetMicroflowId: string;
  sourceType: MicroflowReferenceSourceType;
  sourceId?: string;
  sourceName: string;
  sourcePath?: string;
  sourceVersion?: string;
  referencedVersion?: string;
  referenceKind: MicroflowReferenceKind;
  impactLevel: MicroflowImpactLevel;
  description?: string;
  /** 与 `GetMicroflowReferencesRequest.includeInactive` 配合；未指定时由前端视为 `true`。 */
  active?: boolean;
  canNavigate?: boolean;
}

export interface StudioMicroflowCalleeView {
  sourceMicroflowId: string;
  sourceNodeId: string;
  sourceNodeName?: string;
  targetMicroflowId?: string;
  targetMicroflowName?: string;
  targetMicroflowQualifiedName?: string;
  targetModuleId?: string;
  referenceKind: "callMicroflow";
  stale: boolean;
  staleReason?: "missingTargetId" | "targetNotFound" | "selfCall";
}

export interface MicroflowReferenceImpactSummary {
  total: number;
  none: number;
  low: number;
  medium: number;
  high: number;
}
