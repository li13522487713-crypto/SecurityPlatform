import type { CanvasSchema, NodeTypeMetadata, WorkflowNodeTypeKey } from "../types";
import { buildNodePortsRuntime, resolveDefaultPortKey, type ConnectionRuntime } from "./connection-rules";

export interface CanvasNode {
  key: string;
  type: WorkflowNodeTypeKey;
  title: string;
  x: number;
  y: number;
  configs: Record<string, unknown>;
  inputMappings: Record<string, string>;
  childCanvas?: CanvasSchema;
  inputTypes?: Record<string, string>;
  outputTypes?: Record<string, string>;
  inputSources?: Array<Record<string, unknown>>;
  outputSources?: Array<Record<string, unknown>>;
  debugMeta?: Record<string, unknown>;
}

export interface CanvasConnection extends ConnectionRuntime {}

export type EdgeRuntimeState = "idle" | "running" | "success" | "failed" | "skipped";

export interface WorkflowViewportState {
  x: number;
  y: number;
  zoom: number;
}

export const NODE_WIDTH = 360;
export const NODE_HEIGHT = 160;

export const INITIAL_NODES: CanvasNode[] = [
  {
    key: "entry_1",
    type: "Entry",
    title: "开始",
    x: 160,
    y: 120,
    configs: { entryVariable: "USER_INPUT", entryAutoSaveHistory: true },
    inputMappings: {}
  },
  {
    key: "exit_1",
    type: "Exit",
    title: "结束",
    x: 720,
    y: 120,
    configs: { exitTerminateMode: "return", exitTemplate: "{{entry_1.USER_INPUT}}" },
    inputMappings: {}
  }
];

export const INITIAL_CONNECTIONS: CanvasConnection[] = [
  { id: "conn_entry_1_exit_1", fromNode: "entry_1", fromPort: "output", toNode: "exit_1", toPort: "input", condition: null }
];

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value) && typeof value === "object" && !Array.isArray(value);
}

export function parseCanvasNode(node: unknown): CanvasNode | null {
  if (!isRecord(node)) {
    return null;
  }
  const key = typeof node.key === "string" ? node.key : "";
  const type = typeof node.type === "string" ? (node.type as WorkflowNodeTypeKey) : "TextProcessor";
  const layout = isRecord(node.layout) ? node.layout : {};
  const title = typeof node.title === "string" ? node.title : type;
  const configs = isRecord(node.configs) ? node.configs : {};
  const inputMappings = isRecord(node.inputMappings)
    ? (Object.fromEntries(Object.entries(node.inputMappings).filter(([, value]) => typeof value === "string")) as Record<string, string>)
    : {};
  const childCanvasRaw = isRecord(node.childCanvas) ? node.childCanvas : null;
  const childCanvas =
    childCanvasRaw && Array.isArray(childCanvasRaw.nodes) && Array.isArray(childCanvasRaw.connections)
      ? (childCanvasRaw as unknown as CanvasSchema)
      : undefined;
  if (!key) {
    return null;
  }
  return {
    key,
    type,
    title,
    x: typeof layout.x === "number" ? layout.x : 120,
    y: typeof layout.y === "number" ? layout.y : 120,
    configs,
    inputMappings,
    childCanvas,
    inputTypes: isRecord(node.inputTypes) ? (node.inputTypes as Record<string, string>) : undefined,
    outputTypes: isRecord(node.outputTypes) ? (node.outputTypes as Record<string, string>) : undefined,
    inputSources: Array.isArray(node.inputSources) ? (node.inputSources as Array<Record<string, unknown>>) : undefined,
    outputSources: Array.isArray(node.outputSources) ? (node.outputSources as Array<Record<string, unknown>>) : undefined,
    debugMeta: isRecord(node.debugMeta) ? (node.debugMeta as Record<string, unknown>) : undefined
  };
}

export function parseCanvasConnection(connection: unknown, index: number): CanvasConnection | null {
  if (!isRecord(connection)) {
    return null;
  }
  const fromNode = typeof connection.fromNode === "string" ? connection.fromNode : "";
  const toNode = typeof connection.toNode === "string" ? connection.toNode : "";
  if (!fromNode || !toNode) {
    return null;
  }
  return {
    id:
      typeof connection.id === "string" && connection.id
        ? connection.id
        : `conn_${fromNode}_${toNode}_${index.toString(36)}`,
    fromNode,
    fromPort: typeof connection.fromPort === "string" && connection.fromPort ? connection.fromPort : "output",
    toNode,
    toPort: typeof connection.toPort === "string" && connection.toPort ? connection.toPort : "input",
    condition: typeof connection.condition === "string" ? connection.condition : null
  };
}

export function parseCanvasJson(
  json: string | undefined
): { nodes: CanvasNode[]; connections: CanvasConnection[]; globals: Record<string, unknown>; viewport?: WorkflowViewportState } {
  if (!json) {
    return { nodes: INITIAL_NODES, connections: INITIAL_CONNECTIONS, globals: {} };
  }
  try {
    const parsed = JSON.parse(json) as CanvasSchema;
    if (!Array.isArray(parsed.nodes)) {
      return { nodes: INITIAL_NODES, connections: INITIAL_CONNECTIONS, globals: {} };
    }
    const nodes = parsed.nodes.map((item) => parseCanvasNode(item)).filter((item): item is CanvasNode => item !== null);
    const connections = Array.isArray(parsed.connections)
      ? parsed.connections
          .map((item, index) => parseCanvasConnection(item, index))
          .filter((item): item is CanvasConnection => item !== null)
      : [];
    const viewportCandidate = isRecord(parsed.viewport) ? parsed.viewport : null;
    const viewport =
      viewportCandidate &&
      typeof viewportCandidate.x === "number" &&
      typeof viewportCandidate.y === "number" &&
      typeof viewportCandidate.zoom === "number"
        ? { x: viewportCandidate.x, y: viewportCandidate.y, zoom: viewportCandidate.zoom }
        : undefined;
    return { nodes: nodes.length > 0 ? nodes : INITIAL_NODES, connections, globals: isRecord(parsed.globals) ? parsed.globals : {}, viewport };
  } catch {
    return { nodes: INITIAL_NODES, connections: INITIAL_CONNECTIONS, globals: {} };
  }
}

export function toCanvasJson(
  nodes: CanvasNode[],
  connections: CanvasConnection[],
  globals: Record<string, unknown>,
  viewport: WorkflowViewportState
): string {
  const payload: CanvasSchema = {
    nodes: nodes.map((node) => ({
      key: node.key,
      type: node.type,
      title: node.title,
      layout: { x: node.x, y: node.y, width: NODE_WIDTH, height: NODE_HEIGHT },
      configs: node.configs,
      inputMappings: node.inputMappings,
      childCanvas: node.childCanvas,
      inputTypes: node.inputTypes,
      outputTypes: node.outputTypes,
      inputSources: node.inputSources,
      outputSources: node.outputSources,
      debugMeta: node.debugMeta
    })),
    connections: connections.map((connection) => ({
      fromNode: connection.fromNode,
      fromPort: connection.fromPort,
      toNode: connection.toNode,
      toPort: connection.toPort,
      condition: connection.condition
    })),
    schemaVersion: 2,
    globals,
    viewport
  };
  return JSON.stringify(payload);
}

export function normalizeConnectionsByPorts(
  nodes: CanvasNode[],
  connections: CanvasConnection[],
  nodeTypesMeta: NodeTypeMetadata[]
): { connections: CanvasConnection[]; migratedCount: number } {
  const nodeMap = new Map(nodes.map((node) => [node.key, node]));
  const typeMap = new Map<string, NodeTypeMetadata>(nodeTypesMeta.map((meta) => [String(meta.key), meta]));
  const dedupe = new Set<string>();
  let migratedCount = 0;

  const normalized: CanvasConnection[] = [];
  for (let i = 0; i < connections.length; i += 1) {
    const current = connections[i];
    const fromNode = nodeMap.get(current.fromNode);
    const toNode = nodeMap.get(current.toNode);
    if (!fromNode || !toNode) {
      normalized.push(current);
      continue;
    }

    const fromPorts = buildNodePortsRuntime(typeMap.get(fromNode.type));
    const toPorts = buildNodePortsRuntime(typeMap.get(toNode.type));

    const outputPortExists = fromPorts.outputs.some((port) => port.key === current.fromPort);
    const inputPortExists = toPorts.inputs.some((port) => port.key === current.toPort);

    const nextFromPort = outputPortExists ? current.fromPort : resolveDefaultPortKey(fromPorts.outputs, "output");
    const nextToPort = inputPortExists ? current.toPort : resolveDefaultPortKey(toPorts.inputs, "input");

    if (nextFromPort !== current.fromPort || nextToPort !== current.toPort) {
      migratedCount += 1;
    }

    const dedupeKey = `${current.fromNode}:${nextFromPort}->${current.toNode}:${nextToPort}`;
    if (dedupe.has(dedupeKey)) {
      migratedCount += 1;
      continue;
    }
    dedupe.add(dedupeKey);

    normalized.push({
      ...current,
      fromPort: nextFromPort,
      toPort: nextToPort
    });
  }
  return { connections: normalized, migratedCount };
}
