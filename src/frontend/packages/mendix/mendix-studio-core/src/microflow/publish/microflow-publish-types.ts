import type { MicroflowReference, MicroflowImpactLevel } from "../references/microflow-reference-types";
import type { MicroflowResource } from "../resource/resource-types";
import type { MicroflowBreakingChange, MicroflowPublishedSnapshot, MicroflowValidationSummary, MicroflowVersionSummary } from "../versions/microflow-version-types";

export interface MicroflowPublishInput {
  version: string;
  description?: string;
  releaseNote?: string;
  force?: boolean;
  confirmBreakingChanges?: boolean;
}

export interface MicroflowPublishImpactAnalysis {
  resourceId: string;
  currentVersion?: string;
  nextVersion: string;
  references: MicroflowReference[];
  breakingChanges: MicroflowBreakingChange[];
  impactLevel: MicroflowImpactLevel;
  summary: {
    referenceCount: number;
    breakingChangeCount: number;
    highImpactCount: number;
    mediumImpactCount: number;
    lowImpactCount: number;
  };
}

export interface MicroflowPublishResult {
  resource: MicroflowResource;
  version: MicroflowVersionSummary;
  snapshot: MicroflowPublishedSnapshot;
  validationSummary: MicroflowValidationSummary;
  impactAnalysis: MicroflowPublishImpactAnalysis;
}

export interface MicroflowVersionValidationResult {
  valid: boolean;
  message?: string;
  warning?: string;
}
