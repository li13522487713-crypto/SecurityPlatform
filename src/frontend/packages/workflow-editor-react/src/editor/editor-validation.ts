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
}

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
  nodeTypesMeta: NodeTypeMetadata[]
): CanvasValidationResult {
  const nodeResults: NodeValidationResult[] = [];
  const canvasIssues: string[] = [];
  const typeMap = buildTypeMap(nodeTypesMeta);
  const portsMap = buildPortsMetaMap(nodeTypesMeta);
  const nodeMap = new Map(nodes.map((node) => [node.key, node]));

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
