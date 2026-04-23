import { useEffect, useMemo, useState } from "react";
import { Navigate, Outlet, useLocation, useNavigate } from "react-router-dom";
import { Button } from "@douyinfe/semi-ui";
import { IconChevronLeft } from "@douyinfe/semi-icons";
import { getTenantId } from "@atlas/shared-react-core/utils";
import {
  selectWorkspacePath,
  signPath,
  workspaceProjectsPath
} from "@atlas/app-shell-shared";
import { useAuth } from "../auth-context";
import { useBootstrap } from "../bootstrap-context";
import { OrganizationProvider } from "../organization-context";
import { PermissionProvider } from "../permission-context";
import { WorkspaceProvider, useWorkspaceContext } from "../workspace-context";
import { useAppI18n } from "../i18n";
import {
  EditorWorkspaceResolutionError,
  type EditorResourceType,
  resolveEditorWorkspace
} from "../../services/api-editor-context";
import { rememberLastWorkspaceId } from "./workspace-shell";
import { PageShell } from "../_shared";

function LoadingPage() {
  return <PageShell loading />;
}

interface EditorRouteResource {
  resourceType: EditorResourceType;
  resourceId: string;
}

interface EditorWorkspaceState {
  status: "loading" | "resolved" | "error";
  workspaceId?: string;
  errorCode?: string;
}

export function resolveEditorRouteResource(pathname: string): EditorRouteResource | null {
  const normalizedPath = pathname.trim();
  const candidates: Array<{ regex: RegExp; resourceType: EditorResourceType }> = [
    { regex: /^\/apps\/lowcode\/([^/]+)\/studio$/i, resourceType: "app" },
    { regex: /^\/app\/([^/]+)\/(?:editor|publish)$/i, resourceType: "app" },
    { regex: /^\/workflow\/([^/]+)\/editor$/i, resourceType: "workflow" },
    { regex: /^\/chatflow\/([^/]+)\/editor$/i, resourceType: "workflow" },
    { regex: /^\/agent\/([^/]+)\/(?:editor|publish)$/i, resourceType: "agent" }
  ];

  for (const candidate of candidates) {
    const matched = normalizedPath.match(candidate.regex);
    const resourceId = matched?.[1]?.trim();
    if (resourceId) {
      return {
        resourceType: candidate.resourceType,
        resourceId: decodeURIComponent(resourceId)
      };
    }
  }

  return null;
}

/**
 * 编辑器全屏 Layout（智能体 / 应用 / 工作流 / 对话流编辑器）。
 *
 * - 不渲染左侧 12 项菜单（编辑器是沉浸式工作页）
 * - 顶部条只有“返回项目开发 + 当前工作空间标签”
 * - 仍然挂 OrganizationProvider + WorkspaceProvider + PermissionProvider，
 *   因为内核组件（BotIdePage / CozeWorkflowPage / Lowcode 跳转壳）依赖这些上下文。
 */
export function EditorShellLayout() {
  const auth = useAuth();
  const bootstrap = useBootstrap();
  const location = useLocation();
  const tenantId = getTenantId() ?? "";
  const targetResource = useMemo(
    () => resolveEditorRouteResource(location.pathname),
    [location.pathname]
  );
  const [retryVersion, setRetryVersion] = useState(0);
  const [workspaceState, setWorkspaceState] = useState<EditorWorkspaceState>({
    status: "loading"
  });

  if (bootstrap.loading || auth.loading) {
    return <LoadingPage />;
  }

  if (!bootstrap.platformReady) {
    return <Navigate to="/platform-not-ready" replace />;
  }

  if (!bootstrap.appReady) {
    return <Navigate to="/app-setup" replace />;
  }

  if (!auth.isAuthenticated) {
    return <Navigate to={signPath(location.pathname + location.search)} replace />;
  }

  if (!targetResource) {
    return (
      <EditorWorkspaceFailure
        errorCode="EDITOR_CONTEXT_UNSUPPORTED_ROUTE"
        onRetry={() => setRetryVersion((value) => value + 1)}
      />
    );
  }

  return (
    <ResolvedEditorWorkspace
      key={`${targetResource.resourceType}:${targetResource.resourceId}:${retryVersion}`}
      tenantId={tenantId}
      targetResource={targetResource}
      workspaceState={workspaceState}
      setWorkspaceState={setWorkspaceState}
      onRetry={() => setRetryVersion((value) => value + 1)}
    />
  );
}

function ResolvedEditorWorkspace({
  tenantId,
  targetResource,
  workspaceState,
  setWorkspaceState,
  onRetry
}: {
  tenantId: string;
  targetResource: EditorRouteResource;
  workspaceState: EditorWorkspaceState;
  setWorkspaceState: (state: EditorWorkspaceState) => void;
  onRetry: () => void;
}) {
  useEffect(() => {
    let active = true;
    setWorkspaceState({ status: "loading" });

    void resolveEditorWorkspace(targetResource.resourceType, targetResource.resourceId)
      .then((resolved) => {
        if (!active) {
          return;
        }
        rememberLastWorkspaceId(resolved.workspaceId);
        setWorkspaceState({
          status: "resolved",
          workspaceId: resolved.workspaceId
        });
      })
      .catch((error: unknown) => {
        if (!active) {
          return;
        }
        const errorCode = error instanceof EditorWorkspaceResolutionError
          ? error.code
          : "EDITOR_CONTEXT_RESOLVE_FAILED";
        setWorkspaceState({
          status: "error",
          errorCode
        });
      });

    return () => {
      active = false;
    };
  }, [setWorkspaceState, targetResource.resourceId, targetResource.resourceType]);

  if (workspaceState.status === "loading") {
    return <LoadingPage />;
  }

  if (workspaceState.status !== "resolved" || !workspaceState.workspaceId) {
    return <EditorWorkspaceFailure errorCode={workspaceState.errorCode} onRetry={onRetry} />;
  }

  return (
    <OrganizationProvider orgId={tenantId}>
      <WorkspaceProvider workspaceId={workspaceState.workspaceId}>
        <PermissionProvider>
          <EditorChrome />
        </PermissionProvider>
      </WorkspaceProvider>
    </OrganizationProvider>
  );
}

function EditorWorkspaceFailure({
  errorCode,
  onRetry
}: {
  errorCode?: string;
  onRetry: () => void;
}) {
  const { t } = useAppI18n();
  const navigate = useNavigate();

  const descriptionKey = errorCode === "EDITOR_CONTEXT_WORKSPACE_FORBIDDEN"
    ? "editorWorkspaceForbiddenDesc"
    : errorCode === "EDITOR_CONTEXT_WORKSPACE_UNRESOLVED"
      ? "editorWorkspaceUnresolvedDesc"
      : errorCode === "EDITOR_CONTEXT_UNSUPPORTED_ROUTE"
        ? "editorWorkspaceUnsupportedDesc"
        : "editorWorkspaceGenericDesc";

  return (
    <div
      data-testid="coze-editor-shell-error"
      data-error-code={errorCode ?? "EDITOR_CONTEXT_RESOLVE_FAILED"}
      style={{
        minHeight: "100vh",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        padding: 32
      }}
    >
      <div
        style={{
          width: "min(560px, 100%)",
          borderRadius: 16,
          background: "var(--semi-color-bg-2, #ffffff)",
          border: "1px solid var(--semi-color-border, rgba(15,23,42,0.08))",
          boxShadow: "0 8px 24px rgba(15,23,42,0.06)",
          padding: 24
        }}
      >
        <h1 style={{ margin: 0, fontSize: 20 }}>{t("editorWorkspaceErrorTitle")}</h1>
        <p style={{ margin: "12px 0 0", color: "var(--semi-color-text-2, #64748b)", lineHeight: 1.6 }}>
          {t(descriptionKey)}
        </p>
        <div style={{ display: "flex", justifyContent: "flex-end", gap: 12, marginTop: 20 }}>
          <Button theme="light" onClick={() => navigate(selectWorkspacePath())}>
            {t("editorWorkspaceGoSelect")}
          </Button>
          <Button type="primary" onClick={onRetry}>
            {t("editorWorkspaceRetry")}
          </Button>
        </div>
      </div>
    </div>
  );
}

function EditorChrome() {
  const { t } = useAppI18n();
  const navigate = useNavigate();
  const workspace = useWorkspaceContext();
  const workspaceLabel = workspace.name || workspace.appKey || "Workspace";

  if (workspace.loading) {
    return <LoadingPage />;
  }

  return (
    <div className="coze-editor-shell" data-testid="coze-editor-shell">
      <header className="coze-editor-shell__header">
        <Button
          theme="borderless"
          icon={<IconChevronLeft />}
          onClick={() => navigate(workspaceProjectsPath(workspace.id))}
          data-testid="coze-editor-shell-back"
        >
          {t("cozeCommonGoBack")}
        </Button>
        <span className="coze-editor-shell__workspace">{workspaceLabel}</span>
      </header>
      <main className="coze-editor-shell__main">
        <Outlet />
      </main>
    </div>
  );
}
