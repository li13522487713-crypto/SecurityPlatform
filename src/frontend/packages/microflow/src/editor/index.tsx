import { useEffect, useMemo, useRef, useState, type CSSProperties, type PointerEvent, type ReactNode } from "react";
import { Badge, Button, Card, Checkbox, Drawer, Empty, Input, Modal, Select, Space, Switch, Tabs, Tag, Toast, Typography } from "@douyinfe/semi-ui";
import {
  IconChevronDown,
  IconChevronUp,
  IconCopy,
  IconDelete,
  IconMinus,
  IconPlus,
  IconPlay,
  IconRefresh,
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
  immersive?: boolean;
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
  minHeight: 0,
  background: "var(--semi-color-bg-0, #f7f8fa)",
  color: "var(--semi-color-text-0, #1d2129)"
};

const toolbarStyle: CSSProperties = {
  display: "flex",
  alignItems: "center",
  justifyContent: "space-between",
  padding: "8px 12px",
  borderBottom: "1px solid var(--semi-color-border, #e5e6eb)",
  background: "var(--semi-color-bg-2, #fff)",
  minWidth: 0,
  overflow: "hidden"
};

const bodyStyle: CSSProperties = {
  display: "grid",
  gridTemplateColumns: "300px minmax(520px, 1fr) 400px",
  minHeight: 0,
  overflow: "hidden"
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

function createEditorShellStyle(immersive: boolean): CSSProperties {
  return {
    ...shellStyle,
    gridTemplateRows: immersive ? "56px minmax(0, 1fr)" : shellStyle.gridTemplateRows
  };
}

function createEditorBodyStyle(immersive: boolean, leftCollapsed: boolean, rightCollapsed: boolean): CSSProperties {
  return {
    ...bodyStyle,
    gridTemplateColumns: `${leftCollapsed ? "44px" : immersive ? "300px" : "300px"} minmax(420px, 1fr) ${rightCollapsed ? "44px" : immersive ? "420px" : "400px"}`
  };
}

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

function edgeLabelPosition(edge: MicroflowEdge, nodesById: Map<string, MicroflowNode>): MicroflowPosition {
  const source = nodesById.get(edge.sourceNodeId);
  const target = nodesById.get(edge.targetNodeId);
  if (!source || !target) {
    return { x: 0, y: 0 };
  }
  const sourceSize = nodeSize(source);
  const targetSize = nodeSize(target);
  return {
    x: (source.position.x + sourceSize.width + target.position.x) / 2,
    y: (source.position.y + sourceSize.height / 2 + target.position.y + targetSize.height / 2) / 2 - 8
  };
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
  selectedEdgeId,
  issues,
  traceFrames,
  viewport,
  gridVisible,
  locked,
  onSelectNode,
  onSelectEdge,
  onSchemaChange,
  onDropNode,
  onViewportChange
}: {
  schema: MicroflowSchema;
  selectedNodeId?: string;
  selectedEdgeId?: string;
  issues: MicroflowValidationIssue[];
  traceFrames: MicroflowTraceFrame[];
  viewport: { zoom: number; offset: MicroflowPosition };
  gridVisible: boolean;
  locked: boolean;
  onSelectNode: (nodeId: string) => void;
  onSelectEdge: (edgeId: string) => void;
  onSchemaChange: (schema: MicroflowSchema) => void;
  onDropNode: (payload: MicroflowNodeDragPayload, position: MicroflowPosition) => void;
  onViewportChange: (viewport: { zoom: number; offset: MicroflowPosition }) => void;
}) {
  const nodesById = useMemo(() => new Map(schema.nodes.map(node => [node.id, node])), [schema.nodes]);
  const issueMap = useMemo(() => createIssueMap(issues), [issues]);
  const errorNodeIds = useMemo(() => new Set(traceFrames.filter(frame => frame.error).map(frame => frame.nodeId)), [traceFrames]);
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
        if (!drag || locked) {
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
      onClick={() => onSelectEdge("")}
    >
      {!gridVisible ? <div style={{ position: "absolute", inset: 0, background: "var(--semi-color-fill-0)" }} /> : null}
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
                  stroke={selectedEdgeId === edge.id ? "#165dff" : style.stroke}
                  strokeWidth={selectedEdgeId === edge.id ? 3 : edge.type === "error" ? 2.4 : 1.8}
                  strokeDasharray={style.dash}
                  markerEnd="url(#microflow-arrow)"
                  style={{ pointerEvents: "stroke", cursor: "pointer" }}
                  onClick={event => {
                    event.stopPropagation();
                    onSelectEdge(edge.id);
                  }}
                />
                {edge.label ? (
                  <text {...edgeLabelPosition(edge, nodesById)} fill={style.stroke} fontSize={12}>
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
            issues={errorNodeIds.has(node.id) ? [
              ...(issueMap.get(node.id) ?? []),
              { id: `trace-error:${node.id}`, code: "TRACE_ERROR", severity: "error", message: "Last test run failed on this node.", nodeId: node.id }
            ] : issueMap.get(node.id) ?? []}
            onSelect={onSelectNode}
            onDragStart={(nodeId, pointer) => {
              if (locked) {
                return;
              }
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

function ProblemPanel({
  issues,
  nodesById,
  onLocateNode
}: {
  issues: MicroflowValidationIssue[];
  nodesById: Map<string, MicroflowNode>;
  onLocateNode: (nodeId: string) => void;
}) {
  const [filter, setFilter] = useState<"all" | "error" | "warning" | "info">("all");
  const filtered = filter === "all" ? issues : issues.filter(issue => issue.severity === filter);

  if (issues.length === 0) {
    return <Empty image={<IconTickCircle />} title="No validation issues" description="The current schema passes basic validation." />;
  }

  return (
    <div style={{ height: "100%", display: "grid", gridTemplateRows: "auto minmax(0, 1fr)", minHeight: 0 }}>
      <Tabs type="button" size="small" activeKey={filter} onChange={key => setFilter(key as typeof filter)}>
        <Tabs.TabPane itemKey="all" tab={`All ${issues.length}`} />
        <Tabs.TabPane itemKey="error" tab={`Errors ${issues.filter(issue => issue.severity === "error").length}`} />
        <Tabs.TabPane itemKey="warning" tab={`Warnings ${issues.filter(issue => issue.severity === "warning").length}`} />
        <Tabs.TabPane itemKey="info" tab={`Info ${issues.filter(issue => issue.severity === "info").length}`} />
      </Tabs>
      <div style={{ minHeight: 0, overflow: "auto" }}>
        <div style={{ display: "grid", gridTemplateColumns: "82px 160px minmax(220px, 1fr) minmax(160px, .8fr) 84px", gap: 10, padding: "6px 4px", borderBottom: "1px solid var(--semi-color-border)" }}>
          {["Type", "Node", "Problem", "Details", "Action"].map(label => <Text key={label} strong size="small">{label}</Text>)}
        </div>
        {filtered.map(issue => (
          <div
            key={issue.id}
            style={{ display: "grid", gridTemplateColumns: "82px 160px minmax(220px, 1fr) minmax(160px, .8fr) 84px", gap: 10, padding: "8px 4px", borderBottom: "1px solid var(--semi-color-border)", alignItems: "center" }}
          >
            <Tag color={issue.severity === "error" ? "red" : issue.severity === "warning" ? "orange" : "blue"}>{issue.severity}</Tag>
            <Text ellipsis={{ showTooltip: true }}>{issue.nodeId ? nodesById.get(issue.nodeId)?.title ?? issue.nodeId : issue.edgeId ?? "Schema"}</Text>
            <Text ellipsis={{ showTooltip: true }}>{issue.message}</Text>
            <Text type="tertiary" ellipsis={{ showTooltip: true }}>{issue.code}</Text>
            <Button size="small" disabled={!issue.nodeId} onClick={() => issue.nodeId && onLocateNode(issue.nodeId)}>Locate</Button>
          </div>
        ))}
      </div>
    </div>
  );
}

function DebugPanel({
  frames,
  runStatus,
  onLocateNode
}: {
  frames: MicroflowTraceFrame[];
  runStatus: "idle" | "running" | "succeeded" | "failed";
  onLocateNode: (nodeId: string) => void;
}) {
  const [activeKey, setActiveKey] = useState("trace");

  if (frames.length === 0) {
    return <Empty image={<IconPlay />} title={runStatus === "running" ? "Test run is running" : "No trace yet"} description="Run a test to inspect node input, output, duration, variables, and logs." />;
  }

  return (
    <div style={{ height: "100%", display: "grid", gridTemplateRows: "auto minmax(0, 1fr)", minHeight: 0 }}>
      <Tabs type="button" size="small" activeKey={activeKey} onChange={setActiveKey}>
        <Tabs.TabPane itemKey="trace" tab="Trace" />
        <Tabs.TabPane itemKey="io" tab="Input / Output" />
        <Tabs.TabPane itemKey="variables" tab="Variables" />
        <Tabs.TabPane itemKey="logs" tab="Logs" />
      </Tabs>
      <div style={{ minHeight: 0, overflow: "auto" }}>
        {activeKey === "trace" ? (
          <>
            <div style={{ display: "grid", gridTemplateColumns: "180px 90px 80px minmax(180px, 1fr) minmax(180px, 1fr) minmax(120px, .6fr) 84px", gap: 10, padding: "6px 4px", borderBottom: "1px solid var(--semi-color-border)" }}>
              {["Node", "Status", "ms", "Input", "Output", "Error", "Action"].map(label => <Text key={label} strong size="small">{label}</Text>)}
            </div>
            {frames.map(frame => (
              <div
                key={frame.frameId}
                style={{ display: "grid", gridTemplateColumns: "180px 90px 80px minmax(180px, 1fr) minmax(180px, 1fr) minmax(120px, .6fr) 84px", gap: 10, padding: "8px 4px", borderBottom: "1px solid var(--semi-color-border)", alignItems: "center" }}
              >
                <Text strong ellipsis={{ showTooltip: true }}>{frame.nodeTitle}</Text>
                <Tag color={frame.error ? "red" : "green"}>{frame.error ? "failed" : "succeeded"}</Tag>
                <Text>{frame.durationMs}</Text>
                <Text type="tertiary" ellipsis={{ showTooltip: true }}>{JSON.stringify(frame.input)}</Text>
                <Text type="tertiary" ellipsis={{ showTooltip: true }}>{JSON.stringify(frame.output)}</Text>
                <Text type={frame.error ? "danger" : "tertiary"} ellipsis={{ showTooltip: true }}>{frame.error?.message ?? "-"}</Text>
                <Button size="small" onClick={() => onLocateNode(frame.nodeId)}>Locate</Button>
              </div>
            ))}
          </>
        ) : (
          <pre style={{ margin: 0, padding: 10, background: "var(--semi-color-fill-0)", borderRadius: 8, whiteSpace: "pre-wrap" }}>
            {JSON.stringify(frames.map(frame => ({ nodeId: frame.nodeId, input: frame.input, output: frame.output, error: frame.error })), null, 2)}
          </pre>
        )}
      </div>
    </div>
  );
}

export function MicroflowEditor({
  schema: initialSchema,
  apiClient,
  labels,
  toolbarPrefix,
  toolbarSuffix,
  nodePanelLabels,
  immersive = false,
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
  const [selectedEdgeId, setSelectedEdgeId] = useState<string | undefined>();
  const [issues, setIssues] = useState<MicroflowValidationIssue[]>(() => validateMicroflowSchema(initialSchema));
  const [traceFrames, setTraceFrames] = useState<MicroflowTraceFrame[]>([]);
  const [viewport, setViewport] = useState(initialSchema.viewport ?? { zoom: 0.8, offset: { x: 24, y: 80 } });
  const [testRunOpen, setTestRunOpen] = useState(false);
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [documentationNode, setDocumentationNode] = useState<MicroflowNodeRegistryEntry>();
  const [favoriteNodeKeys, setFavoriteNodeKeys] = useState<string[]>(readFavoriteNodeKeys);
  const [dirty, setDirty] = useState(false);
  const [saving, setSaving] = useState(false);
  const [validating, setValidating] = useState(false);
  const [testing, setTesting] = useState(false);
  const [publishing, setPublishing] = useState(false);
  const [runStatus, setRunStatus] = useState<"idle" | "running" | "succeeded" | "failed">("idle");
  const [activeBottomPanel, setActiveBottomPanel] = useState<"problems" | "debug">("problems");
  const [leftPanelCollapsed, setLeftPanelCollapsed] = useState(false);
  const [rightPanelCollapsed, setRightPanelCollapsed] = useState(false);
  const [bottomPanelCollapsed, setBottomPanelCollapsed] = useState(immersive);
  const [gridVisible, setGridVisible] = useState(true);
  const [canvasLocked, setCanvasLocked] = useState(false);
  const [publishOpen, setPublishOpen] = useState(false);
  const [publishVersion, setPublishVersion] = useState(initialSchema.version || "v1");
  const [publishNote, setPublishNote] = useState("");
  const [overwriteCurrent, setOverwriteCurrent] = useState(true);
  const [testInput, setTestInput] = useState<Record<string, string>>(() =>
    ({ ...Object.fromEntries(initialSchema.parameters.map(parameter => [parameter.name, `<${parameter.type.name}>`])), simulateError: "false" })
  );

  const selectedNode = schema.nodes.find(node => node.id === selectedNodeId);
  const nodesById = useMemo(() => new Map(schema.nodes.map(node => [node.id, node])), [schema.nodes]);
  const errorCount = issues.filter(issue => issue.severity === "error").length;

  useEffect(() => {
    saveFavoriteNodeKeys(favoriteNodeKeys);
  }, [favoriteNodeKeys]);

  useEffect(() => {
    setSchema(initialSchema);
    setIssues(validateMicroflowSchema(initialSchema));
    setViewport(initialSchema.viewport ?? { zoom: 0.8, offset: { x: 24, y: 80 } });
    setSelectedNodeId(initialSchema.nodes[0]?.id);
    setPublishVersion(initialSchema.version || "v1");
    setTestInput({ ...Object.fromEntries(initialSchema.parameters.map(parameter => [parameter.name, `<${parameter.type.name}>`])), simulateError: "false" });
    setDirty(false);
  }, [initialSchema]);

  function commitSchema(nextSchema: MicroflowSchema) {
    setSchema(nextSchema);
    setIssues(validateMicroflowSchema(nextSchema));
    setDirty(true);
    onSchemaChange?.(nextSchema);
  }

  async function handleValidate() {
    setValidating(true);
    try {
      const result = await client.validateMicroflow({ schema });
      setIssues(result.issues);
      setActiveBottomPanel("problems");
      setBottomPanelCollapsed(false);
      onValidateComplete?.(result);
      Toast.info(result.valid ? "Validation passed." : `Validation found ${result.issues.length} issue(s).`);
    } finally {
      setValidating(false);
    }
  }

  async function handleSave() {
    setSaving(true);
    try {
      const result = await client.saveMicroflow({ schema });
      setDirty(false);
      onSaveComplete?.(result);
      Toast.success(`Saved ${result.nodeCount} nodes and ${result.edgeCount} flows.`);
    } finally {
      setSaving(false);
    }
  }

  async function handleTestRun() {
    setTesting(true);
    setRunStatus("running");
    setActiveBottomPanel("debug");
    setBottomPanelCollapsed(false);
    try {
      const result = await client.testRunMicroflow({
        microflowId: schema.id,
        input: testInput,
        schema
      });
      setTraceFrames(result.frames);
      setRunStatus(result.status);
      onTestRunComplete?.(result);
      setTestRunOpen(false);
      Toast.success(`Test run ${result.runId} ${result.status}.`);
    } finally {
      setTesting(false);
    }
  }

  async function handlePublish() {
    if (onPublish) {
      await onPublish(schema);
      return;
    }
    setPublishOpen(true);
  }

  async function handleConfirmPublish() {
    setPublishing(true);
    try {
      await client.saveMicroflow({ schema });
      const result = await client.publishMicroflow(schema.id, {
        version: publishVersion.trim() || schema.version,
        releaseNote: publishNote,
        overwriteCurrent
      });
      const nextSchema = { ...schema, version: result.publishedVersion };
      setSchema(nextSchema);
      setDirty(false);
      onSchemaChange?.(nextSchema);
      setPublishOpen(false);
      Toast.success(`Published ${result.publishedVersion}.`);
    } finally {
      setPublishing(false);
    }
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
    setRightPanelCollapsed(false);
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
    setRightPanelCollapsed(false);
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

  function handleZoom(delta: number) {
    setViewport(current => ({ ...current, zoom: Math.min(1.6, Math.max(0.35, Math.round((current.zoom + delta) * 100) / 100)) }));
  }

  function handleFitView() {
    setViewport(schema.viewport ?? { zoom: 0.58, offset: { x: 24, y: 90 } });
  }

  function handleAutoArrange() {
    const arrangedNodes = schema.nodes.map((node, index) => ({
      ...node,
      position: {
        x: 80 + (index % 6) * 260,
        y: 120 + Math.floor(index / 6) * 170
      }
    }));
    commitSchema({ ...schema, nodes: arrangedNodes, viewport: { zoom: 0.72, offset: { x: 24, y: 72 } } });
    setViewport({ zoom: 0.72, offset: { x: 24, y: 72 } });
  }

  return (
    <MicroflowRuntimeBoundary>
      <div
        style={{
          ...createEditorShellStyle(immersive),
          gridTemplateRows: bottomPanelCollapsed || immersive ? "60px minmax(0, 1fr) 42px" : "60px minmax(0, 1fr) 260px"
        }}
        data-testid="microflow-editor"
      >
        <div style={toolbarStyle}>
          <Space style={{ minWidth: 0, flex: 1 }}>
            {toolbarPrefix}
            <Title heading={5} ellipsis={{ showTooltip: true }} style={{ margin: 0, maxWidth: 280 }}>{schema.name}</Title>
            <Tag color="blue">Microflow</Tag>
            <Tag>{schema.version}</Tag>
            {dirty ? <Tag color="orange">Draft changes</Tag> : <Tag color="green">Saved</Tag>}
            {errorCount > 0 ? <Badge count={errorCount} type="danger" /> : null}
          </Space>
          <Space wrap style={{ justifyContent: "flex-end" }}>
            <Button icon={<IconUndo />} disabled title="History adapter is reserved">
              {copy.undo}
            </Button>
            <Button icon={<IconRedo />} disabled title="History adapter is reserved">
              {copy.redo}
            </Button>
            <Button icon={<IconSearch />} onClick={handleFitView}>
              {copy.fitView}
            </Button>
            <Button icon={<IconRefresh />} onClick={handleAutoArrange}>
              {copy.format}
            </Button>
            <Button icon={<IconTickCircle />} loading={validating} onClick={() => void handleValidate()}>
              {copy.validate}
            </Button>
            <Button icon={<IconSave />} theme="solid" loading={saving} onClick={() => void handleSave()}>
              {copy.save}
            </Button>
            <Button icon={<IconPlay />} type="primary" theme="solid" onClick={() => setTestRunOpen(true)}>
              {copy.testRun}
            </Button>
            <Button icon={<IconCopy />} disabled={!selectedNodeId} onClick={() => selectedNodeId && handleDuplicateNode(selectedNodeId)} />
            <Button icon={<IconDelete />} type="danger" disabled={!selectedNodeId} onClick={handleDeleteSelected} />
            {onPublish ? (
              <Button type="warning" theme="solid" onClick={() => void handlePublish()}>
                {copy.publish}
              </Button>
            ) : (
              <Button type="warning" theme="solid" loading={publishing} onClick={() => setPublishOpen(true)}>
                {copy.publish}
              </Button>
            )}
            <Button icon={<IconSetting />} onClick={() => setSettingsOpen(true)}>
              {copy.settings}
            </Button>
            {toolbarSuffix}
          </Space>
        </div>
        <div style={createEditorBodyStyle(immersive, leftPanelCollapsed, rightPanelCollapsed)}>
          <aside style={panelStyle}>
            {leftPanelCollapsed ? (
              <Button theme="borderless" icon={<IconChevronDown />} onClick={() => setLeftPanelCollapsed(false)} />
            ) : (
              <div style={{ height: "100%", display: "grid", gridTemplateRows: "auto minmax(0, 1fr)", minHeight: 0 }}>
                <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 8 }}>
                  <Text strong>{copy.nodePanel}</Text>
                  <Button size="small" theme="borderless" icon={<IconChevronUp />} onClick={() => setLeftPanelCollapsed(true)} />
                </div>
                <MicroflowNodePanel
                  favoriteNodeKeys={favoriteNodeKeys}
                  onFavoriteChange={setFavoriteNodeKeys}
                  onAddNode={(entry, options) => handleAddNode(entry, options)}
                  onStartDrag={() => undefined}
                  onShowDocumentation={setDocumentationNode}
                  labels={nodePanelLabels}
                />
              </div>
            )}
          </aside>
          <section style={{ display: "grid", gridTemplateRows: "40px 44px minmax(0, 1fr)", minHeight: 0, minWidth: 0 }}>
            <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", padding: "0 12px", borderBottom: "1px solid var(--semi-color-border)", background: "var(--semi-color-bg-1)" }}>
              <Tabs type="button" size="small" activeKey="main">
                <Tabs.TabPane itemKey="main" tab="Main Flow" />
                <Tabs.TabPane itemKey="new" tab="+" disabled />
              </Tabs>
              <Space>
                <Tag color={runStatus === "failed" ? "red" : runStatus === "running" ? "orange" : runStatus === "succeeded" ? "green" : "grey"}>{runStatus}</Tag>
                <Text type="tertiary">{schema.nodes.length} nodes · {schema.edges.length} flows</Text>
              </Space>
            </div>
            <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", padding: "6px 12px", borderBottom: "1px solid var(--semi-color-border)", background: "var(--semi-color-bg-2)" }}>
              <Space>
                <Button size="small" theme="solid" type="primary">Select</Button>
                <Button size="small" disabled>Pan</Button>
                <Button size="small" icon={<IconMinus />} onClick={() => handleZoom(-0.1)} />
                <Tag>{Math.round(viewport.zoom * 100)}%</Tag>
                <Button size="small" icon={<IconPlus />} onClick={() => handleZoom(0.1)} />
                <Button size="small" onClick={handleFitView}>{copy.fitView}</Button>
                <Button size="small" onClick={() => setViewport(current => ({ ...current, offset: { x: 24, y: 90 } }))}>Center</Button>
              </Space>
              <Space>
                <Switch size="small" checked={gridVisible} onChange={setGridVisible} />
                <Text size="small" type="tertiary">Grid</Text>
                <Switch size="small" checked={canvasLocked} onChange={setCanvasLocked} />
                <Text size="small" type="tertiary">Lock</Text>
              </Space>
            </div>
            <div style={{ position: "relative", minHeight: 0, minWidth: 0 }}>
              <MicroflowCanvas
                schema={schema}
                selectedNodeId={selectedNodeId}
                selectedEdgeId={selectedEdgeId}
                issues={issues}
                traceFrames={traceFrames}
                viewport={viewport}
                gridVisible={gridVisible}
                locked={canvasLocked}
                onSelectNode={nodeId => {
                  setSelectedNodeId(nodeId);
                  setSelectedEdgeId(undefined);
                }}
                onSelectEdge={edgeId => {
                  setSelectedEdgeId(edgeId || undefined);
                  setSelectedNodeId(undefined);
                }}
                onSchemaChange={commitSchema}
                onDropNode={handleDropNode}
                onViewportChange={setViewport}
              />
              <Card bodyStyle={{ padding: 6 }} style={{ position: "absolute", right: 16, bottom: 118, background: "rgba(255,255,255,.9)" }}>
                <Space vertical spacing={4}>
                  <Button size="small" icon={<IconPlus />} onClick={() => handleZoom(0.1)} />
                  <Button size="small" icon={<IconMinus />} onClick={() => handleZoom(-0.1)} />
                  <Button size="small" onClick={handleFitView}>Fit</Button>
                  <Button size="small" onClick={() => setViewport(current => ({ ...current, zoom: 1 }))}>100%</Button>
                </Space>
              </Card>
            </div>
          </section>
          <aside style={rightPanelStyle}>
            {rightPanelCollapsed ? (
              <Button theme="borderless" icon={<IconChevronDown />} onClick={() => setRightPanelCollapsed(false)} />
            ) : (
              <div style={{ height: "100%", display: "grid", gridTemplateRows: "auto minmax(0, 1fr)", minHeight: 0 }}>
                <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", paddingBottom: 8 }}>
                  <Text strong>{copy.properties}</Text>
                  <Button size="small" theme="borderless" icon={<IconChevronUp />} onClick={() => setRightPanelCollapsed(true)} />
                </div>
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
              </div>
            )}
          </aside>
        </div>
        <div style={{ borderTop: "1px solid var(--semi-color-border)", background: "var(--semi-color-bg-1)", minHeight: 0, overflow: "hidden" }}>
          <div style={{ height: 42, display: "flex", alignItems: "center", justifyContent: "space-between", padding: "0 12px", borderBottom: bottomPanelCollapsed ? "none" : "1px solid var(--semi-color-border)" }}>
            <Tabs type="button" size="small" activeKey={activeBottomPanel} onChange={key => setActiveBottomPanel(key as "problems" | "debug")}>
              <Tabs.TabPane itemKey="problems" tab={`${copy.problems} (${issues.length})`} />
              <Tabs.TabPane itemKey="debug" tab={`${copy.debug} (${traceFrames.length})`} />
            </Tabs>
            <Button size="small" theme="borderless" icon={bottomPanelCollapsed ? <IconChevronUp /> : <IconChevronDown />} onClick={() => setBottomPanelCollapsed(value => !value)} />
          </div>
          {bottomPanelCollapsed ? null : (
            <div style={{ height: "calc(100% - 42px)", padding: 10, minHeight: 0, overflow: "hidden" }}>
              {activeBottomPanel === "problems" ? (
                <ProblemPanel issues={issues} nodesById={nodesById} onLocateNode={handleLocateNode} />
              ) : (
                <DebugPanel frames={traceFrames} runStatus={runStatus} onLocateNode={handleLocateNode} />
              )}
            </div>
          )}
        </div>
      </div>
      <Modal
        visible={testRunOpen}
        title={copy.testRun}
        onCancel={() => setTestRunOpen(false)}
        onOk={() => void handleTestRun()}
        confirmLoading={testing}
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
          <Input
            value={testInput.simulateError ?? "false"}
            prefix="simulateError"
            onChange={value => setTestInput(current => ({ ...current, simulateError: value }))}
          />
        </Space>
      </Modal>
      <Modal
        visible={publishOpen}
        title={copy.publish}
        onCancel={() => setPublishOpen(false)}
        onOk={() => void handleConfirmPublish()}
        confirmLoading={publishing}
        okText={copy.publish}
      >
        <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
          <Input value={publishVersion} onChange={setPublishVersion} prefix="Version" />
          <Input value={publishNote} onChange={setPublishNote} prefix="Release note" />
          <Checkbox checked={overwriteCurrent} onChange={event => setOverwriteCurrent(Boolean(event.target.checked))}>
            Overwrite current published version
          </Checkbox>
        </Space>
      </Modal>
      <Drawer
        visible={settingsOpen}
        title={copy.settings}
        width={420}
        onCancel={() => setSettingsOpen(false)}
        footer={
          <Space>
            <Button onClick={() => setSettingsOpen(false)}>Close</Button>
            <Button theme="solid" onClick={() => setSettingsOpen(false)}>Apply</Button>
          </Space>
        }
      >
        <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
          <Input value={schema.name} prefix="Name" onChange={name => commitSchema({ ...schema, name })} />
          <Input value={schema.description ?? ""} prefix="Description" onChange={description => commitSchema({ ...schema, description })} />
          <Select
            style={{ width: "100%" }}
            value={(schema.nodes.find(node => node.type === "endEvent")?.config as { returnType?: { name: string } } | undefined)?.returnType?.name ?? "void"}
            prefix="Return type"
            optionList={["void", "Boolean", "String", "Integer", "Object", "List"].map(item => ({ value: item, label: item }))}
            onChange={value => {
              const name = String(value);
              commitSchema({
                ...schema,
                nodes: schema.nodes.map(node => node.type === "endEvent"
                  ? { ...node, config: { ...node.config, returnType: { kind: name === "void" ? "void" : "primitive", name } } }
                  : node)
              });
            }}
          />
          <Input value={(schema.variables.map(variable => variable.name).join(", "))} readonly prefix="Variables" />
          <Text type="tertiary">Settings are persisted through the same MicroflowSchema used by save, validate, test run, and publish.</Text>
        </Space>
      </Drawer>
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
