import { useEffect, useMemo, useState } from "react";
import { Navigate, Outlet, useLocation, useNavigate, useParams } from "react-router-dom";
import {
  IconHome,
  IconAppCenter,
  IconFolder,
  IconTickCircle,
  IconHistogram,
  IconSetting,
  IconGridRectangle,
  IconBox,
  IconGlobe,
  IconLink,
  IconFile,
  IconPlus
} from "@douyinfe/semi-icons";
import type { CozeNavSection } from "@atlas/coze-shell-react";
import { CozeShell } from "@atlas/coze-shell-react";
import { getTenantId } from "@atlas/shared-react-core/utils";
import {
  buildWorkspaceSwitchPath,
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

function getNavIcon(key: string, glyph: string) {
  switch (key) {
    case "home": return <IconHome />;
    case "projects": return <IconAppCenter />;
    case "resources": return <IconFolder />;
    case "tasks": return <IconTickCircle />;
    case "evaluations": return <IconHistogram />;
    case "settings": return <IconSetting />;
    case "templates": return <IconGridRectangle />;
    case "plugins": return <IconBox />;
    case "community": return <IconGlobe />;
    case "open-api": return <IconLink />;
    case "docs": return <IconFile />;
    default: return <span className="app-nav-glyph" aria-hidden="true">{glyph}</span>;
  }
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
          <ShellChrome />
        </PermissionProvider>
      </WorkspaceProvider>
    </OrganizationProvider>
  );
}

/**
 * 非工作空间路由壳子（`/market/*`、`/community/*`、`/open/*`、`/docs`、`/me/*`）。
 *
 * - 不强制 URL 中存在 workspaceId
 * - 从 localStorage 的”上次访问的工作空间”读取，无则跳 `/select-workspace`
 */
export function PlatformShellLayout() {
  const auth = useAuth();
  const bootstrap = useBootstrap();
  const location = useLocation();
  const selectedWorkspaceId = readLastWorkspaceId();
  const tenantId = getTenantId() ?? "";

  if (bootstrap.loading || auth.loading) {
    return <LoadingPage />;
  }

  if (!auth.isAuthenticated) {
    return <Navigate to={signPath(location.pathname + location.search)} replace />;
  }

  if (!selectedWorkspaceId) {
    return <Navigate to={selectWorkspacePath()} replace />;
  }

  return (
    <OrganizationProvider orgId={tenantId}>
      <WorkspaceProvider workspaceId={selectedWorkspaceId}>
        <PermissionProvider>
          <RememberWorkspace />
          <ShellChrome />
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

function ShellChrome() {
  const { t, locale, setLocale } = useAppI18n();
  const auth = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const workspace = useWorkspaceContext();
  const [createOpen, setCreateOpen] = useState(false);

  const handleOpenCreate = () => setCreateOpen(true);

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
        icon: getNavIcon(item.key, item.iconGlyph),
        path: item.buildPath(workspace.id),
        testId: `app-sidebar-item-${item.testIdSuffix}`
      }))
    }));
  }, [workspace.id, t]);

  const headerTitle = useMemo(() => resolveHeaderTitle(location.pathname, t), [location.pathname, t]);
  const workspaceLabel = workspace.name || workspace.appKey || t("cozeShellWorkspaceSwitcherTitle");
  const activePath = `${location.pathname}${location.search}`;
  const handleSelectWorkspace = (targetWorkspaceId: string) => {
    if (!targetWorkspaceId || targetWorkspaceId === workspace.id) {
      return;
    }

    rememberLastWorkspaceId(targetWorkspaceId);
    navigate(buildWorkspaceSwitchPath(activePath, targetWorkspaceId));
  };

  if (workspace.loading) {
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
          onClick: handleOpenCreate,
          testId: "coze-shell-create-button"
        }
      ]}
      sidebarTop={(
        <WorkspaceSwitcher
          workspaceId={workspace.id}
          workspaceLabel={workspaceLabel}
          onSelectWorkspace={handleSelectWorkspace}
        />
      )}
      onNavigate={path => navigate(path)}
      onToggleLocale={() => setLocale(locale === "zh-CN" ? "en-US" : "zh-CN")}
      onOpenProfile={() => navigate(meProfilePath())}
      onLogout={() => {
        void auth.logout().then(() => navigate(signPath(), { replace: true }));
      }}
    >
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
