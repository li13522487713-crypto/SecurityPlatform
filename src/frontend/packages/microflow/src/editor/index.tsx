import { useMemo, useRef, useState, type CSSProperties, type PointerEvent } from "react";
import { Badge, Button, Card, Collapse, Divider, Empty, Space, Tag, Toast, Typography } from "@douyinfe/semi-ui";
import {
  IconDelete,
  IconPlay,
  IconPlus,
  IconSave,
  IconSearch,
  IconTickCircle
} from "@douyinfe/semi-icons";
import { microflowNodeRegistries, createMicroflowNodeFromRegistry, type MicroflowNodeRegistryEntry } from "../node-registry";
import { MicroflowPropertyForm } from "../property-forms";
import { createLocalMicroflowApiClient, type MicroflowApiClient, type MicroflowTraceFrame } from "../runtime-adapter";
import { validateMicroflowSchema } from "../schema/validator";
import type { MicroflowEdge, MicroflowNode, MicroflowSchema, MicroflowValidationIssue, MicroflowPosition } from "../schema/types";

const { Text, Title } = Typography;

export interface MicroflowEditorProps {
  schema: MicroflowSchema;
  apiClient?: MicroflowApiClient;
  labels?: Partial<MicroflowEditorLabels>;
  onSchemaChange?: (schema: MicroflowSchema) => void;
}

export interface MicroflowEditorLabels {
  save: string;
  validate: string;
  testRun: string;
  fitView: string;
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
  nodePanel: "Nodes",
  properties: "Properties",
  problems: "Problems",
  debug: "Debug"
};

const shellStyle: CSSProperties = {
  display: "grid",
  gridTemplateRows: "52px minmax(0, 1fr) 170px",
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
  gridTemplateColumns: "260px minmax(0, 1fr) 320px",
  minHeight: 0
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
  width: 2200,
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

function MicroflowNodePanel({ onAddNode }: { onAddNode: (entry: MicroflowNodeRegistryEntry) => void }) {
  const grouped = useMemo(() => {
    const map = new Map<string, MicroflowNodeRegistryEntry[]>();
    for (const entry of microflowNodeRegistries) {
      map.set(entry.group, [...(map.get(entry.group) ?? []), entry]);
    }
    return [...map.entries()];
  }, []);

  return (
    <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
      <Title heading={6} style={{ margin: 0 }}>Nodes</Title>
      <Collapse defaultActiveKey={grouped.map(([group]) => group)}>
        {grouped.map(([group, entries]) => (
          <Collapse.Panel key={group} header={group} itemKey={group}>
            <Space vertical align="start" spacing={8} style={{ width: "100%" }}>
              {entries.map(entry => (
                <Button
                  key={entry.activityType ? `${entry.type}:${entry.activityType}` : entry.type}
                  theme="light"
                  block
                  icon={<IconPlus />}
                  onClick={() => onAddNode(entry)}
                >
                  {entry.subgroup ? `${entry.subgroup} / ${entry.title}` : entry.title}
                </Button>
              ))}
            </Space>
          </Collapse.Panel>
        ))}
      </Collapse>
    </Space>
  );
}

function MicroflowCanvas({
  schema,
  selectedNodeId,
  issues,
  viewport,
  onSelectNode,
  onSchemaChange,
  onViewportChange
}: {
  schema: MicroflowSchema;
  selectedNodeId?: string;
  issues: MicroflowValidationIssue[];
  viewport: { zoom: number; offset: MicroflowPosition };
  onSelectNode: (nodeId: string) => void;
  onSchemaChange: (schema: MicroflowSchema) => void;
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
        <svg width="2200" height="1100" style={{ position: "absolute", inset: 0, pointerEvents: "none" }}>
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

function ProblemPanel({ issues }: { issues: MicroflowValidationIssue[] }) {
  if (issues.length === 0) {
    return <Empty image={<IconTickCircle />} title="No validation issues" description="The current schema passes basic validation." />;
  }

  return (
    <Space vertical align="start" spacing={6} style={{ width: "100%" }}>
      {issues.map(issue => (
        <div key={issue.id} style={{ display: "flex", gap: 8, alignItems: "center" }}>
          <Tag color={issue.severity === "error" ? "red" : "orange"}>{issue.severity}</Tag>
          <Text>{issue.message}</Text>
          {issue.nodeId ? <Text type="tertiary">node: {issue.nodeId}</Text> : null}
        </div>
      ))}
    </Space>
  );
}

function DebugPanel({ frames }: { frames: MicroflowTraceFrame[] }) {
  if (frames.length === 0) {
    return <Empty image={<IconPlay />} title="No trace yet" description="Run a test to inspect node input, output, duration, and errors." />;
  }

  return (
    <Space vertical align="start" spacing={6} style={{ width: "100%" }}>
      {frames.map(frame => (
        <div key={frame.frameId} style={{ display: "grid", gridTemplateColumns: "180px 80px 1fr", gap: 12, width: "100%" }}>
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

export function MicroflowEditor({ schema: initialSchema, apiClient, labels, onSchemaChange }: MicroflowEditorProps) {
  const copy = { ...defaultLabels, ...labels };
  const client = useMemo(() => apiClient ?? createLocalMicroflowApiClient([initialSchema]), [apiClient, initialSchema]);
  const [schema, setSchema] = useState<MicroflowSchema>(initialSchema);
  const [selectedNodeId, setSelectedNodeId] = useState<string | undefined>(initialSchema.nodes[0]?.id);
  const [issues, setIssues] = useState<MicroflowValidationIssue[]>(() => validateMicroflowSchema(initialSchema));
  const [traceFrames, setTraceFrames] = useState<MicroflowTraceFrame[]>([]);
  const [viewport, setViewport] = useState(initialSchema.viewport ?? { zoom: 0.8, offset: { x: 24, y: 80 } });

  const selectedNode = schema.nodes.find(node => node.id === selectedNodeId);

  function commitSchema(nextSchema: MicroflowSchema) {
    setSchema(nextSchema);
    onSchemaChange?.(nextSchema);
  }

  async function handleValidate() {
    const result = await client.validateMicroflow({ schema });
    setIssues(result.issues);
    Toast.info(result.valid ? "Validation passed." : `Validation found ${result.issues.length} issue(s).`);
  }

  async function handleSave() {
    const result = await client.saveMicroflow({ schema });
    Toast.success(`Saved ${result.nodeCount} nodes and ${result.edgeCount} flows.`);
  }

  async function handleTestRun() {
    const result = await client.testRunMicroflow({
      microflowId: schema.id,
      input: Object.fromEntries(schema.parameters.map(parameter => [parameter.name, `<${parameter.type.name}>`])),
      schema
    });
    setTraceFrames(result.frames);
    Toast.success(`Test run ${result.runId} ${result.status}.`);
  }

  function handleAddNode(entry: MicroflowNodeRegistryEntry) {
    const registryKey = entry.activityType ? `${entry.type}:${entry.activityType}` : entry.type;
    const nextNode = createMicroflowNodeFromRegistry(registryKey, `${registryKey.replace(":", "-")}-${Date.now()}`, {
      x: 320 + schema.nodes.length * 16,
      y: 120 + schema.nodes.length * 12
    });
    commitSchema({ ...schema, nodes: [...schema.nodes, nextNode] });
    setSelectedNodeId(nextNode.id);
  }

  function handleDeleteSelected() {
    if (!selectedNodeId) {
      return;
    }
    commitSchema({
      ...schema,
      nodes: schema.nodes.filter(node => node.id !== selectedNodeId),
      edges: schema.edges.filter(edge => edge.sourceNodeId !== selectedNodeId && edge.targetNodeId !== selectedNodeId)
    });
    setSelectedNodeId(undefined);
  }

  return (
    <MicroflowRuntimeBoundary>
      <div style={shellStyle} data-testid="microflow-editor">
        <div style={toolbarStyle}>
          <Space>
            <Title heading={5} style={{ margin: 0 }}>{schema.name}</Title>
            <Tag color="blue">Microflow</Tag>
          </Space>
          <Space>
            <Button icon={<IconSearch />} onClick={() => setViewport({ zoom: 0.78, offset: { x: 24, y: 80 } })}>
              {copy.fitView}
            </Button>
            <Button icon={<IconTickCircle />} onClick={() => void handleValidate()}>
              {copy.validate}
            </Button>
            <Button icon={<IconSave />} theme="solid" onClick={() => void handleSave()}>
              {copy.save}
            </Button>
            <Button icon={<IconPlay />} type="primary" theme="solid" onClick={() => void handleTestRun()}>
              {copy.testRun}
            </Button>
          </Space>
        </div>
        <div style={bodyStyle}>
          <aside style={panelStyle}>
            <MicroflowNodePanel onAddNode={handleAddNode} />
          </aside>
          <MicroflowCanvas
            schema={schema}
            selectedNodeId={selectedNodeId}
            issues={issues}
            viewport={viewport}
            onSelectNode={setSelectedNodeId}
            onSchemaChange={commitSchema}
            onViewportChange={setViewport}
          />
          <aside style={rightPanelStyle}>
            <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
              <div style={{ display: "flex", justifyContent: "space-between", width: "100%", alignItems: "center" }}>
                <Title heading={6} style={{ margin: 0 }}>{copy.properties}</Title>
                <Button icon={<IconDelete />} type="danger" theme="borderless" disabled={!selectedNodeId} onClick={handleDeleteSelected} />
              </div>
              <Divider margin="8px" />
              <MicroflowPropertyForm node={selectedNode} variables={schema.variables} />
            </Space>
          </aside>
        </div>
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12, padding: 12, borderTop: "1px solid var(--semi-color-border, #e5e6eb)", overflow: "auto", background: "var(--semi-color-bg-1, #fff)" }}>
          <Card title={`${copy.problems} (${issues.length})`} bodyStyle={{ maxHeight: 110, overflow: "auto" }}>
            <ProblemPanel issues={issues} />
          </Card>
          <Card title={`${copy.debug} (${traceFrames.length})`} bodyStyle={{ maxHeight: 110, overflow: "auto" }}>
            <DebugPanel frames={traceFrames} />
          </Card>
        </div>
      </div>
    </MicroflowRuntimeBoundary>
  );
}
