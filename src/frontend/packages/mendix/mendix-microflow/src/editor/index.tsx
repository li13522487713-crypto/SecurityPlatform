import { useCallback, useEffect, useMemo, useRef, useState, type CSSProperties, type DragEvent, type KeyboardEvent, type PointerEvent, type ReactNode } from "react";
import { Badge, Button, Card, Dropdown, Empty, Input, Modal, Select, Space, Tabs, Tag, Toast, Tooltip, Typography } from "@douyinfe/semi-ui";
import {
  IconChevronDown,
  IconCopy,
  IconDelete,
  IconPlay,
  IconRefresh,
  IconSave,
  IconSetting,
  IconTickCircle,
  IconUndo,
  IconRedo,
  IconMore,
  IconChevronRight
} from "@douyinfe/semi-icons";
import { MicroflowNodePanel, type MicroflowNodePanelLabels } from "../node-panel";
import { MicroflowPropertyPanel, type MicroflowEdgePatch, type MicroflowNodePatch } from "../property-panel";
import {
  addMicroflowObjectFromDragPayload,
  createDragPayloadFromRegistryItem,
  createUniqueMicroflowObjectId,
  getMicroflowNodeRegistryKey,
  microflowNodeRegistryByKey,
  type MicroflowNodeDragPayload,
  type MicroflowNodeRegistryItem
} from "../node-registry";
import { createLocalMicroflowApiClient, type MicroflowApiClient, type MicroflowTraceFrame, type SaveMicroflowResponse, type TestRunMicroflowResponse, type ValidateMicroflowResponse } from "../runtime-adapter";
import {
  applyEditorGraphPatchToAuthoring,
  createAnnotationFlow,
  createObjectFromRegistry,
  createSequenceFlow,
  deleteFlow,
  deleteObject,
  duplicateObject,
  emptyVariableIndex,
  ensureAuthoringSchema,
  findObject,
  moveObject,
  refreshDerivedState,
  splitFlowWithObject,
  toEditorGraph,
  updateFlow,
  updateObject
} from "../adapters";
import { MicroflowHistoryManager, labelForHistoryReason, microflowSchemasEqual, type MicroflowHistoryReason, type MicroflowHistorySelection, type MicroflowHistoryState } from "../history";
import { applyAutoLayout } from "../layout";
import { canConnectPorts, inferEdgeKindFromPorts, type MicroflowEditorEdgeKind } from "../node-registry";
import { FlowGramMicroflowCanvas } from "../flowgram";
import {
  EMPTY_MICROFLOW_METADATA_CATALOG,
  MicroflowMetadataProvider,
  useMicroflowMetadataCatalog,
  type MicroflowMetadataAdapter,
  type MicroflowMetadataCatalog,
} from "../metadata";
import { validateMicroflowSchema } from "../schema/validator";
import { collectFlowsRecursive, findFlowWithCollection, findObjectWithCollection } from "../schema/utils/object-utils";
import { MicroflowTestRunModal, type MicroflowRunSession, type MicroflowRuntimeLog, type MicroflowTestRunInput } from "../debug";
import { useDebouncedMicroflowValidation, type MicroflowValidationAdapterLike, type MicroflowValidationMode } from "../performance";
import { useMicroflowShortcuts } from "./shortcuts";
import type {
  MicroflowCaseValue,
  MicroflowEditorEdge,
  MicroflowEditorGraph,
  MicroflowEditorGraphPatch,
  MicroflowEditorNode,
  MicroflowEditorPort,
  MicroflowFlow,
  MicroflowObject,
  MicroflowPoint,
  MicroflowSchema,
  MicroflowValidationIssue
} from "../schema/types";

const { Text, Title } = Typography;

const favoriteStorageKey = "atlas_microflow_node_panel_favorites";
const leftPanelStorageKey = "atlas_microflow_panel_left_open";
const rightPanelStorageKey = "atlas_microflow_panel_right_open";
const bottomPanelStorageKey = "atlas_microflow_panel_bottom_open";
const bottomTabStorageKey = "atlas_microflow_panel_bottom_tab";
const RAIL_WIDTH_PX = 44;
const LEFT_PANEL_EXPANDED_PX = 300;
const RIGHT_PANEL_EXPANDED_PX = 380;
const BOTTOM_STRIP_HEIGHT_PX = 40;
const BOTTOM_PANEL_EXPANDED_PX = 260;
const MOVE_HISTORY_DEBOUNCE_MS = 250;
const defaultFavoriteNodeKeys = ["activity:objectRetrieve", "activity:callRest", "activity:logMessage"];

type MicroflowSchemaChangeSource = "propertyPanel" | "flowgram" | "nodePanel" | "autolayout" | "history" | "runtime";

interface MicroflowApiErrorLike {
  code?: string;
  message?: string;
  details?: string;
  validationIssues?: MicroflowValidationIssue[];
  traceId?: string;
}

interface MicroflowSchemaChangeOptions {
  pushHistory?: boolean;
  historyLabel?: string;
  preserveSelection?: boolean;
  skipValidate?: boolean;
  skipDirty?: boolean;
  source?: MicroflowSchemaChangeSource;
}

function mapSchemaChangeReason(reason: string | MicroflowHistoryReason): MicroflowHistoryReason {
  switch (reason) {
    case "flowgramNodeMove":
      return "moveNode";
    case "flowgramLineAdd":
      return "addFlow";
    case "flowgramLineDelete":
      return "deleteFlow";
    case "updateParameter":
    case "updateParameterObject":
    case "propertyPanel":
      return "updateNodeProperty";
    default:
      return isMicroflowHistoryReason(reason) ? reason : "bulkUpdate";
  }
}

function isMicroflowHistoryReason(value: string): value is MicroflowHistoryReason {
  return [
    "init",
    "addNode",
    "deleteNode",
    "moveNode",
    "addFlow",
    "deleteFlow",
    "updateFlow",
    "updateFlowCase",
    "updateNodeProperty",
    "updateActionProperty",
    "updateEdgeProperty",
    "addLoopNode",
    "deleteLoopNode",
    "addLoopFlow",
    "deleteLoopFlow",
    "autoLayout",
    "bulkUpdate",
    "schemaMigration",
  ].includes(value);
}

function isEditableElement(target: EventTarget | null): boolean {
  return target instanceof HTMLElement && Boolean(target.closest("input, textarea, select, [contenteditable='true']"));
}

function getApiErrorLike(error: unknown): MicroflowApiErrorLike | undefined {
  if (typeof error !== "object" || error === null) {
    return undefined;
  }
  const maybe = error as { apiError?: MicroflowApiErrorLike };
  return maybe.apiError;
}

function getEditorApiErrorMessage(error: unknown): string {
  const apiError = getApiErrorLike(error);
  if (apiError?.message) {
    return apiError.traceId ? `${apiError.message} (Trace ${apiError.traceId})` : apiError.message;
  }
  return error instanceof Error ? error.message : String(error);
}

function applyApiValidationIssues(error: unknown, setIssues: (issues: MicroflowValidationIssue[]) => void, openProblems: () => void): boolean {
  const issues = getApiErrorLike(error)?.validationIssues;
  if (!issues?.length) {
    return false;
  }
  setIssues(issues);
  openProblems();
  return true;
}

function selectionExists(schema: MicroflowSchema, selection?: MicroflowHistorySelection): MicroflowHistorySelection {
  const objectId = selection?.objectId && findObjectWithCollection(schema, selection.objectId) ? selection.objectId : undefined;
  const flowId = !objectId && selection?.flowId && findFlowWithCollection(schema, selection.flowId) ? selection.flowId : undefined;
  const collectionId = objectId
    ? findObjectWithCollection(schema, objectId)?.collectionId
    : flowId
      ? findFlowWithCollection(schema, flowId)?.collectionId
      : selection?.collectionId;
  return { objectId, flowId, collectionId };
}

export interface MicroflowEditorProps {
  schema: MicroflowSchema;
  apiClient?: MicroflowApiClient;
  labels?: Partial<MicroflowEditorLabels>;
  toolbarPrefix?: ReactNode;
  toolbarSuffix?: ReactNode;
  nodePanelLabels?: Partial<MicroflowNodePanelLabels>;
  immersive?: boolean;
  /** 未写入 localStorage 时的初始值；默认随 `immersive` 为 true 时展开，否则收起 */
  defaultRightPanelOpen?: boolean;
  defaultBottomPanelOpen?: boolean;
  /** 是否把右/底面板开关持久化到 localStorage（默认 true） */
  persistAuxPanelState?: boolean;
  readonly?: boolean;
  onPublish?: (schema: MicroflowSchema) => Promise<void> | void;
  onSaveComplete?: (response: SaveMicroflowResponse) => void;
  onValidateComplete?: (response: ValidateMicroflowResponse) => void;
  onTestRunComplete?: (response: TestRunMicroflowResponse) => void;
  onSchemaChange?: (schema: MicroflowSchema) => void;
  /** 生产路径必须由宿主注入真实 metadata adapter；缺失时不回落 mock。 */
  metadataAdapter?: MicroflowMetadataAdapter;
  /** 同步注入目录时可跳过首次异步加载。 */
  metadataCatalog?: MicroflowMetadataCatalog;
  metadataWorkspaceId?: string;
  metadataModuleId?: string;
  /** http/local/mock 校验统一入口；http mode 下由宿主注入后端 ValidationAdapter。 */
  validationAdapter?: MicroflowValidationAdapterLike;
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

function createValidationServiceIssue(error: unknown, mode: MicroflowValidationMode): MicroflowValidationIssue {
  return {
    id: `MICROFLOW_VALIDATION_SERVICE_UNAVAILABLE:${mode}:${Date.now()}`,
    code: "MICROFLOW_VALIDATION_SERVICE_UNAVAILABLE",
    severity: mode === "edit" ? "warning" : "error",
    source: "root",
    fieldPath: "validation",
    message: error instanceof Error ? `校验服务不可用：${error.message}` : "校验服务不可用，请检查后端服务或网络。",
  };
}

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

function readStoredBoolean(key: string): boolean | undefined {
  if (typeof window === "undefined") {
    return undefined;
  }
  try {
    const raw = window.localStorage.getItem(key);
    if (raw === "true") {
      return true;
    }
    if (raw === "false") {
      return false;
    }
  } catch {
    /* ignore */
  }
  return undefined;
}

function writeStoredBoolean(key: string, value: boolean): void {
  if (typeof window === "undefined") {
    return;
  }
  try {
    window.localStorage.setItem(key, value ? "true" : "false");
  } catch {
    /* ignore */
  }
}

function readStoredBottomTab(): "problems" | "debug" | undefined {
  if (typeof window === "undefined") {
    return undefined;
  }
  const raw = window.localStorage.getItem(bottomTabStorageKey);
  return raw === "debug" || raw === "problems" ? raw : undefined;
}

function writeStoredBottomTab(value: "problems" | "debug"): void {
  if (typeof window === "undefined") {
    return;
  }
  try {
    window.localStorage.setItem(bottomTabStorageKey, value);
  } catch {
    /* ignore */
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

const toolbarStyle: CSSProperties = {
  display: "flex",
  alignItems: "center",
  justifyContent: "space-between",
  gap: 8,
  padding: "8px 12px",
  borderBottom: "1px solid var(--semi-color-border, #e5e6eb)",
  background: "var(--semi-color-bg-2, #fff)",
  minWidth: 0,
  overflow: "hidden"
};

const panelStyle: CSSProperties = {
  minHeight: 0,
  overflow: "auto",
  borderRight: "1px solid var(--semi-color-border, #e5e6eb)",
  background: "var(--semi-color-bg-1, #fff)",
  padding: 12,
  minWidth: 0
};

const propertyPaneStyle: CSSProperties = {
  flex: 1,
  minWidth: 0,
  minHeight: 0,
  overflow: "auto",
  borderLeft: "1px solid var(--semi-color-border, #e5e6eb)",
  background: "var(--semi-color-bg-1, #fff)",
  padding: 12
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

function nodeDepth(graph: MicroflowEditorGraph, node: MicroflowEditorNode): number {
  let depth = 0;
  let parentId = node.parentObjectId;
  while (parentId) {
    const parent = graph.nodes.find(item => item.objectId === parentId);
    parentId = parent?.parentObjectId;
    depth += 1;
  }
  return depth;
}

function findLoopAtPosition(graph: MicroflowEditorGraph, position: { x: number; y: number }): string | undefined {
  return graph.nodes
    .filter(node => {
    if (node.nodeKind !== "loopedActivity") {
      return false;
    }
    const width = Math.max(node.size.width, 360);
    const height = Math.max(node.size.height, 220);
    const left = node.position.x - width / 2;
    const top = node.position.y - height / 2;
    const headerHeight = 64;
    return position.x >= left && position.x <= left + width && position.y >= top + headerHeight && position.y <= top + height;
  })
    .sort((a, b) => nodeDepth(graph, b) - nodeDepth(graph, a))[0]?.objectId;
}

function collectVariables(schema: MicroflowSchema) {
  const variables = schema.variables ?? emptyVariableIndex();
  return Object.values(variables.parameters)
    .concat(Object.values(variables.localVariables))
    .concat(Object.values(variables.objectOutputs))
    .concat(Object.values(variables.listOutputs))
    .concat(Object.values(variables.loopVariables))
    .concat(Object.values(variables.errorVariables))
    .concat(Object.values(variables.systemVariables));
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
    const existing = collectFlowsRecursive(schema).filter(
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
    const existingCases = collectFlowsRecursive(schema)
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

function ProblemPanel({
  issues,
  status,
  lastValidatedAt,
  onSelect,
}: {
  issues: MicroflowValidationIssue[];
  status?: string;
  lastValidatedAt?: Date;
  onSelect: (issue: MicroflowValidationIssue) => void;
}) {
  const [severityFilter, setSeverityFilter] = useState<"all" | MicroflowValidationIssue["severity"]>("all");
  const [sourceFilter, setSourceFilter] = useState<string>("all");
  const [keyword, setKeyword] = useState("");
  const summary = useMemo(() => ({
    errors: issues.filter(issue => issue.severity === "error").length,
    warnings: issues.filter(issue => issue.severity === "warning").length,
    infos: issues.filter(issue => issue.severity === "info").length,
  }), [issues]);
  const sources = useMemo(() => [...new Set(issues.map(issue => issue.source).filter(Boolean))] as string[], [issues]);
  const filteredIssues = useMemo(() => {
    const normalizedKeyword = keyword.trim().toLowerCase();
    return issues.filter(issue => {
      const severityMatched = severityFilter === "all" || issue.severity === severityFilter;
      const sourceMatched = sourceFilter === "all" || issue.source === sourceFilter;
      const keywordMatched = !normalizedKeyword ||
        issue.code.toLowerCase().includes(normalizedKeyword) ||
        issue.message.toLowerCase().includes(normalizedKeyword) ||
        (issue.fieldPath ?? "").toLowerCase().includes(normalizedKeyword);
      return severityMatched && sourceMatched && keywordMatched;
    });
  }, [issues, keyword, severityFilter, sourceFilter]);

  return (
    <Space vertical align="start" spacing={10} style={{ width: "100%" }}>
      <Space style={{ width: "100%", justifyContent: "space-between", flexWrap: "wrap" }}>
        <Space>
          <Tag color={summary.errors > 0 ? "red" : "green"}>{summary.errors} errors</Tag>
          <Tag color={summary.warnings > 0 ? "orange" : "grey"}>{summary.warnings} warnings</Tag>
          <Tag color={summary.infos > 0 ? "blue" : "grey"}>{summary.infos} info</Tag>
          {status === "validating" ? <Tag color="blue">Validating...</Tag> : null}
          {lastValidatedAt ? <Tag color="grey">Last {lastValidatedAt.toLocaleTimeString()}</Tag> : null}
        </Space>
        <Space>
          <Input
            size="small"
            placeholder="Search code or message"
            value={keyword}
            onChange={setKeyword}
            style={{ width: 220 }}
          />
          <Select
            size="small"
            value={severityFilter}
            onChange={value => setSeverityFilter(value as "all" | MicroflowValidationIssue["severity"])}
            style={{ width: 120 }}
            optionList={[
              { label: "All", value: "all" },
              { label: "Errors", value: "error" },
              { label: "Warnings", value: "warning" },
              { label: "Info", value: "info" },
            ]}
          />
          <Select
            size="small"
            value={sourceFilter}
            onChange={value => setSourceFilter(String(value))}
            style={{ width: 150 }}
            optionList={[
              { label: "All sources", value: "all" },
              ...sources.map(source => ({ label: source, value: source })),
            ]}
          />
        </Space>
      </Space>
      {status === "validating" && issues.length === 0 ? <Empty title="Validating" description="Schema validation is running in the background." /> : null}
      {status !== "validating" && issues.length === 0 ? <Empty title="No problems" description="Schema validation passed." /> : null}
      {issues.length > 0 && filteredIssues.length === 0 ? <Empty title="No matching problems" description="Adjust filters to see validation issues." /> : null}
      {filteredIssues.map(issue => (
        <div key={issue.id} role="button" tabIndex={0} onClick={() => onSelect(issue)} onKeyDown={event => {
          if (event.key === "Enter" || event.key === " ") {
            event.preventDefault();
            onSelect(issue);
          }
        }}>
          <Card shadows="hover" style={{ width: "100%" }} bodyStyle={{ padding: 10 }}>
            <Space align="start" style={{ width: "100%", justifyContent: "space-between" }}>
              <div style={{ minWidth: 0 }}>
                <Text strong>{issue.code}</Text>
                <br />
                <Text size="small" type="secondary">{issue.message}</Text>
                <br />
                <Text size="small" type="tertiary">
                  {[issue.source, issue.objectId ?? issue.flowId ?? issue.actionId, issue.fieldPath].filter(Boolean).join(" · ")}
                </Text>
              </div>
              <Tag color={issue.severity === "error" ? "red" : issue.severity === "warning" ? "orange" : "blue"}>{issue.severity}</Tag>
            </Space>
          </Card>
        </div>
      ))}
    </Space>
  );
}

function DebugPanel({
  session,
  serviceError,
  activeFrameId,
  onSelectFrame,
  onSelectFlow,
  onSelectError,
  onClear,
  onRerun,
  onCancelRun,
}: {
  session?: MicroflowRunSession;
  serviceError?: string;
  activeFrameId?: string;
  onSelectFrame: (frame: MicroflowTraceFrame) => void;
  onSelectFlow: (flowId: string) => void;
  onSelectError: (error: NonNullable<MicroflowTraceFrame["error"]>) => void;
  onClear: () => void;
  onRerun: () => void;
  onCancelRun: () => void;
}) {
  const [variableKeyword, setVariableKeyword] = useState("");
  const [logObjectFilter, setLogObjectFilter] = useState("all");
  const activeFrame = activeFrameId ? session?.trace.find(frame => frame.id === activeFrameId) : session?.trace.at(-1);
  if (serviceError) {
    return <Empty title="运行服务不可用" description={serviceError} />;
  }
  if (!session || session.trace.length === 0) {
    return <Empty title="No trace" description="Run a test to see object/flow trace frames." />;
  }
  const errors = [session.error, ...session.trace.map(frame => frame.error)].filter(Boolean) as NonNullable<MicroflowTraceFrame["error"]>[];
  const objectIds = [...new Set(session.logs.map(item => item.objectId).filter(Boolean))] as string[];
  const logs = session.logs.filter(item => logObjectFilter === "all" || item.objectId === logObjectFilter);
  const variables = Object.values(activeFrame?.variablesSnapshot ?? {}).filter(variable =>
    !variableKeyword.trim() || variable.name.toLowerCase().includes(variableKeyword.trim().toLowerCase())
  );
  return (
    <Space vertical align="start" style={{ width: "100%" }}>
      <Space style={{ width: "100%", justifyContent: "space-between" }}>
        <Space>
          <Tag color={session.status === "success" ? "green" : "red"}>{session.status}</Tag>
          <Tag>{session.trace.length} frames</Tag>
          <Tag color={errors.length > 0 ? "red" : "grey"}>{errors.length} errors</Tag>
        </Space>
        <Space>
          <Button size="small" onClick={onRerun}>重新运行</Button>
          <Button size="small" type="warning" onClick={onCancelRun}>取消运行</Button>
          <Button size="small" type="danger" theme="borderless" onClick={onClear}>清空</Button>
        </Space>
      </Space>
      <Tabs type="line" style={{ width: "100%" }}>
        <Tabs.TabPane tab="执行轨迹" itemKey="trace">
          <Space vertical align="start" style={{ width: "100%" }}>
            {session.trace.map((frame, index) => (
              <Card
                key={frame.id}
                shadows="hover"
                style={{
                  width: "100%",
                  borderColor: frame.id === activeFrameId ? "var(--semi-color-primary)" : undefined,
                  opacity: frame.status === "skipped" ? 0.58 : 1,
                }}
                bodyStyle={{ padding: 10 }}
              >
                <Space align="start" style={{ width: "100%", justifyContent: "space-between" }}>
                  <button
                    type="button"
                    style={{ textAlign: "left", border: "none", background: "transparent", padding: 0, cursor: "pointer", flex: 1 }}
                    onClick={() => onSelectFrame(frame)}
                  >
                    <Text strong>{index + 1}. {frame.objectTitle ?? frame.objectId}</Text>
                    <br />
                    <Text size="small" type="tertiary">
                      {frame.objectId} · {frame.status} · {frame.durationMs}ms
                      {frame.selectedCaseValue ? ` · case ${caseValueLabel(frame.selectedCaseValue)}` : ""}
                      {frame.loopIteration ? ` · loop index ${frame.loopIteration.index}` : ""}
                    </Text>
                    {frame.error ? <><br /><Text size="small" type="danger">{frame.error.message}</Text></> : null}
                  </button>
                  <Space>
                    {frame.incomingFlowId ? <Button size="small" onClick={() => onSelectFlow(frame.incomingFlowId as string)}>in</Button> : null}
                    {frame.outgoingFlowId ? <Button size="small" onClick={() => onSelectFlow(frame.outgoingFlowId as string)}>out</Button> : null}
                    <Tag color={frame.status === "failed" ? "red" : frame.status === "success" ? "green" : frame.status === "running" ? "blue" : "grey"}>{frame.status}</Tag>
                  </Space>
                </Space>
              </Card>
            ))}
          </Space>
        </Tabs.TabPane>
        <Tabs.TabPane tab="输入 / 输出" itemKey="io">
          <Space vertical align="start" style={{ width: "100%" }}>
            <Text strong>Run</Text>
            <pre style={{ whiteSpace: "pre-wrap", margin: 0 }}>{JSON.stringify({ input: session.input, output: session.output }, null, 2)}</pre>
            {activeFrame ? (
              <>
                <Text strong>Active frame</Text>
                <pre style={{ whiteSpace: "pre-wrap", margin: 0 }}>{JSON.stringify({ input: activeFrame.input, output: activeFrame.output, error: activeFrame.error }, null, 2)}</pre>
              </>
            ) : null}
          </Space>
        </Tabs.TabPane>
        <Tabs.TabPane tab="变量" itemKey="variables">
          <Space vertical align="start" style={{ width: "100%" }}>
            <Input size="small" placeholder="搜索变量名" value={variableKeyword} onChange={setVariableKeyword} style={{ width: 220 }} />
            {variables.length === 0 ? <Empty title="No variables" /> : variables.map(variable => (
              <Card key={`${activeFrame?.id}-${variable.name}`} style={{ width: "100%" }} bodyStyle={{ padding: 8 }}>
                <Text strong>{variable.name}</Text>
                <br />
                <Space spacing={4} wrap>
                  <Tag size="small">{variable.type.kind}</Tag>
                  <Tag size="small" color="blue">{variable.source ?? "runtime"}</Tag>
                  {variable.scopeKind ? <Tag size="small" color="grey">{variable.scopeKind}</Tag> : null}
                  {variable.readonly ? <Tag size="small" color="orange">readonly</Tag> : null}
                </Space>
                <br />
                <Text size="small">{variable.valuePreview || "null"}</Text>
              </Card>
            ))}
          </Space>
        </Tabs.TabPane>
        <Tabs.TabPane tab="日志" itemKey="logs">
          <Space vertical align="start" style={{ width: "100%" }}>
            <Select
              size="small"
              value={logObjectFilter}
              onChange={value => setLogObjectFilter(String(value))}
              style={{ width: 220 }}
              optionList={[{ label: "All objects", value: "all" }, ...objectIds.map(id => ({ label: id, value: id }))]}
            />
            {logs.length === 0 ? <Empty title="No logs" /> : logs.map(item => (
              <LogRow key={item.id} log={item} onSelect={() => item.objectId ? onSelectFrame({ id: item.id, runId: session.id, objectId: item.objectId, actionId: item.actionId, status: "success", startedAt: item.timestamp, durationMs: 0 }) : undefined} />
            ))}
          </Space>
        </Tabs.TabPane>
        <Tabs.TabPane tab="错误" itemKey="errors">
          <Space vertical align="start" style={{ width: "100%" }}>
            {errors.length === 0 ? <Empty title="No errors" /> : errors.map(error => (
              <Card key={`${error.code}-${error.objectId ?? error.flowId ?? ""}`} style={{ width: "100%" }} bodyStyle={{ padding: 10 }}>
                <Space align="start" style={{ width: "100%", justifyContent: "space-between" }}>
                  <div>
                    <Text strong type="danger">{error.code}</Text>
                    <br />
                    <Text size="small">{error.message}</Text>
                    <br />
                    <Text size="small" type="tertiary">{[error.objectId, error.actionId, error.flowId].filter(Boolean).join(" · ")}</Text>
                  </div>
                  <Button size="small" onClick={() => onSelectError(error)}>定位</Button>
                </Space>
              </Card>
            ))}
          </Space>
        </Tabs.TabPane>
      </Tabs>
    </Space>
  );
}

function LogRow({ log, onSelect }: { log: MicroflowRuntimeLog; onSelect: () => void }) {
  const color = log.level === "error" || log.level === "critical" ? "red" : log.level === "warning" ? "orange" : log.level === "info" ? "blue" : "grey";
  const structuredFields = log.structuredFieldsJson ? tryParseJson(log.structuredFieldsJson) : undefined;
  return (
    <Card style={{ width: "100%" }} bodyStyle={{ padding: 8 }}>
      <button type="button" onClick={onSelect} style={{ textAlign: "left", border: "none", background: "transparent", padding: 0, cursor: "pointer", width: "100%" }}>
        <Space wrap>
          <Tag color={color}>{log.level}</Tag>
          {log.logNodeName ? <Tag color="violet">{log.logNodeName}</Tag> : null}
          {log.traceId ? <Tag color="grey">{log.traceId}</Tag> : null}
          <Text size="small" type="tertiary">{log.objectId}</Text>
        </Space>
        <br />
        <Text size="small">{log.message}</Text>
        {structuredFields ? (
          <>
            <br />
            <Text size="small" type="tertiary">{JSON.stringify(structuredFields)}</Text>
          </>
        ) : null}
      </button>
    </Card>
  );
}

function tryParseJson(value: string): unknown | undefined {
  try {
    return JSON.parse(value);
  } catch {
    return undefined;
  }
}

function caseValueLabel(value: MicroflowCaseValue): string {
  if (value.kind === "boolean") {
    return String(value.value);
  }
  if (value.kind === "enumeration") {
    return value.value;
  }
  if (value.kind === "inheritance") {
    return value.entityQualifiedName;
  }
  return value.kind;
}

function MicroflowEditorInner(props: MicroflowEditorProps) {
  const labels = { ...defaultLabels, ...props.labels };
  const loadedMetadata = useMicroflowMetadataCatalog();
  const metadataForRefresh = loadedMetadata ?? props.metadataCatalog ?? EMPTY_MICROFLOW_METADATA_CATALOG;
  const apiClient = props.apiClient ?? createLocalMicroflowApiClient();
  const persistAuxPanelState = props.persistAuxPanelState !== false;
  const rightPanelFallback = props.defaultRightPanelOpen ?? (props.immersive === true);
  const bottomPanelFallback = props.defaultBottomPanelOpen ?? (props.immersive === true);

  const [schema, setSchema] = useState<MicroflowSchema>(() =>
    refreshDerivedState(ensureAuthoringSchema(props.schema), props.metadataCatalog ?? EMPTY_MICROFLOW_METADATA_CATALOG),
  );
  const historyManagerRef = useRef<MicroflowHistoryManager | null>(null);
  if (!historyManagerRef.current) {
    historyManagerRef.current = new MicroflowHistoryManager();
    historyManagerRef.current.init(schema);
  }
  const historyManager = historyManagerRef.current;
  const [historyState, setHistoryState] = useState<MicroflowHistoryState>(() => historyManager.getState());
  const savedSchemaRef = useRef<MicroflowSchema>(schema);
  const latestSchemaRef = useRef<MicroflowSchema>(schema);
  const moveHistoryTimerRef = useRef<number | undefined>();
  const pendingMoveSchemaRef = useRef<MicroflowSchema | undefined>();
  const shellRef = useRef<HTMLDivElement>(null);
  const [favoriteNodeKeys, setFavoriteNodeKeys] = useState(readFavoriteNodeKeys);
  const [validationTrigger, setValidationTrigger] = useState(0);
  const {
    issues,
    setIssues,
    validationStatus,
    lastValidatedAt,
    runValidationNow,
  } = useDebouncedMicroflowValidation({
    schema,
    metadata: loadedMetadata,
    trigger: validationTrigger,
    initialIssues: schema.validation.issues ?? [],
    validationAdapter: props.validationAdapter,
    resourceId: schema.id,
  });
  const [traceFrames, setTraceFrames] = useState<MicroflowTraceFrame[]>([]);
  const [runSession, setRunSession] = useState<MicroflowRunSession>();
  const [runtimeServiceError, setRuntimeServiceError] = useState<string>();
  const [activeTraceFrameId, setActiveTraceFrameId] = useState<string>();
  const [testRunModalOpen, setTestRunModalOpen] = useState(false);
  const [saving, setSaving] = useState(false);
  const [running, setRunning] = useState(false);
  const [dirty, setDirty] = useState(false);
  const [leftOpen, setLeftOpen] = useState(() => {
    if (!persistAuxPanelState) {
      return true;
    }
    const stored = readStoredBoolean(leftPanelStorageKey);
    return stored !== undefined ? stored : true;
  });
  const [rightOpen, setRightOpen] = useState(() => {
    if (!persistAuxPanelState) {
      return rightPanelFallback;
    }
    const stored = readStoredBoolean(rightPanelStorageKey);
    return stored !== undefined ? stored : rightPanelFallback;
  });
  const [bottomOpen, setBottomOpen] = useState(() => {
    if (!persistAuxPanelState) {
      return bottomPanelFallback;
    }
    const stored = readStoredBoolean(bottomPanelStorageKey);
    return stored !== undefined ? stored : bottomPanelFallback;
  });
  const [bottomTab, setBottomTab] = useState<"problems" | "debug">(() => readStoredBottomTab() ?? "problems");

  const validateForMode = useCallback(async (targetSchema: MicroflowSchema, mode: MicroflowValidationMode) => {
    try {
      const result = props.validationAdapter
        ? await props.validationAdapter.validate({
            resourceId: targetSchema.id,
            schema: targetSchema,
            metadata: loadedMetadata,
            mode,
            includeWarnings: true,
            includeInfo: true,
          })
        : validateMicroflowSchema({
            schema: targetSchema,
            metadata: loadedMetadata,
            options: { mode, includeWarnings: true, includeInfo: true },
          });
      setIssues(result.issues);
      return result;
    } catch (error) {
      const issue = createValidationServiceIssue(error, mode);
      setIssues([issue]);
      return {
        issues: [issue],
        summary: {
          errorCount: issue.severity === "error" ? 1 : 0,
          warningCount: issue.severity === "warning" ? 1 : 0,
          infoCount: 0,
        },
      };
    }
  }, [loadedMetadata, props.validationAdapter, setIssues]);

  const shellStyle = useMemo((): CSSProperties => ({
    display: "grid",
    gridTemplateRows: bottomOpen
      ? `60px minmax(0, 1fr) ${BOTTOM_PANEL_EXPANDED_PX}px`
      : `60px minmax(0, 1fr) ${BOTTOM_STRIP_HEIGHT_PX}px`,
    height: "100%",
    minHeight: 0,
    background: "var(--semi-color-bg-0, #f7f8fa)"
  }), [bottomOpen]);

  const bodyStyle = useMemo((): CSSProperties => {
    const leftCol = leftOpen ? LEFT_PANEL_EXPANDED_PX : RAIL_WIDTH_PX;
    const rightCol = rightOpen ? RIGHT_PANEL_EXPANDED_PX : RAIL_WIDTH_PX;
    return {
      display: "grid",
      gridTemplateColumns: `${leftCol}px minmax(320px, 1fr) ${rightCol}px`,
      minHeight: 0,
      minWidth: 0,
      overflow: "hidden"
    };
  }, [leftOpen, rightOpen]);

  const graph = useMemo(() => toEditorGraph({ ...schema, validation: { issues } }), [schema, issues]);
  const selectedObject = schema.editor.selection.objectId ? findObject(schema, schema.editor.selection.objectId) ?? null : null;
  const selectedFlow = schema.editor.selection.flowId ? findFlowWithCollection(schema, schema.editor.selection.flowId)?.flow ?? null : null;

  const refreshHistoryState = () => setHistoryState(historyManager.getState());

  const flushPendingMoveHistory = () => {
    if (moveHistoryTimerRef.current !== undefined) {
      window.clearTimeout(moveHistoryTimerRef.current);
      moveHistoryTimerRef.current = undefined;
    }
    const pendingSchema = pendingMoveSchemaRef.current;
    if (!pendingSchema) {
      return;
    }
    pendingMoveSchemaRef.current = undefined;
    historyManager.push(pendingSchema, "moveNode", labelForHistoryReason("moveNode"));
    refreshHistoryState();
  };

  const clearRuntimeState = () => {
    setRunSession(undefined);
    setTraceFrames([]);
    setActiveTraceFrameId(undefined);
  };

  const commitSchema = (
    next: MicroflowSchema,
    reason: MicroflowHistoryReason | string = "bulkUpdate",
    options: MicroflowSchemaChangeOptions = {},
  ) => {
    const historyReason = mapSchemaChangeReason(reason);
    const source = options.source;
    if (historyReason !== "moveNode") {
      flushPendingMoveHistory();
    }
    const nextWithSelection = options.preserveSelection
      ? {
          ...next,
          editor: {
            ...next.editor,
            selection: schema.editor.selection,
            selectedObjectId: schema.editor.selectedObjectId,
            selectedFlowId: schema.editor.selectedFlowId,
            selectedCollectionId: schema.editor.selectedCollectionId,
          },
        }
      : next;
    const refreshed = refreshDerivedState(nextWithSelection, metadataForRefresh);
    const shouldPushHistory = options.pushHistory !== false && source !== "history" && !historyManager.getState().isRestoring;

    if (shouldPushHistory) {
      if (historyReason === "moveNode") {
        pendingMoveSchemaRef.current = refreshed;
        if (moveHistoryTimerRef.current !== undefined) {
          window.clearTimeout(moveHistoryTimerRef.current);
        }
        moveHistoryTimerRef.current = window.setTimeout(() => {
          moveHistoryTimerRef.current = undefined;
          const pendingSchema = pendingMoveSchemaRef.current;
          pendingMoveSchemaRef.current = undefined;
          if (pendingSchema) {
            historyManager.push(pendingSchema, "moveNode", options.historyLabel ?? labelForHistoryReason("moveNode"));
            refreshHistoryState();
          }
        }, MOVE_HISTORY_DEBOUNCE_MS);
      } else {
        historyManager.push(refreshed, historyReason, options.historyLabel ?? labelForHistoryReason(historyReason));
        refreshHistoryState();
      }
    }

    setSchema(refreshed);
    latestSchemaRef.current = refreshed;
    if (!options.skipValidate) {
      setValidationTrigger(value => value + 1);
    }
    if (!options.skipDirty) {
      setDirty(!microflowSchemasEqual(refreshed, savedSchemaRef.current));
    }
    if (!options.skipDirty && source !== "runtime") {
      clearRuntimeState();
    }
    props.onSchemaChange?.(refreshed);
  };

  const applyPatch = (
    patch: Parameters<typeof applyEditorGraphPatchToAuthoring>[1],
    options: MicroflowSchemaChangeOptions & { reason?: MicroflowHistoryReason | string } = {},
  ) => {
    commitSchema(applyEditorGraphPatchToAuthoring(schema, patch), options.reason ?? "bulkUpdate", options);
  };

  const quickAddPosition = (): MicroflowPoint => {
    const viewport = schema.editor.viewport ?? { x: 0, y: 0, zoom: 1 };
    const indexOffset = graph.nodes.length * 18;
    return {
      x: Math.round(((360 - viewport.x) / Math.max(0.2, viewport.zoom) + indexOffset) / 16) * 16,
      y: Math.round(((220 - viewport.y) / Math.max(0.2, viewport.zoom) + indexOffset / 2) / 16) * 16
    };
  };

  const handleAddNode = (
    item: MicroflowNodeRegistryItem,
    options?: { source?: "doubleClick" | "contextMenu" | "drop"; position?: { x: number; y: number }; insertFlowId?: string; payload?: MicroflowNodeDragPayload }
  ) => {
    const position = options?.position ?? quickAddPosition();
    const parentLoopObjectId = findLoopAtPosition(graph, position);
    const parentLoopNode = parentLoopObjectId ? graph.nodes.find(node => node.objectId === parentLoopObjectId) : undefined;
    const authoringPosition = parentLoopNode
      ? {
          x: Math.max(24, position.x - parentLoopNode.position.x),
          y: Math.max(24, position.y - parentLoopNode.position.y - 76),
        }
      : position;
    const eventType = String(item.type) === "event" ? (item.defaultConfig as { eventType?: string }).eventType : undefined;
    if (eventType && ["start", "end"].includes(eventType) && parentLoopObjectId) {
      Toast.warning("Start / End events cannot be placed inside Loop.");
      return;
    }
    if (eventType && ["break", "continue"].includes(eventType) && !parentLoopObjectId) {
      Toast.warning("Break / Continue can only be placed inside Loop.");
      return;
    }
    const payload = options?.payload ?? createDragPayloadFromRegistryItem(item);
    if (options?.insertFlowId) {
      const object = createObjectFromRegistry(
        item,
        authoringPosition,
        createUniqueMicroflowObjectId(schema, getMicroflowNodeRegistryKey(item))
      );
      const flow = collectFlowsRecursive(schema).find(item => item.id === options.insertFlowId);
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
      }, parentLoopObjectId ? "addLoopNode" : "addNode", { source: "nodePanel" });
      return;
    }
    const result = addMicroflowObjectFromDragPayload({ schema, payload, position: authoringPosition, parentLoopObjectId });
    if (result.blockedReason) {
      Toast.warning(result.blockedReason);
      return;
    }
    commitSchema(result.schema, parentLoopObjectId ? "addLoopNode" : "addNode", { source: "nodePanel" });
    for (const warning of result.warnings) {
      Toast.warning(warning);
    }
  };

  const handleSave = async () => {
    const validation = await validateForMode(schema, "save");
    if (validation.summary.errorCount > 0) {
      setBottomOpen(true);
      setBottomTab("problems");
      Toast.error(`Save blocked by ${validation.summary.errorCount} validation error(s).`);
      return;
    }
    setSaving(true);
    try {
      const response = await apiClient.saveMicroflow({ schema });
      props.onSaveComplete?.(response);
      savedSchemaRef.current = schema;
      historyManager.replaceCurrent(schema, "bulkUpdate");
      refreshHistoryState();
      setDirty(false);
      Toast.success(`Saved ${response.version}`);
    } catch (error) {
      applyApiValidationIssues(error, setIssues, () => {
        setBottomOpen(true);
        setBottomTab("problems");
      });
      Toast.error(getEditorApiErrorMessage(error));
    } finally {
      setSaving(false);
    }
  };

  const handleValidate = async () => {
    try {
      const issues = await runValidationNow(schema);
      const response: ValidateMicroflowResponse = {
        valid: issues.every(issue => issue.severity !== "error"),
        issues,
      };
      props.onValidateComplete?.(response);
      Toast[response.valid ? "success" : "warning"](response.valid ? "Validation passed" : `${response.issues.length} issue(s)`);
    } catch (error) {
      applyApiValidationIssues(error, setIssues, () => {
        setBottomOpen(true);
        setBottomTab("problems");
      });
      Toast.error(getEditorApiErrorMessage(error));
    }
  };

  const handleTestRun = async () => {
    const validation = await validateForMode(schema, "testRun");
    if (validation.summary.errorCount > 0) {
      setBottomOpen(true);
      setBottomTab("problems");
      Toast.error(`Test run blocked by ${validation.summary.errorCount} validation error(s).`);
      return;
    }
    if (validation.summary.warningCount > 0) {
      Toast.warning(`Test run allowed with ${validation.summary.warningCount} warning(s).`);
    }
    setTestRunModalOpen(true);
  };

  const handleExecuteTestRun = async (input: MicroflowTestRunInput) => {
    setRunning(true);
    try {
      const response = await apiClient.testRunMicroflow({ microflowId: schema.id, input: input.parameters, options: input.options, schema });
      const persistedSession = await apiClient.getMicroflowRunSession(response.runId);
      const persistedTrace = await apiClient.getMicroflowRunTrace(response.runId);
      const session = { ...persistedSession, trace: persistedTrace };
      setRuntimeServiceError(undefined);
      setRunSession(session);
      setTraceFrames(persistedTrace);
      setActiveTraceFrameId(persistedTrace[0]?.id);
      setTestRunModalOpen(false);
      setBottomOpen(true);
      setBottomTab("debug");
      props.onTestRunComplete?.({ ...response, session, frames: persistedTrace });
      Toast[response.status === "succeeded" ? "success" : "error"](response.status);
    } catch (error) {
      applyApiValidationIssues(error, setIssues, () => {
        setBottomOpen(true);
        setBottomTab("problems");
      });
      setRunSession(undefined);
      setTraceFrames([]);
      setActiveTraceFrameId(undefined);
      setRuntimeServiceError(getEditorApiErrorMessage(error));
      setBottomOpen(true);
      setBottomTab("debug");
      Toast.error(getEditorApiErrorMessage(error));
    } finally {
      setRunning(false);
    }
  };

  const clearTestRun = () => {
    setRunSession(undefined);
    setRuntimeServiceError(undefined);
    setTraceFrames([]);
    setActiveTraceFrameId(undefined);
  };

  const cancelTestRun = async () => {
    if (!runSession?.id) {
      return;
    }
    setRunning(true);
    try {
      const cancelResult = await apiClient.cancelMicroflowRun(runSession.id);
      const nextSession = await apiClient.getMicroflowRunSession(runSession.id);
      const nextTrace = await apiClient.getMicroflowRunTrace(runSession.id);
      const session = { ...nextSession, trace: nextTrace };
      setRuntimeServiceError(undefined);
      setRunSession(session);
      setTraceFrames(nextTrace);
      setActiveTraceFrameId(current => current ?? nextTrace[0]?.id);
      Toast.info(`Run ${cancelResult?.status ?? session.status}`);
    } catch (error) {
      setRuntimeServiceError(getEditorApiErrorMessage(error));
      setBottomOpen(true);
      setBottomTab("debug");
      Toast.error(getEditorApiErrorMessage(error));
    } finally {
      setRunning(false);
    }
  };

  const selectTraceFrame = (frame: MicroflowTraceFrame) => {
    setActiveTraceFrameId(frame.id);
    setRightOpen(true);
    applyPatch({
      selectedObjectId: frame.objectId,
      selectedFlowId: undefined,
      selectedCollectionId: frame.collectionId ?? findObjectWithCollection(schema, frame.objectId)?.collectionId,
    }, { pushHistory: false, skipDirty: true, skipValidate: true, source: "runtime" });
  };

  const selectTraceFlow = (flowId: string) => {
    setRightOpen(true);
    const located = findFlowWithCollection(schema, flowId);
    applyPatch({
      selectedObjectId: undefined,
      selectedFlowId: flowId,
      selectedCollectionId: located?.collectionId,
    }, { pushHistory: false, skipDirty: true, skipValidate: true, source: "runtime" });
  };

  const selectTraceError = (error: NonNullable<MicroflowTraceFrame["error"]>) => {
    if (error.flowId) {
      selectTraceFlow(error.flowId);
      return;
    }
    if (error.objectId) {
      const frame = runSession?.trace.find(item => item.objectId === error.objectId);
      if (frame) {
        selectTraceFrame(frame);
      } else {
        setRightOpen(true);
        applyPatch(
          { selectedObjectId: error.objectId, selectedFlowId: undefined, selectedCollectionId: findObjectWithCollection(schema, error.objectId)?.collectionId },
          { pushHistory: false, skipDirty: true, skipValidate: true, source: "runtime" },
        );
      }
    }
  };

  const handleUndo = () => {
    flushPendingMoveHistory();
    const restored = historyManager.undo();
    if (!restored) {
      return;
    }
    const nextSchema = refreshDerivedState({
      ...restored.schema,
      editor: {
        ...restored.schema.editor,
        selection: selectionExists(restored.schema as MicroflowSchema, restored.selection),
      },
    } as MicroflowSchema, metadataForRefresh);
    commitSchema(nextSchema, restored.snapshot.reason, { pushHistory: false, source: "history" });
    historyManager.finishRestoring();
    refreshHistoryState();
  };

  const handleRedo = () => {
    flushPendingMoveHistory();
    const restored = historyManager.redo();
    if (!restored) {
      return;
    }
    const nextSchema = refreshDerivedState({
      ...restored.schema,
      editor: {
        ...restored.schema.editor,
        selection: selectionExists(restored.schema as MicroflowSchema, restored.selection),
      },
    } as MicroflowSchema, metadataForRefresh);
    commitSchema(nextSchema, restored.snapshot.reason, { pushHistory: false, source: "history" });
    historyManager.finishRestoring();
    refreshHistoryState();
  };

  const handleAutoLayout = () => {
    const result = applyAutoLayout({ schema, options: { direction: "LR", fitViewAfterLayout: true } });
    if (result.changedObjectIds.length === 0) {
      Toast.warning("No nodes to layout.");
      return;
    }
    commitSchema(result.nextSchema, "autoLayout", { source: "autolayout" });
    Toast.success("Auto layout applied.");
  };

  const handleDeleteSelection = () => {
    if (props.readonly) {
      return;
    }
    const selection = schema.editor.selection;
    if (selection.flowId) {
      const located = findFlowWithCollection(schema, selection.flowId);
      commitSchema(deleteFlow(schema, selection.flowId), located?.parentLoopObjectId ? "deleteLoopFlow" : "deleteFlow");
      return;
    }
    if (selection.objectId) {
      const located = findObjectWithCollection(schema, selection.objectId);
      commitSchema(deleteObject(schema, selection.objectId), located?.parentLoopObjectId ? "deleteLoopNode" : "deleteNode");
    }
  };

  const handleEditorKeyDown = (event: KeyboardEvent<HTMLDivElement>) => {
    if (isEditableElement(event.target)) {
      return;
    }
    const key = event.key.toLowerCase();
    if ((event.ctrlKey || event.metaKey) && key === "z" && !event.shiftKey) {
      event.preventDefault();
      handleUndo();
      return;
    }
    if ((event.ctrlKey || event.metaKey) && (key === "y" || (key === "z" && event.shiftKey))) {
      event.preventDefault();
      handleRedo();
      return;
    }
    if ((event.ctrlKey || event.metaKey) && key === "s") {
      event.preventDefault();
      void handleSave();
      return;
    }
    if ((key === "delete" || key === "backspace") && !props.readonly) {
      event.preventDefault();
      handleDeleteSelection();
      return;
    }
    if (key === "escape") {
      applyPatch(
        { selectedObjectId: undefined, selectedFlowId: undefined, selectedCollectionId: undefined },
        { pushHistory: false, skipDirty: true, skipValidate: true },
      );
    }
  };

  useEffect(() => () => {
    if (moveHistoryTimerRef.current !== undefined) {
      window.clearTimeout(moveHistoryTimerRef.current);
    }
  }, []);

  useEffect(() => {
    if (!persistAuxPanelState) {
      return;
    }
    writeStoredBoolean(leftPanelStorageKey, leftOpen);
    writeStoredBoolean(rightPanelStorageKey, rightOpen);
    writeStoredBoolean(bottomPanelStorageKey, bottomOpen);
    writeStoredBottomTab(bottomTab);
  }, [leftOpen, rightOpen, bottomOpen, bottomTab, persistAuxPanelState]);

  const rightRailStyle: CSSProperties = {
    width: RAIL_WIDTH_PX,
    flexShrink: 0,
    display: "flex",
    flexDirection: "column",
    alignItems: "center",
    paddingTop: 10,
    gap: 6,
    borderLeft: "1px solid var(--semi-color-border, #e5e6eb)",
    background: "var(--semi-color-bg-2, #fff)",
    cursor: "pointer",
    userSelect: "none",
    color: "var(--semi-color-text-1, rgba(28, 31, 35, 0.8))"
  };

  const focusNodeSearch = useCallback(() => {
    setLeftOpen(true);
    window.setTimeout(() => {
      const input = shellRef.current?.querySelector<HTMLInputElement>(".microflow-node-search-input input, input.microflow-node-search-input");
      input?.focus();
      input?.select();
    }, 0);
  }, []);

  const clearSelection = useCallback(() => {
    applyPatch(
      { selectedObjectId: undefined, selectedFlowId: undefined, selectedCollectionId: undefined },
      { pushHistory: false, skipDirty: true, skipValidate: true },
    );
  }, [schema]);

  useMicroflowShortcuts({
    containerRef: shellRef,
    readonly: props.readonly,
    onUndo: handleUndo,
    onRedo: handleRedo,
    onSave: () => void handleSave(),
    onSearch: focusNodeSearch,
    onDeleteSelection: handleDeleteSelection,
    onEscape: clearSelection,
  });

  return (
    <div ref={shellRef} style={shellStyle} tabIndex={0}>
      <div style={toolbarStyle}>
        <Space style={{ minWidth: 0, overflow: "hidden" }}>
          {props.toolbarPrefix}
          <Title heading={5} style={{ margin: 0 }}>{schema.displayName || schema.name}</Title>
          <Tag>{schema.schemaVersion}</Tag>
          {dirty ? <Tag color="orange">dirty</Tag> : null}
          <Tag color={validationStatus === "validating" ? "blue" : issues.some(issue => issue.severity === "error") ? "red" : "green"}>
            {validationStatus === "validating" ? "validating..." : `${issues.length} issues`}
          </Tag>
          {runSession ? <Tag color={runSession.status === "success" ? "green" : "red"}>{runSession.status} · {runSession.trace.length} frames</Tag> : null}
        </Space>
        <Space wrap style={{ justifyContent: "flex-end", rowGap: 4 }}>
          <Tooltip content={historyState.canUndo ? labels.undo : "No history to undo"}>
            <Button aria-label={labels.undo} icon={<IconUndo />} disabled={!historyState.canUndo} onClick={handleUndo} />
          </Tooltip>
          <Tooltip content={historyState.canRedo ? labels.redo : "No history to redo"}>
            <Button aria-label={labels.redo} icon={<IconRedo />} disabled={!historyState.canRedo} onClick={handleRedo} />
          </Tooltip>
          <Tooltip content={labels.validate}>
            <Button aria-label={labels.validate} icon={<IconRefresh />} loading={validationStatus === "validating"} onClick={handleValidate}>{labels.validate}</Button>
          </Tooltip>
          <Tooltip content={labels.testRun}>
            <Button aria-label={labels.testRun} icon={<IconPlay />} loading={running} onClick={handleTestRun}>{labels.testRun}</Button>
          </Tooltip>
          <Tooltip content={dirty ? labels.save : "No unsaved changes"}>
            <Button aria-label={labels.save} icon={<IconSave />} loading={saving} type="primary" onClick={handleSave}>{labels.save}</Button>
          </Tooltip>
          <Dropdown
            trigger="click"
            position="bottomRight"
            render={(
              <Dropdown.Menu>
                <Dropdown.Item icon={<IconDelete />} disabled={props.readonly || !schema.editor.selection.objectId && !schema.editor.selection.flowId} onClick={handleDeleteSelection}>删除选择</Dropdown.Item>
                <Dropdown.Item icon={<IconRefresh />} onClick={handleAutoLayout}>{labels.format}</Dropdown.Item>
                <Dropdown.Item onClick={focusNodeSearch}>搜索节点</Dropdown.Item>
                <Dropdown.Item onClick={() => setLeftOpen(open => !open)}>{leftOpen ? "折叠节点面板" : "展开节点面板"}</Dropdown.Item>
                <Dropdown.Item onClick={() => setRightOpen(open => !open)}>{rightOpen ? "折叠属性面板" : "展开属性面板"}</Dropdown.Item>
                <Dropdown.Item onClick={() => setBottomOpen(open => !open)}>{bottomOpen ? "折叠底部面板" : "展开底部面板"}</Dropdown.Item>
                {runSession ? <Dropdown.Item type="danger" icon={<IconDelete />} onClick={clearTestRun}>清空调试</Dropdown.Item> : null}
              </Dropdown.Menu>
            )}
          >
            <Button aria-label={labels.more} icon={<IconMore />} theme="borderless">{labels.more}</Button>
          </Dropdown>
          {props.toolbarSuffix}
        </Space>
      </div>
      <div style={bodyStyle}>
        {leftOpen ? (
          <div style={panelStyle}>
            <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 8, marginBottom: 8 }}>
              <Text strong>{labels.nodePanel}</Text>
              <Tooltip content="折叠节点面板">
                <Button aria-label="折叠节点面板" size="small" theme="borderless" icon={<IconChevronDown style={{ transform: "rotate(90deg)" }} />} onClick={() => setLeftOpen(false)} />
              </Tooltip>
            </div>
            <MicroflowNodePanel
              favoriteNodeKeys={favoriteNodeKeys}
              onFavoriteChange={keys => {
                setFavoriteNodeKeys(keys);
                saveFavoriteNodeKeys(keys);
              }}
              onAddNode={(item, options) => handleAddNode(item, options)}
              onShowDocumentation={item => Modal.info({ title: item.title, content: item.documentation.summary })}
              labels={props.nodePanelLabels}
              createContext={{ microflowId: schema.id, moduleId: schema.moduleId, metadataAvailable: Boolean(loadedMetadata) }}
            />
          </div>
        ) : (
          <button
            type="button"
            aria-label="展开节点面板"
            title={labels.nodePanel}
            style={{
              border: 0,
              borderRight: "1px solid var(--semi-color-border, #e5e6eb)",
              background: "var(--semi-color-bg-2, #fff)",
              display: "flex",
              flexDirection: "column",
              alignItems: "center",
              justifyContent: "flex-start",
              gap: 8,
              padding: "12px 0",
              cursor: "pointer",
              color: "var(--semi-color-text-1, rgba(28, 31, 35, 0.8))"
            }}
            onClick={() => setLeftOpen(true)}
          >
            <IconChevronRight />
            <Text size="small" strong style={{ writingMode: "vertical-rl", textOrientation: "mixed", letterSpacing: 1 }}>{labels.nodePanel}</Text>
          </button>
        )}
        <FlowGramMicroflowCanvas
          schema={schema}
          validationIssues={issues}
          runtimeTrace={traceFrames}
          readonly={props.readonly}
          onSchemaChange={(nextSchema, reason) => {
            commitSchema(nextSchema, reason, { source: "flowgram" });
          }}
          onSelectionChange={selection => {
            applyPatch({
              selectedObjectId: selection.objectId,
              selectedFlowId: selection.flowId,
              selectedCollectionId: selection.collectionId
            }, { pushHistory: false, skipDirty: true, skipValidate: true, source: "flowgram" });
          }}
          onDropRegistryItem={(item, position, payload) => handleAddNode(item, { position, payload, source: "drop" })}
          canUndo={historyState.canUndo}
          canRedo={historyState.canRedo}
          onUndo={handleUndo}
          onRedo={handleRedo}
          onAutoLayout={handleAutoLayout}
          onViewportChange={viewport => {
            commitSchema(
              { ...schema, editor: { ...schema.editor, viewport, zoom: viewport.zoom } },
              "bulkUpdate",
              { pushHistory: false, skipDirty: true, skipValidate: true, preserveSelection: true, source: "flowgram" },
            );
          }}
          onToggleMiniMap={visible => {
            commitSchema(
              { ...schema, editor: { ...schema.editor, showMiniMap: visible } },
              "bulkUpdate",
              { historyLabel: "Toggle minimap", skipValidate: true, preserveSelection: true, source: "flowgram" },
            );
          }}
          onToggleGrid={enabled => {
            commitSchema(
              { ...schema, editor: { ...schema.editor, gridEnabled: enabled } },
              "bulkUpdate",
              { historyLabel: "Toggle grid", skipValidate: true, preserveSelection: true, source: "flowgram" },
            );
          }}
        />
        <div
          style={{
            display: "flex",
            flexDirection: "row",
            height: "100%",
            minHeight: 0,
            overflow: "hidden",
            background: "var(--semi-color-bg-1, #fff)"
          }}
        >
          {rightOpen ? (
            <div style={propertyPaneStyle}>
              <MicroflowPropertyPanel
                  selectedObject={selectedObject}
                  selectedFlow={selectedFlow}
                  schema={schema}
                  validationIssues={issues}
                  traceFrames={traceFrames}
                  onSchemaChange={(nextSchema, reason) => commitSchema(nextSchema, reason, { source: "propertyPanel" })}
                  onObjectChange={(objectId, patch: MicroflowNodePatch) => {
                    if (!patch.object) {
                      return;
                    }
                    const object = patch.object as MicroflowObject;
                    const reason = object.kind === "actionActivity" ? "updateActionProperty" : "updateNodeProperty";
                    commitSchema(updateObject(schema, objectId, () => object), reason, { source: "propertyPanel" });
                  }}
                  onFlowChange={(flowId, patch: MicroflowEdgePatch) => {
                    const located = findFlowWithCollection(schema, flowId);
                    const reason = "caseValues" in patch ? "updateFlowCase" : "updateEdgeProperty";
                    commitSchema(updateFlow(schema, flowId, flow => {
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
                    }), located?.parentLoopObjectId ? "updateFlow" : reason, { source: "propertyPanel" });
                  }}
                  onDuplicateObject={objectId => {
                    const located = findObjectWithCollection(schema, objectId);
                    commitSchema(duplicateObject(schema, objectId), located?.parentLoopObjectId ? "addLoopNode" : "addNode", { source: "propertyPanel" });
                  }}
                  onDeleteObject={objectId => {
                    const located = findObjectWithCollection(schema, objectId);
                    commitSchema(deleteObject(schema, objectId), located?.parentLoopObjectId ? "deleteLoopNode" : "deleteNode", { source: "propertyPanel" });
                  }}
                  onDeleteFlow={flowId => {
                    const located = findFlowWithCollection(schema, flowId);
                    commitSchema(deleteFlow(schema, flowId), located?.parentLoopObjectId ? "deleteLoopFlow" : "deleteFlow", { source: "propertyPanel" });
                  }}
                  onClose={() => applyPatch({ selectedObjectId: undefined, selectedFlowId: undefined }, { pushHistory: false, skipDirty: true, skipValidate: true })}
                />
            </div>
          ) : null}
          <div
            role="button"
            tabIndex={0}
            aria-label={rightOpen ? "折叠属性面板" : "展开属性面板"}
            title={labels.properties}
            style={rightRailStyle}
            onClick={() => setRightOpen(open => !open)}
            onKeyDown={event => {
              if (event.key === "Enter" || event.key === " ") {
                event.preventDefault();
                setRightOpen(open => !open);
              }
            }}
          >
            <IconSetting style={{ fontSize: 18 }} />
            <Text
              size="small"
              strong
              style={{
                writingMode: "vertical-rl",
                textOrientation: "mixed",
                letterSpacing: 1
              }}
            >
              {labels.properties}
            </Text>
          </div>
        </div>
      </div>
      {bottomOpen ? (
        <div
          style={{
            minHeight: 0,
            borderTop: "1px solid var(--semi-color-border, #e5e6eb)",
            background: "var(--semi-color-bg-1, #fff)",
            overflow: "hidden",
            padding: 12,
            display: "flex",
            flexDirection: "column"
          }}
        >
          <Tabs
            type="line"
            activeKey={bottomTab}
            onChange={key => setBottomTab(key as "problems" | "debug")}
            tabBarExtraContent={
              <Button
                aria-label="折叠底部面板"
                theme="borderless"
                icon={<IconChevronDown />}
                onClick={() => setBottomOpen(false)}
              />
            }
            style={{ flex: 1, minHeight: 0 }}
          >
            <Tabs.TabPane tab={<Space><IconTickCircle />{labels.problems}<Badge count={issues.length} /></Space>} itemKey="problems">
              <div style={{ overflow: "auto", maxHeight: BOTTOM_PANEL_EXPANDED_PX - 56 }}>
                <ProblemPanel
                  issues={issues}
                  status={validationStatus}
                  lastValidatedAt={lastValidatedAt}
                  onSelect={issue => {
                    const selectedFlowId = issue.flowId ?? issue.edgeId;
                    const selectedObjectId = selectedFlowId ? undefined : issue.objectId ?? issue.nodeId;
                    const selectedCollectionId = selectedFlowId
                      ? findFlowWithCollection(schema, selectedFlowId)?.collectionId
                      : selectedObjectId
                        ? findObjectWithCollection(schema, selectedObjectId)?.collectionId
                        : issue.collectionId;
                    setRightOpen(true);
                    applyPatch(
                      { selectedObjectId, selectedFlowId, selectedCollectionId },
                      { pushHistory: false, skipDirty: true, skipValidate: true },
                    );
                  }}
                />
              </div>
            </Tabs.TabPane>
            <Tabs.TabPane tab={labels.debug} itemKey="debug">
              <div style={{ overflow: "auto", maxHeight: BOTTOM_PANEL_EXPANDED_PX - 56 }}>
                <DebugPanel
                  session={runSession}
                  serviceError={runtimeServiceError}
                  activeFrameId={activeTraceFrameId}
                  onSelectFrame={selectTraceFrame}
                  onSelectFlow={selectTraceFlow}
                  onSelectError={selectTraceError}
                  onClear={clearTestRun}
                  onRerun={handleTestRun}
                  onCancelRun={cancelTestRun}
                />
              </div>
            </Tabs.TabPane>
          </Tabs>
        </div>
      ) : (
        <div
          style={{
            display: "flex",
            flexDirection: "row",
            alignItems: "stretch",
            height: BOTTOM_STRIP_HEIGHT_PX,
            minHeight: BOTTOM_STRIP_HEIGHT_PX,
            borderTop: "1px solid var(--semi-color-border, #e5e6eb)",
            background: "var(--semi-color-bg-2, #fff)"
          }}
        >
          <button
            type="button"
            aria-label="展开问题面板"
            style={{
              flex: 1,
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              gap: 8,
              border: "none",
              borderRight: "1px solid var(--semi-color-border, #e5e6eb)",
              background: bottomTab === "problems" ? "var(--semi-color-fill-0, #f4f7fb)" : "transparent",
              cursor: "pointer",
              font: "inherit",
              color: "inherit"
            }}
            onClick={() => {
              setBottomTab("problems");
              setBottomOpen(true);
            }}
          >
            <IconTickCircle />
            <Text size="small" strong>{labels.problems}</Text>
            <Badge count={issues.length} />
          </button>
          <button
            type="button"
            aria-label="展开调试面板"
            style={{
              flex: 1,
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              gap: 8,
              border: "none",
              background: bottomTab === "debug" ? "var(--semi-color-fill-0, #f4f7fb)" : "transparent",
              cursor: "pointer",
              font: "inherit",
              color: "inherit"
            }}
            onClick={() => {
              setBottomTab("debug");
              setBottomOpen(true);
            }}
          >
            <Text size="small" strong>{labels.debug}</Text>
          </button>
        </div>
      )}
      <MicroflowTestRunModal
        visible={testRunModalOpen}
        schema={schema}
        running={running}
        onCancel={() => setTestRunModalOpen(false)}
        onRun={handleExecuteTestRun}
      />
    </div>
  );
}

export function MicroflowEditor(props: MicroflowEditorProps) {
  const { metadataAdapter, metadataCatalog, metadataWorkspaceId, metadataModuleId, ...rest } = props;
  return (
    <MicroflowMetadataProvider adapter={metadataAdapter} initialCatalog={metadataCatalog} workspaceId={metadataWorkspaceId} moduleId={metadataModuleId}>
      <MicroflowEditorInner {...rest} metadataAdapter={metadataAdapter} metadataCatalog={metadataCatalog} />
    </MicroflowMetadataProvider>
  );
}
