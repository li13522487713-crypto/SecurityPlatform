import type { WorkflowNodeTypeKey } from "../types";

export interface SuggestionNode {
  key: string;
  type: WorkflowNodeTypeKey;
  configs: Record<string, unknown>;
  x?: number;
}

export interface VariableSuggestion {
  value: string;
  label: string;
}

export interface SuggestionConnection {
  fromNode: string;
  toNode: string;
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

function resolveUpstreamNodeKeys(connections: SuggestionConnection[], currentNodeKey: string): Set<string> {
  const reverseAdjacency = new Map<string, string[]>();
  for (const connection of connections) {
    const list = reverseAdjacency.get(connection.toNode) ?? [];
    list.push(connection.fromNode);
    reverseAdjacency.set(connection.toNode, list);
  }

  const visited = new Set<string>();
  const queue = [...(reverseAdjacency.get(currentNodeKey) ?? [])];
  while (queue.length > 0) {
    const nodeKey = queue.shift();
    if (!nodeKey || visited.has(nodeKey)) {
      continue;
    }
    visited.add(nodeKey);
    const next = reverseAdjacency.get(nodeKey) ?? [];
    queue.push(...next);
  }
  return visited;
}

export function buildVariableSuggestions(
  nodes: SuggestionNode[],
  currentNodeKey: string,
  connections?: SuggestionConnection[]
): VariableSuggestion[] {
  const suggestions: VariableSuggestion[] = [];
  const currentNode = nodes.find((node) => node.key === currentNodeKey);
  const currentX = typeof currentNode?.x === "number" ? currentNode.x : undefined;
  const upstreamNodeKeys = connections ? resolveUpstreamNodeKeys(connections, currentNodeKey) : null;
  for (const node of nodes) {
    if (node.key === currentNodeKey) {
      continue;
    }
    if (upstreamNodeKeys && !upstreamNodeKeys.has(node.key)) {
      continue;
    }
    if (typeof currentX === "number" && typeof node.x === "number" && node.x > currentX) {
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

