import { message } from "antd";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { CanvasToolbar } from "../components/CanvasToolbar";
import { ChatflowRolePanel } from "../components/ChatflowRolePanel";
import { DragTooltip } from "../components/DragTooltip";
import { FloatLayoutHolder, FloatLayoutProvider, useFloatLayoutService } from "../components/FloatLayout";
import { LineAddButton } from "../components/LineAddButton";
import { MinimapPanel } from "../components/MinimapPanel";
import { NodeDebugPanel } from "../components/NodeDebugPanel";
import { NodePanelPopover } from "../components/NodePanelPopover";
import { NodeSideSheet } from "../components/NodeSideSheet";
import { ProblemPanel } from "../components/ProblemPanel";
import { TestRunPanel } from "../components/TestRunPanel";
import { TracePanel } from "../components/TracePanel";
import { VariablePanel } from "../components/VariablePanel";
import { WorkflowHeader } from "../components/WorkflowHeader";
import { WORKFLOW_NODE_CATALOG } from "../constants/node-catalog";
import { createWorkflowEditorContainer } from "../di/container";
import { WorkflowEditorContainerProvider, useService } from "../di/provider";
import { WORKFLOW_EDITOR_DI } from "../di/symbols";
import { WorkflowRenderProvider } from "../flowgram/workflow-render-provider";
import { ensureWorkflowI18n } from "../i18n";
import { NodeRegistry } from "../node-registry";
import type { CanvasSchema } from "../types";
import { validateCanvas, type CanvasValidationResult } from "./editor-validation";
import type { WorkflowEditorReactProps } from "./workflow-editor-props";
import { ensureChatflowGlobals, readChatflowRoleConfig, validateChatflowRoleConfig } from "./chatflow-role-config";
import { buildVariableSuggestions } from "./smoke-utils";
import { validateTestRunPayload, type TestRunValidationIssueCode } from "./test-run-validation";
import type { CanvasConnection, CanvasNode } from "./workflow-editor-state";
import { NODE_HEIGHT, NODE_WIDTH, parseCanvasJson, toCanvasJson, type WorkflowViewportState } from "./workflow-editor-state";
import { useNodeSideSheetStore } from "../stores/node-side-sheet-store";
import { useWorkflowEditorStore } from "../stores/workflow-editor-store";
import { WorkflowDragService, WorkflowEditService, WorkflowOperationService, WorkflowRunService, WorkflowSaveService } from "../services";
import { WorkflowLineService } from "../services";
import "./workflow-editor.css";

interface ClipboardSnapshot {
  nodes: CanvasNode[];
  connections: CanvasConnection[];
}

interface PendingInsertContext {
  mode: "line" | "port" | "dragLine";
  lineId?: string;
  from?: { nodeKey: string; portKey: string; portType: "input" | "output" };
  to?: { nodeKey: string; portKey: string; portType: "input" | "output" };
  position?: { x: number; y: number };
}

const nodeRegistry = new NodeRegistry();

function WorkflowEditorCore(props: WorkflowEditorReactProps) {
  ensureWorkflowI18n(props.locale ?? "zh-CN");
  const { t } = useTranslation();
  const isReadOnly = Boolean(props.readOnly);
  const layoutService = useFloatLayoutService();
  const { open: openLayoutPanel, close: closeLayoutPanel } = layoutService;
  const dragService = useService<WorkflowDragService>(WORKFLOW_EDITOR_DI.workflowDragService);
  const operationService = useService<WorkflowOperationService>(WORKFLOW_EDITOR_DI.workflowOperationService);
  const editService = useService<WorkflowEditService>(WORKFLOW_EDITOR_DI.workflowEditService);
  const runService = useService<WorkflowRunService>(WORKFLOW_EDITOR_DI.workflowRunService);
  const saveService = useService<WorkflowSaveService>(WORKFLOW_EDITOR_DI.workflowSaveService);
  const lineService = useService<WorkflowLineService>(WORKFLOW_EDITOR_DI.workflowLineService);

  const store = useWorkflowEditorStore();
  const sideSheetVisible = useNodeSideSheetStore((state) => state.isVisible);
  const sideSheetNodeKey = useNodeSideSheetStore((state) => state.currentNodeKey);
  const openSideSheet = useNodeSideSheetStore((state) => state.openSideSheet);
  const closeSideSheet = useNodeSideSheetStore((state) => state.closeSideSheet);

  const [showNodePanel, setShowNodePanel] = useState(false);
  const [showTestPanel, setShowTestPanel] = useState(false);
  const [showProblemPanel, setShowProblemPanel] = useState(false);
  const [showTracePanel, setShowTracePanel] = useState(false);
  const [showMinimap, setShowMinimap] = useState(false);
  const [showVariablePanel, setShowVariablePanel] = useState(false);
  const [showRolePanel, setShowRolePanel] = useState(props.mode === "chatflow");
  const [showDebugPanel, setShowDebugPanel] = useState(false);
  const [interactionMode, setInteractionMode] = useState<"mouse" | "trackpad">("mouse");
  const [canvasValidation, setCanvasValidation] = useState<CanvasValidationResult | null>(null);
  const [draggingCatalogNodeType, setDraggingCatalogNodeType] = useState<string | null>(null);
  const [dragHint, setDragHint] = useState<string | undefined>(undefined);
  const [pendingInsertContext, setPendingInsertContext] = useState<PendingInsertContext | null>(null);
  const [hoveredLineId, setHoveredLineId] = useState<string | null>(null);
  const clipboardRef = useRef<ClipboardSnapshot | null>(null);
  const panRef = useRef({ x: 0, y: 0 });
  const zoomRef = useRef(100);
  const canvasShellRef = useRef<HTMLDivElement | null>(null);
  const selectedNodeKey = store.selectedNodeKeys[0] ?? "";
  const panelCommand = props.panelCommand;
  const focusNodeKey = props.focusNodeKey;
  const workflowMode = props.mode;
  const onValidationChange = props.onValidationChange;
  const onTraceStepsChange = props.onTraceStepsChange;

  useEffect(() => {
    operationService.bindProps(props);
  }, [operationService, props]);

  useEffect(() => {
    const nextTestRunSource = isReadOnly ? "published" : "draft";
    useWorkflowEditorStore.getState().setReadOnlyMode(isReadOnly);
    useWorkflowEditorStore.getState().setTestRunSource(nextTestRunSource);
  }, [isReadOnly]);

  useEffect(() => {
    if (props.workflowId) {
      void saveService.loadDocument();
    }
  }, [saveService, props.workflowId]);

  useEffect(() => {
    panRef.current = store.pan;
  }, [store.pan]);

  useEffect(() => {
    zoomRef.current = store.zoom;
  }, [store.zoom]);

  useEffect(() => {
    if (!selectedNodeKey) {
      if (sideSheetVisible) {
        closeSideSheet();
      }
      closeLayoutPanel("NodeForm");
      return;
    }
    if (sideSheetNodeKey !== selectedNodeKey || !sideSheetVisible) {
      openSideSheet(selectedNodeKey);
    }
    openLayoutPanel("NodeForm", { nodeKey: selectedNodeKey });
  }, [closeLayoutPanel, closeSideSheet, openLayoutPanel, openSideSheet, selectedNodeKey, sideSheetNodeKey, sideSheetVisible]);

  const selectedNode = useMemo(() => store.canvasNodes.find((item) => item.key === selectedNodeKey) ?? null, [selectedNodeKey, store.canvasNodes]);

  useEffect(() => {
    if (!store.debugNodeKey && selectedNode) {
      store.setDebugNodeKey(selectedNode.key);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedNode]);

  useEffect(() => {
    const command = panelCommand;
    if (!command) {
      return;
    }

    switch (command.type) {
      case "openNodePanel":
        setShowNodePanel(true);
        break;
      case "openTestRun":
        setShowTestPanel(true);
        openLayoutPanel("TestRunPanel");
        break;
      case "openTrace":
        setShowTracePanel(true);
        openLayoutPanel("TracePanel");
        break;
      case "openVariables":
        setShowVariablePanel(true);
        break;
      case "openRoleConfig":
        setShowRolePanel(true);
        break;
      case "openDebug":
        setShowDebugPanel(true);
        break;
      case "openProblems":
        setShowProblemPanel(true);
        openLayoutPanel("ProblemPanel");
        break;
      default:
        break;
    }
  }, [openLayoutPanel, panelCommand]);

  useEffect(() => {
    onValidationChange?.(canvasValidation);
  }, [canvasValidation, onValidationChange]);

  useEffect(() => {
    onTraceStepsChange?.(store.traceSteps);
  }, [onTraceStepsChange, store.traceSteps]);

  useEffect(() => {
    if (!focusNodeKey) {
      return;
    }

    editService.focusNode(focusNodeKey);
  }, [editService, focusNodeKey]);

  useEffect(() => {
    if (workflowMode !== "chatflow") {
      return;
    }

    const normalizedGlobals = ensureChatflowGlobals(store.canvasGlobals);
    if (normalizedGlobals !== store.canvasGlobals) {
      store.setCanvasGlobals(normalizedGlobals);
    }
  }, [store.canvasGlobals, store.setCanvasGlobals, workflowMode]);

  useEffect(() => {
    if (workflowMode !== "chatflow") {
      return;
    }

    const current = store.testInputJson.trim();
    if (!current || current === "{\"input\":\"hello\"}") {
      store.setTestInputJson(
        JSON.stringify(
          {
            USER_INPUT: "hello",
            CONVERSATION_NAME: "new_conversation"
          },
          null,
          2
        )
      );
    }
  }, [store.setTestInputJson, store.testInputJson, workflowMode]);

  const variableSuggestions = useMemo(
    () =>
      buildVariableSuggestions(
        store.canvasNodes.map((node) => ({ key: node.key, type: node.type, configs: node.configs, x: node.x })),
        selectedNodeKey,
        store.canvasConnections.map((connection) => ({ fromNode: connection.fromNode, toNode: connection.toNode })),
        store.canvasGlobals
      ),
    [selectedNodeKey, store.canvasConnections, store.canvasGlobals, store.canvasNodes]
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
      store.canvasNodes.map((node) => ({
        value: node.key,
        label: `${node.title || node.key} (${node.type})`
      })),
    [store.canvasNodes]
  );

  const enabledNodeTypes = useMemo(() => {
    if (store.nodeTypesMeta.length === 0) {
      return undefined;
    }
    return store.nodeTypesMeta.map((item) => String(item.key));
  }, [store.nodeTypesMeta]);

  const automationCanvasJson = useMemo(
    () =>
      toCanvasJson(
        store.canvasNodes,
        store.canvasConnections,
        store.canvasGlobals,
        { x: store.pan.x, y: store.pan.y, zoom: store.zoom }
      ),
    [store.canvasConnections, store.canvasGlobals, store.canvasNodes, store.pan.x, store.pan.y, store.zoom]
  );

  const lineSegments = useMemo(() => {
    const nodeMap = new Map(store.canvasNodes.map((node) => [node.key, node]));
    return store.canvasConnections
      .map((line) => {
        const fromNode = nodeMap.get(line.fromNode);
        const toNode = nodeMap.get(line.toNode);
        if (!fromNode || !toNode) {
          return null;
        }
        const from = { x: fromNode.x + NODE_WIDTH / 2, y: fromNode.y + NODE_HEIGHT / 2 };
        const to = { x: toNode.x + NODE_WIDTH / 2, y: toNode.y + NODE_HEIGHT / 2 };
        return {
          lineId: line.id,
          line,
          from,
          to,
          x: (from.x + to.x) / 2,
          y: (from.y + to.y) / 2
        };
      })
      .filter((item): item is { lineId: string; line: CanvasConnection; from: { x: number; y: number }; to: { x: number; y: number }; x: number; y: number } => item !== null);
  }, [store.canvasConnections, store.canvasNodes]);

  function distancePointToSegment(
    point: { x: number; y: number },
    start: { x: number; y: number },
    end: { x: number; y: number }
  ): number {
    const dx = end.x - start.x;
    const dy = end.y - start.y;
    if (dx === 0 && dy === 0) {
      return Math.hypot(point.x - start.x, point.y - start.y);
    }
    const t = Math.max(0, Math.min(1, ((point.x - start.x) * dx + (point.y - start.y) * dy) / (dx * dx + dy * dy)));
    const projection = {
      x: start.x + t * dx,
      y: start.y + t * dy
    };
    return Math.hypot(point.x - projection.x, point.y - projection.y);
  }

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

  function centerPointForCreate(): { x: number; y: number } {
    const shell = canvasShellRef.current;
    if (!shell) {
      return { x: 320, y: 320 };
    }
    const worldX = shell.clientWidth / 2 / (store.zoom / 100) - store.pan.x / (store.zoom / 100);
    const worldY = shell.clientHeight / 2 / (store.zoom / 100) - store.pan.y / (store.zoom / 100);
    return { x: Math.max(40, worldX - NODE_WIDTH / 2), y: Math.max(40, worldY - NODE_HEIGHT / 2) };
  }

  function runCanvasValidationAndReport(): CanvasValidationResult {
    const result = validateCanvas(store.canvasNodes, store.canvasConnections, store.nodeTypesMeta, store.canvasGlobals);
    if (props.mode === "chatflow") {
      const roleIssues = validateChatflowRoleConfig(readChatflowRoleConfig(store.canvasGlobals));
      if (roleIssues.length > 0) {
        result.canvasIssues.push(...roleIssues);
        result.ok = false;
      }
    }
    setCanvasValidation(result);
    if (!result.ok) {
      const firstNodeIssue = result.nodeResults.find((item) => item.issues.length > 0)?.issues[0];
      const firstCanvasIssue = result.canvasIssues[0];
      message.error(firstNodeIssue ?? firstCanvasIssue ?? "工作流配置存在校验错误，请先修复。");
    }
    return result;
  }

  function extractErrorMessage(error: unknown, fallbackMessage: string): string {
    if (typeof error === "string" && error.trim()) {
      return error;
    }
    if (error instanceof Error && error.message.trim()) {
      return error.message;
    }
    if (typeof error === "object" && error !== null) {
      const candidate = error as {
        message?: string;
        response?: {
          data?: {
            message?: string;
            errorMessage?: string;
          };
        };
        data?: {
          message?: string;
          errorMessage?: string;
        };
      };
      const nestedMessage =
        candidate.response?.data?.message ??
        candidate.response?.data?.errorMessage ??
        candidate.data?.message ??
        candidate.data?.errorMessage ??
        candidate.message;
      if (typeof nestedMessage === "string" && nestedMessage.trim()) {
        return nestedMessage;
      }
    }
    return fallbackMessage;
  }

  function getTestRunIssueLabel(issue: TestRunValidationIssueCode): string {
    switch (issue) {
      case "invalidJson":
        return t("wfUi.testRun.invalidJson");
      case "invalidPayload":
        return t("wfUi.testRun.invalidPayload");
      case "userInputRequired":
        return t("wfUi.testRun.userInputRequired");
      case "conversationNameRequired":
        return t("wfUi.testRun.conversationNameRequired");
      case "conversationNameTooLong":
        return t("wfUi.testRun.conversationNameTooLong");
      default:
        return t("wfUi.testRun.invalidPayload");
    }
  }

  const handleSave = useCallback(async (): Promise<void> => {
    if (isReadOnly) {
      message.warning("只读模式下不可保存。");
      return;
    }
    try {
      await saveService.save(false);
      setCanvasValidation(null);
      message.success("草稿已保存。");
    } catch (error) {
      message.error(extractErrorMessage(error, "保存草稿失败，请稍后重试。"));
    }
  }, [isReadOnly, saveService]);

  async function handlePublish(): Promise<void> {
    if (isReadOnly) {
      message.warning("只读模式下不可发布。");
      return;
    }
    const result = runCanvasValidationAndReport();
    if (!result.ok) {
      return;
    }
    try {
      if (store.isDirty) {
        await saveService.save(false);
      }
      await operationService.publish();
      message.success("工作流已发布。");
      store.setDirty(false);
    } catch (error) {
      message.error(extractErrorMessage(error, "发布失败，请先检查工作流配置后重试。"));
    }
  }

  async function handleDuplicate(): Promise<void> {
    if (isReadOnly) {
      message.warning("只读模式下不可复制。");
      return;
    }
    try {
      const duplicatedId = await operationService.copy();
      if (!duplicatedId) {
        message.warning("当前环境未启用复制接口。");
        return;
      }
      const nextUrl = buildEditorUrlByWorkflowId(duplicatedId);
      window.open(nextUrl, "_blank", "noopener,noreferrer");
      message.success("已在新标签页打开复制后的工作流。");
    } catch (error) {
      message.error(extractErrorMessage(error, "复制工作流失败，请稍后重试。"));
    }
  }

  async function handleTestRun(): Promise<void> {
    const validation = validateTestRunPayload(store.testInputJson, props.mode);
    if (validation.issues.length > 0) {
      message.error(getTestRunIssueLabel(validation.issues[0]));
      setShowTestPanel(true);
        openLayoutPanel("TestRunPanel");
        return;
      }

      await runService.testRun();
  }

  const buildClipboardSnapshot = useCallback((): ClipboardSnapshot | null => {
    const state = useWorkflowEditorStore.getState();
    if (state.selectedNodeKeys.length === 0) {
      return null;
    }
    const selectedSet = new Set(state.selectedNodeKeys);
    const nodes = state.canvasNodes.filter((node) => selectedSet.has(node.key));
    if (nodes.length === 0) {
      return null;
    }
    const connections = state.canvasConnections.filter((line) => selectedSet.has(line.fromNode) && selectedSet.has(line.toNode));
    return { nodes: structuredClone(nodes), connections: structuredClone(connections) };
  }, []);

  const pasteClipboardSnapshot = useCallback((snapshot: ClipboardSnapshot): string[] => {
    const state = useWorkflowEditorStore.getState();
    const usedKeys = new Set(state.canvasNodes.map((node) => node.key));
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

    state.setCanvasNodes([...state.canvasNodes, ...createdNodes]);
    state.setCanvasConnections([...state.canvasConnections, ...createdConnections]);
    state.setDirty(true);
    return createdNodes.map((node) => node.key);
  }, []);

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      const state = useWorkflowEditorStore.getState();
      const element = event.target as HTMLElement | null;
      if (element && (element.closest("input, textarea, [contenteditable='true']") || ["INPUT", "TEXTAREA", "SELECT"].includes(element.tagName))) {
        return;
      }

      const isMeta = event.ctrlKey || event.metaKey;
      const lowerKey = event.key.toLowerCase();
      if (isMeta && lowerKey === "c" && !isReadOnly) {
        const snapshot = buildClipboardSnapshot();
        if (!snapshot) {
          return;
        }
        clipboardRef.current = snapshot;
        event.preventDefault();
      } else if (isMeta && lowerKey === "v" && !isReadOnly) {
        if (!clipboardRef.current) {
          return;
        }
        const createdNodeKeys = pasteClipboardSnapshot(clipboardRef.current);
        state.setSelectedNodeKeys(createdNodeKeys);
        event.preventDefault();
      } else if (isMeta && lowerKey === "d" && !isReadOnly) {
        const snapshot = buildClipboardSnapshot();
        if (!snapshot) {
          return;
        }
        clipboardRef.current = snapshot;
        const createdNodeKeys = pasteClipboardSnapshot(snapshot);
        state.setSelectedNodeKeys(createdNodeKeys);
        event.preventDefault();
      } else if (isMeta && lowerKey === "a") {
        state.setSelectedNodeKeys(state.canvasNodes.map((node) => node.key));
        event.preventDefault();
      } else if ((event.key === "Delete" || event.key === "Backspace") && !isReadOnly) {
        if (state.selectedNodeKeys.length === 0) {
          return;
        }
        for (const key of state.selectedNodeKeys) {
          editService.deleteNode(key);
        }
        event.preventDefault();
      } else if (isMeta && lowerKey === "s" && !isReadOnly) {
        event.preventDefault();
        void handleSave();
      }
    };
    window.addEventListener("keydown", onKeyDown);
    return () => {
      window.removeEventListener("keydown", onKeyDown);
    };
  }, [buildClipboardSnapshot, editService, handleSave, isReadOnly, pasteClipboardSnapshot]);

  const flowgramCanvasSchema: CanvasSchema = useMemo(() => ({
    nodes: store.canvasNodes.map((node) => ({
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
    connections: store.canvasConnections.map((line) => ({
      fromNode: line.fromNode,
      fromPort: line.fromPort,
      toNode: line.toNode,
      toPort: line.toPort,
      condition: line.condition
    })),
    schemaVersion: 2,
    globals: store.canvasGlobals,
    viewport: { x: store.pan.x, y: store.pan.y, zoom: store.zoom }
  }), [store.canvasNodes, store.canvasConnections, store.canvasGlobals, store.pan, store.zoom]);

  const hasCanvasValidationErrors = Boolean(
    canvasValidation &&
      (!canvasValidation.ok ||
        canvasValidation.canvasIssues.length > 0 ||
        canvasValidation.nodeResults.some((item) => item.issues.length > 0))
  );
  const embeddedChrome = props.chromeMode === "embedded";

  return (
    <div className={`wf-react-editor-page${embeddedChrome ? " wf-react-editor-page--embedded" : ""}`}>
      {!embeddedChrome ? (
        <>
          <WorkflowHeader
            name={store.workflowName}
            dirty={store.isDirty}
            savedAt={store.lastSavedAt}
            readOnly={isReadOnly || store.saving}
            mode={props.mode}
            onNameChange={(value) => {
              if (isReadOnly) {
                return;
              }
              store.setWorkflowName(value);
              store.setDirty(true);
            }}
            onBack={() => props.onBack?.()}
            onDuplicate={() => void handleDuplicate()}
            onSave={() => void handleSave()}
            onPublish={() => void handlePublish()}
          />
          <div className="wf-react-automation-bridge">
            <label className="wf-react-automation-field">
              <span>画布 JSON</span>
              <textarea
                data-testid="workflow.detail.canvas-json"
                value={automationCanvasJson}
                readOnly={isReadOnly}
                onChange={(event) => {
                  if (isReadOnly) {
                    return;
                  }

                  const parsed = parseCanvasJson(event.target.value);
                  store.setCanvasSnapshot({
                    nodes: parsed.nodes,
                    connections: parsed.connections,
                    globals: parsed.globals,
                    viewport: parsed.viewport ?? { x: store.pan.x, y: store.pan.y, zoom: store.zoom },
                    workflowName: store.workflowName,
                    isDirty: true
                  });
                  setCanvasValidation(null);
                  saveService.listenContentChange({ type: "META_CHANGE" });
                }}
              />
            </label>
            <label className="wf-react-automation-field">
              <span>试运行输入</span>
              <textarea
                data-testid="workflow.detail.run-inputs"
                value={store.testInputJson}
                onChange={(event) => store.setTestInputJson(event.target.value)}
              />
            </label>
          </div>
        </>
      ) : null}
      <div
        ref={canvasShellRef}
        className="wf-react-canvas-shell"
        onMouseMove={(event) => {
          const shell = canvasShellRef.current;
          if (!shell || showNodePanel) {
            setHoveredLineId(null);
            return;
          }
          const rect = shell.getBoundingClientRect();
          const pointerX = event.clientX - rect.left;
          const pointerY = event.clientY - rect.top;
          const scale = store.zoom / 100;
          let winner: { lineId: string; distance: number } | null = null;
          for (const line of lineSegments) {
            const start = { x: line.from.x * scale + store.pan.x, y: line.from.y * scale + store.pan.y };
            const end = { x: line.to.x * scale + store.pan.x, y: line.to.y * scale + store.pan.y };
            const distance = distancePointToSegment({ x: pointerX, y: pointerY }, start, end);
            if (distance <= 14 && (!winner || distance < winner.distance)) {
              winner = { lineId: line.lineId, distance };
            }
          }
          const nextHoveredLineId = winner?.lineId ?? null;
          setHoveredLineId((current) => (current === nextHoveredLineId ? current : nextHoveredLineId));
        }}
        onMouseLeave={() => setHoveredLineId((current) => (current === null ? current : null))}
        onDragOver={(event) => {
          if (draggingCatalogNodeType) {
            event.preventDefault();
            event.dataTransfer.dropEffect = "copy";
            const canDrop = dragService.canDrop({
              coord: { x: event.clientX, y: event.clientY },
              dragNode: { type: draggingCatalogNodeType }
            });
            setDragHint(canDrop ? undefined : dragService.canDropToNode(draggingCatalogNodeType).message);
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
          dragService.startDrag({ type: nodeType });
          const canDrop = dragService.canDrop({
            coord: { x: world.x, y: world.y },
            dragNode: { type: nodeType }
          });
          if (!canDrop) {
            message.warning(dragService.canDropToNode(nodeType).message ?? "当前目标位置不允许放置该节点。");
            dragService.endDrag();
            return;
          }
          editService.addNode(nodeType, undefined, { x: Math.max(24, world.x - NODE_WIDTH / 2), y: Math.max(24, world.y - NODE_HEIGHT / 2) }, true);
          setDraggingCatalogNodeType(null);
          setShowNodePanel(false);
          setDragHint(undefined);
        }}
        onWheel={(event) => {
          if (!event.ctrlKey) {
            return;
          }
          event.preventDefault();
          const next = Math.max(25, Math.min(200, Math.round(store.zoom - event.deltaY * 0.06)));
          store.setZoom(next);
        }}
      >
        <WorkflowRenderProvider
          canvas={flowgramCanvasSchema}
          readonly={isReadOnly}
          nodeTypesMeta={store.nodeTypesMeta}
          edgeStateByKey={store.edgeStateByConnectionKey}
          onPortClick={(params) => {
            if (isReadOnly) {
              return;
            }
            setPendingInsertContext({
              mode: "port",
              from: {
                nodeKey: params.nodeKey,
                portKey: params.portKey,
                portType: params.portType
              }
            });
            setShowNodePanel(true);
          }}
          onDragLineEnd={(params) => {
            if (isReadOnly) {
              return;
            }
            const fromPort = params?.fromPort;
            if (!fromPort || params?.toPort) {
              return;
            }
            const candidate = lineService.onDragLineEnd({
              fromPort: {
                nodeKey: String(fromPort.node?.id ?? ""),
                portKey: String(fromPort.portID ?? "output")
              },
              toPort: undefined
            });
            if (!candidate.allowInsert) {
              if (candidate.message) {
                message.warning(candidate.message);
              }
              return;
            }
            const mousePos = params?.mousePos;
            const position =
              mousePos && typeof mousePos.x === "number" && typeof mousePos.y === "number"
                ? resolveWorldPoint(mousePos.x, mousePos.y) ?? undefined
                : undefined;
            setPendingInsertContext({
              mode: "dragLine",
              from: {
                nodeKey: String(fromPort.node?.id ?? ""),
                portKey: String(fromPort.portID ?? "output"),
                portType: "output"
              },
              position
            });
            setShowNodePanel(true);
          }}
          onSelectionChange={(nodeKeys) => store.setSelectedNodeKeys(nodeKeys)}
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
            const viewport = next.viewport as WorkflowViewportState | undefined;
            store.setCanvasNodes(nextNodes);
            store.setCanvasConnections(nextConnections);
            if (viewport) {
              store.setPan({ x: viewport.x, y: viewport.y });
              store.setZoom(viewport.zoom);
            }
            store.setDirty(true);
            setCanvasValidation(null);
            saveService.listenContentChange({ type: "NODE_ADD" });
          }}
        />

        <NodePanelPopover
          visible={showNodePanel}
          nodes={WORKFLOW_NODE_CATALOG}
          enabledTypes={enabledNodeTypes}
          onDragStart={(nodeType) => {
            setDraggingCatalogNodeType(nodeType);
            dragService.startDrag({ type: nodeType });
          }}
          onDragEnd={() => {
            setDraggingCatalogNodeType(null);
            dragService.endDrag();
            setDragHint(undefined);
          }}
          onSelect={(nodeType) => {
            if (isReadOnly) {
              return;
            }
            if (pendingInsertContext?.mode === "line" && pendingInsertContext.lineId) {
              const targetLine = store.canvasConnections.find((line) => line.id === pendingInsertContext.lineId);
              const midpoint = lineSegments.find((item) => item.lineId === pendingInsertContext.lineId);
              if (targetLine && midpoint) {
                const insertedNode = editService.addNode(nodeType, undefined, { x: midpoint.x - NODE_WIDTH / 2, y: midpoint.y - NODE_HEIGHT / 2 }, false);
                if (insertedNode) {
                  store.setCanvasConnections(store.canvasConnections.filter((line) => line.id !== pendingInsertContext.lineId));
                  lineService.createLine(
                    { nodeKey: targetLine.fromNode, portKey: targetLine.fromPort },
                    { nodeKey: insertedNode.key, portKey: "input" }
                  );
                  lineService.createLine(
                    { nodeKey: insertedNode.key, portKey: "output" },
                    { nodeKey: targetLine.toNode, portKey: targetLine.toPort }
                  );
                }
              }
            } else if (pendingInsertContext?.mode === "port" && pendingInsertContext.from) {
              const from = pendingInsertContext.from;
              const anchorNode = store.canvasNodes.find((node) => node.key === from.nodeKey);
              const fallback = centerPointForCreate();
              const suggested = anchorNode
                ? {
                    x: from.portType === "output" ? anchorNode.x + 360 : Math.max(40, anchorNode.x - 360),
                    y: anchorNode.y
                  }
                : fallback;
              const insertedNode = editService.addNode(nodeType, undefined, suggested, false);
              if (insertedNode) {
                if (from.portType === "output") {
                  lineService.createLine(
                    { nodeKey: from.nodeKey, portKey: from.portKey },
                    { nodeKey: insertedNode.key, portKey: "input" }
                  );
                } else {
                  lineService.createLine(
                    { nodeKey: insertedNode.key, portKey: "output" },
                    { nodeKey: from.nodeKey, portKey: from.portKey }
                  );
                }
              }
            } else if (pendingInsertContext?.mode === "dragLine" && pendingInsertContext.from) {
              const base = pendingInsertContext.position ?? centerPointForCreate();
              const insertedNode = editService.addNode(nodeType, undefined, base, false);
              if (insertedNode) {
                const from = pendingInsertContext.from;
                lineService.createLine(
                  { nodeKey: from.nodeKey, portKey: from.portKey },
                  { nodeKey: insertedNode.key, portKey: "input" }
                );
              }
            } else {
              const center = centerPointForCreate();
              const insertedNode = editService.addNode(nodeType, undefined, center, false);
              if (insertedNode && store.selectedNodeKeys.length === 1) {
                const selectedNode = store.selectedNodeKeys[0];
                if (selectedNode && selectedNode !== insertedNode.key) {
                  lineService.createLine(
                    { nodeKey: selectedNode, portKey: "output" },
                    { nodeKey: insertedNode.key, portKey: "input" }
                  );
                }
              }
            }
            setPendingInsertContext(null);
            setShowNodePanel(false);
          }}
        />

        {!isReadOnly && !showNodePanel
          ? lineSegments.map((item) => {
              const scale = store.zoom / 100;
              return (
                <LineAddButton
                  key={item.lineId}
                  visible={hoveredLineId === item.lineId}
                  x={item.x * scale + store.pan.x}
                  y={item.y * scale + store.pan.y}
                  onClick={() => {
                    setPendingInsertContext({ mode: "line", lineId: item.lineId });
                    setShowNodePanel(true);
                  }}
                />
              );
            })
          : null}

        <FloatLayoutHolder
          nodeForm={<NodeSideSheet />}
          problemPanel={
            <ProblemPanel
              visible={showProblemPanel}
              validation={canvasValidation}
              onClose={() => setShowProblemPanel(false)}
              onSelectNode={(nodeKey) => {
                editService.focusNode(nodeKey);
                setShowProblemPanel(false);
              }}
            />
          }
          tracePanel={<TracePanel visible={showTracePanel} steps={store.traceSteps} onClose={() => setShowTracePanel(false)} />}
          testRunPanel={
            <TestRunPanel
              visible={showTestPanel}
              logs={store.logs}
              running={store.testRunning}
              workflowMode={props.mode}
              source={store.testRunSource}
              mode={store.testRunMode}
              inputJson={store.testInputJson}
              onInputJsonChange={store.setTestInputJson}
              onSourceChange={store.setTestRunSource}
              onModeChange={store.setTestRunMode}
              onClose={() => setShowTestPanel(false)}
              onRun={() => void handleTestRun()}
            />
          }
        />

        <NodeDebugPanel
          visible={showDebugPanel}
          running={store.debugRunning}
          nodeOptions={debugNodeOptions}
          selectedNodeKey={store.debugNodeKey}
          inputJson={store.debugInputJson}
          output={store.debugOutput}
          onNodeChange={store.setDebugNodeKey}
          onInputJsonChange={store.setDebugInputJson}
          onRun={() => void runService.testRunOneNode(store.debugNodeKey, store.debugInputJson)}
          onClose={() => setShowDebugPanel(false)}
        />

        <VariablePanel
          visible={showVariablePanel}
          variables={variablePanelItems}
          globals={store.canvasGlobals}
          onChangeGlobals={(next) => {
            if (isReadOnly) {
              return;
            }
            store.setCanvasGlobals(next);
            store.setDirty(true);
            saveService.listenContentChange({ type: "META_CHANGE" });
          }}
          onClose={() => setShowVariablePanel(false)}
        />

        {props.mode === "chatflow" ? (
          <ChatflowRolePanel visible={showRolePanel} readOnly={isReadOnly} onClose={() => setShowRolePanel(false)} />
        ) : null}

        <MinimapPanel visible={showMinimap} nodes={store.canvasNodes.map((node) => ({ key: node.key, x: node.x, y: node.y }))} selectedNodeKey={selectedNodeKey} />

        <CanvasToolbar
          zoom={store.zoom}
          mode={interactionMode}
          minimapVisible={showMinimap}
          readOnly={isReadOnly}
          onZoomChange={store.setZoom}
          onModeChange={setInteractionMode}
          onToggleNodePanel={() => setShowNodePanel((value) => !value)}
          onToggleMinimap={() => setShowMinimap((value) => !value)}
          onAutoLayout={() => {
            if (isReadOnly) {
              return;
            }
            const xStart = 80;
            const yStart = 80;
            const colGap = 420;
            const rowGap = 220;
            store.setCanvasNodes(
              store.canvasNodes.map((node, index) => ({
                ...node,
                x: xStart + (index % 4) * colGap,
                y: yStart + Math.floor(index / 4) * rowGap
              }))
            );
            store.setDirty(true);
            saveService.listenContentChange({ type: "MOVE_NODE" });
          }}
          onToggleVariables={() => setShowVariablePanel((value) => !value)}
          showRoleConfig={props.mode === "chatflow"}
          onToggleRoleConfig={() => setShowRolePanel((value) => !value)}
          onToggleDebug={() => setShowDebugPanel((value) => !value)}
          onToggleTrace={() => {
            setShowTracePanel((value) => !value);
            layoutService.open("TracePanel");
          }}
          onToggleProblems={() => {
            setShowProblemPanel((value) => !value);
            layoutService.open("ProblemPanel");
          }}
          onRun={() => {
            setShowTestPanel((value) => !value);
            layoutService.open("TestRunPanel");
          }}
        />
        <DragTooltip dragging={Boolean(draggingCatalogNodeType)} message={dragHint} />
      </div>
      {hasCanvasValidationErrors ? <div className="wf-react-validation-banner">检测到校验问题，可点击“问题”按钮查看详情。</div> : null}
    </div>
  );
}

export function WorkflowEditorReact(props: WorkflowEditorReactProps) {
  const containerRef = useRef(createWorkflowEditorContainer());
  return (
    <WorkflowEditorContainerProvider container={containerRef.current}>
      <FloatLayoutProvider>
        <WorkflowEditorCore {...props} />
      </FloatLayoutProvider>
    </WorkflowEditorContainerProvider>
  );
}

function buildEditorUrlByWorkflowId(workflowId: string): string {
  if (typeof window === "undefined") {
    return `/workflows/${encodeURIComponent(workflowId)}/editor`;
  }
  const pathname = window.location.pathname;
  const nextPathname = pathname
    .replace(/\/workflows\/[^/]+\/editor$/, `/workflows/${encodeURIComponent(workflowId)}/editor`)
    .replace(/\/work_flow\/[^/]+\/editor$/, `/work_flow/${encodeURIComponent(workflowId)}/editor`)
    .replace(/\/chat_flow\/[^/]+\/editor$/, `/chat_flow/${encodeURIComponent(workflowId)}/editor`);
  return `${window.location.origin}${nextPathname}${window.location.search}`;
}

export function exportCurrentCanvasJson(): string {
  const state = useWorkflowEditorStore.getState();
  return toCanvasJson(state.canvasNodes, state.canvasConnections, state.canvasGlobals, {
    x: state.pan.x,
    y: state.pan.y,
    zoom: state.zoom
  });
}
