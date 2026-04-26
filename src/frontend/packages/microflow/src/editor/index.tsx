import { useEffect, useMemo, useRef, useState, type CSSProperties, type PointerEvent, type ReactNode } from "react";
import { Badge, Button, Card, Empty, Input, Modal, Space, Tag, Toast, Typography } from "@douyinfe/semi-ui";
import {
  IconPlay,
  IconSave,
  IconSearch,
  IconSetting,
  IconTickCircle,
  IconUndo,
  IconRedo
} from "@douyinfe/semi-icons";
import { MicroflowNodePanel, type MicroflowNodePanelLabels } from "../node-panel";
import { MicroflowPropertyPanel, type MicroflowNodePatch } from "../property-panel";
import {
  createMicroflowNodeFromRegistry,
  getMicroflowNodeByType,
  getMicroflowNodeRegistryKey,
  type MicroflowNodeDragPayload,
  type MicroflowNodeRegistryEntry
} from "../node-registry";
import { createLocalMicroflowApiClient, type MicroflowApiClient, type MicroflowTraceFrame, type SaveMicroflowResponse, type TestRunMicroflowResponse, type ValidateMicroflowResponse } from "../runtime-adapter";
import { validateMicroflowSchema } from "../schema/validator";
import type { MicroflowEdge, MicroflowNode, MicroflowNodeOutput, MicroflowSchema, MicroflowValidationIssue, MicroflowPosition, MicroflowVariable } from "../schema/types";

const { Text, Title } = Typography;

const favoriteStorageKey = "atlas_microflow_node_panel_favorites";
const defaultFavoriteNodeKeys = ["activity:objectRetrieve", "activity:callRest", "activity:logMessage"];

export interface MicroflowEditorProps {
  schema: MicroflowSchema;
  apiClient?: MicroflowApiClient;
  labels?: Partial<MicroflowEditorLabels>;
  toolbarPrefix?: ReactNode;
  toolbarSuffix?: ReactNode;
  nodePanelLabels?: Partial<MicroflowNodePanelLabels>;
  onPublish?: (schema: MicroflowSchema) => Promise<void> | void;
  onSaveComplete?: (response: SaveMicroflowResponse) => void;
  onValidateComplete?: (response: ValidateMicroflowResponse) => void;
  onTestRunComplete?: (response: TestRunMicroflowResponse) => void;
  onSchemaChange?: (schema: MicroflowSchema) => void;
}

export interface MicroflowEditorLabels {
  save: string;
  validate: string;
  testRun: string;
  fitView: string;
  publish: string;
  undo: string;
  redo: string;
  format: string;
  more: string;
  settings: string;
  nodePanel: string;
  properties: string;
  problems: string;
  debug: string;
}

const defaultLabels: MicroflowEditorLabels = {
  save: "Save",
  validate: "Validate",
  testRun: "Test Run",
  fitView: "Fit View",
  publish: "Publish",
  undo: "Undo",
  redo: "Redo",
  format: "Format",
  more: "More",
  settings: "Settings",
  nodePanel: "Nodes",
  properties: "Properties",
  problems: "Problems",
  debug: "Debug"
};

function readFavoriteNodeKeys(): string[] {
  if (typeof window === "undefined") {
    return defaultFavoriteNodeKeys;
  }
  try {
    const saved = window.localStorage.getItem(favoriteStorageKey);
    if (saved === null) {
      return defaultFavoriteNodeKeys;
    }
    const parsed = JSON.parse(saved) as unknown;
    return Array.isArray(parsed) && parsed.every(item => typeof item === "string") ? parsed : defaultFavoriteNodeKeys;
  } catch {
    return defaultFavoriteNodeKeys;
  }
}

function saveFavoriteNodeKeys(keys: string[]): void {
  if (typeof window === "undefined") {
    return;
  }
  try {
    window.localStorage.setItem(favoriteStorageKey, JSON.stringify(keys));
  } catch {
    // Favorite persistence should not block editor interactions.
  }
}

function parseNodeDragPayload(data: string): MicroflowNodeDragPayload | undefined {
  if (!data) {
    return undefined;
  }
  try {
    const value = JSON.parse(data) as Record<string, unknown>;
    if (
      value.sourcePanel !== "microflow-node-panel" ||
      typeof value.nodeType !== "string" ||
      typeof value.registryKey !== "string" ||
      typeof value.title !== "string"
    ) {
      return undefined;
    }
    return {
      nodeType: value.nodeType as MicroflowNodeDragPayload["nodeType"],
      activityType: typeof value.activityType === "string" ? value.activityType as MicroflowNodeDragPayload["activityType"] : undefined,
      registryKey: value.registryKey,
      title: value.title,
      defaultConfig: typeof value.defaultConfig === "object" && value.defaultConfig !== null ? value.defaultConfig as Record<string, unknown> : {},
      sourcePanel: "microflow-node-panel"
    };
  } catch {
    return undefined;
  }
}

function cloneNode<TNode extends MicroflowNode>(node: TNode): TNode {
  return JSON.parse(JSON.stringify(node)) as TNode;
}

function variablesFromOutputs(outputs: MicroflowNodeOutput[] | undefined): MicroflowVariable[] {
  return (outputs ?? []).filter(output => output.name.trim().length > 0).map(output => ({
    id: output.id,
    name: output.name,
    type: output.type,
    scope: "node"
  }));
}

const shellStyle: CSSProperties = {
  display: "grid",
  gridTemplateRows: "60px minmax(0, 1fr) 220px",
  height: "100%",
  minHeight: 720,
  background: "var(--semi-color-bg-0, #f7f8fa)",
  color: "var(--semi-color-text-0, #1d2129)"
};

const toolbarStyle: CSSProperties = {
  display: "flex",
  alignItems: "center",
  justifyContent: "space-between",
  padding: "8px 12px",
  borderBottom: "1px solid var(--semi-color-border, #e5e6eb)",
  background: "var(--semi-color-bg-2, #fff)"
};

const bodyStyle: CSSProperties = {
  display: "grid",
  gridTemplateColumns: "280px minmax(760px, 1fr) 360px 260px",
  minHeight: 0,
  overflow: "auto"
};

const panelStyle: CSSProperties = {
  minHeight: 0,
  overflow: "auto",
  borderRight: "1px solid var(--semi-color-border, #e5e6eb)",
  background: "var(--semi-color-bg-1, #fff)",
  padding: 12
};

const rightPanelStyle: CSSProperties = {
  ...panelStyle,
  borderRight: "none",
  borderLeft: "1px solid var(--semi-color-border, #e5e6eb)"
};

const canvasViewportStyle: CSSProperties = {
  position: "relative",
  minWidth: 0,
  minHeight: 0,
  overflow: "hidden",
  background:
    "radial-gradient(circle, rgba(22, 93, 255, 0.08) 1px, transparent 1px), var(--semi-color-fill-0, #f4f7fb)",
  backgroundSize: "22px 22px"
};

const canvasLayerStyle: CSSProperties = {
  position: "absolute",
  left: 0,
  top: 0,
  width: 3000,
  height: 1100,
  transformOrigin: "0 0"
};

function nodeSize(node: MicroflowNode): { width: number; height: number } {
  return {
    width: node.render.width ?? 160,
    height: node.render.height ?? 72
  };
}

function toneColor(node: MicroflowNode): string {
  if (node.render.tone === "success") {
    return "#12b886";
  }
  if (node.render.tone === "danger") {
    return "#f93920";
  }
  if (node.render.tone === "warning") {
    return "#ff8800";
  }
  if (node.render.tone === "info") {
    return "#165dff";
  }
  return "#4e5969";
}

function edgeStyle(edge: MicroflowEdge): { stroke: string; dash?: string } {
  if (edge.type === "error") {
    return { stroke: "#f93920" };
  }
  if (edge.type === "annotation") {
    return { stroke: "#86909c", dash: "6 6" };
  }
  return { stroke: "#4e5969" };
}

function edgePath(edge: MicroflowEdge, nodesById: Map<string, MicroflowNode>): string {
  const source = nodesById.get(edge.sourceNodeId);
  const target = nodesById.get(edge.targetNodeId);
  if (!source || !target) {
    return "";
  }
  const sourceSize = nodeSize(source);
  const targetSize = nodeSize(target);
  const start = {
    x: source.position.x + sourceSize.width,
    y: source.position.y + sourceSize.height / 2
  };
  const end = {
    x: target.position.x,
    y: target.position.y + targetSize.height / 2
  };
  const distance = Math.max(80, Math.abs(end.x - start.x) / 2);
  return `M ${start.x} ${start.y} C ${start.x + distance} ${start.y}, ${end.x - distance} ${end.y}, ${end.x} ${end.y}`;
}

function createIssueMap(issues: MicroflowValidationIssue[]): Map<string, MicroflowValidationIssue[]> {
  const map = new Map<string, MicroflowValidationIssue[]>();
  for (const issue of issues) {
    if (!issue.nodeId) {
      continue;
    }
    map.set(issue.nodeId, [...(map.get(issue.nodeId) ?? []), issue]);
  }
  return map;
}

function MicroflowRuntimeBoundary({ children }: { children: React.ReactNode }) {
  return (
    <div style={{ height: "100%", width: "100%", overflow: "hidden" }} data-testid="microflow-runtime-boundary">
      {children}
    </div>
  );
}

function MicroflowNodeCard({
  node,
  selected,
  issues,
  onSelect,
  onDragStart
}: {
  node: MicroflowNode;
  selected: boolean;
  issues: MicroflowValidationIssue[];
  onSelect: (nodeId: string) => void;
  onDragStart: (nodeId: string, pointer: MicroflowPosition) => void;
}) {
  const size = nodeSize(node);
  const color = toneColor(node);
  const commonStyle: CSSProperties = {
    position: "absolute",
    left: node.position.x,
    top: node.position.y,
    width: size.width,
    height: size.height,
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    padding: "10px 12px",
    cursor: "grab",
    userSelect: "none",
    border: `2px solid ${selected ? "#165dff" : issues.length > 0 ? "#f93920" : "rgba(78, 89, 105, 0.22)"}`,
    background: node.type === "annotation" ? "rgba(255, 247, 224, 0.96)" : "var(--semi-color-bg-2, #fff)",
    boxShadow: selected ? "0 8px 22px rgba(22, 93, 255, 0.22)" : "0 8px 20px rgba(31, 35, 41, 0.08)"
  };
  const shapeStyle: CSSProperties = node.render.shape === "diamond"
    ? { ...commonStyle, transform: "rotate(45deg)", borderRadius: 10 }
    : node.render.shape === "event"
      ? { ...commonStyle, borderRadius: 999 }
      : node.render.shape === "annotation"
        ? { ...commonStyle, borderRadius: 12, borderStyle: "dashed" }
        : node.render.shape === "loop"
          ? { ...commonStyle, borderRadius: 18 }
          : { ...commonStyle, borderRadius: 14 };

  return (
    <div
      style={shapeStyle}
      role="button"
      tabIndex={0}
      onClick={() => onSelect(node.id)}
      onPointerDown={(event: PointerEvent<HTMLDivElement>) => {
        event.currentTarget.setPointerCapture(event.pointerId);
        onSelect(node.id);
        onDragStart(node.id, { x: event.clientX, y: event.clientY });
      }}
    >
      <div style={node.render.shape === "diamond" ? { transform: "rotate(-45deg)", textAlign: "center" } : { textAlign: "center", width: "100%" }}>
        <Space vertical spacing={2} align="center">
          <Tag color={issues.length > 0 ? "red" : "blue"} size="small">
            {node.type === "activity" ? node.config.activityType : node.type}
          </Tag>
          <Text strong ellipsis={{ showTooltip: true }} style={{ maxWidth: size.width - 24 }}>
            {node.title}
          </Text>
          {node.render.shape === "loop" ? <Text type="tertiary">loop</Text> : null}
          {issues.length > 0 ? <Badge count={issues.length} type="danger" /> : <span style={{ width: 20, height: 3, borderRadius: 2, background: color }} />}
        </Space>
      </div>
    </div>
  );
}

function MicroflowCanvas({
  schema,
  selectedNodeId,
  issues,
  viewport,
  onSelectNode,
  onSchemaChange,
  onDropNode,
  onViewportChange
}: {
  schema: MicroflowSchema;
  selectedNodeId?: string;
  issues: MicroflowValidationIssue[];
  viewport: { zoom: number; offset: MicroflowPosition };
  onSelectNode: (nodeId: string) => void;
  onSchemaChange: (schema: MicroflowSchema) => void;
  onDropNode: (payload: MicroflowNodeDragPayload, position: MicroflowPosition) => void;
  onViewportChange: (viewport: { zoom: number; offset: MicroflowPosition }) => void;
}) {
  const nodesById = useMemo(() => new Map(schema.nodes.map(node => [node.id, node])), [schema.nodes]);
  const issueMap = useMemo(() => createIssueMap(issues), [issues]);
  const dragRef = useRef<{ nodeId: string; startPointer: MicroflowPosition; startPosition: MicroflowPosition }>();

  function updateNodePosition(nodeId: string, position: MicroflowPosition) {
    onSchemaChange({
      ...schema,
      nodes: schema.nodes.map(node => node.id === nodeId ? { ...node, position } : node)
    });
  }

  return (
    <div
      style={canvasViewportStyle}
      onPointerMove={event => {
        const drag = dragRef.current;
        if (!drag) {
          return;
        }
        updateNodePosition(drag.nodeId, {
          x: drag.startPosition.x + (event.clientX - drag.startPointer.x) / viewport.zoom,
          y: drag.startPosition.y + (event.clientY - drag.startPointer.y) / viewport.zoom
        });
      }}
      onPointerUp={() => {
        dragRef.current = undefined;
      }}
      onDragOver={event => {
        if (event.dataTransfer.types.includes("application/x-atlas-microflow-node")) {
          event.preventDefault();
          event.dataTransfer.dropEffect = "copy";
        }
      }}
      onDrop={event => {
        const payload = parseNodeDragPayload(event.dataTransfer.getData("application/x-atlas-microflow-node"));
        if (!payload) {
          return;
        }
        event.preventDefault();
        const rect = event.currentTarget.getBoundingClientRect();
        onDropNode(payload, {
          x: (event.clientX - rect.left - viewport.offset.x) / viewport.zoom,
          y: (event.clientY - rect.top - viewport.offset.y) / viewport.zoom
        });
      }}
      onWheel={event => {
        const nextZoom = Math.min(1.4, Math.max(0.45, viewport.zoom - event.deltaY * 0.001));
        onViewportChange({ ...viewport, zoom: nextZoom });
      }}
    >
      <div
        style={{
          ...canvasLayerStyle,
          transform: `translate(${viewport.offset.x}px, ${viewport.offset.y}px) scale(${viewport.zoom})`
        }}
      >
        <svg width="3000" height="1100" style={{ position: "absolute", inset: 0, pointerEvents: "none" }}>
          <defs>
            <marker id="microflow-arrow" markerWidth="10" markerHeight="8" refX="9" refY="4" orient="auto">
              <path d="M 0 0 L 10 4 L 0 8 z" fill="#4e5969" />
            </marker>
          </defs>
          {schema.edges.map(edge => {
            const style = edgeStyle(edge);
            return (
              <g key={edge.id}>
                <path
                  d={edgePath(edge, nodesById)}
                  fill="none"
                  stroke={style.stroke}
                  strokeWidth={edge.type === "error" ? 2.4 : 1.8}
                  strokeDasharray={style.dash}
                  markerEnd="url(#microflow-arrow)"
                />
                {edge.label ? (
                  <text x="50%" y="50%" fill={style.stroke} fontSize={12}>
                    {edge.label}
                  </text>
                ) : null}
              </g>
            );
          })}
        </svg>
        {schema.nodes.map(node => (
          <MicroflowNodeCard
            key={node.id}
            node={node}
            selected={node.id === selectedNodeId}
            issues={issueMap.get(node.id) ?? []}
            onSelect={onSelectNode}
            onDragStart={(nodeId, pointer) => {
              const current = nodesById.get(nodeId);
              if (current) {
                dragRef.current = { nodeId, startPointer: pointer, startPosition: current.position };
              }
            }}
          />
        ))}
      </div>
      <Card
        bodyStyle={{ padding: 8 }}
        style={{ position: "absolute", right: 16, bottom: 16, width: 180, background: "rgba(255,255,255,0.88)" }}
      >
        <Text type="tertiary">MiniMap</Text>
        <div style={{ position: "relative", height: 86, marginTop: 6, background: "rgba(22,93,255,0.06)", borderRadius: 8 }}>
          {schema.nodes.map(node => (
            <span
              key={node.id}
              style={{
                position: "absolute",
                left: node.position.x / 12,
                top: node.position.y / 12,
                width: 10,
                height: 6,
                borderRadius: 3,
                background: node.id === selectedNodeId ? "#165dff" : "#86909c"
              }}
            />
          ))}
        </div>
      </Card>
    </div>
  );
}

function ProblemPanel({ issues, onSelectNode }: { issues: MicroflowValidationIssue[]; onSelectNode: (nodeId: string) => void }) {
  if (issues.length === 0) {
    return <Empty image={<IconTickCircle />} title="No validation issues" description="The current schema passes basic validation." />;
  }

  return (
    <Space vertical align="start" spacing={6} style={{ width: "100%" }}>
      {issues.map(issue => (
        <div
          key={issue.id}
          style={{ display: "flex", gap: 8, alignItems: "center", cursor: issue.nodeId ? "pointer" : "default" }}
          onClick={() => {
            if (issue.nodeId) {
              onSelectNode(issue.nodeId);
            }
          }}
        >
          <Tag color={issue.severity === "error" ? "red" : "orange"}>{issue.severity}</Tag>
          <Text>{issue.message}</Text>
          {issue.nodeId ? <Text type="tertiary">node: {issue.nodeId}</Text> : null}
        </div>
      ))}
    </Space>
  );
}

function DebugPanel({ frames, onSelectNode }: { frames: MicroflowTraceFrame[]; onSelectNode: (nodeId: string) => void }) {
  if (frames.length === 0) {
    return <Empty image={<IconPlay />} title="No trace yet" description="Run a test to inspect node input, output, duration, and errors." />;
  }

  return (
    <Space vertical align="start" spacing={6} style={{ width: "100%" }}>
      {frames.map(frame => (
        <div
          key={frame.frameId}
          style={{ display: "grid", gridTemplateColumns: "180px 80px 1fr", gap: 12, width: "100%", cursor: "pointer" }}
          onClick={() => onSelectNode(frame.nodeId)}
        >
          <Text strong>{frame.nodeTitle}</Text>
          <Tag color={frame.error ? "red" : "green"}>{frame.durationMs}ms</Tag>
          <Text type="tertiary" ellipsis={{ showTooltip: true }}>
            input {JSON.stringify(frame.input)} output {JSON.stringify(frame.output)}
          </Text>
        </div>
      ))}
    </Space>
  );
}

export function MicroflowEditor({
  schema: initialSchema,
  apiClient,
  labels,
  toolbarPrefix,
  toolbarSuffix,
  nodePanelLabels,
  onPublish,
  onSaveComplete,
  onValidateComplete,
  onTestRunComplete,
  onSchemaChange
}: MicroflowEditorProps) {
  const copy = { ...defaultLabels, ...labels };
  const client = useMemo(() => apiClient ?? createLocalMicroflowApiClient([initialSchema]), [apiClient, initialSchema]);
  const [schema, setSchema] = useState<MicroflowSchema>(initialSchema);
  const [selectedNodeId, setSelectedNodeId] = useState<string | undefined>(initialSchema.nodes[0]?.id);
  const [issues, setIssues] = useState<MicroflowValidationIssue[]>(() => validateMicroflowSchema(initialSchema));
  const [traceFrames, setTraceFrames] = useState<MicroflowTraceFrame[]>([]);
  const [viewport, setViewport] = useState(initialSchema.viewport ?? { zoom: 0.8, offset: { x: 24, y: 80 } });
  const [testRunOpen, setTestRunOpen] = useState(false);
  const [documentationNode, setDocumentationNode] = useState<MicroflowNodeRegistryEntry>();
  const [favoriteNodeKeys, setFavoriteNodeKeys] = useState<string[]>(readFavoriteNodeKeys);
  const [testInput, setTestInput] = useState<Record<string, string>>(() =>
    Object.fromEntries(initialSchema.parameters.map(parameter => [parameter.name, `<${parameter.type.name}>`]))
  );

  const selectedNode = schema.nodes.find(node => node.id === selectedNodeId);

  useEffect(() => {
    saveFavoriteNodeKeys(favoriteNodeKeys);
  }, [favoriteNodeKeys]);

  function commitSchema(nextSchema: MicroflowSchema) {
    setSchema(nextSchema);
    setIssues(validateMicroflowSchema(nextSchema));
    onSchemaChange?.(nextSchema);
  }

  async function handleValidate() {
    const result = await client.validateMicroflow({ schema });
    setIssues(result.issues);
    onValidateComplete?.(result);
    Toast.info(result.valid ? "Validation passed." : `Validation found ${result.issues.length} issue(s).`);
  }

  async function handleSave() {
    const result = await client.saveMicroflow({ schema });
    onSaveComplete?.(result);
    Toast.success(`Saved ${result.nodeCount} nodes and ${result.edgeCount} flows.`);
  }

  async function handleTestRun() {
    const result = await client.testRunMicroflow({
      microflowId: schema.id,
      input: testInput,
      schema
    });
    setTraceFrames(result.frames);
    onTestRunComplete?.(result);
    setTestRunOpen(false);
    Toast.success(`Test run ${result.runId} ${result.status}.`);
  }

  async function handlePublish() {
    if (!onPublish) {
      return;
    }
    await onPublish(schema);
  }

  function handleAddNode(entry: MicroflowNodeRegistryEntry, options?: { position?: MicroflowPosition }) {
    if (!entry.enabled) {
      Toast.warning(entry.disabledReason ?? "This node is disabled.");
      return;
    }
    const registryKey = getMicroflowNodeRegistryKey(entry);
    const position = options?.position ?? {
      x: 320 + schema.nodes.length * 16,
      y: 120 + schema.nodes.length * 12
    };
    const nextNode = createMicroflowNodeFromRegistry(entry, position, `${registryKey.replace(":", "-")}-${Date.now()}`);
    commitSchema({ ...schema, nodes: [...schema.nodes, nextNode] });
    setSelectedNodeId(nextNode.id);
  }

  function handleDropNode(payload: MicroflowNodeDragPayload, position: MicroflowPosition) {
    const entry = getMicroflowNodeByType(payload.nodeType, payload.activityType);
    if (!entry) {
      Toast.error(`Unknown microflow node: ${payload.registryKey}`);
      return;
    }
    handleAddNode(entry, { position });
  }

  function handleNodePatch(nodeId: string, patch: MicroflowNodePatch) {
    let nextOutputs: MicroflowNodeOutput[] | undefined;
    const nextNodes = schema.nodes.map(node => {
      if (node.id !== nodeId) {
        return node;
      }
      const nextNode = {
        ...node,
        ...(patch.node ?? {}),
        config: patch.config ? { ...node.config, ...patch.config } : node.config,
        documentation: patch.documentation ?? node.documentation,
        advanced: patch.advanced ?? node.advanced,
        outputs: patch.outputs ?? node.outputs
      } as MicroflowNode;
      nextOutputs = nextNode.outputs;
      return nextNode;
    });
    const outputVariableIds = new Set((nextOutputs ?? []).map(output => output.id));
    const nextVariables = [
      ...schema.variables.filter(variable => !outputVariableIds.has(variable.id)),
      ...variablesFromOutputs(nextOutputs)
    ];
    commitSchema({ ...schema, nodes: nextNodes, variables: nextVariables });
  }

  function handleDuplicateNode(nodeId: string) {
    const node = schema.nodes.find(item => item.id === nodeId);
    if (!node) {
      return;
    }
    const nextNode = cloneNode(node);
    nextNode.id = `${node.id}-copy-${Date.now()}`;
    nextNode.title = `${node.title} Copy`;
    nextNode.position = { x: node.position.x + 36, y: node.position.y + 36 };
    commitSchema({ ...schema, nodes: [...schema.nodes, nextNode] });
    setSelectedNodeId(nextNode.id);
  }

  function handleLocateNode(nodeId: string) {
    const node = schema.nodes.find(item => item.id === nodeId);
    if (!node) {
      return;
    }
    setSelectedNodeId(nodeId);
    setViewport(current => ({
      ...current,
      offset: {
        x: 360 - node.position.x * current.zoom,
        y: 180 - node.position.y * current.zoom
      }
    }));
  }

  function handleDeleteSelected() {
    if (!selectedNodeId) {
      return;
    }
    handleDeleteNode(selectedNodeId);
  }

  function handleDeleteNode(nodeId: string) {
    commitSchema({
      ...schema,
      nodes: schema.nodes.filter(node => node.id !== nodeId),
      edges: schema.edges.filter(edge => edge.sourceNodeId !== nodeId && edge.targetNodeId !== nodeId)
    });
    setSelectedNodeId(undefined);
  }

  return (
    <MicroflowRuntimeBoundary>
      <div style={shellStyle} data-testid="microflow-editor">
        <div style={toolbarStyle}>
          <Space>
            {toolbarPrefix}
            <Title heading={5} style={{ margin: 0 }}>{schema.name}</Title>
            <Tag color="blue">Microflow</Tag>
          </Space>
          <Space>
            <Button icon={<IconUndo />} disabled>
              {copy.undo}
            </Button>
            <Button icon={<IconRedo />} disabled>
              {copy.redo}
            </Button>
            <Button icon={<IconSearch />} onClick={() => setViewport({ zoom: 0.78, offset: { x: 24, y: 80 } })}>
              {copy.fitView}
            </Button>
            <Button onClick={() => setViewport({ zoom: 0.58, offset: { x: 24, y: 90 } })}>
              {copy.format}
            </Button>
            <Button icon={<IconTickCircle />} onClick={() => void handleValidate()}>
              {copy.validate}
            </Button>
            <Button icon={<IconSave />} theme="solid" onClick={() => void handleSave()}>
              {copy.save}
            </Button>
            <Button icon={<IconPlay />} type="primary" theme="solid" onClick={() => setTestRunOpen(true)}>
              {copy.testRun}
            </Button>
            {onPublish ? (
              <Button type="warning" theme="solid" onClick={() => void handlePublish()}>
                {copy.publish}
              </Button>
            ) : null}
            <Button icon={<IconSetting />} onClick={() => Toast.info("Settings drawer is reserved for runtime and parameter configuration.")}>
              {copy.settings}
            </Button>
            {toolbarSuffix}
          </Space>
        </div>
        <div style={bodyStyle}>
          <aside style={panelStyle}>
            <MicroflowNodePanel
              favoriteNodeKeys={favoriteNodeKeys}
              onFavoriteChange={setFavoriteNodeKeys}
              onAddNode={(entry, options) => handleAddNode(entry, options)}
              onStartDrag={() => undefined}
              onShowDocumentation={setDocumentationNode}
              labels={nodePanelLabels}
            />
          </aside>
          <MicroflowCanvas
            schema={schema}
            selectedNodeId={selectedNodeId}
            issues={issues}
            viewport={viewport}
            onSelectNode={setSelectedNodeId}
            onSchemaChange={commitSchema}
            onDropNode={handleDropNode}
            onViewportChange={setViewport}
          />
          <aside style={rightPanelStyle}>
            <MicroflowPropertyPanel
              selectedNode={selectedNode ?? null}
              schema={schema}
              validationIssues={issues}
              traceFrames={traceFrames.map(frame => ({
                nodeId: frame.nodeId,
                status: frame.error ? "failed" : "succeeded",
                durationMs: frame.durationMs,
                error: frame.error?.message
              }))}
              onNodeChange={handleNodePatch}
              onClose={() => setSelectedNodeId(undefined)}
              onLocateNode={handleLocateNode}
              onDuplicateNode={handleDuplicateNode}
              onDeleteNode={handleDeleteNode}
            />
          </aside>
          <aside style={{ ...rightPanelStyle, borderLeft: "1px solid var(--semi-color-border, #e5e6eb)", background: "var(--semi-color-bg-0, #f7f8fa)" }}>
            <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
              <Title heading={6} style={{ margin: 0 }}>Region Guide</Title>
              {[
                "Toolbar saves drafts, validates, runs tests, publishes, formats, and opens settings.",
                "Node panel groups Mendix-style events, decisions, activities, loops, parameters, and annotations.",
                "Canvas supports selection, dragging, zooming, fit view, minimap, sequence flows, error flows, and annotation flows.",
                "Property panel changes with the selected node and exposes configuration plus error handling.",
                "Problem panel lists validation issues and debug panel shows test-run trace frames with nodeId."
              ].map((item, index) => (
                <div key={item} style={{ display: "flex", gap: 8, alignItems: "flex-start" }}>
                  <Tag color="blue">{index + 1}</Tag>
                  <Text type="tertiary">{item}</Text>
                </div>
              ))}
            </Space>
          </aside>
        </div>
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1.3fr", gap: 12, padding: 12, borderTop: "1px solid var(--semi-color-border, #e5e6eb)", overflow: "auto", background: "var(--semi-color-bg-1, #fff)" }}>
          <Card title={`${copy.problems} (${issues.length})`} bodyStyle={{ maxHeight: 158, overflow: "auto" }}>
            <ProblemPanel issues={issues} onSelectNode={setSelectedNodeId} />
          </Card>
          <Card title={`${copy.debug} (${traceFrames.length})`} bodyStyle={{ maxHeight: 158, overflow: "auto" }}>
            <DebugPanel frames={traceFrames} onSelectNode={setSelectedNodeId} />
          </Card>
        </div>
      </div>
      <Modal
        visible={testRunOpen}
        title={copy.testRun}
        onCancel={() => setTestRunOpen(false)}
        onOk={() => void handleTestRun()}
        okText={copy.testRun}
      >
        <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
          {schema.parameters.length === 0 ? (
            <Text type="tertiary">This microflow has no input parameters.</Text>
          ) : schema.parameters.map(parameter => (
            <Input
              key={parameter.id}
              value={testInput[parameter.name] ?? ""}
              prefix={`${parameter.name}: ${parameter.type.name}`}
              onChange={value => setTestInput(current => ({ ...current, [parameter.name]: value }))}
            />
          ))}
        </Space>
      </Modal>
      <Modal
        visible={Boolean(documentationNode)}
        title={documentationNode?.title ?? "Node documentation"}
        footer={null}
        onCancel={() => setDocumentationNode(undefined)}
      >
        {documentationNode ? (
          <Space vertical align="start" spacing={10} style={{ width: "100%" }}>
            <Text>{documentationNode.documentation ?? documentationNode.description}</Text>
            <Tag color={documentationNode.enabled ? "green" : "grey"}>
              {documentationNode.enabled ? "Enabled" : documentationNode.disabledReason ?? "Disabled"}
            </Tag>
            <Text type="tertiary">
              Type: {getMicroflowNodeRegistryKey(documentationNode)}
            </Text>
            <Text type="tertiary">
              Inputs: {(documentationNode.inputs ?? []).join(", ") || "-"}
            </Text>
            <Text type="tertiary">
              Outputs: {(documentationNode.outputs ?? []).join(", ") || "-"}
            </Text>
          </Space>
        ) : null}
      </Modal>
    </MicroflowRuntimeBoundary>
  );
}
