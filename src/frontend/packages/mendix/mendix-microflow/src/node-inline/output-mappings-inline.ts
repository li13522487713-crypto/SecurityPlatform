import type {
  FlowGramMicroflowNodeData,
  MicroflowNodeInlineConfig,
  MicroflowOutputMapping,
} from "../flowgram/FlowGramMicroflowTypes";
import type { MicroflowWorkflowNodeJSON } from "../schema/types";
import type { DeriveNodeInlineInput } from "./default-node-inline";
import { buildNodeInlineVariableOptions } from "./inline-variable-options";

type JsonObject = Record<string, unknown>;

function asString(value: unknown): string | undefined {
  if (typeof value !== "string") {
    return undefined;
  }
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : undefined;
}

function mappingPreview(mapping: MicroflowOutputMapping): string {
  if (mapping.source === "variable") {
    return `$${mapping.variableName ?? ""}`;
  }
  if (mapping.source === "expression") {
    return mapping.expression ?? "";
  }
  if (typeof mapping.constantValue === "string") {
    return mapping.constantValue;
  }
  return JSON.stringify(mapping.constantValue);
}

function sanitizeOutputMapping(input: unknown, index: number): MicroflowOutputMapping | undefined {
  if (!input || typeof input !== "object") {
    return undefined;
  }
  const record = input as JsonObject;
  const source = asString(record.source) as MicroflowOutputMapping["source"] | undefined;
  if (source !== "variable" && source !== "constant" && source !== "expression") {
    return undefined;
  }
  const key = asString(record.key) ?? `output_${index + 1}`;
  if (source === "variable") {
    return {
      key,
      source,
      variableName: asString(record.variableName) ?? "",
    };
  }
  if (source === "expression") {
    return {
      key,
      source,
      expression: asString(record.expression) ?? "",
    };
  }
  return {
    key,
    source,
    constantValue: record.constantValue,
  };
}

function resolveOutputMappings(input: DeriveNodeInlineInput): MicroflowOutputMapping[] {
  const data = (input.node.data ?? {}) as JsonObject;
  const rawMappings = data.outputMappings;
  if (Array.isArray(rawMappings)) {
    const parsed = rawMappings
      .map((item, index) => sanitizeOutputMapping(item, index))
      .filter((item): item is MicroflowOutputMapping => Boolean(item));
    if (parsed.length > 0) {
      return parsed;
    }
  }
  return [];
}

function isExecutableNode(node: MicroflowWorkflowNodeJSON): boolean {
  const data = (node.data ?? {}) as Partial<FlowGramMicroflowNodeData>;
  const objectKind = String(data.objectKind ?? node.type ?? "");
  return objectKind !== "startEvent" && objectKind !== "annotation" && objectKind !== "parameterObject";
}

export function appendOutputMappingsInlineSection(config: MicroflowNodeInlineConfig, input: DeriveNodeInlineInput): MicroflowNodeInlineConfig {
  if (!isExecutableNode(input.node)) {
    return config;
  }

  const mappings = resolveOutputMappings(input);
  if (mappings.length === 0) {
    return config;
  }

  const variableNameOptions = buildNodeInlineVariableOptions({
    schema: input.schema,
    node: input.node,
    runtimeFrame: input.runtimeFrame,
    mode: "name",
  });

  const field = {
    id: "output-mappings",
    label: "输出字段",
    value: JSON.stringify(mappings),
    fieldPath: "data.outputMappings",
    editType: "outputMappings" as const,
    placeholder: "配置输出字段",
    options: variableNameOptions,
  };

  const section = {
    id: "output-mappings",
    title: "输出字段映射",
    kind: "outputs" as const,
    maxVisibleRows: 1,
    fields: [field],
  };

  const summary = mappings.slice(0, 3).map((item, index) => ({
    id: `output-mapping-${index}`,
    kind: "output" as const,
    label: item.key,
    value: mappingPreview(item),
    fieldPath: "data.outputMappings",
    editable: true,
  }));

  return {
    ...config,
    summaryLines: [...summary, ...config.summaryLines].slice(0, 6),
    sections: [section, ...config.sections],
  };
}
