import type { NodeTypeMetadata } from "../types";
import { buildNodePortsRuntime, type ConnectionRuntime, type NodePortsRuntime, validateConnectionCandidate } from "./connection-rules";
import { validateConfigBySchema, type SchemaValidationIssue } from "./schema-validation";

export interface NodeValidationResult {
  nodeKey: string;
  issues: string[];
  fieldIssues: SchemaValidationIssue[];
}

export interface CanvasValidationResult {
  ok: boolean;
  nodeResults: NodeValidationResult[];
  canvasIssues: string[];
}

interface CanvasNodeForValidation {
  key: string;
  type: string;
  configs: Record<string, unknown>;
  inputMappings: Record<string, string>;
  outputTypes?: Record<string, string>;
}

const VARIABLE_REF_REGEX = /\{\{\s*([^{}]+?)\s*\}\}/g;

function buildTypeMap(nodeTypesMeta: NodeTypeMetadata[]): Map<string, NodeTypeMetadata> {
  const map = new Map<string, NodeTypeMetadata>();
  for (const meta of nodeTypesMeta) {
    map.set(String(meta.key), meta);
  }
  return map;
}

function buildPortsMetaMap(nodeTypesMeta: NodeTypeMetadata[]): Map<string, NodePortsRuntime> {
  const map = new Map<string, NodePortsRuntime>();
  for (const meta of nodeTypesMeta) {
    map.set(String(meta.key), buildNodePortsRuntime(meta));
  }
  return map;
}

export function validateCanvas(
  nodes: CanvasNodeForValidation[],
  connections: ConnectionRuntime[],
  nodeTypesMeta: NodeTypeMetadata[],
  globals: Record<string, unknown> = {}
): CanvasValidationResult {
  const nodeResults: NodeValidationResult[] = [];
  const canvasIssues: string[] = [];
  const typeMap = buildTypeMap(nodeTypesMeta);
  const portsMap = buildPortsMetaMap(nodeTypesMeta);
  const nodeMap = new Map(nodes.map((node) => [node.key, node]));

  // ── VL-01/VL-02: 图结构完整性校验（与后端 CanvasValidator 规则对齐）──────────────

  // 0. 重复 key 检测（对齐后端 DUPLICATE_NODE_KEY）
  const seenKeys = new Set<string>();
  for (const node of nodes) {
    if (seenKeys.has(node.key)) {
      canvasIssues.push(`重复节点标识 '${node.key}'。`);
    }
    seenKeys.add(node.key);
  }

  // 1. 检查 Start/Entry 节点
  const entryNodes = nodes.filter((n) => n.type === "Entry" || n.type === "Start");
  if (entryNodes.length === 0) {
    canvasIssues.push("流程缺少开始节点（Entry），无法执行。");
  } else if (entryNodes.length > 1) {
    canvasIssues.push(`流程包含多个开始节点（${entryNodes.map((n) => n.key).join(", ")}），存在歧义。`);
  }

  // 2. 检查 End/Exit 节点
  const exitNodes = nodes.filter((n) => n.type === "Exit" || n.type === "End");
  if (exitNodes.length === 0) {
    canvasIssues.push("流程缺少结束节点（Exit），输出无法收集。");
  }

  // 3. 孤立节点检测（无入边且无出边，且不是 Entry/Comment）
  const forwardAdjacency = new Map<string, Set<string>>();
  const reverseAdjacency = new Map<string, Set<string>>();
  const nodesWithEdges = new Set<string>();
  for (const connection of connections) {
    const fromNode = connection.fromNode;
    const toNode = connection.toNode;
    const fwdSet = forwardAdjacency.get(fromNode) ?? new Set<string>();
    fwdSet.add(toNode);
    forwardAdjacency.set(fromNode, fwdSet);
    const revSet = reverseAdjacency.get(toNode) ?? new Set<string>();
    revSet.add(fromNode);
    reverseAdjacency.set(toNode, revSet);
    nodesWithEdges.add(fromNode);
    nodesWithEdges.add(toNode);
  }

  for (const node of nodes) {
    if (node.type === "Entry" || node.type === "Start" || node.type === "Comment") continue;
    if (!nodesWithEdges.has(node.key)) {
      canvasIssues.push(`节点 ${node.key}（${node.type}）是孤立节点，未连接到任何其他节点。`);
    }
  }

  // 4. 可达性检查：从所有 Entry 节点出发，BFS 检测不可达节点
  if (entryNodes.length > 0) {
    const reachable = new Set<string>();
    const queue = entryNodes.map((n) => n.key);
    while (queue.length > 0) {
      const current = queue.shift()!;
      if (reachable.has(current)) continue;
      reachable.add(current);
      const neighbors = forwardAdjacency.get(current) ?? new Set<string>();
      queue.push(...neighbors);
    }
    const unreachable = nodes.filter(
      (n) => !reachable.has(n.key) && n.type !== "Comment"
    );
    for (const node of unreachable) {
      canvasIssues.push(`节点 ${node.key}（${node.type}）从开始节点不可达。`);
    }
  }

  for (const node of nodes) {
    const meta = typeMap.get(node.type);
    const fieldIssues = validateConfigBySchema(node.configs, meta?.configSchemaJson).issues;
    const issues: string[] = [];
    if (!meta) {
      issues.push(`节点 ${node.key} 缺少元数据定义（type=${node.type}）。`);
    }
    if (fieldIssues.length > 0) {
      issues.push(...fieldIssues.map((item) => `${item.path || "root"}: ${item.message}`));
    }

    const ports = portsMap.get(node.type) ?? buildNodePortsRuntime(undefined);
    const inputMappingsKeys = Object.keys(node.inputMappings ?? {});
    if (inputMappingsKeys.length > 0) {
      const validInputKeys = new Set(ports.inputs.map((port) => port.key));
      const invalidKeys = inputMappingsKeys.filter((key) => !validInputKeys.has(key));
      if (invalidKeys.length > 0) {
        issues.push(`inputMappings 存在无效端口键：${invalidKeys.join(", ")}`);
      }
    }

    if (node.outputTypes) {
      const invalidOutputTypes = Object.entries(node.outputTypes).filter(([key, value]) => key.trim().length === 0 || String(value ?? "").trim().length === 0);
      if (invalidOutputTypes.length > 0) {
        issues.push("outputTypes 存在空 key 或空类型定义。");
      }
    }

    const upstreamNodeKeys = resolveUpstreamNodeKeys(node.key, reverseAdjacency);
    const refs = [
      ...extractVariableRefs(node.configs),
      ...extractVariableRefs(node.inputMappings)
    ];
    for (const ref of refs) {
      if (!ref) {
        continue;
      }
      const parts = ref.split(/[.\[]/, 2);
      const root = parts[0]?.trim();
      if (!root) {
        continue;
      }
      if (root === "global") {
        const globalKey = ref.replace(/^global\./, "").split(/[.\[]/, 2)[0];
        if (!globalKey || !(globalKey in globals)) {
          issues.push(`变量引用 ${ref} 指向了不存在的 global 变量。`);
        }
        continue;
      }
      if (root === "inputs" || root === "input" || root === "workflow" || root === "runtime") {
        continue;
      }
      if (nodeMap.has(root) && !upstreamNodeKeys.has(root)) {
        issues.push(`变量引用 ${ref} 非法：节点 ${root} 不是当前节点的上游。`);
      }
    }

    nodeResults.push({ nodeKey: node.key, issues, fieldIssues });
  }

  for (const connection of connections) {
    const sourceNode = nodeMap.get(connection.fromNode);
    const targetNode = nodeMap.get(connection.toNode);
    if (!sourceNode || !targetNode) {
      canvasIssues.push(`连接 ${connection.id} 指向了不存在的节点。`);
      continue;
    }
    const sourcePorts = portsMap.get(sourceNode.type) ?? buildNodePortsRuntime(undefined);
    const targetPorts = portsMap.get(targetNode.type) ?? buildNodePortsRuntime(undefined);
    const existingWithoutCurrent = connections.filter((item) => item.id !== connection.id);
    const result = validateConnectionCandidate(connection, existingWithoutCurrent, sourcePorts.outputs, targetPorts.inputs);
    if (!result.ok) {
      canvasIssues.push(`连接 ${connection.id} 非法：${result.message}`);
    }
  }

  return {
    ok: nodeResults.every((item) => item.issues.length === 0) && canvasIssues.length === 0,
    nodeResults,
    canvasIssues
  };
}

function resolveUpstreamNodeKeys(nodeKey: string, reverseAdjacency: Map<string, Set<string>>): Set<string> {
  const visited = new Set<string>();
  const queue = [...(reverseAdjacency.get(nodeKey) ?? new Set<string>())];
  while (queue.length > 0) {
    const current = queue.shift();
    if (!current || visited.has(current)) {
      continue;
    }
    visited.add(current);
    const next = reverseAdjacency.get(current) ?? new Set<string>();
    queue.push(...next);
  }
  return visited;
}

function extractVariableRefs(source: unknown): string[] {
  const refs: string[] = [];
  walkValue(source, refs);
  return refs;
}

function walkValue(value: unknown, refs: string[]): void {
  if (typeof value === "string") {
    for (const match of value.matchAll(VARIABLE_REF_REGEX)) {
      const ref = (match[1] ?? "").trim();
      if (ref) {
        refs.push(ref);
      }
    }
    return;
  }
  if (Array.isArray(value)) {
    for (const item of value) {
      walkValue(item, refs);
    }
    return;
  }
  if (value && typeof value === "object") {
    for (const nestedValue of Object.values(value as Record<string, unknown>)) {
      walkValue(nestedValue, refs);
    }
  }
}
