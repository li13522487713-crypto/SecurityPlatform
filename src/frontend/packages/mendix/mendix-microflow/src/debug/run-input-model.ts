import type { TestRunMicroflowRequest } from "../runtime-adapter/types";
import type { MicroflowDataType, MicroflowObjectCollection, MicroflowParameter, MicroflowSchema, MicroflowValidationIssue } from "../schema/types";
import type { MicroflowTestRunOptions } from "./trace-types";

export type MicroflowRunInputControlKind = "text" | "number" | "boolean" | "dateTime" | "json" | "readonly";

export interface MicroflowRunInputField {
  parameter: MicroflowParameter;
  controlKind: MicroflowRunInputControlKind;
  typeLabel: string;
  defaultValue: unknown;
  warning?: string;
}

export interface MicroflowRunInputModel {
  microflowId: string;
  schemaVersion: string;
  fields: MicroflowRunInputField[];
  warnings: string[];
}

export interface MicroflowRunInputValidationResult {
  valid: boolean;
  values: Record<string, unknown>;
  errors: Record<string, string>;
}

export interface MicroflowRunPanelState {
  runInputsByMicroflowId: Record<string, Record<string, unknown>>;
  runResultByMicroflowId: Record<string, unknown>;
  runErrorByMicroflowId: Record<string, string | undefined>;
  activeRunIdByMicroflowId: Record<string, string | undefined>;
}

export type MicroflowRunDirtyStrategy = "saveAndRun" | "blockUntilSaved" | "runDraftSchema";

export function buildRunInputModel(schema: MicroflowSchema): MicroflowRunInputModel {
  const schemaParameters = Array.isArray(schema.parameters) ? schema.parameters : [];
  const nodeParameters = collectParameterObjects(schema.objectCollection).map(object => {
    const existing = schemaParameters.find(parameter => parameter.id === object.parameterId);
    return existing ?? {
      id: object.parameterId,
      stableId: object.parameterId,
      name: object.parameterName ?? object.caption ?? object.parameterId,
      dataType: { kind: "unknown" as const, reason: "schema-level parameter missing" },
      required: true,
      documentation: object.documentation,
    };
  });
  const parameters = schemaParameters.length > 0 ? schemaParameters : nodeParameters;
  const warnings: string[] = [];

  if (schemaParameters.length === 0 && nodeParameters.length > 0) {
    warnings.push("schema-level parameters missing; falling back to Parameter nodes.");
  }
  if (schemaParameters.length > 0 && hasParameterNodeMismatch(schemaParameters, nodeParameters)) {
    warnings.push("schema-level parameters and Parameter nodes differ; schema-level parameters are used.");
  }

  return {
    microflowId: schema.id,
    schemaVersion: schema.schemaVersion,
    fields: parameters.map(parameter => ({
      parameter,
      controlKind: controlKindForDataType(parameter.dataType),
      typeLabel: dataTypeLabel(parameter.dataType),
      defaultValue: defaultRunInputValue(parameter),
      warning: warningForDataType(parameter.dataType),
    })),
    warnings,
  };
}

export function buildDefaultRunInputValues(model: MicroflowRunInputModel): Record<string, unknown> {
  return Object.fromEntries(model.fields.map(field => [field.parameter.name, field.defaultValue]));
}

export function validateRunInputs(model: MicroflowRunInputModel, values: Record<string, unknown>): MicroflowRunInputValidationResult {
  const errors: Record<string, string> = {};
  const coercedValues: Record<string, unknown> = {};

  for (const field of model.fields) {
    const name = field.parameter.name;
    const rawValue = values[name];
    if (field.parameter.required && isEmptyRunInputValue(rawValue)) {
      errors[name] = "必填参数不能为空";
      continue;
    }
    if (!field.parameter.required && isEmptyRunInputValue(rawValue)) {
      continue;
    }
    const coerced = coerceRunInputValue(rawValue, field.parameter.dataType);
    if (coerced.error) {
      errors[name] = coerced.error;
      continue;
    }
    coercedValues[name] = coerced.value;
  }

  return { valid: Object.keys(errors).length === 0, values: coercedValues, errors };
}

export function coerceRunInputValue(rawValue: unknown, dataType: MicroflowDataType): { value: unknown; error?: string } {
  if (rawValue === undefined || rawValue === null || rawValue === "") {
    return { value: undefined };
  }
  if (dataType.kind === "integer" || dataType.kind === "long") {
    const value = typeof rawValue === "number" ? rawValue : Number(String(rawValue).trim());
    return Number.isFinite(value) && Number.isInteger(value) ? { value } : { value: rawValue, error: "请输入整数" };
  }
  if (dataType.kind === "decimal") {
    const value = typeof rawValue === "number" ? rawValue : Number(String(rawValue).trim());
    return Number.isFinite(value) ? { value } : { value: rawValue, error: "请输入数字" };
  }
  if (dataType.kind === "boolean") {
    if (typeof rawValue === "boolean") {
      return { value: rawValue };
    }
    if (String(rawValue).toLowerCase() === "true") {
      return { value: true };
    }
    if (String(rawValue).toLowerCase() === "false") {
      return { value: false };
    }
    return { value: rawValue, error: "请输入 true 或 false" };
  }
  if (dataType.kind === "list") {
    const parsed = parseJsonInput(rawValue);
    if (!parsed.ok) {
      return { value: rawValue, error: parsed.error };
    }
    return Array.isArray(parsed.value) ? { value: parsed.value } : { value: rawValue, error: "请输入 JSON array" };
  }
  if (dataType.kind === "object" || dataType.kind === "json" || dataType.kind === "fileDocument" || dataType.kind === "unknown") {
    const parsed = parseJsonInput(rawValue);
    return parsed.ok ? { value: parsed.value } : { value: rawValue, error: parsed.error };
  }
  if (dataType.kind === "void") {
    return { value: undefined };
  }
  return { value: String(rawValue) };
}

export function buildRunRequest(schema: MicroflowSchema, values: Record<string, unknown>, options?: MicroflowTestRunOptions): TestRunMicroflowRequest {
  return { microflowId: schema.id, schema, input: values, options };
}

export function shouldBlockRun(
  validationIssues: Pick<MicroflowValidationIssue, "severity">[],
  inputErrors: Record<string, string>,
  dirty: boolean,
  dirtyStrategy: MicroflowRunDirtyStrategy = "saveAndRun",
): { blocked: boolean; reason?: "validation" | "inputs" | "dirty" } {
  if (validationIssues.some(issue => issue.severity === "error")) {
    return { blocked: true, reason: "validation" };
  }
  if (Object.keys(inputErrors).length > 0) {
    return { blocked: true, reason: "inputs" };
  }
  if (dirty && dirtyStrategy === "blockUntilSaved") {
    return { blocked: true, reason: "dirty" };
  }
  return { blocked: false };
}

export function updateRunInputsForMicroflow(state: MicroflowRunPanelState, microflowId: string, values: Record<string, unknown>): MicroflowRunPanelState {
  return { ...state, runInputsByMicroflowId: { ...state.runInputsByMicroflowId, [microflowId]: values } };
}

export function updateRunResultForMicroflow(state: MicroflowRunPanelState, microflowId: string, result: unknown, runId?: string): MicroflowRunPanelState {
  return {
    ...state,
    runResultByMicroflowId: { ...state.runResultByMicroflowId, [microflowId]: result },
    runErrorByMicroflowId: { ...state.runErrorByMicroflowId, [microflowId]: undefined },
    activeRunIdByMicroflowId: { ...state.activeRunIdByMicroflowId, [microflowId]: runId },
  };
}

export function updateRunErrorForMicroflow(state: MicroflowRunPanelState, microflowId: string, error: string): MicroflowRunPanelState {
  return { ...state, runErrorByMicroflowId: { ...state.runErrorByMicroflowId, [microflowId]: error } };
}

function controlKindForDataType(dataType: MicroflowDataType): MicroflowRunInputControlKind {
  if (dataType.kind === "boolean") return "boolean";
  if (dataType.kind === "integer" || dataType.kind === "long" || dataType.kind === "decimal") return "number";
  if (dataType.kind === "dateTime") return "dateTime";
  if (dataType.kind === "object" || dataType.kind === "list" || dataType.kind === "json" || dataType.kind === "fileDocument" || dataType.kind === "unknown") return "json";
  if (dataType.kind === "void" || dataType.kind === "binary") return "readonly";
  return "text";
}

function defaultRunInputValue(parameter: MicroflowParameter): unknown {
  if (parameter.exampleValue !== undefined) {
    return coerceRunInputValue(parameter.exampleValue, parameter.dataType).value ?? parameter.exampleValue;
  }
  if (parameter.defaultValue?.raw !== undefined) {
    return expressionDefaultValue(parameter.defaultValue.raw, parameter.dataType);
  }
  if (parameter.dataType.kind === "boolean") return false;
  if (parameter.dataType.kind === "object" || parameter.dataType.kind === "json" || parameter.dataType.kind === "unknown") return "{}";
  if (parameter.dataType.kind === "list") return "[]";
  return "";
}

function expressionDefaultValue(raw: string, dataType: MicroflowDataType): unknown {
  const trimmed = raw.trim();
  if ((trimmed.startsWith("'") && trimmed.endsWith("'")) || (trimmed.startsWith("\"") && trimmed.endsWith("\""))) {
    return trimmed.slice(1, -1);
  }
  if (dataType.kind === "boolean" && (trimmed === "true" || trimmed === "false")) return trimmed === "true";
  if ((dataType.kind === "integer" || dataType.kind === "long" || dataType.kind === "decimal") && Number.isFinite(Number(trimmed))) return Number(trimmed);
  return trimmed;
}

function dataTypeLabel(dataType: MicroflowDataType): string {
  if (dataType.kind === "enumeration") return `Enumeration<${dataType.enumerationQualifiedName}>`;
  if (dataType.kind === "object") return `Object<${dataType.entityQualifiedName}>`;
  if (dataType.kind === "fileDocument") return `FileDocument<${dataType.entityQualifiedName ?? "System.FileDocument"}>`;
  if (dataType.kind === "list") return `List<${dataTypeLabel(dataType.itemType)}>`;
  if (dataType.kind === "unknown") return `Unknown${dataType.reason ? ` (${dataType.reason})` : ""}`;
  return dataType.kind;
}

function warningForDataType(dataType: MicroflowDataType): string | undefined {
  if (dataType.kind === "object" || dataType.kind === "fileDocument") return "本轮不接真实对象选择器，请输入 JSON object。";
  if (dataType.kind === "list") return "本轮不接真实列表选择器，请输入 JSON array。";
  if (dataType.kind === "unknown") return "未知类型会按 JSON 提交，后端可能拒绝。";
  if (dataType.kind === "binary" || dataType.kind === "void") return "该类型不能作为可编辑运行输入。";
  return undefined;
}

function isEmptyRunInputValue(value: unknown): boolean {
  return value === undefined || value === null || value === "";
}

function parseJsonInput(rawValue: unknown): { ok: true; value: unknown } | { ok: false; error: string } {
  if (typeof rawValue !== "string") {
    return { ok: true, value: rawValue };
  }
  if (!rawValue.trim()) {
    return { ok: true, value: undefined };
  }
  try {
    return { ok: true, value: JSON.parse(rawValue) };
  } catch {
    return { ok: false, error: "请输入合法 JSON" };
  }
}

function collectParameterObjects(collection: MicroflowObjectCollection): Array<Extract<MicroflowObjectCollection["objects"][number], { kind: "parameterObject" }>> {
  const result: Array<Extract<MicroflowObjectCollection["objects"][number], { kind: "parameterObject" }>> = [];
  for (const object of collection.objects) {
    if (object.kind === "parameterObject") result.push(object);
    if (object.kind === "loopedActivity") result.push(...collectParameterObjects(object.objectCollection));
  }
  return result;
}

function hasParameterNodeMismatch(schemaParameters: MicroflowParameter[], nodeParameters: MicroflowParameter[]): boolean {
  if (nodeParameters.length === 0) return false;
  const schemaNames = new Map(schemaParameters.map(parameter => [parameter.id, parameter.name]));
  return nodeParameters.some(parameter => schemaNames.get(parameter.id) !== parameter.name);
}
