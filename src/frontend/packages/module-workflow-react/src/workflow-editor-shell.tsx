import { useCallback, useEffect, useMemo, useState } from "react";
import { Button, Empty, Input, Spin, Toast, Typography } from "@douyinfe/semi-ui";
import type {
  CanvasSchema,
  NodeTypeMetadata,
  WorkflowDetailResponse,
  WorkflowVersionItem
} from "@atlas/workflow-core-react";
import {
  WorkflowEditor,
  type WorkflowPanelCommand,
  type WorkflowPanelCommandType
} from "@atlas/workflow-editor-react";
import type { WorkflowListItem, WorkflowPageProps, WorkflowResourceMode } from "./types";
import { getWorkflowModuleCopy } from "./copy";
import { buildReferenceSidebarSections, buildResourceSidebarSections, type WorkflowSidebarItem } from "./coze-adapter";

interface WorkflowEditorShellProps extends WorkflowPageProps {
  workflowId: string;
  onBack: () => void;
  backPath?: string;
  mode?: WorkflowResourceMode;
}

interface WorkflowProcessSnapshot {
  status?: number;
  startedAt?: string;
  completedAt?: string;
  errorMessage?: string;
  nodeExecutions: Array<{
    nodeKey: string;
    status: number;
    errorMessage?: string;
  }>;
}

type SidebarTabKey = "resources" | "references";
type WorkspaceViewKey = "logic" | "ui";

interface WorkflowProcessResponse {
  status?: number;
  startedAt?: string;
  completedAt?: string;
  errorMessage?: string;
  nodeExecutions?: Array<{
    nodeKey: string;
    status: number;
    errorMessage?: string;
  }>;
}

function normalizeCanvasSchema(canvas: unknown): CanvasSchema {
  if (!canvas || typeof canvas !== "object") {
    return {
      nodes: [],
      connections: []
    };
  }

  const record = canvas as Partial<CanvasSchema>;
  return {
    nodes: Array.isArray(record.nodes) ? record.nodes : [],
    connections: Array.isArray(record.connections) ? record.connections : [],
    schemaVersion: record.schemaVersion,
    viewport: record.viewport,
    globals: record.globals
  };
}

function safeParseCanvas(canvasJson?: string): CanvasSchema {
  if (!canvasJson) {
    return { nodes: [], connections: [] };
  }

  try {
    return normalizeCanvasSchema(JSON.parse(canvasJson));
  } catch {
    return { nodes: [], connections: [] };
  }
}

function resolveExecutionStatus(locale: "zh-CN" | "en-US", status?: number): string {
  if (locale === "en-US") {
    switch (status) {
      case 1:
        return "Running";
      case 2:
        return "Completed";
      case 3:
        return "Failed";
      case 4:
        return "Cancelled";
      case 5:
        return "Interrupted";
      case 6:
        return "Skipped";
      case 7:
        return "Blocked";
      default:
        return "Pending";
    }
  }

  switch (status) {
    case 1:
      return "运行中";
    case 2:
      return "已完成";
    case 3:
      return "失败";
    case 4:
      return "已取消";
    case 5:
      return "已中断";
    case 6:
      return "已跳过";
    case 7:
      return "已阻塞";
    default:
      return "待执行";
  }
}

function formatDateTime(value: string | undefined, locale: "zh-CN" | "en-US"): string {
  if (!value) {
    return "-";
  }

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString(locale);
}

function buildEditorPath(workflowId: string, mode: WorkflowResourceMode): string {
  if (typeof window === "undefined") {
    return mode === "chatflow"
      ? `/chat_flow/${encodeURIComponent(workflowId)}/editor`
      : `/work_flow/${encodeURIComponent(workflowId)}/editor`;
  }

  const segment = mode === "chatflow" ? "chat_flow" : "work_flow";
  return `${window.location.pathname.replace(
    /\/(work_flow|chat_flow)\/[^/]+\/editor$/,
    `/${segment}/${encodeURIComponent(workflowId)}/editor`
  )}${window.location.search}`;
}

export function WorkflowEditorShell({
  api,
  locale,
  workflowId,
  onBack,
  backPath,
  mode = "workflow"
}: WorkflowEditorShellProps) {
  const copy = getWorkflowModuleCopy(locale);
  const apiClient = api.apiClient;
  const [loading, setLoading] = useState(true);
  const [detail, setDetail] = useState<WorkflowDetailResponse | null>(null);
  const [versions, setVersions] = useState<WorkflowVersionItem[]>([]);
  const [nodeTypes, setNodeTypes] = useState<NodeTypeMetadata[]>([]);
  const [workflowItems, setWorkflowItems] = useState<WorkflowListItem[]>([]);
  const [processSnapshot, setProcessSnapshot] = useState<WorkflowProcessSnapshot | null>(null);
  const [sidebarTab, setSidebarTab] = useState<SidebarTabKey>("resources");
  const [workspaceView, setWorkspaceView] = useState<WorkspaceViewKey>("logic");
  const [sidebarKeyword, setSidebarKeyword] = useState("");
  const [panelCommand, setPanelCommand] = useState<WorkflowPanelCommand | undefined>(undefined);
  const [commandNonce, setCommandNonce] = useState(0);

  const loadContext = useCallback(async (keyword = "") => {
    setLoading(true);
    try {
      const [detailResponse, versionsResponse, nodeTypesResponse, workflowResult] = await Promise.all([
        apiClient.getDetail?.(workflowId),
        apiClient.getVersions?.(workflowId),
        apiClient.getNodeTypes?.(),
        api.listWorkflows({ pageIndex: 1, pageSize: 20, keyword, mode, status: "all" })
      ]);

      setDetail(detailResponse?.data ?? null);
      setVersions(versionsResponse?.data ?? []);
      setNodeTypes(nodeTypesResponse?.data ?? []);
      setWorkflowItems(workflowResult.items);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : copy.loadFailure);
    } finally {
      setLoading(false);
    }
  }, [api, apiClient, copy.loadFailure, mode, workflowId]);

  useEffect(() => {
    void loadContext();
  }, [loadContext]);

  useEffect(() => {
    const handle = window.setTimeout(() => {
      void loadContext(sidebarKeyword);
    }, 280);

    return () => {
      window.clearTimeout(handle);
    };
  }, [loadContext, sidebarKeyword]);

  const canvas = useMemo(() => safeParseCanvas(detail?.canvasJson), [detail?.canvasJson]);
  const sidebarResourceSections = useMemo(
    () =>
      buildResourceSidebarSections({
        copy,
        mode,
        currentWorkflowId: workflowId,
        workflowItems,
        keyword: sidebarKeyword
      }),
    [copy, mode, sidebarKeyword, workflowId, workflowItems]
  );
  const sidebarReferenceSections = useMemo(
    () =>
      buildReferenceSidebarSections({
        copy,
        detail,
        versions,
        nodes: canvas.nodes,
        nodeTypes
      }),
    [canvas.nodes, copy, detail, nodeTypes, versions]
  );

  const currentSections = sidebarTab === "resources" ? sidebarResourceSections : sidebarReferenceSections;
  const panelCommandLabel = useMemo(() => {
    switch (panelCommand?.type) {
      case "openVariables":
        return copy.variablesLabel;
      case "openTrace":
        return copy.traceLabel;
      case "openTestRun":
        return copy.testRunLabel;
      case "openProblems":
        return copy.problemsLabel;
      case "openNodePanel":
        return copy.addNodeLabel;
      case "openDebug":
        return copy.debugLabel;
      default:
        return "-";
    }
  }, [copy.addNodeLabel, copy.debugLabel, copy.problemsLabel, copy.testRunLabel, copy.traceLabel, copy.variablesLabel, panelCommand?.type]);

  async function refreshExecution(executionId: string) {
    const response = await (apiClient.getProcess?.(executionId) as Promise<{ data?: WorkflowProcessResponse }> | undefined);
    const process = response?.data;
    setProcessSnapshot(
      process
        ? {
            status: process.status,
            startedAt: process.startedAt,
            completedAt: process.completedAt,
            errorMessage: process.errorMessage,
            nodeExecutions: process.nodeExecutions ?? []
          }
        : null
    );
  }

  function emitPanelCommand(type: WorkflowPanelCommandType) {
    const nextNonce = commandNonce + 1;
    setCommandNonce(nextNonce);
    setPanelCommand({ type, nonce: nextNonce });
  }

  function handleSidebarItemClick(item: WorkflowSidebarItem) {
    if (item.disabled) {
      return;
    }

    if (item.action.type === "workflow") {
      if (item.action.workflowId === workflowId) {
        return;
      }

      window.location.assign(buildEditorPath(item.action.workflowId, mode));
      return;
    }

    if (item.action.type === "command") {
      emitPanelCommand(item.action.command);
    }
  }

  function handleBack() {
    if (backPath) {
      window.location.assign(backPath);
      return;
    }

    onBack();
  }

  async function handleQuickTestRun() {
    if (!apiClient.runSync) {
      return;
    }

    try {
      const response = await apiClient.runSync(workflowId, { source: "draft", inputsJson: "{}" });
      const executionId = response.data?.executionId;
      if (!executionId) {
        return;
      }
      await refreshExecution(executionId);
      emitPanelCommand("openTestRun");
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : copy.testRunLabel);
    }
  }

  function renderSidebarSections() {
    if (currentSections.length === 0) {
      return <Empty image={null} title={sidebarTab === "resources" ? copy.resourcesTitle : copy.referencesTitle} />;
    }

    return currentSections.map(section => (
      <section key={section.key} className="module-workflow__coze-section">
        <div className="module-workflow__coze-section-head">
          <span>{section.title}</span>
          {section.key === "workflow" ? <button type="button" onClick={() => emitPanelCommand("openNodePanel")}>+</button> : null}
        </div>
        <div className="module-workflow__coze-section-items">
          {section.items.map(item => (
            <button
              key={item.key}
              type="button"
              className={`module-workflow__coze-item${item.active ? " is-active" : ""}${item.disabled ? " is-disabled" : ""}`}
              onClick={() => handleSidebarItemClick(item)}
            >
              <span className="module-workflow__coze-item-main">
                <strong>{item.label}</strong>
                {item.hint ? <small>{item.hint}</small> : null}
              </span>
              {item.badge ? <span className="module-workflow__coze-item-badge">{item.badge}</span> : null}
            </button>
          ))}
          {section.items.length === 0 && section.emptyText ? (
            <div className="module-workflow__coze-empty">{section.emptyText}</div>
          ) : null}
        </div>
      </section>
    ));
  }

  return (
    <section className="module-workflow__coze-editor" data-testid={mode === "chatflow" ? "app-chatflow-editor-shell" : "app-workflow-editor-shell"}>
      <div className="module-workflow__coze-workspace-tabs">
        <button
          type="button"
          className={`module-workflow__coze-workspace-tab${workspaceView === "logic" ? " is-active" : ""}`}
          onClick={() => setWorkspaceView("logic")}
        >
          {copy.editorTabLogic}
        </button>
        <button
          type="button"
          className={`module-workflow__coze-workspace-tab${workspaceView === "ui" ? " is-active" : ""}`}
          onClick={() => {
            setWorkspaceView("ui");
            Toast.info(copy.editorUiComingSoon);
          }}
        >
          {copy.editorTabUi}
        </button>
      </div>

      <div className="module-workflow__coze-layout">
        <aside className="module-workflow__coze-sidebar">
          <div className="module-workflow__coze-sidebar-top">
            <div className="module-workflow__coze-sidebar-tabs">
              <button
                type="button"
                className={sidebarTab === "resources" ? "is-active" : ""}
                onClick={() => setSidebarTab("resources")}
              >
                {copy.resourcesTab}
              </button>
              <button
                type="button"
                className={sidebarTab === "references" ? "is-active" : ""}
                onClick={() => setSidebarTab("references")}
              >
                {copy.referencesTab}
              </button>
            </div>
            {sidebarTab === "resources" ? (
              <Input
                value={sidebarKeyword}
                onChange={setSidebarKeyword}
                placeholder={copy.sidebarSearchPlaceholder}
                showClear
              />
            ) : null}
          </div>
          <div className="module-workflow__coze-sidebar-body">
            {loading ? (
              <div className="module-workflow__coze-loading">
                <Spin />
              </div>
            ) : (
              renderSidebarSections()
            )}
          </div>
        </aside>

        <div className="module-workflow__coze-workspace">
          <div className="module-workflow__coze-workspace-header">
            <div className="module-workflow__coze-workspace-chip">
              <span className="module-workflow__coze-workspace-dot" />
              <strong>{detail?.name ?? workflowId}</strong>
            </div>
            <div className="module-workflow__coze-workspace-actions">
              <Button theme="borderless" onClick={() => void loadContext(sidebarKeyword)}>
                {copy.refreshCanvasLabel}
              </Button>
              <Button theme="borderless" onClick={() => emitPanelCommand("openNodePanel")}>
                {copy.addNodeLabel}
              </Button>
              <Button theme="borderless" onClick={() => emitPanelCommand("openTrace")}>
                {copy.traceLabel}
              </Button>
              <Button theme="light" type="tertiary" onClick={() => emitPanelCommand("openDebug")}>
                {copy.debugLabel}
              </Button>
              <Button theme="solid" type="secondary" onClick={() => void handleQuickTestRun()}>
                {copy.testRunLabel}
              </Button>
            </div>
          </div>

          <div className="module-workflow__coze-editor-surface">
            {workspaceView === "ui" ? (
              <div className="module-workflow__coze-ui-placeholder">
                <Typography.Title heading={5} style={{ margin: 0 }}>
                  {copy.openUiPreviewLabel}
                </Typography.Title>
                <Typography.Text type="tertiary">{copy.editorUiComingSoon}</Typography.Text>
              </div>
            ) : (
              <WorkflowEditor
                workflowId={workflowId}
                apiClient={apiClient}
                locale={locale}
                mode={mode}
                panelCommand={panelCommand}
                onBack={handleBack}
              />
            )}
          </div>

          <div className="module-workflow__coze-footer-strip">
            <div>
              <strong>{copy.currentWorkflowLabel}</strong>
              <span>{detail?.name ?? workflowId}</span>
            </div>
            <div>
              <strong>{copy.relatedVersionsLabel}</strong>
              <span>{versions.length}</span>
            </div>
            <div>
              <strong>{copy.traceLabel}</strong>
              <span>{panelCommandLabel}</span>
            </div>
            <div>
              <strong>{copy.testRunLabel}</strong>
              <span>{resolveExecutionStatus(locale, processSnapshot?.status)}</span>
            </div>
            <div>
              <strong>{copy.problemsLabel}</strong>
              <span>{canvas.nodes.length}</span>
            </div>
            <div>
              <strong>{copy.versionLabel}</strong>
              <span>v{detail?.latestVersionNumber ?? 0}</span>
            </div>
            <div>
              <strong>{copy.publishedAtLabel}</strong>
              <span>{formatDateTime(detail?.publishedAt, locale)}</span>
            </div>
            <div>
              <strong>{copy.updatedAtLabel}</strong>
              <span>{formatDateTime(detail?.updatedAt, locale)}</span>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
