import { useCallback, useEffect, useImperativeHandle, useMemo, useRef, useState, type CSSProperties, type DragEvent, type KeyboardEvent, type PointerEvent, type ReactNode, type Ref } from "react";
import { Badge, Button, Card, Checkbox, Dropdown, Empty, Input, Modal, Select, Space, Tabs, Tag, Toast, Tooltip, Typography } from "@douyinfe/semi-ui";
import {
  IconChevronDown,
  IconClose,
  IconCopy,
  IconDelete,
  IconDownloadStroked,
  IconExpand,
  IconPlay,
  IconRefresh,
  IconSave,
  IconSetting,
  IconTickCircle,
  IconUndo,
  IconRedo,
  IconMore
} from "@douyinfe/semi-icons";
import { MicroflowNodePanel, type MicroflowNodePanelLabels, type MicroflowNodePanelTemplate } from "../node-panel";
import { MicroflowPropertyPanel, type MicroflowEdgePatch, type MicroflowNodePatch } from "../property-panel";
import { buildDesignPropertyPanelModel } from "../property-panel/design-protocol-model";
import {
  addMicroflowObjectFromDragPayload,
  createDragPayloadFromRegistryItem,
  createUniqueMicroflowObjectId,
  getMicroflowNodeRegistryKey,
  microflowNodeRegistryByKey,
  objectKindFromRegistryItem,
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
import { FlowGramMicroflowNativeCanvas } from "../flowgram";
import { getMendixMicroflowNodeSize } from "../flowgram/flowgram-node-geometry";
import { createMicroflowWorkflowNode } from "../flowgram/flowgram-native-schema";
import { stripTransientDesignSchema } from "../flowgram/transient-workflow-state";
import {
  MICROFLOW_INLINE_FIELD_COMMIT_EVENT,
  MICROFLOW_INLINE_LINE_LABEL_COMMIT_EVENT,
  MICROFLOW_INLINE_NODE_INSPECT_EVENT,
  MICROFLOW_INLINE_NODE_TOGGLE_EVENT,
  MICROFLOW_INLINE_QUICK_FIX_EVENT,
  subscribeInlineNodeInspect,
  subscribeInlineNodeToggle,
  type MicroflowInlineFieldCommitDetail,
  type MicroflowInlineLineLabelCommitDetail,
  type MicroflowInlineNodeInspectDetail,
  type MicroflowInlineNodeToggleDetail,
  type MicroflowInlineQuickFixDetail,
} from "../flowgram/inline-events";
import type { MicroflowNodeViewMode } from "../flowgram/FlowGramMicroflowTypes";
import { MICROFLOW_GRID_SIZE } from "../flowgram/adapters/flowgram-coordinate";
import { createStableId } from "../schema/utils/ids";
import {
  EMPTY_MICROFLOW_METADATA_CATALOG,
  MicroflowMetadataProvider,
  useMicroflowMetadataCatalog,
  type MicroflowMetadataAdapter,
  type MicroflowMetadataCatalog,
} from "../metadata";
import { validateMicroflowSchema } from "../validators/validate-microflow-schema";
import { normalizeMicroflowAuthoringSchemaForRuntime } from "../schema/normalizer";
import { collectFlowsRecursive, findFlowWithCollection, findObjectWithCollection } from "../schema/utils/object-utils";
import { canApplyBooleanBranchQuickFix, createMissingBooleanBranch } from "./problem-quick-fixes";
import {
  collectDebugSessionMicroflowIds,
  MicroflowRunHistoryPanel,
  MicroflowStepDebugPanel,
  MicroflowTracePanel,
  MicroflowTestRunModal,
  buildExecutionPath,
  buildRunHistoryItemFromSession,
  buildRunRequest,
  filterNodeResultsByMicroflowId,
  readStoredTestRunSamples,
  resolveDeepestDebugMicroflowId,
  shouldBlockRun,
  writeStoredTestRunSamples,
  type MicroflowDebugCommand,
  type MicroflowDebugBreakpointDto,
  type MicroflowDebugConditionalBreakpointDto,
  type MicroflowDebugTraceEventDto,
  type MicroflowDebugTimelineEventDto,
  type MicroflowDebugSessionDto,
  type MicroflowDebugVariableSnapshotDto,
    type MicroflowDebugWatchExpressionDto,
  type MicroflowRunSession,
  type MicroflowTestRunInput,
  type MicroflowTestRunSamplesByMicroflowId,
  type MicroflowTestRunSample
} from "../debug";
import { createDebugStore, type DebugCallStackFrame as DebugWsCallStackFrame, type DebugLoopIteration, type DebugWsNodeHighlight } from "../stores/debug-store";
import { useDebugWebSocket, DEBUG_WS_COMMANDS } from "../hooks/use-debug-ws";
import { MicroflowStepDebugApiClient } from "../debug/step-debug-api";
import { createMicroflowGraphIndex, useDebouncedMicroflowValidation, type MicroflowValidationAdapterLike, type MicroflowValidationMode } from "../performance";
import { useMicroflowShortcuts } from "./shortcuts";
import { exportCanvasAsPng } from "./export-image";
import { buildAcceptance120Schema } from "./acceptance-120-fixture";
import { getDebugLatencyColor, getDebugWsStatusTag } from "./debug-status";
import { composeTraceFramesForRuntimePreview } from "./ws-runtime-trace";
import { summarizeMicroflowComplexity } from "../utils/microflow-validator";
import { buildNodeUsageHighlights, buildVariableUsageHighlights } from "../variables";
import {
  consumeRuntimeCommand,
  createRuntimeCommandConsoleEntry,
  isSupportedClientRuntimeCommand,
  parseRuntimeCommandPayload,
  type RuntimeCommandConsoleEntry,
} from "./runtime-command-consumer";
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
  MicroflowValidationIssue,
  MicroflowWorkflowEdgeJSON,
  MicroflowWorkflowNodeJSON
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
const RIGHT_PANEL_EXPANDED_PX = 380;
const BOTTOM_STRIP_HEIGHT_PX = 32;
const BOTTOM_DOCK_PEEK_HEIGHT_PX = 260;
const BOTTOM_DOCK_FULL_DEFAULT_PX = 420;
const LOOP_BODY_OFFSET_Y = 76;
const NODE_COLLISION_GAP_PX = 24;
const BOTTOM_DOCK_FULL_MIN_PX = 320;
const MOVE_HISTORY_DEBOUNCE_MS = 250;
const defaultFavoriteNodeKeys = ["activity:objectRetrieve", "activity:callRest", "activity:logMessage"];
const INTERNAL_TOOLBAR_ROW_PX = 60;
const AUXILIARY_PANELS_ENABLED = true;

function isDesignSchema(schema: unknown): schema is MicroflowDesignSchema {
  return (schema as { workflow?: unknown }).workflow != null;
}

function stripTransientSchemaState<T extends MicroflowSchema | MicroflowDesignSchema>(targetSchema: T): T {
  if (!isDesignSchema(targetSchema)) {
    return targetSchema;
  }
  return stripTransientDesignSchema(targetSchema) as T;
}

function createUniqueDesignNodeId(schema: MicroflowDesignSchema, registryKey: string): string {
  const prefix = registryKey.replace(/[^a-zA-Z0-9_-]/g, "-") || "node";
  const existingIds = new Set(schema.workflow.nodes.map(node => node.id));
  let id = createStableId(prefix);
  while (existingIds.has(id)) {
    id = createStableId(prefix);
  }
  return id;
}

function designEdgeId(edge: MicroflowWorkflowEdgeJSON): string | undefined {
  const data = edge.data as { flowId?: string } | undefined;
  return data?.flowId ?? edge.id;
}

function clearDesignSelection(schema: MicroflowDesignSchema): MicroflowDesignSchema {
  return {
    ...schema,
    editor: {
      ...schema.editor,
      selection: {
        ...schema.editor.selection,
        objectId: undefined,
        flowId: undefined,
        collectionId: undefined,
        objectIds: [],
        flowIds: [],
        mode: "none",
      },
    },
  };
}

function deleteDesignSelection(
  schema: MicroflowDesignSchema,
  objectIds: string[],
  flowIds: string[],
): MicroflowDesignSchema {
  const removedObjects = new Set(objectIds);
  const removedFlows = new Set(flowIds);
  const nodes = schema.workflow.nodes.filter(node => !removedObjects.has(node.id));
  const edges = schema.workflow.edges.filter(edge => {
    const flowId = designEdgeId(edge);
    return !removedObjects.has(edge.sourceNodeID)
      && !removedObjects.has(edge.targetNodeID)
      && !(flowId && removedFlows.has(flowId));
  });
  return clearDesignSelection({
    ...schema,
    workflow: {
      ...schema.workflow,
      nodes,
      edges,
    },
  });
}

function createUniqueDesignCopyId(existingIds: Set<string>, sourceId: string): string {
  const prefix = `${sourceId}-copy`.replace(/[^a-zA-Z0-9_-]/g, "-") || "node-copy";
  let id = createStableId(prefix);
  while (existingIds.has(id)) {
    id = createStableId(prefix);
  }
  existingIds.add(id);
  return id;
}

function duplicateDesignSelection(
  schema: MicroflowDesignSchema,
  objectIds: string[],
  flowIds: string[],
): MicroflowDesignSchema {
  const selectedObjects = new Set(objectIds);
  const selectedFlows = new Set(flowIds);
  const existingIds = new Set(schema.workflow.nodes.map(node => node.id));
  const idMap = new Map<string, string>();
  for (const node of schema.workflow.nodes) {
    if (selectedObjects.has(node.id)) {
      idMap.set(node.id, createUniqueDesignCopyId(existingIds, node.id));
    }
  }
  const copiedNodes = schema.workflow.nodes
    .filter(node => selectedObjects.has(node.id))
    .map(node => {
      const nextId = idMap.get(node.id) ?? createUniqueDesignCopyId(existingIds, node.id);
      const data = node.data as Record<string, unknown> | undefined;
      const parentObjectId = typeof data?.parentObjectId === "string"
        ? idMap.get(data.parentObjectId) ?? data.parentObjectId
        : data?.parentObjectId;
      return {
        ...node,
        id: nextId,
        data: {
          ...data,
          objectId: nextId,
          parentObjectId,
        },
        meta: {
          ...node.meta,
          position: {
            x: Number(node.meta?.position?.x ?? 0) + MICROFLOW_GRID_SIZE * 3,
            y: Number(node.meta?.position?.y ?? 0) + MICROFLOW_GRID_SIZE * 2,
          },
          parentObjectId,
        },
      } as MicroflowWorkflowNodeJSON;
    });

  const copiedEdges = schema.workflow.edges
    .filter(edge => {
      const flowId = designEdgeId(edge);
      const internalSelectedEdge = selectedObjects.has(edge.sourceNodeID) && selectedObjects.has(edge.targetNodeID);
      return (flowId && selectedFlows.has(flowId) && internalSelectedEdge) || internalSelectedEdge;
    })
    .map(edge => {
      const nextId = createStableId(`${designEdgeId(edge) ?? "flow"}-copy`);
      const data = edge.data as Record<string, unknown> | undefined;
      return {
        ...edge,
        id: nextId,
        sourceNodeID: idMap.get(edge.sourceNodeID) ?? edge.sourceNodeID,
        targetNodeID: idMap.get(edge.targetNodeID) ?? edge.targetNodeID,
        data: {
          ...data,
          flowId: nextId,
        },
      } as MicroflowWorkflowEdgeJSON;
    });

  const nextObjectIds = copiedNodes.map(node => node.id);
  return {
    ...schema,
    workflow: {
      ...schema.workflow,
      nodes: [...schema.workflow.nodes, ...copiedNodes],
      edges: [...schema.workflow.edges, ...copiedEdges],
    },
    editor: {
      ...schema.editor,
      selection: {
        ...schema.editor.selection,
        objectId: nextObjectIds[0],
        flowId: undefined,
        objectIds: nextObjectIds,
        flowIds: [],
        mode: nextObjectIds.length > 1 ? "multi" : nextObjectIds.length === 1 ? "single" : "none",
      },
    },
  };
}

type MendixLayoutInspectorMode = "floating" | "docked";
type BottomDockMode = "collapsed" | "peek" | "full";
export type MicroflowWorkbenchBottomTab = "problems" | "debug" | "references" | "info" | "console";
export type InlineEditState = "idle" | "editing" | "paused-edit" | "blocked";
export type PanelSyncEvent =
  | { type: "inline-edit"; nodeId?: string; flowId?: string; fieldPath?: string }
  | { type: "property-edit"; nodeId?: string; flowId?: string; fieldPath?: string }
  | { type: "problem-fix"; issueId?: string; nodeId?: string; flowId?: string }
  | { type: "trace-focus"; nodeId?: string; flowId?: string; frameId?: string };
export interface CommandPaletteAction {
  id: string;
  label: string;
  disabled?: boolean;
  disabledReason?: string;
  run: () => void;
}

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
  objectId?: string;
  flowId?: string;
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
  onLayoutStateChange?: (state: MicroflowWorkbenchLayoutState) => void;
  onWorkbenchStatusChange?: (status: MicroflowWorkbenchStatus) => void;
  onOpenMicroflow?: (microflowId: string) => void;
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
  /** Toggle “pan / hand” mode on the FlowGram canvas. */
  togglePanTool: () => void;
  toggleToolbox: () => void;
  toggleFullscreen: () => void;
  toggleFocusMode: () => void;
  toggleMinimap: () => void;
  exportAsImage: () => Promise<void>;
  resetLayout: () => void;
  getStatus: () => MicroflowEditorStatusSnapshot;
  openBottomTab: (tab: MicroflowWorkbenchBottomTab) => void;
  setBottomDockMode: (mode: BottomDockMode) => void;
  getLayoutState: () => MicroflowWorkbenchLayoutState;
  configureAllNodeAcceptance120?: () => void | Promise<void>;
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
  nodeElementCount: number;
  recommendedMaxNodeCount: number;
  nodeCountLevel: "ok" | "warning" | "error";
  annotationRecommended: boolean;
  hasAnnotation: boolean;
  canUndo: boolean;
  canRedo: boolean;
  zoomPercent: number;
  /** Native canvas pan tool; false when not supported or off. */
  canvasPanToolActive: boolean;
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
  shellMode: "editor-native-layout";
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
  contextProperties: string;
  contextRename: string;
  contextDuplicate: string;
  contextDelete: string;
  contextCenterView: string;
  contextCopyId: string;
  contextAddBreakpoint: string;
  contextRemoveBreakpoint: string;
  contextDisable: string;
  contextEnable: string;
  quickFix: string;
  quickFixUnavailable: string;
  missingDecisionBranchCreated: string;
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
  debug: "Debug",
  contextProperties: "Properties",
  contextRename: "Rename",
  contextDuplicate: "Duplicate",
  contextDelete: "Delete",
  contextCenterView: "Center View",
  contextCopyId: "Copy ID",
  contextAddBreakpoint: "Add Breakpoint",
  contextRemoveBreakpoint: "Remove Breakpoint",
  contextDisable: "Disable Node",
  contextEnable: "Enable Node",
  quickFix: "Quick fix",
  quickFixUnavailable: "Quick fix is not available for this issue.",
  missingDecisionBranchCreated: "Missing Decision branch created."
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

function createNormalizerIssues(
  microflowId: string,
  blockingIssues: ReturnType<typeof normalizeMicroflowAuthoringSchemaForRuntime>["report"]["blockingIssues"],
): MicroflowValidationIssue[] {
  return blockingIssues.map(item => ({
    id: `${microflowId}:normalizer:${item.code}:${item.flowId ?? item.objectId ?? "schema"}`,
    microflowId,
    code: item.code,
    severity: item.severity,
    source: "schema",
    message: item.message,
    objectId: item.objectId,
    flowId: item.flowId,
    edgeId: item.flowId,
    fieldPath: item.fieldPath ?? (item.flowId ? `flows.${item.flowId}` : "objectCollection"),
    blockSave: true,
    blockPublish: true,
  }));
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

function issueObjectTypeLabel(issue: Pick<MicroflowValidationIssue, "flowId" | "edgeId" | "objectId" | "nodeId">): "flow" | "node" | "global" {
  if (issue.flowId || issue.edgeId) {
    return "flow";
  }
  if (issue.objectId || issue.nodeId) {
    return "node";
  }
  return "global";
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

function setValueAtPath(target: Record<string, unknown>, path: string, value: unknown): boolean {
  const segments = path.split(".").filter(Boolean);
  if (segments.length === 0) {
    return false;
  }
  let cursor: Record<string, unknown> = target;
  for (let i = 0; i < segments.length - 1; i += 1) {
    const segment = segments[i];
    const next = cursor[segment];
    if (typeof next !== "object" || next === null || Array.isArray(next)) {
      cursor[segment] = {};
    }
    cursor = cursor[segment] as Record<string, unknown>;
  }
  cursor[segments[segments.length - 1]] = value;
  return true;
}

function parseInlineJsonValue(value: string): unknown {
  try {
    return JSON.parse(value);
  } catch {
    return value;
  }
}

function normalizeInlineCommitValue(detail: MicroflowInlineFieldCommitDetail): unknown {
  if (detail.editType === "outputMappings") {
    const parsed = parseInlineJsonValue(detail.value);
    return Array.isArray(parsed) ? parsed : [];
  }
  if (detail.editType === "json" || detail.editType === "mapping") {
    return parseInlineJsonValue(detail.value);
  }
  return detail.value;
}

function setInlineFieldOnDesignNode(node: MicroflowWorkflowNodeJSON, detail: MicroflowInlineFieldCommitDetail): boolean {
  const value = normalizeInlineCommitValue(detail);
  const fieldPath = detail.fieldPath.startsWith("data.") ? detail.fieldPath : `data.${detail.fieldPath}`;
  const changed = setValueAtPath(node as unknown as Record<string, unknown>, fieldPath, value);
  const objectKind = String((node.data as { objectKind?: unknown } | undefined)?.objectKind ?? node.type ?? "");
  if (objectKind === "annotation" && (fieldPath === "data.text" || fieldPath.endsWith(".text"))) {
    const nextTitle = String(value ?? "").trim() || "Annotation";
    return setValueAtPath(node as unknown as Record<string, unknown>, "data.title", nextTitle) || changed;
  }
  return changed;
}

function updateDesignNodeInlineField(schema: MicroflowDesignSchema, detail: MicroflowInlineFieldCommitDetail): MicroflowDesignSchema | undefined {
  let changed = false;
  const nodes = schema.workflow.nodes.map(node => {
    const nodeData = node.data ?? {};
    const objectId = typeof nodeData.objectId === "string" ? nodeData.objectId : node.id;
    if (node.id !== detail.nodeId && objectId !== detail.nodeId) {
      return node;
    }
    const nextNode = structuredClone(node) as MicroflowWorkflowNodeJSON;
    changed = setInlineFieldOnDesignNode(nextNode, detail) || changed;
    return nextNode;
  });
  if (!changed) {
    return undefined;
  }
  return {
    ...schema,
    workflow: {
      ...schema.workflow,
      nodes,
    },
    audit: {
      ...schema.audit,
      updatedAt: new Date().toISOString(),
    },
  };
}

function waitForMs(ms: number): Promise<void> {
  return new Promise(resolve => {
    window.setTimeout(resolve, ms);
  });
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
    x: ((event.clientX - (rect?.left ?? 0)) + graph.viewport.x) / graph.viewport.zoom,
    y: ((event.clientY - (rect?.top ?? 0)) + graph.viewport.y) / graph.viewport.zoom
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

function nodesOverlap(
  a: { position: MicroflowPoint; size: { width: number; height: number } },
  b: { position: MicroflowPoint; size: { width: number; height: number } },
  gap = NODE_COLLISION_GAP_PX,
): boolean {
  return Math.abs(a.position.x - b.position.x) < (a.size.width + b.size.width) / 2 + gap
    && Math.abs(a.position.y - b.position.y) < (a.size.height + b.size.height) / 2 + gap;
}

function resolveNonOverlappingPosition(
  graph: MicroflowEditorGraph,
  position: MicroflowPoint,
  size: { width: number; height: number },
  options: {
    parentObjectId?: string;
    excludeObjectId?: string;
  } = {},
): MicroflowPoint {
  const normalizedParentId = String(options.parentObjectId ?? "");
  const siblingNodes = graph.nodes.filter(node =>
    node.objectId !== options.excludeObjectId
    && String(node.parentObjectId ?? "") === normalizedParentId,
  );
  const stepX = Math.max(
    MICROFLOW_GRID_SIZE * 2,
    Math.ceil((size.width + NODE_COLLISION_GAP_PX) / MICROFLOW_GRID_SIZE) * MICROFLOW_GRID_SIZE,
  );
  const stepY = Math.max(
    MICROFLOW_GRID_SIZE * 2,
    Math.ceil((size.height + NODE_COLLISION_GAP_PX) / MICROFLOW_GRID_SIZE) * MICROFLOW_GRID_SIZE,
  );
  const candidates: MicroflowPoint[] = [position];
  for (let ring = 1; ring <= 8; ring += 1) {
    for (let dx = -ring; dx <= ring; dx += 1) {
      for (let dy = -ring; dy <= ring; dy += 1) {
        if (Math.max(Math.abs(dx), Math.abs(dy)) !== ring) {
          continue;
        }
        candidates.push({
          x: position.x + dx * stepX,
          y: position.y + dy * stepY,
        });
      }
    }
  }
  for (const candidate of candidates) {
    if (!siblingNodes.some(node => nodesOverlap({ position: candidate, size }, node))) {
      return candidate;
    }
  }
  return {
    x: position.x + stepX,
    y: position.y + stepY,
  };
}

function resolveNonOverlappingWorkflowPosition(
  nodes: MicroflowWorkflowNodeJSON[],
  position: MicroflowPoint,
  size: { width: number; height: number },
  options: {
    parentObjectId?: string;
    excludeObjectId?: string;
  } = {},
): MicroflowPoint {
  const normalizedParentId = String(options.parentObjectId ?? "");
  const siblingNodes = nodes
    .filter(node =>
      node.id !== options.excludeObjectId
      && String((node.data as { parentObjectId?: string } | undefined)?.parentObjectId ?? "") === normalizedParentId,
    )
    .map(node => ({
      objectId: node.id,
      position: {
        x: Number(node.meta?.position?.x ?? 0),
        y: Number(node.meta?.position?.y ?? 0),
      },
      size: node.meta?.size ?? { width: 160, height: 76 },
    }));
  const stepX = Math.max(
    MICROFLOW_GRID_SIZE * 2,
    Math.ceil((size.width + NODE_COLLISION_GAP_PX) / MICROFLOW_GRID_SIZE) * MICROFLOW_GRID_SIZE,
  );
  const stepY = Math.max(
    MICROFLOW_GRID_SIZE * 2,
    Math.ceil((size.height + NODE_COLLISION_GAP_PX) / MICROFLOW_GRID_SIZE) * MICROFLOW_GRID_SIZE,
  );
  const candidates: MicroflowPoint[] = [position];
  for (let ring = 1; ring <= 8; ring += 1) {
    for (let dx = -ring; dx <= ring; dx += 1) {
      for (let dy = -ring; dy <= ring; dy += 1) {
        if (Math.max(Math.abs(dx), Math.abs(dy)) !== ring) {
          continue;
        }
        candidates.push({
          x: position.x + dx * stepX,
          y: position.y + dy * stepY,
        });
      }
    }
  }
  for (const candidate of candidates) {
    if (!siblingNodes.some(node => nodesOverlap({ position: candidate, size }, node))) {
      return candidate;
    }
  }
  return {
    x: position.x + stepX,
    y: position.y + stepY,
  };
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

function ProblemPanel({
  issues,
  status,
  lastValidatedAt,
  lastError,
  onSelect,
  onApplyQuickFix,
  onRetry,
  quickFixLabel,
}: {
  issues: MicroflowValidationIssue[];
  status?: string;
  lastValidatedAt?: Date;
  lastError?: unknown;
  onSelect: (issue: MicroflowValidationIssue) => void;
  onApplyQuickFix?: (issue: MicroflowValidationIssue) => void;
  onRetry: () => void;
  quickFixLabel: string;
}) {
  const [severityFilter, setSeverityFilter] = useState<"all" | MicroflowValidationIssue["severity"]>("all");
  const [sourceFilter, setSourceFilter] = useState<string>("all");
  const [objectTypeFilter, setObjectTypeFilter] = useState<"all" | "flow" | "node" | "global">("all");
  const [onlyBlocking, setOnlyBlocking] = useState(false);
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
      const objectTypeMatched = objectTypeFilter === "all" || issueObjectTypeLabel(issue) === objectTypeFilter;
      const blockingMatched = !onlyBlocking || Boolean(issue.blockSave || issue.blockPublish || issue.severity === "error");
      const keywordMatched = !normalizedKeyword ||
        issue.code.toLowerCase().includes(normalizedKeyword) ||
        issue.message.toLowerCase().includes(normalizedKeyword) ||
        (issue.fieldPath ?? "").toLowerCase().includes(normalizedKeyword);
      return severityMatched && sourceMatched && objectTypeMatched && blockingMatched && keywordMatched;
    });
  }, [issues, keyword, objectTypeFilter, onlyBlocking, severityFilter, sourceFilter]);
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
          <Select
            size="small"
            value={objectTypeFilter}
            onChange={value => setObjectTypeFilter(value as "all" | "flow" | "node" | "global")}
            style={{ width: 130 }}
            optionList={[
              { label: "All types", value: "all" },
              { label: "Node", value: "node" },
              { label: "Flow", value: "flow" },
              { label: "Global", value: "global" },
            ]}
          />
          <Checkbox checked={onlyBlocking} onChange={event => setOnlyBlocking(Boolean(event.target.checked))}>
            Only blockers
          </Checkbox>
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
                    <Space vertical align="end" spacing={6}>
                      <Tag color={issue.flowId || issue.edgeId ? "purple" : issue.objectId || issue.nodeId ? "blue" : "grey"}>
                        {issue.flowId || issue.edgeId ? "flow" : issue.objectId || issue.nodeId ? "node" : "global"}
                      </Tag>
                      {onApplyQuickFix && canApplyBooleanBranchQuickFix(issue) ? (
                        <Button
                          size="small"
                          theme="borderless"
                          onClick={event => {
                            event.stopPropagation();
                            onApplyQuickFix(issue);
                          }}
                        >
                          {quickFixLabel}
                        </Button>
                      ) : null}
                    </Space>
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
  debugTimeline,
  debugSuspendPolicy,
  loopIteration,
  activeError,
  activeErrorStack,
  runtimeNodeState,
  runtimeCallStack,
  activeUsageVariableName,
  activeFrameId,
  onSelectFrame,
  onSelectFlow,
  onSelectError,
  onClear,
  onRerun,
  onCancelRun,
  onRetryQueuedRun,
  onDebugCommand,
  onDebugEvaluate,
  onDebugSuspendPolicyChange,
  onHighlightVariableUsage,
  onDebugRefreshTimeline,
  onDebugMutateVariable,
  onOpenMicroflow,
}: {
  microflowId: string;
  microflowName?: string;
  session?: MicroflowRunSession;
  serviceError?: string;
  debugAvailable?: boolean;
  debugSession?: MicroflowDebugSessionDto;
  debugVariables?: MicroflowDebugVariableSnapshotDto[];
  debugWatches?: MicroflowDebugWatchExpressionDto[];
  debugTimeline?: MicroflowDebugTimelineEventDto[];
  debugSuspendPolicy?: "all" | "branchOnly";
  loopIteration?: DebugLoopIteration;
  activeError?: string;
  activeErrorStack?: string;
  runtimeNodeState?: DebugWsNodeHighlight;
  runtimeCallStack?: DebugWsCallStackFrame[];
  activeUsageVariableName?: string;
  activeFrameId?: string;
  onSelectFrame: (frame: MicroflowTraceFrame) => void;
  onSelectFlow: (flowId: string) => void;
  onSelectError: (error: NonNullable<MicroflowTraceFrame["error"]>) => void;
  onClear: () => void;
  onRerun: () => void;
  onCancelRun: () => void;
  onRetryQueuedRun?: () => void;
  onDebugCommand?: (command: MicroflowDebugCommand) => void;
  onDebugEvaluate?: (expression: string) => void;
  onDebugSuspendPolicyChange?: (policy: "all" | "branchOnly") => void;
  onHighlightVariableUsage?: (variableName?: string) => void;
  onDebugRefreshTimeline?: () => void;
  onDebugMutateVariable?: (name: string, value: string) => void;
  onOpenMicroflow?: (microflowId: string) => void;
}) {
  const [suspendPolicyDraft, setSuspendPolicyDraft] = useState<"all" | "branchOnly">(debugSuspendPolicy ?? "all");
  const [mutateVariableName, setMutateVariableName] = useState("");
  const [mutateVariableValue, setMutateVariableValue] = useState("");
  useEffect(() => {
    setSuspendPolicyDraft(debugSuspendPolicy ?? "all");
  }, [debugSuspendPolicy]);
  useEffect(() => {
    if (!mutateVariableName && debugVariables && debugVariables.length > 0) {
      setMutateVariableName(debugVariables[0].name);
    }
  }, [debugVariables, mutateVariableName]);
  if (serviceError) {
    return <Empty title="运行服务不可用" description={serviceError} />;
  }
  const hasTrace = Boolean(session && buildExecutionPath(session).length > 0);
  const canApplyPolicy = Boolean(onDebugSuspendPolicyChange);
  const applyPolicyDisabledReason = canApplyPolicy ? "" : "Suspend policy update is unavailable in current context.";
  const canRefreshTimeline = Boolean(onDebugRefreshTimeline);
  const refreshTimelineDisabledReason = canRefreshTimeline ? "" : "Debug timeline refresh is unavailable in current context.";
  const canRerun = session ? session.status !== "running" : false;
  const rerunDisabledReason = canRerun ? "" : "Cannot rerun while current run is still running.";
  const canCancelRun = session ? session.status === "running" || session.status === "queued" : false;
  const cancelRunDisabledReason = canCancelRun ? "" : "Only queued/running sessions can be cancelled.";
  const canRetryQueuedRun = Boolean(onRetryQueuedRun && session && session.status !== "running" && session.status !== "queued");
  const retryQueuedRunDisabledReason = !onRetryQueuedRun
    ? "Current runtime adapter does not support queued retry."
    : !session
      ? "No run session to retry."
      : session.status === "running" || session.status === "queued"
        ? "Cannot retry while current run is queued/running."
        : "";
  const effectiveNodeId = debugSession?.currentNodeObjectId ?? debugSession?.currentSafePoint?.nodeObjectId ?? runtimeNodeState?.currentNodeId;
  const effectiveFlowId = debugSession?.currentSafePoint?.incomingFlowId ?? debugSession?.currentSafePoint?.outgoingFlowId ?? runtimeNodeState?.currentFlowId;
  const effectiveBranchId = debugSession?.currentSafePoint?.branchId ?? runtimeNodeState?.currentBranchId;
  const effectivePhase = debugSession?.pausePhase ?? debugSession?.currentSafePoint?.phase ?? runtimeNodeState?.currentSafePoint;
  const effectiveCallStack = (debugSession?.callStack?.length ?? 0) > 0
    ? (debugSession?.callStack ?? []).map(frame => ({ id: frame.id, name: `${frame.depth}:${frame.microflowId}`, microflowId: frame.microflowId }))
    : (runtimeCallStack ?? []).map(frame => ({ id: `${frame.runId}:${frame.depth}`, name: `${frame.depth}:${frame.microflowId}`, microflowId: frame.microflowId }));
  return (
    <Space vertical align="start" style={{ width: "100%" }}>
      {debugAvailable && debugSession ? (
        <>
          <MicroflowStepDebugPanel
            status={debugSession.status}
            currentNodeId={effectiveNodeId}
            currentFlowId={effectiveFlowId}
            currentBranchId={effectiveBranchId}
            currentPhase={effectivePhase}
            activeError={activeError}
            activeErrorStack={activeErrorStack}
            variables={(debugVariables ?? []).map(variable => ({
              name: variable.name,
              valuePreview: variable.valuePreview ?? "",
              scope: variable.scopeKind,
            }))}
            activeVariableName={activeUsageVariableName}
            watches={(debugWatches ?? []).map(watch => ({
              expression: watch.expression,
              value: watch.valuePreview,
              error: watch.error,
            }))}
            callStack={effectiveCallStack}
            breakpoints={[
              ...((debugSession.breakpoints ?? []).map(item => ({
                id: item.id,
                targetId: item.microflowObjectId,
                scope: normalizeDebugBreakpointScope(item.scope),
                stale: item.stale,
                hitTarget: item.hitTarget,
                logpoint: false,
                condition: undefined,
              }))),
              ...((debugSession.conditionalBreakpoints ?? []).map(item => ({
                id: item.id,
                targetId: item.microflowObjectId,
                scope: normalizeDebugBreakpointScope(item.scope),
                stale: item.stale,
                hitTarget: item.hitTarget,
                logpoint: item.logOnly,
                condition: item.conditionExpression,
              }))),
            ]}
            loopIteration={loopIteration}
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
            onVariableSelect={onHighlightVariableUsage}
            onCallStackFrameClick={frame => {
              if (!frame.microflowId || !onOpenMicroflow) {
                return;
              }
              onOpenMicroflow(frame.microflowId);
            }}
          />
          <Card title="Debug Controls" style={{ width: "100%" }} bodyStyle={{ padding: 10 }}>
            <Space vertical align="start" style={{ width: "100%" }}>
              <Space wrap>
                <Select
                  style={{ width: 180 }}
                  value={suspendPolicyDraft}
                  onChange={value => setSuspendPolicyDraft(value as "all" | "branchOnly")}
                  optionList={[
                    { label: "Suspend all", value: "all" },
                    { label: "Branch only", value: "branchOnly" },
                  ]}
                />
                <Tooltip content={applyPolicyDisabledReason || "Apply debug suspend policy"}>
                  <span style={{ display: "inline-flex" }}>
                    <Button size="small" disabled={!canApplyPolicy} onClick={() => onDebugSuspendPolicyChange?.(suspendPolicyDraft)}>Apply Policy</Button>
                  </span>
                </Tooltip>
                <Tooltip content={refreshTimelineDisabledReason || "Refresh debug timeline"}>
                  <span style={{ display: "inline-flex" }}>
                    <Button size="small" disabled={!canRefreshTimeline} onClick={onDebugRefreshTimeline}>Refresh Timeline</Button>
                  </span>
                </Tooltip>
              </Space>
              <Space wrap>
                <Input
                  style={{ width: 180 }}
                  value={mutateVariableName}
                  onChange={setMutateVariableName}
                  placeholder="Variable name"
                />
                <Input
                  style={{ width: 260 }}
                  value={mutateVariableValue}
                  onChange={setMutateVariableValue}
                  placeholder="New value preview/json"
                />
                <Tooltip content={!debugSession.currentSafePoint ? "仅暂停点允许修改变量" : !mutateVariableName.trim() ? "变量名不能为空" : ""}>
                  <span style={{ display: "inline-flex" }}>
                    <Button
                      size="small"
                      disabled={!debugSession.currentSafePoint || !mutateVariableName.trim()}
                      onClick={() => onDebugMutateVariable?.(mutateVariableName.trim(), mutateVariableValue)}
                    >
                      Mutate Variable
                    </Button>
                  </span>
                </Tooltip>
              </Space>
              <div style={{ width: "100%", maxHeight: 180, overflow: "auto", border: "1px solid var(--semi-color-border)", borderRadius: 6, padding: 8 }}>
                {(debugTimeline ?? []).slice(0, 50).map(item => (
                  <div key={item.id} style={{ marginBottom: 6 }}>
                    <Text size="small" type="secondary">{item.occurredAt} · {item.phase ?? "event"}</Text>
                    <br />
                    <Text
                      size="small"
                      link={Boolean(item.objectId || item.flowId)}
                      onClick={() => {
                        if (item.flowId) {
                          onSelectFlow(item.flowId);
                          return;
                        }
                        if (item.objectId) {
                          const frame = session?.trace.find(trace => trace.objectId === item.objectId);
                          if (frame) {
                            onSelectFrame(frame);
                          }
                        }
                      }}
                    >
                      {item.summary ?? item.objectId ?? item.flowId ?? item.id}
                    </Text>
                  </div>
                ))}
                {(!debugTimeline || debugTimeline.length === 0) ? (
                  <Text size="small" type="tertiary">No timeline events.</Text>
                ) : null}
              </div>
            </Space>
          </Card>
        </>
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
          <Tooltip content={rerunDisabledReason || "重新运行"}>
            <span style={{ display: "inline-flex" }}>
              <Button size="small" disabled={!canRerun} onClick={onRerun}>重新运行</Button>
            </span>
          </Tooltip>
          {onRetryQueuedRun ? (
            <Tooltip content={retryQueuedRunDisabledReason || "重试队列运行"}>
              <span style={{ display: "inline-flex" }}>
                <Button size="small" disabled={!canRetryQueuedRun} onClick={onRetryQueuedRun}>重试队列运行</Button>
              </span>
            </Tooltip>
          ) : null}
          <Tooltip content={cancelRunDisabledReason || "取消运行"}>
            <span style={{ display: "inline-flex" }}>
              <Button size="small" type="warning" disabled={!canCancelRun} onClick={onCancelRun}>取消运行</Button>
            </span>
          </Tooltip>
          <Tooltip content="清空当前调试视图（不影响已保存的运行记录）">
            <Button size="small" type="danger" theme="borderless" onClick={onClear}>清空</Button>
          </Tooltip>
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

function normalizeDebugBreakpointScope(scope: MicroflowDebugBreakpointDto["scope"] | MicroflowDebugConditionalBreakpointDto["scope"] | undefined): "node" | "flow" | "expression" | "error" | "gatewayBranch" {
  switch (scope) {
    case 1:
    case "flow":
      return "flow";
    case 2:
    case "expression":
      return "expression";
    case 3:
    case "errorHandler":
      return "error";
    case 4:
    case "gatewayBranch":
      return "gatewayBranch";
    default:
      return "node";
  }
}

function resolveDeepestDebugWsMicroflowId(callStack: DebugWsCallStackFrame[]): string | undefined {
  for (let index = callStack.length - 1; index >= 0; index -= 1) {
    const microflowId = String(callStack[index]?.microflowId ?? "").trim();
    if (microflowId) {
      return microflowId;
    }
  }
  return undefined;
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
  commands: CommandPaletteAction[];
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
            <Tooltip key={command.id} content={command.disabled ? (command.disabledReason ?? "This command is currently unavailable.") : ""}>
              <Button
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
            </Tooltip>
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
    refreshDerivedState(stripTransientSchemaState(props.schema), props.metadataCatalog ?? EMPTY_MICROFLOW_METADATA_CATALOG),
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
  const [runtimeCommandEntriesByMicroflowId, setRuntimeCommandEntriesByMicroflowId] = useState<Record<string, RuntimeCommandConsoleEntry[]>>({});
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
  const [debugTimelineBySessionId, setDebugTimelineBySessionId] = useState<Record<string, MicroflowDebugTimelineEventDto[]>>({});
  const [debugSuspendPolicyBySessionId, setDebugSuspendPolicyBySessionId] = useState<Record<string, "all" | "branchOnly">>({});
  const [debugWatchesBySessionId, setDebugWatchesBySessionId] = useState<Record<string, MicroflowDebugWatchExpressionDto[]>>({});
  const debugStoreRef = useRef(createDebugStore());
  const [debugStoreSnapshot, setDebugStoreSnapshot] = useState(() => debugStoreRef.current.getSnapshot());
  const stepDebugApiClient = useMemo(() => new MicroflowStepDebugApiClient(), []);
  const [testRunModalOpen, setTestRunModalOpen] = useState(false);
  const [runInputsByMicroflowId, setRunInputsByMicroflowId] = useState<Record<string, Record<string, unknown>>>({});
  const [testRunSamplesByMicroflowId, setTestRunSamplesByMicroflowId] = useState<MicroflowTestRunSamplesByMicroflowId>(() => readStoredTestRunSamples());
  const [saving, setSaving] = useState(false);
  const [running, setRunning] = useState(false);
  const [dirty, setDirty] = useState(false);
  const lastDebugRouteKeyRef = useRef<string>();
  const schemaRevisionRef = useRef(0);
  const toolbarMode = props.toolbarMode ?? "internal";
  const shouldShowCanvasContextMenu = toolbarMode === "external" || AUXILIARY_PANELS_ENABLED;
  const shellMode = "editor-native-layout" as const;
  const externalLayout = true;
  const [leftOpen, setLeftOpen] = useState(() => {
    if (props.toolbarMode === "external") {
      return readMendixLayoutStorage().nodesDrawerOpen ?? true;
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
  useEffect(() => {
    if (leftOpen && rightOpen) {
      setRightOpen(false);
    }
  }, [leftOpen, rightOpen]);
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
  const [canvasPanToolActive, setCanvasPanToolActive] = useState(false);
  const [nodeViewModes, setNodeViewModes] = useState<Record<string, MicroflowNodeViewMode>>({});
  const onValidationStateChangeRef = useRef(props.onValidationStateChange);
  const onSchemaChangeRef = useRef(props.onSchemaChange);

  useEffect(() => {
    onValidationStateChangeRef.current = props.onValidationStateChange;
  }, [props.onValidationStateChange]);

  useEffect(() => {
    onSchemaChangeRef.current = props.onSchemaChange;
  }, [props.onSchemaChange]);

  useEffect(() => {
    writeStoredTestRunSamples(testRunSamplesByMicroflowId);
  }, [testRunSamplesByMicroflowId]);

  const validateForMode = useCallback(async (targetSchema: MicroflowSchema | MicroflowDesignSchema, mode: MicroflowValidationMode) => {
    const stableTargetSchema = stripTransientSchemaState(targetSchema);
    const normalized = isDesignSchema(stableTargetSchema) ? null : normalizeMicroflowAuthoringSchemaForRuntime(stableTargetSchema);
    const schemaForValidation = normalized?.schema ?? stableTargetSchema;
    const normalizerIssues = normalized
      ? createNormalizerIssues(schemaForValidation.id, normalized.report.blockingIssues)
      : [];
    try {
      const localResult = validateMicroflowSchema({
        schema: schemaForValidation,
        metadata: loadedMetadata,
        options: { mode, includeWarnings: true, includeInfo: true },
      });
      const localIssues = [...normalizerIssues, ...localResult.issues];
      setIssues(localIssues);
      if (!props.validationAdapter) {
        return {
          ...localResult,
          issues: localIssues,
          summary: summarizeValidationIssues(localIssues),
        };
      }
      const serverResult = await props.validationAdapter.validate({
        resourceId: schemaForValidation.id,
        schema: schemaForValidation,
        metadata: loadedMetadata,
        mode,
        includeWarnings: true,
        includeInfo: true,
      });
      const serverIssues = serverResult.issues.map(issue => ({
        ...asServerValidationIssue(issue),
        microflowId: schemaForValidation.id,
        blockSave: issue.blockSave ?? issue.severity === "error",
        blockPublish: issue.blockPublish ?? issue.severity === "error",
      }));
      const issues = [...normalizerIssues, ...localResult.issues, ...serverIssues];
      const summary = summarizeValidationIssues(issues);
      setIssues(issues);
      return {
        ...serverResult,
        issues,
        summary,
      };
    } catch (error) {
      const issue = createValidationServiceIssue(error, mode, schemaForValidation.id);
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
      : !AUXILIARY_PANELS_ENABLED
        ? externalLayout
          ? "minmax(0, 1fr)"
          : `${INTERNAL_TOOLBAR_ROW_PX}px minmax(0, 1fr)`
      : externalLayout
        ? `minmax(0, 1fr) ${BOTTOM_STRIP_HEIGHT_PX}px`
        : `${INTERNAL_TOOLBAR_ROW_PX}px minmax(0, 1fr) ${BOTTOM_STRIP_HEIGHT_PX}px`,
    height: "100%",
    minHeight: 0,
    position: "relative",
    background: "var(--semi-color-bg-0, #f7f8fa)"
  }), [externalLayout, focusMode]);

  const bodyStyle = useMemo((): CSSProperties => {
    const rightCol = focusMode || !AUXILIARY_PANELS_ENABLED
      ? 0
      : leftOpen || rightOpen
        ? RIGHT_PANEL_EXPANDED_PX
        : RAIL_WIDTH_PX;
    return {
      display: "grid",
      gridTemplateColumns: `minmax(0, 1fr) ${rightCol}px`,
      minHeight: 0,
      minWidth: 0,
      overflow: "hidden",
      position: "relative"
    };
  }, [focusMode, leftOpen, rightOpen]);

  const graph = useMemo(() => toEditorGraph({ ...schema, validation: { issues } }), [schema, issues]);
  const graphIndex = useMemo(() => createMicroflowGraphIndex(schema), [schema.objectCollection, schema.flows]);
  const designPropertyPanelModel = useMemo(() => isDesignSchema(schema) ? buildDesignPropertyPanelModel(schema) : undefined, [schema]);
  const selectedObject = designPropertyPanelModel?.selectedObject ?? (schema.editor.selection.objectId ? graphIndex.objectsById.get(schema.editor.selection.objectId) ?? null : null);
  const selectedFlow = designPropertyPanelModel?.selectedFlow ?? (schema.editor.selection.flowId ? graphIndex.flowsById.get(schema.editor.selection.flowId) ?? null : null);
  const activeMicroflowId = schema.id;
  const saveBlockers = issues.filter(issue => issue.blockSave && issue.severity === "error");
  const publishBlockers = issues.filter(issue => issue.blockPublish && issue.severity === "error");
  const runSession = runSessionByMicroflowId[activeMicroflowId];
  const runtimeServiceError = runtimeServiceErrorByMicroflowId[activeMicroflowId];
  const selectedRunId = selectedRunIdByMicroflowId[activeMicroflowId];
  const runHistoryFilter = runHistoryStatusByMicroflowId[activeMicroflowId] ?? "all";
  const runHistoryItems = runHistoryByMicroflowId[activeMicroflowId] ?? [];
  const runtimeCommandEntries = runtimeCommandEntriesByMicroflowId[activeMicroflowId] ?? [];
  const runHistoryLoading = Boolean(runHistoryLoadingByMicroflowId[activeMicroflowId]);
  const runHistoryError = runHistoryErrorByMicroflowId[activeMicroflowId];
  const selectedRunSession = selectedRunId ? runDetailsByRunId[selectedRunId] : runSession;
  const activeDebugSession = debugSessionByMicroflowId[activeMicroflowId];
  const debugSessionId = activeDebugSession?.id ?? debugStoreSnapshot.sessionId;
  const {
    status: debugConnectionStatus,
    latencyMs: debugLatencyMs,
    connect: connectDebugConnection,
    disconnect: disconnectDebugConnection,
    send: sendDebugConnectionCommand,
  } = useDebugWebSocket(activeMicroflowId, {
    sessionId: debugSessionId,
    store: debugStoreRef.current,
    autoReconnect: 5,
    pingIntervalMs: 30_000,
  });
  useEffect(() => {
    return debugStoreRef.current.subscribe(setDebugStoreSnapshot);
  }, []);
  useEffect(() => {
    if (!debugSessionId) {
      disconnectDebugConnection();
      return;
    }
    disconnectDebugConnection();
    connectDebugConnection();
  }, [connectDebugConnection, debugSessionId, disconnectDebugConnection]);
  useEffect(() => () => {
    disconnectDebugConnection();
  }, [disconnectDebugConnection]);
  useEffect(() => {
    if (!props.onOpenMicroflow) {
      return;
    }
    const deepestWsDebugMicroflowId = resolveDeepestDebugWsMicroflowId(debugStoreSnapshot.callStack);
    if (!deepestWsDebugMicroflowId || deepestWsDebugMicroflowId === activeMicroflowId) {
      return;
    }
    const routeKey = [
      "ws",
      debugSessionId ?? "",
      activeMicroflowId,
      deepestWsDebugMicroflowId,
      debugStoreSnapshot.nodeState.currentNodeId ?? "",
      debugStoreSnapshot.nodeState.currentSafePoint ?? "",
      String(debugStoreSnapshot.callStack.length),
    ].join("|");
    if (lastDebugRouteKeyRef.current === routeKey) {
      return;
    }
    lastDebugRouteKeyRef.current = routeKey;
    props.onOpenMicroflow(deepestWsDebugMicroflowId);
  }, [
    props.onOpenMicroflow,
    debugStoreSnapshot.callStack,
    debugStoreSnapshot.nodeState.currentNodeId,
    debugStoreSnapshot.nodeState.currentSafePoint,
    activeMicroflowId,
    debugSessionId,
  ]);
  const isDebugPaused = useMemo(() => {
    const status = String(activeDebugSession?.status ?? "").toLowerCase();
    if (status.includes("paused")) {
      return true;
    }
    const commands = new Set((activeDebugSession?.availableCommands ?? []).map(item => String(item).toLowerCase()));
    return commands.has("continue") || commands.has("stepover") || commands.has("stepinto") || commands.has("stepout");
  }, [activeDebugSession]);
  const runtimeInlineReadonly = running && !isDebugPaused;
  const hasExpandedInlineNode = useMemo(
    () => Object.values(nodeViewModes).some(mode => mode === "expanded"),
    [nodeViewModes],
  );
  const inlineEditState = useMemo<InlineEditState>(() => {
    if (runtimeInlineReadonly) {
      return "blocked";
    }
    if (isDebugPaused) {
      return "paused-edit";
    }
    if (hasExpandedInlineNode) {
      return "editing";
    }
    return "idle";
  }, [hasExpandedInlineNode, isDebugPaused, runtimeInlineReadonly]);
  const activeDebugVariables = activeDebugSession ? debugVariablesBySessionId[activeDebugSession.id] ?? [] : [];
  const activeDebugTimeline = activeDebugSession ? debugTimelineBySessionId[activeDebugSession.id] ?? [] : [];
  const activeDebugSuspendPolicy = activeDebugSession ? debugSuspendPolicyBySessionId[activeDebugSession.id] ?? "all" : "all";
  const activeDebugWatches = activeDebugSession ? debugWatchesBySessionId[activeDebugSession.id] ?? [] : [];
  const debugWsStatusTag = getDebugWsStatusTag(debugConnectionStatus);
  const traceFrames = useMemo(() => {
    const base = filterNodeResultsByMicroflowId(selectedRunSession, activeMicroflowId);
    return composeTraceFramesForRuntimePreview({
      baseFrames: base,
      wsCurrentNodeId: debugStoreSnapshot.nodeState.currentNodeId,
      wsCurrentBranchId: debugStoreSnapshot.nodeState.currentBranchId,
      sessionId: debugSessionId,
    });
  }, [
    selectedRunSession,
    activeMicroflowId,
    debugStoreSnapshot.nodeState.currentNodeId,
    debugStoreSnapshot.nodeState.currentBranchId,
    debugSessionId,
  ]);

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
    const stableNext = stripTransientSchemaState(next);
    const nextWithSelection = options.preserveSelection
      ? {
          ...stableNext,
          editor: {
            ...stableNext.editor,
            selection: schema.editor.selection,
            selectedObjectId: schema.editor.selectedObjectId,
            selectedFlowId: schema.editor.selectedFlowId,
            selectedCollectionId: schema.editor.selectedCollectionId,
          },
        }
      : stableNext;
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

  const emitPanelSyncEvent = useCallback((detail: PanelSyncEvent) => {
    window.dispatchEvent(new CustomEvent<PanelSyncEvent>("atlas:microflow-panel-sync", { detail }));
  }, []);

  const quickAddPosition = (): MicroflowPoint => {
    const viewport = schema.editor.viewport ?? { x: 0, y: 0, zoom: 1 };
    const indexOffset = graph.nodes.length * 18;
    return {
      x: Math.round(((360 + viewport.x) / Math.max(0.2, viewport.zoom) + indexOffset) / MICROFLOW_GRID_SIZE) * MICROFLOW_GRID_SIZE,
      y: Math.round(((220 + viewport.y) / Math.max(0.2, viewport.zoom) + indexOffset / 2) / MICROFLOW_GRID_SIZE) * MICROFLOW_GRID_SIZE
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
    const requestedPosition = options?.position ?? quickAddPosition();
    const parentLoopObjectId = options?.parentLoopObjectId ?? findLoopAtPosition(graph, requestedPosition);
    const parentLoopNode = parentLoopObjectId ? graph.nodes.find(node => node.objectId === parentLoopObjectId) : undefined;
    const authoringPosition = parentLoopNode
      ? {
          x: Math.max(24, requestedPosition.x - parentLoopNode.position.x),
          y: Math.max(24, requestedPosition.y - parentLoopNode.position.y - LOOP_BODY_OFFSET_Y),
        }
      : requestedPosition;
    const itemObjectKind = objectKindFromRegistryItem(item);
    const nodeSize = getMendixMicroflowNodeSize(itemObjectKind);
    const projectedPosition = parentLoopNode
      ? {
          x: parentLoopNode.position.x + authoringPosition.x,
          y: parentLoopNode.position.y + LOOP_BODY_OFFSET_Y + authoringPosition.y,
        }
      : authoringPosition;
    const resolvedGraphPosition = resolveNonOverlappingPosition(graph, projectedPosition, nodeSize, {
      parentObjectId: parentLoopObjectId,
    });
    const resolvedAuthoringPosition = parentLoopNode
      ? {
          x: Math.max(24, resolvedGraphPosition.x - parentLoopNode.position.x),
          y: Math.max(24, resolvedGraphPosition.y - parentLoopNode.position.y - LOOP_BODY_OFFSET_Y),
        }
      : resolvedGraphPosition;
    if (["startEvent", "endEvent"].includes(itemObjectKind) && parentLoopObjectId) {
      Toast.warning("Start / End events cannot be placed inside Loop.");
      return;
    }
    if (["breakEvent", "continueEvent"].includes(itemObjectKind) && !parentLoopObjectId) {
      Toast.warning("Break / Continue can only be placed inside Loop.");
      return;
    }
    const payload = options?.payload ?? createDragPayloadFromRegistryItem(item);
    if (isDesignSchema(schema)) {
      if (options?.insertFlowId) {
        Toast.warning("FlowGram Studio 暂不支持从连线中插入节点，请直接拖入画布后再连接。");
        return;
      }
      const registryKey = getMicroflowNodeRegistryKey(item);
      const objectId = createUniqueDesignNodeId(schema, registryKey);
      const parentLoopWorkflowNode = parentLoopObjectId
        ? schema.workflow.nodes.find(node => node.id === parentLoopObjectId)
        : undefined;
      const parentLoopData = parentLoopWorkflowNode?.data as { bodyCollectionId?: string } | undefined;
      const collectionId = parentLoopObjectId
        ? parentLoopData?.bodyCollectionId ?? `${parentLoopObjectId}-collection`
        : undefined;
      const resolvedWorkflowPosition = resolveNonOverlappingWorkflowPosition(
        schema.workflow.nodes,
        resolvedGraphPosition,
        nodeSize,
        { parentObjectId: parentLoopObjectId },
      );
      const parameterId = itemObjectKind === "parameterObject" ? createStableId("param") : undefined;
      const parameterName = itemObjectKind === "parameterObject" ? "parameter" : undefined;
      const node = createMicroflowWorkflowNode({
        id: objectId,
        registryKey,
        position: resolvedWorkflowPosition,
        title: parameterName ?? item.titleZh ?? item.title,
        data: parentLoopObjectId
          ? {
              parentObjectId: parentLoopObjectId,
              collectionId,
            }
          : itemObjectKind === "parameterObject"
            ? {
                parameterId,
                parameterName,
              }
            : undefined,
      }) as MicroflowWorkflowNodeJSON;
      const nextSchema: MicroflowDesignSchema = {
        ...schema,
        workflow: {
          ...schema.workflow,
          nodes: [...schema.workflow.nodes, node],
        },
        parameters: itemObjectKind === "parameterObject" && parameterId && parameterName
          ? [
              ...schema.parameters,
              {
                id: parameterId,
                stableId: parameterId,
                name: parameterName,
                dataType: { kind: "string" },
                type: { kind: "primitive", name: "string" },
                required: true,
              },
            ]
          : schema.parameters,
        editor: {
          ...schema.editor,
          selection: {
            ...schema.editor.selection,
            objectId,
            flowId: undefined,
            objectIds: [objectId],
            flowIds: [],
            mode: "single",
          },
        },
        audit: {
          ...schema.audit,
          updatedAt: new Date().toISOString(),
        },
      };
      commitSchema(nextSchema as unknown as MicroflowSchema, parentLoopObjectId ? "addLoopNode" : "addNode", { source: "nodePanel" });
      return;
    }
    if (options?.insertFlowId) {
      const object = createObjectFromRegistry(
        item,
        resolvedAuthoringPosition,
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
    const result = addMicroflowObjectFromDragPayload({ schema, payload, position: resolvedAuthoringPosition, parentLoopObjectId });
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
      flows: [...(Array.isArray(schema.flows) ? schema.flows : []), ...flows],
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

  const handleConfigureAllNodeAcceptance120 = async () => {
    if (!isDesignSchema(schema)) {
      Toast.error("旧版 Authoring 编辑器不支持验收120配置，请使用新版 FlowGram Studio。");
      return;
    }
    const nextDesignSchema = buildAcceptance120Schema(schema);
    const nextSchema = nextDesignSchema as unknown as MicroflowSchema;
    commitSchema(nextSchema, "insertTemplate", { historyLabel: "Configure acceptance 120 graph", source: "nodePanel" });
    setSaving(true);
    try {
      const response = await apiClient.saveMicroflow({ schema: nextDesignSchema });
      props.onSaveComplete?.(response);
      savedSchemaRef.current = nextSchema;
      historyManager.replaceCurrent(nextSchema, "bulkUpdate");
      refreshHistoryState();
      setDirty(false);
      Toast.success("已配置并保存全节点验收计算图，预期输出 120。");
    } catch (error) {
      applyApiValidationIssues(error, setIssues, () => {
        setBottomTab("problems");
      });
      Toast.error(getEditorApiErrorMessage(error));
    } finally {
      setSaving(false);
    }
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
    const stableSchema = stripTransientSchemaState(schema);
    const normalized = isDesignSchema(stableSchema) ? null : normalizeMicroflowAuthoringSchemaForRuntime(stableSchema);
    const schemaToSave = normalized?.schema ?? stableSchema;
    if (normalized?.report.repaired && !microflowSchemasEqual(schemaToSave, schema)) {
      commitSchema(schemaToSave, "bulkUpdate", { pushHistory: false, skipValidate: true });
    }
    const saveRevision = schemaRevisionRef.current;
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
      void runValidationNow(stripTransientSchemaState(latestSchemaRef.current));
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

  const handleExportAsImage = useCallback(async () => {
    const canvasElement = shellRef.current?.querySelector<HTMLElement>(".microflow-flowgram-canvas");
    if (!canvasElement) {
      Toast.warning("Microflow canvas is not ready for export.");
      return;
    }
    const result = await exportCanvasAsPng(canvasElement, schema.displayName || schema.name);
    if (result.ok) {
      Toast.success("PNG exported.");
      return;
    }
    Toast.error(result.error);
  }, [schema.displayName, schema.name]);

  const handleValidate = async () => {
    try {
      const issues = await runValidationNow(stripTransientSchemaState(schema));
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
    setTestRunModalOpen(true);
    const validation = await validateForMode(stripTransientSchemaState(schema), "testRun");
    if (validation.summary.errorCount > 0) {
      setBottomTab("problems");
      Toast.error("Fix validation errors before running.");
      return;
    }
    if (validation.summary.warningCount > 0) {
      Toast.warning(`Test run allowed with ${validation.summary.warningCount} warning(s).`);
    }
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

  const hydrateRunSession = useCallback(async (microflowId: string, runId: string): Promise<MicroflowRunSession> => {
    let lastError: unknown;
    for (let attempt = 0; attempt < 3; attempt += 1) {
      try {
        const detail = await apiClient.getMicroflowRunDetail(microflowId, runId);
        const trace = await apiClient.getMicroflowRunTrace(runId);
        return { ...detail, trace };
      } catch (error) {
        lastError = error;
        if (attempt < 2) {
          await waitForMs(500);
        }
      }
    }
    throw lastError ?? new Error(`Unable to hydrate run session: ${runId}`);
  }, [apiClient]);

  const waitForTerminalRunStatus = useCallback(async (runId: string): Promise<"success" | "failed" | "cancelled" | "running" | "queued"> => {
    if (!apiClient.getMicroflowRunStatus) {
      return "running";
    }
    let currentStatus: "queued" | "running" | "success" | "failed" | "cancelled" = "queued";
    for (let poll = 0; poll < 60; poll += 1) {
      const status = await apiClient.getMicroflowRunStatus(runId);
      currentStatus = status.status;
      if (currentStatus === "success" || currentStatus === "failed" || currentStatus === "cancelled") {
        return currentStatus;
      }
      await waitForMs(1000);
    }
    return currentStatus;
  }, [apiClient]);

  const selectRunHistoryItem = useCallback(async (microflowId: string, runId: string) => {
    setSelectedRunIdByMicroflowId(current => ({ ...current, [microflowId]: runId }));
    const cached = runDetailsByRunId[runId];
    const cachedNeedsHydration = cached
      ? (cached.childRuns?.length ?? 0) === 0 && (cached.childRunIds?.length ?? 0) > 0
        || (cached.callStack?.length ?? 0) === 0
        || cached.hasHydratedTrace === false
      : false;
    if (cached && !cachedNeedsHydration) {
      setRunSessionByMicroflowId(current => ({ ...current, [microflowId]: cached }));
      setActiveTraceFrameId(cached.trace[0]?.id);
      return;
    }
    try {
      const detail = await hydrateRunSession(microflowId, runId);
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
  }, [hydrateRunSession, runDetailsByRunId]);

  const refreshDebugSession = useCallback(async (sessionId: string, microflowId = schema.id) => {
    const [session, variables, timeline] = await Promise.all([
      stepDebugApiClient.getSession(sessionId),
      stepDebugApiClient.listVariables(sessionId).catch(() => []),
      stepDebugApiClient.getTimeline(sessionId).catch(() => []),
    ]);
    const debugMicroflowIds = collectDebugSessionMicroflowIds(session, microflowId);
    setDebugSessionByMicroflowId(current => {
      const next = { ...current };
      for (const id of debugMicroflowIds) {
        next[id] = session;
      }
      return next;
    });
    setDebugVariablesBySessionId(current => ({ ...current, [sessionId]: variables }));
    setDebugTimelineBySessionId(current => ({ ...current, [sessionId]: timeline }));
    return session;
  }, [schema.id, stepDebugApiClient]);

  const syncDebugSessionNavigation = useCallback((
    session: MicroflowDebugSessionDto | undefined,
    sourceMicroflowId: string,
    linkedRunSession?: MicroflowRunSession,
  ) => {
    if (!session) {
      return;
    }
    const debugMicroflowIds = collectDebugSessionMicroflowIds(session, sourceMicroflowId);
    if (debugMicroflowIds.length > 0) {
      setDebugSessionByMicroflowId(current => {
        const next = { ...current };
        for (const id of debugMicroflowIds) {
          next[id] = session;
        }
        return next;
      });
      if (linkedRunSession) {
        setRunSessionByMicroflowId(current => {
          const next = { ...current };
          for (const id of debugMicroflowIds) {
            next[id] = linkedRunSession;
          }
          return next;
        });
        setSelectedRunIdByMicroflowId(current => {
          const next = { ...current };
          for (const id of debugMicroflowIds) {
            next[id] = linkedRunSession.id;
          }
          return next;
        });
      }
    }
    const targetMicroflowId = resolveDeepestDebugMicroflowId(session, sourceMicroflowId);
    if (!props.onOpenMicroflow || !targetMicroflowId || targetMicroflowId === sourceMicroflowId) {
      return;
    }
    const routeKey = [
      session.id,
      sourceMicroflowId,
      targetMicroflowId,
      session.currentSafePoint?.nodeObjectId ?? session.currentNodeObjectId ?? "",
      session.currentSafePoint?.phase ?? session.pausePhase ?? "",
      session.lastUpdatedAt ?? "",
    ].join("|");
    if (lastDebugRouteKeyRef.current === routeKey) {
      return;
    }
    lastDebugRouteKeyRef.current = routeKey;
    props.onOpenMicroflow(targetMicroflowId);
  }, [props.onOpenMicroflow]);

  const refreshDebugTimeline = useCallback(async (sessionId: string) => {
    if (!sessionId) {
      return;
    }
    try {
      const timeline = await stepDebugApiClient.getTimeline(sessionId);
      setDebugTimelineBySessionId(current => ({ ...current, [sessionId]: timeline }));
    } catch (error) {
      Toast.warning(getEditorApiErrorMessage(error));
    }
  }, [stepDebugApiClient]);

  const startDebugSession = useCallback(async () => {
    try {
      const session = await stepDebugApiClient.createSession(schema.id);
      debugStoreRef.current.resetForSession(session.id);
      debugStoreRef.current.setSession(session.id);
      setPendingDebugSessionId(session.id);
      syncDebugSessionNavigation(session, schema.id);
      setDebugSuspendPolicyBySessionId(current => ({ ...current, [session.id]: current[session.id] ?? "all" }));
      setBottomDockMode("peek");
      setBottomTab("debug");
      setTestRunModalOpen(true);
    } catch (error) {
      setBottomDockMode("peek");
      setBottomTab("debug");
      Toast.error(getEditorApiErrorMessage(error));
      await handleTestRun();
    }
  }, [handleTestRun, schema.id, stepDebugApiClient, syncDebugSessionNavigation]);

  const handleDebugCommand = useCallback(async (command: MicroflowDebugCommand) => {
    if (!activeDebugSession) {
      return;
    }
    const commandMap: Record<MicroflowDebugCommand, keyof typeof DEBUG_WS_COMMANDS> = {
      continue: "CONTINUE",
      pause: "PAUSE",
      stepOver: "STEP_OVER",
      stepInto: "STEP_INTO",
      stepOut: "STEP_OUT",
      runToNode: "RUN_TO_NODE",
      runToCursor: "RUN_TO_CURSOR",
      cancel: "STOP",
      stop: "STOP",
    };
    try {
      if (command === "continue" || command === "stepOver" || command === "stepInto" || command === "stepOut") {
        const validation = await validateForMode(stripTransientSchemaState(schema), "testRun");
        const gate = shouldBlockRun(validation.issues, {}, false, "saveAndRun");
        if (gate.blocked) {
          const blockingIssue = validation.issues.find(issue => issue.severity === "error");
          if (blockingIssue) {
            const selectedFlowId = blockingIssue.flowId ?? blockingIssue.edgeId;
            const selectedObjectId = selectedFlowId ? undefined : blockingIssue.objectId ?? blockingIssue.nodeId;
            const selectedCollectionId = selectedFlowId
              ? findFlowWithCollection(schema, selectedFlowId)?.collectionId
              : selectedObjectId
                ? findObjectWithCollection(schema, selectedObjectId)?.collectionId
                : blockingIssue.collectionId;
            openPropertiesPanel();
            applyPatch(
              {
                selectedObjectId,
                selectedFlowId,
                selectedCollectionId,
              },
              { pushHistory: false, skipDirty: true, skipValidate: true },
            );
          } else {
            setBottomDockMode("peek");
            setBottomTab("problems");
          }
          setBottomDockMode("peek");
          setBottomTab("problems");
          Toast.error("继续执行前校验未通过，请先修复问题。");
          return;
        }
      }
      sendDebugConnectionCommand(DEBUG_WS_COMMANDS[commandMap[command]]);
      const refreshed = await refreshDebugSession(activeDebugSession.id, schema.id);
      syncDebugSessionNavigation(refreshed, schema.id, selectedRunSession);
    } catch (error) {
      Toast.error(getEditorApiErrorMessage(error));
    }
  }, [activeDebugSession, applyPatch, refreshDebugSession, schema, schema.id, selectedRunSession, sendDebugConnectionCommand, syncDebugSessionNavigation, validateForMode]);

  const handleDebugEvaluate = useCallback(async (expression: string) => {
    if (!activeDebugSession || !expression.trim()) {
      return;
    }
    try {
      const watch = await stepDebugApiClient.evaluate(activeDebugSession.id, expression);
      setDebugWatchesBySessionId(current => ({
        ...current,
        [activeDebugSession.id]: [watch, ...(current[activeDebugSession.id] ?? []).filter(item => item.expression !== expression)].slice(0, 20),
      }));
    } catch (error) {
      Toast.error(getEditorApiErrorMessage(error));
    }
  }, [activeDebugSession, stepDebugApiClient]);

  const handleDebugSuspendPolicyChange = useCallback(async (policy: "all" | "branchOnly") => {
    if (!activeDebugSession) {
      Toast.info("当前不支持暂停策略更新。");
      return;
    }
    try {
      const result = await stepDebugApiClient.updateSuspendPolicy(activeDebugSession.id, policy);
      setDebugSuspendPolicyBySessionId(current => ({ ...current, [result.sessionId]: result.policy }));
      Toast.success(`Suspend policy updated: ${result.policy}`);
      await refreshDebugSession(activeDebugSession.id, schema.id);
    } catch (error) {
      Toast.error(getEditorApiErrorMessage(error));
    }
  }, [activeDebugSession, refreshDebugSession, schema.id, stepDebugApiClient]);

  const handleDebugMutateVariable = useCallback(async (name: string, value: string) => {
    if (!activeDebugSession) {
      Toast.info("当前不支持变量修改。");
      return;
    }
    try {
      await stepDebugApiClient.mutateVariable(activeDebugSession.id, { name, value });
      await refreshDebugSession(activeDebugSession.id, schema.id);
      Toast.success(`Variable mutated: ${name}`);
    } catch (error) {
      Toast.error(getEditorApiErrorMessage(error));
    }
  }, [activeDebugSession, refreshDebugSession, schema.id, stepDebugApiClient]);

  const handleExecuteTestRun = async (input: MicroflowTestRunInput) => {
    const microflowId = schema.id;
    const stableSchema = stripTransientSchemaState(schema);
    const validation = await validateForMode(stableSchema, "testRun");
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
      if (!pendingDebugSessionId && apiClient.enqueueMicroflowRun && apiClient.getMicroflowRunStatus) {
        const enqueued = await apiClient.enqueueMicroflowRun({
          microflowId,
          schemaId: schema.id,
          input: input.parameters,
        });
        const terminalStatus = await waitForTerminalRunStatus(enqueued.runId);
        if (terminalStatus === "queued" || terminalStatus === "running") {
          setRuntimeServiceErrorByMicroflowId(current => ({
            ...current,
            [microflowId]: `运行已入队（${enqueued.runId}），仍在执行中，请稍后刷新 Run History。`,
          }));
          setBottomDockMode("peek");
          setBottomTab("debug");
          setTestRunModalOpen(false);
          void loadRunHistory(microflowId, runHistoryFilter);
          Toast.info(`Run ${terminalStatus}`);
          return;
        }
        const session = await hydrateRunSession(microflowId, enqueued.runId);
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
        if (input.sampleId) {
          setTestRunSamplesByMicroflowId(current => ({
            ...current,
            [microflowId]: (current[microflowId] ?? []).map(sample => sample.id === input.sampleId
              ? {
                ...sample,
                previousResult: sample.lastResult,
                lastResult: session.output,
                lastStatus: session.status,
                lastRunId: session.id,
                lastRunAt: session.endedAt ?? session.startedAt,
                updatedAt: new Date().toISOString(),
              }
              : sample),
          }));
        }
        setSelectedRunIdByMicroflowId(current => ({ ...current, [microflowId]: session.id }));
        setActiveTraceFrameId(session.trace[0]?.id);
        setTestRunModalOpen(false);
        setBottomDockMode("peek");
        setBottomTab("debug");
        void loadRunHistory(microflowId, runHistoryFilter);
        Toast[terminalStatus === "success" ? "success" : terminalStatus === "cancelled" ? "warning" : "error"](`Run ${terminalStatus}`);
        return;
      }

      const debugSessionId = pendingDebugSessionId;
      const normalized = normalizeMicroflowAuthoringSchemaForRuntime(stableSchema);
      const response = await apiClient.testRunMicroflow(buildRunRequest(normalized.schema, input.parameters, input.options, true, debugSessionId));
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
      if (input.sampleId) {
        setTestRunSamplesByMicroflowId(current => ({
          ...current,
          [microflowId]: (current[microflowId] ?? []).map(sample => sample.id === input.sampleId
            ? {
              ...sample,
              previousResult: sample.lastResult,
              lastResult: session.output,
              lastStatus: session.status,
              lastRunId: session.id,
              lastRunAt: session.endedAt ?? session.startedAt,
              updatedAt: new Date().toISOString(),
            }
            : sample),
        }));
      }
      setSelectedRunIdByMicroflowId(current => ({ ...current, [microflowId]: session.id }));
      setActiveTraceFrameId(response.frames[0]?.id);
      setTestRunModalOpen(false);
      setBottomDockMode("peek");
      setBottomTab("debug");
      if (response.debugSession) {
        syncDebugSessionNavigation(response.debugSession, microflowId, session);
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
        const runtimeIssues: MicroflowValidationIssue[] = [];
        const deferredMessages: string[] = [];
        const runtimeCommandEntriesForRun: RuntimeCommandConsoleEntry[] = [];
        for (const command of response.runtimeCommands) {
          const payload = parseRuntimeCommandPayload(command.payloadJson);
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

          if (isSupportedClientRuntimeCommand(command.commandKind)) {
            const consumed = consumeRuntimeCommand(command, payload);
            runtimeCommandEntriesForRun.push(createRuntimeCommandConsoleEntry(command, consumed, response.runId));
            window.dispatchEvent(new CustomEvent("atlas:microflow-runtime-command", {
              detail: {
                command,
                payload,
                consumed,
              },
            }));
            Toast[consumed.severity](consumed.message);
            continue;
          }

          deferredMessages.push(command.commandKind);
        }

        if (runtimeIssues.length > 0) {
          setIssues([...issues, ...runtimeIssues]);
          setBottomTab("problems");
        }

        if (runtimeCommandEntriesForRun.length > 0) {
          setRuntimeCommandEntriesByMicroflowId(current => ({
            ...current,
            [microflowId]: [...runtimeCommandEntriesForRun, ...(current[microflowId] ?? [])].slice(0, 200),
          }));
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

  const handleSaveTestRunSample = (sample: Omit<MicroflowTestRunSample, "id" | "updatedAt"> & { id?: string }) => {
    const microflowId = schema.id;
    const now = new Date().toISOString();
    setTestRunSamplesByMicroflowId(current => {
      const samples = current[microflowId] ?? [];
      const id = sample.id ?? `sample-${Date.now()}`;
      const nextSample: MicroflowTestRunSample = {
        id,
        name: sample.name,
        parameters: sample.parameters,
        expectedResult: sample.expectedResult,
        lastResult: sample.lastResult,
        lastStatus: sample.lastStatus,
        lastRunId: sample.lastRunId,
        lastRunAt: sample.lastRunAt,
        previousResult: sample.previousResult,
        updatedAt: now,
      };
      return {
        ...current,
        [microflowId]: [nextSample, ...samples.filter(item => item.id !== id)].slice(0, 20),
      };
    });
  };

  const handleRunAllTestSamples = async (samples: MicroflowTestRunSample[], options?: MicroflowTestRunInput["options"]) => {
    for (const sample of samples) {
      await handleExecuteTestRun({ parameters: sample.parameters, options, sampleId: sample.id });
    }
  };

  const clearTestRun = () => {
    const debugSessionId = debugSessionByMicroflowId[activeMicroflowId]?.id;
    if (debugSessionId) {
      debugStoreRef.current.resetForSession(undefined);
      void stepDebugApiClient.deleteSession(debugSessionId).catch(() => undefined);
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

  const handleRetryQueuedRun = useCallback(async () => {
    const microflowId = schema.id;
    const runId = selectedRunIdByMicroflowId[microflowId] ?? runSessionByMicroflowId[microflowId]?.id;
    if (!runId) {
      Toast.info("当前没有可重试的运行。");
      return;
    }
    if (!apiClient.retryMicroflowRun || !apiClient.getMicroflowRunStatus) {
      Toast.info("当前运行适配器不支持队列重试。");
      return;
    }
    setRunning(true);
    try {
      const retried = await apiClient.retryMicroflowRun(runId);
      const terminalStatus = await waitForTerminalRunStatus(retried.runId);
      if (terminalStatus === "queued" || terminalStatus === "running") {
        setRuntimeServiceErrorByMicroflowId(current => ({
          ...current,
          [microflowId]: `重试已入队（${retried.runId}），仍在执行中，请稍后刷新 Run History。`,
        }));
        void loadRunHistory(microflowId, runHistoryFilter);
        Toast.info(`Retry ${terminalStatus}`);
        return;
      }
      const session = await hydrateRunSession(microflowId, retried.runId);
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
      setActiveTraceFrameId(session.trace[0]?.id);
      setBottomDockMode("peek");
      setBottomTab("debug");
      void loadRunHistory(microflowId, runHistoryFilter);
      Toast[terminalStatus === "success" ? "success" : terminalStatus === "cancelled" ? "warning" : "error"](`Retry ${terminalStatus}`);
    } catch (error) {
      setRuntimeServiceErrorByMicroflowId(current => ({ ...current, [microflowId]: getEditorApiErrorMessage(error) }));
      setBottomDockMode("peek");
      setBottomTab("debug");
      Toast.error(getEditorApiErrorMessage(error));
    } finally {
      setRunning(false);
    }
  }, [apiClient, hydrateRunSession, loadRunHistory, runHistoryFilter, runSessionByMicroflowId, schema.id, selectedRunIdByMicroflowId, waitForTerminalRunStatus]);

  const describeRetentionResult = useCallback((result: {
    dryRun: boolean;
    cutoffAt?: string;
    candidateRuns?: number;
    deletedRuns: number;
    deletedTraceFrames: number;
    deletedLogs?: number;
    sampleRunIds?: string[];
  }) => {
    const mode = result.dryRun ? "dry-run" : "execute";
    const samples = (result.sampleRunIds ?? []).slice(0, 3);
    return [
      `Retention ${mode} completed`,
      `candidates=${result.candidateRuns ?? 0}`,
      `deletedRuns=${result.deletedRuns}`,
      `deletedTraces=${result.deletedTraceFrames}`,
      `deletedLogs=${result.deletedLogs ?? 0}`,
      result.cutoffAt ? `cutoff=${result.cutoffAt}` : "",
      samples.length > 0 ? `sample=${samples.join(",")}` : "",
    ].filter(Boolean).join(" · ");
  }, []);

  const handleRetentionDryRun = useCallback(async () => {
    if (!apiClient.runRetention) {
      Toast.info("当前运行适配器不支持 retention。");
      return;
    }
    try {
      const result = await apiClient.runRetention({ dryRun: true, retainDays: 30 });
      Toast.success(describeRetentionResult(result));
    } catch (error) {
      Toast.error(getEditorApiErrorMessage(error));
    }
  }, [apiClient, describeRetentionResult]);

  const handleRetentionExecute = useCallback(() => {
    const runRetention = apiClient.runRetention;
    if (!runRetention) {
      Toast.info("当前运行适配器不支持 retention。");
      return;
    }
    Modal.confirm({
      title: "执行 Retention 清理",
      content: "将按 30 天阈值正式删除候选 run/trace/log。该操作不可撤销，是否继续？",
      okText: "执行清理",
      okButtonProps: { type: "danger" },
      cancelText: "取消",
      onOk: async () => {
        try {
          const result = await runRetention({ dryRun: false, retainDays: 30 });
          Toast.success(describeRetentionResult(result));
        } catch (error) {
          Toast.error(getEditorApiErrorMessage(error));
        }
      },
    });
  }, [apiClient, describeRetentionResult]);

  const handleRetentionPreview = useCallback(async () => {
    if (!apiClient.runRetention) {
      Toast.info("当前运行适配器不支持 retention。");
      return;
    }
    try {
      const result = await apiClient.runRetention({ dryRun: true, retainDays: 30 });
      Modal.info({
        title: "Retention 预览结果",
        content: (
          <div>
            <div>{describeRetentionResult(result)}</div>
            {(result.sampleRunIds ?? []).length > 0 ? (
              <div style={{ marginTop: 8 }}>
                <Text type="tertiary" size="small">Sample runIds: {(result.sampleRunIds ?? []).slice(0, 20).join(", ")}</Text>
              </div>
            ) : null}
          </div>
        ),
      });
    } catch (error) {
      Toast.error(getEditorApiErrorMessage(error));
    }
  }, [apiClient, describeRetentionResult]);

  const selectTraceFrame = (frame: MicroflowTraceFrame) => {
    setActiveTraceFrameId(frame.id);
    openPropertiesPanel();
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
    emitPanelSyncEvent({
      type: "trace-focus",
      nodeId: targetObjectId,
      frameId: frame.id,
    });
  };

  const selectTraceFlow = (flowId: string) => {
    openPropertiesPanel();
    const located = findFlowWithCollection(schema, flowId);
    applyPatch({
      selectedObjectId: undefined,
      selectedFlowId: flowId,
      selectedCollectionId: located?.collectionId,
    }, { pushHistory: false, skipDirty: true, skipValidate: true, source: "runtime" });
    emitPanelSyncEvent({
      type: "trace-focus",
      flowId,
    });
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
        openPropertiesPanel();
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
      x: Math.round(target.x * zoom - 360),
      y: Math.round(target.y * zoom - 260),
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

  const focusProblemIssue = useCallback((issue: MicroflowValidationIssue) => {
    const selectedFlowId = issue.flowId ?? issue.edgeId;
    const selectedObjectId = selectedFlowId ? undefined : issue.objectId ?? issue.nodeId;
    const selectedCollectionId = selectedFlowId
      ? findFlowWithCollection(schema, selectedFlowId)?.collectionId
      : selectedObjectId
        ? findObjectWithCollection(schema, selectedObjectId)?.collectionId
        : issue.collectionId;
    setBottomDockMode("peek");
    setBottomTab("problems");
    openPropertiesPanel();
    applyPatch(
      {
        selectedObjectId,
        selectedFlowId,
        selectedCollectionId,
        viewport: viewportForProblemIssue(issue),
      },
      { pushHistory: false, skipDirty: true, skipValidate: true },
    );
  }, [applyPatch, schema]);

  const viewportForCanvasContextSelection = (selection?: CanvasNodeContextMenuState) => {
    if (!selection) {
      return undefined;
    }
    if (selection.flowId) {
      const edge = graph.edges.find(item => item.flowId === selection.flowId);
      const source = edge ? graph.nodes.find(item => item.objectId === edge.sourceObjectId) : undefined;
      const target = edge ? graph.nodes.find(item => item.objectId === edge.targetObjectId) : undefined;
      if (source && target) {
        return viewportCenteredOn({
          x: (source.position.x + target.position.x) / 2,
          y: (source.position.y + target.position.y) / 2,
        });
      }
    }
    return viewportCenteredOn(graph.nodes.find(item => item.objectId === selection.objectId)?.position);
  };

  const handleCanvasContextDuplicate = () => {
    if (props.readonly || !canvasNodeContextMenu) {
      return;
    }
    const objectId = canvasNodeContextMenu.objectId;
    const flowId = canvasNodeContextMenu.flowId;
    if (objectId) {
      if (isDesignSchema(schema)) {
        commitSchema(
          duplicateDesignSelection(schema, [objectId], []) as unknown as MicroflowSchema,
          "addNode",
          { historyLabel: labels.contextDuplicate, source: "flowgram" },
        );
      } else {
        const located = findObjectWithCollection(schema, objectId);
        commitSchema(duplicateObject(schema, objectId), located?.parentLoopObjectId ? "addLoopNode" : "addNode", { source: "flowgram" });
      }
      setCanvasNodeContextMenu(undefined);
      Toast.success(labels.contextDuplicate);
      return;
    }
    if (flowId && !isDesignSchema(schema)) {
      const locatedFlow = findFlowWithCollection(schema, flowId);
      commitSchema(
        duplicateObjectSelection(schema, { objectIds: [], flowIds: [flowId] }),
        locatedFlow?.parentLoopObjectId ? "addLoopFlow" : "addFlow",
        { historyLabel: labels.contextDuplicate, source: "flowgram" },
      );
      setCanvasNodeContextMenu(undefined);
      Toast.success(labels.contextDuplicate);
      return;
    }
    if (flowId && isDesignSchema(schema)) {
      Toast.info("当前设计模式不支持连线快捷复制。");
    }
    setCanvasNodeContextMenu(undefined);
  };

  const handleCanvasContextRename = () => {
    if (props.readonly || !canvasNodeContextMenu?.objectId) {
      return;
    }
    const objectId = canvasNodeContextMenu.objectId;
    const currentTitle = isDesignSchema(schema)
      ? (schema.workflow.nodes.find(node => node.id === objectId)?.data?.title ?? objectId)
      : (findObject(schema, objectId)?.caption ?? objectId);
    const value = window.prompt(labels.contextRename, String(currentTitle));
    if (!value) {
      return;
    }
    const nextTitle = value.trim();
    if (!nextTitle) {
      Toast.warning("名称不能为空。");
      return;
    }
    if (isDesignSchema(schema)) {
      commitSchema({
        ...schema,
        workflow: {
          ...schema.workflow,
          nodes: schema.workflow.nodes.map(node => node.id === objectId ? {
            ...node,
            data: { ...node.data, title: nextTitle },
          } : node),
        },
      } as unknown as MicroflowSchema, "updateNodeProperty", { source: "flowgram" });
    } else {
      const located = findObjectWithCollection(schema, objectId);
      commitSchema(
        updateObject(schema, objectId, nextObject => ({ ...nextObject, title: nextTitle })),
        located?.parentLoopObjectId ? "addLoopNode" : "addNode",
        { source: "flowgram" },
      );
    }
    setCanvasNodeContextMenu(undefined);
  };

  const handleCanvasContextDelete = () => {
    if (props.readonly || !canvasNodeContextMenu) {
      return;
    }
    const currentSchema = latestSchemaRef.current;
    const objectIds = canvasNodeContextMenu.objectId ? [canvasNodeContextMenu.objectId] : [];
    const flowIds = canvasNodeContextMenu.flowId ? [canvasNodeContextMenu.flowId] : [];
    if (objectIds.length === 0 && flowIds.length === 0) {
      return;
    }
    if (objectIds.length === 1 && flowIds.length === 0) {
      const objectId = objectIds[0];
      if (isDesignSchema(currentSchema)) {
        commitSchema(
          deleteDesignSelection(currentSchema, objectIds, flowIds) as unknown as MicroflowSchema,
          "flowgramNodeDelete",
          { historyLabel: "Delete selection", source: "flowgram" },
        );
      } else {
        const located = findObjectWithCollection(currentSchema, objectId);
        if (!located) {
          setCanvasNodeContextMenu(undefined);
          return;
        }
        commitSchema(
          deleteObject(currentSchema, objectId),
          located.parentLoopObjectId ? "deleteLoopNode" : "deleteNode",
          { source: "flowgram" },
        );
      }
      Toast.info({ content: "已删除节点，可按 Ctrl+Z 撤销", duration: 4 });
      setCanvasNodeContextMenu(undefined);
      return;
    }
    if (flowIds.length === 1 && objectIds.length === 0) {
      const flowId = flowIds[0];
      if (isDesignSchema(currentSchema)) {
        commitSchema(
          deleteDesignSelection(currentSchema, objectIds, flowIds) as unknown as MicroflowSchema,
          "flowgramLineDelete",
          { historyLabel: "Delete selection", source: "flowgram" },
        );
      } else {
        const located = findFlowWithCollection(currentSchema, flowId);
        if (!located) {
          setCanvasNodeContextMenu(undefined);
          return;
        }
        commitSchema(
          deleteFlow(currentSchema, flowId),
          located.parentLoopObjectId ? "deleteLoopFlow" : "deleteFlow",
          { source: "flowgram" },
        );
      }
      Toast.info({ content: "已删除连线，可按 Ctrl+Z 撤销", duration: 4 });
      setCanvasNodeContextMenu(undefined);
      return;
    }
    if (isDesignSchema(currentSchema)) {
      const selectedObjectCount = objectIds.length;
      const selectedFlowCount = flowIds.length;
      Modal.confirm({
        title: "删除前影响确认",
        okText: "确认删除",
        okButtonProps: { type: "danger" },
        cancelText: "取消",
        content: `将删除节点 ${selectedObjectCount} 个、连线 ${selectedFlowCount} 条。`,
        onOk: () => {
          commitSchema(
            deleteDesignSelection(currentSchema, objectIds, flowIds) as unknown as MicroflowSchema,
            selectedObjectCount > 0 ? "flowgramNodeDelete" : "flowgramNodeDelete",
            { historyLabel: "Delete selection", source: "flowgram" },
          );
        },
      });
      setCanvasNodeContextMenu(undefined);
      return;
    }
    confirmDeleteTargets(objectIds, flowIds, "flowgram");
    setCanvasNodeContextMenu(undefined);
  };

  const handleCanvasContextCenter = () => {
    if (!canvasNodeContextMenu) {
      return;
    }
    const selectedFlowId = canvasNodeContextMenu.flowId;
    const selectedObjectId = selectedFlowId ? undefined : canvasNodeContextMenu.objectId;
    applyPatch({
      selectedObjectId,
      selectedFlowId,
      selectedCollectionId: selectedFlowId
        ? findFlowWithCollection(schema, selectedFlowId)?.collectionId
        : selectedObjectId
          ? findObjectWithCollection(schema, selectedObjectId)?.collectionId
          : canvasNodeContextMenu.collectionId,
      viewport: viewportForCanvasContextSelection(canvasNodeContextMenu),
    }, { pushHistory: false, skipDirty: true, skipValidate: true });
    setCanvasNodeContextMenu(undefined);
  };

  const handleCanvasContextCopyId = () => {
    const targetId = canvasNodeContextMenu?.objectId ?? canvasNodeContextMenu?.flowId;
    if (!targetId) {
      return;
    }
    const copyText = async () => {
      if (typeof navigator === "undefined" || !navigator.clipboard?.writeText) {
        window.prompt("请复制以下 ID", targetId);
        return;
      }
      try {
        await navigator.clipboard.writeText(targetId);
        Toast.success(`已复制: ${targetId}`);
      } catch (error) {
        Toast.error(`复制失败: ${error instanceof Error ? error.message : "请稍后重试"}`);
      }
    };
    void copyText();
    setCanvasNodeContextMenu(undefined);
  };

  const handleCanvasContextToggleBreakpoint = async () => {
    const objectId = canvasNodeContextMenu?.objectId;
    if (props.readonly || !objectId) {
      Toast.info("当前不支持在此模式下编辑断点。");
      return;
    }

    let session = activeDebugSession;
    if (!session) {
      session = await stepDebugApiClient.createSession(schema.id);
      debugStoreRef.current.resetForSession(session.id);
      setDebugSessionByMicroflowId(current => ({ ...current, [schema.id]: session }));
      setDebugSuspendPolicyBySessionId(current => ({ ...current, [session.id]: current[session.id] ?? "all" }));
    }

    const existingBreakpoint = session.breakpoints?.find(item =>
      item.enabled !== false
      && item.microflowObjectId === objectId
      && normalizeDebugBreakpointScope(item.scope) === "node",
    );

    try {
      const next = existingBreakpoint
        ? await stepDebugApiClient.removeBreakpoint(session.id, existingBreakpoint.id)
        : await stepDebugApiClient.upsertBreakpoint(session.id, {
          id: `bp-node-${objectId}`,
          microflowObjectId: objectId,
          scope: 0,
          stale: false,
          enabled: true,
          suspendPolicy: activeDebugSuspendPolicy === "branchOnly" ? 2 : 0,
        });
      if (existingBreakpoint) {
        sendDebugConnectionCommand(DEBUG_WS_COMMANDS.REMOVE_BP, { breakpoint: { id: next.id, nodeId: objectId } });
      } else {
        sendDebugConnectionCommand(DEBUG_WS_COMMANDS.SET_BP, { breakpoint: { id: `bp-node-${objectId}`, nodeId: objectId, scope: "node", enabled: true } });
      }
      setDebugSessionByMicroflowId(current => ({ ...current, [schema.id]: next }));
      await refreshDebugSession(next.id, schema.id);
      Toast.success(existingBreakpoint ? labels.contextRemoveBreakpoint : labels.contextAddBreakpoint);
    } catch (error) {
      Toast.error(getEditorApiErrorMessage(error));
    } finally {
      setCanvasNodeContextMenu(undefined);
      setBottomDockMode("peek");
      setBottomTab("debug");
    }
  };

  const handleCanvasContextToggleDisabled = () => {
    const objectId = canvasNodeContextMenu?.objectId;
    if (props.readonly || !objectId) {
      return;
    }
    if (isDesignSchema(schema)) {
      const currentNode = schema.workflow.nodes.find(node => node.id === objectId);
      if (!currentNode) {
        return;
      }
      const nextDisabled = !Boolean(currentNode.data?.disabled);
      commitSchema({
        ...schema,
        workflow: {
          ...schema.workflow,
          nodes: schema.workflow.nodes.map(node => node.id === objectId ? {
            ...node,
            data: { ...node.data, disabled: nextDisabled },
          } : node),
        },
      } as unknown as MicroflowSchema, "updateNodeProperty", { source: "flowgram" });
    } else {
      commitSchema(
        updateObject(schema, objectId, nextObject => ({ ...nextObject, disabled: !Boolean(nextObject.disabled) })),
        "updateNodeProperty",
        { source: "flowgram" },
      );
    }
    setCanvasNodeContextMenu(undefined);
  };

  const handleApplyProblemQuickFix = (issue: MicroflowValidationIssue) => {
    if (props.readonly) {
      return;
    }
    const nextSchema = createMissingBooleanBranch(schema, issue);
    if (!nextSchema) {
      Toast.warning(labels.quickFixUnavailable);
      return;
    }
    commitSchema(nextSchema, "addNode", { historyLabel: "Create missing Decision branch", source: "propertyPanel" });
    emitPanelSyncEvent({
      type: "problem-fix",
      issueId: issue.id,
      nodeId: issue.objectId ?? issue.nodeId,
      flowId: issue.flowId ?? issue.edgeId,
    });
    setBottomDockMode("peek");
    setBottomTab("problems");
    Toast.success(labels.missingDecisionBranchCreated);
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
    Toast.info({ content: "已自动排版，可按 Ctrl+Z 撤销", duration: 4 });
  };

  const collectDeleteImpact = useCallback((objectIds: string[], flowIds: string[]) => {
    const uniqueObjectIds = [...new Set(objectIds)];
    const uniqueFlowIds = [...new Set(flowIds)];
    const objectIdSet = new Set(uniqueObjectIds);
    const selectedFlowIdSet = new Set(uniqueFlowIds);
    const allFlows = collectFlowsRecursive(schema);
    const cascadedFlowIds = allFlows
      .filter(flow => (objectIdSet.has(flow.originObjectId) || objectIdSet.has(flow.destinationObjectId)) && !selectedFlowIdSet.has(flow.id))
      .map(flow => flow.id);
    const objectKinds = uniqueObjectIds
      .map(id => findObject(schema, id)?.kind)
      .filter((kind): kind is NonNullable<MicroflowObject["kind"]> => Boolean(kind));
    const splitNodeCount = objectKinds.filter(kind => kind === "exclusiveSplit" || kind === "inheritanceSplit" || kind === "parallelGateway" || kind === "inclusiveGateway").length;
    const callMicroflowCount = uniqueObjectIds
      .map(id => findObject(schema, id))
      .filter((node): node is Extract<MicroflowObject, { kind: "actionActivity" }> => Boolean(node && node.kind === "actionActivity"))
      .filter(node => node.action.kind === "callMicroflow")
      .length;
    return {
      nodeCount: uniqueObjectIds.length,
      selectedFlowCount: uniqueFlowIds.length,
      cascadedFlowCount: cascadedFlowIds.length,
      splitNodeCount,
      callMicroflowCount,
      totalElements: uniqueObjectIds.length + uniqueFlowIds.length + cascadedFlowIds.length,
    };
  }, [schema]);

  const applyDeleteTargets = useCallback((objectIds: string[], flowIds: string[], source: MicroflowSchemaChangeSource) => {
    const uniqueObjectIds = [...new Set(objectIds)];
    const uniqueFlowIds = [...new Set(flowIds)];
    const totalCount = uniqueObjectIds.length + uniqueFlowIds.length;
    if (totalCount === 0) {
      return;
    }
    if (uniqueFlowIds.length > 1 || uniqueObjectIds.length > 1 || totalCount > 1) {
      let next = schema;
      for (const flowId of uniqueFlowIds) {
        if (findFlowWithCollection(next, flowId)) {
          next = deleteFlow(next, flowId);
        }
      }
      for (const objectId of uniqueObjectIds) {
        if (findObjectWithCollection(next, objectId)) {
          next = deleteObject(next, objectId);
        }
      }
      commitSchema(next, "bulkUpdate", { historyLabel: "Delete selection", source });
      Toast.info({ content: `已删除 ${totalCount} 个元素，可按 Ctrl+Z 撤销`, duration: 4 });
      return;
    }
    if (uniqueFlowIds.length === 1) {
      const flowId = uniqueFlowIds[0];
      const located = findFlowWithCollection(schema, flowId);
      if (!located) {
        return;
      }
      commitSchema(deleteFlow(schema, flowId), located.parentLoopObjectId ? "deleteLoopFlow" : "deleteFlow", { source });
      Toast.info({ content: "已删除连线，可按 Ctrl+Z 撤销", duration: 4 });
      return;
    }
    if (uniqueObjectIds.length === 1) {
      const objectId = uniqueObjectIds[0];
      const located = findObjectWithCollection(schema, objectId);
      if (!located) {
        return;
      }
      const node = findObject(schema, objectId);
      const nodeName = (node as { title?: string } | undefined)?.title ?? "节点";
      commitSchema(deleteObject(schema, objectId), located.parentLoopObjectId ? "deleteLoopNode" : "deleteNode", { source });
      Toast.info({ content: `已删除"${nodeName}"，可按 Ctrl+Z 撤销`, duration: 4 });
    }
  }, [schema]);

  const confirmDeleteTargets = useCallback((objectIds: string[], flowIds: string[], source: MicroflowSchemaChangeSource) => {
    const uniqueObjectIds = [...new Set(objectIds)];
    const uniqueFlowIds = [...new Set(flowIds)];
    const totalCount = uniqueObjectIds.length + uniqueFlowIds.length;
    if (totalCount === 0) {
      return;
    }
    const impact = collectDeleteImpact(uniqueObjectIds, uniqueFlowIds);
    Modal.confirm({
      title: "删除前影响确认",
      okText: "确认删除",
      okButtonProps: { type: "danger" },
      cancelText: "取消",
      content: (
        <Space vertical align="start" spacing={6}>
          <Text>将删除节点 {impact.nodeCount} 个、连线 {impact.selectedFlowCount} 条。</Text>
          {impact.cascadedFlowCount > 0 ? (
            <Text type="warning">受节点删除影响，还会级联删除额外连线 {impact.cascadedFlowCount} 条。</Text>
          ) : null}
          {impact.splitNodeCount > 0 ? (
            <Text type="warning">包含 {impact.splitNodeCount} 个分支/网关节点，可能影响下游分支路径。</Text>
          ) : null}
          {impact.callMicroflowCount > 0 ? (
            <Text type="warning">包含 {impact.callMicroflowCount} 个 Call Microflow 节点，可能影响调用链路。</Text>
          ) : null}
          <Text type="tertiary" size="small">本次影响元素总计约 {impact.totalElements} 项，删除后可使用 Ctrl+Z 撤销。</Text>
        </Space>
      ),
      onOk: () => {
        applyDeleteTargets(uniqueObjectIds, uniqueFlowIds, source);
      },
    });
  }, [applyDeleteTargets, collectDeleteImpact]);

  const handleDeleteSelection = () => {
    if (props.readonly) {
      return;
    }
    const selection = schema.editor.selection;
    const flowIds = [...new Set([...(selection.flowIds ?? []), selection.flowId].filter((id): id is string => Boolean(id)))];
    const objectIds = [...new Set([...(selection.objectIds ?? []), selection.objectId].filter((id): id is string => Boolean(id)))];
    if (isDesignSchema(schema)) {
      if (objectIds.length === 0 && flowIds.length === 0) {
        return;
      }
      commitSchema(
        deleteDesignSelection(schema, objectIds, flowIds) as unknown as MicroflowSchema,
        "flowgramNodeDelete",
        { historyLabel: "Delete selection", source: "flowgram" },
      );
      return;
    }
    confirmDeleteTargets(objectIds, flowIds, "flowgram");
  };

  const handleSelectAll = () => {
    if (isDesignSchema(schema)) {
      const objectIds = schema.workflow.nodes.map(node => node.id);
      if (objectIds.length === 0) return;
      commitSchema({
        ...schema,
        editor: {
          ...schema.editor,
          selection: {
            ...schema.editor.selection,
            objectId: objectIds[0],
            flowId: undefined,
            objectIds,
            flowIds: [],
            mode: objectIds.length > 1 ? "multi" : "single",
          },
        },
      } as unknown as MicroflowSchema, "bulkUpdate", { pushHistory: false, skipDirty: true, skipValidate: true, preserveSelection: true, source: "flowgram" });
      return;
    }
    const objectIds = graph.nodes.map(n => n.objectId);
    if (objectIds.length === 0) return;
    applyPatch(
      {
        selectedObjectIds: objectIds,
        selectedFlowIds: [],
        selectionMode: "multi",
      },
      { pushHistory: false, skipDirty: true, skipValidate: true, preserveSelection: true, source: "flowgram" },
    );
  };

  const handleDuplicateSelection = () => {
    if (props.readonly) return;
    const selection = schema.editor.selection;
    if (isDesignSchema(schema)) {
      const objectIds = [...new Set([...(selection.objectIds ?? []), selection.objectId].filter((id): id is string => Boolean(id && schema.workflow.nodes.some(node => node.id === id))))];
      const flowIds = [...new Set([...(selection.flowIds ?? []), selection.flowId].filter((id): id is string => Boolean(id && schema.workflow.edges.some(edge => designEdgeId(edge) === id))))];
      if (objectIds.length === 0) {
        Toast.info("请先选择节点再复制。");
        return;
      }
      commitSchema(
        duplicateDesignSelection(schema, objectIds, flowIds) as unknown as MicroflowSchema,
        "addNode",
        { historyLabel: "Duplicate selection", source: "flowgram" },
      );
      Toast.success(objectIds.length > 1 ? `已复制 ${objectIds.length} 个节点。` : "已复制节点。");
      return;
    }
    const objectIds = [...new Set([...(selection.objectIds ?? []), selection.objectId].filter((id): id is string => Boolean(id && findObject(schema, id))))];
    const flowIds = [...new Set([...(selection.flowIds ?? []), selection.flowId].filter((id): id is string => Boolean(id && findFlowWithCollection(schema, id))))];
    if (objectIds.length === 0) {
      Toast.info("请先选择节点再复制。");
      return;
    }
    const source = findObjectWithCollection(schema, objectIds[0]);
    const nextSchema = objectIds.length > 1 || flowIds.length > 0
      ? duplicateObjectSelection(schema, { objectIds, flowIds })
      : duplicateObject(schema, objectIds[0]);
    commitSchema(nextSchema, source?.parentLoopObjectId ? "addLoopNode" : "addNode", { historyLabel: "Duplicate selection", source: "flowgram" });
    Toast.success(objectIds.length > 1 ? `已复制 ${objectIds.length} 个节点。` : "已复制节点。");
  };

  const handleMoveSelection = (dx: number, dy: number) => {
    if (props.readonly) return;
    const selection = schema.editor.selection;
    const objectIds = [...new Set([...(selection.objectIds ?? []), selection.objectId].filter((id): id is string => Boolean(id)))];
    if (objectIds.length === 0) return;
    if (isDesignSchema(schema)) {
      const moved = new Set(objectIds);
      commitSchema({
        ...schema,
        workflow: {
          ...schema.workflow,
          nodes: schema.workflow.nodes.map(node => {
            if (!moved.has(node.id)) {
              return node;
            }
            return {
              ...node,
              meta: {
                ...node.meta,
                position: {
                  x: Number(node.meta?.position?.x ?? 0) + dx,
                  y: Number(node.meta?.position?.y ?? 0) + dy,
                },
              },
            };
          }),
        },
      } as unknown as MicroflowSchema, "moveNode", { source: "flowgram" });
      return;
    }
    let next = schema;
    for (const objectId of objectIds) {
      if (findObjectWithCollection(next, objectId)) {
        next = moveObject(next, objectId, { x: dx, y: dy });
      }
    }
    // commitSchema 对 "moveNode" 内置了 debounce 历史推送，直接调用即可
    commitSchema(next, "moveNode", { source: "flowgram" });
  };

  const handleCopySelection = () => {
    const selection = schema.editor.selection;
    if (isDesignSchema(schema)) {
      const objectIds = [...new Set([...(selection.objectIds ?? []), selection.objectId].filter((id): id is string => Boolean(id && schema.workflow.nodes.some(node => node.id === id))))];
      const flowIds = [...new Set([...(selection.flowIds ?? []), selection.flowId].filter((id): id is string => Boolean(id && schema.workflow.edges.some(edge => designEdgeId(edge) === id))))];
      if (objectIds.length === 0) {
        Toast.info("请选择一个节点后复制。");
        return;
      }
      setClipboardObject({ microflowId: schema.id, objectIds, flowIds });
      Toast.success(objectIds.length > 1 ? `已复制 ${objectIds.length} 个节点。` : "已复制节点。");
      return;
    }
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
    if (isDesignSchema(schema)) {
      const objectIds = clipboardObject.objectIds.filter(id => schema.workflow.nodes.some(node => node.id === id));
      const flowIds = clipboardObject.flowIds.filter(id => schema.workflow.edges.some(edge => designEdgeId(edge) === id));
      if (objectIds.length === 0) {
        Toast.warning("复制的源节点已不存在。");
        return;
      }
      commitSchema(
        duplicateDesignSelection(schema, objectIds, flowIds) as unknown as MicroflowSchema,
        "addNode",
        { historyLabel: "Paste selection", source: "flowgram" },
      );
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

  const handleEnterInlineEdit = useCallback(() => {
    const selection = schema.editor.selection;
    const selectedObjectId = selection.objectId
      ?? selection.objectIds?.find(id => Boolean(id && findObjectWithCollection(schema, id)));
    if (!selectedObjectId) {
      Toast.info("请先选中一个节点，再进入内敛编辑。");
      return;
    }
    setNodeViewModes(current => ({ ...current, [selectedObjectId]: "expanded" }));
    openPropertiesPanel();
    applyPatch(
      {
        selectedObjectId,
        selectedFlowId: undefined,
        selectedCollectionId: findObjectWithCollection(schema, selectedObjectId)?.collectionId,
      },
      { pushHistory: false, skipDirty: true, skipValidate: true, source: "flowgram" },
    );
    if (runtimeInlineReadonly) {
      Toast.info("运行中当前为只读；请在暂停点提交变更。");
    }
  }, [applyPatch, runtimeInlineReadonly, schema]);

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
    if ((event.ctrlKey || event.metaKey) && key === "e") {
      event.preventDefault();
      handleEnterInlineEdit();
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

  const openNodePanel = useCallback(() => {
    setLeftOpen(true);
    setRightOpen(false);
  }, []);

  const closeNodePanel = useCallback(() => {
    setLeftOpen(false);
  }, []);

  const openPropertiesPanel = useCallback(() => {
    setLeftOpen(false);
    setRightOpen(true);
  }, []);

  const closePropertiesPanel = useCallback(() => {
    setRightOpen(false);
  }, []);

  const toggleNodePanel = useCallback(() => {
    if (leftOpen) {
      closeNodePanel();
      return;
    }
    openNodePanel();
  }, [closeNodePanel, leftOpen, openNodePanel]);

  const togglePropertiesPanel = useCallback(() => {
    if (rightOpen) {
      closePropertiesPanel();
      return;
    }
    openPropertiesPanel();
  }, [closePropertiesPanel, openPropertiesPanel, rightOpen]);

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
    openNodePanel();
    window.setTimeout(() => {
      const input = shellRef.current?.querySelector<HTMLInputElement>(".microflow-node-search-input input, input.microflow-node-search-input");
      input?.focus();
      input?.select();
    }, 0);
  }, [openNodePanel]);

  const openCommandPalette = useCallback(() => {
    setCommandPaletteQuery("");
    setCommandPaletteOpen(true);
  }, []);

  const clearSelection = useCallback(() => {
    if (canvasNodeContextMenu) {
      setCanvasNodeContextMenu(undefined);
      return;
    }
    if (leftOpen) {
      closeNodePanel();
      return;
    }
    if (rightOpen) {
      closePropertiesPanel();
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
  }, [bottomDockMode, canvasNodeContextMenu, closeNodePanel, closePropertiesPanel, leftOpen, rightOpen, schema]);

  const resetWorkbenchLayout = useCallback(() => {
    setFocusMode(false);
    setFullscreenActive(false);
    if (Boolean(props.defaultRightPanelOpen ?? props.immersive)) {
      openPropertiesPanel();
    } else {
      openNodePanel();
    }
    setRightPinned(false);
    setBottomDockHeight(BOTTOM_DOCK_FULL_DEFAULT_PX);
    setBottomDockMode(bottomPanelFallbackMode);
    setBottomTab("problems");
    setCanvasPanToolActive(false);
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
  }, [bottomPanelFallbackMode, commitSchema, openNodePanel, openPropertiesPanel, props.defaultRightPanelOpen, props.immersive, schema]);

  useMicroflowShortcuts({
    containerRef: shellRef,
    readonly: props.readonly,
    onUndo: handleUndo,
    onRedo: handleRedo,
    onSave: () => void handleSave(),
    onSearch: focusNodeSearch,
    onSearchAll: openCommandPalette,
    onStepInto: () => void handleDebugCommand("stepInto"),
    onStepOver: () => void handleDebugCommand("stepOver"),
    onStepOut: () => void handleDebugCommand("stepOut"),
    onContinue: () => void handleDebugCommand("continue"),
    onCopySelection: handleCopySelection,
    onPasteSelection: handlePasteSelection,
    onDeleteSelection: handleDeleteSelection,
    onEscape: clearSelection,
    onFocusMode: () => setFocusMode(value => !value),
    onSelectAll: handleSelectAll,
    onDuplicateSelection: handleDuplicateSelection,
    onFitView: () => window.dispatchEvent(new CustomEvent("atlas:microflow-flowgram-fit-view")),
    onMoveSelection: handleMoveSelection,
  });

  const [fullscreenActive, setFullscreenActive] = useState(false);
  const [selectedUsageVariableName, setSelectedUsageVariableName] = useState<string>();
  const layoutState = useMemo<MicroflowWorkbenchLayoutState>(() => ({
    shellMode,
    activeBottomTab: bottomTab,
    bottomDockMode,
    focusMode,
    minimapVisible: schema.editor.showMiniMap === true,
    gridVisible: schema.editor.gridEnabled !== false,
  }), [bottomDockMode, bottomTab, focusMode, schema.editor.gridEnabled, schema.editor.showMiniMap, shellMode]);
  const complexitySummary = useMemo(() => summarizeMicroflowComplexity(schema), [schema]);
  const usageHighlights = useMemo(() => {
    const authoringSchema = isDesignSchema(schema)
      ? buildDesignPropertyPanelModel(schema).authoringSchema
      : schema;
    if (selectedUsageVariableName) {
      return buildVariableUsageHighlights(authoringSchema, selectedUsageVariableName);
    }
    const selectedObjectId = schema.editor.selection?.objectId ?? schema.editor.selectedObjectId;
    if (!selectedObjectId) {
      return undefined;
    }
    return buildNodeUsageHighlights(authoringSchema, selectedObjectId);
  }, [schema, selectedUsageVariableName]);
  const selectedUsageObjectId = schema.editor.selection?.objectId ?? schema.editor.selectedObjectId;
  const selectedUsageFlowId = schema.editor.selection?.flowId ?? schema.editor.selectedFlowId;
  useEffect(() => {
    setSelectedUsageVariableName(undefined);
  }, [selectedUsageFlowId, selectedUsageObjectId]);
  const handleHighlightVariableUsage = useCallback((variableName?: string) => {
    const trimmed = String(variableName ?? "").trim();
    if (!trimmed) {
      setSelectedUsageVariableName(undefined);
      return;
    }
    const normalized = trimmed.startsWith("$") ? trimmed : `$${trimmed}`;
    setSelectedUsageVariableName(current => current === normalized ? undefined : normalized);
  }, []);
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
      nodeElementCount: complexitySummary.totalElements,
      recommendedMaxNodeCount: complexitySummary.recommendedMaxNodes,
      nodeCountLevel: complexitySummary.level,
      annotationRecommended: complexitySummary.annotationRecommended,
      hasAnnotation: complexitySummary.hasAnnotation,
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
      canvasPanToolActive,
      layout: layoutState,
    };
  }, [activeDebugSession?.lastUpdatedAt, bottomDockMode, bottomTab, canvasPanToolActive, complexitySummary, dirty, focusMode, fullscreenActive, historyState.canRedo, historyState.canUndo, issues, layoutState, runSession, running, saving, schema.editor.viewport, schema.editor.zoom, schema.id, schema.schemaVersion, selectedRunSession, validationStatus]);

  useEffect(() => {
    props.onLayoutStateChange?.(layoutState);
  }, [layoutState, props.onLayoutStateChange]);

  useEffect(() => {
    props.onWorkbenchStatusChange?.(workbenchStatus);
  }, [props.onWorkbenchStatusChange, workbenchStatus]);

  useEffect(() => {
    const resolveInlineNodeId = (detail?: { nodeId?: string; runtimeNodeId?: string }) => detail?.nodeId ?? detail?.runtimeNodeId;
    const resolveInlineNodeAliases = (seed: string): string[] => {
      const trimmed = seed.trim();
      if (!trimmed) return [];
      if (trimmed.startsWith("node-")) {
        return [trimmed, trimmed.slice("node-".length)];
      }
      return [trimmed, `node-${trimmed}`];
    };
    const resolveInlineNodeIds = (detail?: { nodeId?: string; runtimeNodeId?: string }): string[] => {
      const seeds = [detail?.nodeId, detail?.runtimeNodeId].filter((item): item is string => Boolean(item)).map(item => item.trim()).filter(Boolean);
      if (seeds.length === 0) return [];

      const aliases = new Set<string>();
      for (const seed of seeds) {
        for (const alias of resolveInlineNodeAliases(seed)) {
          aliases.add(alias);
        }
      }

      const nodes = (isDesignSchema(schema)
        ? (schema.workflow.nodes as Array<{ id: string; data?: unknown }> | undefined)
        : undefined) ?? [];
      const matchedNodeIds = new Set<string>();
      for (const node of nodes) {
        const data = (node.data ?? {}) as { objectId?: unknown };
        const candidates = [node.id, typeof data.objectId === "string" ? data.objectId : undefined].filter((item): item is string => Boolean(item));
        const matched = candidates.some(candidate => {
          const normalized = candidate.startsWith("node-") ? candidate.slice("node-".length) : candidate;
          return aliases.has(candidate) || aliases.has(normalized) || aliases.has(`node-${normalized}`);
        });
        if (matched) {
          matchedNodeIds.add(node.id);
        }
      }
      if (matchedNodeIds.size > 0) {
        return [...matchedNodeIds];
      }
      // Fallback: keep original seed so we at least store a mode for something.
      return [seeds[0]!];
    };
    const onNodeToggleDetail = (detail: MicroflowInlineNodeToggleDetail) => {
      const nodeIds = resolveInlineNodeIds(detail);
      if (nodeIds.length === 0) {
        return;
      }
      setNodeViewModes(current => ({
        ...current,
        ...Object.fromEntries(nodeIds.map(nodeId => [nodeId, detail.expanded ? "expanded" : "compact"])),
      }));
    };
    const onNodeInspectDetail = (detail: MicroflowInlineNodeInspectDetail) => {
      const nodeIds = resolveInlineNodeIds(detail);
      const primaryNodeId = nodeIds[0];
      if (!primaryNodeId) {
        return;
      }
      setNodeViewModes(current => ({
        ...current,
        ...Object.fromEntries(nodeIds.map(nodeId => [nodeId, detail.inspect === "error" ? "inspectingError" : "inspectingRuntime"])),
      }));
      applyPatch({ selectedObjectId: primaryNodeId, selectedFlowId: undefined }, { pushHistory: false, skipDirty: true, skipValidate: true, source: "flowgram" });
      openPropertiesPanel();
    };
    const onNodeToggle = (event: Event) => {
      onNodeToggleDetail((event as CustomEvent<MicroflowInlineNodeToggleDetail>).detail);
    };
    const onNodeInspect = (event: Event) => {
      onNodeInspectDetail((event as CustomEvent<MicroflowInlineNodeInspectDetail>).detail);
    };
    const onFieldCommit = (event: Event) => {
      if (props.readonly) {
        return;
      }
      const detail = (event as CustomEvent<MicroflowInlineFieldCommitDetail>).detail;
      if (!detail?.nodeId || !detail.fieldPath) {
        return;
      }
      if (running && !isDebugPaused) {
        Toast.warning("运行中仅支持在暂停点修改。请先 Pause/断点暂停后再提交。");
        return;
      }
      if (isDesignSchema(schema)) {
        const nextSchema = updateDesignNodeInlineField(schema, detail);
        if (!nextSchema) {
          Toast.warning("当前字段暂不支持内联提交。");
          return;
        }
        commitSchema(nextSchema as unknown as MicroflowSchema, "updateNodeProperty", { source: "propertyPanel" });
        emitPanelSyncEvent({ type: "inline-edit", nodeId: detail.nodeId, fieldPath: detail.fieldPath });
        applyPatch({ selectedObjectId: detail.nodeId, selectedFlowId: undefined }, { pushHistory: false, skipDirty: true, skipValidate: true, source: "flowgram" });
        openPropertiesPanel();
        return;
      }
      const located = findObjectWithCollection(schema, detail.nodeId);
      if (!located) {
        return;
      }
      const object = structuredClone(located.object) as MicroflowObject;
      const value = normalizeInlineCommitValue(detail);
      const patched = setValueAtPath(object as unknown as Record<string, unknown>, detail.fieldPath, value)
        || (detail.fieldPath.startsWith("data.") && setValueAtPath(object as unknown as Record<string, unknown>, detail.fieldPath.slice(5), value));
      if (!patched) {
        Toast.warning("当前字段暂不支持内联提交。");
        return;
      }
      const reason = object.kind === "actionActivity" ? "updateActionProperty" : "updateNodeProperty";
      commitSchema(updateObject(schema, detail.nodeId, () => object), reason, { source: "propertyPanel" });
      emitPanelSyncEvent({ type: "inline-edit", nodeId: detail.nodeId, fieldPath: detail.fieldPath });
      applyPatch({ selectedObjectId: detail.nodeId, selectedFlowId: undefined }, { pushHistory: false, skipDirty: true, skipValidate: true, source: "flowgram" });
      openPropertiesPanel();
    };
    const onLineLabelCommit = (event: Event) => {
      if (props.readonly) {
        return;
      }
      const detail = (event as CustomEvent<MicroflowInlineLineLabelCommitDetail>).detail;
      const flowId = detail?.flowId ?? detail?.edgeId;
      if (!flowId) {
        return;
      }
      if (running && !isDebugPaused) {
        Toast.warning("运行中仅支持在暂停点修改。请先 Pause/断点暂停后再提交。");
        return;
      }
      const located = findFlowWithCollection(schema, flowId);
      if (!located) {
        return;
      }
      commitSchema(updateFlow(schema, flowId, flow => (
        flow.kind === "sequence"
          ? { ...flow, label: detail.value ?? "" }
          : { ...flow, line: { ...flow.line, content: detail.value ?? "" } }
      )), "updateEdgeProperty", { source: "propertyPanel" });
      emitPanelSyncEvent({ type: "inline-edit", flowId });
      applyPatch({ selectedObjectId: undefined, selectedFlowId: flowId }, { pushHistory: false, skipDirty: true, skipValidate: true, source: "flowgram" });
      openPropertiesPanel();
    };
    const onQuickFix = (event: Event) => {
      const detail = (event as CustomEvent<MicroflowInlineQuickFixDetail>).detail;
      if (!detail?.nodeId || detail.actionKind !== "setFieldValue" || !detail.fieldPath) {
        return;
      }
      onFieldCommit(new CustomEvent<MicroflowInlineFieldCommitDetail>("inline", {
        detail: {
          nodeId: detail.nodeId,
          fieldPath: detail.fieldPath,
          value: String(detail.value ?? ""),
          editType: String(detail.editType ?? "text"),
        },
      }));
    };
    const unsubscribeNodeToggle = subscribeInlineNodeToggle(onNodeToggleDetail);
    const unsubscribeNodeInspect = subscribeInlineNodeInspect(onNodeInspectDetail);
    window.addEventListener(MICROFLOW_INLINE_NODE_TOGGLE_EVENT, onNodeToggle as EventListener);
    window.addEventListener(MICROFLOW_INLINE_NODE_INSPECT_EVENT, onNodeInspect as EventListener);
    window.addEventListener(MICROFLOW_INLINE_FIELD_COMMIT_EVENT, onFieldCommit as EventListener);
    window.addEventListener(MICROFLOW_INLINE_LINE_LABEL_COMMIT_EVENT, onLineLabelCommit as EventListener);
    window.addEventListener(MICROFLOW_INLINE_QUICK_FIX_EVENT, onQuickFix as EventListener);
    return () => {
      window.removeEventListener(MICROFLOW_INLINE_NODE_TOGGLE_EVENT, onNodeToggle as EventListener);
      window.removeEventListener(MICROFLOW_INLINE_NODE_INSPECT_EVENT, onNodeInspect as EventListener);
      unsubscribeNodeToggle();
      unsubscribeNodeInspect();
      window.removeEventListener(MICROFLOW_INLINE_FIELD_COMMIT_EVENT, onFieldCommit as EventListener);
      window.removeEventListener(MICROFLOW_INLINE_LINE_LABEL_COMMIT_EVENT, onLineLabelCommit as EventListener);
      window.removeEventListener(MICROFLOW_INLINE_QUICK_FIX_EVENT, onQuickFix as EventListener);
    };
  }, [applyPatch, commitSchema, emitPanelSyncEvent, isDebugPaused, props.readonly, running, schema]);

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
    exportAsImage: handleExportAsImage,
    togglePanTool: () => {
      setCanvasPanToolActive(value => !value);
    },
    toggleToolbox: () => {
      toggleNodePanel();
    },
    configureAllNodeAcceptance120: handleConfigureAllNodeAcceptance120,
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
  }), [commitSchema, dirty, focusMode, fullscreenActive, handleConfigureAllNodeAcceptance120, handleExportAsImage, handleSave, handleUndo, handleRedo, handleValidate, handleTestRun, handleAutoLayout, historyState.canRedo, historyState.canUndo, issues, labels.debug, layoutState, openBottomDock, props.onPublish, props.readonly, resetWorkbenchLayout, runSession, running, saving, schema, startDebugSession, toggleNodePanel, validationStatus, workbenchStatus]);

  const nodePaletteActions = useMemo<CommandPaletteAction[]>(() => (
    graph.nodes
      .slice(0, 30)
      .map(node => ({
        id: `node:${node.objectId}`,
        label: `Go to node: ${node.title || node.objectId}`,
        run: () => {
          openPropertiesPanel();
          applyPatch(
            {
              selectedObjectId: node.objectId,
              selectedFlowId: undefined,
              selectedCollectionId: findObjectWithCollection(schema, node.objectId)?.collectionId,
              viewport: viewportCenteredOn(node.position),
            },
            { pushHistory: false, skipDirty: true, skipValidate: true, source: "runtime" },
          );
        },
      }))
  ), [applyPatch, graph.nodes, schema]);

  const problemPaletteActions = useMemo<CommandPaletteAction[]>(() => (
    [...issues]
      .sort((a, b) => {
        const rank = (issue: MicroflowValidationIssue) => issue.severity === "error" ? 0 : issue.severity === "warning" ? 1 : 2;
        return rank(a) - rank(b);
      })
      .slice(0, 20)
      .map(issue => ({
        id: `problem:${issue.id}`,
        label: `Go to problem: [${issue.severity}] ${issue.code}`,
        run: () => focusProblemIssue(issue),
      }))
  ), [focusProblemIssue, issues]);

  const tracePaletteActions = useMemo<CommandPaletteAction[]>(() => (
    traceFrames
      .slice(0, 20)
      .map(frame => ({
        id: `trace:${frame.id}`,
        label: `Go to trace: ${frame.objectId ?? "unknown"} · ${frame.status}${typeof frame.durationMs === "number" ? ` · ${frame.durationMs}ms` : ""}`,
        run: () => selectTraceFrame(frame),
      }))
  ), [traceFrames]);

  const commandItems = useMemo<CommandPaletteAction[]>(() => [
    { id: "save", label: "Save", disabled: props.readonly || saving, disabledReason: props.readonly ? "Readonly mode cannot save." : saving ? "Save is in progress." : undefined, run: () => void handleSave() },
    { id: "validate", label: "Validate", run: () => void handleValidate() },
    { id: "run", label: "Run Test", disabled: running, disabledReason: running ? "A run is already in progress." : undefined, run: () => void handleTestRun() },
    { id: "debug", label: "Run Debug", disabled: running, disabledReason: running ? "A run is already in progress." : undefined, run: () => void startDebugSession() },
    {
      id: "inline-edit",
      label: "Enter Inline Edit",
      disabled: !(schema.editor.selection.objectId || schema.editor.selection.objectIds?.length),
      disabledReason: !(schema.editor.selection.objectId || schema.editor.selection.objectIds?.length) ? "Select a node first." : undefined,
      run: () => handleEnterInlineEdit(),
    },
    ...(AUXILIARY_PANELS_ENABLED ? [
      { id: "problems", label: "Open Problems", run: () => { setBottomDockMode("peek"); setBottomTab("problems"); } },
      { id: "properties", label: rightOpen ? "Hide Properties" : "Show Properties", run: () => togglePropertiesPanel() },
    ] : []),
    { id: "toolbox", label: leftOpen ? "Hide Toolbox" : "Show Toolbox", run: () => toggleNodePanel() },
    { id: "undo", label: "Undo", disabled: !historyState.canUndo, disabledReason: !historyState.canUndo ? "No history to undo." : undefined, run: handleUndo },
    { id: "redo", label: "Redo", disabled: !historyState.canRedo, disabledReason: !historyState.canRedo ? "No history to redo." : undefined, run: handleRedo },
    { id: "export-image", label: "Export PNG", run: () => void handleExportAsImage() },
    { id: "fit-view", label: "Fit View", run: () => window.dispatchEvent(new CustomEvent("atlas:microflow-flowgram-fit-view")) },
    { id: "zoom-100", label: "Zoom 100%", run: () => applyPatch({ viewport: { ...schema.editor.viewport, zoom: 1 } }, { pushHistory: false, skipDirty: true, skipValidate: true, preserveSelection: true, source: "flowgram" }) },
    { id: "auto-layout", label: "Auto Layout", disabled: props.readonly, disabledReason: props.readonly ? "Readonly mode cannot change layout." : undefined, run: handleAutoLayout },
    { id: "retry-queued-run", label: "Retry Queued Run", disabled: !apiClient.retryMicroflowRun || (!(selectedRunId ?? runSession?.id)), disabledReason: !apiClient.retryMicroflowRun ? "Current runtime adapter does not support queued retry." : !(selectedRunId ?? runSession?.id) ? "No selected run to retry." : undefined, run: () => void handleRetryQueuedRun() },
    { id: "retention-preview", label: "Retention Preview (30d)", disabled: !apiClient.runRetention, disabledReason: !apiClient.runRetention ? "Current runtime adapter does not support retention." : undefined, run: () => void handleRetentionPreview() },
    { id: "retention-dry-run", label: "Retention Dry Run (30d)", disabled: !apiClient.runRetention, disabledReason: !apiClient.runRetention ? "Current runtime adapter does not support retention." : undefined, run: () => void handleRetentionDryRun() },
    { id: "retention-execute", label: "Retention Execute (30d)", disabled: !apiClient.runRetention, disabledReason: !apiClient.runRetention ? "Current runtime adapter does not support retention." : undefined, run: () => void handleRetentionExecute() },
    { id: "focus-mode", label: focusMode ? "Exit Focus Mode" : "Enter Focus Mode", run: () => setFocusMode(value => !value) },
    ...problemPaletteActions,
    ...tracePaletteActions,
    ...nodePaletteActions,
  ], [apiClient.retryMicroflowRun, apiClient.runRetention, dirty, focusMode, handleEnterInlineEdit, handleExportAsImage, handleRetentionDryRun, handleRetentionExecute, handleRetentionPreview, handleRetryQueuedRun, handleSave, historyState.canRedo, historyState.canUndo, leftOpen, nodePaletteActions, problemPaletteActions, props.readonly, rightOpen, runSession?.id, running, saving, schema.editor.selection.objectId, schema.editor.selection.objectIds, schema.editor.viewport, selectedRunId, startDebugSession, toggleNodePanel, togglePropertiesPanel, tracePaletteActions]);

  const canCopySelection = Boolean(schema.editor.selection.objectId || schema.editor.selection.objectIds?.length);
  const canPasteSelection = Boolean(clipboardObject);
  const canDeleteSelection = Boolean(
    schema.editor.selection.objectId
      || schema.editor.selection.flowId
      || schema.editor.selection.objectIds?.length
      || schema.editor.selection.flowIds?.length
  );
  const runDisabledReason = saving
    ? "Save is in progress."
    : props.readonly
      ? "Readonly mode cannot run."
      : !schema.id
        ? "Schema is not ready."
        : "";
  const saveDisabledReason = saving
    ? "Save is in progress."
    : props.readonly
      ? "Readonly mode cannot save."
      : !dirty
        ? "No unsaved changes."
        : !schema.id
          ? "Schema is not ready."
          : "";
  const debugDisabledReason = running ? "A run is already in progress." : "";
  const copyDisabledReason = props.readonly ? "Readonly mode cannot copy nodes." : canCopySelection ? "" : "Select at least one node first.";
  const pasteDisabledReason = props.readonly ? "Readonly mode cannot paste nodes." : canPasteSelection ? "" : "Clipboard is empty.";
  const deleteDisabledReason = props.readonly ? "Readonly mode cannot delete selection." : canDeleteSelection ? "" : "Select node/flow first.";

  return (
    <div
      ref={shellRef}
      data-testid="microflow-editor-shell"
      data-microflow-id={schema.id}
      data-usage-selected-object-id={usageHighlights?.selectedObjectId ?? ""}
      data-usage-selected-variable={selectedUsageVariableName ?? ""}
      data-usage-source-ids={(usageHighlights?.sourceNodeIds ?? []).join(",")}
      data-usage-consumer-ids={(usageHighlights?.consumerNodeIds ?? []).join(",")}
      style={shellStyle}
      tabIndex={0}
    >
      <MicroflowCommandPalette
        visible={commandPaletteOpen}
        query={commandPaletteQuery}
        commands={commandItems}
        onQueryChange={setCommandPaletteQuery}
        onClose={() => setCommandPaletteOpen(false)}
      />
      <style>{`@keyframes microflow-ws-blink { 50% { opacity: 0.45; } }`}</style>
      {toolbarMode === "internal" ? (
      <div data-testid="microflow-editor-toolbar" style={toolbarStyle}>
        <Space style={{ minWidth: 0, overflow: "hidden" }}>
          {props.toolbarPrefix}
          <Title heading={5} style={{ margin: 0 }}>{schema.displayName || schema.name}</Title>
          {!focusMode ? <Tag>{schema.schemaVersion}</Tag> : null}
          {!focusMode && dirty ? <Tag color="orange">dirty</Tag> : null}
          <Tag color={validationStatus === "validating" ? "blue" : issues.some(issue => issue.severity === "error") ? "red" : "green"}>
            {validationStatus === "validating" ? "validating..." : `${issues.length} issues`}
          </Tag>
          {!focusMode && saveBlockers.length > 0 ? <Tag color="red">Save blocked {saveBlockers.length}</Tag> : null}
          {!focusMode && publishBlockers.length > 0 ? <Tag color="red">Publish blocked by {publishBlockers.length} errors</Tag> : null}
          {!focusMode && inlineEditState === "blocked" ? <Tag color="orange">Inline edit: running-readonly (editable on pause)</Tag> : null}
          {!focusMode && inlineEditState === "editing" ? <Tag color="green">Inline edit: editing</Tag> : null}
          {!focusMode && inlineEditState === "paused-edit" ? <Tag color="blue">Inline edit: paused-edit</Tag> : null}
          {!focusMode && runSession ? <Tag color={runSession.status === "success" ? "green" : "red"}>{runSession.status} · {runSession.trace.length} frames</Tag> : null}
          {!focusMode ? (
            <span
              data-testid="microflow-debug-ws-status"
              style={{
                display: "inline-flex",
                alignItems: "center",
                gap: 6,
                padding: "2px 8px",
                borderRadius: 12,
                border: "1px solid var(--semi-color-border, #e5e6eb)",
                animation: debugConnectionStatus === "reconnecting" ? "microflow-ws-blink 1s step-end infinite" : undefined,
              }}
            >
              <span
                style={{
                  width: 8,
                  height: 8,
                  borderRadius: "50%",
                  background: debugWsStatusTag.color === "green"
                    ? "#4ade80"
                    : debugWsStatusTag.color === "blue"
                      ? "#fcd34d"
                      : debugWsStatusTag.color === "red"
                        ? "#ef4444"
                        : debugWsStatusTag.color === "orange"
                          ? "#f59e0b"
                          : "#6b7280",
                }}
              />
              <span>{debugWsStatusTag.text}</span>
              <span style={{ color: getDebugLatencyColor(debugLatencyMs) }}>
                {debugLatencyMs}ms
              </span>
            </span>
          ) : null}
        </Space>
        <Space wrap style={{ justifyContent: "flex-end", rowGap: 4 }}>
          {focusMode ? (
            <>
              <Tooltip content={runDisabledReason || (dirty ? "Save & Run opens the input panel" : labels.testRun)}>
                <span style={{ display: "inline-flex" }}>
                  <Button
                    data-testid="microflow-editor-run"
                    aria-label={labels.testRun}
                    icon={<IconPlay />}
                    loading={running}
                    disabled={saving || props.readonly || !schema.id}
                    onClick={handleTestRun}
                  >
                    {dirty ? "Save & Run" : labels.testRun}
                  </Button>
                </span>
              </Tooltip>
              <Tooltip content={saveDisabledReason || labels.save}>
                <span style={{ display: "inline-flex" }}>
                  <Button
                    data-testid="microflow-editor-save"
                    aria-label={labels.save}
                    icon={<IconSave />}
                    loading={saving}
                    disabled={saving || props.readonly || !dirty || !schema.id}
                    type="primary"
                    onClick={handleSave}
                  >
                    {labels.save}
                  </Button>
                </span>
              </Tooltip>
              <Tooltip content="Export current canvas as PNG">
                <span style={{ display: "inline-flex" }}>
                  <Button
                    data-testid="microflow-editor-export-image"
                    aria-label="Export PNG"
                    icon={<IconDownloadStroked />}
                    disabled={!schema.id}
                    onClick={() => void handleExportAsImage()}
                  >
                    Export PNG
                  </Button>
                </span>
              </Tooltip>
              <Tooltip content={debugDisabledReason || "Start debug run"}>
                <span style={{ display: "inline-flex" }}>
                  <Button
                    data-testid="microflow-editor-debug"
                    aria-label="Debug"
                    icon={<IconTickCircle />}
                    disabled={running}
                    onClick={() => void startDebugSession()}
                  >
                    Debug
                  </Button>
                </span>
              </Tooltip>
            </>
          ) : (
            <>
          <Tooltip content={historyState.canUndo ? labels.undo : "No history to undo"}>
            <span style={{ display: "inline-flex" }}>
              <Button data-testid="microflow-editor-undo" aria-label={labels.undo} icon={<IconUndo />} disabled={!historyState.canUndo} onClick={handleUndo} />
            </span>
          </Tooltip>
          <Tooltip content={historyState.canRedo ? labels.redo : "No history to redo"}>
            <span style={{ display: "inline-flex" }}>
              <Button data-testid="microflow-editor-redo" aria-label={labels.redo} icon={<IconRedo />} disabled={!historyState.canRedo} onClick={handleRedo} />
            </span>
          </Tooltip>
          <Tooltip content={labels.validate}>
            <Button data-testid="microflow-editor-validate" aria-label={labels.validate} icon={<IconRefresh />} loading={validationStatus === "validating"} onClick={handleValidate}>{labels.validate}</Button>
          </Tooltip>
          <Tooltip content={runDisabledReason || (dirty ? "Save & Run opens the input panel" : labels.testRun)}>
            <span style={{ display: "inline-flex" }}>
              <Button data-testid="microflow-editor-run" aria-label={labels.testRun} icon={<IconPlay />} loading={running} disabled={saving || props.readonly || !schema.id} onClick={handleTestRun}>
                {dirty ? "Save & Run" : labels.testRun}
              </Button>
            </span>
          </Tooltip>
          <Tooltip content={saveDisabledReason || labels.save}>
            <span style={{ display: "inline-flex" }}>
              <Button data-testid="microflow-editor-save" aria-label={labels.save} icon={<IconSave />} loading={saving} disabled={saving || props.readonly || !dirty || !schema.id} type="primary" onClick={handleSave}>{labels.save}</Button>
            </span>
          </Tooltip>
          <Tooltip content="Export current canvas as PNG">
            <span style={{ display: "inline-flex" }}>
              <Button data-testid="microflow-editor-export-image" aria-label="Export PNG" icon={<IconDownloadStroked />} disabled={!schema.id} onClick={() => void handleExportAsImage()}>Export PNG</Button>
            </span>
          </Tooltip>
          <Dropdown
            trigger="click"
            position="bottomRight"
            render={(
              <Dropdown.Menu>
                <Tooltip content={copyDisabledReason}>
                  <Dropdown.Item icon={<IconCopy />} disabled={props.readonly || !canCopySelection} onClick={handleCopySelection}>复制节点</Dropdown.Item>
                </Tooltip>
                <Tooltip content={pasteDisabledReason}>
                  <Dropdown.Item icon={<IconCopy />} disabled={props.readonly || !canPasteSelection} onClick={handlePasteSelection}>粘贴节点</Dropdown.Item>
                </Tooltip>
                <Tooltip content={deleteDisabledReason}>
                  <Dropdown.Item icon={<IconDelete />} disabled={props.readonly || !canDeleteSelection} onClick={handleDeleteSelection}>删除选择</Dropdown.Item>
                </Tooltip>
                <Dropdown.Item icon={<IconRefresh />} onClick={handleAutoLayout}>{labels.format}</Dropdown.Item>
                <Dropdown.Item icon={<IconDownloadStroked />} onClick={() => void handleExportAsImage()}>Export PNG</Dropdown.Item>
                <Dropdown.Item onClick={focusNodeSearch}>搜索节点</Dropdown.Item>
                <Dropdown.Item onClick={toggleNodePanel}>{leftOpen ? "折叠节点面板" : "展开节点面板"}</Dropdown.Item>
                {AUXILIARY_PANELS_ENABLED ? (
                  <>
                    <Dropdown.Item onClick={togglePropertiesPanel}>{rightOpen ? "折叠属性面板" : "展开属性面板"}</Dropdown.Item>
                    <Dropdown.Item onClick={toggleBottomDock}>{bottomOpen ? "折叠底部 Dock" : "展开底部 Dock"}</Dropdown.Item>
                  </>
                ) : null}
                {runSession ? <Dropdown.Item type="danger" icon={<IconDelete />} onClick={clearTestRun}>清空调试</Dropdown.Item> : null}
              </Dropdown.Menu>
            )}
          >
            <Button data-testid="microflow-editor-more" aria-label={labels.more} icon={<IconMore />} theme="borderless">{labels.more}</Button>
          </Dropdown>
            </>
          )}
          {props.toolbarSuffix}
        </Space>
      </div>
      ) : null}
      <div style={bodyStyle}>
        <div data-testid="microflow-canvas" style={{ minWidth: 0, minHeight: 0, display: "contents" }}>
        <FlowGramMicroflowNativeCanvas
          schema={schema}
          validationIssues={issues}
          runtimeTrace={traceFrames}
          nodeViewModes={nodeViewModes}
          usageHighlights={usageHighlights}
          focusObjectId={focusObjectId}
          focusRequestKey={focusRequestSeq}
          readonly={props.readonly || runtimeInlineReadonly}
          onSchemaChange={(nextSchema, reason) => {
            commitSchema(nextSchema, reason, { source: "flowgram", skipValidate: true });
          }}
          onSelectionChange={selection => {
            setCanvasNodeContextMenu(undefined);
            if (isDesignSchema(schema)) {
              commitSchema({
                ...schema,
                editor: {
                  ...schema.editor,
                  selection: {
                    ...schema.editor.selection,
                    objectId: selection.objectId,
                    flowId: selection.flowId,
                    collectionId: selection.collectionId,
                    objectIds: selection.objectIds ?? [],
                    flowIds: selection.flowIds ?? [],
                    mode: selection.mode,
                  },
                },
              }, "bulkUpdate", { pushHistory: false, skipDirty: true, skipValidate: true, source: "flowgram" });
              return;
            }
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
            if (isDesignSchema(schema)) {
              commitSchema({
                ...schema,
                editor: {
                  ...schema.editor,
                  selection: {
                    ...schema.editor.selection,
                    objectId: undefined,
                    flowId: undefined,
                    collectionId: undefined,
                    objectIds: [],
                    flowIds: [],
                    mode: "none",
                  },
                },
              }, "bulkUpdate", { pushHistory: false, skipDirty: true, skipValidate: true, source: "flowgram" });
              return;
            }
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
            setCanvasNodeContextMenu(selection.objectId || selection.flowId ? {
              objectId: selection.objectId,
              flowId: selection.flowId,
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
          onOpenProblemsPanel={AUXILIARY_PANELS_ENABLED ? () => {
            openBottomDock("problems");
          } : undefined}
          canvasPanToolActive={canvasPanToolActive}
          onCanvasPanToolChange={setCanvasPanToolActive}
          showBuiltInToolbar={toolbarMode === "internal"}
        />
        </div>
        {shouldShowCanvasContextMenu && canvasNodeContextMenu ? (() => {
          const hasNode = Boolean(canvasNodeContextMenu.objectId);
          const nodeBreakpoint = hasNode
            ? activeDebugSession?.breakpoints?.find(item =>
              item.enabled !== false
              && item.microflowObjectId === canvasNodeContextMenu.objectId
              && normalizeDebugBreakpointScope(item.scope) === "node")
            : undefined;
          const nodeDisabled = hasNode
            ? isDesignSchema(schema)
              ? Boolean(schema.workflow.nodes.find(node => node.id === canvasNodeContextMenu.objectId)?.data?.disabled)
              : Boolean(findObject(schema, canvasNodeContextMenu.objectId!)?.disabled)
            : false;
          return (
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
              {hasNode ? (
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
                    openPropertiesPanel();
                    setCanvasNodeContextMenu(undefined);
                  }}
                >
                  {labels.contextProperties}
                </Button>
              ) : null}
              {hasNode ? (
                <Button
                  block
                  size="small"
                  theme="borderless"
                  type="tertiary"
                  icon={<IconCopy />}
                  style={{ justifyContent: "flex-start" }}
                  onClick={handleCanvasContextRename}
                >
                  {labels.contextRename}
                </Button>
              ) : null}
              {hasNode ? (
                <Button
                  block
                  size="small"
                  theme="borderless"
                  type="tertiary"
                  icon={<IconCopy />}
                  style={{ justifyContent: "flex-start" }}
                  onClick={handleCanvasContextDuplicate}
                >
                  {labels.contextDuplicate}
                </Button>
              ) : (
                <Button
                  block
                  size="small"
                  theme="borderless"
                  type="tertiary"
                  icon={<IconCopy />}
                  style={{ justifyContent: "flex-start" }}
                  disabled={props.readonly}
                  onClick={handleCanvasContextDuplicate}
                >
                  {labels.contextDuplicate}
                </Button>
              )}
              {hasNode ? (
                <Button
                  block
                  size="small"
                  theme="borderless"
                  type="tertiary"
                  style={{ justifyContent: "flex-start" }}
                  disabled={props.readonly}
                  onClick={() => { void handleCanvasContextToggleBreakpoint(); }}
                >
                  {nodeBreakpoint ? labels.contextRemoveBreakpoint : labels.contextAddBreakpoint}
                </Button>
              ) : null}
              {hasNode ? (
                <Button
                  block
                  size="small"
                  theme="borderless"
                  type="tertiary"
                  style={{ justifyContent: "flex-start" }}
                  disabled={props.readonly}
                  onClick={handleCanvasContextToggleDisabled}
                >
                  {nodeDisabled ? labels.contextEnable : labels.contextDisable}
                </Button>
              ) : null}
              <div style={{ height: 1, margin: "4px 0", background: "var(--semi-color-border, #e5e6eb)" }} />
              <Button
                block
                size="small"
                theme="borderless"
                type="tertiary"
                style={{ justifyContent: "flex-start" }}
                onClick={handleCanvasContextCenter}
              >
                {labels.contextCenterView}
              </Button>
              <Button
                block
                size="small"
                theme="borderless"
                type="tertiary"
                style={{ justifyContent: "flex-start" }}
                onClick={handleCanvasContextCopyId}
              >
                {labels.contextCopyId}
              </Button>
              <Button
                block
                size="small"
                theme="borderless"
                type="danger"
                style={{ justifyContent: "flex-start", color: "var(--semi-color-danger, #f53f3f)" }}
                disabled={props.readonly}
                icon={<IconDelete />}
                onClick={handleCanvasContextDelete}
              >
                {labels.contextDelete}
              </Button>
            </div>
          );
        })() : null}
        {AUXILIARY_PANELS_ENABLED && !focusMode ? <div
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
                width: (leftOpen || rightOpen) ? RIGHT_PANEL_EXPANDED_PX + RAIL_WIDTH_PX : RAIL_WIDTH_PX,
                zIndex: 22,
                boxShadow: (leftOpen || rightOpen) ? "0 12px 32px rgba(31, 35, 41, 0.14)" : undefined
              } satisfies CSSProperties
              : {})
          }}
        >
          {leftOpen ? (
            <div data-testid="microflow-editor-right-node-panel" style={propertyPaneStyle}>
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
                  <IconMore />
                  <Text strong>{labels.nodePanel}</Text>
                </Space>
                <Button
                  aria-label="关闭节点面板"
                  size="small"
                  theme="borderless"
                  icon={<IconClose />}
                  onClick={closeNodePanel}
                />
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
          ) : null}
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
                    onClick={closePropertiesPanel}
                  />
                </Space>
              </div>
              {isDesignSchema(schema) ? (
                <MicroflowPropertyPanel
                  schemaProtocol="design"
                  schema={schema}
                  validationIssues={issues}
                  traceFrames={traceFrames}
                  highlightedVariableName={selectedUsageVariableName}
                  onSchemaChange={(nextSchema, reason) => {
                    commitSchema(nextSchema, reason, { source: "propertyPanel" });
                    emitPanelSyncEvent({ type: "property-edit" });
                  }}
                  onClose={() => {
                    applyPatch({ selectedObjectId: undefined, selectedFlowId: undefined }, { pushHistory: false, skipDirty: true, skipValidate: true });
                    if (!rightPinned) {
                      closePropertiesPanel();
                    }
                  }}
                  onHighlightVariableUsage={handleHighlightVariableUsage}
                />
              ) : (
                <MicroflowPropertyPanel
                  selectedObject={selectedObject}
                  selectedFlow={selectedFlow}
                  schema={schema}
                  validationIssues={issues}
                  traceFrames={traceFrames}
                  highlightedVariableName={selectedUsageVariableName}
                  onSchemaChange={(nextSchema, reason) => {
                    commitSchema(nextSchema, reason, { source: "propertyPanel" });
                    emitPanelSyncEvent({ type: "property-edit" });
                  }}
                  onObjectChange={(objectId, patch: MicroflowNodePatch) => {
                    if (!patch.object) {
                      return;
                    }
                    const object = patch.object as MicroflowObject;
                    const reason = object.kind === "actionActivity" ? "updateActionProperty" : "updateNodeProperty";
                    commitSchema(updateObject(schema, objectId, () => object), reason, { source: "propertyPanel" });
                    emitPanelSyncEvent({ type: "property-edit", nodeId: objectId });
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
                    emitPanelSyncEvent({ type: "property-edit", flowId });
                  }}
                  onDuplicateObject={objectId => {
                    const located = findObjectWithCollection(schema, objectId);
                    commitSchema(duplicateObject(schema, objectId), located?.parentLoopObjectId ? "addLoopNode" : "addNode", { source: "propertyPanel" });
                  }}
                  onDeleteObject={objectId => {
                    confirmDeleteTargets([objectId], [], "propertyPanel");
                  }}
                  onDeleteFlow={flowId => {
                    confirmDeleteTargets([], [flowId], "propertyPanel");
                  }}
                  onClose={() => {
                    applyPatch({ selectedObjectId: undefined, selectedFlowId: undefined }, { pushHistory: false, skipDirty: true, skipValidate: true });
                    if (!rightPinned) {
                      closePropertiesPanel();
                    }
                  }}
                  onHighlightVariableUsage={handleHighlightVariableUsage}
                />
              )}
            </div>
          ) : null}
          <div style={rightRailStyle}>
            <button
              type="button"
              data-testid="microflow-node-panel-rail"
              aria-label={leftOpen ? "折叠节点面板" : "展开节点面板"}
              title={labels.nodePanel}
              style={{
                border: 0,
                background: leftOpen ? "rgba(22, 93, 255, 0.08)" : "transparent",
                width: "100%",
                display: "flex",
                flexDirection: "column",
                alignItems: "center",
                gap: 6,
                padding: "10px 0",
                cursor: "pointer",
                color: leftOpen ? "var(--semi-color-primary, #165dff)" : "inherit"
              }}
              onClick={toggleNodePanel}
            >
              <IconMore style={{ fontSize: 18 }} />
              <Text size="small" strong style={{ writingMode: "vertical-rl", textOrientation: "mixed", letterSpacing: 1 }}>{labels.nodePanel}</Text>
            </button>
            <button
              type="button"
              aria-label={rightOpen ? "折叠属性面板" : "展开属性面板"}
              title={labels.properties}
              style={{
                border: 0,
                background: rightOpen ? "rgba(22, 93, 255, 0.08)" : "transparent",
                width: "100%",
                display: "flex",
                flexDirection: "column",
                alignItems: "center",
                gap: 6,
                padding: "10px 0",
                cursor: "pointer",
                color: rightOpen ? "var(--semi-color-primary, #165dff)" : "inherit"
              }}
              onClick={togglePropertiesPanel}
              onKeyDown={event => {
                if (event.key === "Enter" || event.key === " ") {
                  event.preventDefault();
                  togglePropertiesPanel();
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
            </button>
          </div>
        </div> : null}
      </div>
      {AUXILIARY_PANELS_ENABLED && !focusMode && bottomOpen ? (
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
                  onRetry={() => void runValidationNow(stripTransientSchemaState(schema))}
                  onApplyQuickFix={handleApplyProblemQuickFix}
                  quickFixLabel={labels.quickFix}
                  onSelect={focusProblemIssue}
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
                      debugAvailable={Boolean(schema.id)}
                      debugSession={activeDebugSession}
                      debugVariables={activeDebugVariables}
                      debugTimeline={activeDebugTimeline}
                      debugSuspendPolicy={activeDebugSuspendPolicy}
                      loopIteration={debugStoreSnapshot.loopIteration}
                      activeError={debugStoreSnapshot.activeError}
                      activeErrorStack={debugStoreSnapshot.activeErrorStack}
                      runtimeNodeState={debugStoreSnapshot.nodeState}
                      runtimeCallStack={debugStoreSnapshot.callStack}
                      debugWatches={activeDebugWatches}
                      activeUsageVariableName={selectedUsageVariableName}
                      activeFrameId={activeTraceFrameId}
                      onSelectFrame={selectTraceFrame}
                      onSelectFlow={selectTraceFlow}
                      onSelectError={selectTraceError}
                      onClear={clearTestRun}
                      onRerun={handleTestRun}
                      onCancelRun={cancelTestRun}
                      onRetryQueuedRun={handleRetryQueuedRun}
                      onDebugCommand={command => void handleDebugCommand(command)}
                      onDebugEvaluate={expression => void handleDebugEvaluate(expression)}
                      onDebugSuspendPolicyChange={policy => void handleDebugSuspendPolicyChange(policy)}
                      onHighlightVariableUsage={handleHighlightVariableUsage}
                      onDebugRefreshTimeline={() => {
                        if (activeDebugSession) {
                          void refreshDebugTimeline(activeDebugSession.id);
                        }
                      }}
                      onDebugMutateVariable={(name, value) => void handleDebugMutateVariable(name, value)}
                      onOpenMicroflow={props.onOpenMicroflow}
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
                {(selectedRunSession?.logs?.length || runtimeCommandEntries.length) ? (
                  <Space vertical align="start" spacing={6} style={{ width: "100%" }}>
                    {runtimeCommandEntries.map(entry => (
                      <Card key={entry.id} style={{ width: "100%" }}>
                        <Space vertical align="start" spacing={4}>
                          <Tag color={entry.handled ? "green" : "orange"}>{entry.commandKind}</Tag>
                          <Text>{entry.message}</Text>
                          <Text type="tertiary" size="small">
                            {entry.timestamp}
                            {entry.target ? ` · ${entry.target}` : ""}
                            {entry.sourceObjectId ? ` · object=${entry.sourceObjectId}` : ""}
                          </Text>
                        </Space>
                      </Card>
                    ))}
                    {(selectedRunSession?.logs ?? []).map(log => (
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
                  <Empty title="No runtime logs" description="执行产生的日志和 runtime command 消费记录会在这里展示。" />
                )}
              </div>
            </Tabs.TabPane>
          </Tabs>
        </div>
      ) : null}
      {AUXILIARY_PANELS_ENABLED && !focusMode ? (
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
        samples={testRunSamplesByMicroflowId[schema.id] ?? []}
        onCancel={() => setTestRunModalOpen(false)}
        onValuesChange={values => setRunInputsByMicroflowId(current => ({ ...current, [schema.id]: values }))}
        onSaveSample={handleSaveTestRunSample}
        onRunAllSamples={handleRunAllTestSamples}
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

