import type { MicroflowRuntimeVariableValue, MicroflowTraceFrame } from "./trace-types";

export type MicroflowRuntimeValueKind = "empty" | "primitive" | "object" | "list" | "json";

export interface MicroflowRuntimeFieldRow {
  path: string;
  type: string;
  value: string;
  rawValue?: unknown;
}

export interface MicroflowRuntimeListTable {
  rowCount: number;
  fieldCount: number;
  columns: string[];
  rows: Array<Record<string, string>>;
  json: string;
}

export interface MicroflowRuntimeValueViewModel {
  name: string;
  type: string;
  source?: string;
  kind: MicroflowRuntimeValueKind;
  summary: string;
  valuePreview: string;
  fields: MicroflowRuntimeFieldRow[];
  list?: MicroflowRuntimeListTable;
  json: string;
}

export interface MicroflowRuntimeValueGroup {
  title: string;
  emptyLabel: string;
  values: MicroflowRuntimeValueViewModel[];
}

const MAX_LIST_SCHEMA_ROWS = 20;
const MAX_TABLE_ROWS = 20;
const MAX_FIELD_ROWS = 80;
const MAX_PREVIEW_LENGTH = 80;

function truncate(text: string, max = MAX_PREVIEW_LENGTH): string {
  return text.length > max ? `${text.slice(0, Math.max(0, max - 1))}...` : text;
}

function stableJson(value: unknown): string {
  try {
    return JSON.stringify(value, null, 2);
  } catch {
    return String(value);
  }
}

function parseRawValue(variable: Partial<MicroflowRuntimeVariableValue>): { value: unknown; json: string } {
  if (typeof variable.rawValueJson === "string" && variable.rawValueJson.trim()) {
    try {
      const value = JSON.parse(variable.rawValueJson);
      return { value, json: stableJson(value) };
    } catch {
      return { value: variable.valuePreview, json: variable.rawValueJson };
    }
  }
  if (variable.rawValue !== undefined) {
    return { value: variable.rawValue, json: stableJson(variable.rawValue) };
  }
  return { value: variable.valuePreview ?? "", json: stableJson(variable.valuePreview ?? "") };
}

function typeLabel(type: MicroflowRuntimeVariableValue["type"] | undefined, value: unknown): string {
  if (type && typeof type === "object" && "kind" in type && typeof type.kind === "string") {
    return type.kind;
  }
  if (Array.isArray(value)) {
    return "list";
  }
  if (value === null) {
    return "null";
  }
  return typeof value === "object" ? "object" : typeof value;
}

function primitivePreview(value: unknown): string {
  if (value === null) {
    return "null";
  }
  if (value === undefined) {
    return "";
  }
  if (typeof value === "string") {
    return truncate(value);
  }
  if (typeof value === "number" || typeof value === "boolean") {
    return String(value);
  }
  return truncate(stableJson(value).replace(/\s+/g, " "));
}

function valueKind(value: unknown): MicroflowRuntimeValueKind {
  if (value === null || value === undefined || value === "") {
    return "empty";
  }
  if (Array.isArray(value)) {
    return "list";
  }
  if (typeof value === "object") {
    return "object";
  }
  return "primitive";
}

function flattenObject(value: unknown, prefix = ""): MicroflowRuntimeFieldRow[] {
  if (!value || typeof value !== "object" || Array.isArray(value)) {
    return [{
      path: prefix || "value",
      type: value === null ? "null" : typeof value,
      value: primitivePreview(value),
      rawValue: value,
    }];
  }
  const rows: MicroflowRuntimeFieldRow[] = [];
  for (const [key, child] of Object.entries(value as Record<string, unknown>)) {
    const path = prefix ? `${prefix}.${key}` : key;
    if (child && typeof child === "object" && !Array.isArray(child)) {
      rows.push(...flattenObject(child, path));
      continue;
    }
    rows.push({
      path,
      type: Array.isArray(child) ? "list" : child === null ? "null" : typeof child,
      value: primitivePreview(child),
      rawValue: child,
    });
  }
  return rows.slice(0, MAX_FIELD_ROWS);
}

function collectListColumns(items: unknown[]): string[] {
  const columns = new Set<string>();
  const sample = items.slice(0, MAX_LIST_SCHEMA_ROWS);
  for (const item of sample) {
    if (!item || typeof item !== "object" || Array.isArray(item)) {
      columns.add("value");
      continue;
    }
    for (const row of flattenObject(item)) {
      columns.add(row.path);
    }
  }
  return Array.from(columns).slice(0, 12);
}

function readPath(value: unknown, path: string): unknown {
  if (path === "value") {
    return value;
  }
  return path.split(".").reduce<unknown>((current, part) => {
    if (!current || typeof current !== "object" || Array.isArray(current)) {
      return undefined;
    }
    return (current as Record<string, unknown>)[part];
  }, value);
}

function buildListTable(value: unknown[]): MicroflowRuntimeListTable {
  const columns = collectListColumns(value);
  return {
    rowCount: value.length,
    fieldCount: columns.length,
    columns,
    rows: value.slice(0, MAX_TABLE_ROWS).map((item, index) => {
      const row: Record<string, string> = { "#": String(index + 1) };
      for (const column of columns) {
        row[column] = primitivePreview(readPath(item, column));
      }
      return row;
    }),
    json: stableJson(value),
  };
}

function variableToViewModel(variableName: string, variable: Partial<MicroflowRuntimeVariableValue>): MicroflowRuntimeValueViewModel {
  const parsed = parseRawValue(variable);
  const kind = valueKind(parsed.value);
  const label = variable.name || variableName;
  const inferredType = typeLabel(variable.type, parsed.value);
  const fields = kind === "object" ? flattenObject(parsed.value) : [];
  const list = Array.isArray(parsed.value) ? buildListTable(parsed.value) : undefined;
  const summary = kind === "list"
    ? `${label}[${list?.rowCount ?? 0}]`
    : kind === "object"
      ? `${label}{${fields.length}}`
      : `${label} = ${primitivePreview(parsed.value) || variable.valuePreview || ""}`;
  return {
    name: label,
    type: inferredType,
    source: variable.source,
    kind,
    summary,
    valuePreview: variable.valuePreview || primitivePreview(parsed.value),
    fields,
    list,
    json: parsed.json,
  };
}

function recordToValues(record: Record<string, MicroflowRuntimeVariableValue> | undefined): MicroflowRuntimeValueViewModel[] {
  return Object.entries(record ?? {}).map(([name, variable]) => variableToViewModel(name, variable));
}

export function buildRuntimeValueGroups(frame: MicroflowTraceFrame): {
  inputs: MicroflowRuntimeValueGroup;
  outputs: MicroflowRuntimeValueGroup;
  variables: MicroflowRuntimeValueGroup;
  outputSummaries: string[];
} {
  const inputs = recordToValues(frame.inputVariables);
  const outputs = recordToValues(frame.outputVariables);
  const variables = recordToValues(frame.variablesSnapshot);
  const fallbackOutput = outputs.length === 0 && frame.output !== undefined
    ? [variableToViewModel("output", {
        name: "output",
        rawValue: frame.output,
        valuePreview: primitivePreview(frame.output),
      })]
    : outputs;
  return {
    inputs: { title: "inputs", emptyLabel: "no inputs", values: inputs },
    outputs: { title: "outputs", emptyLabel: "no outputs", values: fallbackOutput },
    variables: { title: "variables", emptyLabel: "no variables", values: variables },
    outputSummaries: (fallbackOutput.length > 0 ? fallbackOutput : outputs).map(item => item.summary),
  };
}
