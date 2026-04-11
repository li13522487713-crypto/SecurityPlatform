import { message } from "antd";
import { useEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { CanvasToolbar } from "../components/CanvasToolbar";
import { NodeCard } from "../components/NodeCard";
import { NodePanelPopover } from "../components/NodePanelPopover";
import { PropertiesPanel } from "../components/PropertiesPanel";
import { TestRunPanel } from "../components/TestRunPanel";
import { WorkflowHeader } from "../components/WorkflowHeader";
import { WORKFLOW_NODE_CATALOG, type WorkflowNodeCatalogItem } from "../constants/node-catalog";
import { ensureWorkflowI18n } from "../i18n";
import { createMetadataBundle, mergeNodeDefaults, NodeRegistry } from "../node-registry";
import type {
  CanvasSchema,
  NodeTemplateMetadata,
  NodeTypeMetadata,
  WorkflowDetailResponse,
  WorkflowNodeTypeKey,
  WorkflowSaveRequest
} from "../types";
import {
  buildNodePortsRuntime,
  resolveDefaultPortKey,
  type ConnectionRuntime,
  type NodePortsRuntime,
  type PortRuntime,
  validateConnectionCandidate
} from "./connection-rules";
import { validateCanvas, type CanvasValidationResult } from "./editor-validation";
import { buildVariableSuggestions } from "./smoke-utils";
import "./workflow-editor.css";

interface WorkflowApiClient {
  getDetail?: (id: string) => Promise<{ data?: WorkflowDetailResponse }>;
  saveDraft?: (id: string, req: WorkflowSaveRequest) => Promise<unknown>;
  getNodeTypes?: () => Promise<{ data?: NodeTypeMetadata[] }>;
  getNodeTemplates?: () => Promise<{ data?: NodeTemplateMetadata[] }>;
  runSync?: (
    id: string,
    req: { inputsJson?: string; source?: "published" | "draft" }
  ) => Promise<{ data?: { executionId: string } }>;
  runStream?: (
    id: string,
    req: { inputsJson?: string; source?: "published" | "draft" },
    callbacks: {
      onExecutionStarted?: (ev: { executionId: string }) => void;
      onNodeStarted?: (ev: { nodeKey: string; nodeType: string }) => void;
      onNodeOutput?: (ev: { nodeKey: string }) => void;
      onNodeCompleted?: (ev: { nodeKey: string; durationMs?: number }) => void;
      onNodeFailed?: (ev: { nodeKey: string; errorMessage: string }) => void;
      onExecutionCompleted?: (ev: { outputsJson?: string }) => void;
      onExecutionFailed?: (ev: { errorMessage: string }) => void;
      onExecutionCancelled?: (ev: { errorMessage?: string }) => void;
      onExecutionInterrupted?: (ev: { interruptType: string; nodeKey?: string }) => void;
      onError?: (err: Event | Error) => void;
    }
  ) => { abort: () => void; done: Promise<void> };
  getProcess?: (executionId: string) => Promise<{ data?: { nodeExecutions?: Array<{ nodeKey: string; status: number; errorMessage?: string }> } }>;
}

export interface WorkflowEditorReactProps {
  workflowId: string;
  locale?: string;
  apiClient: WorkflowApiClient;
  onBack?: () => void;
}

interface CanvasNode {
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
}

interface CanvasConnection extends ConnectionRuntime {}

interface DragNodeOperation {
  kind: "drag-node";
  nodeKey: string;
  startClientX: number;
  startClientY: number;
  startX: number;
  startY: number;
}

interface PanCanvasOperation {
  kind: "pan-canvas";
  startClientX: number;
  startClientY: number;
  startX: number;
  startY: number;
}

interface ConnectOperation {
  kind: "connect";
  fromNode: string;
  fromPort: string;
}

type CanvasOperation = DragNodeOperation | PanCanvasOperation | ConnectOperation;

const nodeRegistry = new NodeRegistry();
const NODE_WIDTH = 360;
const NODE_HEIGHT = 160;

const INITIAL_NODES: CanvasNode[] = [
  {
    key: "entry_1",
    type: "Entry",
    title: "开始",
    x: 160,
    y: 120,
    configs: { entry: { variable: "USER_INPUT", autoSaveHistory: true } },
    inputMappings: {}
  },
  {
    key: "llm_1",
    type: "Llm",
    title: "大模型",
    x: 620,
    y: 120,
    configs: { llm: { provider: "qwen", model: "qwen-max", userPrompt: "{{entry_1.output}}" } },
    inputMappings: {}
  },
  {
    key: "exit_1",
    type: "Exit",
    title: "结束",
    x: 1080,
    y: 120,
    configs: { exit: { terminateMode: "return", template: "{{llm_1.result}}" } },
    inputMappings: {}
  }
];

const INITIAL_CONNECTIONS: CanvasConnection[] = [
  { id: "conn_entry_1_llm_1", fromNode: "entry_1", fromPort: "output", toNode: "llm_1", toPort: "input", condition: null },
  { id: "conn_llm_1_exit_1", fromNode: "llm_1", fromPort: "output", toNode: "exit_1", toPort: "input", condition: null }
];

function isRecord(value: unknown): value is Record<string, unknown> {
  return Boolean(value) && typeof value === "object" && !Array.isArray(value);
}

function parseCanvasNode(node: unknown): CanvasNode | null {
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
    outputSources: Array.isArray(node.outputSources) ? (node.outputSources as Array<Record<string, unknown>>) : undefined
  };
}

function parseCanvasConnection(connection: unknown, index: number): CanvasConnection | null {
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

function parseCanvasJson(json: string | undefined): { nodes: CanvasNode[]; connections: CanvasConnection[] } {
  if (!json) {
    return { nodes: INITIAL_NODES, connections: INITIAL_CONNECTIONS };
  }
  try {
    const parsed = JSON.parse(json) as CanvasSchema;
    if (!Array.isArray(parsed.nodes)) {
      return { nodes: INITIAL_NODES, connections: INITIAL_CONNECTIONS };
    }
    const nodes = parsed.nodes.map((item) => parseCanvasNode(item)).filter((item): item is CanvasNode => item !== null);
    const connections = Array.isArray(parsed.connections)
      ? parsed.connections
          .map((item, index) => parseCanvasConnection(item, index))
          .filter((item): item is CanvasConnection => item !== null)
      : [];
    return {
      nodes: nodes.length > 0 ? nodes : INITIAL_NODES,
      connections
    };
  } catch {
    return { nodes: INITIAL_NODES, connections: INITIAL_CONNECTIONS };
  }
}

function toCanvasJson(nodes: CanvasNode[], connections: CanvasConnection[]): string {
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
      outputSources: node.outputSources
    })),
    connections: connections.map((connection) => ({
      fromNode: connection.fromNode,
      fromPort: connection.fromPort,
      toNode: connection.toNode,
      toPort: connection.toPort,
      condition: connection.condition
    }))
  };
  return JSON.stringify(payload);
}

function connectionPath(fromX: number, fromY: number, toX: number, toY: number): string {
  const delta = Math.max(80, Math.abs(toX - fromX) * 0.42);
  const c1x = fromX + delta;
  const c2x = toX - delta;
  return `M ${fromX} ${fromY} C ${c1x} ${fromY}, ${c2x} ${toY}, ${toX} ${toY}`;
}

function getPortAnchor(
  node: CanvasNode,
  ports: NodePortsRuntime,
  direction: "input" | "output",
  portKey: string
): { x: number; y: number } {
  const list = direction === "output" ? ports.outputs : ports.inputs;
  const index = Math.max(0, list.findIndex((port) => port.key === portKey));
  const ratio = (index + 1) / (list.length + 1);
  return {
    x: direction === "output" ? node.x + NODE_WIDTH : node.x,
    y: node.y + NODE_HEIGHT * ratio
  };
}

function normalizeConnectionsByPorts(
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

function asObjectRecord(value: unknown): Record<string, unknown> {
  return value && typeof value === "object" && !Array.isArray(value) ? (value as Record<string, unknown>) : {};
}

function deriveSelectorDynamicPorts(node: CanvasNode, current: NodePortsRuntime): NodePortsRuntime {
  if (node.type !== "Selector") {
    return current;
  }

  const configs = asObjectRecord(node.configs);
  const conditionsRaw = configs.conditions;
  const conditions = Array.isArray(conditionsRaw) ? conditionsRaw : [];
  const elsePort = current.outputs.find((port) => port.key === "false" || port.key === "else");
  const templatePort = current.outputs[0] ?? {
    key: "true",
    name: "true",
    direction: "output" as const,
    dataType: "any",
    isRequired: false,
    maxConnections: 99
  };
  const truePorts = conditions.length > 0 ? conditions.map((_, index) => (index === 0 ? "true" : `true_${index}`)) : ["true"];
  const dynamicOutputs: PortRuntime[] = truePorts.map((key) => ({
    ...templatePort,
    key,
    name: key
  }));
  dynamicOutputs.push({
    ...(elsePort ?? templatePort),
    key: "false",
    name: "false"
  });

  return {
    inputs: current.inputs,
    outputs: dynamicOutputs
  };
}

export function WorkflowEditorReact(props: WorkflowEditorReactProps) {
  ensureWorkflowI18n(props.locale ?? "zh-CN");
  const { t } = useTranslation();

  const canvasShellRef = useRef<HTMLDivElement | null>(null);
  const operationRef = useRef<CanvasOperation | null>(null);
  const pointerIdRef = useRef<number | null>(null);
  const panRef = useRef({ x: 0, y: 0 });
  const zoomRef = useRef(100);

  const [workflowName, setWorkflowName] = useState(`Workflow_${props.workflowId}`);
  const [isDirty, setIsDirty] = useState(false);
  const [zoom, setZoom] = useState(100);
  const [pan, setPan] = useState({ x: 0, y: 0 });
  const [selectedNodeKey, setSelectedNodeKey] = useState<string>("llm_1");
  const [showNodePanel, setShowNodePanel] = useState(false);
  const [showTestPanel, setShowTestPanel] = useState(false);
  const [logs, setLogs] = useState<string[]>([]);
  const [canvasNodes, setCanvasNodes] = useState<CanvasNode[]>(INITIAL_NODES);
  const [canvasConnections, setCanvasConnections] = useState<CanvasConnection[]>(INITIAL_CONNECTIONS);
  const [nodeTypesMeta, setNodeTypesMeta] = useState<NodeTypeMetadata[]>([]);
  const [nodeTemplates, setNodeTemplates] = useState<NodeTemplateMetadata[]>([]);
  const [canvasValidation, setCanvasValidation] = useState<CanvasValidationResult | null>(null);
  const [connectingPreview, setConnectingPreview] = useState<{
    fromNode: string;
    fromPort: string;
    startX: number;
    startY: number;
    currentX: number;
    currentY: number;
  } | null>(null);
  const [connectableInputPortKeysByNode, setConnectableInputPortKeysByNode] = useState<Record<string, Set<string>>>({});
  const [draggingCatalogNodeType, setDraggingCatalogNodeType] = useState<string | null>(null);
  const [executionStateByNodeKey, setExecutionStateByNodeKey] = useState<
    Record<string, { state: "idle" | "running" | "success" | "failed" | "skipped"; hint?: string }>
  >({});
  const [runningConnectionIds, setRunningConnectionIds] = useState<Set<string>>(new Set());
  const [testInputJson, setTestInputJson] = useState<string>('{"input":"hello"}');
  const [testRunMode, setTestRunMode] = useState<"stream" | "sync">("stream");
  const [testRunSource, setTestRunSource] = useState<"published" | "draft">("published");
  const [testRunning, setTestRunning] = useState(false);
  const streamAbortRef = useRef<null | (() => void)>(null);

  const scale = zoom / 100;

  useEffect(() => {
    panRef.current = pan;
  }, [pan]);

  useEffect(() => {
    zoomRef.current = zoom;
  }, [zoom]);

  useEffect(() => {
    let disposed = false;
    const load = async () => {
      let loadedNodeTypes: NodeTypeMetadata[] = [];
      if (props.apiClient.getNodeTypes) {
        const response = await props.apiClient.getNodeTypes();
        loadedNodeTypes = response.data ?? [];
        if (!disposed) {
          setNodeTypesMeta(loadedNodeTypes);
        }
      }
      if (props.apiClient.getNodeTemplates) {
        const response = await props.apiClient.getNodeTemplates();
        if (!disposed) {
          setNodeTemplates(response.data ?? []);
        }
      }
      if (props.apiClient.getDetail) {
        const response = await props.apiClient.getDetail(props.workflowId);
        if (!disposed && response.data) {
          const parsed = parseCanvasJson(response.data.canvasJson);
          const normalized = normalizeConnectionsByPorts(parsed.nodes, parsed.connections, loadedNodeTypes);
          setWorkflowName(response.data.name || `Workflow_${props.workflowId}`);
          setCanvasNodes(parsed.nodes);
          setCanvasConnections(normalized.connections);
          if (normalized.migratedCount > 0) {
            message.info(`已迁移 ${normalized.migratedCount} 条历史连线到默认端口。`);
          }
          if (parsed.nodes.length > 0) {
            setSelectedNodeKey(parsed.nodes[0]?.key ?? "");
          }
        }
      }
    };
    void load();
    return () => {
      disposed = true;
    };
  }, [props.apiClient, props.workflowId]);

  const metadataBundle = useMemo(() => createMetadataBundle(nodeTypesMeta, nodeTemplates), [nodeTemplates, nodeTypesMeta]);

  const selectedNode = useMemo(() => {
    const node = canvasNodes.find((item: CanvasNode) => item.key === selectedNodeKey);
    return node ?? null;
  }, [canvasNodes, selectedNodeKey]);

  const nodeMap = useMemo(() => {
    const result = new Map<string, WorkflowNodeCatalogItem>();
    for (const item of WORKFLOW_NODE_CATALOG) {
      result.set(item.type, item);
    }
    for (const type of nodeRegistry.getAllTypes()) {
      if (!result.has(type)) {
        result.set(type, {
          type,
          titleKey: `wfUi.nodeTypes.${type}`,
          category: "dataProcess",
          color: "#64748B",
          iconText: type.slice(0, 2).toUpperCase()
        });
      }
    }
    return result;
  }, []);

  const nodeByKey = useMemo(() => {
    const map = new Map<string, CanvasNode>();
    for (const node of canvasNodes) {
      map.set(node.key, node);
    }
    return map;
  }, [canvasNodes]);

  const nodePortsByNodeKey = useMemo(() => {
    const map = new Map<string, NodePortsRuntime>();
    for (const node of canvasNodes) {
      const basePorts = buildNodePortsRuntime(metadataBundle.nodeTypesMap.get(node.type));
      map.set(node.key, deriveSelectorDynamicPorts(node, basePorts));
    }
    return map;
  }, [canvasNodes, metadataBundle.nodeTypesMap]);

  const variableSuggestions = useMemo(
    () =>
      buildVariableSuggestions(
        canvasNodes.map((node) => ({ key: node.key, type: node.type, configs: node.configs, x: node.x })),
        selectedNodeKey
      ),
    [canvasNodes, selectedNodeKey]
  );

  const renderConnections = useMemo(
    () =>
      canvasConnections
        .map((connection) => {
          const fromNode = nodeByKey.get(connection.fromNode);
          const toNode = nodeByKey.get(connection.toNode);
          if (!fromNode || !toNode) {
            return null;
          }
          const fromPorts = nodePortsByNodeKey.get(connection.fromNode) ?? buildNodePortsRuntime(undefined);
          const toPorts = nodePortsByNodeKey.get(connection.toNode) ?? buildNodePortsRuntime(undefined);
          const from = getPortAnchor(fromNode, fromPorts, "output", connection.fromPort);
          const to = getPortAnchor(toNode, toPorts, "input", connection.toPort);
          return { id: connection.id, d: connectionPath(from.x, from.y, to.x, to.y), running: runningConnectionIds.has(connection.id) };
        })
        .filter((item): item is { id: string; d: string; running: boolean } => item !== null),
    [canvasConnections, nodeByKey, nodePortsByNodeKey, runningConnectionIds]
  );

  function resolveWorldPoint(clientX: number, clientY: number): { x: number; y: number } | null {
    const shell = canvasShellRef.current;
    if (!shell) {
      return null;
    }
    const rect = shell.getBoundingClientRect();
    const nextScale = zoomRef.current / 100;
    if (nextScale <= 0) {
      return null;
    }
    return {
      x: (clientX - rect.left - panRef.current.x) / nextScale,
      y: (clientY - rect.top - panRef.current.y) / nextScale
    };
  }

  function clearConnectingState() {
    setConnectingPreview(null);
    setConnectableInputPortKeysByNode({});
  }

  function startNodeDrag(node: CanvasNode, event: React.PointerEvent<HTMLButtonElement>) {
    if (event.button !== 0) {
      return;
    }
    if ((event.target as HTMLElement).closest("[data-wf-port='true']")) {
      return;
    }
    operationRef.current = {
      kind: "drag-node",
      nodeKey: node.key,
      startClientX: event.clientX,
      startClientY: event.clientY,
      startX: node.x,
      startY: node.y
    };
    pointerIdRef.current = event.pointerId;
    setSelectedNodeKey(node.key);
    event.preventDefault();
    event.stopPropagation();
  }

  function computeConnectableInputPorts(fromNodeKey: string, fromPortKey: string): Record<string, Set<string>> {
    const sourceNode = nodeByKey.get(fromNodeKey);
    if (!sourceNode) {
      return {};
    }
    const sourcePorts = nodePortsByNodeKey.get(fromNodeKey) ?? buildNodePortsRuntime(undefined);
    const result: Record<string, Set<string>> = {};
    for (const target of canvasNodes) {
      if (target.key === fromNodeKey) {
        continue;
      }
      const targetPorts = nodePortsByNodeKey.get(target.key) ?? buildNodePortsRuntime(undefined);
      for (const input of targetPorts.inputs) {
        const check = validateConnectionCandidate(
          {
            fromNode: fromNodeKey,
            fromPort: fromPortKey,
            toNode: target.key,
            toPort: input.key
          },
          canvasConnections,
          sourcePorts.outputs,
          targetPorts.inputs
        );
        if (check.ok) {
          const set = result[target.key] ?? new Set<string>();
          set.add(input.key);
          result[target.key] = set;
        }
      }
    }
    return result;
  }

  function startConnection(node: CanvasNode, port: PortRuntime, event: React.PointerEvent<HTMLSpanElement>) {
    if (event.button !== 0 || port.direction !== "output") {
      return;
    }
    const ports = nodePortsByNodeKey.get(node.key) ?? buildNodePortsRuntime(undefined);
    const anchor = getPortAnchor(node, ports, "output", port.key);
    operationRef.current = { kind: "connect", fromNode: node.key, fromPort: port.key };
    pointerIdRef.current = event.pointerId;
    setConnectingPreview({
      fromNode: node.key,
      fromPort: port.key,
      startX: anchor.x,
      startY: anchor.y,
      currentX: anchor.x,
      currentY: anchor.y
    });
    setConnectableInputPortKeysByNode(computeConnectableInputPorts(node.key, port.key));
    event.preventDefault();
    event.stopPropagation();
  }

  function startPanCanvas(event: React.PointerEvent<HTMLDivElement>) {
    if (event.button !== 0 && event.button !== 1) {
      return;
    }
    const target = event.target as HTMLElement;
    if (target.closest(".wf-react-node") || target.closest(".wf-react-properties-panel") || target.closest(".wf-react-node-panel")) {
      return;
    }
    operationRef.current = {
      kind: "pan-canvas",
      startClientX: event.clientX,
      startClientY: event.clientY,
      startX: panRef.current.x,
      startY: panRef.current.y
    };
    pointerIdRef.current = event.pointerId;
    event.preventDefault();
  }

  useEffect(() => {
    const onPointerMove = (event: PointerEvent) => {
      if (pointerIdRef.current !== null && event.pointerId !== pointerIdRef.current) {
        return;
      }
      const operation = operationRef.current;
      if (!operation) {
        return;
      }

      if (operation.kind === "drag-node") {
        const dx = (event.clientX - operation.startClientX) / (zoomRef.current / 100);
        const dy = (event.clientY - operation.startClientY) / (zoomRef.current / 100);
        setCanvasNodes((prev) =>
          prev.map((node) =>
            node.key === operation.nodeKey
              ? {
                  ...node,
                  x: Math.round((operation.startX + dx) * 10) / 10,
                  y: Math.round((operation.startY + dy) * 10) / 10
                }
              : node
          )
        );
        return;
      }

      if (operation.kind === "pan-canvas") {
        const dx = event.clientX - operation.startClientX;
        const dy = event.clientY - operation.startClientY;
        setPan({
          x: operation.startX + dx,
          y: operation.startY + dy
        });
        return;
      }

      const world = resolveWorldPoint(event.clientX, event.clientY);
      if (!world) {
        return;
      }
      setConnectingPreview((prev) =>
        prev
          ? {
              ...prev,
              currentX: world.x,
              currentY: world.y
            }
          : null
      );
    };

    const onPointerUp = (event: PointerEvent) => {
      if (pointerIdRef.current !== null && event.pointerId !== pointerIdRef.current) {
        return;
      }
      const operation = operationRef.current;
      if (!operation) {
        return;
      }

      if (operation.kind === "drag-node") {
        setIsDirty(true);
        setCanvasValidation(null);
      } else if (operation.kind === "connect") {
        const target = document.elementFromPoint(event.clientX, event.clientY) as HTMLElement | null;
        const portElement = target?.closest("[data-wf-port='true']") as HTMLElement | null;
        const toNode = portElement?.dataset.nodeKey;
        const toPortKind = portElement?.dataset.portKind;
        const toPort = portElement?.dataset.portKey;
        if (toNode && toPortKind === "input" && toPort) {
          const sourceNode = nodeByKey.get(operation.fromNode);
          const targetNode = nodeByKey.get(toNode);
          if (sourceNode && targetNode) {
            const sourcePorts = nodePortsByNodeKey.get(sourceNode.key) ?? buildNodePortsRuntime(undefined);
            const targetPorts = nodePortsByNodeKey.get(targetNode.key) ?? buildNodePortsRuntime(undefined);
            const check = validateConnectionCandidate(
              {
                fromNode: operation.fromNode,
                fromPort: operation.fromPort,
                toNode,
                toPort
              },
              canvasConnections,
              sourcePorts.outputs,
              targetPorts.inputs
            );
            if (check.ok) {
              const nextId = `conn_${operation.fromNode}_${operation.fromPort}_${toNode}_${toPort}_${Date.now().toString(36)}`;
              setCanvasConnections((prev) => [
                ...prev,
                {
                  id: nextId,
                  fromNode: operation.fromNode,
                  fromPort: operation.fromPort,
                  toNode,
                  toPort,
                  condition: null
                }
              ]);
              setIsDirty(true);
              setCanvasValidation(null);
            } else {
              message.warning(check.message);
            }
          }
        }
        clearConnectingState();
      }

      operationRef.current = null;
      pointerIdRef.current = null;
    };

    window.addEventListener("pointermove", onPointerMove);
    window.addEventListener("pointerup", onPointerUp);
    window.addEventListener("pointercancel", onPointerUp);

    return () => {
      window.removeEventListener("pointermove", onPointerMove);
      window.removeEventListener("pointerup", onPointerUp);
      window.removeEventListener("pointercancel", onPointerUp);
    };
  }, [canvasConnections, canvasNodes, nodeByKey, nodePortsByNodeKey]);

  function centerPointForCreate(): { x: number; y: number } {
    const shell = canvasShellRef.current;
    if (!shell) {
      return { x: 320, y: 320 };
    }
    const worldX = shell.clientWidth / 2 / scale - pan.x / scale;
    const worldY = shell.clientHeight / 2 / scale - pan.y / scale;
    return { x: Math.max(40, worldX - NODE_WIDTH / 2), y: Math.max(40, worldY - NODE_HEIGHT / 2) };
  }

  function createNodeByType(nodeType: string, x: number, y: number) {
    const definition = nodeRegistry.resolve(nodeType);
    const normalizedType = definition.type;
    const template = metadataBundle.templatesMap.get(normalizedType);
    const key = `${nodeType.toLowerCase()}_${Date.now().toString(36)}`;
    const nextConfigs = mergeNodeDefaults(definition, template, {});
    const catalog = nodeMap.get(normalizedType);
    setCanvasNodes((prev: CanvasNode[]) => [
      ...prev,
      {
        key,
        type: normalizedType,
        title: catalog ? t(catalog.titleKey) : normalizedType,
        x: Math.round(x),
        y: Math.round(y),
        configs: nextConfigs,
        inputMappings: {}
      }
    ]);
    setSelectedNodeKey(key);
    setIsDirty(true);
    setCanvasValidation(null);
    return key;
  }

  function runCanvasValidationAndReport(): CanvasValidationResult {
    const result = validateCanvas(canvasNodes, canvasConnections, nodeTypesMeta);
    setCanvasValidation(result);
    if (!result.ok) {
      const firstNodeIssue = result.nodeResults.find((item) => item.issues.length > 0)?.issues[0];
      const firstCanvasIssue = result.canvasIssues[0];
      message.error(firstNodeIssue ?? firstCanvasIssue ?? "工作流配置存在校验错误，请先修复。");
    }
    return result;
  }

  const hasCanvasValidationErrors = Boolean(
    canvasValidation && (!canvasValidation.ok || canvasValidation.canvasIssues.length > 0 || canvasValidation.nodeResults.some((item) => item.issues.length > 0))
  );

  function appendLog(line: string) {
    setLogs((prev) => [...prev, `${new Date().toLocaleTimeString()} ${line}`]);
  }

  function markNodeState(nodeKey: string, state: "idle" | "running" | "success" | "failed" | "skipped", hint?: string) {
    setExecutionStateByNodeKey((prev) => ({
      ...prev,
      [nodeKey]: { state, hint }
    }));
  }

  async function handleRunTest() {
    if (testRunning) {
      streamAbortRef.current?.();
      streamAbortRef.current = null;
      setTestRunning(false);
      appendLog("execution_cancelled");
      return;
    }

    let parsedInputs: unknown = {};
    if (testInputJson.trim()) {
      try {
        parsedInputs = JSON.parse(testInputJson);
      } catch {
        message.error("测试输入 JSON 不合法。");
        return;
      }
    }

    setExecutionStateByNodeKey({});
    setRunningConnectionIds(new Set());
    setTestRunning(true);

    if (testRunMode === "stream" && props.apiClient.runStream) {
      const handle = props.apiClient.runStream(
        props.workflowId,
        {
          inputsJson: JSON.stringify(parsedInputs),
          source: testRunSource
        },
        {
          onExecutionStarted: (ev) => appendLog(`execution_start ${ev.executionId}`),
          onNodeStarted: (ev) => {
            markNodeState(ev.nodeKey, "running");
            setRunningConnectionIds(
              new Set(canvasConnections.filter((item) => item.fromNode === ev.nodeKey).map((item) => item.id))
            );
            appendLog(`node_start ${ev.nodeKey}`);
          },
          onNodeOutput: (ev) => appendLog(`node_output ${ev.nodeKey}`),
          onNodeCompleted: (ev) => {
            markNodeState(ev.nodeKey, "success", ev.durationMs ? `${ev.durationMs}ms` : undefined);
            setRunningConnectionIds(new Set());
            appendLog(`node_complete ${ev.nodeKey}`);
          },
          onNodeFailed: (ev) => {
            markNodeState(ev.nodeKey, "failed", ev.errorMessage);
            setRunningConnectionIds(new Set());
            appendLog(`node_failed ${ev.nodeKey} ${ev.errorMessage}`);
          },
          onExecutionCompleted: () => {
            setTestRunning(false);
            setRunningConnectionIds(new Set());
            appendLog("execution_complete");
          },
          onExecutionCancelled: (ev) => {
            setTestRunning(false);
            setRunningConnectionIds(new Set());
            appendLog(`execution_cancelled ${ev.errorMessage ?? ""}`.trim());
          },
          onExecutionInterrupted: (ev) => {
            setTestRunning(false);
            setRunningConnectionIds(new Set());
            appendLog(`execution_interrupted ${ev.nodeKey ?? ""}`.trim());
          },
          onExecutionFailed: (ev) => {
            setTestRunning(false);
            setRunningConnectionIds(new Set());
            appendLog(`execution_failed ${ev.errorMessage}`);
          },
          onError: (err) => {
            setTestRunning(false);
            setRunningConnectionIds(new Set());
            appendLog(`stream_error ${err instanceof Error ? err.message : "unknown"}`);
          }
        }
      );
      streamAbortRef.current = handle.abort;
      await handle.done;
      streamAbortRef.current = null;
      setTestRunning(false);
      return;
    }

    if (props.apiClient.runSync) {
      const result = await props.apiClient.runSync(props.workflowId, {
        inputsJson: JSON.stringify(parsedInputs),
        source: testRunSource
      });
      appendLog(`execution_start ${result.data?.executionId ?? "-"}`);
      if (result.data?.executionId && props.apiClient.getProcess) {
        const process = await props.apiClient.getProcess(result.data.executionId);
        const nodeExecutions = process.data?.nodeExecutions ?? [];
        for (const item of nodeExecutions) {
          if (item.status === 2) {
            markNodeState(item.nodeKey, "success");
          } else if (item.status === 3) {
            markNodeState(item.nodeKey, "failed", item.errorMessage);
          } else if (item.status === 6) {
            markNodeState(item.nodeKey, "skipped");
          }
        }
      }
      appendLog("execution_complete");
    }
    setTestRunning(false);
  }

  return (
    <div className="wf-react-editor-page">
      <WorkflowHeader
        name={workflowName}
        dirty={isDirty}
        onNameChange={(value) => {
          setWorkflowName(value);
          setIsDirty(true);
        }}
        onBack={() => props.onBack?.()}
        onSave={async () => {
          const result = runCanvasValidationAndReport();
          if (!result.ok) {
            return;
          }
          if (props.apiClient.saveDraft) {
            await props.apiClient.saveDraft(props.workflowId, {
              canvasJson: toCanvasJson(canvasNodes, canvasConnections)
            });
          }
          setIsDirty(false);
          setLogs((prev: string[]) => [...prev, `${new Date().toLocaleTimeString()} save_draft`]);
        }}
        onPublish={() => {
          const result = runCanvasValidationAndReport();
          if (!result.ok) {
            return;
          }
          setLogs((prev: string[]) => [...prev, `${new Date().toLocaleTimeString()} publish`]);
        }}
      />
      <div
        ref={canvasShellRef}
        className="wf-react-canvas-shell"
        onPointerDown={startPanCanvas}
        onDragOver={(event) => {
          if (draggingCatalogNodeType) {
            event.preventDefault();
            event.dataTransfer.dropEffect = "copy";
          }
        }}
        onDrop={(event) => {
          const nodeType =
            event.dataTransfer.getData("application/x-atlas-workflow-node-type") ||
            event.dataTransfer.getData("text/plain") ||
            draggingCatalogNodeType;
          if (!nodeType) {
            return;
          }
          event.preventDefault();
          const world = resolveWorldPoint(event.clientX, event.clientY);
          if (!world) {
            return;
          }
          createNodeByType(nodeType, Math.max(24, world.x - NODE_WIDTH / 2), Math.max(24, world.y - NODE_HEIGHT / 2));
          setDraggingCatalogNodeType(null);
          setShowNodePanel(false);
        }}
        onWheel={(event) => {
          if (!event.ctrlKey) {
            return;
          }
          event.preventDefault();
          setZoom((prev) => {
            const next = prev - event.deltaY * 0.06;
            return Math.max(25, Math.min(200, Math.round(next)));
          });
        }}
      >
        <div className="wf-react-dot-grid" />
        <div className="wf-react-scene" style={{ transform: `translate(${pan.x}px, ${pan.y}px) scale(${scale})` }}>
          <svg className="wf-react-edge-layer" width="100%" height="100%">
            {renderConnections.map((item) => (
              <path key={item.id} d={item.d} className={`wf-react-edge-path${item.running ? " wf-react-edge-path-running" : ""}`} />
            ))}
            {connectingPreview ? (
              <path
                d={connectionPath(connectingPreview.startX, connectingPreview.startY, connectingPreview.currentX, connectingPreview.currentY)}
                className="wf-react-edge-path wf-react-edge-preview"
              />
            ) : null}
          </svg>
          <div className="wf-react-node-layer">
            {canvasNodes.map((node: CanvasNode) => {
              const meta = nodeMap.get(node.type);
              if (!meta) {
                return null;
              }
              const nodePorts = nodePortsByNodeKey.get(node.key) ?? buildNodePortsRuntime(undefined);
              return (
                <div key={node.key} className="wf-react-node-wrap" style={{ left: node.x, top: node.y }}>
                  <NodeCard
                    nodeKey={node.key}
                    title={node.title || t(meta.titleKey)}
                    color={meta.color}
                    iconText={meta.iconText}
                    selected={selectedNodeKey === node.key}
                    subtitle={node.type}
                    inputPorts={nodePorts.inputs}
                    outputPorts={nodePorts.outputs}
                    connectableInputPortKeys={connectableInputPortKeysByNode[node.key]}
                    connectingFromNodeKey={connectingPreview?.fromNode}
                    onClick={() => setSelectedNodeKey(node.key)}
                    onPointerDown={(event) => startNodeDrag(node, event)}
                    onPortPointerDown={(event, port) => startConnection(node, port, event)}
                    executionState={executionStateByNodeKey[node.key]?.state}
                    executionHint={executionStateByNodeKey[node.key]?.hint}
                  />
                </div>
              );
            })}
          </div>
        </div>

        <NodePanelPopover
          visible={showNodePanel}
          nodes={WORKFLOW_NODE_CATALOG}
          onDragStart={(nodeType) => setDraggingCatalogNodeType(nodeType)}
          onDragEnd={() => setDraggingCatalogNodeType(null)}
          onSelect={(nodeType) => {
            const center = centerPointForCreate();
            createNodeByType(nodeType, center.x, center.y);
            setShowNodePanel(false);
          }}
        />

        <PropertiesPanel
          visible={Boolean(selectedNode)}
          selectedNode={selectedNode}
          selectedNodeLabel={selectedNode ? selectedNode.title || t(nodeMap.get(selectedNode.type)?.titleKey ?? selectedNode.type) : ""}
          template={selectedNode ? metadataBundle.templatesMap.get(selectedNode.type) : undefined}
          nodeTypeMeta={selectedNode ? metadataBundle.nodeTypesMap.get(selectedNode.type) : undefined}
          variableSuggestions={variableSuggestions}
          onChangeNode={(next) => {
            if (!selectedNode) {
              return;
            }
            setCanvasNodes((prev) =>
              prev.map((node) => (node.key === selectedNode.key ? { ...node, title: next.title, configs: next.configs } : node))
            );
            setIsDirty(true);
            setCanvasValidation(null);
          }}
          onClose={() => setSelectedNodeKey("")}
        />

        <TestRunPanel
          visible={showTestPanel}
          logs={logs}
          running={testRunning}
          source={testRunSource}
          mode={testRunMode}
          inputJson={testInputJson}
          onInputJsonChange={setTestInputJson}
          onSourceChange={setTestRunSource}
          onModeChange={setTestRunMode}
          onClose={() => setShowTestPanel(false)}
          onRun={() => void handleRunTest()}
        />

        <CanvasToolbar
          zoom={zoom}
          onZoomChange={(value: number) => setZoom(value)}
          onToggleNodePanel={() => setShowNodePanel((value: boolean) => !value)}
          onRun={() => setShowTestPanel((value: boolean) => !value)}
        />
      </div>
      {hasCanvasValidationErrors ? (
        <div className="wf-react-validation-banner">
          {canvasValidation?.canvasIssues.map((issue) => (
            <div key={issue}>{issue}</div>
          ))}
          {canvasValidation?.nodeResults
            .filter((item) => item.issues.length > 0)
            .slice(0, 10)
            .map((item) => (
              <div key={item.nodeKey}>{`${item.nodeKey}: ${item.issues[0]}`}</div>
            ))}
        </div>
      ) : null}
    </div>
  );
}