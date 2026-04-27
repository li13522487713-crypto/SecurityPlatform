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
  for (const [index, item] of issues.entries()) {
    const key = [
      item.id,
      item.message,
      item.flowId,
      item.relatedFlowIds?.join(","),
      index,
    ].filter(Boolean).join(":");
    byId.set(key, item);
  }
  return [...byId.values()].sort((a, b) =>
    severityRank(a.severity) - severityRank(b.severity) ||
    (a.objectId ?? a.flowId ?? a.actionId ?? "").localeCompare(b.objectId ?? b.flowId ?? b.actionId ?? "") ||
    a.code.localeCompare(b.code)
  );
}

function cloneValidationInput<T>(value: T): T {
  if (typeof globalThis.structuredClone === "function") {
    return globalThis.structuredClone(value);
  }
  return JSON.parse(JSON.stringify(value)) as T;
}

function collectRawSchemaShapeIssues(input: unknown): MicroflowValidationIssue[] {
  if (input == null || typeof input !== "object") {
    return [issue(
      "MF_ROOT_SCHEMA_INVALID",
      "Microflow schema must be an object.",
      { source: "schema", fieldPath: "schema" },
      "error",
    )];
  }
  const record = input as Partial<MicroflowAuthoringSchema>;
  const issues: MicroflowValidationIssue[] = [];
  if (!record.objectCollection || typeof record.objectCollection !== "object") {
    issues.push(issue(
      "MF_OBJECT_COLLECTION_MISSING",
      "Microflow must have objectCollection.",
      { source: "schema", fieldPath: "objectCollection" },
      "error",
    ));
  }
  if (!Array.isArray(record.flows)) {
    issues.push(issue(
      "MF_FLOWS_MISSING",
      "Microflow must have flows array.",
      { source: "schema", fieldPath: "flows" },
      "error",
    ));
  }
  if (!Array.isArray(record.parameters)) {
    issues.push(issue(
      "MF_PARAMETERS_MISSING",
      "Microflow must have parameters array.",
      { source: "parameter", fieldPath: "parameters" },
      "error",
    ));
  }
  return issues;
}

function applyMode(issues: MicroflowValidationIssue[], mode: MicroflowValidatorContext["mode"]): MicroflowValidationIssue[] {
  return issues.map(item => {
    if (mode === "edit") {
      if (item.code === "MF_ACTION_REQUIRED_FIELD_MISSING" || item.code.endsWith("_MISSING")) {
        return { ...item, severity: "warning" };
      }
      return item;
    }
    if (mode === "publish" || mode === "testRun") {
      if (
        item.code === "MF_METADATA_CATALOG_MISSING" ||
        item.code.startsWith("MF_METADATA_") ||
        item.code === "MF_ACTION_NANOFLOW_ONLY" ||
        item.code === "MF_ACTION_REQUIRES_CONNECTOR" ||
        (mode === "testRun" && (item.code === "MF_ACTION_MODELED_ONLY" || item.code === "MF_VARIABLE_OUTPUT_MODELED_ONLY" || item.code === "MF_EXPR_UNKNOWN_TYPE"))
      ) {
        return { ...item, severity: "error" };
      }
    }
    return item;
  });
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
        { source: "schema", details: error instanceof Error ? error.message : String(error) },
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
    "元数据目录未加载。结构校验会继续运行，但实体、成员、枚举和引用 stale 检查只能在 metadata 可用后完成。",
    { source: "domainModel", fieldPath: "metadata" },
    "warning",
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
    const rawShapeIssues = collectRawSchemaShapeIssues(input.schema);
    const schema = normalizeMicroflowSchema(cloneValidationInput(input.schema) as unknown);
    if (input.metadata == null) {
      const missing = metadataCatalogMissingIssue();
      const variableIndex = input.variableIndex ?? buildVariableIndex(schema, EMPTY_MICROFLOW_METADATA_CATALOG);
      const mode = input.options?.mode ?? "edit";
      const context: MicroflowValidatorContext = { metadata: EMPTY_MICROFLOW_METADATA_CATALOG, variableIndex, mode };
      const includeWarnings = input.options?.includeWarnings !== false;
      const includeInfo = input.options?.includeInfo === true;
      const issues = applyMode(normalizeIssues([...rawShapeIssues, missing, ...runValidators(schema, context)]), mode).filter(item =>
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
    const metadata = input.metadata;
    const variableIndex = input.variableIndex ?? buildVariableIndex(schema, metadata);
    const mode = input.options?.mode ?? "edit";
    const context: MicroflowValidatorContext = { metadata, variableIndex, mode };
    const includeWarnings = input.options?.includeWarnings !== false;
    const includeInfo = input.options?.includeInfo === true;
    const issues = applyMode(normalizeIssues([...rawShapeIssues, ...runValidators(schema, context)]), mode).filter(item =>
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
  return [metadataCatalogMissingIssue()];
}

export type { MicroflowValidationIssue, MicroflowValidator } from "./validator-types";
