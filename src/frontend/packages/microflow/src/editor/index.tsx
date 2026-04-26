import { useEffect, useMemo, useRef, useState, type CSSProperties, type DragEvent, type PointerEvent, type ReactNode } from "react";
import { Badge, Button, Card, Empty, Modal, Space, Tabs, Tag, Toast, Typography } from "@douyinfe/semi-ui";
import { IconCopy, IconDelete, IconPlay, IconRefresh, IconSave, IconTickCircle, IconUndo, IconRedo } from "@douyinfe/semi-icons";
import { MicroflowNodePanel, type MicroflowNodePanelLabels } from "../node-panel";
import { MicroflowPropertyPanel, type MicroflowEdgePatch, type MicroflowNodePatch } from "../property-panel";
import {
  getMicroflowNodeRegistryKey,
  microflowNodeRegistryByKey,
  type MicroflowNodeDragPayload,
  type MicroflowNodeRegistryItem
} from "../node-registry";
import { createLocalMicroflowApiClient, type MicroflowApiClient, type MicroflowTraceFrame, type SaveMicroflowResponse, type TestRunMicroflowResponse, type ValidateMicroflowResponse } from "../runtime-adapter";
import {
  applyEditorGraphPatchToAuthoring,
  addParameter,
  createAutoLayoutPatch,
  createAnnotationFlow,
  createObjectFromRegistry,
  createSequenceFlow,
  deleteFlow,
  deleteObject,
  duplicateObject,
  ensureAuthoringSchema,
  findObject,
  moveObject,
  refreshDerivedState,
  splitFlowWithObject,
  toEditorGraph,
  updateFlow,
  updateObject
} from "../adapters";
import { canConnectPorts, inferEdgeKindFromPorts, type MicroflowEditorEdgeKind } from "../node-registry";
import { FlowGramMicroflowCanvas } from "../flowgram";
import { validateMicroflowSchema } from "../schema/validator";
import type {
  MicroflowCaseValue,
  MicroflowDataType,
  MicroflowEditorEdge,
  MicroflowEditorGraph,
  MicroflowEditorGraphPatch,
  MicroflowEditorNode,
  MicroflowEditorPort,
  MicroflowFlow,
  MicroflowObject,
  MicroflowParameter,
  MicroflowSchema,
  MicroflowValidationIssue
} from "../schema/types";

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

const unknownDataType: MicroflowDataType = { kind: "unknown", reason: "new parameter" };

function readFavoriteNodeKeys(): string[] {
  if (typeof window === "undefined") {
    return defaultFavoriteNodeKeys;
  }
  try {
    const value = window.localStorage.getItem(favoriteStorageKey);
    const parsed = value ? JSON.parse(value) as unknown : defaultFavoriteNodeKeys;
    return Array.isArray(parsed) && parsed.every(item => typeof item === "string") ? parsed : defaultFavoriteNodeKeys;
  } catch {
    return defaultFavoriteNodeKeys;
  }
}

function saveFavoriteNodeKeys(keys: string[]): void {
  if (typeof window !== "undefined") {
    window.localStorage.setItem(favoriteStorageKey, JSON.stringify(keys));
  }
}

function parseDragPayload(value: string): MicroflowNodeDragPayload | undefined {
  try {
    const parsed = JSON.parse(value) as MicroflowNodeDragPayload;
    return parsed.dragType === "microflow-node" || parsed.sourcePanel === "microflow-node-panel" || parsed.sourcePanel === "nodes" ? parsed : undefined;
  } catch {
    return undefined;
  }
}

const shellStyle: CSSProperties = {
  display: "grid",
  gridTemplateRows: "60px minmax(0, 1fr) 220px",
  height: "100%",
  minHeight: 0,
  background: "var(--semi-color-bg-0, #f7f8fa)"
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

const canvasStyle: CSSProperties = {
  position: "relative",
  minWidth: 0,
  minHeight: 0,
  overflow: "hidden",
  background: "radial-gradient(circle, rgba(22, 93, 255, 0.08) 1px, transparent 1px), var(--semi-color-fill-0, #f4f7fb)",
  backgroundSize: "22px 22px"
};

function screenToCanvas(container: HTMLDivElement | null, event: { clientX: number; clientY: number }, graph: MicroflowEditorGraph) {
  const rect = container?.getBoundingClientRect();
  return {
    x: ((event.clientX - (rect?.left ?? 0)) - graph.viewport.x) / graph.viewport.zoom,
    y: ((event.clientY - (rect?.top ?? 0)) - graph.viewport.y) / graph.viewport.zoom
  };
}

function snapCanvasPoint(point: { x: number; y: number }, gridSize = 12) {
  return {
    x: Math.round(point.x / gridSize) * gridSize,
    y: Math.round(point.y / gridSize) * gridSize
  };
}

function findLoopAtPosition(graph: MicroflowEditorGraph, position: { x: number; y: number }): string | undefined {
  return graph.nodes.find(node => {
    if (node.nodeKind !== "loopedActivity") {
      return false;
    }
    const width = node.size.width + 280;
    const height = node.size.height + 190;
    return position.x >= node.position.x && position.x <= node.position.x + width && position.y >= node.position.y && position.y <= node.position.y + height;
  })?.objectId;
}

function collectVariables(schema: MicroflowSchema) {
  return Object.values(schema.variables.parameters)
    .concat(Object.values(schema.variables.localVariables))
    .concat(Object.values(schema.variables.objectOutputs))
    .concat(Object.values(schema.variables.listOutputs))
    .concat(Object.values(schema.variables.loopVariables))
    .concat(Object.values(schema.variables.errorVariables))
    .concat(Object.values(schema.variables.systemVariables));
}

function createFlowForConnection(schema: MicroflowSchema, sourceObjectId: string, targetObjectId: string): MicroflowFlow {
  const source = findObject(schema, sourceObjectId);
  const target = findObject(schema, targetObjectId);
  if (!source || !target) {
    return createSequenceFlow({ originObjectId: sourceObjectId, destinationObjectId: targetObjectId });
  }
  if (source.kind === "annotation" || target.kind === "annotation") {
    return createAnnotationFlow({ originObjectId: sourceObjectId, destinationObjectId: targetObjectId });
  }
  const supportsErrorHandling = source.kind === "actionActivity"
    ? source.action.errorHandlingType !== "rollback"
    : source.kind === "loopedActivity"
      ? source.errorHandlingType !== "rollback"
      : source.kind === "exclusiveSplit" || source.kind === "inheritanceSplit";
  if (target.kind === "errorEvent" && supportsErrorHandling) {
    return createSequenceFlow({
      originObjectId: sourceObjectId,
      destinationObjectId: targetObjectId,
      isErrorHandler: true,
      edgeKind: "errorHandler",
      label: "error"
    });
  }
  if (source.kind === "exclusiveSplit") {
    const existing = schema.flows.filter(
      (flow): flow is Extract<MicroflowFlow, { kind: "sequence" }> =>
        flow.kind === "sequence" && flow.originObjectId === source.id && !flow.isErrorHandler
    );
    if (source.splitCondition.kind === "expression" && source.splitCondition.resultType === "boolean") {
      const used = new Set(
        existing.flatMap(flow => flow.caseValues)
          .filter(caseValue => caseValue.kind === "boolean")
          .map(caseValue => caseValue.value)
      );
      if (!used.has(true)) {
        return createSequenceFlow({
          originObjectId: sourceObjectId,
          destinationObjectId: targetObjectId,
          edgeKind: "decisionCondition",
          caseValues: [{ kind: "boolean", officialType: "Microflows$EnumerationCase", value: true, persistedValue: "true" }],
          label: "true"
        });
      }
      if (!used.has(false)) {
        return createSequenceFlow({
          originObjectId: sourceObjectId,
          destinationObjectId: targetObjectId,
          edgeKind: "decisionCondition",
          caseValues: [{ kind: "boolean", officialType: "Microflows$EnumerationCase", value: false, persistedValue: "false" }],
          label: "false"
        });
      }
    }
    return createSequenceFlow({
      originObjectId: sourceObjectId,
      destinationObjectId: targetObjectId,
      edgeKind: "decisionCondition",
      caseValues: [{ kind: "fallback", officialType: "Microflows$NoCase" }],
      label: "fallback"
    });
  }
  if (source.kind === "inheritanceSplit") {
    const existingCases = schema.flows
      .filter((flow): flow is Extract<MicroflowFlow, { kind: "sequence" }> => flow.kind === "sequence" && flow.originObjectId === source.id && !flow.isErrorHandler)
      .flatMap(flow => flow.caseValues)
      .filter(caseValue => caseValue.kind === "inheritance")
      .map(caseValue => caseValue.entityQualifiedName);
    const nextEntity = source.entity.allowedSpecializations.find(entity => !existingCases.includes(entity));
    return createSequenceFlow({
      originObjectId: sourceObjectId,
      destinationObjectId: targetObjectId,
      edgeKind: "objectTypeCondition",
      caseValues: nextEntity
        ? [{ kind: "inheritance", officialType: "Microflows$InheritanceCase", entityQualifiedName: nextEntity }]
        : [{ kind: "fallback", officialType: "Microflows$NoCase" }],
      label: nextEntity ?? "fallback"
    });
  }
  return createSequenceFlow({ originObjectId: sourceObjectId, destinationObjectId: targetObjectId });
}

function caseValueFromPort(sourcePort: MicroflowEditorPort, source?: MicroflowObject): MicroflowCaseValue[] {
  if (source?.kind === "exclusiveSplit") {
    const label = sourcePort.label.toLowerCase();
    if (label === "true" || sourcePort.kind === "decisionOut" && sourcePort.connectionIndex === 1) {
      return [{ kind: "boolean", officialType: "Microflows$EnumerationCase", value: true, persistedValue: "true" }];
    }
    if (label === "false" || sourcePort.kind === "decisionOut") {
      return [{ kind: "boolean", officialType: "Microflows$EnumerationCase", value: false, persistedValue: "false" }];
    }
  }
  if (source?.kind === "inheritanceSplit") {
    const entityQualifiedName = source.entity.allowedSpecializations[sourcePort.connectionIndex] ?? "";
    return entityQualifiedName
      ? [{ kind: "inheritance", officialType: "Microflows$InheritanceCase", entityQualifiedName }]
      : [{ kind: "fallback", officialType: "Microflows$NoCase" }];
  }
  return [];
}

function createFlowFromPorts(schema: MicroflowSchema, sourcePort: MicroflowEditorPort, targetPort: MicroflowEditorPort): MicroflowFlow {
  const source = findObject(schema, sourcePort.objectId);
  const target = findObject(schema, targetPort.objectId);
  if (!source || !target) {
    return createSequenceFlow({ originObjectId: sourcePort.objectId, destinationObjectId: targetPort.objectId });
  }
  const edgeKind = inferEdgeKindFromPorts(source, target, sourcePort);
  if (edgeKind === "annotation") {
    return createAnnotationFlow({
      originObjectId: sourcePort.objectId,
      destinationObjectId: targetPort.objectId,
      label: "annotation"
    });
  }
  return createSequenceFlow({
    originObjectId: sourcePort.objectId,
    destinationObjectId: targetPort.objectId,
    originConnectionIndex: sourcePort.connectionIndex,
    destinationConnectionIndex: targetPort.connectionIndex,
    edgeKind: edgeKind === "errorHandler" ? "errorHandler" : edgeKind,
    isErrorHandler: edgeKind === "errorHandler",
    caseValues: edgeKind === "decisionCondition" || edgeKind === "objectTypeCondition" ? caseValueFromPort(sourcePort, source) : [],
    label: edgeKind === "errorHandler" ? "Error" : sourcePort.label
  });
}

function absolutePortPosition(node: MicroflowEditorNode, port: MicroflowEditorPort) {
  const relative = port.position ?? {
    x: port.direction === "input" ? 0 : node.size.width,
    y: node.size.height / 2
  };
  return {
    x: node.position.x + relative.x,
    y: node.position.y + relative.y
  };
}

function connectionPath(source: { x: number; y: number }, target: { x: number; y: number }) {
  const mid = (source.x + target.x) / 2;
  return `M ${source.x} ${source.y} C ${mid} ${source.y}, ${mid} ${target.y}, ${target.x} ${target.y}`;
}

function distanceToSegment(point: { x: number; y: number }, source: { x: number; y: number }, target: { x: number; y: number }) {
  const dx = target.x - source.x;
  const dy = target.y - source.y;
  const lengthSquared = dx * dx + dy * dy;
  if (lengthSquared === 0) {
    return Math.hypot(point.x - source.x, point.y - source.y);
  }
  const t = Math.max(0, Math.min(1, ((point.x - source.x) * dx + (point.y - source.y) * dy) / lengthSquared));
  return Math.hypot(point.x - (source.x + t * dx), point.y - (source.y + t * dy));
}

function findNearestFlowAtPoint(graph: MicroflowEditorGraph, point: { x: number; y: number }, threshold = 18): string | undefined {
  const nodeById = new Map(graph.nodes.map(node => [node.objectId, node]));
  let nearest: { flowId: string; distance: number } | undefined;
  for (const edge of graph.edges) {
    const source = nodeById.get(edge.sourceNodeId.replace(/^node-/, ""));
    const target = nodeById.get(edge.targetNodeId.replace(/^node-/, ""));
    if (!source || !target || edge.edgeKind === "annotation") {
      continue;
    }
    const sourcePort = source.ports.find(port => port.id === edge.sourcePortId) ?? source.ports.find(port => port.direction === "output");
    const targetPort = target.ports.find(port => port.id === edge.targetPortId) ?? target.ports.find(port => port.direction === "input");
    const distance = distanceToSegment(
      point,
      sourcePort ? absolutePortPosition(source, sourcePort) : { x: source.position.x + source.size.width, y: source.position.y + source.size.height / 2 },
      targetPort ? absolutePortPosition(target, targetPort) : { x: target.position.x, y: target.position.y + target.size.height / 2 }
    );
    if (distance <= threshold && (!nearest || distance < nearest.distance)) {
      nearest = { flowId: edge.flowId, distance };
    }
  }
  return nearest?.flowId;
}

function NodeCard({
  node,
  selected,
  connecting,
  connectionMode,
  validTargetPortIds,
  invalidTargetPortIds,
  trace,
  onPointerDown,
  onSelect,
  onConnect,
  onStartConnection,
  onFinishConnection
}: {
  node: MicroflowEditorNode;
  selected: boolean;
  connecting: boolean;
  connectionMode: boolean;
  validTargetPortIds: Set<string>;
  invalidTargetPortIds: Set<string>;
  trace?: MicroflowTraceFrame;
  onPointerDown: (event: PointerEvent<HTMLDivElement>) => void;
  onSelect: () => void;
  onConnect: () => void;
  onStartConnection: (port: MicroflowEditorPort, event: PointerEvent<HTMLButtonElement>) => void;
  onFinishConnection: (port: MicroflowEditorPort, event: PointerEvent<HTMLButtonElement>) => void;
}) {
  const showPorts = selected || connecting || connectionMode;
  return (
    <div
      onClick={event => {
        event.stopPropagation();
        onSelect();
      }}
      onPointerDown={onPointerDown}
      style={{
        position: "absolute",
        left: node.position.x,
        top: node.position.y,
        width: node.size.width,
        minHeight: node.size.height,
        padding: 10,
        borderRadius: node.nodeKind === "startEvent" || node.nodeKind === "endEvent" ? 999 : 14,
        border: selected ? "2px solid #165dff" : "1px solid rgba(78, 89, 105, 0.22)",
        background: node.state.disabled ? "rgba(242,243,245,0.92)" : "var(--semi-color-bg-2, #fff)",
        boxShadow: selected ? "0 12px 32px rgba(22, 93, 255, 0.16)" : "0 8px 24px rgba(29, 33, 41, 0.08)",
        cursor: "grab",
        userSelect: "none"
      }}
    >
      <Space align="start" style={{ width: "100%", justifyContent: "space-between" }}>
        <div style={{ minWidth: 0 }}>
          <Text strong ellipsis={{ showTooltip: true }}>{node.title}</Text>
          <br />
          <Text size="small" type="tertiary" ellipsis={{ showTooltip: true }}>{node.activityKind ?? node.nodeKind}</Text>
        </div>
        <Button
          size="small"
          theme={connecting ? "solid" : "borderless"}
          type={connecting ? "primary" : "tertiary"}
          onClick={event => {
            event.stopPropagation();
            onConnect();
          }}
        >
          +
        </Button>
      </Space>
      {trace ? <Tag color={trace.status === "failed" ? "red" : "green"} style={{ marginTop: 8 }}>{trace.status} {trace.durationMs}ms</Tag> : null}
      {node.state.hasError ? <Badge dot type="danger" style={{ position: "absolute", right: -3, top: -3 }} /> : null}
      {showPorts ? node.ports.map(port => {
        const relative = port.position ?? { x: port.direction === "input" ? 0 : node.size.width, y: node.size.height / 2 };
        const valid = validTargetPortIds.has(port.id);
        const invalid = invalidTargetPortIds.has(port.id);
        const color = port.kind === "errorOut"
          ? "#f93920"
          : port.kind === "annotation"
            ? "#86909c"
            : valid
              ? "#00b42a"
              : invalid
                ? "#f93920"
                : "#165dff";
        return (
          <button
            key={port.id}
            type="button"
            title={port.label}
            aria-label={port.label}
            onPointerDown={event => {
              event.stopPropagation();
              if (port.direction === "output") {
                onStartConnection(port, event);
              }
            }}
            onPointerUp={event => {
              event.stopPropagation();
              if (port.direction === "input") {
                onFinishConnection(port, event);
              }
            }}
            style={{
              position: "absolute",
              left: relative.x - 6,
              top: relative.y - 6,
              width: 12,
              height: 12,
              borderRadius: 999,
              border: `2px solid ${color}`,
              background: valid ? "#e8ffea" : invalid ? "#fff1f0" : "var(--semi-color-bg-2, #fff)",
              cursor: port.direction === "output" ? "crosshair" : connectionMode ? "copy" : "default",
              padding: 0
            }}
          />
        );
      }) : null}
    </div>
  );
}

function EdgeLayer({
  graph,
  selectedFlowId,
  onSelect
}: {
  graph: MicroflowEditorGraph;
  selectedFlowId?: string;
  onSelect: (flowId: string) => void;
}) {
  const nodeById = new Map(graph.nodes.map(node => [node.objectId, node]));
  return (
    <svg style={{ position: "absolute", inset: 0, width: "100%", height: "100%", overflow: "visible", pointerEvents: "none" }}>
      {graph.edges.map(edge => {
        const source = nodeById.get(edge.sourceNodeId.replace(/^node-/, ""));
        const target = nodeById.get(edge.targetNodeId.replace(/^node-/, ""));
        if (!source || !target) {
          return null;
        }
        const sourcePort = source.ports.find(port => port.id === edge.sourcePortId) ?? source.ports.find(port => port.direction === "output");
        const targetPort = target.ports.find(port => port.id === edge.targetPortId) ?? target.ports.find(port => port.direction === "input");
        const sourcePosition = sourcePort ? absolutePortPosition(source, sourcePort) : { x: source.position.x + source.size.width, y: source.position.y + source.size.height / 2 };
        const targetPosition = targetPort ? absolutePortPosition(target, targetPort) : { x: target.position.x, y: target.position.y + target.size.height / 2 };
        const mid = (sourcePosition.x + targetPosition.x) / 2;
        const selected = selectedFlowId === edge.flowId;
        return (
          <g key={edge.id} style={{ pointerEvents: "auto" }} onClick={event => { event.stopPropagation(); onSelect(edge.flowId); }}>
            <path
              d={connectionPath(sourcePosition, targetPosition)}
              fill="none"
              stroke={selected ? "#165dff" : edge.style.colorToken}
              strokeWidth={selected ? 3 : 2}
              strokeDasharray={edge.style.strokeType === "dashed" ? "6 4" : edge.style.strokeType === "dotted" ? "2 4" : undefined}
            />
            <circle cx={targetPosition.x} cy={targetPosition.y} r={4} fill={selected ? "#165dff" : edge.style.colorToken} />
            {edge.label ? <text x={mid} y={(sourcePosition.y + targetPosition.y) / 2 - 8} fontSize={12} fill="#4e5969">{edge.label}</text> : null}
          </g>
        );
      })}
    </svg>
  );
}

// Legacy HTML/SVG canvas retained only as a rollback reference. The editor shell renders FlowGramMicroflowCanvas by default.
function LegacyHtmlMicroflowCanvas({
  schema,
  graph,
  traceFrames,
  onPatch,
  onDropRegistryItem,
  onConnectPorts
}: {
  schema: MicroflowSchema;
  graph: MicroflowEditorGraph;
  traceFrames: MicroflowTraceFrame[];
  onPatch: (schemaPatch: MicroflowEditorGraphPatch) => void;
  onDropRegistryItem: (item: MicroflowNodeRegistryItem, position: { x: number; y: number }, insertFlowId?: string) => void;
  onConnectPorts: (sourcePort: MicroflowEditorPort, targetPort: MicroflowEditorPort) => void;
}) {
  const containerRef = useRef<HTMLDivElement>(null);
  const [drag, setDrag] = useState<{ objectId: string; offsetX: number; offsetY: number } | null>(null);
  const [connectingFrom, setConnectingFrom] = useState<string | undefined>();
  const [dropActive, setDropActive] = useState(false);
  const [connectionDraft, setConnectionDraft] = useState<{ sourceNodeId: string; sourcePort: MicroflowEditorPort; target: { x: number; y: number } }>();
  const traceByObject = useMemo(() => new Map(traceFrames.map(frame => [frame.objectId, frame])), [traceFrames]);
  const selectedFlowId = graph.selection.flowId;
  const selectedObjectId = graph.selection.objectId;
  const nodeByObjectId = useMemo(() => new Map(graph.nodes.map(node => [node.objectId, node])), [graph.nodes]);
  const validTargetPortIds = useMemo(() => {
    if (!connectionDraft) {
      return new Set<string>();
    }
    const result = new Set<string>();
    for (const node of graph.nodes) {
      for (const port of node.ports) {
        if (canConnectPorts(schema, connectionDraft.sourcePort, port).allowed) {
          result.add(port.id);
        }
      }
    }
    return result;
  }, [connectionDraft, graph.nodes, schema]);
  const invalidTargetPortIds = useMemo(() => {
    if (!connectionDraft) {
      return new Set<string>();
    }
    const result = new Set<string>();
    for (const node of graph.nodes) {
      for (const port of node.ports) {
        if (port.direction === "input" && !validTargetPortIds.has(port.id)) {
          result.add(port.id);
        }
      }
    }
    return result;
  }, [connectionDraft, graph.nodes, validTargetPortIds]);

  const emitPatch = (patch: Parameters<typeof applyEditorGraphPatchToAuthoring>[1]) => {
    onPatch(patch);
  };

  return (
    <div
      ref={containerRef}
      style={canvasStyle}
      onClick={() => emitPatch({ selectedObjectId: undefined, selectedFlowId: undefined })}
      onDragEnter={event => {
        event.preventDefault();
        setDropActive(true);
      }}
      onDragOver={event => {
        event.preventDefault();
        event.dataTransfer.dropEffect = "copy";
        setDropActive(true);
      }}
      onDragLeave={event => {
        if (event.currentTarget === event.target) {
          setDropActive(false);
        }
      }}
      onDrop={(event: DragEvent<HTMLDivElement>) => {
        event.preventDefault();
        setDropActive(false);
        const payload = parseDragPayload(event.dataTransfer.getData("application/x-atlas-microflow-node") || event.dataTransfer.getData("application/json"));
        const registryKey = payload?.registryKey ?? event.dataTransfer.getData("text/plain");
        if (!registryKey) {
          return;
        }
        const entry = microflowNodeRegistryByKey.get(registryKey);
        if (!entry) {
          return;
        }
        const position = snapCanvasPoint(screenToCanvas(containerRef.current, event, graph));
        onDropRegistryItem(entry, position, findNearestFlowAtPoint(graph, position));
      }}
      onPointerMove={event => {
        if (connectionDraft) {
          setConnectionDraft({ ...connectionDraft, target: screenToCanvas(containerRef.current, event, graph) });
          return;
        }
        if (!drag) {
          return;
        }
        const position = screenToCanvas(containerRef.current, event, graph);
        emitPatch({ movedNodes: [{ objectId: drag.objectId, position: { x: position.x - drag.offsetX, y: position.y - drag.offsetY } }] });
      }}
      onPointerUp={() => {
        setDrag(null);
        setConnectionDraft(undefined);
      }}
      onKeyDown={event => {
        if (event.key === "Escape") {
          setConnectionDraft(undefined);
        }
      }}
      tabIndex={0}
    >
      {dropActive ? (
        <div style={{ position: "absolute", inset: 12, border: "2px dashed #165dff", borderRadius: 12, pointerEvents: "none", zIndex: 2 }} />
      ) : null}
      <div
        style={{
          position: "absolute",
          transform: `translate(${graph.viewport.x}px, ${graph.viewport.y}px) scale(${graph.viewport.zoom})`,
          transformOrigin: "0 0",
          width: 3200,
          height: 2200
        }}
      >
        <EdgeLayer graph={graph} selectedFlowId={selectedFlowId} onSelect={flowId => emitPatch({ selectedObjectId: undefined, selectedFlowId: flowId })} />
        {connectionDraft ? (() => {
          const sourceNode = nodeByObjectId.get(connectionDraft.sourceNodeId);
          if (!sourceNode) {
            return null;
          }
          const sourcePosition = absolutePortPosition(sourceNode, connectionDraft.sourcePort);
          return (
            <svg style={{ position: "absolute", inset: 0, width: "100%", height: "100%", overflow: "visible", pointerEvents: "none" }}>
              <path d={connectionPath(sourcePosition, connectionDraft.target)} fill="none" stroke="#165dff" strokeWidth={2} strokeDasharray="6 4" />
            </svg>
          );
        })() : null}
        {graph.nodes.map(node => (
          <NodeCard
            key={node.id}
            node={node}
            selected={node.objectId === selectedObjectId}
            connecting={connectingFrom === node.objectId}
            connectionMode={Boolean(connectionDraft)}
            validTargetPortIds={validTargetPortIds}
            invalidTargetPortIds={invalidTargetPortIds}
            trace={traceByObject.get(node.objectId)}
            onSelect={() => {
              if (connectingFrom && connectingFrom !== node.objectId) {
                emitPatch({ addFlow: createFlowForConnection(schema, connectingFrom, node.objectId) });
                setConnectingFrom(undefined);
                return;
              }
              emitPatch({ selectedObjectId: node.objectId, selectedFlowId: undefined });
            }}
            onConnect={() => setConnectingFrom(connectingFrom === node.objectId ? undefined : node.objectId)}
            onStartConnection={(port, event) => {
              const position = screenToCanvas(containerRef.current, event, graph);
              setConnectingFrom(undefined);
              setConnectionDraft({ sourceNodeId: node.objectId, sourcePort: port, target: position });
            }}
            onFinishConnection={(port) => {
              if (!connectionDraft) {
                return;
              }
              if (canConnectPorts(schema, connectionDraft.sourcePort, port).allowed) {
                onConnectPorts(connectionDraft.sourcePort, port);
              }
              setConnectionDraft(undefined);
            }}
            onPointerDown={event => {
              if (connectionDraft) {
                return;
              }
              event.stopPropagation();
              const position = screenToCanvas(containerRef.current, event, graph);
              setDrag({ objectId: node.objectId, offsetX: position.x - node.position.x, offsetY: position.y - node.position.y });
            }}
          />
        ))}
      </div>
    </div>
  );
}

function ProblemPanel({ issues, onSelect }: { issues: MicroflowValidationIssue[]; onSelect: (issue: MicroflowValidationIssue) => void }) {
  if (issues.length === 0) {
    return <Empty title="No problems" description="Schema validation passed." />;
  }
  return (
    <Space vertical align="start" style={{ width: "100%" }}>
      {issues.map(issue => (
        <Card key={issue.id} shadows="hover" style={{ width: "100%" }} bodyStyle={{ padding: 10 }} onClick={() => onSelect(issue)}>
          <Space align="start" style={{ width: "100%", justifyContent: "space-between" }}>
            <div style={{ minWidth: 0 }}>
              <Text strong>{issue.code}</Text>
              <br />
              <Text size="small" type="secondary">{issue.message}</Text>
              <br />
              <Text size="small" type="tertiary">{issue.fieldPath ?? issue.objectId ?? issue.flowId ?? issue.actionId}</Text>
            </div>
            <Tag color={issue.severity === "error" ? "red" : issue.severity === "warning" ? "orange" : "blue"}>{issue.severity}</Tag>
          </Space>
        </Card>
      ))}
    </Space>
  );
}

function DebugPanel({ frames }: { frames: MicroflowTraceFrame[] }) {
  if (frames.length === 0) {
    return <Empty title="No trace" description="Run a test to see object/flow trace frames." />;
  }
  return (
    <Space vertical align="start" style={{ width: "100%" }}>
      {frames.map(frame => (
        <Card key={frame.id} style={{ width: "100%" }} bodyStyle={{ padding: 10 }}>
          <Text strong>{frame.objectTitle}</Text>
          <br />
          <Text size="small" type="tertiary">{frame.objectId} · {frame.status} · {frame.durationMs}ms</Text>
        </Card>
      ))}
    </Space>
  );
}

export function MicroflowEditor(props: MicroflowEditorProps) {
  const labels = { ...defaultLabels, ...props.labels };
  const apiClient = props.apiClient ?? createLocalMicroflowApiClient();
  const [schema, setSchema] = useState<MicroflowSchema>(() => refreshDerivedState(ensureAuthoringSchema(props.schema)));
  const [favoriteNodeKeys, setFavoriteNodeKeys] = useState(readFavoriteNodeKeys);
  const [issues, setIssues] = useState<MicroflowValidationIssue[]>(schema.validation.issues ?? []);
  const [traceFrames, setTraceFrames] = useState<MicroflowTraceFrame[]>([]);
  const [history, setHistory] = useState<MicroflowSchema[]>([]);
  const [future, setFuture] = useState<MicroflowSchema[]>([]);
  const [saving, setSaving] = useState(false);
  const [running, setRunning] = useState(false);
  const [dirty, setDirty] = useState(false);

  const graph = useMemo(() => toEditorGraph({ ...schema, validation: { issues } }), [schema, issues]);
  const selectedObject = schema.editor.selection.objectId ? findObject(schema, schema.editor.selection.objectId) ?? null : null;
  const selectedFlow = schema.editor.selection.flowId ? schema.flows.find(flow => flow.id === schema.editor.selection.flowId) ?? null : null;

  const commitSchema = (next: MicroflowSchema, markDirty = true) => {
    const refreshed = refreshDerivedState(next);
    if (markDirty) {
      setHistory(items => [...items.slice(-30), schema]);
      setFuture([]);
      setDirty(true);
    }
    setSchema(refreshed);
    setIssues(validateMicroflowSchema(refreshed));
    props.onSchemaChange?.(refreshed);
  };

  const applyPatch = (patch: Parameters<typeof applyEditorGraphPatchToAuthoring>[1], markDirty = true) => {
    commitSchema(applyEditorGraphPatchToAuthoring(schema, patch), markDirty);
  };

  function createParameterForNode(item: MicroflowNodeRegistryItem, position: { x: number; y: number }): MicroflowSchema {
    const parameterId = `param-${Date.now()}`;
    const parameterName = `input${schema.parameters.length + 1}`;
    const parameter: MicroflowParameter = {
      id: parameterId,
      stableId: parameterId,
      name: parameterName,
      dataType: unknownDataType,
      type: { kind: "unknown", name: "Unknown" },
      required: true,
      documentation: item.documentation.summary
    };
    const objectId = `parameter-object-${parameterId}`;
    const next = addParameter(schema, parameter, position);
    return {
      ...next,
      editor: {
        ...next.editor,
        selection: { objectId, flowId: undefined }
      }
    };
  }

  const handleAddNode = (item: MicroflowNodeRegistryItem, options?: { position?: { x: number; y: number }; insertFlowId?: string }) => {
    const position = options?.position ?? { x: 120 + graph.nodes.length * 36, y: 120 + graph.nodes.length * 18 };
    const parentLoopObjectId = findLoopAtPosition(graph, position);
    if ((item.type === "event" && (item.defaultConfig as { eventType?: string }).eventType === "start") && parentLoopObjectId) {
      Toast.warning("Start Event 不能放入 Loop 内部");
      return;
    }
    if ((item.type === "event" && ["break", "continue"].includes((item.defaultConfig as { eventType?: string }).eventType ?? "")) && !parentLoopObjectId) {
      Toast.warning("Break / Continue 只能放在 Loop 内");
      return;
    }
    if (item.type === "parameter") {
      commitSchema(createParameterForNode(item, position));
      return;
    }
    const object = createObjectFromRegistry(item, position);
    if (options?.insertFlowId) {
      const flow = schema.flows.find(item => item.id === options.insertFlowId);
      if (flow?.kind === "annotation") {
        Toast.warning("AnnotationFlow 暂不支持插入节点");
        return;
      }
      commitSchema({
        ...splitFlowWithObject(schema, options.insertFlowId, object),
        editor: {
          ...schema.editor,
          selection: { objectId: object.id, flowId: undefined }
        }
      });
      return;
    }
    commitSchema(applyEditorGraphPatchToAuthoring(schema, {
      addObject: { object, parentLoopObjectId },
      selectedObjectId: object.id,
      selectedFlowId: undefined
    }));
  };

  const handleSave = async () => {
    setSaving(true);
    try {
      const response = await apiClient.saveMicroflow({ schema });
      props.onSaveComplete?.(response);
      setDirty(false);
      Toast.success(`Saved ${response.version}`);
    } finally {
      setSaving(false);
    }
  };

  const handleValidate = async () => {
    const response = await apiClient.validateMicroflow({ schema });
    setIssues(response.issues);
    props.onValidateComplete?.(response);
    Toast[response.valid ? "success" : "warning"](response.valid ? "Validation passed" : `${response.issues.length} issue(s)`);
  };

  const handleTestRun = async () => {
    setRunning(true);
    try {
      const response = await apiClient.testRunMicroflow({ microflowId: schema.id, input: {}, schema });
      setTraceFrames(response.frames);
      props.onTestRunComplete?.(response);
      Toast[response.status === "succeeded" ? "success" : "error"](response.status);
    } finally {
      setRunning(false);
    }
  };

  const handleUndo = () => {
    const previous = history.at(-1);
    if (!previous) {
      return;
    }
    const restored = refreshDerivedState(previous);
    setFuture(items => [schema, ...items]);
    setHistory(items => items.slice(0, -1));
    setSchema(restored);
    setIssues(validateMicroflowSchema(restored));
    setDirty(true);
    props.onSchemaChange?.(restored);
  };

  const handleRedo = () => {
    const next = future[0];
    if (!next) {
      return;
    }
    const restored = refreshDerivedState(next);
    setHistory(items => [...items, schema]);
    setFuture(items => items.slice(1));
    setSchema(restored);
    setIssues(validateMicroflowSchema(restored));
    setDirty(true);
    props.onSchemaChange?.(restored);
  };

  const handleAutoLayout = () => {
    const patch = createAutoLayoutPatch(schema);
    if (!patch.movedNodes?.length) {
      Toast.warning("No nodes to layout.");
      return;
    }
    commitSchema(applyEditorGraphPatchToAuthoring(schema, patch));
    Toast.success("Auto layout applied.");
  };

  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      if (!event.ctrlKey && !event.metaKey) {
        return;
      }
      const target = event.target instanceof HTMLElement ? event.target : null;
      if (target?.closest("input, textarea, [contenteditable='true']")) {
        return;
      }
      const key = event.key.toLowerCase();
      if (key === "z" && !event.shiftKey) {
        event.preventDefault();
        handleUndo();
        return;
      }
      if (key === "y" || (key === "z" && event.shiftKey)) {
        event.preventDefault();
        handleRedo();
      }
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [future, history, schema]);

  return (
    <div style={shellStyle}>
      <div style={toolbarStyle}>
        <Space>
          {props.toolbarPrefix}
          <Title heading={5} style={{ margin: 0 }}>{schema.displayName || schema.name}</Title>
          <Tag>{schema.schemaVersion}</Tag>
          {dirty ? <Tag color="orange">dirty</Tag> : null}
          <Tag color={issues.some(issue => issue.severity === "error") ? "red" : "green"}>{issues.length} issues</Tag>
        </Space>
        <Space>
          <Button icon={<IconUndo />} disabled={history.length === 0} onClick={handleUndo}>{labels.undo}</Button>
          <Button icon={<IconRedo />} disabled={future.length === 0} onClick={handleRedo}>{labels.redo}</Button>
          <Button icon={<IconRefresh />} onClick={handleValidate}>{labels.validate}</Button>
          <Button icon={<IconPlay />} loading={running} onClick={handleTestRun}>{labels.testRun}</Button>
          <Button icon={<IconSave />} loading={saving} type="primary" onClick={handleSave}>{labels.save}</Button>
          {props.toolbarSuffix}
        </Space>
      </div>
      <div style={bodyStyle}>
        <div style={panelStyle}>
          <MicroflowNodePanel
            favoriteNodeKeys={favoriteNodeKeys}
            onFavoriteChange={keys => {
              setFavoriteNodeKeys(keys);
              saveFavoriteNodeKeys(keys);
            }}
            onAddNode={(item, options) => handleAddNode(item, options)}
            onShowDocumentation={item => Modal.info({ title: item.title, content: item.documentation.summary })}
            labels={props.nodePanelLabels}
          />
        </div>
        <FlowGramMicroflowCanvas
          schema={schema}
          validationIssues={issues}
          runtimeTrace={traceFrames}
          onSchemaChange={nextSchema => {
            commitSchema(nextSchema);
          }}
          onSelectionChange={selection => {
            applyPatch({
              selectedObjectId: selection.objectId,
              selectedFlowId: selection.flowId
            }, false);
          }}
          onDropRegistryItem={(item, position) => handleAddNode(item, { position })}
          canUndo={history.length > 0}
          canRedo={future.length > 0}
          onUndo={handleUndo}
          onRedo={handleRedo}
          onAutoLayout={handleAutoLayout}
        />
        <div style={rightPanelStyle}>
          <MicroflowPropertyPanel
            selectedObject={selectedObject}
            selectedFlow={selectedFlow}
            schema={schema}
            validationIssues={issues}
            traceFrames={traceFrames}
            onObjectChange={(objectId, patch: MicroflowNodePatch) => {
              if (!patch.object) {
                return;
              }
              commitSchema(updateObject(schema, objectId, () => patch.object as MicroflowObject));
            }}
            onFlowChange={(flowId, patch: MicroflowEdgePatch) => {
              commitSchema(updateFlow(schema, flowId, flow => {
                const next = { ...flow, ...patch } as MicroflowFlow;
                if (flow.kind === "sequence") {
                  const partial = patch as Partial<Extract<MicroflowFlow, { kind: "sequence" }>>;
                  return {
                    ...flow,
                    ...partial,
                    line: partial.line ?? flow.line,
                    editor: partial.editor ? { ...flow.editor, ...partial.editor } : flow.editor
                  };
                }
                const partial = patch as Partial<Extract<MicroflowFlow, { kind: "annotation" }>>;
                return {
                  ...flow,
                  ...partial,
                  line: partial.line ?? flow.line,
                  editor: partial.editor ? { ...flow.editor, ...partial.editor } : flow.editor
                };
              }));
            }}
            onDuplicateObject={objectId => commitSchema(duplicateObject(schema, objectId))}
            onDeleteObject={objectId => commitSchema(deleteObject(schema, objectId))}
            onDeleteFlow={flowId => commitSchema(deleteFlow(schema, flowId))}
            onClose={() => applyPatch({ selectedObjectId: undefined, selectedFlowId: undefined }, false)}
          />
        </div>
      </div>
      <div style={{ minHeight: 0, borderTop: "1px solid var(--semi-color-border, #e5e6eb)", background: "var(--semi-color-bg-1, #fff)", overflow: "auto", padding: 12 }}>
        <Tabs type="line">
          <Tabs.TabPane tab={<Space><IconTickCircle />{labels.problems}<Badge count={issues.length} /></Space>} itemKey="problems">
            <ProblemPanel
              issues={issues}
              onSelect={issue => applyPatch({ selectedObjectId: issue.objectId, selectedFlowId: issue.flowId }, false)}
            />
          </Tabs.TabPane>
          <Tabs.TabPane tab={labels.debug} itemKey="debug">
            <DebugPanel frames={traceFrames} />
          </Tabs.TabPane>
        </Tabs>
      </div>
    </div>
  );
}
