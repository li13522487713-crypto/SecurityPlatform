import { useEffect, useMemo, useState, type ReactNode } from "react";
import { Navigate, Outlet, useLocation, useNavigate, useParams } from "react-router-dom";
import { Avatar, Button, Divider, Space, Typography } from "@coze-arch/coze-design";
import { WorkspaceSubMenu, type IWorkspaceListItem } from "@coze-foundation/space-ui-base";
import { useInitSpace } from "@coze-foundation/space-ui-adapter";
import { getTenantId } from "@atlas/shared-react-core/utils";
import {
  communityWorksPath,
  docsPath,
  marketPluginsPath,
  marketTemplatesPath,
  meProfilePath,
  openApiPath,
  selectWorkspacePath,
  signPath,
  workspaceEvaluationsPath,
  workspaceHomePath,
  workspaceRootPath,
  workspaceSettingsPublishPath,
  workspaceTasksPath
} from "@atlas/app-shell-shared";
import { I18nProvider } from "../../../../../packages/arch/i18n/src/i18n-provider";
import { I18n, initI18nInstance } from "../../../../../packages/arch/i18n/src/raw";
import { toCozeLocale } from "../workflow-runtime-boundary";
import { useAuth } from "../auth-context";
import { useBootstrap } from "../bootstrap-context";
import { useAppI18n } from "../i18n";
import { OrganizationProvider } from "../organization-context";
import { PermissionProvider } from "../permission-context";
import {
  WorkspaceProvider,
  useOptionalWorkspaceContext,
  useWorkspaceContext
} from "../workspace-context";
import { WorkspaceSwitcher } from "../components/workspace-switcher";
import { PageShell } from "../_shared";
import type { AppMessageKey } from "../messages";

const LAST_WORKSPACE_KEY = "atlas_last_workspace_id";

interface ShellLink {
  key: string;
  label: string;
  path: string;
  testId: string;
  activeMatchers?: string[];
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

export function RememberWorkspace() {
  const workspace = useOptionalWorkspaceContext();

  useEffect(() => {
    if (workspace?.id) {
      rememberLastWorkspaceId(workspace.id);
    }
  }, [workspace?.id]);

  return null;
}

function isActivePath(pathnameWithSearch: string, item: ShellLink): boolean {
  if (pathnameWithSearch === item.path || pathnameWithSearch.startsWith(`${item.path}/`)) {
    return true;
  }

  return (item.activeMatchers ?? []).some((matcher) =>
    pathnameWithSearch === matcher || pathnameWithSearch.startsWith(`${matcher}/`)
  );
}

function buildSpaceSubMenuItems(t: (key: AppMessageKey) => string): IWorkspaceListItem[] {
  return [
    {
      title: () => t("cozeMenuProjects"),
      path: "develop",
      dataTestId: "app-sidebar-item-projects"
    },
    {
      title: () => t("cozeMenuResources"),
      path: "library",
      dataTestId: "app-sidebar-item-resources"
    }
  ];
}

function deriveSpaceSubMenu(pathname: string, workspaceId: string): string | undefined {
  const rootPath = `${workspaceRootPath(workspaceId)}/`;
  if (!pathname.startsWith(rootPath)) {
    return undefined;
  }

  const suffix = pathname.slice(rootPath.length);
  if (
    suffix.startsWith("library") ||
    suffix.startsWith("resources") ||
    suffix.startsWith("plugin/") ||
    suffix.startsWith("knowledge/") ||
    suffix.startsWith("database/") ||
    suffix.startsWith("knowledge-bases") ||
    suffix.startsWith("databases")
  ) {
    return "library";
  }

  if (
    suffix.startsWith("develop") ||
    suffix.startsWith("bot/") ||
    suffix.startsWith("publish/agent/") ||
    suffix.startsWith("project-ide/") ||
    suffix.startsWith("apps/") ||
    suffix.startsWith("agents/") ||
    suffix.startsWith("workflows") ||
    suffix.startsWith("chatflows")
  ) {
    return "develop";
  }

  return undefined;
}

function buildSpaceLinks(workspaceId: string, t: (key: AppMessageKey) => string): ShellLink[] {
  return [
    {
      key: "home",
      label: t("cozeMenuHome"),
      path: workspaceHomePath(workspaceId),
      testId: "app-sidebar-item-home"
    },
    {
      key: "tasks",
      label: t("cozeMenuTasks"),
      path: workspaceTasksPath(workspaceId),
      testId: "app-sidebar-item-tasks"
    },
    {
      key: "evaluations",
      label: t("cozeMenuEvaluations"),
      path: workspaceEvaluationsPath(workspaceId),
      testId: "app-sidebar-item-evaluations"
    },
    {
      key: "settings",
      label: t("cozeMenuSettings"),
      path: workspaceSettingsPublishPath(workspaceId),
      testId: "app-sidebar-item-settings",
      activeMatchers: [`${workspaceRootPath(workspaceId)}/settings`]
    }
  ];
}

function buildPlatformLinks(t: (key: AppMessageKey) => string): ShellLink[] {
  return [
    {
      key: "templates",
      label: t("cozeMenuTemplates"),
      path: marketTemplatesPath(),
      testId: "app-sidebar-item-templates"
    },
    {
      key: "plugins",
      label: t("cozeMenuPlugins"),
      path: marketPluginsPath(),
      testId: "app-sidebar-item-plugins"
    },
    {
      key: "community",
      label: t("cozeMenuCommunity"),
      path: communityWorksPath(),
      testId: "app-sidebar-item-community"
    },
    {
      key: "open-api",
      label: t("cozeMenuOpenApi"),
      path: openApiPath(),
      testId: "app-sidebar-item-open-api"
    },
    {
      key: "docs",
      label: t("cozeMenuDocs"),
      path: docsPath(),
      testId: "app-sidebar-item-docs"
    },
    {
      key: "me",
      label: t("cozeMeProfileTitle"),
      path: meProfilePath(),
      testId: "app-sidebar-item-profile",
      activeMatchers: ["/me"]
    }
  ];
}

function resolveShellHeaderTitle(pathname: string, t: (key: AppMessageKey) => string): string {
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

  if (pathname.startsWith("/me/profile")) {
    return t("cozeMeProfileTitle");
  }

  if (pathname.startsWith("/me/settings")) {
    return t("cozeMeSettingsTitle");
  }

  if (pathname.startsWith("/me/notifications")) {
    return t("cozeMenuNotifications");
  }

  if (pathname.includes("/home")) {
    return t("cozeMenuHome");
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

  if (pathname.includes("/library") || pathname.includes("/resources")) {
    return t("cozeMenuResources");
  }

  return t("cozeMenuProjects");
}

function NativeShellFrame({
  activePath,
  headerTitle,
  headerSubtitle,
  localeLabel,
  userName,
  sidebarTop,
  sidebarPrimary,
  sidebarSecondary,
  onNavigate,
  onToggleLocale,
  onOpenProfile,
  onLogout,
  children
}: {
  activePath: string;
  headerTitle: string;
  headerSubtitle?: string;
  localeLabel: string;
  userName: string;
  sidebarTop?: ReactNode;
  sidebarPrimary?: ReactNode;
  sidebarSecondary?: ShellLink[];
  onNavigate: (path: string) => void;
  onToggleLocale: () => void;
  onOpenProfile?: () => void;
  onLogout: () => void;
  children: ReactNode;
}) {
  return (
    <div style={{ display: "flex", minHeight: "100vh", background: "#f7f8fa" }}>
      <aside
        data-testid="app-sidebar"
        style={{
          width: 288,
          borderRight: "1px solid rgba(28,31,35,0.08)",
          background: "#fff",
          padding: 16,
          display: "flex",
          flexDirection: "column",
          gap: 16
        }}
      >
        <Space align="center" spacing={8}>
          <Avatar shape="square" size="small">
            扣
          </Avatar>
          <Typography.Text strong>扣子</Typography.Text>
        </Space>

        {sidebarTop ? <div>{sidebarTop}</div> : null}
        {sidebarPrimary ? <div style={{ minHeight: 0, flex: 1 }}>{sidebarPrimary}</div> : null}

        {sidebarSecondary && sidebarSecondary.length > 0 ? (
          <>
            <Divider />
            <Space vertical spacing={6}>
              {sidebarSecondary.map((item) => {
                const active = isActivePath(activePath, item);
                return (
                  <Button
                    key={item.key}
                    block
                    theme={active ? "solid" : "borderless"}
                    color={active ? "brand" : "secondary"}
                    onClick={() => onNavigate(item.path)}
                    data-testid={item.testId}
                    style={{ justifyContent: "flex-start" }}
                  >
                    {item.label}
                  </Button>
                );
              })}
            </Space>
          </>
        ) : null}
      </aside>

      <div style={{ flex: 1, minWidth: 0, display: "flex", flexDirection: "column" }}>
        <header
          style={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            gap: 16,
            padding: "16px 24px",
            borderBottom: "1px solid rgba(28,31,35,0.08)",
            background: "#fff"
          }}
        >
          <div style={{ minWidth: 0 }}>
            <Typography.Title heading={5} style={{ margin: 0 }}>
              {headerTitle}
            </Typography.Title>
            {headerSubtitle ? (
              <Typography.Text type="secondary" style={{ display: "block", marginTop: 6 }}>
                {headerSubtitle}
              </Typography.Text>
            ) : null}
          </div>

          <Space align="center" spacing={8}>
            <Button theme="borderless" color="highlight" onClick={onToggleLocale} data-testid="app-shell-toggle-locale">
              {localeLabel}
            </Button>
            {onOpenProfile ? (
              <Button theme="borderless" onClick={onOpenProfile}>
                {userName}
              </Button>
            ) : null}
            <Button theme="borderless" color="secondary" onClick={onLogout}>
              退出登录
            </Button>
          </Space>
        </header>

        <main style={{ flex: 1, minWidth: 0, padding: 24, overflow: "auto" }}>{children}</main>
      </div>
    </div>
  );
}

export function SpaceShellLayout() {
  const { space_id = "" } = useParams<{ space_id: string }>();
  const auth = useAuth();
  const bootstrap = useBootstrap();
  const location = useLocation();
  const { loading, spaceListLoading, spaceList } = useInitSpace(space_id);

  if (bootstrap.loading || auth.loading || loading) {
    return <LoadingPage />;
  }

  if (!auth.isAuthenticated) {
    return <Navigate to={signPath(location.pathname + location.search)} replace />;
  }

  if (!space_id || spaceList.length === 0) {
    return <Navigate to={selectWorkspacePath()} replace />;
  }

  const tenantId = getTenantId() ?? "";
  return (
    <OrganizationProvider orgId={tenantId}>
      <WorkspaceProvider workspaceId={space_id}>
        <PermissionProvider>
          <RememberWorkspace />
          <SpaceShellChrome />
        </PermissionProvider>
      </WorkspaceProvider>
    </OrganizationProvider>
  );
}

function SpaceShellChrome() {
  const { t, locale, setLocale } = useAppI18n();
  const auth = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const workspace = useWorkspaceContext();
  const currentSubMenu = deriveSpaceSubMenu(location.pathname, workspace.id);
  const sidebarSecondary = useMemo(() => [
    ...buildSpaceLinks(workspace.id, t),
    ...buildPlatformLinks(t)
  ], [t, workspace.id]);

  const cozeLocale = toCozeLocale(locale);
  const [cozeI18nReady, setCozeI18nReady] = useState(false);

  useEffect(() => {
    if (cozeI18nReady) {
      I18n.setLang(cozeLocale);
      return;
    }
    let cancelled = false;
    void initI18nInstance({ language: cozeLocale })
      .then(() => {
        I18n.setLang(cozeLocale);
        if (!cancelled) setCozeI18nReady(true);
      })
      .catch(() => {
        I18n.setLang(cozeLocale);
        if (!cancelled) setCozeI18nReady(true);
      });
    return () => { cancelled = true; };
  }, [cozeLocale, cozeI18nReady]);

  if (workspace.loading || !cozeI18nReady) {
    return <LoadingPage />;
  }

  return (
    <I18nProvider i18n={I18n}>
    <NativeShellFrame
      activePath={`${location.pathname}${location.search}`}
      headerTitle={resolveShellHeaderTitle(location.pathname, t)}
      headerSubtitle={workspace.name || workspace.appKey}
      localeLabel={t(locale === "zh-CN" ? "switchToEnglish" : "switchToChinese")}
      userName={auth.profile?.displayName || auth.profile?.username || "Atlas"}
      sidebarTop={<WorkspaceSwitcher workspaceId={workspace.id} workspaceLabel={workspace.name || workspace.appKey} />}
      sidebarPrimary={(
        <WorkspaceSubMenu
          header={<div />}
          menus={buildSpaceSubMenuItems(t)}
          currentSubMenu={currentSubMenu}
        />
      )}
      sidebarSecondary={sidebarSecondary}
      onNavigate={navigate}
      onToggleLocale={() => setLocale(locale === "zh-CN" ? "en-US" : "zh-CN")}
      onOpenProfile={() => navigate(meProfilePath())}
      onLogout={() => {
        void auth.logout().then(() => navigate(signPath(), { replace: true }));
      }}
    >
      <Outlet />
    </NativeShellFrame>
    </I18nProvider>
  );
}

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
          <PlatformShellChrome />
        </PermissionProvider>
      </WorkspaceProvider>
    </OrganizationProvider>
  );
}

function PlatformShellChrome() {
  const { t, locale, setLocale } = useAppI18n();
  const auth = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const workspace = useWorkspaceContext();
  const sidebarSecondary = useMemo(() => buildPlatformLinks(t), [t]);

  if (workspace.loading) {
    return <LoadingPage />;
  }

  return (
    <NativeShellFrame
      activePath={`${location.pathname}${location.search}`}
      headerTitle={resolveShellHeaderTitle(location.pathname, t)}
      headerSubtitle={workspace.name || workspace.appKey}
      localeLabel={t(locale === "zh-CN" ? "switchToEnglish" : "switchToChinese")}
      userName={auth.profile?.displayName || auth.profile?.username || "Atlas"}
      sidebarTop={(
        <Space vertical spacing={8} style={{ width: "100%" }}>
          <WorkspaceSwitcher workspaceId={workspace.id} workspaceLabel={workspace.name || workspace.appKey} />
          <Button
            block
            theme="solid"
            color="brand"
            onClick={() => navigate(`${workspaceRootPath(workspace.id)}/develop`)}
            data-testid="app-sidebar-item-current-workspace"
          >
            返回工作空间
          </Button>
        </Space>
      )}
      sidebarSecondary={sidebarSecondary}
      onNavigate={navigate}
      onToggleLocale={() => setLocale(locale === "zh-CN" ? "en-US" : "zh-CN")}
      onOpenProfile={() => navigate(meProfilePath())}
      onLogout={() => {
        void auth.logout().then(() => navigate(signPath(), { replace: true }));
      }}
    >
      <Outlet />
    </NativeShellFrame>
  );
}
