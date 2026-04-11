import { message } from "antd";
import { useEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { CanvasToolbar } from "../components/CanvasToolbar";
import { NodeCard } from "../components/NodeCard";
import { NodePanelPopover } from "../components/NodePanelPopover";
import { NodeDebugPanel } from "../components/NodeDebugPanel";
import { PropertiesPanel } from "../components/PropertiesPanel";
import { TestRunPanel } from "../components/TestRunPanel";
import { TracePanel, type TraceStepItem } from "../components/TracePanel";
import { MinimapPanel } from "../components/MinimapPanel";
import { ProblemPanel } from "../components/ProblemPanel";
import { VariablePanel } from "../components/VariablePanel";
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
import { resolveNodePorts } from "./dynamic-port-resolver";
import { validateCanvas, type CanvasValidationResult } from "./editor-validation";
import { buildVariableSuggestions } from "./smoke-utils";
import { WorkflowRenderProvider } from "../flowgram/workflow-render-provider";
import "./workflow-editor.css";

interface WorkflowApiClient {
  getDetail?: (id: string) => Promise<{ data?: WorkflowDetailResponse }>;
  saveDraft?: (id: string, req: WorkflowSaveRequest) => Promise<unknown>;
  publish?: (id: string, req: { changeLog?: string }) => Promise<unknown>;
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
      onNodeSkipped?: (ev: { nodeKey: string; reason?: string }) => void;
      onNodeBlocked?: (ev: { nodeKey: string; reason?: string }) => void;
      onEdgeStatusChanged?: (ev: {
        edge?: {
          sourceNodeKey?: string;
          sourcePort?: string;
          targetNodeKey?: string;
          targetPort?: string;
          status?: number;
        };
      }) => void;
      onBranchDecision?: (ev: {
        executionId?: string;
        nodeKey: string;
        nodeType?: string;
        selectedBranch?: string;
        candidates?: string[];
      }) => void;
      onExecutionCompleted?: (ev: { outputsJson?: string }) => void;
      onExecutionFailed?: (ev: { errorMessage: string }) => void;
      onExecutionCancelled?: (ev: { errorMessage?: string }) => void;
      onExecutionInterrupted?: (ev: { interruptType: string; nodeKey?: string }) => void;
      onError?: (err: Event | Error) => void;
    }
  ) => { abort: () => void; done: Promise<void> };
  getProcess?: (executionId: string) => Promise<{ data?: { nodeExecutions?: Array<{ nodeKey: string; status: number; errorMessage?: string }> } }>;
  debugNode?: (
    workflowId: string,
    nodeKey: string,
    req: { nodeKey: string; inputsJson?: string; inputs?: Record<string, unknown> }
  ) => Promise<{ data?: { outputsJson?: string; status: number; executionId?: string } }>;
}

export interface WorkflowEditorReactProps {
  workflowId: string;
  locale?: string;
  readOnly?: boolean;
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
  debugMeta?: Record<string, unknown>;
}

interface CanvasConnection extends ConnectionRuntime {}
type EdgeRuntimeState = "idle" | "running" | "success" | "failed" | "skipped";

interface DragNodeOperation {
  kind: "drag-node";
  nodeKeys: string[];
  startClientX: number;
  startClientY: number;
  startPositions: Record<string, { x: number; y: number }>;
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

interface BoxSelectOperation {
  kind: "box-select";
  startX: number;
  startY: number;
  currentX: number;
  currentY: number;
  additive: boolean;
}

type CanvasOperation = DragNodeOperation | PanCanvasOperation | ConnectOperation | BoxSelectOperation;

interface ClipboardSnapshot {
  nodes: CanvasNode[];
  connections: CanvasConnection[];
}

const nodeRegistry = new NodeRegistry();
const NODE_WIDTH = 360;
const NODE_HEIGHT = 160;
const USE_FLOWGRAM_CANVAS = true;

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
    outputSources: Array.isArray(node.outputSources) ? (node.outputSources as Array<Record<string, unknown>>) : undefined,
    debugMeta: isRecord(node.debugMeta) ? (node.debugMeta as Record<string, unknown>) : undefined
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

function parseCanvasJson(
  json: string | undefined
): { nodes: CanvasNode[]; connections: CanvasConnection[]; globals: Record<string, unknown>; viewport?: { x: number; y: number; zoom: number } } {
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

function toCanvasJson(
  nodes: CanvasNode[],
  connections: CanvasConnection[],
  globals: Record<string, unknown>,
  viewport: { x: number; y: number; zoom: number }
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

export function WorkflowEditorReact(props: WorkflowEditorReactProps) {
  ensureWorkflowI18n(props.locale ?? "zh-CN");
  const { t } = useTranslation();
  const isReadOnly = Boolean(props.readOnly);

  const canvasShellRef = useRef<HTMLDivElement | null>(null);
  const operationRef = useRef<CanvasOperation | null>(null);
  const pointerIdRef = useRef<number | null>(null);
  const panRef = useRef({ x: 0, y: 0 });
  const zoomRef = useRef(100);
  const spacePressedRef = useRef(false);

  const [workflowName, setWorkflowName] = useState(`Workflow_${props.workflowId}`);
  const [isDirty, setIsDirty] = useState(false);
  const [zoom, setZoom] = useState(100);
  const [pan, setPan] = useState({ x: 0, y: 0 });
  const [selectedNodeKeys, setSelectedNodeKeys] = useState<string[]>(["llm_1"]);
  const [showNodePanel, setShowNodePanel] = useState(false);
  const [showTestPanel, setShowTestPanel] = useState(false);
  const [showProblemPanel, setShowProblemPanel] = useState(false);
  const [showTracePanel, setShowTracePanel] = useState(false);
  const [showMinimap, setShowMinimap] = useState(false);
  const [showVariablePanel, setShowVariablePanel] = useState(false);
  const [showDebugPanel, setShowDebugPanel] = useState(false);
  const [interactionMode, setInteractionMode] = useState<"mouse" | "trackpad">("mouse");
  const [logs, setLogs] = useState<string[]>([]);
  const [traceSteps, setTraceSteps] = useState<TraceStepItem[]>([]);
  const [canvasNodes, setCanvasNodes] = useState<CanvasNode[]>(INITIAL_NODES);
  const [canvasConnections, setCanvasConnections] = useState<CanvasConnection[]>(INITIAL_CONNECTIONS);
  const [canvasGlobals, setCanvasGlobals] = useState<Record<string, unknown>>({});
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
  const [selectionBoxRect, setSelectionBoxRect] = useState<{ left: number; top: number; width: number; height: number } | null>(null);
  const [draggingCatalogNodeType, setDraggingCatalogNodeType] = useState<string | null>(null);
  const [executionStateByNodeKey, setExecutionStateByNodeKey] = useState<
    Record<string, { state: "idle" | "running" | "success" | "failed" | "skipped" | "blocked"; hint?: string }>
  >({});
  const [edgeStateByConnectionKey, setEdgeStateByConnectionKey] = useState<Record<string, EdgeRuntimeState>>({});
  const [testInputJson, setTestInputJson] = useState<string>('{"input":"hello"}');
  const [testRunMode, setTestRunMode] = useState<"stream" | "sync">("stream");
  const [testRunSource, setTestRunSource] = useState<"published" | "draft">("published");
  const [testRunning, setTestRunning] = useState(false);
  const [debugNodeKey, setDebugNodeKey] = useState("");
  const [debugInputJson, setDebugInputJson] = useState('{"input":"hello"}');
  const [debugOutput, setDebugOutput] = useState("");
  const [debugRunning, setDebugRunning] = useState(false);
  const streamAbortRef = useRef<null | (() => void)>(null);
  const clipboardRef = useRef<ClipboardSnapshot | null>(null);

  function buildEdgeRuntimeKey(connection: Pick<CanvasConnection, "fromNode" | "fromPort" | "toNode" | "toPort">): string {
    return `${connection.fromNode}:${connection.fromPort}->${connection.toNode}:${connection.toPort}`;
  }

  const scale = zoom / 100;
  const selectedNodeKey = selectedNodeKeys[0] ?? "";

  useEffect(() => {
    panRef.current = pan;
  }, [pan]);

  useEffect(() => {
    zoomRef.current = zoom;
  }, [zoom]);

  useEffect(() => {
    if (isReadOnly || !isDirty) {
      return;
    }
    const onBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = "";
    };
    window.addEventListener("beforeunload", onBeforeUnload);
    return () => {
      window.removeEventListener("beforeunload", onBeforeUnload);
    };
  }, [isDirty, isReadOnly]);

  useEffect(() => {
    let disposed = false;
    const load = async () => {
      const apiClient = props.apiClient;
      let loadedNodeTypes: NodeTypeMetadata[] = [];
      if (apiClient?.getNodeTypes) {
        const response = await apiClient.getNodeTypes();
        loadedNodeTypes = response.data ?? [];
        if (!disposed) {
          setNodeTypesMeta(loadedNodeTypes);
        }
      }
      if (apiClient?.getNodeTemplates) {
        const response = await apiClient.getNodeTemplates();
        if (!disposed) {
          setNodeTemplates(response.data ?? []);
        }
      }
      if (apiClient?.getDetail) {
        const response = await apiClient.getDetail(props.workflowId);
        if (!disposed && response.data) {
          const parsed = parseCanvasJson(response.data.canvasJson);
          const normalized = normalizeConnectionsByPorts(parsed.nodes, parsed.connections, loadedNodeTypes);
          setWorkflowName(response.data.name || `Workflow_${props.workflowId}`);
          setCanvasNodes(parsed.nodes);
          setCanvasConnections(normalized.connections);
          setCanvasGlobals(parsed.globals);
          if (parsed.viewport) {
            setPan({ x: parsed.viewport.x, y: parsed.viewport.y });
            setZoom(parsed.viewport.zoom);
          }
          if (normalized.migratedCount > 0) {
            message.info(`已迁移 ${normalized.migratedCount} 条历史连线到默认端口。`);
          }
          if (parsed.nodes.length > 0) {
            setSelectedNodeKeys([parsed.nodes[0]?.key ?? ""]);
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

  useEffect(() => {
    if (!debugNodeKey && selectedNode) {
      setDebugNodeKey(selectedNode.key);
    }
  }, [debugNodeKey, selectedNode]);

  function isEditableTarget(target: EventTarget | null): boolean {
    const element = target as HTMLElement | null;
    if (!element) {
      return false;
    }
    const tagName = element.tagName.toLowerCase();
    if (tagName === "input" || tagName === "textarea" || tagName === "select") {
      return true;
    }
    return Boolean(element.closest("input, textarea, [contenteditable='true']"));
  }

  function buildUniqueNodeKey(baseType: string, existingNodes: CanvasNode[]): string {
    const normalizedBase = `${baseType.toLowerCase()}_${Date.now().toString(36)}`;
    if (!existingNodes.some((node) => node.key === normalizedBase)) {
      return normalizedBase;
    }
    let cursor = 1;
    let candidate = `${normalizedBase}_${cursor}`;
    while (existingNodes.some((node) => node.key === candidate)) {
      cursor += 1;
      candidate = `${normalizedBase}_${cursor}`;
    }
    return candidate;
  }

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

  useEffect(() => {
    setSelectedNodeKeys((prev) => prev.filter((key) => nodeByKey.has(key)));
  }, [nodeByKey]);

  const nodePortsByNodeKey = useMemo(() => {
    const map = new Map<string, NodePortsRuntime>();
    for (const node of canvasNodes) {
      const basePorts = buildNodePortsRuntime(metadataBundle.nodeTypesMap.get(node.type));
      map.set(node.key, resolveNodePorts(node, basePorts));
    }
    return map;
  }, [canvasNodes, metadataBundle.nodeTypesMap]);

  const variableSuggestions = useMemo(
    () =>
      buildVariableSuggestions(
        canvasNodes.map((node) => ({ key: node.key, type: node.type, configs: node.configs, x: node.x })),
        selectedNodeKey,
        canvasConnections.map((connection) => ({ fromNode: connection.fromNode, toNode: connection.toNode })),
        canvasGlobals
      ),
    [canvasConnections, canvasGlobals, canvasNodes, selectedNodeKey]
  );
  const variablePanelItems = useMemo(
    () =>
      variableSuggestions.map((item) => ({
        key: item.value.replace(/^\{\{|\}\}$/g, ""),
        label: item.label ?? item.value,
        source: item.value.replace(/^\{\{|\}\}$/g, "").split(".")[0] ?? "vars"
      })),
    [variableSuggestions]
  );
  const debugNodeOptions = useMemo(
    () =>
      canvasNodes.map((node) => ({
        value: node.key,
        label: `${node.title || node.key} (${node.type})`
      })),
    [canvasNodes]
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
          const edgeState = edgeStateByConnectionKey[buildEdgeRuntimeKey(connection)] ?? "idle";
          return { id: connection.id, d: connectionPath(from.x, from.y, to.x, to.y), edgeState };
        })
        .filter((item): item is { id: string; d: string; edgeState: EdgeRuntimeState } => item !== null),
    [canvasConnections, edgeStateByConnectionKey, nodeByKey, nodePortsByNodeKey]
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

  function setSingleSelection(nodeKey: string) {
    setSelectedNodeKeys(nodeKey ? [nodeKey] : []);
  }

  function buildClipboardSnapshot(): ClipboardSnapshot | null {
    if (selectedNodeKeys.length === 0) {
      return null;
    }
    const selectedSet = new Set(selectedNodeKeys);
    const nodes = canvasNodes.filter((node) => selectedSet.has(node.key));
    if (nodes.length === 0) {
      return null;
    }
    const connections = canvasConnections.filter((line) => selectedSet.has(line.fromNode) && selectedSet.has(line.toNode));
    return {
      nodes: structuredClone(nodes),
      connections: structuredClone(connections)
    };
  }

  function pasteClipboardSnapshot(snapshot: ClipboardSnapshot): string[] {
    const usedKeys = new Set(canvasNodes.map((node) => node.key));
    const keyMap = new Map<string, string>();
    const buildNextKey = (baseType: string) => {
      let candidate = `${baseType.toLowerCase()}_${Date.now().toString(36)}`;
      let cursor = 1;
      while (usedKeys.has(candidate)) {
        candidate = `${baseType.toLowerCase()}_${Date.now().toString(36)}_${cursor}`;
        cursor += 1;
      }
      usedKeys.add(candidate);
      return candidate;
    };
    const createdNodes = snapshot.nodes.map((node) => {
      const nextKey = buildNextKey(node.type);
      keyMap.set(node.key, nextKey);
      return {
        ...structuredClone(node),
        key: nextKey,
        title: `${node.title}-副本`,
        x: node.x + 48,
        y: node.y + 48
      };
    });
    const createdConnections = snapshot.connections
      .map((line, index) => {
        const fromNode = keyMap.get(line.fromNode);
        const toNode = keyMap.get(line.toNode);
        if (!fromNode || !toNode) {
          return null;
        }
        return {
          ...structuredClone(line),
          id: `conn_${fromNode}_${line.fromPort}_${toNode}_${line.toPort}_${Date.now().toString(36)}_${index}`,
          fromNode,
          toNode
        };
      })
      .filter((item): item is CanvasConnection => item !== null);

    setCanvasNodes((prev) => [...prev, ...createdNodes]);
    setCanvasConnections((prev) => [...prev, ...createdConnections]);
    setIsDirty(true);
    setCanvasValidation(null);
    return createdNodes.map((node) => node.key);
  }

  function startNodeDrag(node: CanvasNode, event: React.PointerEvent<HTMLButtonElement>) {
    if (isReadOnly) {
      return;
    }
    if (event.button !== 0) {
      return;
    }
    if ((event.target as HTMLElement).closest("[data-wf-port='true']")) {
      return;
    }
    const dragNodeKeys = selectedNodeKeys.includes(node.key) ? selectedNodeKeys : [node.key];
    const startPositions: Record<string, { x: number; y: number }> = {};
    for (const key of dragNodeKeys) {
      const current = nodeByKey.get(key);
      if (current) {
        startPositions[key] = { x: current.x, y: current.y };
      }
    }
    operationRef.current = {
      kind: "drag-node",
      nodeKeys: dragNodeKeys,
      startClientX: event.clientX,
      startClientY: event.clientY,
      startPositions
    };
    pointerIdRef.current = event.pointerId;
    setSelectedNodeKeys(dragNodeKeys);
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
    if (isReadOnly) {
      return;
    }
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
    if (event.button === 1 || spacePressedRef.current) {
      operationRef.current = {
        kind: "pan-canvas",
        startClientX: event.clientX,
        startClientY: event.clientY,
        startX: panRef.current.x,
        startY: panRef.current.y
      };
      pointerIdRef.current = event.pointerId;
      event.preventDefault();
      return;
    }
    const world = resolveWorldPoint(event.clientX, event.clientY);
    if (!world) {
      return;
    }
    operationRef.current = {
      kind: "box-select",
      startX: world.x,
      startY: world.y,
      currentX: world.x,
      currentY: world.y,
      additive: event.shiftKey
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
            operation.nodeKeys.includes(node.key)
              ? {
                  ...node,
                  x: Math.round(((operation.startPositions[node.key]?.x ?? node.x) + dx) * 10) / 10,
                  y: Math.round(((operation.startPositions[node.key]?.y ?? node.y) + dy) * 10) / 10
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

      if (operation.kind === "box-select") {
        const world = resolveWorldPoint(event.clientX, event.clientY);
        if (!world) {
          return;
        }
        const nextOp: BoxSelectOperation = {
          ...operation,
          currentX: world.x,
          currentY: world.y
        };
        operationRef.current = nextOp;
        const left = Math.min(nextOp.startX, nextOp.currentX) * (zoomRef.current / 100) + panRef.current.x;
        const top = Math.min(nextOp.startY, nextOp.currentY) * (zoomRef.current / 100) + panRef.current.y;
        const width = Math.abs(nextOp.currentX - nextOp.startX) * (zoomRef.current / 100);
        const height = Math.abs(nextOp.currentY - nextOp.startY) * (zoomRef.current / 100);
        setSelectionBoxRect({ left, top, width, height });
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
      } else if (operation.kind === "box-select") {
        const minX = Math.min(operation.startX, operation.currentX);
        const maxX = Math.max(operation.startX, operation.currentX);
        const minY = Math.min(operation.startY, operation.currentY);
        const maxY = Math.max(operation.startY, operation.currentY);
        const width = Math.abs(operation.currentX - operation.startX);
        const height = Math.abs(operation.currentY - operation.startY);
        const selectedByBox = canvasNodes
          .filter((node) => node.x < maxX && node.x + NODE_WIDTH > minX && node.y < maxY && node.y + NODE_HEIGHT > minY)
          .map((node) => node.key);
        if (width < 2 && height < 2) {
          if (!operation.additive) {
            setSelectedNodeKeys([]);
          }
        } else if (operation.additive) {
          setSelectedNodeKeys((prev) => Array.from(new Set([...prev, ...selectedByBox])));
        } else {
          setSelectedNodeKeys(selectedByBox);
        }
        setSelectionBoxRect(null);
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
        setSelectionBoxRect(null);
      } else if (operation.kind === "pan-canvas") {
        setSelectionBoxRect(null);
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
    if (isReadOnly) {
      return "";
    }
    const definition = nodeRegistry.resolve(nodeType);
    const normalizedType = definition.type;
    const template = metadataBundle.templatesMap.get(normalizedType);
    const key = buildUniqueNodeKey(nodeType, canvasNodes);
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
    setSingleSelection(key);
    setIsDirty(true);
    setCanvasValidation(null);
    return key;
  }

  function runCanvasValidationAndReport(): CanvasValidationResult {
    const result = validateCanvas(canvasNodes, canvasConnections, nodeTypesMeta, canvasGlobals);
    setCanvasValidation(result);
    setShowProblemPanel(!result.ok);
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
  const flowgramCanvasSchema: CanvasSchema = {
    nodes: canvasNodes.map((node) => ({
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
    connections: canvasConnections.map((line) => ({
      fromNode: line.fromNode,
      fromPort: line.fromPort,
      toNode: line.toNode,
      toPort: line.toPort,
      condition: line.condition
    })),
    schemaVersion: 2,
    globals: canvasGlobals,
    viewport: { x: pan.x, y: pan.y, zoom }
  };

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (isEditableTarget(event.target)) {
        return;
      }

      if (event.key === " ") {
        spacePressedRef.current = true;
      }

      const isMeta = event.ctrlKey || event.metaKey;
      const lowerKey = event.key.toLowerCase();

      if (isMeta && lowerKey === "c") {
        if (isReadOnly) {
          return;
        }
        const snapshot = buildClipboardSnapshot();
        if (!snapshot) {
          return;
        }
        clipboardRef.current = snapshot;
        event.preventDefault();
        return;
      }

      if (isMeta && lowerKey === "v") {
        if (isReadOnly) {
          return;
        }
        if (!clipboardRef.current) {
          return;
        }
        const createdNodeKeys = pasteClipboardSnapshot(clipboardRef.current);
        setSelectedNodeKeys(createdNodeKeys);
        event.preventDefault();
        return;
      }

      if (isMeta && lowerKey === "d") {
        if (isReadOnly) {
          return;
        }
        const snapshot = buildClipboardSnapshot();
        if (!snapshot) {
          return;
        }
        clipboardRef.current = snapshot;
        const createdNodeKeys = pasteClipboardSnapshot(snapshot);
        setSelectedNodeKeys(createdNodeKeys);
        event.preventDefault();
        return;
      }

      if (isMeta && lowerKey === "a") {
        setSelectedNodeKeys(canvasNodes.map((node) => node.key));
        event.preventDefault();
        return;
      }

      if (event.key === "Delete" || event.key === "Backspace") {
        if (isReadOnly) {
          return;
        }
        if (selectedNodeKeys.length === 0) {
          return;
        }
        const selectedSet = new Set(selectedNodeKeys);
        const remainingNodes = canvasNodes.filter((node) => !selectedSet.has(node.key));
        if (remainingNodes.length === canvasNodes.length) {
          return;
        }
        setCanvasNodes(remainingNodes);
        setCanvasConnections((prev) => prev.filter((line) => !selectedSet.has(line.fromNode) && !selectedSet.has(line.toNode)));
        setSelectedNodeKeys(remainingNodes.length > 0 ? [remainingNodes[0]?.key ?? ""] : []);
        setIsDirty(true);
        setCanvasValidation(null);
        event.preventDefault();
        return;
      }

      if (isMeta && lowerKey === "s") {
        if (isReadOnly) {
          return;
        }
        event.preventDefault();
        void (async () => {
          const result = runCanvasValidationAndReport();
          if (!result.ok || !props.apiClient.saveDraft) {
            return;
          }
          await props.apiClient.saveDraft(props.workflowId, {
            canvasJson: toCanvasJson(canvasNodes, canvasConnections, canvasGlobals, { x: pan.x, y: pan.y, zoom })
          });
          setIsDirty(false);
          appendLog("save_draft");
        })();
      }
    };

    const onKeyUp = (event: KeyboardEvent) => {
      if (event.key === " ") {
        spacePressedRef.current = false;
      }
    };

    window.addEventListener("keydown", onKeyDown);
    window.addEventListener("keyup", onKeyUp);
    return () => {
      window.removeEventListener("keydown", onKeyDown);
      window.removeEventListener("keyup", onKeyUp);
    };
  }, [canvasConnections, canvasGlobals, canvasNodes, isReadOnly, pan.x, pan.y, props.apiClient, props.workflowId, selectedNodeKeys, zoom]);

  function appendLog(line: string) {
    setLogs((prev) => [...prev, `${new Date().toLocaleTimeString()} ${line}`]);
  }

  function appendTrace(step: TraceStepItem) {
    setTraceSteps((prev) => [...prev, step]);
  }

  function markNodeState(nodeKey: string, state: "idle" | "running" | "success" | "failed" | "skipped" | "blocked", hint?: string) {
    setExecutionStateByNodeKey((prev) => ({
      ...prev,
      [nodeKey]: { state, hint }
    }));
    setCanvasNodes((prev) =>
      prev.map((node) =>
        node.key === nodeKey
          ? {
              ...node,
              debugMeta: {
                ...(node.debugMeta ?? {}),
                executionState: state,
                executionHint: hint
              }
            }
          : node
      )
    );
  }

  function markOutgoingEdgesState(nodeKey: string, state: EdgeRuntimeState) {
    setEdgeStateByConnectionKey((prev) => {
      const next = { ...prev };
      for (const connection of canvasConnections) {
        if (connection.fromNode === nodeKey) {
          next[buildEdgeRuntimeKey(connection)] = state;
        }
      }
      return next;
    });
  }

  function markEdgeStateByRuntimeEdge(
    edge: { sourceNodeKey?: string; sourcePort?: string; targetNodeKey?: string; targetPort?: string },
    status: number | undefined
  ) {
    if (!edge.sourceNodeKey || !edge.sourcePort || !edge.targetNodeKey || !edge.targetPort) {
      return;
    }
    const mappedState: EdgeRuntimeState =
      status === 1 ? "success" : status === 2 ? "skipped" : status === 3 ? "failed" : "idle";
    const key = buildEdgeRuntimeKey({
      fromNode: edge.sourceNodeKey,
      fromPort: edge.sourcePort,
      toNode: edge.targetNodeKey,
      toPort: edge.targetPort
    });
    setEdgeStateByConnectionKey((prev) => ({ ...prev, [key]: mappedState }));
  }

  function resetRuntimeVisualization() {
    setExecutionStateByNodeKey({});
    setEdgeStateByConnectionKey({});
    setCanvasNodes((prev) =>
      prev.map((node) => {
        if (!node.debugMeta) {
          return node;
        }
        const nextDebugMeta = { ...node.debugMeta };
        delete nextDebugMeta.executionState;
        delete nextDebugMeta.executionHint;
        return {
          ...node,
          debugMeta: Object.keys(nextDebugMeta).length > 0 ? nextDebugMeta : undefined
        };
      })
    );
    setTraceSteps([]);
  }

  async function handleRunTest() {
    if (testRunning) {
      streamAbortRef.current?.();
      streamAbortRef.current = null;
      setTestRunning(false);
      appendLog("execution_cancelled");
      return;
    }

    const validation = runCanvasValidationAndReport();
    if (!validation.ok) {
      setShowProblemPanel(true);
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

    resetRuntimeVisualization();
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
            markOutgoingEdgesState(ev.nodeKey, "running");
            appendTrace({
              timestamp: new Date().toLocaleTimeString(),
              nodeKey: ev.nodeKey,
              status: "running"
            });
            appendLog(`node_start ${ev.nodeKey}`);
          },
          onNodeOutput: (ev) => appendLog(`node_output ${ev.nodeKey}`),
          onNodeCompleted: (ev) => {
            markNodeState(ev.nodeKey, "success", ev.durationMs ? `${ev.durationMs}ms` : undefined);
            markOutgoingEdgesState(ev.nodeKey, "success");
            appendTrace({
              timestamp: new Date().toLocaleTimeString(),
              nodeKey: ev.nodeKey,
              status: "success",
              detail: ev.durationMs ? `${ev.durationMs}ms` : undefined
            });
            appendLog(`node_complete ${ev.nodeKey}`);
          },
          onNodeFailed: (ev) => {
            markNodeState(ev.nodeKey, "failed", ev.errorMessage);
            markOutgoingEdgesState(ev.nodeKey, "failed");
            appendTrace({
              timestamp: new Date().toLocaleTimeString(),
              nodeKey: ev.nodeKey,
              status: "failed",
              detail: ev.errorMessage
            });
            appendLog(`node_failed ${ev.nodeKey} ${ev.errorMessage}`);
          },
          onNodeSkipped: (ev) => {
            markNodeState(ev.nodeKey, "skipped", ev.reason);
            markOutgoingEdgesState(ev.nodeKey, "skipped");
            appendTrace({
              timestamp: new Date().toLocaleTimeString(),
              nodeKey: ev.nodeKey,
              status: "skipped",
              detail: ev.reason
            });
            appendLog(`node_skipped ${ev.nodeKey}`);
          },
          onNodeBlocked: (ev) => {
            markNodeState(ev.nodeKey, "blocked", ev.reason);
            markOutgoingEdgesState(ev.nodeKey, "skipped");
            appendTrace({
              timestamp: new Date().toLocaleTimeString(),
              nodeKey: ev.nodeKey,
              status: "blocked",
              detail: ev.reason
            });
            appendLog(`node_blocked ${ev.nodeKey} ${ev.reason ?? ""}`.trim());
          },
          onEdgeStatusChanged: (ev) => {
            markEdgeStateByRuntimeEdge(ev.edge ?? {}, ev.edge?.status);
          },
          onBranchDecision: (ev) => {
            const candidatesText = Array.isArray(ev.candidates) && ev.candidates.length > 0 ? ev.candidates.join(",") : "-";
            appendTrace({
              timestamp: new Date().toLocaleTimeString(),
              nodeKey: ev.nodeKey,
              status: "success",
              detail: `branch=${ev.selectedBranch ?? "-"} | candidates=${candidatesText}`
            });
            appendLog(`branch_decision ${ev.nodeKey} ${ev.selectedBranch ?? "-"}`);
          },
          onExecutionCompleted: () => {
            setTestRunning(false);
            appendLog("execution_complete");
          },
          onExecutionCancelled: (ev) => {
            setTestRunning(false);
            appendLog(`execution_cancelled ${ev.errorMessage ?? ""}`.trim());
          },
          onExecutionInterrupted: (ev) => {
            setTestRunning(false);
            appendLog(`execution_interrupted ${ev.nodeKey ?? ""}`.trim());
          },
          onExecutionFailed: (ev) => {
            setTestRunning(false);
            appendLog(`execution_failed ${ev.errorMessage}`);
          },
          onError: (err) => {
            setTestRunning(false);
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
            markOutgoingEdgesState(item.nodeKey, "success");
            appendTrace({ timestamp: new Date().toLocaleTimeString(), nodeKey: item.nodeKey, status: "success" });
          } else if (item.status === 3) {
            markNodeState(item.nodeKey, "failed", item.errorMessage);
            markOutgoingEdgesState(item.nodeKey, "failed");
            appendTrace({
              timestamp: new Date().toLocaleTimeString(),
              nodeKey: item.nodeKey,
              status: "failed",
              detail: item.errorMessage
            });
          } else if (item.status === 6) {
            markNodeState(item.nodeKey, "skipped");
            markOutgoingEdgesState(item.nodeKey, "skipped");
            appendTrace({ timestamp: new Date().toLocaleTimeString(), nodeKey: item.nodeKey, status: "skipped" });
          } else if (item.status === 7) {
            markNodeState(item.nodeKey, "blocked", item.errorMessage);
            markOutgoingEdgesState(item.nodeKey, "skipped");
            appendTrace({
              timestamp: new Date().toLocaleTimeString(),
              nodeKey: item.nodeKey,
              status: "blocked",
              detail: item.errorMessage
            });
          }
        }
      }
      appendLog("execution_complete");
    }
    setTestRunning(false);
  }

  function handleAutoLayout() {
    if (isReadOnly) {
      return;
    }
    const xStart = 80;
    const yStart = 80;
    const colGap = 420;
    const rowGap = 220;
    setCanvasNodes((prev) =>
      prev.map((node, index) => ({
        ...node,
        x: xStart + (index % 4) * colGap,
        y: yStart + Math.floor(index / 4) * rowGap
      }))
    );
    setIsDirty(true);
  }

  async function handleDebugNode() {
    if (!props.apiClient.debugNode || !debugNodeKey) {
      message.warning("当前环境未启用单节点调试接口。");
      return;
    }
    let parsed: unknown = {};
    if (debugInputJson.trim()) {
      try {
        parsed = JSON.parse(debugInputJson);
      } catch {
        message.error("调试输入 JSON 不合法。");
        return;
      }
    }

    setDebugRunning(true);
    markNodeState(debugNodeKey, "running", "debug");
    try {
      const response = await props.apiClient.debugNode(props.workflowId, debugNodeKey, {
        nodeKey: debugNodeKey,
        inputsJson: JSON.stringify(parsed)
      });
      const outputText = JSON.stringify(response.data ?? {}, null, 2);
      setDebugOutput(outputText);
      markNodeState(debugNodeKey, "success", "debug ok");
      setCanvasNodes((prev) =>
        prev.map((node) =>
          node.key === debugNodeKey
            ? {
                ...node,
                debugMeta: {
                  ...(node.debugMeta ?? {}),
                  debugResult: response.data ?? null,
                  lastDebugAt: new Date().toISOString()
                }
              }
            : node
        )
      );
      appendLog(`node_debug ${debugNodeKey} success`);
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : "调试失败";
      setDebugOutput(errorMessage);
      markNodeState(debugNodeKey, "failed", errorMessage);
      appendLog(`node_debug ${debugNodeKey} failed: ${errorMessage}`);
    } finally {
      setDebugRunning(false);
    }
  }

  return (
    <div className="wf-react-editor-page">
      <WorkflowHeader
        name={workflowName}
        dirty={isDirty}
        readOnly={isReadOnly}
        onNameChange={(value) => {
          if (isReadOnly) {
            return;
          }
          setWorkflowName(value);
          setIsDirty(true);
        }}
        onBack={() => props.onBack?.()}
        onSave={async () => {
          if (isReadOnly) {
            message.warning("只读模式下不可保存。");
            return;
          }
          const result = runCanvasValidationAndReport();
          if (!result.ok) {
            return;
          }
          if (props.apiClient.saveDraft) {
            await props.apiClient.saveDraft(props.workflowId, {
              canvasJson: toCanvasJson(canvasNodes, canvasConnections, canvasGlobals, { x: pan.x, y: pan.y, zoom })
            });
          }
          setIsDirty(false);
          setLogs((prev: string[]) => [...prev, `${new Date().toLocaleTimeString()} save_draft`]);
        }}
        onPublish={async () => {
          if (isReadOnly) {
            message.warning("只读模式下不可发布。");
            return;
          }
          const result = runCanvasValidationAndReport();
          if (!result.ok) {
            return;
          }
          try {
            if (props.apiClient.saveDraft && isDirty) {
              await props.apiClient.saveDraft(props.workflowId, {
                canvasJson: toCanvasJson(canvasNodes, canvasConnections, canvasGlobals, { x: pan.x, y: pan.y, zoom })
              });
            }
            if (props.apiClient.publish) {
              await props.apiClient.publish(props.workflowId, {});
              message.success("工作流已发布。");
              setIsDirty(false);
            } else {
              message.warning("当前环境未启用发布接口。");
            }
            setLogs((prev: string[]) => [...prev, `${new Date().toLocaleTimeString()} publish`]);
          } catch (error) {
            message.error(error instanceof Error ? error.message : "发布失败");
          }
        }}
      />
      <div
        ref={canvasShellRef}
        className="wf-react-canvas-shell"
        onPointerDown={USE_FLOWGRAM_CANVAS ? undefined : startPanCanvas}
        onDragOver={(event) => {
          if (draggingCatalogNodeType) {
            event.preventDefault();
            event.dataTransfer.dropEffect = "copy";
          }
        }}
        onDrop={(event) => {
          if (isReadOnly) {
            return;
          }
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
        {USE_FLOWGRAM_CANVAS ? (
          <WorkflowRenderProvider
            canvas={flowgramCanvasSchema}
            readonly={isReadOnly}
            nodeTypesMeta={nodeTypesMeta}
            edgeStateByKey={edgeStateByConnectionKey}
            onSelectionChange={(nodeKeys) => setSelectedNodeKeys(nodeKeys)}
            onCanvasChange={(next) => {
              if (isReadOnly) {
                return;
              }
              const nextNodes: CanvasNode[] = next.nodes.map((node) => ({
                key: node.key,
                type: node.type,
                title: node.title,
                x: node.layout?.x ?? 0,
                y: node.layout?.y ?? 0,
                configs: node.configs,
                inputMappings: node.inputMappings,
                childCanvas: node.childCanvas,
                inputTypes: node.inputTypes,
                outputTypes: node.outputTypes,
                inputSources: node.inputSources,
                outputSources: node.outputSources,
                debugMeta: node.debugMeta
              }));
              const nextConnections: CanvasConnection[] = next.connections.map((line, index) => ({
                id: `conn_${line.fromNode}_${line.fromPort}_${line.toNode}_${line.toPort}_${index}`,
                fromNode: line.fromNode,
                fromPort: line.fromPort,
                toNode: line.toNode,
                toPort: line.toPort,
                condition: line.condition
              }));

              setCanvasNodes(nextNodes);
              setCanvasConnections(nextConnections);
              setIsDirty(true);
            }}
          />
        ) : (
          <>
            <div className="wf-react-dot-grid" />
            <div className="wf-react-scene" style={{ transform: `translate(${pan.x}px, ${pan.y}px) scale(${scale})` }}>
              <svg className="wf-react-edge-layer" width="100%" height="100%">
                {renderConnections.map((item) => (
                  <path
                    key={item.id}
                    d={item.d}
                    className={`wf-react-edge-path${
                      item.edgeState === "running"
                        ? " wf-react-edge-path-running"
                        : item.edgeState === "success"
                          ? " wf-react-edge-path-success"
                          : item.edgeState === "failed"
                            ? " wf-react-edge-path-failed"
                            : item.edgeState === "skipped"
                              ? " wf-react-edge-path-skipped"
                              : ""
                    }`}
                  />
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
                        selected={selectedNodeKeys.includes(node.key)}
                        subtitle={node.type}
                        inputPorts={nodePorts.inputs}
                        outputPorts={nodePorts.outputs}
                        connectableInputPortKeys={connectableInputPortKeysByNode[node.key]}
                        connectingFromNodeKey={connectingPreview?.fromNode}
                        onClick={(event) => {
                          if (event.shiftKey) {
                            setSelectedNodeKeys((prev) =>
                              prev.includes(node.key) ? prev.filter((key) => key !== node.key) : [...prev, node.key]
                            );
                            return;
                          }
                          setSingleSelection(node.key);
                        }}
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
          </>
        )}
        {selectionBoxRect && !USE_FLOWGRAM_CANVAS ? (
          <div
            className="wf-react-selection-box"
            style={{
              left: selectionBoxRect.left,
              top: selectionBoxRect.top,
              width: selectionBoxRect.width,
              height: selectionBoxRect.height
            }}
          />
        ) : null}

        <NodePanelPopover
          visible={showNodePanel}
          nodes={WORKFLOW_NODE_CATALOG}
          onDragStart={(nodeType) => setDraggingCatalogNodeType(nodeType)}
          onDragEnd={() => setDraggingCatalogNodeType(null)}
          onSelect={(nodeType) => {
            if (isReadOnly) {
              return;
            }
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
            if (isReadOnly) {
              return;
            }
            if (!selectedNode) {
              return;
            }
            setCanvasNodes((prev) =>
              prev.map((node) => (node.key === selectedNode.key ? { ...node, title: next.title, configs: next.configs } : node))
            );
            setIsDirty(true);
            setCanvasValidation(null);
          }}
          onClose={() => setSingleSelection("")}
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

        <TracePanel visible={showTracePanel} steps={traceSteps} onClose={() => setShowTracePanel(false)} />

        <ProblemPanel
          visible={showProblemPanel}
          validation={canvasValidation}
          onClose={() => setShowProblemPanel(false)}
          onSelectNode={(nodeKey) => {
            setSingleSelection(nodeKey);
            setShowProblemPanel(false);
          }}
        />

        <NodeDebugPanel
          visible={showDebugPanel}
          running={debugRunning}
          nodeOptions={debugNodeOptions}
          selectedNodeKey={debugNodeKey}
          inputJson={debugInputJson}
          output={debugOutput}
          onNodeChange={setDebugNodeKey}
          onInputJsonChange={setDebugInputJson}
          onRun={() => void handleDebugNode()}
          onClose={() => setShowDebugPanel(false)}
        />

        <VariablePanel
          visible={showVariablePanel}
          variables={variablePanelItems}
          globals={canvasGlobals}
          onChangeGlobals={(next) => {
            if (isReadOnly) {
              return;
            }
            setCanvasGlobals(next);
            setIsDirty(true);
          }}
          onClose={() => setShowVariablePanel(false)}
        />

        <MinimapPanel visible={showMinimap} nodes={canvasNodes.map((node) => ({ key: node.key, x: node.x, y: node.y }))} selectedNodeKey={selectedNodeKey} />

        <CanvasToolbar
          zoom={zoom}
          mode={interactionMode}
          minimapVisible={showMinimap}
          readOnly={isReadOnly}
          onZoomChange={(value: number) => setZoom(value)}
          onModeChange={setInteractionMode}
          onToggleNodePanel={() => setShowNodePanel((value: boolean) => !value)}
          onToggleMinimap={() => setShowMinimap((value) => !value)}
          onAutoLayout={handleAutoLayout}
          onToggleVariables={() => setShowVariablePanel((value) => !value)}
          onToggleDebug={() => setShowDebugPanel((value) => !value)}
          onToggleTrace={() => setShowTracePanel((value) => !value)}
          onToggleProblems={() => setShowProblemPanel((value) => !value)}
          onRun={() => setShowTestPanel((value: boolean) => !value)}
        />
      </div>
      {hasCanvasValidationErrors ? <div className="wf-react-validation-banner">检测到校验问题，可点击“问题”按钮查看详情。</div> : null}
    </div>
  );
}