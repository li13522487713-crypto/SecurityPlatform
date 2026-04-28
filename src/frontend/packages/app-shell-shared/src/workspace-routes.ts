/**
 * Coze 平台 PRD（07-前端路由表与菜单权限表）路径辅助函数。
 *
 * 注意：这一组函数是“工作空间为唯一维度”的扁平路由（/space/:workspaceId/...）。
 * 旧的 /org/:orgId/workspaces/:workspaceId/... 与 /apps/:appKey/... 仍由 ./routes.ts 提供，
 * 仅用于兼容跳转，不再用于菜单与新页面。
 */

export type WorkspaceSettingsTab = "publish" | "models";

export type WorkspaceSettingsPublishTab = "agents" | "apps" | "workflows" | "channels";

export type ResourceLeaf =
  | "workflows"
  | "chatflows"
  | "plugins"
  | "knowledge"
  | "databases"
  | "variables"
  | "prompts";

export type MeSettingsTab = "account" | "general" | "channels" | "datasource";

function encodeSegment(value: string | number): string {
  return encodeURIComponent(String(value).trim());
}

export function selectWorkspacePath(): string {
  return "/space";
}

export function workspaceRootPath(workspaceId: string): string {
  return `/space/${encodeSegment(workspaceId)}`;
}

/** 与 Coze 风格资源库页 `WorkspaceLibraryPage` 的 tab 对齐（用于 `?tab=` 深链） */
export type WorkspaceLibraryTab =
  | "all"
  | "plugin"
  | "workflow"
  | "microflow"
  | "knowledge-base"
  | "card"
  | "prompt"
  | "database"
  | "voice"
  | "memory";

export function workspaceLibraryPath(workspaceId: string, tab?: WorkspaceLibraryTab): string {
  const base = `${workspaceRootPath(workspaceId)}/library`;
  if (!tab || tab === "all") {
    return base;
  }
  return `${base}?tab=${encodeURIComponent(tab)}`;
}

export function workspaceHomePath(workspaceId: string): string {
  return `${workspaceRootPath(workspaceId)}/home`;
}

export function workspaceProjectsPath(workspaceId: string): string {
  return `${workspaceRootPath(workspaceId)}/projects`;
}

export function workspaceProjectsFolderPath(workspaceId: string, folderId: string): string {
  return `${workspaceProjectsPath(workspaceId)}/folder/${encodeSegment(folderId)}`;
}

export function workspaceResourcesPath(workspaceId: string, leaf?: ResourceLeaf): string {
  const base = `${workspaceRootPath(workspaceId)}/resources`;
  return leaf ? `${base}/${leaf}` : base;
}

export function workspaceTasksPath(workspaceId: string, taskId?: string): string {
  const base = `${workspaceRootPath(workspaceId)}/tasks`;
  return taskId ? `${base}/${encodeSegment(taskId)}` : base;
}

export function workspaceEvaluationsPath(workspaceId: string, evaluationId?: string): string {
  const base = `${workspaceRootPath(workspaceId)}/evaluations`;
  return evaluationId ? `${base}/${encodeSegment(evaluationId)}` : base;
}

export function workspaceSettingsPath(workspaceId: string): string {
  return `${workspaceRootPath(workspaceId)}/settings`;
}

export function workspaceSettingsPublishPath(
  workspaceId: string,
  tab?: WorkspaceSettingsPublishTab
): string {
  const base = `${workspaceSettingsPath(workspaceId)}/publish`;
  return tab ? `${base}/${tab}` : base;
}

export function workspaceSettingsModelsPath(workspaceId: string): string {
  return `${workspaceSettingsPath(workspaceId)}/models`;
}

function splitPathAndQuery(pathWithSearch: string): { pathname: string; search: string } {
  const trimmed = pathWithSearch.trim();
  if (!trimmed) {
    return { pathname: "", search: "" };
  }

  const queryIndex = trimmed.indexOf("?");
  if (queryIndex < 0) {
    return { pathname: trimmed, search: "" };
  }

  return {
    pathname: trimmed.slice(0, queryIndex),
    search: trimmed.slice(queryIndex)
  };
}

export function buildWorkspaceSwitchPath(pathWithSearch: string, workspaceId: string): string {
  const { pathname, search } = splitPathAndQuery(pathWithSearch);
  const decodedWorkspaceId = encodeSegment(workspaceId);

  if (!pathname.startsWith("/space/")) {
    return `${pathname}${search}`;
  }

  const detailFallbacks: Array<[RegExp, string]> = [
    [/^\/space\/[^/]+\/projects\/folder\/[^/]+$/u, workspaceProjectsPath(workspaceId)],
    [/^\/space\/[^/]+\/tasks\/[^/]+$/u, workspaceTasksPath(workspaceId)],
    [/^\/space\/[^/]+\/evaluations\/[^/]+$/u, workspaceEvaluationsPath(workspaceId)]
  ];

  for (const [pattern, fallbackPath] of detailFallbacks) {
    if (pattern.test(pathname)) {
      return fallbackPath;
    }
  }

  return pathname.replace(/^\/space\/[^/]+/u, `/space/${decodedWorkspaceId}`) + search;
}

export function marketTemplatesPath(): string {
  return "/market/templates";
}

export function marketPluginsPath(): string {
  return "/market/plugins";
}

export function communityWorksPath(): string {
  return "/community/works";
}

export function openApiPath(): string {
  return "/open/api";
}

export function docsPath(slug?: string): string {
  return slug ? `/docs/${encodeSegment(slug)}` : "/docs";
}

export function platformGeneralPath(): string {
  return "/platform/general";
}

export function meProfilePath(): string {
  return "/me/profile";
}

export function meSettingsPath(tab?: MeSettingsTab): string {
  const base = "/me/settings";
  return tab ? `${base}/${tab}` : base;
}

export function meNotificationsPath(): string {
  return "/me/notifications";
}

export function agentEditorPath(agentId: string): string {
  return `/agent/${encodeSegment(agentId)}/editor`;
}

export function agentPublishPath(agentId: string): string {
  return `/agent/${encodeSegment(agentId)}/publish`;
}

export function appEditorPath(projectId: string): string {
  return `/app/${encodeSegment(projectId)}/editor`;
}

export function appPublishPath(projectId: string): string {
  return `/app/${encodeSegment(projectId)}/publish`;
}

export function workflowEditorPath(workflowId: string): string {
  return `/workflow/${encodeSegment(workflowId)}/editor`;
}

export function microflowEditorPath(microflowId: string): string {
  return `/microflow/${encodeSegment(microflowId)}/editor`;
}

export function workspaceMicroflowEditorPath(workspaceId: string, microflowId: string): string {
  return `${workspaceRootPath(workspaceId)}/microflows/${encodeSegment(microflowId)}`;
}

export function workflowHistoryPath(workflowId: string): string {
  return `/workflow/${encodeSegment(workflowId)}/history`;
}

export function chatflowEditorPath(chatflowId: string): string {
  return `/chatflow/${encodeSegment(chatflowId)}/editor`;
}
