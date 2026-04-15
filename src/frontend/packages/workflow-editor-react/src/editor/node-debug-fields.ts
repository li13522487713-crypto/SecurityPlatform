import type { CanvasNode } from "./workflow-editor-state";

export type NodeDebugFieldKind = "text" | "number" | "boolean" | "json";

export interface NodeDebugFieldDefinition {
  path: string;
  label: string;
  kind: NodeDebugFieldKind;
  source: "inputMapping" | "inputSource" | "configPath" | "templateRef";
}

const TEMPLATE_VARIABLE_REGEX = /{{\s*([^{}]+?)\s*}}/g;
const VARIABLE_PATH_CONFIG_KEYS = new Set([
  "inputPath",
  "answerPath",
  "collectionPath",
  "inputArrayPath",
  "inputsVariable",
  "inputVariable"
]);

function normalizePath(raw: string): string {
  const trimmed = raw.trim();
  if (!trimmed) {
    return "";
  }

  if (trimmed.startsWith("{{") && trimmed.endsWith("}}") && trimmed.length > 4) {
    return normalizePath(trimmed.slice(2, -2));
  }

  if (trimmed.toLowerCase().startsWith("global.")) {
    return trimmed.slice("global.".length).trim();
  }

  return trimmed;
}

function guessKind(path: string, node: CanvasNode): NodeDebugFieldKind {
  const normalizedPath = normalizePath(path);
  const exactType =
    node.inputTypes?.[normalizedPath] ??
    node.inputTypes?.[normalizedPath.split(".")[0] ?? normalizedPath] ??
    "";
  const normalizedType = exactType.trim().toLowerCase();
  if (normalizedType === "number" || normalizedType === "integer" || normalizedType === "float" || normalizedType === "double") {
    return "number";
  }
  if (normalizedType === "boolean" || normalizedType === "bool") {
    return "boolean";
  }
  if (normalizedType === "object" || normalizedType === "array" || normalizedType === "json") {
    return "json";
  }

  if (normalizedPath.includes("[") || normalizedPath.includes(".")) {
    return "json";
  }

  return "text";
}

function titleizePath(path: string): string {
  const lastSegment = path.split(".").at(-1) ?? path;
  return lastSegment.replace(/\[(\d+)\]/g, "[$1]");
}

function addField(
  target: Map<string, NodeDebugFieldDefinition>,
  node: CanvasNode,
  rawPath: string,
  source: NodeDebugFieldDefinition["source"],
  label?: string
) {
  const normalizedPath = normalizePath(rawPath);
  if (!normalizedPath || target.has(normalizedPath)) {
    return;
  }

  target.set(normalizedPath, {
    path: normalizedPath,
    label: label?.trim() || titleizePath(normalizedPath),
    kind: guessKind(normalizedPath, node),
    source
  });
}

function extractFromConfigValue(
  node: CanvasNode,
  configKey: string,
  value: unknown,
  target: Map<string, NodeDebugFieldDefinition>
) {
  if (typeof value === "string") {
    if (VARIABLE_PATH_CONFIG_KEYS.has(configKey)) {
      addField(target, node, value, "configPath", configKey);
    }

    for (const match of value.matchAll(TEMPLATE_VARIABLE_REGEX)) {
      const path = match[1] ?? "";
      addField(target, node, path, "templateRef");
    }
    return;
  }

  if (Array.isArray(value)) {
    value.forEach((item) => extractFromConfigValue(node, configKey, item, target));
    return;
  }

  if (!value || typeof value !== "object") {
    return;
  }

  for (const [key, child] of Object.entries(value as Record<string, unknown>)) {
    extractFromConfigValue(node, key, child, target);
  }
}

export function extractNodeDebugFields(node: CanvasNode | null | undefined): NodeDebugFieldDefinition[] {
  if (!node) {
    return [];
  }

  const fields = new Map<string, NodeDebugFieldDefinition>();
  for (const [field, path] of Object.entries(node.inputMappings ?? {})) {
    addField(fields, node, field, "inputMapping", field);
    addField(fields, node, path, "inputMapping", field);
  }

  for (const mapping of node.inputSources ?? []) {
    const field = typeof mapping.field === "string" ? mapping.field : "";
    const path = typeof mapping.path === "string" ? mapping.path : "";
    if (field) {
      addField(fields, node, field, "inputSource", field);
    }
    if (path) {
      addField(fields, node, path, "inputSource", field || path);
    }
  }

  for (const [configKey, value] of Object.entries(node.configs ?? {})) {
    extractFromConfigValue(node, configKey, value, fields);
  }

  return [...fields.values()].sort((left, right) => left.path.localeCompare(right.path));
}
