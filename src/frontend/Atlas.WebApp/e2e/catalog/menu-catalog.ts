import type { CatalogEntry, SeedState } from "./seed-types";

const withSeed = (resolver: (seed: SeedState) => string | null | undefined) => resolver;
const shell = { type: "testid", value: "e2e-shell-main" } as const;

export const menuCatalog: CatalogEntry[] = [
  {
    id: "console-home",
    domain: "console",
    priority: "P0",
    loginRole: "superadmin",
    path: "/console",
    landmark: { type: "testid", value: "e2e-console-page" },
    successFlow: "open console home",
    restrictedRole: "readonly",
    restrictedFlow: "readonly cannot open console home"
  },
  {
    id: "console-apps",
    domain: "console",
    priority: "P0",
    loginRole: "superadmin",
    path: "/console/apps",
    landmark: { type: "testid", value: "e2e-console-page" },
    successFlow: "open console apps"
  },
  {
    id: "console-datasources",
    domain: "console",
    priority: "P1",
    loginRole: "sysadmin",
    path: "/console/datasources",
    landmark: shell,
    successFlow: "open console datasources",
    restrictedRole: "readonly",
    restrictedFlow: "readonly cannot open console datasources"
  },
  {
    id: "console-system-configs",
    domain: "console",
    priority: "P1",
    loginRole: "sysadmin",
    path: "/console/settings/system/configs",
    landmark: shell,
    successFlow: "open console system configs",
    restrictedRole: "readonly",
    restrictedFlow: "readonly cannot open console system configs"
  },
  {
    id: "org-departments",
    domain: "system",
    priority: "P0",
    loginRole: "sysadmin",
    path: "/settings/org/departments",
    landmark: shell,
    successFlow: "open departments page",
    restrictedRole: "readonly",
    restrictedFlow: "readonly cannot open departments page"
  },
  {
    id: "org-positions",
    domain: "system",
    priority: "P1",
    loginRole: "sysadmin",
    path: "/settings/org/positions",
    landmark: shell,
    successFlow: "open positions page"
  },
  {
    id: "org-users",
    domain: "system",
    priority: "P0",
    loginRole: "sysadmin",
    path: "/settings/org/users",
    landmark: shell,
    successFlow: "open users page",
    restrictedRole: "readonly",
    restrictedFlow: "readonly cannot open users page"
  },
  {
    id: "auth-roles",
    domain: "system",
    priority: "P0",
    loginRole: "sysadmin",
    path: "/settings/auth/roles",
    landmark: shell,
    successFlow: "open roles page"
  },
  {
    id: "auth-menus",
    domain: "system",
    priority: "P1",
    loginRole: "sysadmin",
    path: "/settings/auth/menus",
    landmark: shell,
    successFlow: "open menus page"
  },
  {
    id: "settings-projects",
    domain: "system",
    priority: "P1",
    loginRole: "sysadmin",
    path: "/settings/projects",
    landmark: shell,
    successFlow: "open projects page"
  },
  {
    id: "settings-system-datasources",
    domain: "system",
    priority: "P0",
    loginRole: "sysadmin",
    path: "/settings/system/datasources",
    landmark: shell,
    successFlow: "open tenant datasource page"
  },
  {
    id: "settings-system-dict-types",
    domain: "system",
    priority: "P1",
    loginRole: "sysadmin",
    path: "/settings/system/dict-types",
    landmark: shell,
    successFlow: "open dictionary types page"
  },
  {
    id: "settings-system-configs",
    domain: "system",
    priority: "P1",
    loginRole: "sysadmin",
    path: "/settings/system/configs",
    landmark: shell,
    successFlow: "open system configs page"
  },
  {
    id: "system-notifications",
    domain: "system",
    priority: "P0",
    loginRole: "sysadmin",
    path: "/system/notifications",
    landmark: shell,
    successFlow: "open notifications inbox"
  },
  {
    id: "settings-system-webhooks",
    domain: "system",
    priority: "P1",
    loginRole: "sysadmin",
    path: "/settings/system/webhooks",
    landmark: shell,
    successFlow: "open webhooks page"
  },
  {
    id: "assets",
    domain: "security",
    priority: "P0",
    loginRole: "securityadmin",
    path: "/assets",
    landmark: shell,
    successFlow: "open assets page",
    restrictedRole: "readonly",
    restrictedFlow: "readonly cannot open assets page"
  },
  {
    id: "audit",
    domain: "security",
    priority: "P1",
    loginRole: "securityadmin",
    path: "/audit",
    landmark: shell,
    successFlow: "open audit page"
  },
  {
    id: "alert",
    domain: "security",
    priority: "P1",
    loginRole: "securityadmin",
    path: "/alert",
    landmark: shell,
    successFlow: "open alert page"
  },
  {
    id: "ai-model-configs",
    domain: "ai",
    priority: "P1",
    loginRole: "aiadmin",
    path: "/settings/ai/model-configs",
    landmark: shell,
    successFlow: "open model configs"
  },
  {
    id: "ai-agents",
    domain: "ai",
    priority: "P0",
    loginRole: "aiadmin",
    path: "/ai/agents",
    landmark: shell,
    successFlow: "open agent list"
  },
  {
    id: "ai-knowledge-bases",
    domain: "ai",
    priority: "P0",
    loginRole: "aiadmin",
    path: "/ai/knowledge-bases",
    landmark: shell,
    successFlow: "open knowledge bases"
  },
  {
    id: "ai-databases",
    domain: "ai",
    priority: "P1",
    loginRole: "aiadmin",
    path: "/ai/databases",
    landmark: shell,
    successFlow: "open ai databases"
  },
  {
    id: "ai-variables",
    domain: "ai",
    priority: "P2",
    loginRole: "aiadmin",
    path: "/ai/variables",
    landmark: shell,
    successFlow: "open ai variables"
  },
  {
    id: "ai-plugins",
    domain: "ai",
    priority: "P1",
    loginRole: "aiadmin",
    path: "/ai/plugins",
    landmark: shell,
    successFlow: "open ai plugins"
  },
  {
    id: "ai-apps",
    domain: "ai",
    priority: "P1",
    loginRole: "aiadmin",
    path: "/ai/apps",
    landmark: shell,
    successFlow: "open ai apps"
  },
  {
    id: "ai-open-platform",
    domain: "ai",
    priority: "P1",
    loginRole: "aiadmin",
    path: "/ai/open-platform",
    landmark: shell,
    successFlow: "open ai open platform"
  },
  {
    id: "ai-workspace",
    domain: "ai",
    priority: "P1",
    loginRole: "aiadmin",
    path: "/ai/workspace",
    landmark: shell,
    successFlow: "open ai workspace"
  },
  {
    id: "ai-library",
    domain: "ai",
    priority: "P2",
    loginRole: "aiadmin",
    path: "/ai/library",
    landmark: shell,
    successFlow: "open ai library"
  },
  {
    id: "ai-test-sets",
    domain: "ai",
    priority: "P2",
    loginRole: "aiadmin",
    path: "/ai/devops/test-sets",
    landmark: shell,
    successFlow: "open ai test sets"
  },
  {
    id: "ai-mock-sets",
    domain: "ai",
    priority: "P2",
    loginRole: "aiadmin",
    path: "/ai/devops/mock-sets",
    landmark: shell,
    successFlow: "open ai mock sets"
  },
  {
    id: "ai-shortcuts",
    domain: "ai",
    priority: "P2",
    loginRole: "aiadmin",
    path: "/ai/shortcuts",
    landmark: shell,
    successFlow: "open ai shortcuts"
  },
  {
    id: "ai-search",
    domain: "ai",
    priority: "P2",
    loginRole: "aiadmin",
    path: "/ai/search",
    landmark: shell,
    successFlow: "open ai search"
  },
  {
    id: "ai-marketplace",
    domain: "ai",
    priority: "P2",
    loginRole: "aiadmin",
    path: "/ai/marketplace",
    landmark: shell,
    successFlow: "open ai marketplace"
  },
  {
    id: "approval-flows",
    domain: "approval",
    priority: "P0",
    loginRole: "approvaladmin",
    path: "/approval/flows",
    landmark: shell,
    successFlow: "open approval flows"
  },
  {
    id: "approval-workspace",
    domain: "approval",
    priority: "P0",
    loginRole: "approvaladmin",
    path: "/approval/workspace",
    landmark: shell,
    successFlow: "open approval workspace"
  },
  {
    id: "approval-designer",
    domain: "approval",
    priority: "P1",
    loginRole: "approvaladmin",
    path: "/approval/designer",
    landmark: shell,
    successFlow: "open approval designer"
  },
  {
    id: "approval-flows-manage",
    domain: "approval",
    priority: "P1",
    loginRole: "approvaladmin",
    path: "/approval/flows/manage",
    landmark: shell,
    successFlow: "open approval flow manager"
  },
  {
    id: "approval-instances-manage",
    domain: "approval",
    priority: "P1",
    loginRole: "approvaladmin",
    path: "/approval/instances/manage",
    landmark: shell,
    successFlow: "open approval instance manager"
  },
  {
    id: "lowcode-apps",
    domain: "lowcode",
    priority: "P0",
    loginRole: "appadmin",
    path: "/lowcode/apps",
    landmark: shell,
    successFlow: "open lowcode apps"
  },
  {
    id: "lowcode-forms",
    domain: "lowcode",
    priority: "P1",
    loginRole: "appadmin",
    path: "/lowcode/forms",
    landmark: shell,
    successFlow: "open lowcode forms"
  },
  {
    id: "lowcode-plugin-market",
    domain: "lowcode",
    priority: "P2",
    loginRole: "appadmin",
    path: "/lowcode/plugin-market",
    landmark: shell,
    successFlow: "open lowcode plugin market"
  },
  {
    id: "writeback-monitor",
    domain: "lowcode",
    priority: "P1",
    loginRole: "sysadmin",
    path: "/monitor/writeback-failures",
    landmark: shell,
    successFlow: "open writeback monitor"
  },
  {
    id: "app-dashboard",
    domain: "lowcode",
    priority: "P0",
    loginRole: "appadmin",
    path: withSeed((seed) => (seed.lowCodeApps.e2e_workspace_app ? `/apps/${seed.lowCodeApps.e2e_workspace_app}/dashboard` : null)),
    landmark: { type: "testid", value: "e2e-app-workspace-layout" },
    successFlow: "open app workspace dashboard"
  },
  {
    id: "app-builder",
    domain: "lowcode",
    priority: "P0",
    loginRole: "appadmin",
    path: withSeed((seed) => (seed.lowCodeApps.e2e_workspace_app ? `/apps/${seed.lowCodeApps.e2e_workspace_app}/builder` : null)),
    landmark: { type: "testid", value: "e2e-app-workspace-layout" },
    successFlow: "open app builder"
  },
  {
    id: "app-settings",
    domain: "lowcode",
    priority: "P1",
    loginRole: "appadmin",
    path: withSeed((seed) => (seed.lowCodeApps.e2e_workspace_app ? `/apps/${seed.lowCodeApps.e2e_workspace_app}/settings` : null)),
    landmark: { type: "testid", value: "e2e-app-workspace-layout" },
    successFlow: "open app settings"
  },
  {
    id: "app-runtime-home",
    domain: "lowcode",
    priority: "P0",
    loginRole: "appadmin",
    path: withSeed((seed) => (seed.lowCodeApps.e2e_workspace_app ? `/apps/${seed.lowCodeApps.e2e_workspace_app}/run/home` : null)),
    landmark: { type: "testid", value: "e2e-app-workspace-layout" },
    successFlow: "open in-app runtime"
  },
  {
    id: "app-runtime-external",
    domain: "lowcode",
    priority: "P1",
    loginRole: "appadmin",
    path: withSeed((seed) => (seed.lowCodeAppKeys.e2e_workspace_app ? `/r/${seed.lowCodeAppKeys.e2e_workspace_app}/home` : null)),
    landmark: { type: "title" },
    successFlow: "open external runtime"
  },
  {
    id: "monitor-message-queue",
    domain: "monitor",
    priority: "P1",
    loginRole: "sysadmin",
    path: "/monitor/message-queue",
    landmark: shell,
    successFlow: "open message queue monitor"
  },
  {
    id: "monitor-server-info",
    domain: "monitor",
    priority: "P1",
    loginRole: "sysadmin",
    path: "/monitor/server-info",
    landmark: shell,
    successFlow: "open server info"
  },
  {
    id: "monitor-scheduled-jobs",
    domain: "monitor",
    priority: "P1",
    loginRole: "sysadmin",
    path: "/monitor/scheduled-jobs",
    landmark: shell,
    successFlow: "open scheduled jobs"
  },
  {
    id: "workflow-list",
    domain: "compat",
    priority: "P1",
    loginRole: "approvaladmin",
    path: "/workflow",
    landmark: shell,
    successFlow: "open workflow list"
  },
  {
    id: "login-logs",
    domain: "compat",
    priority: "P1",
    loginRole: "sysadmin",
    path: "/system/login-logs",
    landmark: shell,
    successFlow: "open login logs"
  },
  {
    id: "online-users",
    domain: "compat",
    priority: "P1",
    loginRole: "sysadmin",
    path: "/system/online-users",
    landmark: shell,
    successFlow: "open online users"
  },
  {
    id: "notification-manage",
    domain: "compat",
    priority: "P1",
    loginRole: "sysadmin",
    path: "/system/notifications/manage",
    landmark: shell,
    successFlow: "open notification manage"
  },
  {
    id: "settings-license",
    domain: "compat",
    priority: "P2",
    loginRole: "sysadmin",
    path: "/settings/license",
    landmark: shell,
    successFlow: "open license page"
  },
  {
    id: "console-tools",
    domain: "compat",
    priority: "P2",
    loginRole: "sysadmin",
    path: "/console/tools",
    landmark: shell,
    successFlow: "open tools center"
  },
  {
    id: "console-resources",
    domain: "compat",
    priority: "P2",
    loginRole: "superadmin",
    path: "/console/resources",
    landmark: { type: "testid", value: "e2e-console-page" },
    successFlow: "open console resources"
  },
  {
    id: "console-releases",
    domain: "compat",
    priority: "P2",
    loginRole: "superadmin",
    path: "/console/releases",
    landmark: { type: "testid", value: "e2e-console-page" },
    successFlow: "open console releases"
  },
  {
    id: "legacy-notifications",
    domain: "compat",
    priority: "P2",
    loginRole: "superadmin",
    path: "/notifications",
    landmark: shell,
    successFlow: "redirect legacy notifications"
  },
  {
    id: "legacy-system-configs",
    domain: "compat",
    priority: "P2",
    loginRole: "sysadmin",
    path: "/system/configs",
    landmark: shell,
    successFlow: "redirect legacy system configs"
  },
  {
    id: "legacy-alerts",
    domain: "compat",
    priority: "P2",
    loginRole: "securityadmin",
    path: "/alerts",
    landmark: shell,
    successFlow: "redirect legacy alerts"
  },
  {
    id: "profile",
    domain: "navigation",
    priority: "P1",
    loginRole: "superadmin",
    path: "/profile",
    landmark: shell,
    successFlow: "open profile"
  },
  {
    id: "hidden-agent-edit",
    domain: "ai",
    priority: "P1",
    loginRole: "aiadmin",
    path: withSeed((seed) => (seed.ai.agentId ? `/ai/agents/${seed.ai.agentId}/edit` : null)),
    landmark: shell,
    successFlow: "open hidden agent edit"
  },
  {
    id: "hidden-knowledge-detail",
    domain: "ai",
    priority: "P1",
    loginRole: "aiadmin",
    path: withSeed((seed) => (seed.ai.knowledgeBaseId ? `/ai/knowledge-bases/${seed.ai.knowledgeBaseId}` : null)),
    landmark: shell,
    successFlow: "open hidden knowledge base detail"
  },
  {
    id: "hidden-database-detail",
    domain: "ai",
    priority: "P1",
    loginRole: "aiadmin",
    path: withSeed((seed) => (seed.ai.databaseId ? `/ai/databases/${seed.ai.databaseId}` : null)),
    landmark: shell,
    successFlow: "open hidden database detail"
  },
  {
    id: "hidden-plugin-detail",
    domain: "ai",
    priority: "P2",
    loginRole: "aiadmin",
    path: withSeed((seed) => (seed.ai.pluginId ? `/ai/plugins/${seed.ai.pluginId}` : null)),
    landmark: shell,
    successFlow: "open hidden plugin detail"
  },
  {
    id: "hidden-ai-app-edit",
    domain: "ai",
    priority: "P1",
    loginRole: "aiadmin",
    path: withSeed((seed) => (seed.ai.aiAppId ? `/ai/apps/${seed.ai.aiAppId}/edit` : null)),
    landmark: shell,
    successFlow: "open hidden ai app edit"
  },
  {
    id: "hidden-approval-instance-detail",
    domain: "approval",
    priority: "P1",
    loginRole: "approvaladmin",
    path: withSeed((seed) => (seed.approval.instanceId ? `/process/instances/${seed.approval.instanceId}` : null)),
    landmark: shell,
    successFlow: "open hidden approval instance detail"
  }
];

export function resolveCatalogPath(entry: CatalogEntry, seed: SeedState) {
  if (typeof entry.path === "function") {
    return entry.path(seed);
  }
  return entry.path;
}
