import type { MicroflowTraceFrame } from "../debug/trace-types";
import type { MicroflowNodeViewMode, MicroflowNodeInlineConfig } from "../flowgram/FlowGramMicroflowTypes";
import type { MicroflowDesignSchema, MicroflowValidationIssue, MicroflowWorkflowNodeJSON } from "../schema/types";
import { deriveApprovalNodeInline } from "./approval-node-inline";
import { deriveAnnotationNodeInline } from "./annotation-node-inline";
import { deriveActionNodeInline } from "./action-node-inline";
import { deriveCallMicroflowNodeInline } from "./call-microflow-node-inline";
import { createDefaultInlineConfig, type DeriveNodeInlineInput } from "./default-node-inline";
import { deriveDecisionNodeInline } from "./decision-node-inline";
import { deriveEndNodeInline } from "./end-node-inline";
import { deriveErrorNodeInline } from "./error-node-inline";
import { deriveLoopNodeInline } from "./loop-node-inline";
import { deriveRestNodeInline } from "./rest-node-inline";
import { deriveStartNodeInline } from "./start-node-inline";
import { deriveVariableNodeInline } from "./variable-node-inline";
import { buildNodeInlineVariableOptions } from "./inline-variable-options";
import { appendOutputMappingsInlineSection } from "./output-mappings-inline";

function collectLeafFields(
  value: unknown,
  path: string,
  acc: Array<{ path: string; value: string; editType: "text" | "json" | "expression" }>,
): void {
  if (value === null || value === undefined) {
    return;
  }
  if (typeof value === "string" || typeof value === "number" || typeof value === "boolean") {
    const text = String(value);
    const looksExpression = text.includes("$") || text.includes("${");
    acc.push({ path, value: text, editType: looksExpression ? "expression" : "text" });
    return;
  }
  if (Array.isArray(value)) {
    if (value.length === 0) {
      return;
    }
    acc.push({ path, value: JSON.stringify(value, null, 2), editType: "json" });
    return;
  }
  if (typeof value === "object") {
    const entries = Object.entries(value as Record<string, unknown>);
    if (entries.length === 1 && entries[0]?.[0] === "raw" && typeof entries[0][1] === "string") {
      acc.push({ path: `${path}.raw`, value: entries[0][1], editType: "expression" });
      return;
    }
    for (const [key, child] of entries) {
      collectLeafFields(child, `${path}.${key}`, acc);
    }
  }
}

function appendExtraNodeDataFields(config: MicroflowNodeInlineConfig, deriveInput: DeriveNodeInlineInput): MicroflowNodeInlineConfig {
  const existing = new Set(
    config.sections.flatMap(section => section.fields.map(field => field.fieldPath)).filter(Boolean),
  );
  const extras: Array<{ path: string; value: string; editType: "text" | "json" | "expression" }> = [];
  collectLeafFields(deriveInput.node.data, "data", extras);
  const extraFields = extras
    .filter(item => item.path.startsWith("data."))
    .filter(item => !item.path.startsWith("data.inlineConfig"))
    .filter(item => !existing.has(item.path))
    .slice(0, 48)
    .map((item, index) => ({
      id: `extra-node-${index}`,
      label: item.path.replace(/^data\./, ""),
      value: item.value,
      fieldPath: item.path,
      editType: item.editType,
    }));
  if (extraFields.length === 0) {
    return config;
  }
  const classifySectionKind = (path: string): MicroflowNodeInlineConfig["sections"][number]["kind"] => {
    if (path.includes(".request.") || path.includes(".response.") || path.includes(".headers") || path.includes(".query")) {
      return "http";
    }
    if (path.includes("condition") || path.includes("expression")) {
      return "conditions";
    }
    if (path.includes("branch") || path.includes("caseValues") || path.includes("sourcePortID")) {
      return "branches";
    }
    if (path.includes("output") || path.includes("result") || path.includes("return")) {
      return "outputs";
    }
    if (path.includes("input") || path.includes("parameter") || path.includes("mapping") || path.includes("argument")) {
      return "inputs";
    }
    return "advanced";
  };
  const groups = new Map<string, typeof extraFields>();
  for (const field of extraFields) {
    const kind = classifySectionKind(field.fieldPath);
    const list = groups.get(kind) ?? [];
    list.push(field);
    groups.set(kind, list);
  }
  const sectionTitle: Record<string, string> = {
    inputs: "完整字段 · 输入",
    outputs: "完整字段 · 输出",
    conditions: "完整字段 · 条件",
    branches: "完整字段 · 分支",
    http: "完整字段 · HTTP",
    advanced: "完整字段 · 高级",
  };
  const extraSections = [...groups.entries()].map(([kind, fields]) => ({
    id: `all-node-fields-${kind}`,
    title: sectionTitle[kind] ?? "完整字段",
    kind: kind as MicroflowNodeInlineConfig["sections"][number]["kind"],
    collapsed: true,
    fields,
  }));
  return {
    ...config,
    sections: [
      ...config.sections,
      ...extraSections,
    ],
  };
}

function withDefaultVariableOptions(config: MicroflowNodeInlineConfig, deriveInput: DeriveNodeInlineInput): MicroflowNodeInlineConfig {
  const expressionOptions = buildNodeInlineVariableOptions({
    schema: deriveInput.schema,
    node: deriveInput.node,
    runtimeFrame: deriveInput.runtimeFrame,
    mode: "expression",
  });
  const variableNameOptions = buildNodeInlineVariableOptions({
    schema: deriveInput.schema,
    node: deriveInput.node,
    runtimeFrame: deriveInput.runtimeFrame,
    mode: "name",
  });
  return {
    ...config,
    sections: config.sections.map(section => ({
      ...section,
      fields: section.fields.map(field => {
        if (field.options && field.options.length > 0) {
          return field;
        }
        if (field.editType === "variable" || field.editType === "select" || field.editType === "text") {
          return { ...field, options: variableNameOptions };
        }
        if (
          field.editType === "expression" ||
          field.editType === "condition" ||
          field.editType === "http" ||
          field.editType === "assignment" ||
          field.editType === "mapping" ||
          field.editType === "json"
        ) {
          return { ...field, options: expressionOptions };
        }
        return field;
      }),
    })),
  };
}

function finalizeInlineConfig(config: MicroflowNodeInlineConfig, deriveInput: DeriveNodeInlineInput): MicroflowNodeInlineConfig {
  const withMappings = appendOutputMappingsInlineSection(config, deriveInput);
  return withDefaultVariableOptions(withMappings, deriveInput);
}

export function deriveNodeInlineConfig(input: {
  node: MicroflowWorkflowNodeJSON;
  schema: MicroflowDesignSchema;
  runtimeFrame?: MicroflowTraceFrame;
  issues?: MicroflowValidationIssue[];
  viewMode?: MicroflowNodeViewMode;
}): MicroflowNodeInlineConfig {
  const deriveInput: DeriveNodeInlineInput = {
    node: input.node,
    schema: input.schema,
    runtimeFrame: input.runtimeFrame,
    issues: input.issues,
    viewMode: input.viewMode,
  };
  const data = (input.node.data ?? {}) as Record<string, unknown>;
  const objectKind = String(data.objectKind ?? input.node.type);
  const actionKind = String(data.actionKind ?? (data.action as { kind?: string } | undefined)?.kind ?? "");

  if (objectKind === "startEvent") {
    return finalizeInlineConfig(deriveStartNodeInline(deriveInput), deriveInput);
  }
  if (objectKind === "annotation") {
    return finalizeInlineConfig(deriveAnnotationNodeInline(deriveInput), deriveInput);
  }
  if (objectKind === "endEvent") {
    return finalizeInlineConfig(deriveEndNodeInline(deriveInput), deriveInput);
  }
  if (objectKind === "exclusiveSplit" || objectKind === "inheritanceSplit") {
    return finalizeInlineConfig(deriveDecisionNodeInline(deriveInput), deriveInput);
  }
  if (objectKind === "loopedActivity" || actionKind === "forEach") {
    return finalizeInlineConfig(deriveLoopNodeInline(deriveInput), deriveInput);
  }
  if (objectKind === "errorHandler" || actionKind === "errorHandler") {
    return finalizeInlineConfig(deriveErrorNodeInline(deriveInput), deriveInput);
  }
  if (actionKind === "restCall" || actionKind === "restOperationCall") {
    return finalizeInlineConfig(deriveRestNodeInline(deriveInput), deriveInput);
  }
  if (actionKind === "callMicroflow") {
    return finalizeInlineConfig(deriveCallMicroflowNodeInline(deriveInput), deriveInput);
  }
  if (actionKind === "createVariable" || actionKind === "changeVariable") {
    return finalizeInlineConfig(deriveVariableNodeInline(deriveInput), deriveInput);
  }
  if (actionKind === "completeUserTask" || actionKind === "changeWorkflowState" || objectKind === "tryCatch") {
    return finalizeInlineConfig(deriveApprovalNodeInline(deriveInput), deriveInput);
  }
  if (objectKind === "actionActivity" || actionKind.length > 0) {
    return finalizeInlineConfig(deriveActionNodeInline(deriveInput), deriveInput);
  }
  return finalizeInlineConfig(createDefaultInlineConfig(deriveInput), deriveInput);
}
