/**
 * Coze 平台 PRD（07-前端路由表与菜单权限表）路径辅助函数。
 *
 * 注意：这一组函数是“工作空间为唯一维度”的扁平路由（/workspace/:workspaceId/...）。
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
  return "/select-workspace";
}

export function workspaceRootPath(workspaceId: string): string {
  return `/workspace/${encodeSegment(workspaceId)}`;
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

export function workflowHistoryPath(workflowId: string): string {
  return `/workflow/${encodeSegment(workflowId)}/history`;
}

export function chatflowEditorPath(chatflowId: string): string {
  return `/chatflow/${encodeSegment(chatflowId)}/editor`;
}
