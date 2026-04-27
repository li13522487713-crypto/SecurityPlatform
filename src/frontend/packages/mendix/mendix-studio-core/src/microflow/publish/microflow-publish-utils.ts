import { validateMicroflowSchema } from "@atlas/microflow";
import type { MicroflowAuthoringSchema } from "@atlas/microflow";
import type { MicroflowMetadataCatalog } from "@atlas/microflow/metadata";

import type { MicroflowReference } from "../references/microflow-reference-types";
import { diffMicroflowSchemas } from "../versions/microflow-version-diff";
import type { MicroflowBreakingChange, MicroflowPublishedSnapshot, MicroflowValidationSummary, MicroflowVersionSummary } from "../versions/microflow-version-types";
import type { MicroflowPublishImpactAnalysis, MicroflowVersionValidationResult } from "./microflow-publish-types";

const SEMVER_LIKE = /^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$/u;

export function validatePublishVersion(version: string, existingVersions: MicroflowVersionSummary[]): MicroflowVersionValidationResult {
  const normalized = version.trim();
  if (!normalized) {
    return { valid: false, message: "版本号必填。" };
  }
  if (!SEMVER_LIKE.test(normalized)) {
    return { valid: false, message: "版本号需符合 1.0.0 或 1.0.0-beta.1 格式。" };
  }
  if (existingVersions.some(item => item.status === "published" && item.version === normalized)) {
    return { valid: false, message: "该发布版本已存在。" };
  }
  if (normalized.includes("draft")) {
    return { valid: true, warning: "发布版本不推荐包含 draft 后缀。" };
  }
  return { valid: true };
}

export function summarizeValidation(schema: MicroflowAuthoringSchema, metadata?: MicroflowMetadataCatalog): MicroflowValidationSummary {
  const result = validateMicroflowSchema({
    schema,
    metadata,
    options: { mode: "publish", includeWarnings: true, includeInfo: true },
  });
  return result.summary;
}

export function analyzeMicroflowPublishImpact(input: {
  resourceId: string;
  currentVersion?: string;
  nextVersion: string;
  currentSchema: MicroflowAuthoringSchema;
  latestSnapshot?: MicroflowPublishedSnapshot;
  references: MicroflowReference[];
}): MicroflowPublishImpactAnalysis {
  const diff = input.latestSnapshot ? diffMicroflowSchemas(input.latestSnapshot.schema, input.currentSchema) : undefined;
  const breakingChanges: MicroflowBreakingChange[] = diff?.breakingChanges ?? [];
  const highImpactCount = breakingChanges.filter(item => item.severity === "high").length;
  const mediumImpactCount = breakingChanges.filter(item => item.severity === "medium").length;
  const lowImpactCount = breakingChanges.filter(item => item.severity === "low").length;
  const impactLevel = highImpactCount > 0 ? "high" : mediumImpactCount > 0 ? "medium" : lowImpactCount > 0 ? "low" : "none";

  return {
    resourceId: input.resourceId,
    currentVersion: input.currentVersion,
    nextVersion: input.nextVersion,
    references: input.references,
    breakingChanges,
    impactLevel,
    summary: {
      referenceCount: input.references.length,
      breakingChangeCount: breakingChanges.length,
      highImpactCount,
      mediumImpactCount,
      lowImpactCount
    }
  };
}

export function hashSchemaSnapshot(schema: MicroflowAuthoringSchema): string {
  let hash = 0;
  const value = JSON.stringify(schema);
  for (let index = 0; index < value.length; index += 1) {
    hash = (hash << 5) - hash + value.charCodeAt(index);
    hash |= 0;
  }
  return Math.abs(hash).toString(16);
}
