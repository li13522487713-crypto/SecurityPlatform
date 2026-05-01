import { useCallback, useEffect, useImperativeHandle, useMemo, useRef, useState, type CSSProperties, type DragEvent, type KeyboardEvent, type PointerEvent, type ReactNode, type Ref } from "react";
import { Badge, Button, Card, Dropdown, Empty, Input, Modal, Select, Space, Tabs, Tag, Toast, Tooltip, Typography } from "@douyinfe/semi-ui";
import {
  IconChevronDown,
  IconClose,
  IconCopy,
  IconDelete,
  IconExpand,
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
import { MicroflowNodePanel, type MicroflowNodePanelLabels, type MicroflowNodePanelTemplate } from "../node-panel";
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
import {
  type MicroflowApiClient,
  type MicroflowRunHistoryItem,
  type MicroflowTraceFrame,
  type SaveMicroflowResponse,
  type TestRunMicroflowResponse,
  type ValidateMicroflowResponse
} from "../runtime-adapter";
import {
  applyEditorGraphPatchToAuthoring,
  createAnnotationFlow,
  createMicroflowFlowId,
  createObjectFromRegistry,
  createSequenceFlow,
  deleteFlow,
  deleteObject,
  duplicateObject,
  duplicateObjectSelection,
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
import { MICROFLOW_GRID_SIZE } from "../flowgram/adapters/flowgram-coordinate";
import {
  EMPTY_MICROFLOW_METADATA_CATALOG,
  MicroflowMetadataProvider,
  useMicroflowMetadataCatalog,
  type MicroflowMetadataAdapter,
  type MicroflowMetadataCatalog,
} from "../metadata";
import { validateMicroflowSchema } from "../schema/validator";
import { collectFlowsRecursive, findFlowWithCollection, findObjectWithCollection } from "../schema/utils/object-utils";
import {
  MicroflowRunHistoryPanel,
  MicroflowStepDebugPanel,
  MicroflowTracePanel,
  MicroflowTestRunModal,
  buildExecutionPath,
  buildRunHistoryItemFromSession,
  buildRunRequest,
  filterNodeResultsByMicroflowId,
  shouldBlockRun,
  type MicroflowDebugCommand,
  type MicroflowDebugTraceEventDto,
  type MicroflowDebugSessionDto,
  type MicroflowDebugVariableSnapshotDto,
  type MicroflowDebugWatchExpressionDto,
  type MicroflowRunSession,
  type MicroflowTestRunInput
} from "../debug";
import { createMicroflowGraphIndex, useDebouncedMicroflowValidation, type MicroflowValidationAdapterLike, type MicroflowValidationMode } from "../performance";
import { useMicroflowShortcuts } from "./shortcuts";
import type {
  MicroflowCaseValue,
  MicroflowEditorEdge,
  MicroflowEditorGraph,
  MicroflowEditorGraphPatch,
  MicroflowEditorNode,
  MicroflowEditorPort,
  MicroflowFlow,
  MicroflowDesignSchema,
  MicroflowObject,
  MicroflowPoint,
  MicroflowSchema,
  MicroflowValidationIssue
} from "../schema/types";

const { Text, Title } = Typography;

const favoriteStorageKey = "atlas_microflow_node_panel_favorites";
const leftPanelStorageKey = "atlas_microflow_panel_left_open";
const rightPanelStorageKey = "atlas_microflow_panel_right_open";
const rightPanelPinnedStorageKey = "atlas_microflow_panel_right_pinned";
const bottomPanelStorageKey = "atlas_microflow_panel_bottom_open";
const bottomTabStorageKey = "atlas_microflow_panel_bottom_tab";
const mendixLayoutStorageKey = "lowcode-studio:mendix-layout:v1";
const RAIL_WIDTH_PX = 44;
const LEFT_PANEL_EXPANDED_PX = 300;
const RIGHT_PANEL_EXPANDED_PX = 380;
const BOTTOM_STRIP_HEIGHT_PX = 32;
const BOTTOM_DOCK_PEEK_HEIGHT_PX = 260;
const BOTTOM_DOCK_FULL_DEFAULT_PX = 420;
const BOTTOM_DOCK_FULL_MIN_PX = 320;
const MOVE_HISTORY_DEBOUNCE_MS = 250;
const defaultFavoriteNodeKeys = ["activity:objectRetrieve", "activity:callRest", "activity:logMessage"];
const INTERNAL_TOOLBAR_ROW_PX = 60;

function isDesignSchema(schema: unknown): schema is MicroflowDesignSchema {
  return (schema as { workflow?: unknown }).workflow != null;
}

type MendixLayoutInspectorMode = "floating" | "docked";
type BottomDockMode = "collapsed" | "peek" | "full";
export type MicroflowWorkbenchBottomTab = "problems" | "debug" | "references" | "info" | "console";

interface MendixLayoutStorage {
  nodesDrawerOpen?: boolean;
  inspectorOpen?: boolean;
  inspectorMode?: MendixLayoutInspectorMode;
  inspectorWidth?: number;
  bottomOpen?: boolean;
  bottomMode?: BottomDockMode;
  bottomHeight?: number;
  activeBottomTab?: "problems" | "debug" | "validation" | "references" | "info" | "console";
  focusMode?: boolean;
  minimapVisible?: boolean;
  gridVisible?: boolean;
}

type MicroflowSchemaChangeSource = "propertyPanel" | "flowgram" | "nodePanel" | "autolayout" | "history" | "runtime";

interface CanvasNodeContextMenuState {
  objectId: string;
  collectionId?: string;
  x: number;
  y: number;
}

interface MicroflowClipboardSelection {
  microflowId: string;
  objectIds: string[];
  flowIds: string[];
}

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
    case "flowgramNodeDelete":
      return "deleteNode";
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
  const objectIds = (selection?.objectIds ?? []).filter(id => Boolean(findObjectWithCollection(schema, id)));
  const flowIds = (selection?.flowIds ?? []).filter(id => Boolean(findFlowWithCollection(schema, id)));
  const objectId = selection?.objectId && findObjectWithCollection(schema, selection.objectId) ? selection.objectId : objectIds[0];
  const flowId = !objectId && selection?.flowId && findFlowWithCollection(schema, selection.flowId) ? selection.flowId : objectId ? undefined : flowIds[0];
  const collectionId = objectId
    ? findObjectWithCollection(schema, objectId)?.collectionId
    : flowId
      ? findFlowWithCollection(schema, flowId)?.collectionId
      : selection?.collectionId;
  const count = objectIds.length + flowIds.length;
  return { objectId, flowId, collectionId, objectIds, flowIds, mode: count === 0 ? "none" : count === 1 ? "single" : "multi" };
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
  onValidationStateChange?: (state: { microflowId: string; issues: MicroflowValidationIssue[]; status: string; lastValidatedAt?: Date }) => void;
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
  /**
   * 工作台外置工具栏模式。
   *
   * - "internal"（默认）：编辑器内部继续渲染顶部 Save / Validate / TestRun /
   *   Publish 等按钮；与现有 `/microflow/:id/editor` 路由表现一致。
   * - "external"：外部宿主（例如 Mendix Studio Workbench Toolbar）通过
   *   {@link MicroflowEditorHandle} 远程触发保存 / 校验 / 运行 / 发布等动作；
   *   此时编辑器隐藏自带的顶部工具栏，避免双层 toolbar 视觉重叠。
   */
  toolbarMode?: "internal" | "external";
  /**
   * 命令式 ref 句柄，外置工具栏可通过它远程触发动作并读取状态。
   * 编辑器仍掌握所有真实业务逻辑，宿主只发起命令、读快照。
   */
  editorRef?: Ref<MicroflowEditorHandle>;
  shellMode?: "legacy-host-layout" | "editor-native-layout";
  onLayoutStateChange?: (state: MicroflowWorkbenchLayoutState) => void;
  onWorkbenchStatusChange?: (status: MicroflowWorkbenchStatus) => void;
}

/**
 * 工作台 / 外置宿主可通过该 ref 操控编辑器，对齐 Mendix Studio 顶部工具栏的
 * 保存 / 校验 / 运行 / 调试 / 发布 / 撤销 / 重做 / 适应画布 / 自动布局 /
 * 缩放 / 全屏 / 小地图 等按钮。
 *
 * 所有 promise 方法都会内部 catch & toast，宿主调用方无需自己处理失败；状态
 * 读取方法返回当前快照，便于工具栏显示按钮的 disabled / loading 等。
 */
export interface MicroflowEditorHandle {
  save: () => Promise<void>;
  validate: () => Promise<void>;
  runTest: () => Promise<void>;
  /**
   * P1-3: 与 runTest 走同一 testRun 路径，但调试模式打开底部 trace 抽屉并切换到
   * "Debug" tab，便于观察单步 trace。当前 server 端无 step-debug 能力，run 之后
   * 仍是一次性 trace 列表；此入口仅做 UX 区分。
   */
  runDebug: () => Promise<void>;
  publish: () => Promise<void>;
  undo: () => void;
  redo: () => void;
  autoLayout: () => void;
  fitView: () => void;
  zoomIn: () => void;
  zoomOut: () => void;
  setZoom: (zoom: number) => void;
  toggleFullscreen: () => void;
  toggleFocusMode: () => void;
  toggleMinimap: () => void;
  resetLayout: () => void;
  getStatus: () => MicroflowEditorStatusSnapshot;
  openBottomTab: (tab: MicroflowWorkbenchBottomTab) => void;
  setBottomDockMode: (mode: BottomDockMode) => void;
  getLayoutState: () => MicroflowWorkbenchLayoutState;
  configureAllNodeAcceptance120?: () => void;
}

/** 外置 Toolbar 渲染按钮 disabled / loading 等状态的依赖快照。 */
export interface MicroflowEditorStatusSnapshot {
  microflowId: string;
  schemaVersion: string;
  dirty: boolean;
  saving: boolean;
  running: boolean;
  validationStatus: string;
  errorCount: number;
  warningCount: number;
  canUndo: boolean;
  canRedo: boolean;
  zoomPercent: number;
  hasRunSession: boolean;
  fullscreen: boolean;
  activeBottomTab: MicroflowWorkbenchBottomTab;
  bottomDockMode: BottomDockMode;
  sessionHydrated: boolean;
  traceHydrated: boolean;
  debugSessionHydrated: boolean;
  degradedRunSession: boolean;
}

export interface MicroflowWorkbenchLayoutState {
  shellMode: "legacy-host-layout" | "editor-native-layout";
  activeBottomTab: MicroflowWorkbenchBottomTab;
  bottomDockMode: BottomDockMode;
  focusMode: boolean;
  minimapVisible: boolean;
  gridVisible: boolean;
}

export interface MicroflowWorkbenchStatus extends MicroflowEditorStatusSnapshot {
  layout: MicroflowWorkbenchLayoutState;
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

function createValidationServiceIssue(error: unknown, mode: MicroflowValidationMode, microflowId: string): MicroflowValidationIssue {
  return {
    id: `${microflowId}:server:MICROFLOW_VALIDATION_API_FAILED:${mode}`,
    microflowId,
    code: "MICROFLOW_VALIDATION_API_FAILED",
    severity: mode === "edit" ? "warning" : "error",
    source: "server",
    fieldPath: "validation",
    message: error instanceof Error ? `后端校验失败：${error.message}` : "后端校验失败，请检查网络、权限或服务状态。",
    blockSave: mode !== "edit",
    blockPublish: mode !== "edit",
  };
}

function createMissingMicroflowApiClient(): MicroflowApiClient {
  return new Proxy({}, {
    get: (_target, property) => {
      if (typeof property === "string") {
        return () => {
          throw new Error(`Microflow apiClient is required for ${property}; localStorage persistence is not used in this editor.`);
        };
      }
      return undefined;
    }
  }) as MicroflowApiClient;
}

function summarizeValidationIssues(issues: MicroflowValidationIssue[]) {
  return {
    errorCount: issues.filter(issue => issue.severity === "error").length,
    warningCount: issues.filter(issue => issue.severity === "warning").length,
    infoCount: issues.filter(issue => issue.severity === "info").length,
  };
}

function asServerValidationIssue(issue: MicroflowValidationIssue): MicroflowValidationIssue {
  return {
    ...issue,
    id: issue.id.includes(":server:") ? issue.id : `server:${issue.id}`,
    source: issue.source ?? "server",
  };
}

function issueSourceLabel(source?: MicroflowValidationIssue["source"]): string {
  const labels: Record<string, string> = {
    schema: "Schema",
    root: "Schema",
    node: "Nodes",
    objectCollection: "Nodes",
    event: "Nodes",
    flow: "Flows",
    parameter: "Parameters",
    variable: "Variables",
    callMicroflow: "Call Microflow",
    domainModel: "Domain Model",
    metadata: "Domain Model",
    loop: "Loop",
    decision: "Decision",
    action: "Actions",
    expression: "Expressions",
    errorHandling: "Error Handling",
    reachability: "Reachability",
    server: "Server",
  };
  return source ? labels[source] ?? source : "Schema";
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

function normalizeBottomDockMode(value: unknown): BottomDockMode | undefined {
  if (value === "collapsed" || value === "peek" || value === "full") {
    return value;
  }
  if (value === true || value === "true") {
    return "peek";
  }
  if (value === false || value === "false") {
    return "collapsed";
  }
  return undefined;
}

function readStoredBottomDockMode(key: string): BottomDockMode | undefined {
  if (typeof window === "undefined") {
    return undefined;
  }
  try {
    return normalizeBottomDockMode(window.localStorage.getItem(key));
  } catch {
    return undefined;
  }
}

function writeStoredBottomDockMode(key: string, value: BottomDockMode): void {
  if (typeof window === "undefined") {
    return;
  }
  try {
    window.localStorage.setItem(key, value);
  } catch {
    /* ignore */
  }
}

function normalizeWorkbenchBottomTab(value: unknown): MicroflowWorkbenchBottomTab | undefined {
  return value === "problems"
    || value === "debug"
    || value === "references"
    || value === "info"
    || value === "console"
    ? value
    : undefined;
}

function readStoredBottomTab(): MicroflowWorkbenchBottomTab | undefined {
  if (typeof window === "undefined") {
    return undefined;
  }
  const raw = window.localStorage.getItem(bottomTabStorageKey);
  return normalizeWorkbenchBottomTab(raw);
}

function writeStoredBottomTab(value: MicroflowWorkbenchBottomTab): void {
  if (typeof window === "undefined") {
    return;
  }
  try {
    window.localStorage.setItem(bottomTabStorageKey, value);
  } catch {
    /* ignore */
  }
}

function readMendixLayoutStorage(): MendixLayoutStorage {
  if (typeof window === "undefined") {
    return {};
  }
  try {
    return JSON.parse(window.localStorage.getItem(mendixLayoutStorageKey) || "{}") as MendixLayoutStorage;
  } catch {
    return {};
  }
}

function writeMendixLayoutStorage(patch: MendixLayoutStorage): void {
  if (typeof window === "undefined") {
    return;
  }
  try {
    const current = readMendixLayoutStorage();
    window.localStorage.setItem(mendixLayoutStorageKey, JSON.stringify({ ...current, ...patch }));
  } catch {
    window.localStorage.setItem(mendixLayoutStorageKey, JSON.stringify(patch));
  }
}

function readStoredExternalBottomTab(): MicroflowWorkbenchBottomTab | undefined {
  return normalizeWorkbenchBottomTab(readMendixLayoutStorage().activeBottomTab);
}

function readStoredExternalBottomDockMode(): BottomDockMode | undefined {
  const storage = readMendixLayoutStorage();
  return normalizeBottomDockMode(storage.bottomMode ?? storage.bottomOpen);
}

function clampBottomDockHeight(value: number): number {
  const maxHeight = typeof window === "undefined" ? 640 : Math.max(BOTTOM_DOCK_FULL_MIN_PX, Math.floor(window.innerHeight * 0.7));
  return Math.min(Math.max(value, BOTTOM_DOCK_FULL_MIN_PX), maxHeight);
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

function snapCanvasPoint(point: { x: number; y: number }, gridSize = MICROFLOW_GRID_SIZE) {
  if (!Number.isFinite(point.x) || !Number.isFinite(point.y) || gridSize <= 1) {
    return point;
  }
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
    return createSequenceFlow({ id: createMicroflowFlowId(schema, "flow"), originObjectId: sourceObjectId, destinationObjectId: targetObjectId });
  }
  if (source.kind === "annotation" || target.kind === "annotation") {
    return createAnnotationFlow({ id: createMicroflowFlowId(schema, "annotation-flow"), originObjectId: sourceObjectId, destinationObjectId: targetObjectId });
  }
  const supportsErrorHandling = source.kind === "actionActivity"
    ? source.action.errorHandlingType !== "rollback"
    : source.kind === "loopedActivity"
      ? source.errorHandlingType !== "rollback"
      : source.kind === "exclusiveSplit" || source.kind === "inheritanceSplit";
  if (target.kind === "errorEvent" && supportsErrorHandling) {
    return createSequenceFlow({
      id: createMicroflowFlowId(schema, "flow"),
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
          id: createMicroflowFlowId(schema, "flow"),
          originObjectId: sourceObjectId,
          destinationObjectId: targetObjectId,
          edgeKind: "decisionCondition",
          caseValues: [{ kind: "boolean", officialType: "Microflows$EnumerationCase", value: true, persistedValue: "true" }],
          label: "true"
        });
      }
      if (!used.has(false)) {
        return createSequenceFlow({
          id: createMicroflowFlowId(schema, "flow"),
          originObjectId: sourceObjectId,
          destinationObjectId: targetObjectId,
          edgeKind: "decisionCondition",
          caseValues: [{ kind: "boolean", officialType: "Microflows$EnumerationCase", value: false, persistedValue: "false" }],
          label: "false"
        });
      }
    }
    return createSequenceFlow({
      id: createMicroflowFlowId(schema, "flow"),
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
      id: createMicroflowFlowId(schema, "flow"),
      originObjectId: sourceObjectId,
      destinationObjectId: targetObjectId,
      edgeKind: "objectTypeCondition",
      caseValues: nextEntity
        ? [{ kind: "inheritance", officialType: "Microflows$InheritanceCase", entityQualifiedName: nextEntity }]
        : [{ kind: "fallback", officialType: "Microflows$NoCase" }],
      label: nextEntity ?? "fallback"
    });
  }
  return createSequenceFlow({ id: createMicroflowFlowId(schema, "flow"), originObjectId: sourceObjectId, destinationObjectId: targetObjectId });
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
    return createSequenceFlow({ id: createMicroflowFlowId(schema, "flow"), originObjectId: sourcePort.objectId, destinationObjectId: targetPort.objectId });
  }
  const edgeKind = inferEdgeKindFromPorts(source, target, sourcePort);
  if (edgeKind === "annotation") {
    return createAnnotationFlow({
      id: createMicroflowFlowId(schema, "annotation-flow"),
      originObjectId: sourcePort.objectId,
      destinationObjectId: targetPort.objectId,
      label: "annotation"
    });
  }
  return createSequenceFlow({
    id: createMicroflowFlowId(schema, "flow"),
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
  lastError,
  onSelect,
  onRetry,
}: {
  issues: MicroflowValidationIssue[];
  status?: string;
  lastValidatedAt?: Date;
  lastError?: unknown;
  onSelect: (issue: MicroflowValidationIssue) => void;
  onRetry: () => void;
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
  const groupedIssues = useMemo(() => {
    const groups = new Map<string, MicroflowValidationIssue[]>();
    for (const issue of filteredIssues) {
      const key = issueSourceLabel(issue.source);
      groups.set(key, [...(groups.get(key) ?? []), issue]);
    }
    return [...groups.entries()];
  }, [filteredIssues]);

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
              ...sources.map(source => ({ label: issueSourceLabel(source as MicroflowValidationIssue["source"]), value: source })),
            ]}
          />
        </Space>
      </Space>
      {status === "validating" && issues.length === 0 ? <Empty title="Validating" description="Schema validation is running in the background." /> : null}
      {status === "failed" ? (
        <Empty
          title="Validation failed"
          description={lastError instanceof Error ? lastError.message : "后端或本地校验执行失败，请重试。"}
        >
          <Button size="small" type="primary" onClick={onRetry}>Retry</Button>
        </Empty>
      ) : null}
      {status !== "validating" && status !== "failed" && issues.length === 0 ? <Empty title="No problems found" description="Schema validation passed." /> : null}
      {issues.length > 0 && filteredIssues.length === 0 ? <Empty title="No matching problems" description="Adjust filters to see validation issues." /> : null}
      {groupedIssues.map(([source, sourceIssues]) => (
        <div key={source} style={{ width: "100%" }}>
          <Text strong>{source}</Text>
          <Space vertical align="start" spacing={8} style={{ width: "100%", marginTop: 8 }}>
            {sourceIssues.map(issue => (
              <div key={issue.id} role="button" tabIndex={0} style={{ width: "100%" }} onClick={() => onSelect(issue)} onKeyDown={event => {
                if (event.key === "Enter" || event.key === " ") {
                  event.preventDefault();
                  onSelect(issue);
                }
              }}>
                <Card shadows="hover" style={{ width: "100%" }} bodyStyle={{ padding: 10 }}>
                  <Space align="start" style={{ width: "100%", justifyContent: "space-between" }}>
                    <div style={{ minWidth: 0 }}>
                      <Space spacing={6}>
                        <Tag color={issue.severity === "error" ? "red" : issue.severity === "warning" ? "orange" : "blue"}>{issue.severity}</Tag>
                        <Text strong>{issue.code}</Text>
                      </Space>
                      <br />
                      <Text size="small" type="secondary">{issue.message}</Text>
                      <br />
                      <Text size="small" type="tertiary">
                        {[issue.objectId ?? issue.flowId ?? issue.actionId ?? issue.parameterId, issue.fieldPath].filter(Boolean).join(" · ") || "Global"}
                      </Text>
                    </div>
                    <Tag color={issue.flowId || issue.edgeId ? "purple" : issue.objectId || issue.nodeId ? "blue" : "grey"}>
                      {issue.flowId || issue.edgeId ? "flow" : issue.objectId || issue.nodeId ? "node" : "global"}
                    </Tag>
                  </Space>
                </Card>
              </div>
            ))}
          </Space>
        </div>
      ))}
    </Space>
  );
}

function DebugPanel({
  microflowId,
  microflowName,
  session,
  serviceError,
  debugAvailable,
  debugSession,
  debugVariables,
  debugWatches,
  activeFrameId,
  onSelectFrame,
  onSelectFlow,
  onSelectError,
  onClear,
  onRerun,
  onCancelRun,
  onDebugCommand,
  onDebugEvaluate,
}: {
  microflowId: string;
  microflowName?: string;
  session?: MicroflowRunSession;
  serviceError?: string;
  debugAvailable?: boolean;
  debugSession?: MicroflowDebugSessionDto;
  debugVariables?: MicroflowDebugVariableSnapshotDto[];
  debugWatches?: MicroflowDebugWatchExpressionDto[];
  activeFrameId?: string;
  onSelectFrame: (frame: MicroflowTraceFrame) => void;
  onSelectFlow: (flowId: string) => void;
  onSelectError: (error: NonNullable<MicroflowTraceFrame["error"]>) => void;
  onClear: () => void;
  onRerun: () => void;
  onCancelRun: () => void;
  onDebugCommand?: (command: MicroflowDebugCommand) => void;
  onDebugEvaluate?: (expression: string) => void;
}) {
  if (serviceError) {
    return <Empty title="运行服务不可用" description={serviceError} />;
  }
  const hasTrace = Boolean(session && buildExecutionPath(session).length > 0);
  return (
    <Space vertical align="start" style={{ width: "100%" }}>
      {debugAvailable && debugSession ? (
        <MicroflowStepDebugPanel
          status={debugSession.status}
          currentNodeId={debugSession.currentNodeObjectId ?? debugSession.currentSafePoint?.nodeObjectId}
          currentFlowId={debugSession.currentSafePoint?.incomingFlowId ?? debugSession.currentSafePoint?.outgoingFlowId}
          currentBranchId={debugSession.currentSafePoint?.branchId}
          currentPhase={debugSession.pausePhase ?? debugSession.currentSafePoint?.phase}
          variables={(debugVariables ?? []).map(variable => ({
            name: variable.name,
            valuePreview: variable.valuePreview ?? "",
            scope: variable.scopeKind,
          }))}
          watches={(debugWatches ?? []).map(watch => ({
            expression: watch.expression,
            value: watch.valuePreview,
            error: watch.error,
          }))}
          labels={{
            statusPrefix: "Status",
            nodePrefix: "Node",
            flowPrefix: "Flow",
            branchPrefix: "Branch",
            phasePrefix: "Phase",
            breakpointsTitle: "Breakpoints",
            staleBreakpoint: "stale",
            logpoint: "logpoint",
            variablesTitle: "Variables",
            watchesTitle: "Watches",
            callStackTitle: "Call Stack",
            branchTreeTitle: "Branches",
            watchPlaceholder: "Expression",
            evaluate: "Evaluate",
            commands: {
              continue: "Continue",
              pause: "Pause",
              stepOver: "Step Over",
              stepInto: "Step Into",
              stepOut: "Step Out",
              runToNode: "Run To Node",
              cancel: "Cancel",
              stop: "Stop",
            },
          }}
          onCommand={onDebugCommand}
          onEvaluate={onDebugEvaluate}
        />
      ) : null}
      {!debugAvailable ? (
        <Tag color="grey">Trace mode</Tag>
      ) : null}
      {!hasTrace ? <Empty title="No trace" description="Run a test to see object/flow trace frames." /> : null}
      {session && hasTrace ? (
      <>
      <Space style={{ width: "100%", justifyContent: "space-between" }}>
        <Space>
          <Tag color={session.status === "success" ? "green" : "red"}>{session.status}</Tag>
          <Tag>{buildExecutionPath(session).length} nodeResults</Tag>
          <Tag>{session.childRuns?.length ?? 0} child runs</Tag>
        </Space>
        <Space>
          <Button size="small" onClick={onRerun}>重新运行</Button>
          <Button size="small" type="warning" onClick={onCancelRun}>取消运行</Button>
          <Button size="small" type="danger" theme="borderless" onClick={onClear}>清空</Button>
        </Space>
      </Space>
      <MicroflowTracePanel
        microflowId={microflowId}
        microflowName={microflowName}
        session={session}
        activeFrameId={activeFrameId}
        onSelectFrame={onSelectFrame}
        onSelectFlow={onSelectFlow}
        onSelectError={onSelectError}
      />
      </>
      ) : null}
    </Space>
  );
}

interface MicroflowCommandItem {
  id: string;
  label: string;
  disabled?: boolean;
  run: () => void;
}

function MicroflowCommandPalette({
  visible,
  query,
  commands,
  onQueryChange,
  onClose,
}: {
  visible: boolean;
  query: string;
  commands: MicroflowCommandItem[];
  onQueryChange: (value: string) => void;
  onClose: () => void;
}) {
  const normalized = query.trim().toLocaleLowerCase();
  const filtered = normalized
    ? commands.filter(command => command.label.toLocaleLowerCase().includes(normalized) || command.id.toLocaleLowerCase().includes(normalized))
    : commands;
  return (
    <Modal
      visible={visible}
      title="Command Palette"
      footer={null}
      onCancel={onClose}
      style={{ maxWidth: 520 }}
      bodyStyle={{ paddingTop: 8 }}
    >
      <Space vertical align="start" style={{ width: "100%" }}>
        <Input
          autoFocus
          value={query}
          placeholder="Search commands"
          onChange={onQueryChange}
          onEnterPress={() => {
            const first = filtered.find(command => !command.disabled);
            if (first) {
              first.run();
              onClose();
            }
          }}
        />
        <Space vertical align="start" spacing={4} style={{ width: "100%", maxHeight: 360, overflow: "auto" }}>
          {filtered.map(command => (
            <Button
              key={command.id}
              theme="borderless"
              type="tertiary"
              disabled={command.disabled}
              style={{ justifyContent: "flex-start" }}
              onClick={() => {
                command.run();
                onClose();
              }}
            >
              {command.label}
            </Button>
          ))}
          {filtered.length === 0 ? <Empty title="No commands" description="Try a different search." /> : null}
        </Space>
      </Space>
    </Modal>
  );
}

function MicroflowEditorInner(props: MicroflowEditorProps) {
  const labels = { ...defaultLabels, ...props.labels };
  const loadedMetadata = useMicroflowMetadataCatalog();
  const metadataForRefresh = loadedMetadata ?? props.metadataCatalog ?? EMPTY_MICROFLOW_METADATA_CATALOG;
  const apiClient = props.apiClient ?? createMissingMicroflowApiClient();
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
  const [clipboardObject, setClipboardObject] = useState<MicroflowClipboardSelection>();
  const [favoriteNodeKeys, setFavoriteNodeKeys] = useState(readFavoriteNodeKeys);
  const [validationTrigger, setValidationTrigger] = useState(0);
  const {
    issues,
    setIssues,
    validationStatus,
    lastValidatedAt,
    lastError,
    runValidationNow,
  } = useDebouncedMicroflowValidation({
    schema,
    metadata: loadedMetadata,
    trigger: validationTrigger,
    initialIssues: schema.validation.issues ?? [],
    validationAdapter: props.validationAdapter,
    resourceId: schema.id,
  });
  const [runSessionByMicroflowId, setRunSessionByMicroflowId] = useState<Record<string, MicroflowRunSession | undefined>>({});
  const [runtimeServiceErrorByMicroflowId, setRuntimeServiceErrorByMicroflowId] = useState<Record<string, string | undefined>>({});
  const [runHistoryByMicroflowId, setRunHistoryByMicroflowId] = useState<Record<string, MicroflowRunHistoryItem[]>>({});
  const [selectedRunIdByMicroflowId, setSelectedRunIdByMicroflowId] = useState<Record<string, string | undefined>>({});
  const [runDetailsByRunId, setRunDetailsByRunId] = useState<Record<string, MicroflowRunSession | undefined>>({});
  const [runHistoryLoadingByMicroflowId, setRunHistoryLoadingByMicroflowId] = useState<Record<string, boolean>>({});
  const [runHistoryErrorByMicroflowId, setRunHistoryErrorByMicroflowId] = useState<Record<string, string | undefined>>({});
  const [runHistoryStatusByMicroflowId, setRunHistoryStatusByMicroflowId] = useState<Record<string, "all" | "success" | "failed" | "unsupported" | "cancelled">>({});
  const [focusObjectId, setFocusObjectId] = useState<string>();
  const [focusRequestSeq, setFocusRequestSeq] = useState(0);
  const [canvasNodeContextMenu, setCanvasNodeContextMenu] = useState<CanvasNodeContextMenuState>();
  const [commandPaletteOpen, setCommandPaletteOpen] = useState(false);
  const [commandPaletteQuery, setCommandPaletteQuery] = useState("");
  const runHistoryRequestSeqRef = useRef<Record<string, number>>({});
  const [activeTraceFrameId, setActiveTraceFrameId] = useState<string>();
  const [pendingDebugSessionId, setPendingDebugSessionId] = useState<string>();
  const [debugSessionByMicroflowId, setDebugSessionByMicroflowId] = useState<Record<string, MicroflowDebugSessionDto | undefined>>({});
  const [debugVariablesBySessionId, setDebugVariablesBySessionId] = useState<Record<string, MicroflowDebugVariableSnapshotDto[]>>({});
  const [debugTraceBySessionId, setDebugTraceBySessionId] = useState<Record<string, MicroflowDebugTraceEventDto[]>>({});
  const [debugWatchesBySessionId, setDebugWatchesBySessionId] = useState<Record<string, MicroflowDebugWatchExpressionDto[]>>({});
  const [testRunModalOpen, setTestRunModalOpen] = useState(false);
  const [runInputsByMicroflowId, setRunInputsByMicroflowId] = useState<Record<string, Record<string, unknown>>>({});
  const [saving, setSaving] = useState(false);
  const [running, setRunning] = useState(false);
  const [dirty, setDirty] = useState(false);
  const schemaRevisionRef = useRef(0);
  const toolbarMode = props.toolbarMode ?? "internal";
  const shellMode = props.shellMode ?? (toolbarMode === "external" ? "editor-native-layout" : "legacy-host-layout");
  const externalLayout = shellMode === "editor-native-layout";
  const [leftOpen, setLeftOpen] = useState(() => {
    if (props.toolbarMode === "external") {
      return Boolean(readMendixLayoutStorage().nodesDrawerOpen);
    }
    if (!persistAuxPanelState) {
      return true;
    }
    const stored = readStoredBoolean(leftPanelStorageKey);
    return stored !== undefined ? stored : true;
  });
  const [rightOpen, setRightOpen] = useState(() => {
    if (props.toolbarMode === "external") {
      return Boolean(readMendixLayoutStorage().inspectorOpen);
    }
    if (!persistAuxPanelState) {
      return rightPanelFallback;
    }
    const stored = readStoredBoolean(rightPanelStorageKey);
    return stored !== undefined ? stored : rightPanelFallback;
  });
  const [rightPinned, setRightPinned] = useState(() => {
    if (!persistAuxPanelState) {
      return false;
    }
    return readStoredBoolean(rightPanelPinnedStorageKey) === true;
  });
  const bottomPanelFallbackMode: BottomDockMode = bottomPanelFallback ? "peek" : "collapsed";
  const [bottomDockMode, setBottomDockMode] = useState<BottomDockMode>(() => {
    if (props.toolbarMode === "external") {
      return readStoredExternalBottomDockMode() ?? bottomPanelFallbackMode;
    }
    if (!persistAuxPanelState) {
      return bottomPanelFallbackMode;
    }
    return readStoredBottomDockMode(bottomPanelStorageKey) ?? bottomPanelFallbackMode;
  });
  const [bottomDockHeight, setBottomDockHeight] = useState(() => {
    const storedHeight = readMendixLayoutStorage().bottomHeight;
    return clampBottomDockHeight(typeof storedHeight === "number" ? storedHeight : BOTTOM_DOCK_FULL_DEFAULT_PX);
  });
  const [bottomTab, setBottomTab] = useState<MicroflowWorkbenchBottomTab>(() => (
    externalLayout ? readStoredExternalBottomTab() : readStoredBottomTab()
  ) ?? "problems");
  const bottomOpen = bottomDockMode !== "collapsed";
  const activeBottomDockHeight = bottomDockMode === "full" ? bottomDockHeight : BOTTOM_DOCK_PEEK_HEIGHT_PX;
  const [focusMode, setFocusMode] = useState(() => Boolean(readMendixLayoutStorage().focusMode));
  const onValidationStateChangeRef = useRef(props.onValidationStateChange);
  const onSchemaChangeRef = useRef(props.onSchemaChange);

  useEffect(() => {
    onValidationStateChangeRef.current = props.onValidationStateChange;
  }, [props.onValidationStateChange]);

  useEffect(() => {
    onSchemaChangeRef.current = props.onSchemaChange;
  }, [props.onSchemaChange]);

  const validateForMode = useCallback(async (targetSchema: MicroflowSchema, mode: MicroflowValidationMode) => {
    try {
      const localResult = validateMicroflowSchema({
        schema: targetSchema,
        metadata: loadedMetadata,
        options: { mode, includeWarnings: true, includeInfo: true },
      });
      setIssues(localResult.issues);
      if (!props.validationAdapter) {
        return localResult;
      }
      const serverResult = await props.validationAdapter.validate({
        resourceId: targetSchema.id,
        schema: targetSchema,
        metadata: loadedMetadata,
        mode,
        includeWarnings: true,
        includeInfo: true,
      });
      const serverIssues = serverResult.issues.map(issue => ({
        ...asServerValidationIssue(issue),
        microflowId: targetSchema.id,
        blockSave: issue.blockSave ?? issue.severity === "error",
        blockPublish: issue.blockPublish ?? issue.severity === "error",
      }));
      const issues = [...localResult.issues, ...serverIssues];
      const summary = summarizeValidationIssues(issues);
      setIssues(issues);
      return {
        ...serverResult,
        issues,
        summary,
      };
    } catch (error) {
      const issue = createValidationServiceIssue(error, mode, targetSchema.id);
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
    gridTemplateRows: focusMode
      ? "minmax(0, 1fr)"
      : externalLayout
        ? `minmax(0, 1fr) ${BOTTOM_STRIP_HEIGHT_PX}px`
        : `${INTERNAL_TOOLBAR_ROW_PX}px minmax(0, 1fr) ${BOTTOM_STRIP_HEIGHT_PX}px`,
    height: "100%",
    minHeight: 0,
    position: "relative",
    background: "var(--semi-color-bg-0, #f7f8fa)"
  }), [externalLayout, focusMode]);

  const bodyStyle = useMemo((): CSSProperties => {
    const leftCol = focusMode ? 0 : leftOpen ? LEFT_PANEL_EXPANDED_PX : RAIL_WIDTH_PX;
    const rightCol = focusMode ? 0 : externalLayout ? RAIL_WIDTH_PX : rightOpen ? RIGHT_PANEL_EXPANDED_PX : RAIL_WIDTH_PX;
    return {
      display: "grid",
      gridTemplateColumns: `${leftCol}px minmax(0, 1fr) ${rightCol}px`,
      minHeight: 0,
      minWidth: 0,
      overflow: "hidden",
      position: "relative"
    };
  }, [externalLayout, focusMode, leftOpen, rightOpen]);

  const graph = useMemo(() => toEditorGraph({ ...schema, validation: { issues } }), [schema, issues]);
  const graphIndex = useMemo(() => createMicroflowGraphIndex(schema), [schema.objectCollection, schema.flows]);
  const selectedObject = schema.editor.selection.objectId ? graphIndex.objectsById.get(schema.editor.selection.objectId) ?? null : null;
  const selectedFlow = schema.editor.selection.flowId ? graphIndex.flowsById.get(schema.editor.selection.flowId) ?? null : null;
  const activeMicroflowId = schema.id;
  const saveBlockers = issues.filter(issue => issue.blockSave && issue.severity === "error");
  const publishBlockers = issues.filter(issue => issue.blockPublish && issue.severity === "error");
  const runSession = runSessionByMicroflowId[activeMicroflowId];
  const runtimeServiceError = runtimeServiceErrorByMicroflowId[activeMicroflowId];
  const selectedRunId = selectedRunIdByMicroflowId[activeMicroflowId];
  const runHistoryFilter = runHistoryStatusByMicroflowId[activeMicroflowId] ?? "all";
  const runHistoryItems = runHistoryByMicroflowId[activeMicroflowId] ?? [];
  const runHistoryLoading = Boolean(runHistoryLoadingByMicroflowId[activeMicroflowId]);
  const runHistoryError = runHistoryErrorByMicroflowId[activeMicroflowId];
  const selectedRunSession = selectedRunId ? runDetailsByRunId[selectedRunId] : runSession;
  const activeDebugSession = debugSessionByMicroflowId[activeMicroflowId];
  const activeDebugVariables = activeDebugSession ? debugVariablesBySessionId[activeDebugSession.id] ?? [] : [];
  const activeDebugWatches = activeDebugSession ? debugWatchesBySessionId[activeDebugSession.id] ?? [] : [];
  const traceFrames = useMemo(
    () => filterNodeResultsByMicroflowId(selectedRunSession, activeMicroflowId),
    [selectedRunSession, activeMicroflowId]
  );

  useEffect(() => {
    onValidationStateChangeRef.current?.({
      microflowId: activeMicroflowId,
      issues,
      status: validationStatus,
      lastValidatedAt,
    });
  }, [activeMicroflowId, issues, lastValidatedAt, validationStatus]);

  useEffect(() => {
    if (!canvasNodeContextMenu) {
      return undefined;
    }
    const close = () => setCanvasNodeContextMenu(undefined);
    document.addEventListener("click", close);
    window.addEventListener("blur", close);
    return () => {
      document.removeEventListener("click", close);
      window.removeEventListener("blur", close);
    };
  }, [canvasNodeContextMenu]);

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
    setRunSessionByMicroflowId(current => ({ ...current, [activeMicroflowId]: undefined }));
    setRuntimeServiceErrorByMicroflowId(current => ({ ...current, [activeMicroflowId]: undefined }));
    setSelectedRunIdByMicroflowId(current => ({ ...current, [activeMicroflowId]: undefined }));
    setActiveTraceFrameId(undefined);
    setFocusObjectId(undefined);
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
      schemaRevisionRef.current += 1;
      setDirty(!microflowSchemasEqual(refreshed, savedSchemaRef.current));
    }
    if (!options.skipDirty && source !== "runtime") {
      clearRuntimeState();
    }
    if (!options.skipDirty) {
      onSchemaChangeRef.current?.(refreshed);
    }
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
      x: Math.round(((360 - viewport.x) / Math.max(0.2, viewport.zoom) + indexOffset) / MICROFLOW_GRID_SIZE) * MICROFLOW_GRID_SIZE,
      y: Math.round(((220 - viewport.y) / Math.max(0.2, viewport.zoom) + indexOffset / 2) / MICROFLOW_GRID_SIZE) * MICROFLOW_GRID_SIZE
    };
  };

  const handleAddNode = (
    item: MicroflowNodeRegistryItem,
    options?: {
      source?: "doubleClick" | "contextMenu" | "drop";
      position?: { x: number; y: number };
      insertFlowId?: string;
      payload?: MicroflowNodeDragPayload;
      parentLoopObjectId?: string;
    }
  ) => {
    const position = options?.position ?? quickAddPosition();
    const parentLoopObjectId = options?.parentLoopObjectId ?? findLoopAtPosition(graph, position);
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

  const handleInsertTemplate = (template: MicroflowNodePanelTemplate) => {
    if (props.readonly) {
      return;
    }
    const base = template.defaultOffset ?? quickAddPosition();
    const objects = template.nodeKeys.map((key, index) => {
      const entry = microflowNodeRegistryByKey.get(key);
      if (!entry) {
        return undefined;
      }
      return createObjectFromRegistry(
        entry,
        {
          x: base.x + index * 220,
          y: base.y + (index % 2) * 80,
        },
        createUniqueMicroflowObjectId(schema, key.replace(":", "-")),
      );
    }).filter((object): object is MicroflowObject => Boolean(object));
    if (objects.length === 0) {
      Toast.warning("Template has no available nodes.");
      return;
    }
    const flows = (template.flowPairs ?? []).flatMap(pair => {
      const from = objects[pair.from];
      const to = objects[pair.to];
      if (!from || !to) {
        return [];
      }
      const booleanCase = pair.label === "true" || pair.label === "false";
      const booleanValue = pair.label === "true";
      return [createSequenceFlow({
        id: createMicroflowFlowId(schema, "template-flow"),
        originObjectId: from.id,
        destinationObjectId: to.id,
        label: pair.label,
        caseValues: booleanCase
          ? [{ kind: "boolean", officialType: "Microflows$EnumerationCase", value: booleanValue, persistedValue: booleanValue ? "true" : "false" }]
          : pair.label === "default"
            ? [{ kind: "fallback", officialType: "Microflows$NoCase" }]
            : [],
        edgeKind: booleanCase ? "decisionCondition" : "sequence",
      })];
    });
    const nextSchema = refreshDerivedState({
      ...schema,
      objectCollection: {
        ...schema.objectCollection,
        objects: [...schema.objectCollection.objects, ...objects],
      },
      flows: [...schema.flows, ...flows],
      editor: {
        ...schema.editor,
        selection: {
          ...schema.editor.selection,
          objectId: objects[0]?.id,
          flowId: undefined,
          objectIds: objects.map(object => object.id),
          flowIds: [],
          mode: objects.length > 1 ? "multi" : "single",
        },
      },
    }, metadataForRefresh);
    commitSchema(nextSchema, "insertTemplate", { source: "nodePanel" });
    Toast.success(`${template.name} inserted`);
  };

  const saveCurrentSchema = async (reason: "save" | "saveAndRun" = "save"): Promise<boolean> => {
    if (props.readonly) {
      Toast.warning("Readonly microflow cannot be saved.");
      return false;
    }
    flushPendingMoveHistory();
    if (!dirty) {
      Toast.info("Already saved.");
      return true;
    }
    const saveRevision = schemaRevisionRef.current;
    const schemaToSave = schema;
    if (!isDesignSchema(schemaToSave)) {
      Toast.error("旧版 Authoring 编辑器不再支持保存，请使用新版 FlowGram Studio。");
      return false;
    }
    const validation = await validateForMode(schemaToSave, "save");
    const blockers = validation.issues.filter(issue => issue.blockSave && issue.severity === "error");
    if (blockers.length > 0) {
      setBottomTab("problems");
      Toast.error(`${reason === "saveAndRun" ? "Save & Run" : "Save"} blocked by ${blockers.length} schema-breaking validation error(s).`);
      return false;
    }
    if (validation.summary.errorCount > 0 || validation.summary.warningCount > 0) {
      setBottomTab("problems");
      Toast.warning(`Draft save allowed with ${validation.summary.errorCount} publish blocker(s) and ${validation.summary.warningCount} warning(s).`);
    }
    setSaving(true);
    try {
      const response = await apiClient.saveMicroflow({ schema: schemaToSave });
      props.onSaveComplete?.(response);
      if (schemaRevisionRef.current === saveRevision) {
        savedSchemaRef.current = schemaToSave;
        historyManager.replaceCurrent(schemaToSave, "bulkUpdate");
        refreshHistoryState();
        setDirty(false);
      }
      void runValidationNow(latestSchemaRef.current);
      Toast.success(`Saved ${response.version}`);
      return true;
    } catch (error) {
      applyApiValidationIssues(error, setIssues, () => {
        setBottomTab("problems");
      });
      Toast.error(getEditorApiErrorMessage(error));
      return false;
    } finally {
      setSaving(false);
    }
  };

  const handleSave = async () => {
    await saveCurrentSchema("save");
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
        setBottomTab("problems");
      });
      Toast.error(getEditorApiErrorMessage(error));
    }
  };

  const handleTestRun = async () => {
    if (saving || running || props.readonly || !schema.id) {
      return;
    }
    const validation = await validateForMode(schema, "testRun");
    if (validation.summary.errorCount > 0) {
      setBottomTab("problems");
      Toast.error("Fix validation errors before running.");
      return;
    }
    if (validation.summary.warningCount > 0) {
      Toast.warning(`Test run allowed with ${validation.summary.warningCount} warning(s).`);
    }
    setTestRunModalOpen(true);
  };

  const loadRunHistory = useCallback(async (microflowId: string, status: "all" | "success" | "failed" | "unsupported" | "cancelled" = "all") => {
    const requestSeq = (runHistoryRequestSeqRef.current[microflowId] ?? 0) + 1;
    runHistoryRequestSeqRef.current[microflowId] = requestSeq;
    setRunHistoryLoadingByMicroflowId(current => ({ ...current, [microflowId]: true }));
    setRunHistoryErrorByMicroflowId(current => ({ ...current, [microflowId]: undefined }));
    try {
      const response = await apiClient.listMicroflowRuns(microflowId, { pageIndex: 1, pageSize: 20, status });
      if (runHistoryRequestSeqRef.current[microflowId] !== requestSeq) {
        return;
      }
      setRunHistoryByMicroflowId(current => ({ ...current, [microflowId]: response.items }));
    } catch (error) {
      if (runHistoryRequestSeqRef.current[microflowId] !== requestSeq) {
        return;
      }
      setRunHistoryErrorByMicroflowId(current => ({ ...current, [microflowId]: getEditorApiErrorMessage(error) }));
    } finally {
      if (runHistoryRequestSeqRef.current[microflowId] === requestSeq) {
        setRunHistoryLoadingByMicroflowId(current => ({ ...current, [microflowId]: false }));
      }
    }
  }, [apiClient]);

  const selectRunHistoryItem = useCallback(async (microflowId: string, runId: string) => {
    setSelectedRunIdByMicroflowId(current => ({ ...current, [microflowId]: runId }));
    const cached = runDetailsByRunId[runId];
    if (cached) {
      setRunSessionByMicroflowId(current => ({ ...current, [microflowId]: cached }));
      setActiveTraceFrameId(cached.trace[0]?.id);
      return;
    }
    try {
      const detail = await apiClient.getMicroflowRunDetail(microflowId, runId);
      setRunDetailsByRunId(current => ({ ...current, [runId]: detail }));
      setRunSessionByMicroflowId(current => ({ ...current, [microflowId]: detail }));
      setRuntimeServiceErrorByMicroflowId(current => ({ ...current, [microflowId]: undefined }));
      setActiveTraceFrameId(detail.trace[0]?.id);
      setBottomDockMode("peek");
      setBottomTab("debug");
    } catch (error) {
      setRuntimeServiceErrorByMicroflowId(current => ({ ...current, [microflowId]: getEditorApiErrorMessage(error) }));
      setBottomDockMode("peek");
      setBottomTab("debug");
    }
  }, [apiClient, runDetailsByRunId]);

  const refreshDebugSession = useCallback(async (sessionId: string, microflowId = schema.id) => {
    if (!apiClient.debugAdapter) {
      return undefined;
    }
    const [session, variables] = await Promise.all([
      apiClient.debugAdapter.getSession(sessionId),
      apiClient.debugAdapter.listVariables(sessionId).catch(() => []),
    ]);
    setDebugSessionByMicroflowId(current => ({ ...current, [microflowId]: session }));
    setDebugVariablesBySessionId(current => ({ ...current, [sessionId]: variables }));
    return session;
  }, [apiClient.debugAdapter, schema.id]);

  const startDebugSession = useCallback(async () => {
    if (!apiClient.debugAdapter) {
      setBottomDockMode("peek");
      setBottomTab("debug");
      Toast.info("Step debug adapter is not available; showing trace mode.");
      await handleTestRun();
      return;
    }
    const session = await apiClient.debugAdapter.createSession(schema.id);
    setPendingDebugSessionId(session.id);
    setDebugSessionByMicroflowId(current => ({ ...current, [schema.id]: session }));
    setBottomDockMode("peek");
    setBottomTab("debug");
    setTestRunModalOpen(true);
  }, [apiClient.debugAdapter, schema.id]);

  const handleDebugCommand = useCallback(async (command: MicroflowDebugCommand) => {
    if (!apiClient.debugAdapter || !activeDebugSession) {
      return;
    }
    try {
      const next = await apiClient.debugAdapter.sendCommand(activeDebugSession.id, command);
      setDebugSessionByMicroflowId(current => ({ ...current, [schema.id]: next }));
      await refreshDebugSession(next.id, schema.id);
    } catch (error) {
      Toast.error(getEditorApiErrorMessage(error));
    }
  }, [activeDebugSession, apiClient.debugAdapter, refreshDebugSession, schema.id]);

  const handleDebugEvaluate = useCallback(async (expression: string) => {
    if (!apiClient.debugAdapter || !activeDebugSession || !expression.trim()) {
      return;
    }
    try {
      const watch = await apiClient.debugAdapter.evaluate(activeDebugSession.id, expression);
      setDebugWatchesBySessionId(current => ({
        ...current,
        [activeDebugSession.id]: [watch, ...(current[activeDebugSession.id] ?? []).filter(item => item.expression !== expression)].slice(0, 20),
      }));
    } catch (error) {
      Toast.error(getEditorApiErrorMessage(error));
    }
  }, [activeDebugSession, apiClient.debugAdapter]);

  const handleExecuteTestRun = async (input: MicroflowTestRunInput) => {
    const microflowId = schema.id;
    const validation = await validateForMode(schema, "testRun");
    const gate = shouldBlockRun(validation.issues, {}, dirty, "saveAndRun");
    if (gate.blocked) {
      if (gate.reason === "validation") {
        setBottomTab("problems");
        Toast.error("Fix validation errors before running.");
      }
      return;
    }
    if (dirty) {
      const saved = await saveCurrentSchema("saveAndRun");
      if (!saved) {
        return;
      }
    }
    setRunning(true);
    setSelectedRunIdByMicroflowId(current => ({ ...current, [microflowId]: undefined }));
    setRunSessionByMicroflowId(current => ({ ...current, [microflowId]: undefined }));
    setFocusObjectId(undefined);
    try {
      const debugSessionId = pendingDebugSessionId;
      const response = await apiClient.testRunMicroflow(buildRunRequest(schema, input.parameters, input.options, true, debugSessionId));
      const session = response.session;
      setRuntimeServiceErrorByMicroflowId(current => ({ ...current, [microflowId]: undefined }));
      setRunSessionByMicroflowId(current => ({ ...current, [microflowId]: session }));
      setRunDetailsByRunId(current => ({ ...current, [session.id]: session }));
      setRunHistoryByMicroflowId(current => ({
        ...current,
        [microflowId]: [
          buildRunHistoryItemFromSession(microflowId, session),
          ...(current[microflowId] ?? []).filter(item => item.runId !== session.id),
        ].slice(0, 20) as MicroflowRunHistoryItem[],
      }));
      setSelectedRunIdByMicroflowId(current => ({ ...current, [microflowId]: session.id }));
      setActiveTraceFrameId(response.frames[0]?.id);
      setTestRunModalOpen(false);
      setBottomDockMode("peek");
      setBottomTab("debug");
      if (response.debugSession) {
        setDebugSessionByMicroflowId(current => ({ ...current, [microflowId]: response.debugSession }));
      }
      if (response.debugVariables && (response.debugSession?.id ?? debugSessionId)) {
        const responseDebugSessionId = response.debugSession?.id ?? debugSessionId!;
        setDebugVariablesBySessionId(current => ({ ...current, [responseDebugSessionId]: response.debugVariables ?? [] }));
      }
      if (response.debugTrace && (response.debugSession?.id ?? debugSessionId)) {
        const responseDebugSessionId = response.debugSession?.id ?? debugSessionId!;
        setDebugTraceBySessionId(current => ({ ...current, [responseDebugSessionId]: response.debugTrace ?? [] }));
      }
      if (debugSessionId) {
        setPendingDebugSessionId(undefined);
      }
      const hydration = response.hydration;
      if (hydration?.degraded) {
        setRuntimeServiceErrorByMicroflowId(current => ({
          ...current,
          [microflowId]: hydration.warning ?? "运行会话回读未完全成功，请刷新 Run History 或重新运行。",
        }));
      }
      if (response.runtimeCommands?.length) {
        const parsePayload = (payloadJson?: string) => {
          if (!payloadJson) {
            return undefined;
          }
          try {
            return JSON.parse(payloadJson) as Record<string, unknown>;
          } catch {
            return undefined;
          }
        };

        const runtimeIssues: MicroflowValidationIssue[] = [];
        const deferredMessages: string[] = [];
        for (const command of response.runtimeCommands) {
          const payload = parsePayload(command.payloadJson);
          if (command.commandKind === "showMessage") {
            const level = typeof payload?.messageType === "string"
              ? payload.messageType
              : typeof payload?.level === "string"
                ? payload.level
                : "info";
            const message =
              typeof payload?.message === "string" ? payload.message
                : typeof payload?.messageExpression === "string" ? payload.messageExpression
                  : typeof payload?.template === "string" ? payload.template
                    : `Runtime command: ${command.commandKind}`;
            Toast[(level === "error" || level === "warning" || level === "success") ? level : "info"](message);
            continue;
          }

          if (command.commandKind === "validationFeedback") {
            runtimeIssues.push({
              id: `runtime-command:${microflowId}:${command.sourceObjectId ?? "unknown"}:${command.commandKind}`,
              microflowId,
              code: "RUNTIME_VALIDATION_FEEDBACK",
              severity: "warning",
              message: typeof payload?.feedbackMessage === "string"
                ? payload.feedbackMessage
                : typeof payload?.message === "string"
                  ? payload.message
                  : "Runtime validation feedback received.",
              source: "runtimeCommand",
              objectId: command.sourceObjectId,
              actionId: command.sourceActionId,
              fieldPath: typeof payload?.targetPath === "string" ? payload.targetPath : undefined,
              blockPublish: false,
            } satisfies MicroflowValidationIssue);
            continue;
          }

          deferredMessages.push(command.commandKind);
        }

        if (runtimeIssues.length > 0) {
          setIssues([...issues, ...runtimeIssues]);
          setBottomTab("problems");
        }

        if (deferredMessages.length > 0) {
          setRuntimeServiceErrorByMicroflowId(current => ({
            ...current,
            [microflowId]: `这些 runtime commands 已生成但前端尚未完整消费：${deferredMessages.join(", ")}`,
          }));
        }
      }
      props.onTestRunComplete?.(response);
      void loadRunHistory(microflowId, runHistoryFilter);
      Toast[response.status === "succeeded" ? "success" : "error"](
        response.hydration?.degraded ? `Run ${response.status} (degraded hydration)` : `Run ${response.status}`
      );
    } catch (error) {
      applyApiValidationIssues(error, setIssues, () => {
        setBottomTab("problems");
      });
      setRunSessionByMicroflowId(current => ({ ...current, [microflowId]: undefined }));
      setActiveTraceFrameId(undefined);
      setRuntimeServiceErrorByMicroflowId(current => ({ ...current, [microflowId]: getEditorApiErrorMessage(error) }));
      setBottomDockMode("peek");
      setBottomTab("debug");
      Toast.error(getEditorApiErrorMessage(error));
    } finally {
      setRunning(false);
    }
  };

  const clearTestRun = () => {
    const debugSessionId = debugSessionByMicroflowId[activeMicroflowId]?.id;
    if (debugSessionId && apiClient.debugAdapter) {
      void apiClient.debugAdapter.deleteSession(debugSessionId).catch(() => undefined);
    }
    setRunSessionByMicroflowId(current => ({ ...current, [activeMicroflowId]: undefined }));
    setRuntimeServiceErrorByMicroflowId(current => ({ ...current, [activeMicroflowId]: undefined }));
    setSelectedRunIdByMicroflowId(current => ({ ...current, [activeMicroflowId]: undefined }));
    setPendingDebugSessionId(undefined);
    setDebugSessionByMicroflowId(current => ({ ...current, [activeMicroflowId]: undefined }));
    setActiveTraceFrameId(undefined);
    setFocusObjectId(undefined);
  };

  const cancelTestRun = async () => {
    if (!runSession?.id) {
      return;
    }
    const microflowId = schema.id;
    setRunning(true);
    try {
      const cancelResult = await apiClient.cancelMicroflowRun(runSession.id);
      const nextSession = await apiClient.getMicroflowRunDetail(microflowId, runSession.id);
      const nextTrace = await apiClient.getMicroflowRunTrace(runSession.id);
      const session = { ...nextSession, trace: nextTrace };
      setRuntimeServiceErrorByMicroflowId(current => ({ ...current, [microflowId]: undefined }));
      setRunSessionByMicroflowId(current => ({ ...current, [microflowId]: session }));
      setRunDetailsByRunId(current => ({ ...current, [session.id]: session }));
      setRunHistoryByMicroflowId(current => ({
        ...current,
        [microflowId]: [
          buildRunHistoryItemFromSession(microflowId, session),
          ...(current[microflowId] ?? []).filter(item => item.runId !== session.id),
        ].slice(0, 20) as MicroflowRunHistoryItem[],
      }));
      setActiveTraceFrameId(current => current ?? nextTrace[0]?.id);
      Toast.info(`Run ${cancelResult?.status ?? session.status}`);
    } catch (error) {
      setRuntimeServiceErrorByMicroflowId(current => ({ ...current, [microflowId]: getEditorApiErrorMessage(error) }));
      setBottomDockMode("peek");
      setBottomTab("debug");
      Toast.error(getEditorApiErrorMessage(error));
    } finally {
      setRunning(false);
    }
  };

  const selectTraceFrame = (frame: MicroflowTraceFrame) => {
    setActiveTraceFrameId(frame.id);
    setRightOpen(true);
    const targetObjectId = !frame.microflowId || frame.microflowId === schema.id
      ? frame.objectId
      : frame.callerObjectId;
    if (!targetObjectId) {
      Toast.info("该节点属于子微流，请在子微流画布中定位。");
      return;
    }
    const targetPosition = graph.nodes.find(item => item.objectId === targetObjectId)?.position;
    if (!targetPosition) {
      Toast.info("当前画布未找到该节点，可能属于子微流。");
      return;
    }
    setFocusObjectId(targetObjectId);
    setFocusRequestSeq(value => value + 1);
    applyPatch({
      selectedObjectId: targetObjectId,
      selectedFlowId: undefined,
      selectedCollectionId: frame.collectionId ?? findObjectWithCollection(schema, targetObjectId)?.collectionId,
      viewport: viewportCenteredOn(targetPosition),
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
      const frame = selectedRunSession?.trace.find(item => item.objectId === error.objectId);
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

  const viewportCenteredOn = (target?: { x: number; y: number }): MicroflowSchema["editor"]["viewport"] | undefined => {
    if (!target) {
      return undefined;
    }
    const zoom = Math.max(0.6, schema.editor.viewport?.zoom ?? 1);
    return {
      x: Math.round(360 - target.x * zoom),
      y: Math.round(260 - target.y * zoom),
      zoom,
    };
  };

  const viewportForProblemIssue = (issue: MicroflowValidationIssue): MicroflowSchema["editor"]["viewport"] | undefined => {
    const flowId = issue.flowId ?? issue.edgeId;
    if (flowId) {
      const edge = graph.edges.find(item => item.flowId === flowId);
      const source = edge ? graph.nodes.find(item => item.objectId === edge.sourceObjectId) : undefined;
      const target = edge ? graph.nodes.find(item => item.objectId === edge.targetObjectId) : undefined;
      if (source && target) {
        return viewportCenteredOn({
          x: (source.position.x + target.position.x) / 2,
          y: (source.position.y + target.position.y) / 2,
        });
      }
    }
    const objectId = issue.objectId ?? issue.nodeId;
    return viewportCenteredOn(graph.nodes.find(item => item.objectId === objectId)?.position);
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
    const flowIds = [...new Set([...(selection.flowIds ?? []), selection.flowId].filter((id): id is string => Boolean(id)))];
    const objectIds = [...new Set([...(selection.objectIds ?? []), selection.objectId].filter((id): id is string => Boolean(id)))];
    if (flowIds.length > 1 || objectIds.length > 1 || (flowIds.length + objectIds.length) > 1) {
      let next = schema;
      for (const flowId of flowIds) {
        if (findFlowWithCollection(next, flowId)) {
          next = deleteFlow(next, flowId);
        }
      }
      for (const objectId of objectIds) {
        if (findObjectWithCollection(next, objectId)) {
          next = deleteObject(next, objectId);
        }
      }
      commitSchema(next, "bulkUpdate", { historyLabel: "Delete selection", source: "flowgram" });
      return;
    }
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

  const handleCopySelection = () => {
    const selection = schema.editor.selection;
    const objectIds = [...new Set([...(selection.objectIds ?? []), selection.objectId].filter((id): id is string => Boolean(id && findObject(schema, id))))];
    const flowIds = [...new Set([...(selection.flowIds ?? []), selection.flowId].filter((id): id is string => Boolean(id && findFlowWithCollection(schema, id))))];
    if (objectIds.length === 0) {
      Toast.info("请选择一个节点后复制。");
      return;
    }
    setClipboardObject({ microflowId: schema.id, objectIds, flowIds });
    Toast.success(objectIds.length > 1 ? `已复制 ${objectIds.length} 个节点。` : "已复制节点。");
  };

  const handlePasteSelection = () => {
    if (props.readonly) {
      return;
    }
    if (!clipboardObject) {
      Toast.info("没有可粘贴的节点。");
      return;
    }
    if (clipboardObject.microflowId !== schema.id) {
      Toast.warning("本轮暂不支持跨微流粘贴，请在源微流内粘贴。");
      return;
    }
    const source = findObjectWithCollection(schema, clipboardObject.objectIds[0]);
    if (!source) {
      Toast.warning("复制的源节点已不存在。");
      return;
    }
    const nextSchema = clipboardObject.objectIds.length > 1 || clipboardObject.flowIds.length > 0
      ? duplicateObjectSelection(schema, clipboardObject)
      : duplicateObject(schema, clipboardObject.objectIds[0]);
    commitSchema(nextSchema, source.parentLoopObjectId ? "addLoopNode" : "addNode", { historyLabel: "Paste selection", source: "flowgram" });
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
    if ((event.ctrlKey || event.metaKey) && key === "k") {
      event.preventDefault();
      setCommandPaletteQuery("");
      setCommandPaletteOpen(true);
      return;
    }
    if ((event.ctrlKey || event.metaKey) && key === "0") {
      event.preventDefault();
      window.dispatchEvent(new CustomEvent("atlas:microflow-flowgram-fit-view"));
      return;
    }
    if ((event.ctrlKey || event.metaKey) && key === "1") {
      event.preventDefault();
      applyPatch({ viewport: { ...schema.editor.viewport, zoom: 1 } }, { pushHistory: false, skipDirty: true, skipValidate: true, preserveSelection: true, source: "flowgram" });
      return;
    }
    if ((event.ctrlKey || event.metaKey) && key === "c" && !props.readonly) {
      event.preventDefault();
      handleCopySelection();
      return;
    }
    if ((event.ctrlKey || event.metaKey) && key === "v" && !props.readonly) {
      event.preventDefault();
      handlePasteSelection();
      return;
    }
    if ((key === "delete" || key === "backspace") && !props.readonly) {
      event.preventDefault();
      handleDeleteSelection();
      return;
    }
    if (key === "escape") {
      event.preventDefault();
      clearSelection();
      return;
    }
  };

  useEffect(() => () => {
    if (moveHistoryTimerRef.current !== undefined) {
      window.clearTimeout(moveHistoryTimerRef.current);
    }
  }, []);

  useEffect(() => {
    if (externalLayout) {
      const timer = window.setTimeout(() => {
        writeMendixLayoutStorage({
          nodesDrawerOpen: leftOpen,
          inspectorOpen: rightOpen,
          inspectorMode: "floating",
          inspectorWidth: RIGHT_PANEL_EXPANDED_PX,
          bottomOpen,
          bottomMode: bottomDockMode,
          bottomHeight: bottomDockHeight,
          activeBottomTab: bottomTab,
          focusMode,
          minimapVisible: schema.editor.showMiniMap === true,
          gridVisible: schema.editor.gridEnabled !== false
        });
      }, 160);
      return () => window.clearTimeout(timer);
    }
    if (!persistAuxPanelState) {
      return undefined;
    }
    writeStoredBoolean(leftPanelStorageKey, leftOpen);
    writeStoredBoolean(rightPanelStorageKey, rightOpen);
    writeStoredBoolean(rightPanelPinnedStorageKey, rightPinned);
    writeStoredBottomDockMode(bottomPanelStorageKey, bottomDockMode);
    writeStoredBottomTab(bottomTab);
    return undefined;
  }, [bottomDockHeight, bottomDockMode, bottomOpen, bottomTab, externalLayout, focusMode, leftOpen, persistAuxPanelState, rightOpen, rightPinned, schema.editor.gridEnabled, schema.editor.showMiniMap]);

  useEffect(() => {
    if (!schema.id) {
      return;
    }
    void loadRunHistory(schema.id, runHistoryFilter);
  }, [schema.id, runHistoryFilter, loadRunHistory]);

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

  const openBottomDock = (tab: MicroflowWorkbenchBottomTab, mode: Exclude<BottomDockMode, "collapsed"> = "peek") => {
    setBottomTab(tab);
    setBottomDockMode(current => current === "full" ? "full" : mode);
  };

  const toggleBottomDock = () => {
    setBottomDockMode(current => current === "collapsed" ? "peek" : "collapsed");
  };

  const startBottomDockResize = (event: PointerEvent<HTMLDivElement>) => {
    if (bottomDockMode !== "full") {
      return;
    }
    event.preventDefault();
    const startY = event.clientY;
    const startHeight = bottomDockHeight;
    const handlePointerMove = (moveEvent: globalThis.PointerEvent) => {
      setBottomDockHeight(clampBottomDockHeight(startHeight - (moveEvent.clientY - startY)));
    };
    const handlePointerUp = () => {
      window.removeEventListener("pointermove", handlePointerMove);
      window.removeEventListener("pointerup", handlePointerUp);
    };
    window.addEventListener("pointermove", handlePointerMove);
    window.addEventListener("pointerup", handlePointerUp);
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
    if (canvasNodeContextMenu) {
      setCanvasNodeContextMenu(undefined);
      return;
    }
    if (rightOpen) {
      setRightOpen(false);
      return;
    }
    if (bottomDockMode === "full") {
      setBottomDockMode("peek");
      return;
    }
    if (bottomDockMode === "peek") {
      setBottomDockMode("collapsed");
      return;
    }
    applyPatch(
      {
        selectedObjectId: undefined,
        selectedFlowId: undefined,
        selectedCollectionId: undefined,
        selectedObjectIds: [],
        selectedFlowIds: [],
        selectionMode: "none",
      },
      { pushHistory: false, skipDirty: true, skipValidate: true },
    );
  }, [bottomDockMode, canvasNodeContextMenu, rightOpen, schema]);

  const resetWorkbenchLayout = useCallback(() => {
    setFocusMode(false);
    setFullscreenActive(false);
    setLeftOpen(true);
    setRightOpen(Boolean(props.defaultRightPanelOpen ?? props.immersive));
    setRightPinned(false);
    setBottomDockHeight(BOTTOM_DOCK_FULL_DEFAULT_PX);
    setBottomDockMode(bottomPanelFallbackMode);
    setBottomTab("problems");
    commitSchema(
      {
        ...schema,
        editor: {
          ...schema.editor,
          showMiniMap: false,
          gridEnabled: true,
        },
      },
      "bulkUpdate",
      { historyLabel: "Reset workbench layout", skipDirty: true, skipValidate: true, preserveSelection: true, source: "flowgram" }
    );
  }, [bottomPanelFallbackMode, commitSchema, props.defaultRightPanelOpen, props.immersive, schema]);

  useMicroflowShortcuts({
    containerRef: shellRef,
    readonly: props.readonly,
    onUndo: handleUndo,
    onRedo: handleRedo,
    onSave: () => void handleSave(),
    onSearch: focusNodeSearch,
    onCopySelection: handleCopySelection,
    onPasteSelection: handlePasteSelection,
    onDeleteSelection: handleDeleteSelection,
    onEscape: clearSelection,
    onFocusMode: () => setFocusMode(value => !value),
  });

  const [fullscreenActive, setFullscreenActive] = useState(false);
  const layoutState = useMemo<MicroflowWorkbenchLayoutState>(() => ({
    shellMode,
    activeBottomTab: bottomTab,
    bottomDockMode,
    focusMode,
    minimapVisible: schema.editor.showMiniMap === true,
    gridVisible: schema.editor.gridEnabled !== false,
  }), [bottomDockMode, bottomTab, focusMode, schema.editor.gridEnabled, schema.editor.showMiniMap, shellMode]);
  const workbenchStatus = useMemo<MicroflowWorkbenchStatus>(() => {
    const currentRunSession = selectedRunSession ?? runSession;
    return {
      microflowId: schema.id,
      schemaVersion: schema.schemaVersion,
      dirty,
      saving,
      running,
      validationStatus,
      errorCount: issues.filter(issue => issue.severity === "error").length,
      warningCount: issues.filter(issue => issue.severity === "warning").length,
      canUndo: historyState.canUndo,
      canRedo: historyState.canRedo,
      zoomPercent: Math.round((schema.editor.viewport?.zoom ?? schema.editor.zoom ?? 1) * 100),
      hasRunSession: Boolean(currentRunSession),
      fullscreen: fullscreenActive || focusMode,
      activeBottomTab: bottomTab,
      bottomDockMode,
      sessionHydrated: currentRunSession?.hasHydratedTrace ?? Boolean(currentRunSession?.persistedAt),
      traceHydrated: currentRunSession?.hasHydratedTrace ?? false,
      debugSessionHydrated: Boolean(activeDebugSession?.lastUpdatedAt),
      degradedRunSession: Boolean(currentRunSession && currentRunSession.hasHydratedTrace === false),
      layout: layoutState,
    };
  }, [activeDebugSession?.lastUpdatedAt, bottomDockMode, bottomTab, dirty, focusMode, fullscreenActive, historyState.canRedo, historyState.canUndo, issues, layoutState, runSession, running, saving, schema.editor.viewport, schema.editor.zoom, schema.id, schema.schemaVersion, selectedRunSession, validationStatus]);

  useEffect(() => {
    props.onLayoutStateChange?.(layoutState);
  }, [layoutState, props.onLayoutStateChange]);

  useEffect(() => {
    props.onWorkbenchStatusChange?.(workbenchStatus);
  }, [props.onWorkbenchStatusChange, workbenchStatus]);

  // External hosts ride imperative handle; internal mode also receives the handle so
  // higher-level UIs can opt-in without forcing internal toolbar to disappear.
  useImperativeHandle(props.editorRef, () => ({
    save: async () => {
      try {
        await handleSave();
      } catch (error) {
        Toast.error(getEditorApiErrorMessage(error));
      }
    },
    validate: async () => {
      try {
        await handleValidate();
      } catch (error) {
        Toast.error(getEditorApiErrorMessage(error));
      }
    },
    runTest: async () => {
      try {
        await handleTestRun();
      } catch (error) {
        Toast.error(getEditorApiErrorMessage(error));
      }
    },
    runDebug: async () => {
      try {
        setBottomDockMode("peek");
        setBottomTab("debug");
        await startDebugSession();
      } catch (error) {
        Toast.error(getEditorApiErrorMessage(error));
      }
    },
    publish: async () => {
      if (!props.onPublish) {
        Toast.warning("Publish handler is not configured for this editor.");
        return;
      }
      try {
        await props.onPublish(schema);
      } catch (error) {
        Toast.error(getEditorApiErrorMessage(error));
      }
    },
    undo: handleUndo,
    redo: handleRedo,
    autoLayout: handleAutoLayout,
    fitView: () => {
      window.dispatchEvent(new CustomEvent("atlas:microflow-flowgram-fit-view"));
    },
    zoomIn: () => {
      window.dispatchEvent(new CustomEvent("atlas:microflow-flowgram-zoom", { detail: { delta: 0.1 } }));
    },
    zoomOut: () => {
      window.dispatchEvent(new CustomEvent("atlas:microflow-flowgram-zoom", { detail: { delta: -0.1 } }));
    },
    setZoom: (zoom: number) => {
      window.dispatchEvent(new CustomEvent("atlas:microflow-flowgram-zoom", { detail: { zoom } }));
    },
    toggleFullscreen: () => {
      setFocusMode(value => !value);
      setFullscreenActive(value => !value);
      const target = shellRef.current;
      if (!target) {
        return;
      }
      if (document.fullscreenElement === target) {
        void document.exitFullscreen();
      } else {
        void target.requestFullscreen?.();
      }
    },
    toggleFocusMode: () => {
      setFocusMode(value => !value);
    },
    toggleMinimap: () => {
      const next = !schema.editor.showMiniMap;
      commitSchema(
        { ...schema, editor: { ...schema.editor, showMiniMap: next } },
        "bulkUpdate",
        { historyLabel: "Toggle minimap", skipValidate: true, preserveSelection: true, source: "flowgram" }
      );
    },
    resetLayout: resetWorkbenchLayout,
    openBottomTab: (tab: MicroflowWorkbenchBottomTab) => {
      openBottomDock(tab);
    },
    setBottomDockMode: (mode: BottomDockMode) => {
      setBottomDockMode(mode);
    },
    getLayoutState: () => layoutState,
    getStatus: () => workbenchStatus,
  // The handle reads many derived values; React will re-create the impl each
  // render so callers always observe fresh values.
  }), [commitSchema, dirty, focusMode, fullscreenActive, handleSave, handleUndo, handleRedo, handleValidate, handleTestRun, handleAutoLayout, historyState.canRedo, historyState.canUndo, issues, labels.debug, layoutState, openBottomDock, props.onPublish, props.readonly, resetWorkbenchLayout, runSession, running, saving, schema, startDebugSession, validationStatus, workbenchStatus]);

  const commandItems = useMemo<MicroflowCommandItem[]>(() => [
    { id: "save", label: "Save", disabled: props.readonly || saving, run: () => void handleSave() },
    { id: "validate", label: "Validate", run: () => void handleValidate() },
    { id: "run", label: "Run Test", disabled: running, run: () => void handleTestRun() },
    { id: "debug", label: "Run Debug", disabled: running, run: () => void startDebugSession() },
    { id: "problems", label: "Open Problems", run: () => { setBottomDockMode("peek"); setBottomTab("problems"); } },
    { id: "properties", label: rightOpen ? "Hide Properties" : "Show Properties", run: () => setRightOpen(open => !open) },
    { id: "toolbox", label: leftOpen ? "Hide Toolbox" : "Show Toolbox", run: () => setLeftOpen(open => !open) },
    { id: "undo", label: "Undo", disabled: !historyState.canUndo, run: handleUndo },
    { id: "redo", label: "Redo", disabled: !historyState.canRedo, run: handleRedo },
    { id: "fit-view", label: "Fit View", run: () => window.dispatchEvent(new CustomEvent("atlas:microflow-flowgram-fit-view")) },
    { id: "zoom-100", label: "Zoom 100%", run: () => applyPatch({ viewport: { ...schema.editor.viewport, zoom: 1 } }, { pushHistory: false, skipDirty: true, skipValidate: true, preserveSelection: true, source: "flowgram" }) },
    { id: "auto-layout", label: "Auto Layout", disabled: props.readonly, run: handleAutoLayout },
    { id: "focus-mode", label: focusMode ? "Exit Focus Mode" : "Enter Focus Mode", run: () => setFocusMode(value => !value) },
  ], [dirty, focusMode, handleSave, historyState.canRedo, historyState.canUndo, leftOpen, props.readonly, rightOpen, running, saving, schema.editor.viewport, startDebugSession]);

  return (
    <div ref={shellRef} data-testid="microflow-editor-shell" data-microflow-id={schema.id} style={shellStyle} tabIndex={0}>
      <MicroflowCommandPalette
        visible={commandPaletteOpen}
        query={commandPaletteQuery}
        commands={commandItems}
        onQueryChange={setCommandPaletteQuery}
        onClose={() => setCommandPaletteOpen(false)}
      />
      {toolbarMode === "internal" ? (
      <div data-testid="microflow-editor-toolbar" style={toolbarStyle}>
        <Space style={{ minWidth: 0, overflow: "hidden" }}>
          {props.toolbarPrefix}
          <Title heading={5} style={{ margin: 0 }}>{schema.displayName || schema.name}</Title>
          <Tag>{schema.schemaVersion}</Tag>
          {dirty ? <Tag color="orange">dirty</Tag> : null}
          <Tag color={validationStatus === "validating" ? "blue" : issues.some(issue => issue.severity === "error") ? "red" : "green"}>
            {validationStatus === "validating" ? "validating..." : `${issues.length} issues`}
          </Tag>
          {saveBlockers.length > 0 ? <Tag color="red">Save blocked {saveBlockers.length}</Tag> : null}
          {publishBlockers.length > 0 ? <Tag color="red">Publish blocked by {publishBlockers.length} errors</Tag> : null}
          {runSession ? <Tag color={runSession.status === "success" ? "green" : "red"}>{runSession.status} · {runSession.trace.length} frames</Tag> : null}
        </Space>
        <Space wrap style={{ justifyContent: "flex-end", rowGap: 4 }}>
          <Tooltip content={historyState.canUndo ? labels.undo : "No history to undo"}>
            <Button data-testid="microflow-editor-undo" aria-label={labels.undo} icon={<IconUndo />} disabled={!historyState.canUndo} onClick={handleUndo} />
          </Tooltip>
          <Tooltip content={historyState.canRedo ? labels.redo : "No history to redo"}>
            <Button data-testid="microflow-editor-redo" aria-label={labels.redo} icon={<IconRedo />} disabled={!historyState.canRedo} onClick={handleRedo} />
          </Tooltip>
          <Tooltip content={labels.validate}>
            <Button data-testid="microflow-editor-validate" aria-label={labels.validate} icon={<IconRefresh />} loading={validationStatus === "validating"} onClick={handleValidate}>{labels.validate}</Button>
          </Tooltip>
          <Tooltip content={dirty ? "Save & Run opens the input panel" : labels.testRun}>
            <Button data-testid="microflow-editor-run" aria-label={labels.testRun} icon={<IconPlay />} loading={running} disabled={saving || props.readonly || !schema.id} onClick={handleTestRun}>
              {dirty ? "Save & Run" : labels.testRun}
            </Button>
          </Tooltip>
          <Tooltip content={dirty ? labels.save : "No unsaved changes"}>
            <Button data-testid="microflow-editor-save" aria-label={labels.save} icon={<IconSave />} loading={saving} disabled={saving || props.readonly || !dirty || !schema.id} type="primary" onClick={handleSave}>{labels.save}</Button>
          </Tooltip>
          <Dropdown
            trigger="click"
            position="bottomRight"
            render={(
              <Dropdown.Menu>
                <Dropdown.Item icon={<IconCopy />} disabled={props.readonly || !schema.editor.selection.objectId && !(schema.editor.selection.objectIds?.length)} onClick={handleCopySelection}>复制节点</Dropdown.Item>
                <Dropdown.Item icon={<IconCopy />} disabled={props.readonly || !clipboardObject} onClick={handlePasteSelection}>粘贴节点</Dropdown.Item>
                <Dropdown.Item icon={<IconDelete />} disabled={props.readonly || !schema.editor.selection.objectId && !schema.editor.selection.flowId && !(schema.editor.selection.objectIds?.length) && !(schema.editor.selection.flowIds?.length)} onClick={handleDeleteSelection}>删除选择</Dropdown.Item>
                <Dropdown.Item icon={<IconRefresh />} onClick={handleAutoLayout}>{labels.format}</Dropdown.Item>
                <Dropdown.Item onClick={focusNodeSearch}>搜索节点</Dropdown.Item>
                <Dropdown.Item onClick={() => setLeftOpen(open => !open)}>{leftOpen ? "折叠节点面板" : "展开节点面板"}</Dropdown.Item>
                <Dropdown.Item onClick={() => setRightOpen(open => !open)}>{rightOpen ? "折叠属性面板" : "展开属性面板"}</Dropdown.Item>
                <Dropdown.Item onClick={toggleBottomDock}>{bottomOpen ? "折叠底部 Dock" : "展开底部 Dock"}</Dropdown.Item>
                {runSession ? <Dropdown.Item type="danger" icon={<IconDelete />} onClick={clearTestRun}>清空调试</Dropdown.Item> : null}
              </Dropdown.Menu>
            )}
          >
            <Button data-testid="microflow-editor-more" aria-label={labels.more} icon={<IconMore />} theme="borderless">{labels.more}</Button>
          </Dropdown>
          {props.toolbarSuffix}
        </Space>
      </div>
      ) : null}
      <div style={bodyStyle}>
        {!focusMode && leftOpen ? (
          <div data-testid="microflow-editor-left-panel" style={panelStyle}>
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
              onInsertTemplate={handleInsertTemplate}
              onShowDocumentation={item => Modal.info({ title: item.title, content: item.documentation.summary })}
              labels={props.nodePanelLabels}
              createContext={{ microflowId: schema.id, moduleId: schema.moduleId, metadataAvailable: Boolean(loadedMetadata), schemaLoaded: true, readonly: props.readonly }}
            />
          </div>
        ) : !focusMode ? (
          <button
            type="button"
            data-testid="microflow-node-panel-rail"
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
        ) : null}
        <div data-testid="microflow-canvas" style={{ minWidth: 0, minHeight: 0, display: "contents" }}>
        <FlowGramMicroflowCanvas
          schema={schema}
          validationIssues={issues}
          runtimeTrace={traceFrames}
          focusObjectId={focusObjectId}
          focusRequestKey={focusRequestSeq}
          readonly={props.readonly}
          onSchemaChange={(nextSchema, reason) => {
            commitSchema(nextSchema, reason, { source: "flowgram", skipValidate: true });
          }}
          onSelectionChange={selection => {
            setCanvasNodeContextMenu(undefined);
            applyPatch({
              selectedObjectId: selection.objectId,
              selectedFlowId: selection.flowId,
              selectedCollectionId: selection.collectionId,
              selectedObjectIds: selection.objectIds,
              selectedFlowIds: selection.flowIds,
              selectionMode: selection.mode,
            }, { pushHistory: false, skipDirty: true, skipValidate: true, source: "flowgram" });
          }}
          onCanvasBlankClick={() => {
            setCanvasNodeContextMenu(undefined);
            applyPatch({
              selectedObjectId: undefined,
              selectedFlowId: undefined,
              selectedCollectionId: undefined,
              selectedObjectIds: [],
              selectedFlowIds: [],
              selectionMode: "none",
            }, { pushHistory: false, skipDirty: true, skipValidate: true, source: "flowgram" });
          }}
          onNodeContextMenu={(selection, point) => {
            setCanvasNodeContextMenu(selection.objectId ? {
              objectId: selection.objectId,
              collectionId: selection.collectionId,
              x: point.x,
              y: point.y,
            } : undefined);
          }}
          onDropRegistryItem={(item, position, payload, options) => handleAddNode(item, {
            position,
            payload,
            source: "drop",
            parentLoopObjectId: options?.parentLoopObjectId,
          })}
          canUndo={historyState.canUndo}
          canRedo={historyState.canRedo}
          onUndo={handleUndo}
          onRedo={handleRedo}
          onAutoLayout={handleAutoLayout}
          onViewportChange={(viewport, options) => {
            commitSchema(
              { ...schema, editor: { ...schema.editor, viewport, zoom: viewport.zoom } },
              "bulkUpdate",
              { pushHistory: false, historyLabel: "Update viewport", skipDirty: options?.skipDirty, skipValidate: true, preserveSelection: true, source: "flowgram" },
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
          dirty={dirty}
          saving={saving}
          validating={validationStatus === "validating"}
          onOpenProblemsPanel={() => {
            openBottomDock("problems");
          }}
        />
        </div>
        {canvasNodeContextMenu ? (
          <div
            data-testid="microflow-canvas-node-context-menu"
            style={{
              position: "fixed",
              left: canvasNodeContextMenu.x,
              top: canvasNodeContextMenu.y,
              zIndex: 1200,
              minWidth: 152,
              padding: 6,
              borderRadius: 8,
              border: "1px solid var(--semi-color-border, #e5e6eb)",
              background: "var(--semi-color-bg-2, #fff)",
              boxShadow: "0 8px 24px rgba(31, 35, 41, 0.14)"
            }}
            onClick={event => event.stopPropagation()}
            onContextMenu={event => event.preventDefault()}
          >
            <Button
              block
              size="small"
              theme="borderless"
              type="tertiary"
              icon={<IconSetting />}
              style={{ justifyContent: "flex-start" }}
              onClick={() => {
                applyPatch({
                  selectedObjectId: canvasNodeContextMenu.objectId,
                  selectedFlowId: undefined,
                  selectedCollectionId: canvasNodeContextMenu.collectionId,
                }, { pushHistory: false, skipDirty: true, skipValidate: true, source: "flowgram" });
                setFocusMode(false);
                setRightOpen(true);
                setCanvasNodeContextMenu(undefined);
              }}
            >
              {labels.properties}
            </Button>
          </div>
        ) : null}
        {!focusMode ? <div
          data-testid="microflow-editor-right-shell"
          style={{
            display: "flex",
            flexDirection: "row",
            height: "100%",
            minHeight: 0,
            overflow: "hidden",
            background: "var(--semi-color-bg-1, #fff)",
            ...(externalLayout
              ? {
                position: "absolute",
                top: 0,
                right: 0,
                bottom: 0,
                width: rightOpen ? RIGHT_PANEL_EXPANDED_PX + RAIL_WIDTH_PX : RAIL_WIDTH_PX,
                zIndex: 22,
                boxShadow: rightOpen ? "0 12px 32px rgba(31, 35, 41, 0.14)" : undefined
              } satisfies CSSProperties
              : {})
          }}
        >
          {rightOpen ? (
            <div data-testid="microflow-property-panel" style={propertyPaneStyle}>
              <div
                style={{
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "space-between",
                  gap: 8,
                  padding: "0 0 8px",
                  marginBottom: 8,
                  borderBottom: "1px solid var(--semi-color-border, #e5e6eb)"
                }}
              >
                <Space spacing={6}>
                  <IconSetting />
                  <Text strong>{labels.properties}</Text>
                </Space>
                <Space spacing={4}>
                  <Button
                    size="small"
                    theme={rightPinned ? "solid" : "borderless"}
                    type="tertiary"
                    onClick={() => setRightPinned(value => !value)}
                  >
                    {rightPinned ? "已固定" : "固定"}
                  </Button>
                  <Button
                    aria-label="关闭属性面板"
                    size="small"
                    theme="borderless"
                    icon={<IconClose />}
                    onClick={() => setRightOpen(false)}
                  />
                </Space>
              </div>
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
                  onClose={() => {
                    applyPatch({ selectedObjectId: undefined, selectedFlowId: undefined }, { pushHistory: false, skipDirty: true, skipValidate: true });
                    if (!rightPinned) {
                      setRightOpen(false);
                    }
                  }}
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
        </div> : null}
      </div>
      {!focusMode && bottomOpen ? (
        <div
          data-testid="microflow-bottom-panel"
          style={{
            position: "absolute",
            left: 0,
            right: 0,
            bottom: BOTTOM_STRIP_HEIGHT_PX,
            zIndex: 28,
            height: activeBottomDockHeight,
            minHeight: 0,
            borderTop: "1px solid var(--semi-color-border, #e5e6eb)",
            background: "var(--semi-color-bg-1, #fff)",
            overflow: "hidden",
            padding: "8px 12px 12px",
            display: "flex",
            flexDirection: "column",
            boxShadow: "0 -8px 24px rgba(31, 35, 41, 0.08)"
          }}
        >
          {bottomDockMode === "full" ? (
            <div
              aria-label="调整底部 Dock 高度"
              role="separator"
              style={{
                position: "absolute",
                left: 0,
                right: 0,
                top: 0,
                height: 6,
                cursor: "row-resize"
              }}
              onPointerDown={startBottomDockResize}
            />
          ) : null}
          <Tabs
            type="line"
            activeKey={bottomTab}
            onChange={key => setBottomTab(key as MicroflowWorkbenchBottomTab)}
            tabBarExtraContent={
              <Space spacing={4}>
                <Button
                  aria-label={bottomDockMode === "full" ? "恢复底部 Dock" : "展开底部 Dock"}
                  size="small"
                  theme="borderless"
                  icon={bottomDockMode === "full" ? <IconChevronDown /> : <IconExpand />}
                  onClick={() => setBottomDockMode(mode => mode === "full" ? "peek" : "full")}
                />
                <Button
                  aria-label="折叠底部 Dock"
                  size="small"
                  theme="borderless"
                  icon={<IconClose />}
                  onClick={() => setBottomDockMode("collapsed")}
                />
              </Space>
            }
            style={{ flex: 1, minHeight: 0 }}
          >
            <Tabs.TabPane tab={<Space><IconTickCircle />{labels.problems}<Badge count={issues.length} /></Space>} itemKey="problems">
              <div style={{ overflow: "auto", maxHeight: activeBottomDockHeight - 56 }}>
                <ProblemPanel
                  issues={issues}
                  status={validationStatus}
                  lastValidatedAt={lastValidatedAt}
                  lastError={lastError}
                  onRetry={() => void runValidationNow(schema)}
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
                      { selectedObjectId, selectedFlowId, selectedCollectionId, viewport: viewportForProblemIssue(issue) },
                      { pushHistory: false, skipDirty: true, skipValidate: true },
                    );
                  }}
                />
              </div>
            </Tabs.TabPane>
            <Tabs.TabPane tab={labels.debug} itemKey="debug">
              <div style={{ overflow: "auto", maxHeight: activeBottomDockHeight - 56 }}>
                <Tabs type="card" defaultActiveKey="trace">
                  <Tabs.TabPane tab={<span data-testid="microflow-trace-tab-label">Trace</span>} itemKey="trace">
                    <div data-testid="microflow-trace-panel" data-run-id={selectedRunSession?.id} data-run-status={selectedRunSession?.status} data-trace-count={selectedRunSession?.trace.length ?? 0}>
                    <DebugPanel
                      microflowId={schema.id}
                      microflowName={schema.displayName || schema.name}
                      session={selectedRunSession}
                      serviceError={runtimeServiceError}
                      debugAvailable={Boolean(apiClient.debugAdapter)}
                      debugSession={activeDebugSession}
                      debugVariables={activeDebugVariables}
                      debugWatches={activeDebugWatches}
                      activeFrameId={activeTraceFrameId}
                      onSelectFrame={selectTraceFrame}
                      onSelectFlow={selectTraceFlow}
                      onSelectError={selectTraceError}
                      onClear={clearTestRun}
                      onRerun={handleTestRun}
                      onCancelRun={cancelTestRun}
                      onDebugCommand={command => void handleDebugCommand(command)}
                      onDebugEvaluate={expression => void handleDebugEvaluate(expression)}
                    />
                    </div>
                  </Tabs.TabPane>
                  <Tabs.TabPane tab={<span data-testid="microflow-run-history-tab-label">Run History</span>} itemKey="history">
                    <div data-testid="microflow-run-history-panel">
                    <MicroflowRunHistoryPanel
                      items={runHistoryItems}
                      selectedRunId={selectedRunId}
                      loading={runHistoryLoading}
                      error={runHistoryError}
                      statusFilter={runHistoryFilter}
                      onChangeFilter={status => {
                        setRunHistoryStatusByMicroflowId(current => ({ ...current, [schema.id]: status }));
                        void loadRunHistory(schema.id, status);
                      }}
                      onRefresh={() => {
                        void loadRunHistory(schema.id, runHistoryFilter);
                      }}
                      onSelectRun={runId => {
                        void selectRunHistoryItem(schema.id, runId);
                      }}
                    />
                    </div>
                  </Tabs.TabPane>
                </Tabs>
              </div>
            </Tabs.TabPane>
            <Tabs.TabPane tab="References" itemKey="references">
              <div style={{ overflow: "auto", maxHeight: activeBottomDockHeight - 56 }}>
                <Empty
                  title="References are managed by the workbench"
                  description="当前版本仍通过宿主引用抽屉展示 callers / callees / impact。"
                />
              </div>
            </Tabs.TabPane>
            <Tabs.TabPane tab="Info" itemKey="info">
              <div style={{ overflow: "auto", maxHeight: activeBottomDockHeight - 56, paddingTop: 8 }}>
                <Space vertical align="start" spacing={8}>
                  <Tag color="blue">Schema {schema.schemaVersion}</Tag>
                  <Text>Microflow: {schema.displayName || schema.name}</Text>
                  <Text>Module: {schema.moduleName || schema.moduleId}</Text>
                  <Text>Status: {dirty ? "Dirty" : "Saved"}</Text>
                  <Text>Bottom Dock: {bottomDockMode}</Text>
                  <Text>Focus Mode: {focusMode ? "On" : "Off"}</Text>
                </Space>
              </div>
            </Tabs.TabPane>
            <Tabs.TabPane tab="Console" itemKey="console">
              <div style={{ overflow: "auto", maxHeight: activeBottomDockHeight - 56 }}>
                {selectedRunSession?.logs?.length ? (
                  <Space vertical align="start" spacing={6} style={{ width: "100%" }}>
                    {selectedRunSession.logs.map(log => (
                      <Card key={log.id} style={{ width: "100%" }}>
                        <Space vertical align="start" spacing={4}>
                          <Tag color={log.level === "error" ? "red" : log.level === "warning" ? "orange" : "blue"}>{log.level}</Tag>
                          <Text>{log.message}</Text>
                          <Text type="tertiary" size="small">{log.timestamp}{log.traceId ? ` · ${log.traceId}` : ""}</Text>
                        </Space>
                      </Card>
                    ))}
                  </Space>
                ) : (
                  <Empty title="No runtime logs" description="执行产生的日志会在这里汇总展示。" />
                )}
              </div>
            </Tabs.TabPane>
          </Tabs>
        </div>
      ) : null}
      {!focusMode ? (
        <div
          data-testid="microflow-bottom-status-strip"
          style={{
            display: "flex",
            flexDirection: "row",
            alignItems: "center",
            justifyContent: "space-between",
            height: BOTTOM_STRIP_HEIGHT_PX,
            minHeight: BOTTOM_STRIP_HEIGHT_PX,
            borderTop: "1px solid var(--semi-color-border, #e5e6eb)",
            background: "var(--semi-color-bg-2, #fff)",
            padding: "0 10px",
            gap: 8
          }}
        >
          <Space spacing={8}>
            <Button
              aria-label="展开问题 Dock"
              size="small"
              theme={bottomTab === "problems" && bottomOpen ? "solid" : "borderless"}
              type="tertiary"
              icon={<IconTickCircle />}
              onClick={() => openBottomDock("problems")}
            >
              {labels.problems}
              <Badge count={issues.length} />
            </Button>
            <Button
              aria-label="展开调试 Dock"
              size="small"
              theme={bottomTab === "debug" && bottomOpen ? "solid" : "borderless"}
              type="tertiary"
              onClick={() => openBottomDock("debug")}
            >
              {labels.debug}
            </Button>
          </Space>
          <Space spacing={8}>
            {runSession ? <Tag color={runSession.status === "success" ? "green" : "red"}>{runSession.status} · {runSession.trace.length} frames</Tag> : null}
            {bottomOpen ? <Text size="small" type="tertiary">{bottomDockMode === "full" ? "Full dock" : "Peek dock"}</Text> : null}
          </Space>
        </div>
      ) : null}
      <MicroflowTestRunModal
        visible={testRunModalOpen}
        schema={schema}
        running={running}
        dirty={dirty}
        validationErrorCount={issues.filter(issue => issue.severity === "error").length}
        values={runInputsByMicroflowId[schema.id]}
        lastSession={runSession}
        serviceError={runtimeServiceError}
        onCancel={() => setTestRunModalOpen(false)}
        onValuesChange={values => setRunInputsByMicroflowId(current => ({ ...current, [schema.id]: values }))}
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
