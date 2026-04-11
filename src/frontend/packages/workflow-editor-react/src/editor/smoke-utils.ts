import type { WorkflowNodeTypeKey } from "../types";

export interface SuggestionNode {
  key: string;
  type: WorkflowNodeTypeKey;
  configs: Record<string, unknown>;
}

export interface VariableSuggestion {
  value: string;
  label: string;
}

function readString(source: unknown): string | undefined {
  return typeof source === "string" && source.trim().length > 0 ? source.trim() : undefined;
}

function readNestedString(config: Record<string, unknown>, path: string[]): string | undefined {
  let current: unknown = config;
  for (const segment of path) {
    if (!current || typeof current !== "object") {
      return undefined;
    }
    current = (current as Record<string, unknown>)[segment];
  }
  return readString(current);
}

export function deriveOutputKeys(type: WorkflowNodeTypeKey, config: Record<string, unknown>): string[] {
  const candidates = new Set<string>();
  const fromPaths: Array<string[]> = [
    ["ai", "outputKey"],
    ["processor", "outputKey"],
    ["variables", "target"],
    ["io", "key"],
    ["http", "outputKey"],
    ["knowledge", "outputKey"],
    ["database", "outputKey"],
    ["conversation", "outputKey"],
    ["llm", "outputKey"]
  ];
  for (const path of fromPaths) {
    const value = readNestedString(config, path);
    if (value) {
      candidates.add(value);
    }
  }
  if (candidates.size === 0) {
    if (type === "Llm") {
      candidates.add("result");
    } else if (type === "Entry") {
      candidates.add("input");
    } else if (type === "Exit") {
      candidates.add("output");
    } else {
      candidates.add("result");
    }
  }
  return [...candidates];
}

export function buildVariableSuggestions(nodes: SuggestionNode[], currentNodeKey: string): VariableSuggestion[] {
  const suggestions: VariableSuggestion[] = [];
  for (const node of nodes) {
    if (node.key === currentNodeKey) {
      continue;
    }
    const outputs = deriveOutputKeys(node.type, node.configs);
    for (const outputKey of outputs) {
      const value = `{{${node.key}.${outputKey}}}`;
      suggestions.push({
        value,
        label: `${node.key}.${outputKey}`
      });
    }
  }
  return suggestions;
}

