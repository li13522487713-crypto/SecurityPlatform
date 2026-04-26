import { EMPTY_MICROFLOW_METADATA_CATALOG } from "../metadata/metadata-catalog";
import { normalizeMicroflowSchema } from "../schema/legacy/legacy-migration";
import type { MicroflowAuthoringSchema, MicroflowValidationIssue } from "../schema/types";
import { buildVariableIndex } from "../variables";
import { validateActions } from "./validate-actions";
import { validateDecisions } from "./validate-decisions";
import { validateErrorHandling } from "./validate-error-handling";
import { validateEvents } from "./validate-events";
import { validateExpressions } from "./validate-expressions";
import { validateFlows } from "./validate-flows";
import { validateLoop } from "./validate-loop";
import { validateMetadataReferences } from "./validate-metadata-references";
import { validateObjectCollection } from "./validate-object-collection";
import { validateReachability } from "./validate-reachability";
import { validateRoot } from "./validate-root";
import { validateVariables } from "./validate-variables";
import { issue } from "./shared";
import type {
  MicroflowValidationInput,
  MicroflowValidationResult,
  MicroflowValidationSummary,
  MicroflowValidator,
  MicroflowValidatorContext,
} from "./validator-types";

function summarizeIssues(issues: MicroflowValidationIssue[]): MicroflowValidationSummary {
  return {
    errorCount: issues.filter(item => item.severity === "error").length,
    warningCount: issues.filter(item => item.severity === "warning").length,
    infoCount: issues.filter(item => item.severity === "info").length,
  };
}

function severityRank(severity: MicroflowValidationIssue["severity"]): number {
  if (severity === "error") {
    return 0;
  }
  if (severity === "warning") {
    return 1;
  }
  return 2;
}

function normalizeIssues(issues: MicroflowValidationIssue[]): MicroflowValidationIssue[] {
  const byId = new Map<string, MicroflowValidationIssue>();
  for (const item of issues) {
    byId.set(item.id, item);
  }
  return [...byId.values()].sort((a, b) =>
    severityRank(a.severity) - severityRank(b.severity) ||
    (a.objectId ?? a.flowId ?? a.actionId ?? "").localeCompare(b.objectId ?? b.flowId ?? b.actionId ?? "") ||
    a.code.localeCompare(b.code)
  );
}

function runValidators(schema: MicroflowAuthoringSchema, context: MicroflowValidatorContext): MicroflowValidationIssue[] {
  const validators: MicroflowValidator[] = [
    { validate: validateRoot },
    { validate: validateObjectCollection },
    { validate: validateFlows },
    { validate: validateEvents },
    { validate: validateDecisions },
    { validate: validateLoop },
    { validate: validateActions },
    { validate: validateMetadataReferences },
    { validate: validateVariables },
    { validate: validateExpressions },
    { validate: validateErrorHandling },
    { validate: validateReachability },
  ];
  const issues: MicroflowValidationIssue[] = [];
  for (const validator of validators) {
    try {
      issues.push(...validator.validate(schema, context));
    } catch (error) {
      issues.push(issue(
        "MF_ROOT_SCHEMA_INVALID",
        "Validator failed while checking this microflow schema.",
        { source: "root", details: error instanceof Error ? error.message : String(error) },
        "error",
      ));
    }
  }
  return normalizeIssues(issues);
}

function isValidationInput(value: unknown): value is MicroflowValidationInput {
  return Boolean(value && typeof value === "object" && "schema" in value && "metadata" in value);
}

function metadataCatalogMissingIssue(): MicroflowValidationIssue {
  return issue(
    "MF_METADATA_CATALOG_MISSING",
    "元数据目录未加载。请通过 MicroflowMetadataProvider / Adapter 注入后再校验。",
    { source: "root", fieldPath: "metadata" },
    "error",
  );
}

/**
 * 统一校验入口：必须显式传入 `metadata`（可为已加载目录；`null`/`undefined` 仅产生 `MF_METADATA_CATALOG_MISSING`）。
 */
export function validateMicroflowSchema(input: MicroflowValidationInput): MicroflowValidationResult;
/** @deprecated 请使用 {@link validateMicroflowSchema}({ schema, metadata })；schema-only 调用不再回落 mock，仅返回元数据缺失问题。 */
export function validateMicroflowSchema(schema: MicroflowAuthoringSchema): MicroflowValidationIssue[];
export function validateMicroflowSchema(input: MicroflowAuthoringSchema | MicroflowValidationInput | unknown): MicroflowValidationResult | MicroflowValidationIssue[] {
  if (isValidationInput(input)) {
    if (input.metadata == null) {
      const missing = metadataCatalogMissingIssue();
      const schema = normalizeMicroflowSchema(input.schema as unknown);
      const variableIndex = input.variableIndex ?? buildVariableIndex(schema, EMPTY_MICROFLOW_METADATA_CATALOG);
      return {
        issues: [missing],
        variableIndex,
        summary: summarizeIssues([missing]),
      };
    }
    const schema = normalizeMicroflowSchema(input.schema as unknown);
    const metadata = input.metadata;
    const variableIndex = input.variableIndex ?? buildVariableIndex(schema, metadata);
    const context: MicroflowValidatorContext = { metadata, variableIndex };
    const includeWarnings = input.options?.includeWarnings !== false;
    const includeInfo = input.options?.includeInfo === true;
    const issues = runValidators(schema, context).filter(item =>
      item.severity === "error" ||
      (item.severity === "warning" && includeWarnings) ||
      (item.severity === "info" && includeInfo),
    );
    return {
      issues,
      variableIndex,
      summary: summarizeIssues(issues),
    };
  }
  const legacySchema = normalizeMicroflowSchema(input as MicroflowAuthoringSchema);
  return [metadataCatalogMissingIssue()];
}

export type { MicroflowValidationIssue, MicroflowValidator } from "./validator-types";
