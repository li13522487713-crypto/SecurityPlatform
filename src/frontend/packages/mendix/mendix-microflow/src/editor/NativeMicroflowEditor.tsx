import { useCallback, useEffect, useImperativeHandle, useMemo, useRef, useState, type CSSProperties, type KeyboardEvent as ReactKeyboardEvent, type ReactNode, type Ref } from "react";

import { Badge, Button, Empty, Space, Tabs, Tag, Toast, Tooltip, Typography } from "@douyinfe/semi-ui";
import { IconClose, IconDelete, IconPlay, IconRefresh, IconSave, IconSetting, IconTickCircle, IconUndo, IconRedo } from "@douyinfe/semi-icons";

import { MicroflowNodePanel, type MicroflowNodePanelLabels } from "../node-panel";
import { MicroflowPropertyPanel } from "../property-panel";
import type { MicroflowApiClient, SaveMicroflowResponse, TestRunMicroflowResponse, ValidateMicroflowResponse } from "../runtime-adapter";
import type { MicroflowTraceFrame } from "../debug/trace-types";
import type { MicroflowValidationAdapterLike, MicroflowValidationMode } from "../performance";
import type { MicroflowMetadataAdapter, MicroflowMetadataCatalog } from "../metadata";
import type { FlowGramMicroflowEdgeData, FlowGramMicroflowNodeData, FlowGramMicroflowSelection } from "../flowgram/FlowGramMicroflowTypes";
import {
  FlowGramMicroflowNativeCanvas,
} from "../flowgram/FlowGramMicroflowNativeCanvas";
import {
  createWorkflowNodeFromPanelItem,
  workflowEdgeById,
  workflowEdgeCount,
  workflowNodeById,
  workflowNodeCount,
} from "../flowgram/flowgram-native-schema";
import type {
  MicroflowDesignSchema,
  MicroflowValidationIssue,
  MicroflowWorkflowEdgeJSON,
  MicroflowWorkflowJSON,
  MicroflowWorkflowNodeJSON,
} from "../schema/types";
import type {
  MicroflowEditorHandle,
  MicroflowEditorLabels,
  MicroflowEditorStatusSnapshot,
  MicroflowWorkbenchBottomTab,
  MicroflowWorkbenchLayoutState,
  MicroflowWorkbenchStatus,
} from "./index";

const { Text, Title } = Typography;

const RAIL_WIDTH_PX = 44;
const LEFT_PANEL_EXPANDED_PX = 300;
const RIGHT_PANEL_EXPANDED_PX = 360;
const BOTTOM_STRIP_HEIGHT_PX = 32;
const BOTTOM_DOCK_PEEK_HEIGHT_PX = 260;

type BottomDockMode = "collapsed" | "peek" | "full";
type NativeHistoryReason = "init" | "workflow" | "property" | "selection" | "layout" | "delete" | "runtime";
type NativeContextMenuState = {
  x: number;
  y: number;
  selection: FlowGramMicroflowSelection;
};

export interface NativeMicroflowEditorProps {
  schema: MicroflowDesignSchema;
  apiClient?: MicroflowApiClient;
  labels?: Partial<MicroflowEditorLabels>;
  toolbarPrefix?: ReactNode;
  toolbarSuffix?: ReactNode;
  nodePanelLabels?: Partial<MicroflowNodePanelLabels>;
  readonly?: boolean;
  onPublish?: (schema: MicroflowDesignSchema) => Promise<void> | void;
  onSaveComplete?: (response: SaveMicroflowResponse) => void;
  onValidateComplete?: (response: ValidateMicroflowResponse) => void;
  onValidationStateChange?: (state: { microflowId: string; issues: MicroflowValidationIssue[]; status: string; lastValidatedAt?: Date }) => void;
  onTestRunComplete?: (response: TestRunMicroflowResponse) => void;
  onSchemaChange?: (schema: MicroflowDesignSchema) => void;
  metadataAdapter?: MicroflowMetadataAdapter;
  metadataCatalog?: MicroflowMetadataCatalog;
  metadataWorkspaceId?: string;
  metadataModuleId?: string;
  validationAdapter?: MicroflowValidationAdapterLike;
  toolbarMode?: "internal" | "external";
  editorRef?: Ref<MicroflowEditorHandle>;
  shellMode?: "legacy-host-layout" | "editor-native-layout";
  onLayoutStateChange?: (state: MicroflowWorkbenchLayoutState) => void;
  onWorkbenchStatusChange?: (status: MicroflowWorkbenchStatus) => void;
}

const defaultLabels: Partial<MicroflowEditorLabels> = {
  save: "保存",
  validate: "校验",
  testRun: "运行",
  publish: "发布",
  undo: "撤销",
  redo: "重做",
  nodePanel: "Nodes",
  properties: "Properties",
  problems: "Problems",
  debug: "Debug",
  format: "Auto",
};

function cloneSchema(schema: MicroflowDesignSchema): MicroflowDesignSchema {
  return JSON.parse(JSON.stringify(schema)) as MicroflowDesignSchema;
}

function isEditableElement(target: EventTarget | null): boolean {
  return target instanceof HTMLElement && Boolean(target.closest("input, textarea, select, [contenteditable='true']"));
}

function schemaWorkflowSignature(schema: MicroflowDesignSchema): string {
  return JSON.stringify(schema.workflow);
}

function issue(id: string, message: string, code: string, severity: MicroflowValidationIssue["severity"] = "error", extra: Partial<MicroflowValidationIssue> = {}): MicroflowValidationIssue {
  return {
    id,
    severity,
    message,
    code,
    source: "schema",
    blockSave: severity === "error",
    blockPublish: severity === "error",
    createdAt: new Date().toISOString(),
    ...extra,
  };
}

function validateNativeSchema(schema: MicroflowDesignSchema, mode: MicroflowValidationMode = "edit"): MicroflowValidationIssue[] {
  const nodes = schema.workflow.nodes as MicroflowWorkflowNodeJSON[];
  const edges = schema.workflow.edges as MicroflowWorkflowEdgeJSON[];
  const issues: MicroflowValidationIssue[] = [];
  const starts = nodes.filter(node => ((node.data as Partial<FlowGramMicroflowNodeData> | undefined)?.objectKind ?? node.type) === "startEvent");
  const ends = nodes.filter(node => ((node.data as Partial<FlowGramMicroflowNodeData> | undefined)?.objectKind ?? node.type) === "endEvent");
  if (starts.length !== 1) {
    issues.push(issue(`${schema.id}:start-count`, "微流必须且只能有一个 Start 节点。", "MF_START_COUNT"));
  }
  if (ends.length < 1) {
    issues.push(issue(`${schema.id}:end-missing`, "微流至少需要一个 End 节点。", "MF_END_MISSING"));
  }
  const nodeIds = new Set(nodes.map(node => node.id));
  const edgeKeys = new Set<string>();
  for (const edge of edges) {
    const edgeKey = [edge.sourceNodeID, edge.sourcePortID ?? "", edge.targetNodeID, edge.targetPortID ?? ""].join("::");
    if (edgeKeys.has(edgeKey)) {
      issues.push(issue(`${schema.id}:duplicate-edge:${edgeKey}`, "不允许重复连线。", "MF_EDGE_DUPLICATED", "error", { flowId: edge.id }));
    }
    edgeKeys.add(edgeKey);
    if (!nodeIds.has(edge.sourceNodeID) || !nodeIds.has(edge.targetNodeID)) {
      issues.push(issue(`${schema.id}:edge-missing-node:${edge.id}`, "连线引用了不存在的节点。", "MF_EDGE_NODE_MISSING", "error", { flowId: edge.id }));
    }
    if (edge.sourceNodeID === edge.targetNodeID) {
      issues.push(issue(`${schema.id}:edge-self:${edge.id}`, "不允许节点自连。", "MF_EDGE_SELF_LOOP", "error", { flowId: edge.id }));
    }
  }
  if (mode !== "edit" && starts[0] && edges.every(edge => edge.sourceNodeID !== starts[0].id)) {
    issues.push(issue(`${schema.id}:start-no-outgoing`, "Start 节点需要至少一条出线。", "MF_START_NO_OUTGOING", "error", { objectId: starts[0].id }));
  }
  return issues;
}

function summarizeIssues(issues: MicroflowValidationIssue[]) {
  return {
    errorCount: issues.filter(item => item.severity === "error").length,
    warningCount: issues.filter(item => item.severity === "warning").length,
    infoCount: issues.filter(item => item.severity === "info").length,
  };
}

function selectionPatch(schema: MicroflowDesignSchema, selection: FlowGramMicroflowSelection): MicroflowDesignSchema {
  return {
    ...schema,
    editor: {
      ...schema.editor,
      selectedObjectId: selection.objectId,
      selectedFlowId: selection.flowId,
      selectedCollectionId: selection.collectionId,
      selection: {
        objectId: selection.objectId,
        flowId: selection.flowId,
        collectionId: selection.collectionId,
        objectIds: selection.objectIds,
        flowIds: selection.flowIds,
        mode: selection.mode,
      },
    },
  };
}

function isProtectedWorkflowNode(node: MicroflowWorkflowNodeJSON): boolean {
  const kind = (node.data as Partial<FlowGramMicroflowNodeData> | undefined)?.objectKind ?? node.type;
  return node.id === "start" || node.id === "end" || kind === "startEvent" || kind === "endEvent";
}

function selectionHasOnlyProtectedObjects(schema: MicroflowDesignSchema, selection: FlowGramMicroflowSelection): boolean {
  const selectedObjects = [selection.objectId, ...(selection.objectIds ?? [])].filter((id): id is string => Boolean(id));
  if (!selectedObjects.length) {
    return false;
  }
  return selectedObjects.every(objectId => {
    const node = workflowNodeById(schema.workflow, objectId) as MicroflowWorkflowNodeJSON | undefined;
    return node ? isProtectedWorkflowNode(node) : false;
  });
}

function deleteSelection(schema: MicroflowDesignSchema, targetSelection?: FlowGramMicroflowSelection): MicroflowDesignSchema {
  const selection = targetSelection ?? schema.editor.selection;
  const selectedObjects = new Set([...(selection.objectIds ?? []), selection.objectId].filter((id): id is string => Boolean(id)));
  const selectedFlows = new Set([...(selection.flowIds ?? []), selection.flowId].filter((id): id is string => Boolean(id)));
  const protectedObjects = new Set(
    (schema.workflow.nodes as MicroflowWorkflowNodeJSON[])
      .filter(node => selectedObjects.has(node.id) && isProtectedWorkflowNode(node))
      .map(node => node.id),
  );
  const deletedParameterIds = new Set(
    (schema.workflow.nodes as MicroflowWorkflowNodeJSON[])
      .filter(node => selectedObjects.has(node.id) && !protectedObjects.has(node.id))
      .flatMap(node => {
        const data = node.data as (Partial<FlowGramMicroflowNodeData> & {
          parameterId?: string;
        }) | undefined;
        const parameterId = data?.parameterId;
        return data?.objectKind === "parameterObject"
          ? [parameterId].filter((id): id is string => Boolean(id))
          : [];
      }),
  );
  return {
    ...schema,
    parameters: deletedParameterIds.size > 0
      ? schema.parameters.filter(parameter => !deletedParameterIds.has(parameter.id))
      : schema.parameters,
    workflow: {
      ...schema.workflow,
      nodes: (schema.workflow.nodes as MicroflowWorkflowNodeJSON[]).filter(node => !selectedObjects.has(node.id) || protectedObjects.has(node.id)),
      edges: (schema.workflow.edges as MicroflowWorkflowEdgeJSON[]).filter(edge => {
        const id = (edge.data as Partial<FlowGramMicroflowEdgeData> | undefined)?.flowId ?? edge.id;
        return !selectedFlows.has(String(id))
          && (!selectedObjects.has(edge.sourceNodeID) || protectedObjects.has(edge.sourceNodeID))
          && (!selectedObjects.has(edge.targetNodeID) || protectedObjects.has(edge.targetNodeID));
      }),
    },
    editor: {
      ...schema.editor,
      selectedObjectId: undefined,
      selectedFlowId: undefined,
      selection: {},
    },
  };
}

function applyAutoLayout(schema: MicroflowDesignSchema): MicroflowDesignSchema {
  const nodes = schema.workflow.nodes as MicroflowWorkflowNodeJSON[];
  return {
    ...schema,
    workflow: {
      ...schema.workflow,
      nodes: nodes.map((node, index) => ({
        ...node,
        meta: {
          ...node.meta,
          position: {
            x: 260 + index * 260,
            y: 220 + (index % 2) * 120,
          },
        },
      })),
    },
  };
}

function panelStyle(): CSSProperties {
  return {
    minWidth: 0,
    minHeight: 0,
    overflow: "auto",
    padding: 12,
    borderRight: "1px solid var(--semi-color-border, #e5e6eb)",
    background: "var(--semi-color-bg-2, #fff)",
  };
}

function propertyPaneStyle(): CSSProperties {
  return {
    width: RIGHT_PANEL_EXPANDED_PX,
    minWidth: 0,
    minHeight: 0,
    overflow: "auto",
    padding: 12,
    borderLeft: "1px solid var(--semi-color-border, #e5e6eb)",
    background: "var(--semi-color-bg-1, #fff)",
  };
}

function contextMenuPosition(point: { x: number; y: number }): { left: number; top: number } {
  const menuWidth = 168;
  const menuHeight = 128;
  const margin = 8;
  if (typeof window === "undefined") {
    return { left: point.x, top: point.y };
  }
  return {
    left: Math.max(margin, Math.min(point.x, window.innerWidth - menuWidth - margin)),
    top: Math.max(margin, Math.min(point.y, window.innerHeight - menuHeight - margin)),
  };
}

export function NativeMicroflowEditor(props: NativeMicroflowEditorProps) {
  const labels = { ...defaultLabels, ...props.labels };
  const [schema, setSchema] = useState<MicroflowDesignSchema>(() => cloneSchema(props.schema));
  const [issues, setIssues] = useState<MicroflowValidationIssue[]>(() => validateNativeSchema(props.schema));
  const [validationStatus, setValidationStatus] = useState("idle");
  const [lastValidatedAt, setLastValidatedAt] = useState<Date>();
  const [dirty, setDirty] = useState(false);
  const [saving, setSaving] = useState(false);
  const [running, setRunning] = useState(false);
  const [leftOpen, setLeftOpen] = useState(true);
  const [rightOpen, setRightOpen] = useState(true);
  const [bottomDockMode, setBottomDockMode] = useState<BottomDockMode>("collapsed");
  const [bottomTab, setBottomTab] = useState<MicroflowWorkbenchBottomTab>("problems");
  const [focusObjectId, setFocusObjectId] = useState<string>();
  const [focusRequestSeq, setFocusRequestSeq] = useState(0);
  const [contextMenu, setContextMenu] = useState<NativeContextMenuState>();
  const [historyPast, setHistoryPast] = useState<MicroflowDesignSchema[]>([]);
  const [historyFuture, setHistoryFuture] = useState<MicroflowDesignSchema[]>([]);
  const shellRef = useRef<HTMLDivElement>(null);
  const contextMenuRef = useRef<HTMLDivElement>(null);
  const latestSchemaRef = useRef(schema);
  const savedSchemaSignatureRef = useRef(schemaWorkflowSignature(schema));
  const toolbarMode = props.toolbarMode ?? "internal";
  const shellMode = props.shellMode ?? (toolbarMode === "external" ? "editor-native-layout" : "legacy-host-layout");
  const externalLayout = shellMode === "editor-native-layout";
  const bottomOpen = bottomDockMode !== "collapsed";

  useEffect(() => {
    const next = cloneSchema(props.schema);
    setSchema(next);
    latestSchemaRef.current = next;
    savedSchemaSignatureRef.current = schemaWorkflowSignature(next);
    setDirty(false);
    setHistoryPast([]);
    setHistoryFuture([]);
    setIssues(validateNativeSchema(next));
  }, [props.schema]);

  const commitSchema = useCallback((next: MicroflowDesignSchema, reason: NativeHistoryReason, options: { pushHistory?: boolean; dirty?: boolean } = {}) => {
    const shouldPushHistory = options.pushHistory !== false && reason !== "selection" && reason !== "runtime";
    const normalized = cloneSchema(next);
    latestSchemaRef.current = normalized;
    setSchema(current => {
      if (shouldPushHistory) {
        setHistoryPast(past => [...past, cloneSchema(current)].slice(-100));
        setHistoryFuture([]);
      }
      return normalized;
    });
    const nextDirty = options.dirty ?? (reason !== "selection" && reason !== "runtime");
    if (nextDirty) {
      setDirty(true);
      props.onSchemaChange?.(normalized);
    }
    const nextIssues = validateNativeSchema(normalized);
    setIssues(nextIssues);
  }, [props]);

  const handleUndo = useCallback(() => {
    setHistoryPast(past => {
      const previous = past.at(-1);
      if (!previous) {
        return past;
      }
      setHistoryFuture(future => [cloneSchema(latestSchemaRef.current), ...future].slice(0, 100));
      const next = cloneSchema(previous);
      latestSchemaRef.current = next;
      setSchema(next);
      setDirty(schemaWorkflowSignature(next) !== savedSchemaSignatureRef.current);
      props.onSchemaChange?.(next);
      return past.slice(0, -1);
    });
  }, [props]);

  const handleRedo = useCallback(() => {
    setHistoryFuture(future => {
      const nextFuture = future[0];
      if (!nextFuture) {
        return future;
      }
      setHistoryPast(past => [...past, cloneSchema(latestSchemaRef.current)].slice(-100));
      const next = cloneSchema(nextFuture);
      latestSchemaRef.current = next;
      setSchema(next);
      setDirty(schemaWorkflowSignature(next) !== savedSchemaSignatureRef.current);
      props.onSchemaChange?.(next);
      return future.slice(1);
    });
  }, [props]);

  const runValidation = useCallback(async (mode: MicroflowValidationMode = "edit") => {
    setValidationStatus("validating");
    try {
      const localIssues = validateNativeSchema(latestSchemaRef.current, mode);
      setIssues(localIssues);
      const result: ValidateMicroflowResponse = {
        valid: !localIssues.some(item => item.severity === "error"),
        issues: localIssues,
      };
      props.onValidateComplete?.(result);
      props.onValidationStateChange?.({
        microflowId: latestSchemaRef.current.id,
        issues: localIssues,
        status: result.valid ? "valid" : "invalid",
        lastValidatedAt: new Date(),
      });
      setValidationStatus(result.valid ? "valid" : "invalid");
      setLastValidatedAt(new Date());
      return { issues: localIssues, summary: summarizeIssues(localIssues) };
    } catch (error) {
      const validationIssue = issue(`${latestSchemaRef.current.id}:validation-error`, error instanceof Error ? error.message : String(error), "MF_VALIDATION_SERVICE_ERROR");
      setIssues([validationIssue]);
      setValidationStatus("invalid");
      return { issues: [validationIssue], summary: summarizeIssues([validationIssue]) };
    }
  }, [props]);

  const handleSave = useCallback(async () => {
    if (props.readonly || saving) {
      return;
    }
    setSaving(true);
    try {
      const validation = await runValidation("save");
      const blockers = validation.issues.filter(item => item.blockSave && item.severity === "error");
      if (blockers.length > 0 || validation.summary.errorCount > 0) {
        setBottomDockMode("peek");
        setBottomTab("problems");
        Toast.error(`保存被 ${blockers.length || validation.summary.errorCount} 个校验错误阻止。`);
        return;
      }
      const response = await props.apiClient?.saveMicroflow({ schema: latestSchemaRef.current });
      savedSchemaSignatureRef.current = schemaWorkflowSignature(latestSchemaRef.current);
      setDirty(false);
      if (response) {
        props.onSaveComplete?.(response);
      }
      Toast.success("保存成功");
    } finally {
      setSaving(false);
    }
  }, [props, runValidation, saving]);

  const handleTestRun = useCallback(async () => {
    setRunning(true);
    try {
      const validation = await runValidation("testRun");
      if (validation.summary.errorCount > 0) {
        setBottomDockMode("peek");
        setBottomTab("problems");
        Toast.warning("存在校验错误，无法运行。");
        return;
      }
      const response = await props.apiClient?.testRunMicroflow({
        microflowId: latestSchemaRef.current.id,
        input: {},
        schema: latestSchemaRef.current,
      });
      if (response) {
        props.onTestRunComplete?.(response);
        Toast.success(`Run ${response.status}`);
      } else {
        Toast.warning("当前未配置运行适配器。");
      }
    } finally {
      setRunning(false);
    }
  }, [props, runValidation]);

  const handlePublish = useCallback(async () => {
    if (!props.onPublish) {
      Toast.warning("Publish handler is not configured.");
      return;
    }
    await props.onPublish(latestSchemaRef.current);
  }, [props]);

  const handleAddNode = useCallback((item: Parameters<NonNullable<React.ComponentProps<typeof MicroflowNodePanel>["onAddNode"]>>[0], options?: { position?: { x: number; y: number } }) => {
    const position = options?.position ?? { x: 360 + schema.workflow.nodes.length * 80, y: 240 };
    const node = createWorkflowNodeFromPanelItem(item, position, schema.workflow.nodes.map(existing => existing.id));
    const next = selectionPatch({
      ...schema,
      workflow: {
        ...schema.workflow,
        nodes: [...schema.workflow.nodes, node] as MicroflowWorkflowJSON["nodes"],
      },
    }, { objectId: node.id, flowId: undefined, collectionId: "root-collection", objectIds: [node.id], flowIds: [], mode: "single" });
    commitSchema(next, "workflow");
    setRightOpen(true);
  }, [commitSchema, schema]);

  const clearSelection = useCallback(() => {
    setContextMenu(undefined);
    commitSchema(selectionPatch(schema, { mode: "none", objectIds: [], flowIds: [] }), "selection", { pushHistory: false, dirty: false });
  }, [commitSchema, schema]);

  const handleDeleteSelection = useCallback((targetSelection?: FlowGramMicroflowSelection) => {
    if (props.readonly) {
      return;
    }
    const sourceSchema = latestSchemaRef.current;
    const selection = targetSelection ?? sourceSchema.editor.selection;
    const hasSelection = Boolean(selection.objectId || selection.flowId || selection.objectIds?.length || selection.flowIds?.length);
    if (!hasSelection) {
      return;
    }
    setContextMenu(undefined);
    commitSchema(deleteSelection(sourceSchema, selection), "delete");
  }, [commitSchema, props.readonly]);

  const handleAutoLayout = useCallback(() => {
    commitSchema(applyAutoLayout(schema), "layout");
    Toast.success("Auto layout applied.");
  }, [commitSchema, schema]);

  const handleEditorShortcut = useCallback((event: {
    key: string;
    target: EventTarget | null;
    ctrlKey: boolean;
    metaKey: boolean;
    shiftKey: boolean;
    defaultPrevented?: boolean;
    preventDefault: () => void;
  }) => {
    if (event.defaultPrevented || isEditableElement(event.target)) {
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
      event.preventDefault();
      clearSelection();
    }
  }, [clearSelection, handleDeleteSelection, handleRedo, handleSave, handleUndo, props.readonly]);

  const handleKeyDown = (event: ReactKeyboardEvent<HTMLDivElement>) => {
    handleEditorShortcut(event);
  };

  useEffect(() => {
    const handleDocumentKeyDown = (event: globalThis.KeyboardEvent) => {
      const shell = shellRef.current;
      if (!shell) {
        return;
      }
      const targetElement = event.target instanceof HTMLElement ? event.target : null;
      const activeElement = document.activeElement instanceof HTMLElement ? document.activeElement : null;
      const activeInEditor = targetElement ? shell.contains(targetElement) : activeElement ? shell.contains(activeElement) : false;
      if (!activeInEditor) {
        return;
      }
      handleEditorShortcut(event);
    };
    document.addEventListener("keydown", handleDocumentKeyDown, true);
    return () => document.removeEventListener("keydown", handleDocumentKeyDown, true);
  }, [handleEditorShortcut]);

  const layoutState = useMemo<MicroflowWorkbenchLayoutState>(() => ({
    shellMode,
    activeBottomTab: bottomTab,
    bottomDockMode,
    focusMode: false,
    minimapVisible: schema.editor.showMiniMap === true,
    gridVisible: schema.editor.gridEnabled !== false,
  }), [bottomDockMode, bottomTab, schema.editor.gridEnabled, schema.editor.showMiniMap, shellMode]);

  const workbenchStatus = useMemo<MicroflowWorkbenchStatus>(() => ({
    microflowId: schema.id,
    schemaVersion: schema.schemaVersion,
    dirty,
    saving,
    running,
    validationStatus,
    errorCount: issues.filter(item => item.severity === "error").length,
    warningCount: issues.filter(item => item.severity === "warning").length,
    canUndo: historyPast.length > 0,
    canRedo: historyFuture.length > 0,
    zoomPercent: Math.round((schema.editor.viewport?.zoom ?? 1) * 100),
    hasRunSession: false,
    fullscreen: false,
    activeBottomTab: bottomTab,
    bottomDockMode,
    sessionHydrated: false,
    traceHydrated: false,
    debugSessionHydrated: false,
    degradedRunSession: false,
    layout: layoutState,
  }), [bottomDockMode, bottomTab, dirty, historyFuture.length, historyPast.length, issues, layoutState, running, saving, schema.editor.viewport?.zoom, schema.id, schema.schemaVersion, validationStatus]);

  useEffect(() => {
    props.onLayoutStateChange?.(layoutState);
  }, [layoutState, props]);

  useEffect(() => {
    props.onWorkbenchStatusChange?.(workbenchStatus);
  }, [props, workbenchStatus]);

  useEffect(() => {
    if (!contextMenu) {
      return undefined;
    }
    const close = (event: MouseEvent) => {
      const target = event.target instanceof Node ? event.target : null;
      if (target && contextMenuRef.current?.contains(target)) {
        return;
      }
      setContextMenu(undefined);
    };
    const closeOnScroll = () => setContextMenu(undefined);
    window.addEventListener("mousedown", close);
    window.addEventListener("scroll", closeOnScroll, true);
    return () => {
      window.removeEventListener("mousedown", close);
      window.removeEventListener("scroll", closeOnScroll, true);
    };
  }, [contextMenu]);

  useImperativeHandle(props.editorRef, () => ({
    save: async () => {
      await handleSave();
    },
    validate: async () => {
      await runValidation("save");
    },
    runTest: async () => {
      await handleTestRun();
    },
    runDebug: async () => {
      setBottomDockMode("peek");
      setBottomTab("debug");
      await handleTestRun();
    },
    publish: handlePublish,
    undo: handleUndo,
    redo: handleRedo,
    autoLayout: handleAutoLayout,
    fitView: () => window.dispatchEvent(new CustomEvent("atlas:microflow-flowgram-fit-view")),
    zoomIn: () => window.dispatchEvent(new CustomEvent("atlas:microflow-flowgram-zoom", { detail: { delta: 0.1 } })),
    zoomOut: () => window.dispatchEvent(new CustomEvent("atlas:microflow-flowgram-zoom", { detail: { delta: -0.1 } })),
    setZoom: (zoom: number) => window.dispatchEvent(new CustomEvent("atlas:microflow-flowgram-zoom", { detail: { zoom } })),
    toggleFullscreen: () => undefined,
    toggleFocusMode: () => undefined,
    toggleMinimap: () => {
      commitSchema({ ...schema, editor: { ...schema.editor, showMiniMap: !schema.editor.showMiniMap } }, "layout");
    },
    resetLayout: () => undefined,
    getStatus: () => workbenchStatus as MicroflowEditorStatusSnapshot,
    openBottomTab: (tab: MicroflowWorkbenchBottomTab) => {
      setBottomTab(tab);
      setBottomDockMode("peek");
    },
    setBottomDockMode,
    getLayoutState: () => layoutState,
  }), [commitSchema, handleAutoLayout, handlePublish, handleRedo, handleSave, handleTestRun, handleUndo, layoutState, runValidation, schema, workbenchStatus]);

  const shellStyle: CSSProperties = {
    display: "grid",
    gridTemplateRows: [
      toolbarMode === "internal" ? "60px" : undefined,
      "minmax(0, 1fr)",
      bottomOpen ? `${BOTTOM_DOCK_PEEK_HEIGHT_PX}px` : undefined,
      `${BOTTOM_STRIP_HEIGHT_PX}px`,
    ].filter(Boolean).join(" "),
    height: "100%",
    minHeight: 0,
    position: "relative",
    background: "var(--semi-color-bg-0, #f7f8fa)",
  };
  const bodyStyle: CSSProperties = {
    display: "grid",
    gridTemplateColumns: `${RAIL_WIDTH_PX}px minmax(0, 1fr) ${RAIL_WIDTH_PX}px`,
    minHeight: 0,
    minWidth: 0,
    overflow: "hidden",
    position: "relative",
  };
  const leftDockStyle: CSSProperties = {
    position: "absolute",
    top: 0,
    bottom: 0,
    left: RAIL_WIDTH_PX,
    zIndex: 34,
    width: LEFT_PANEL_EXPANDED_PX,
    boxShadow: "8px 0 18px rgba(31, 35, 41, 0.08)",
  };
  const rightDockStyle: CSSProperties = {
    position: "absolute",
    top: 0,
    right: RAIL_WIDTH_PX,
    bottom: 0,
    zIndex: 34,
    boxShadow: "-8px 0 18px rgba(31, 35, 41, 0.08)",
  };
  const selectedNode = workflowNodeById(schema.workflow, schema.editor.selection.objectId) as MicroflowWorkflowNodeJSON | undefined;
  const selectedFlow = workflowEdgeById(schema.workflow, schema.editor.selection.flowId) as MicroflowWorkflowEdgeJSON | undefined;

  return (
    <div ref={shellRef} data-testid="microflow-editor-shell" data-microflow-id={schema.id} style={shellStyle} tabIndex={0} onKeyDown={handleKeyDown}>
      {toolbarMode === "internal" ? (
        <div data-testid="microflow-editor-toolbar" style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 12, padding: "8px 12px", borderBottom: "1px solid var(--semi-color-border, #e5e6eb)", background: "var(--semi-color-bg-2, #fff)" }}>
          <Space style={{ minWidth: 0, overflow: "hidden" }}>
            {props.toolbarPrefix}
            <Title heading={5} style={{ margin: 0 }}>{schema.displayName || schema.name}</Title>
            <Tag>{schema.schemaVersion}</Tag>
            {dirty ? <Tag color="orange">dirty</Tag> : <Tag color="green">saved</Tag>}
            <Tag color={issues.some(item => item.severity === "error") ? "red" : "green"}>{issues.length} issues</Tag>
          </Space>
          <Space>
            <Tooltip content={labels.undo}><Button icon={<IconUndo />} disabled={historyPast.length === 0} onClick={handleUndo} /></Tooltip>
            <Tooltip content={labels.redo}><Button icon={<IconRedo />} disabled={historyFuture.length === 0} onClick={handleRedo} /></Tooltip>
            <Button data-testid="microflow-editor-validate" icon={<IconRefresh />} loading={validationStatus === "validating"} onClick={() => void runValidation("save")}>{labels.validate}</Button>
            <Button data-testid="microflow-editor-run" icon={<IconPlay />} loading={running} disabled={saving || props.readonly} onClick={() => void handleTestRun()}>{labels.testRun}</Button>
            <Button data-testid="microflow-editor-save" icon={<IconSave />} type="primary" loading={saving} disabled={saving || props.readonly || !dirty} onClick={() => void handleSave()}>{labels.save}</Button>
            {props.toolbarSuffix}
          </Space>
        </div>
      ) : null}
      <div style={bodyStyle}>
        <button
          type="button"
          data-testid="microflow-node-panel-rail"
          aria-label={leftOpen ? "节点面板已展开" : "展开节点面板"}
          aria-expanded={leftOpen}
          style={{
            border: 0,
            borderRight: "1px solid var(--semi-color-border, #e5e6eb)",
            background: leftOpen ? "var(--semi-color-primary-light-default, #e8f3ff)" : "var(--semi-color-bg-2, #fff)",
            color: leftOpen ? "var(--semi-color-primary, #165dff)" : "inherit",
            cursor: "pointer",
          }}
          onClick={() => setLeftOpen(true)}
        >
          <Text size="small" strong style={{ writingMode: "vertical-rl", textOrientation: "mixed" }}>{labels.nodePanel}</Text>
        </button>
        {leftOpen ? (
          <div data-testid="microflow-editor-left-panel" style={{ ...panelStyle(), ...leftDockStyle }}>
            <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", gap: 8, marginBottom: 8 }}>
              <Text strong>{labels.nodePanel}</Text>
              <Button aria-label="折叠节点面板" size="small" theme="borderless" icon={<IconClose />} onClick={() => setLeftOpen(false)} />
            </div>
            <MicroflowNodePanel
              favoriteNodeKeys={[]}
              onFavoriteChange={() => undefined}
              onAddNode={(item, options) => handleAddNode(item, { position: options?.position })}
              onShowDocumentation={item => {
                Toast.info(item.documentation.summary);
              }}
              labels={props.nodePanelLabels}
              createContext={{ microflowId: schema.id, moduleId: schema.moduleId, metadataAvailable: Boolean(props.metadataCatalog), schemaLoaded: true, readonly: props.readonly }}
            />
          </div>
        ) : null}
        <div data-testid="microflow-canvas" style={{ minWidth: 0, minHeight: 0, display: "contents" }}>
          <FlowGramMicroflowNativeCanvas
            schema={schema}
            validationIssues={issues}
            runtimeTrace={[] as MicroflowTraceFrame[]}
            focusObjectId={focusObjectId}
            focusRequestKey={focusRequestSeq}
            readonly={props.readonly}
            onSchemaChange={(nextSchema, reason) => {
              commitSchema(nextSchema, reason === "flowgramNodeMove" ? "workflow" : "workflow");
            }}
            onSelectionChange={selection => {
              commitSchema(selectionPatch(latestSchemaRef.current, selection), "selection", { pushHistory: false, dirty: false });
              if (selection.objectId || selection.flowId) {
                setRightOpen(true);
              }
            }}
            onCanvasBlankClick={clearSelection}
            onNodeContextMenu={(selection, point) => {
              commitSchema(selectionPatch(latestSchemaRef.current, selection), "selection", { pushHistory: false, dirty: false });
              setContextMenu({ x: point.x, y: point.y, selection });
              setRightOpen(true);
            }}
            onDropRegistryItem={(item, position) => handleAddNode(item, { position })}
            canUndo={historyPast.length > 0}
            canRedo={historyFuture.length > 0}
            onUndo={handleUndo}
            onRedo={handleRedo}
            onAutoLayout={handleAutoLayout}
            onViewportChange={(viewport, options) => {
              commitSchema({ ...schema, editor: { ...schema.editor, viewport, zoom: viewport.zoom } }, "selection", { pushHistory: false, dirty: options?.skipDirty === true ? false : true });
            }}
            onToggleMiniMap={visible => commitSchema({ ...schema, editor: { ...schema.editor, showMiniMap: visible } }, "layout")}
            onToggleGrid={enabled => commitSchema({ ...schema, editor: { ...schema.editor, gridEnabled: enabled } }, "layout")}
            dirty={dirty}
            saving={saving}
            validating={validationStatus === "validating"}
            onOpenProblemsPanel={() => {
              setBottomDockMode("peek");
              setBottomTab("problems");
            }}
          />
        </div>
        <button
          type="button"
          data-testid="microflow-property-panel-rail"
          aria-label={rightOpen ? "属性面板已展开" : "展开属性面板"}
          aria-expanded={rightOpen}
          style={{
            border: 0,
            borderLeft: "1px solid var(--semi-color-border, #e5e6eb)",
            background: rightOpen ? "var(--semi-color-primary-light-default, #e8f3ff)" : "var(--semi-color-bg-2, #fff)",
            color: rightOpen ? "var(--semi-color-primary, #165dff)" : "inherit",
            cursor: "pointer",
          }}
          onClick={() => setRightOpen(true)}
        >
          <IconSetting />
          <Text size="small" strong style={{ writingMode: "vertical-rl", textOrientation: "mixed" }}>{labels.properties}</Text>
        </button>
        {rightOpen ? (
          <div data-testid="microflow-property-panel" style={{ ...propertyPaneStyle(), ...rightDockStyle }}>
            <MicroflowPropertyPanel
              schemaProtocol="design"
              schema={schema}
              validationIssues={issues}
              traceFrames={[] as MicroflowTraceFrame[]}
              readonly={props.readonly}
              onSchemaChange={(next, reason) => commitSchema(next, reason === "deleteNode" || reason === "deleteFlow" ? "delete" : "property")}
              onLocateObject={objectId => {
                setFocusObjectId(objectId);
                setFocusRequestSeq(value => value + 1);
              }}
              onClose={() => setRightOpen(false)}
            />
          </div>
        ) : null}
        {contextMenu ? (
          <div
            ref={contextMenuRef}
            data-testid="microflow-canvas-context-menu"
            data-context-object-id={contextMenu.selection.objectId ?? ""}
            data-context-flow-id={contextMenu.selection.flowId ?? ""}
            data-context-object-ids={(contextMenu.selection.objectIds ?? []).join(",")}
            data-context-flow-ids={(contextMenu.selection.flowIds ?? []).join(",")}
            role="menu"
            style={{
              position: "fixed",
              ...contextMenuPosition(contextMenu),
              zIndex: 1000,
              minWidth: 168,
              padding: 6,
              display: "grid",
              gap: 4,
              border: "1px solid var(--semi-color-border, #e5e6eb)",
              borderRadius: 6,
              boxShadow: "0 8px 24px rgba(31, 35, 41, 0.14)",
              background: "var(--semi-color-bg-2, #fff)",
            }}
            onPointerDownCapture={event => {
              const target = event.target;
              if (!(target instanceof HTMLElement) || !target.closest("[data-menu-action='delete']")) {
                return;
              }
              event.preventDefault();
              event.stopPropagation();
              handleDeleteSelection(contextMenu.selection);
            }}
            onMouseDown={event => event.stopPropagation()}
            onClick={event => event.stopPropagation()}
          >
            <Button
              role="menuitem"
              theme="borderless"
              style={{ justifyContent: "flex-start" }}
              onClick={() => {
                commitSchema(selectionPatch(schema, contextMenu.selection), "selection", { pushHistory: false, dirty: false });
                setRightOpen(true);
                setContextMenu(undefined);
              }}
            >
              属性
            </Button>
            {contextMenu.selection.objectId ? (
              <Button
                role="menuitem"
                theme="borderless"
                style={{ justifyContent: "flex-start" }}
                onClick={() => {
                  setFocusObjectId(contextMenu.selection.objectId);
                  setFocusRequestSeq(value => value + 1);
                  setContextMenu(undefined);
                }}
              >
                定位
              </Button>
            ) : null}
            <button
              type="button"
              role="menuitem"
              data-testid="microflow-canvas-context-menu-delete"
              data-menu-action="delete"
              disabled={props.readonly || selectionHasOnlyProtectedObjects(latestSchemaRef.current, contextMenu.selection)}
              style={{
                minHeight: 32,
                display: "flex",
                alignItems: "center",
                gap: 8,
                justifyContent: "flex-start",
                border: 0,
                borderRadius: 4,
                padding: "6px 12px",
                color: "var(--semi-color-danger, #f93920)",
                background: "transparent",
                cursor: props.readonly || selectionHasOnlyProtectedObjects(latestSchemaRef.current, contextMenu.selection) ? "not-allowed" : "pointer",
                opacity: props.readonly || selectionHasOnlyProtectedObjects(latestSchemaRef.current, contextMenu.selection) ? 0.45 : 1,
                font: "inherit",
              }}
              onPointerDown={event => {
                event.preventDefault();
                event.stopPropagation();
                handleDeleteSelection(contextMenu.selection);
              }}
              onMouseDown={event => {
                event.preventDefault();
                event.stopPropagation();
                handleDeleteSelection(contextMenu.selection);
              }}
              onClick={event => {
                event.preventDefault();
                event.stopPropagation();
                handleDeleteSelection(contextMenu.selection);
              }}
            >
              <IconDelete />
              删除
            </button>
          </div>
        ) : null}
      </div>
      {bottomOpen ? (
        <div
          data-testid="microflow-bottom-panel"
          style={{
            minHeight: 0,
            borderTop: "1px solid var(--semi-color-border, #e5e6eb)",
            background: "var(--semi-color-bg-1, #fff)",
            overflow: "hidden",
            padding: "8px 12px 12px",
          }}
        >
          <Tabs type="line" activeKey={bottomTab} onChange={key => setBottomTab(key as MicroflowWorkbenchBottomTab)}>
            <Tabs.TabPane tab={<Space><IconTickCircle />{labels.problems}<Badge count={issues.length} /></Space>} itemKey="problems">
              <Space vertical align="start" style={{ width: "100%", maxHeight: 200, overflow: "auto" }}>
                {issues.length === 0 ? <Empty title="No problems" /> : issues.map(item => (
                  <Button
                    key={item.id}
                    theme="borderless"
                    type={item.severity === "error" ? "danger" : "tertiary"}
                    onClick={() => {
                      if (item.objectId) {
                        setFocusObjectId(item.objectId);
                        setFocusRequestSeq(value => value + 1);
                      }
                    }}
                  >
                    {item.code}: {item.message}
                  </Button>
                ))}
              </Space>
            </Tabs.TabPane>
            <Tabs.TabPane tab={labels.debug} itemKey="debug">
              <Empty title="Debug trace is generated by the runtime adapter" description="当前原生设计态会在运行前单向编译为 runtime schema。" />
            </Tabs.TabPane>
          </Tabs>
        </div>
      ) : null}
      <div style={{ height: BOTTOM_STRIP_HEIGHT_PX, display: "flex", alignItems: "center", justifyContent: "space-between", padding: "0 12px", borderTop: "1px solid var(--semi-color-border, #e5e6eb)", background: "var(--semi-color-bg-2, #fff)" }}>
        <Space>
          <Tag color={issues.some(item => item.severity === "error") ? "red" : "green"}>{issues.length} issues</Tag>
          <Tag>{workflowNodeCount(schema.workflow)} nodes</Tag>
          <Tag>{workflowEdgeCount(schema.workflow)} edges</Tag>
          {lastValidatedAt ? <Text size="small" type="tertiary">{lastValidatedAt.toLocaleTimeString()}</Text> : null}
        </Space>
        <Space>
          {selectedNode ? <Tag color="blue">Node {selectedNode.id}</Tag> : null}
          {selectedFlow ? <Tag color="blue">Flow {(selectedFlow.data as Partial<FlowGramMicroflowEdgeData> | undefined)?.flowId ?? selectedFlow.id}</Tag> : null}
          <Button size="small" theme="borderless" icon={<IconDelete />} disabled={props.readonly || !(selectedNode || selectedFlow)} onClick={() => handleDeleteSelection()} />
          <Button size="small" theme="borderless" onClick={() => setBottomDockMode(mode => mode === "collapsed" ? "peek" : "collapsed")}>{bottomOpen ? "Hide" : "Problems"}</Button>
        </Space>
      </div>
    </div>
  );
}
