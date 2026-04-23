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
import { readLastWorkspaceId } from "./workspace-shell";
import { PageShell } from "../_shared";

function LoadingPage() {
  return <PageShell loading />;
}

/**
 * 编辑器全屏 Layout（智能体 / 应用 / 工作流 / 对话流编辑器）。
 *
 * - 不渲染左侧 12 项菜单（编辑器是沉浸式工作页）
 * - 顶部条只有“返回项目开发 + 当前工作空间标签”
 * - 仍然挂 OrganizationProvider + WorkspaceProvider + PermissionProvider，
 *   因为内核组件（BotIdePage / CozeWorkflowPage / Lowcode 跳转壳）依赖这些上下文。
 *
 * workspaceId 来源：localStorage `atlas_last_workspace_id`，无则跳 `/console`。
 * 第三阶段后端补齐"按对象 ID 反查 workspaceId"接口后，再做 URL → workspace 校正。
 */
export function EditorShellLayout() {
  const auth = useAuth();
  const bootstrap = useBootstrap();
  const location = useLocation();
  const lastWorkspaceId = readLastWorkspaceId();
  const tenantId = getTenantId() ?? "";

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

  if (!lastWorkspaceId) {
    return <Navigate to={selectWorkspacePath()} replace />;
  }

  return (
    <OrganizationProvider orgId={tenantId}>
      <WorkspaceProvider workspaceId={lastWorkspaceId}>
        <PermissionProvider>
          <EditorChrome />
        </PermissionProvider>
      </WorkspaceProvider>
    </OrganizationProvider>
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
