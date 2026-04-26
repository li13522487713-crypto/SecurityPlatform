import { useEffect, useMemo, useState, type ReactNode } from "react";
import { Navigate, Outlet, useLocation, useNavigate, useParams } from "react-router-dom";
import { useInitSpace } from "@coze-foundation/space-ui-adapter";
import { getTenantId } from "@atlas/shared-react-core/utils";
import {
  communityWorksPath,
  docsPath,
  marketPluginsPath,
  marketTemplatesPath,
  meProfilePath,
  meSettingsPath,
  openApiPath,
  selectWorkspacePath,
  signPath,
  workspaceEvaluationsPath,
  workspaceHomePath,
  workspaceProjectsPath,
  workspaceRootPath,
  workspaceSettingsPublishPath,
  workspaceTasksPath
} from "@atlas/app-shell-shared";
import { Dropdown, Layout, Nav, Avatar, Button, Typography, Space, Divider, Badge, Tag } from "@douyinfe/semi-ui";
import {
  IconHome, IconCode, IconFolder, IconList, IconHistogram, IconSetting,
  IconBox, IconPuzzle, IconGlobe, IconLink, IconArticle,
  IconChevronDown, IconBell, IconExpand, IconAlertCircle, IconExit
} from "@douyinfe/semi-icons";
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
  label: ReactNode;
  path: string;
  testId: string;
  icon?: ReactNode;
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

function buildAllSpaceLinks(workspaceId: string, t: (key: AppMessageKey) => string): ShellLink[] {
  return [
    {
      key: "home",
      label: t("cozeMenuHome"),
      path: workspaceHomePath(workspaceId),
      icon: <IconHome />,
      testId: "app-sidebar-item-home"
    },
    {
      key: "develop",
      label: t("cozeMenuProjects"),
      path: workspaceProjectsPath(workspaceId),
      icon: <IconCode />,
      testId: "app-sidebar-item-projects",
      activeMatchers: [
        workspaceProjectsPath(workspaceId),
        `${workspaceRootPath(workspaceId)}/bot`,
        `${workspaceRootPath(workspaceId)}/publish/agent`
      ]
    },
    {
      key: "library",
      label: t("cozeMenuResources"),
      path: `${workspaceRootPath(workspaceId)}/library`,
      icon: <IconFolder />,
      testId: "app-sidebar-item-resources",
      activeMatchers: [
        `${workspaceRootPath(workspaceId)}/library`,
        `${workspaceRootPath(workspaceId)}/plugin`,
        `${workspaceRootPath(workspaceId)}/knowledge`,
        `${workspaceRootPath(workspaceId)}/database`,
        `${workspaceRootPath(workspaceId)}/databases`
      ]
    },
    {
      key: "tasks",
      label: t("cozeMenuTasks"),
      path: workspaceTasksPath(workspaceId),
      icon: <IconList />,
      testId: "app-sidebar-item-tasks"
    },
    {
      key: "evaluations",
      label: t("cozeMenuEvaluations"),
      path: workspaceEvaluationsPath(workspaceId),
      icon: <IconHistogram />,
      testId: "app-sidebar-item-evaluations"
    },
    {
      key: "settings",
      label: (
        <Space>
          {t("cozeMenuSettings")}
          <IconAlertCircle style={{ color: "var(--semi-color-warning)" }} />
        </Space>
      ),
      path: workspaceSettingsPublishPath(workspaceId),
      icon: <IconSetting />,
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
      icon: <IconBox />,
      testId: "app-sidebar-item-templates"
    },
    {
      key: "plugins",
      label: t("cozeMenuPlugins"),
      path: marketPluginsPath(),
      icon: <IconPuzzle />,
      testId: "app-sidebar-item-plugins"
    },
    {
      key: "community",
      label: t("cozeMenuCommunity"),
      path: communityWorksPath(),
      icon: <IconGlobe />,
      testId: "app-sidebar-item-community"
    },
    {
      key: "open-api",
      label: t("cozeMenuOpenApi"),
      path: openApiPath(),
      icon: <IconLink />,
      testId: "app-sidebar-item-open-api"
    },
    {
      key: "docs",
      label: t("cozeMenuDocs"),
      path: docsPath(),
      icon: <IconArticle />,
      testId: "app-sidebar-item-docs"
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

  if (pathname.includes("/mendix-studio")) {
    return t("cozeMenuMendixStudio");
  }

  return t("cozeMenuProjects");
}

function NativeShellFrame({
  activePath,
  headerTitle,
  headerSubtitle,
  localeLabel,
  logoutLabel,
  userName,
  userHandle,
  workspaceName: _workspaceName,
  sidebarTop,
  spaceLinks,
  platformLinks,
  onNavigate,
  onToggleLocale,
  onOpenProfile: _onOpenProfile,
  onLogout,
  children
}: {
  activePath: string;
  headerTitle: string;
  headerSubtitle?: string;
  localeLabel: string;
  logoutLabel: string;
  userName: string;
  userHandle?: string;
  workspaceName: string;
  sidebarTop?: ReactNode;
  spaceLinks: ShellLink[];
  platformLinks: ShellLink[];
  onNavigate: (path: string) => void;
  onToggleLocale: () => void;
  onOpenProfile?: () => void;
  onLogout: () => void;
  children: ReactNode;
}) {
  const activeKey = [...spaceLinks, ...platformLinks].find(link => isActivePath(activePath, link))?.key || "";

  return (
    <Layout style={{ minHeight: "100vh", background: "#f7f8fa" }}>
      <Layout.Sider
        style={{
          width: 256,
          backgroundColor: "#f7f8fa",
          borderRight: "1px solid var(--semi-color-border)",
          display: "flex",
          flexDirection: "column"
        }}
      >
        <div style={{ padding: "16px 20px", display: "flex", alignItems: "center", gap: 12 }}>
          <Avatar size="small" shape="square" color="blue" style={{ borderRadius: 8, fontWeight: "bold", background: "linear-gradient(135deg, #4f46e5, #7c3aed)" }}>
            扣
          </Avatar>
          <Typography.Title heading={4} style={{ margin: 0, fontWeight: 600 }}>扣子</Typography.Title>
        </div>

        <div style={{ padding: "0 16px 16px 16px" }}>
          {sidebarTop}
        </div>

        <div style={{ flex: 1, overflow: "auto" }}>
          <Nav
            mode="vertical"
            style={{ width: "100%", borderRight: "none", backgroundColor: "transparent", padding: "0 8px" }}
            selectedKeys={[activeKey]}
            onSelect={(data) => {
              const link = [...spaceLinks, ...platformLinks].find(l => l.key === data.itemKey);
              if (link) onNavigate(link.path);
            }}
          >
            {spaceLinks.map(link => (
              <Nav.Item 
                key={link.key} 
                itemKey={link.key} 
                text={link.label} 
                icon={link.icon} 
                style={{ borderRadius: 8, height: 40, marginTop: 2, marginBottom: 2, fontWeight: activeKey === link.key ? 600 : 400 }}
              />
            ))}
          </Nav>
          
          <div style={{ padding: "8px 16px" }}>
            <Divider style={{ margin: 0 }} />
          </div>

          <Nav
            mode="vertical"
            style={{ width: "100%", borderRight: "none", backgroundColor: "transparent", padding: "0 8px" }}
            selectedKeys={[activeKey]}
            onSelect={(data) => {
              const link = [...spaceLinks, ...platformLinks].find(l => l.key === data.itemKey);
              if (link) onNavigate(link.path);
            }}
          >
            {platformLinks.map(link => (
              <Nav.Item 
                key={link.key} 
                itemKey={link.key} 
                text={link.label} 
                icon={link.icon} 
                style={{ borderRadius: 8, height: 40, marginTop: 2, marginBottom: 2, fontWeight: activeKey === link.key ? 600 : 400 }}
              />
            ))}
          </Nav>
        </div>

        <div style={{ padding: "16px" }}>
          <div style={{ backgroundColor: "#fff", padding: "16px", borderRadius: 8, border: "1px solid var(--semi-color-border)", marginBottom: 16 }}>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 12 }}>
              <Typography.Text style={{ color: "#1c1f23" }}>总积分: 500</Typography.Text>
              <Tag color="blue" size="small" shape="square" style={{ backgroundColor: "#e8eaff", color: "#3d4df4", border: "none" }}>专业版</Tag>
            </div>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <Typography.Text type="secondary" size="small">2200-01-01 到期</Typography.Text>
              <IconChevronDown size="small" style={{ transform: "rotate(-90deg)", color: "#1c1f23" }} />
            </div>
          </div>
          
          <Dropdown
            trigger="click"
            position="topLeft"
            render={
              <Dropdown.Menu style={{ width: 220 }}>
                <div style={{ padding: "12px 16px 10px", display: "flex", alignItems: "center", gap: 10 }}>
                  <Avatar color="blue" style={{ width: 40, height: 40, fontSize: 18, flexShrink: 0 }}>
                    {userName.charAt(0).toUpperCase()}
                  </Avatar>
                  <div style={{ minWidth: 0 }}>
                    <Typography.Text strong ellipsis={{ showTooltip: true }}>{userName}</Typography.Text>
                    {userHandle ? (
                      <Typography.Text type="tertiary" size="small" style={{ display: "block" }}>@{userHandle}</Typography.Text>
                    ) : null}
                  </div>
                </div>
                <Dropdown.Divider />
                <Dropdown.Item icon={<IconHome />} onClick={() => onNavigate(meProfilePath())}>个人主页</Dropdown.Item>
                <Dropdown.Item icon={<IconSetting />} onClick={() => onNavigate(meSettingsPath())}>账号设置</Dropdown.Item>
                <Dropdown.Item icon={<IconArticle />}>
                  <div style={{ display: "flex", justifyContent: "space-between", width: "100%" }}>
                    <span>联系我们</span>
                    <IconChevronDown style={{ transform: "rotate(-90deg)" }} />
                  </div>
                </Dropdown.Item>
                <Dropdown.Divider />
                <Dropdown.Item icon={<IconExit />} type="danger" onClick={onLogout}>退出登录</Dropdown.Item>
              </Dropdown.Menu>
            }
          >
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", padding: "0 4px", cursor: "pointer" }}>
              <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                <Avatar size="small" color="blue" style={{ width: 28, height: 28, fontSize: 14 }}>
                  {userName.charAt(0).toUpperCase()}
                </Avatar>
                <Typography.Text style={{ maxWidth: 80, fontWeight: 500 }} ellipsis={{ showTooltip: true }}>{userName}</Typography.Text>
              </div>
              <Space spacing={16}>
                <IconExpand style={{ color: "var(--semi-color-text-2)" }} />
                <Badge count={39} overflowCount={99} type="danger">
                  <IconBell style={{ color: "var(--semi-color-text-2)" }} />
                </Badge>
              </Space>
            </div>
          </Dropdown>
        </div>
      </Layout.Sider>

      <Layout style={{ backgroundColor: "#fff" }}>
        <Layout.Header
          style={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            padding: "16px 24px",
            borderBottom: "1px solid var(--semi-color-border)",
            backgroundColor: "#fff"
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
            <Button theme="borderless" onClick={onToggleLocale} data-testid="app-shell-toggle-locale">
              {localeLabel}
            </Button>
            <Button theme="borderless" color="secondary" onClick={onLogout}>
              {logoutLabel}
            </Button>
          </Space>
        </Layout.Header>
        <Layout.Content style={{ padding: 24, overflow: "auto" }}>
          {children}
        </Layout.Content>
      </Layout>
    </Layout>
  );
}

export function SpaceShellLayout() {
  const { space_id = "" } = useParams<{ space_id: string }>();
  const auth = useAuth();
  const bootstrap = useBootstrap();
  const location = useLocation();
  const { loading, spaceListLoading: _spaceListLoading, spaceList } = useInitSpace(space_id);

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
  const spaceLinks = useMemo(() => buildAllSpaceLinks(workspace.id, t), [t, workspace.id]);
  const platformLinks = useMemo(() => buildPlatformLinks(t), [t]);

  const cozeLocale = toCozeLocale(locale);
  const [cozeI18nReady, setCozeI18nReady] = useState(false);

  useEffect(() => {
    if (cozeI18nReady) {
      I18n.setLang(cozeLocale);
      return;
    }
    let cancelled = false;
    void initI18nInstance({ lng: cozeLocale })
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

  if (location.pathname.includes("/mendix-studio")) {
    return (
      <I18nProvider key={cozeLocale} i18n={I18n}>
        <Outlet />
      </I18nProvider>
    );
  }

  return (
    <I18nProvider key={cozeLocale} i18n={I18n}>
    <NativeShellFrame
      activePath={`${location.pathname}${location.search}`}
      headerTitle={resolveShellHeaderTitle(location.pathname, t)}
      headerSubtitle={workspace.name || workspace.appKey}
      localeLabel={t(locale === "zh-CN" ? "switchToEnglish" : "switchToChinese")}
      logoutLabel={t("logout")}
      userName={auth.profile?.displayName || auth.profile?.username || "Atlas"}
      userHandle={auth.profile?.username}
      workspaceName={workspace.name || workspace.appKey || "个人空间"}
      sidebarTop={<WorkspaceSwitcher workspaceId={workspace.id} workspaceLabel={workspace.name || workspace.appKey} />}
      spaceLinks={spaceLinks}
      platformLinks={platformLinks}
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
  const spaceLinks = useMemo(() => buildAllSpaceLinks(workspace.id, t), [t, workspace.id]);
  const platformLinks = useMemo(() => buildPlatformLinks(t), [t]);

  if (workspace.loading) {
    return <LoadingPage />;
  }

  return (
    <NativeShellFrame
      activePath={`${location.pathname}${location.search}`}
      headerTitle={resolveShellHeaderTitle(location.pathname, t)}
      headerSubtitle={workspace.name || workspace.appKey}
      localeLabel={t(locale === "zh-CN" ? "switchToEnglish" : "switchToChinese")}
      logoutLabel={t("logout")}
      userName={auth.profile?.displayName || auth.profile?.username || "Atlas"}
      userHandle={auth.profile?.username}
      workspaceName={workspace.name || workspace.appKey || "个人空间"}
      sidebarTop={<WorkspaceSwitcher workspaceId={workspace.id} workspaceLabel={workspace.name || workspace.appKey} />}
      spaceLinks={spaceLinks}
      platformLinks={platformLinks}
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
