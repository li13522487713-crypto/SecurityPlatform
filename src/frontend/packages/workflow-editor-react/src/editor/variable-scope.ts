/**
 * VM-01: 前端变量作用域计算工具
 *
 * 通过 DAG 反向遍历，为每个节点计算其可见的上游变量集合。
 * 上游节点的输出变量 + 全局变量 + 工作流输入变量 = 当前节点可用的变量。
 */

export interface VariableScopeNode {
  key: string;
  type: string;
  outputTypes?: Record<string, string>;
  configs?: Record<string, unknown>;
}

export interface VariableScopeConnection {
  fromNode: string;
  toNode: string;
}

export interface ScopeVariable {
  /** 变量的完整引用路径，如 `llm_1.output` */
  path: string;
  /** 变量的来源节点 Key */
  sourceNodeKey: string;
  /** 变量的端口名或字段名 */
  fieldName: string;
  /** 类型提示（如 "string", "number", "object"），可选 */
  valueType?: string;
}

export interface NodeVariableScope {
  nodeKey: string;
  /** 该节点可以引用的所有上游变量 */
  availableVariables: ScopeVariable[];
  /** 该节点自身产生的变量（对下游可见） */
  producedVariables: ScopeVariable[];
}

const SYSTEM_VARIABLE_PATHS: ScopeVariable[] = [
  { path: "inputs.input", sourceNodeKey: "__system__", fieldName: "input", valueType: "string" },
  { path: "inputs.inputsJson", sourceNodeKey: "__system__", fieldName: "inputsJson", valueType: "string" },
  { path: "workflow.executionId", sourceNodeKey: "__system__", fieldName: "executionId", valueType: "string" },
  { path: "workflow.startedAt", sourceNodeKey: "__system__", fieldName: "startedAt", valueType: "string" },
  { path: "runtime.userId", sourceNodeKey: "__system__", fieldName: "userId", valueType: "string" },
  { path: "runtime.tenantId", sourceNodeKey: "__system__", fieldName: "tenantId", valueType: "string" },
];

/**
 * 从节点 outputTypes 中提取该节点产生的变量列表。
 */
function extractProducedVariables(node: VariableScopeNode): ScopeVariable[] {
  const result: ScopeVariable[] = [];
  const outputTypes = node.outputTypes ?? {};
  for (const [field, type] of Object.entries(outputTypes)) {
    if (field.trim()) {
      result.push({
        path: `${node.key}.${field}`,
        sourceNodeKey: node.key,
        fieldName: field,
        valueType: type || "unknown"
      });
    }
  }
  return result;
}

/**
 * 构建反向邻接表（toNode → [fromNode...]）。
 */
function buildReverseAdjacency(connections: VariableScopeConnection[]): Map<string, string[]> {
  const map = new Map<string, string[]>();
  for (const conn of connections) {
    const list = map.get(conn.toNode) ?? [];
    list.push(conn.fromNode);
    map.set(conn.toNode, list);
  }
  return map;
}

/**
 * 通过 BFS 收集从 nodeKey 出发的所有上游节点 Key（包括传递性上游）。
 */
function collectUpstreamNodeKeys(
  nodeKey: string,
  reverseAdj: Map<string, string[]>
): Set<string> {
  const visited = new Set<string>();
  const queue: string[] = [...(reverseAdj.get(nodeKey) ?? [])];
  while (queue.length > 0) {
    const current = queue.shift()!;
    if (visited.has(current)) continue;
    visited.add(current);
    const parents = reverseAdj.get(current) ?? [];
    queue.push(...parents);
  }
  return visited;
}

/**
 * 计算每个节点的变量作用域。
 *
 * @param nodes 画布节点列表
 * @param connections 连接列表
 * @param globalVariables 全局变量名映射（key → type）
 * @returns 每个节点的 NodeVariableScope
 */
export function computeVariableScopes(
  nodes: VariableScopeNode[],
  connections: VariableScopeConnection[],
  globalVariables: Record<string, string> = {}
): Map<string, NodeVariableScope> {
  const reverseAdj = buildReverseAdjacency(connections);
  const nodeMap = new Map(nodes.map((n) => [n.key, n]));

  // 全局变量
  const globalVarList: ScopeVariable[] = Object.entries(globalVariables).map(([key, type]) => ({
    path: `global.${key}`,
    sourceNodeKey: "__global__",
    fieldName: key,
    valueType: type || "unknown"
  }));

  const result = new Map<string, NodeVariableScope>();

  for (const node of nodes) {
    const upstreamKeys = collectUpstreamNodeKeys(node.key, reverseAdj);
    const availableVariables: ScopeVariable[] = [
      ...SYSTEM_VARIABLE_PATHS,
      ...globalVarList,
    ];

    for (const upstreamKey of upstreamKeys) {
      const upstreamNode = nodeMap.get(upstreamKey);
      if (!upstreamNode) continue;
      const produced = extractProducedVariables(upstreamNode);
      availableVariables.push(...produced);
    }

    const producedVariables = extractProducedVariables(node);

    result.set(node.key, {
      nodeKey: node.key,
      availableVariables,
      producedVariables
    });
  }

  return result;
}

/**
 * 快速获取某节点可用变量路径的集合（用于引用校验）。
 */
export function getAvailableVariablePaths(
  nodeKey: string,
  nodes: VariableScopeNode[],
  connections: VariableScopeConnection[],
  globalVariables: Record<string, string> = {}
): Set<string> {
  const scopes = computeVariableScopes(nodes, connections, globalVariables);
  const scope = scopes.get(nodeKey);
  if (!scope) return new Set<string>();
  return new Set(scope.availableVariables.map((v) => v.path));
}
