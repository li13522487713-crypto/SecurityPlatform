import { useEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { ensureWorkflowI18n } from "../i18n";
import { WORKFLOW_NODE_CATALOG, type WorkflowNodeCatalogItem } from "../constants/node-catalog";
import { createMetadataBundle, mergeNodeDefaults, NodeRegistry } from "../node-registry";
import type {
  CanvasSchema,
  NodeTemplateMetadata,
  NodeTypeMetadata,
  WorkflowDetailResponse,
  WorkflowNodeTypeKey,
  WorkflowSaveRequest
} from "../types";
import { WorkflowHeader } from "../components/WorkflowHeader";
import { CanvasToolbar } from "../components/CanvasToolbar";
import { NodePanelPopover } from "../components/NodePanelPopover";
import { NodeCard } from "../components/NodeCard";
import { PropertiesPanel } from "../components/PropertiesPanel";
import { TestRunPanel } from "../components/TestRunPanel";
import { buildVariableSuggestions } from "./smoke-utils";
import "./workflow-editor.css";

interface WorkflowApiClient {
  getDetail?: (id: string) => Promise<{ data?: WorkflowDetailResponse }>;
  saveDraft?: (id: string, req: WorkflowSaveRequest) => Promise<unknown>;
  getNodeTypes?: () => Promise<{ data?: NodeTypeMetadata[] }>;
  getNodeTemplates?: () => Promise<{ data?: NodeTemplateMetadata[] }>;
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
}

interface CanvasConnection {
  id: string;
  fromNode: string;
  fromPort: string;
  toNode: string;
  toPort: string;
  condition: string | null;
}

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
  fromPort: "output";
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
    inputMappings
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
      inputMappings: node.inputMappings
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

export function WorkflowEditorReact(props: WorkflowEditorReactProps) {
  ensureWorkflowI18n(props.locale ?? "zh-CN");
  const { t } = useTranslation();

  const canvasShellRef = useRef<HTMLDivElement | null>(null);
  const operationRef = useRef<CanvasOperation | null>(null);
  const pointerIdRef = useRef<number | null>(null);
  const panRef = useRef({ x: 0, y: 0 });
  const zoomRef = useRef(100);
  const nodesRef = useRef<CanvasNode[]>(INITIAL_NODES);

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
  const [connectingPreview, setConnectingPreview] = useState<{
    fromNode: string;
    startX: number;
    startY: number;
    currentX: number;
    currentY: number;
  } | null>(null);

  const scale = zoom / 100;

  useEffect(() => {
    panRef.current = pan;
  }, [pan]);

  useEffect(() => {
    zoomRef.current = zoom;
  }, [zoom]);

  useEffect(() => {
    nodesRef.current = canvasNodes;
  }, [canvasNodes]);

  useEffect(() => {
    let disposed = false;
    const load = async () => {
      if (props.apiClient.getNodeTypes) {
        const response = await props.apiClient.getNodeTypes();
        if (!disposed) {
          setNodeTypesMeta(response.data ?? []);
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
          setWorkflowName(response.data.name || `Workflow_${props.workflowId}`);
          setCanvasNodes(parsed.nodes);
          setCanvasConnections(parsed.connections);
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

  const variableSuggestions = useMemo(
    () =>
      buildVariableSuggestions(
        canvasNodes.map((node) => ({ key: node.key, type: node.type, configs: node.configs, x: node.x })),
        selectedNodeKey
      ),
    [canvasNodes, selectedNodeKey]
  );

  const nodeByKey = useMemo(() => {
    const map = new Map<string, CanvasNode>();
    for (const node of canvasNodes) {
      map.set(node.key, node);
    }
    return map;
  }, [canvasNodes]);

  const renderConnections = useMemo(
    () =>
      canvasConnections
        .map((connection) => {
          const fromNode = nodeByKey.get(connection.fromNode);
          const toNode = nodeByKey.get(connection.toNode);
          if (!fromNode || !toNode) {
            return null;
          }
          const fromX = fromNode.x + NODE_WIDTH;
          const fromY = fromNode.y + NODE_HEIGHT / 2;
          const toX = toNode.x;
          const toY = toNode.y + NODE_HEIGHT / 2;
          return { id: connection.id, d: connectionPath(fromX, fromY, toX, toY) };
        })
        .filter((item): item is { id: string; d: string } => item !== null),
    [canvasConnections, nodeByKey]
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

  function startConnection(node: CanvasNode, side: "left" | "right", event: React.PointerEvent<HTMLSpanElement>) {
    if (side !== "right" || event.button !== 0) {
      return;
    }
    const startX = node.x + NODE_WIDTH;
    const startY = node.y + NODE_HEIGHT / 2;
    operationRef.current = { kind: "connect", fromNode: node.key, fromPort: "output" };
    pointerIdRef.current = event.pointerId;
    setConnectingPreview({
      fromNode: node.key,
      startX,
      startY,
      currentX: startX,
      currentY: startY
    });
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
      } else if (operation.kind === "connect") {
        const target = document.elementFromPoint(event.clientX, event.clientY) as HTMLElement | null;
        const portElement = target?.closest("[data-wf-port='true']") as HTMLElement | null;
        const toNode = portElement?.dataset.nodeKey;
        const toPortKind = portElement?.dataset.portKind;
        if (toNode && toPortKind === "input" && toNode !== operation.fromNode) {
          const nextId = `conn_${operation.fromNode}_${toNode}_${Date.now().toString(36)}`;
          setCanvasConnections((prev) => {
            const duplicate = prev.some(
              (item) =>
                item.fromNode === operation.fromNode &&
                item.fromPort === operation.fromPort &&
                item.toNode === toNode &&
                item.toPort === "input"
            );
            if (duplicate) {
              return prev;
            }
            setIsDirty(true);
            return [
              ...prev,
              {
                id: nextId,
                fromNode: operation.fromNode,
                fromPort: operation.fromPort,
                toNode,
                toPort: "input",
                condition: null
              }
            ];
          });
        }
        setConnectingPreview(null);
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
  }, []);

  function centerPointForCreate(): { x: number; y: number } {
    const shell = canvasShellRef.current;
    if (!shell) {
      return { x: 320, y: 320 };
    }
    const worldX = shell.clientWidth / 2 / scale - pan.x / scale;
    const worldY = shell.clientHeight / 2 / scale - pan.y / scale;
    return { x: Math.max(40, worldX - NODE_WIDTH / 2), y: Math.max(40, worldY - NODE_HEIGHT / 2) };
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
          if (props.apiClient.saveDraft) {
            await props.apiClient.saveDraft(props.workflowId, {
              canvasJson: toCanvasJson(canvasNodes, canvasConnections)
            });
          }
          setIsDirty(false);
          setLogs((prev: string[]) => [...prev, `${new Date().toLocaleTimeString()} save_draft`]);
        }}
        onPublish={() => setLogs((prev: string[]) => [...prev, `${new Date().toLocaleTimeString()} publish`])}
      />
      <div
        ref={canvasShellRef}
        className="wf-react-canvas-shell"
        onPointerDown={startPanCanvas}
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
              <path key={item.id} d={item.d} className="wf-react-edge-path" />
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
              return (
                <div key={node.key} className="wf-react-node-wrap" style={{ left: node.x, top: node.y }}>
                  <NodeCard
                    nodeKey={node.key}
                    title={node.title || t(meta.titleKey)}
                    color={meta.color}
                    iconText={meta.iconText}
                    selected={selectedNodeKey === node.key}
                    subtitle={node.type}
                    onClick={() => setSelectedNodeKey(node.key)}
                    onPointerDown={(event) => startNodeDrag(node, event)}
                    onPortPointerDown={(event, side) => startConnection(node, side, event)}
                  />
                </div>
              );
            })}
          </div>
        </div>

        <NodePanelPopover
          visible={showNodePanel}
          nodes={WORKFLOW_NODE_CATALOG}
          onSelect={(nodeType) => {
            const definition = nodeRegistry.resolve(nodeType);
            const normalizedType = definition.type;
            const template = metadataBundle.templatesMap.get(normalizedType);
            const key = `${nodeType.toLowerCase()}_${canvasNodes.length + 1}`;
            const nextConfigs = mergeNodeDefaults(definition, template, {});
            const catalog = nodeMap.get(normalizedType);
            const center = centerPointForCreate();
            setCanvasNodes((prev: CanvasNode[]) => [
              ...prev,
              {
                key,
                type: normalizedType,
                title: catalog ? t(catalog.titleKey) : normalizedType,
                x: Math.round(center.x),
                y: Math.round(center.y),
                configs: nextConfigs,
                inputMappings: {}
              }
            ]);
            setSelectedNodeKey(key);
            setShowNodePanel(false);
            setIsDirty(true);
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
          }}
          onClose={() => setSelectedNodeKey("")}
        />

        <TestRunPanel
          visible={showTestPanel}
          logs={logs}
          onClose={() => setShowTestPanel(false)}
          onRun={() => setLogs((prev: string[]) => [...prev, `${new Date().toLocaleTimeString()} execution_start`])}
        />

        <CanvasToolbar
          zoom={zoom}
          onZoomChange={(value: number) => setZoom(value)}
          onToggleNodePanel={() => setShowNodePanel((value: boolean) => !value)}
          onRun={() => setShowTestPanel((value: boolean) => !value)}
        />
      </div>
    </div>
  );
}
