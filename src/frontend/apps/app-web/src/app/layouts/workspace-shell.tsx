import { useEffect, useMemo, useState } from "react";
import { Navigate, Outlet, useLocation, useNavigate, useParams } from "react-router-dom";
import { IconPlus } from "@douyinfe/semi-icons";
import type { CozeNavSection } from "@atlas/coze-shell-react";
import { CozeShell } from "@atlas/coze-shell-react";
import { getTenantId } from "@atlas/shared-react-core/utils";
import {
  meProfilePath,
  selectWorkspacePath,
  signPath,
  workspaceHomePath
} from "@atlas/app-shell-shared";
import { useAuth } from "../auth-context";
import { useBootstrap } from "../bootstrap-context";
import { useAppI18n } from "../i18n";
import {
  WorkspaceProvider,
  useOptionalWorkspaceContext,
  useWorkspaceContext
} from "../workspace-context";
import { OrganizationProvider } from "../organization-context";
import { PermissionProvider } from "../permission-context";
import { MENU_GROUPS } from "../menu-config";
import { WorkspaceSwitcher } from "../components/workspace-switcher";
import { GlobalCreateModal } from "../components/global-create-modal";
import { PageShell } from "../_shared";
import type { AppMessageKey } from "../messages";

const LAST_WORKSPACE_KEY = "atlas_last_workspace_id";

function navGlyph(label: string) {
  return <span className="app-nav-glyph" aria-hidden="true">{label}</span>;
}

function LoadingPage() {
  return <PageShell loading />;
}

export function rememberLastWorkspaceId(workspaceId: string): void {
  if (typeof window === "undefined") {
    return;
  }
  try {
    window.localStorage.setItem(LAST_WORKSPACE_KEY, workspaceId);
  } catch {
    // ignore
  }
}

export function readLastWorkspaceId(): string | null {
  if (typeof window === "undefined") {
    return null;
  }
  try {
    return window.localStorage.getItem(LAST_WORKSPACE_KEY);
  } catch {
    return null;
  }
}

/**
 * 新 PRD 风格工作空间路由壳子 (`/workspace/:workspaceId/*`)。
 *
 * 加载顺序：BootstrapContext → AuthContext → WorkspaceContext → PermissionContext → 渲染。
 */
export function WorkspaceShellLayout() {
  const { workspaceId } = useParams<{ workspaceId: string }>();
  const auth = useAuth();
  const bootstrap = useBootstrap();
  const location = useLocation();

  if (bootstrap.loading || auth.loading) {
    return <LoadingPage />;
  }

  if (!bootstrap.platformReady) {
    return <Navigate to="/platform-not-ready" replace />;
  }

  if (!auth.isAuthenticated) {
    return <Navigate to={signPath(location.pathname + location.search)} replace />;
  }

  if (!workspaceId) {
    return <Navigate to={selectWorkspacePath()} replace />;
  }

  const tenantId = getTenantId() ?? "";
  return (
    <OrganizationProvider orgId={tenantId}>
      <WorkspaceProvider workspaceId={workspaceId}>
        <PermissionProvider>
          <RememberWorkspace />
          <ShellChrome variant="workspace" />
        </PermissionProvider>
      </WorkspaceProvider>
    </OrganizationProvider>
  );
}

/**
 * 平台域/个人域路由壳子（`/market/*`、`/community/*`、`/open/*`、`/docs`、`/platform/general`、`/me/*`）。
 *
 * - 不强制 URL 中存在 workspaceId
 * - 从 localStorage 的“上次访问的工作空间”读取，无则跳 `/select-workspace`
 * - 工作空间域 6 项菜单仍渲染并指向最近访问空间
 */
export function PlatformShellLayout() {
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
          <ShellChrome variant="platform" />
        </PermissionProvider>
      </WorkspaceProvider>
    </OrganizationProvider>
  );
}

function RememberWorkspace() {
  const workspace = useOptionalWorkspaceContext();
  useEffect(() => {
    if (workspace?.id) {
      rememberLastWorkspaceId(workspace.id);
    }
  }, [workspace?.id]);
  return null;
}

interface ShellChromeProps {
  variant: "workspace" | "platform";
}

function ShellChrome({ variant }: ShellChromeProps) {
  const { t, locale, setLocale } = useAppI18n();
  const auth = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const workspace = useWorkspaceContext();
  const [createOpen, setCreateOpen] = useState(false);

  useEffect(() => {
    if (auth.isAuthenticated && !auth.profile && !auth.loading) {
      void auth.ensureProfile();
    }
  }, [auth]);

  const navSections = useMemo<CozeNavSection[]>(() => {
    return MENU_GROUPS.map(group => ({
      key: group.key,
      title: t(group.titleKey),
      items: group.items.map(item => ({
        key: item.key,
        label: t(item.labelKey),
        icon: navGlyph(item.iconGlyph),
        path: item.buildPath(workspace.id),
        testId: `app-sidebar-item-${item.testIdSuffix}`
      }))
    }));
  }, [workspace.id, t]);

  const headerTitle = useMemo(() => resolveHeaderTitle(location.pathname, t), [location.pathname, t]);
  const workspaceLabel = workspace.name || workspace.appKey || t("cozeShellWorkspaceSwitcherTitle");
  const activePath = `${location.pathname}${location.search}`;

  if (variant === "workspace" && workspace.loading) {
    return <LoadingPage />;
  }

  return (
    <>
      <CozeShell
        appKey={workspace.appKey}
        backPath={workspaceHomePath(workspace.id || "")}
        workspaceLabel={workspaceLabel}
        activePath={activePath}
        navSections={navSections}
        headerTitle={headerTitle}
        headerSubtitle={workspaceLabel}
        localeLabel={t(locale === "zh-CN" ? "switchToEnglish" : "switchToChinese")}
        userName={auth.profile?.displayName || auth.profile?.username || "Atlas"}
        profileLabel={t("cozeShellAvatarMenuProfile")}
        logoutLabel={t("cozeShellAvatarMenuLogout")}
        extraActions={[
          {
            key: "create",
            label: t("cozeShellCreateButton"),
            icon: <IconPlus />,
            onClick: () => setCreateOpen(true),
            testId: "coze-shell-create-button"
          }
        ]}
        onNavigate={path => navigate(path)}
        onToggleLocale={() => setLocale(locale === "zh-CN" ? "en-US" : "zh-CN")}
        onOpenProfile={() => navigate(meProfilePath())}
        onLogout={() => {
          void auth.logout().then(() => navigate(signPath(), { replace: true }));
        }}
      >
        <div className="coze-workspace-shell-toolbar" data-testid="coze-workspace-shell-toolbar">
          <WorkspaceSwitcher workspaceId={workspace.id} workspaceLabel={workspaceLabel} />
        </div>
        <Outlet />
      </CozeShell>

      <GlobalCreateModal
        visible={createOpen}
        workspaceId={workspace.id}
        onClose={() => setCreateOpen(false)}
      />
    </>
  );
}

function resolveHeaderTitle(pathname: string, t: (key: AppMessageKey) => string): string {
  if (pathname.startsWith("/me/profile")) {
    return t("cozeMeProfileTitle");
  }
  if (pathname.startsWith("/me/settings")) {
    return t("cozeMeSettingsTitle");
  }
  if (pathname.startsWith("/market/templates")) {
    return t("cozeMenuTemplates");
  }
  if (pathname.startsWith("/market/plugins")) {
    return t("cozeMenuPlugins");
  }
  if (pathname.startsWith("/community")) {
    return t("cozeMenuCommunity");
  }
  if (pathname.startsWith("/open")) {
    return t("cozeMenuOpenApi");
  }
  if (pathname.startsWith("/docs")) {
    return t("cozeMenuDocs");
  }
  if (pathname.startsWith("/platform")) {
    return t("cozeMenuPlatform");
  }
  if (pathname.includes("/home")) {
    return t("cozeMenuHome");
  }
  if (pathname.includes("/projects")) {
    return t("cozeMenuProjects");
  }
  if (pathname.includes("/resources")) {
    return t("cozeMenuResources");
  }
  if (pathname.includes("/tasks")) {
    return t("cozeMenuTasks");
  }
  if (pathname.includes("/evaluations")) {
    return t("cozeMenuEvaluations");
  }
  if (pathname.includes("/settings/models")) {
    return t("cozeSettingsModelsTitle");
  }
  if (pathname.includes("/settings")) {
    return t("cozeMenuSettings");
  }
  return t("cozeMenuHome");
}
