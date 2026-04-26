import { mockMicroflowMetadataCatalog } from "../metadata";
import { normalizeMicroflowSchema } from "../schema/legacy/legacy-migration";
import type { MicroflowAuthoringSchema, MicroflowValidationIssue, MicroflowValidator } from "../schema/types";
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
import type { MicroflowValidationInput, MicroflowValidationResult, MicroflowValidationSummary } from "./validator-types";

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

function runValidators(schema: MicroflowAuthoringSchema): MicroflowValidationIssue[] {
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
    { validate: validateReachability }
  ];
  const issues: MicroflowValidationIssue[] = [];
  for (const validator of validators) {
    try {
      issues.push(...validator.validate(schema));
    } catch (error) {
      issues.push(issue(
        "MF_ROOT_SCHEMA_INVALID",
        "Validator failed while checking this microflow schema.",
        { source: "root", details: error instanceof Error ? error.message : String(error) },
        "error"
      ));
    }
  }
  return normalizeIssues(issues);
}

function isValidationInput(value: unknown): value is MicroflowValidationInput {
  return Boolean(value && typeof value === "object" && "schema" in value);
}

export function validateMicroflowSchema(schema: MicroflowAuthoringSchema): MicroflowValidationIssue[];
export function validateMicroflowSchema(input: MicroflowValidationInput): MicroflowValidationResult;
export function validateMicroflowSchema(input: MicroflowAuthoringSchema | MicroflowValidationInput | unknown): MicroflowValidationIssue[] | MicroflowValidationResult {
  if (isValidationInput(input)) {
    const metadata = input.metadata ?? mockMicroflowMetadataCatalog;
    const schema = normalizeMicroflowSchema(input.schema as unknown);
    const variableIndex = input.variableIndex ?? buildVariableIndex(schema, metadata);
    const includeWarnings = input.options?.includeWarnings !== false;
    const includeInfo = input.options?.includeInfo === true;
    const issues = runValidators(schema).filter(item =>
      item.severity === "error" ||
      (item.severity === "warning" && includeWarnings) ||
      (item.severity === "info" && includeInfo)
    );
    return {
      issues,
      variableIndex,
      summary: summarizeIssues(issues),
    };
  }
  return runValidators(normalizeMicroflowSchema(input));
}
export type { MicroflowValidationIssue, MicroflowValidator } from "./validator-types";
