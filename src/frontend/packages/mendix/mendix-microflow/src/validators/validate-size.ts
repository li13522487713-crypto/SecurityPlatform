import type { MicroflowSchema, MicroflowValidationIssue } from "../schema/types";
import { collectMicroflowBestPracticeWarnings, MICROFLOW_LIMITS, summarizeMicroflowComplexity, validateVariableNames } from "../utils/microflow-validator";
import { issue } from "./shared";
import type { MicroflowValidatorContext } from "./validator-types";

export function validateMicroflowSize(schema: MicroflowSchema, _context: MicroflowValidatorContext): MicroflowValidationIssue[] {
  const issues: MicroflowValidationIssue[] = [];
  const summary = summarizeMicroflowComplexity(schema);

  if (summary.totalElements >= MICROFLOW_LIMITS.ERROR_LEVEL) {
    issues.push(issue(
      "MF_TOO_LARGE",
      `微流包含 ${summary.totalElements} 个元素，超过推荐上限 25 个。建议将部分逻辑提取为子微流。`,
      {
        microflowId: schema.id,
        source: "schema",
        fieldPath: "objectCollection",
        blockSave: false,
        blockPublish: false,
      },
      "warning",
    ));
  } else if (summary.totalElements >= MICROFLOW_LIMITS.WARN_LEVEL) {
    issues.push(issue(
      "MF_APPROACHING_LIMIT",
      `微流包含 ${summary.totalElements} 个元素，接近推荐上限 25 个。`,
      {
        microflowId: schema.id,
        source: "schema",
        fieldPath: "objectCollection",
        blockSave: false,
        blockPublish: false,
      },
      "warning",
    ));
  }

  if (summary.annotationRecommended && !summary.hasAnnotation) {
    issues.push(issue(
      "MF_MISSING_ANNOTATION",
      "复杂微流（超过 10 个活动或 2 个 Decision）建议在起始处添加注释说明目的和参数。",
      {
        microflowId: schema.id,
        source: "schema",
        fieldPath: "objectCollection",
        blockSave: false,
        blockPublish: false,
      },
      "warning",
    ));
  }

  issues.push(...collectMicroflowBestPracticeWarnings(schema).map(warning => issue(
    warning.code,
    warning.message,
    {
      microflowId: schema.id,
      source: "action",
      objectId: warning.objectId,
      fieldPath: warning.fieldPath,
      blockSave: false,
      blockPublish: false,
    },
    warning.severity,
  )));

  issues.push(...validateVariableNames(schema).map(conflict => issue(
    conflict.code,
    conflict.message,
    {
      microflowId: schema.id,
      source: "schema",
      objectId: conflict.nodeIds[0],
      fieldPath: "objectCollection",
      relatedObjectIds: conflict.nodeIds.slice(1),
      blockSave: false,
      blockPublish: false,
    },
    "error",
  )));

  return issues;
}
