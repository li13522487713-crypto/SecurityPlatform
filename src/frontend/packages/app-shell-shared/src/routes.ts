export type AppPrimaryArea = "workspace" | "explore" | "admin";

export type WorkspaceLeaf =
  | "develop"
  | "library"
  | "chat"
  | "assistant"
  | "model-configs";

export type ExploreLeaf = "plugin" | "template";

export type AdminLeaf =
  | "overview"
  | "users"
  | "roles"
  | "departments"
  | "positions"
  | "approval"
  | "reports"
  | "dashboards"
  | "visualization"
  | "settings"
  | "profile";

export const DEFAULT_SPACE_ID = "atlas-space";

function encodeSegment(value: string | number): string {
  return encodeURIComponent(String(value).trim());
}

function withQuery(path: string, query?: Record<string, string | null | undefined>): string {
  if (!query) {
    return path;
  }

  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    if (value) {
      search.set(key, value);
    }
  }

  const queryText = search.toString();
  return queryText ? `${path}?${queryText}` : path;
}

export function appRootPath(appKey: string): string {
  return `/apps/${encodeSegment(appKey)}`;
}

export function appSignPath(appKey: string, redirect?: string): string {
  return withQuery(`${appRootPath(appKey)}/sign`, {
    redirect
  });
}

export function appForbiddenPath(appKey: string): string {
  return `${appRootPath(appKey)}/forbidden`;
}

export function replacePathAppKey(pathname: string, appKey: string): string {
  return pathname.replace(/^\/apps\/[^/]+/, appRootPath(appKey));
}

export function workspaceBasePath(appKey: string, spaceId = DEFAULT_SPACE_ID): string {
  return `${appRootPath(appKey)}/space/${encodeSegment(spaceId)}`;
}

export function workspaceLeafPath(appKey: string, spaceId: string, leaf: WorkspaceLeaf): string {
  return `${workspaceBasePath(appKey, spaceId)}/${leaf}`;
}

export function workspaceDevelopPath(appKey: string, spaceId = DEFAULT_SPACE_ID): string {
  return workspaceLeafPath(appKey, spaceId, "develop");
}

export function workspaceLibraryPath(appKey: string, spaceId = DEFAULT_SPACE_ID): string {
  return workspaceLeafPath(appKey, spaceId, "library");
}

export function workspaceChatPath(appKey: string, spaceId = DEFAULT_SPACE_ID): string {
  return workspaceLeafPath(appKey, spaceId, "chat");
}

export function workspaceAssistantPath(appKey: string, spaceId = DEFAULT_SPACE_ID): string {
  return workspaceLeafPath(appKey, spaceId, "assistant");
}

export function workspaceModelConfigsPath(appKey: string, spaceId = DEFAULT_SPACE_ID): string {
  return workspaceLeafPath(appKey, spaceId, "model-configs");
}

export function studioBasePath(appKey: string): string {
  return `${appRootPath(appKey)}/studio`;
}

export function studioDevelopPath(appKey: string): string {
  return `${studioBasePath(appKey)}/develop`;
}

export function studioDashboardPath(appKey: string): string {
  return `${studioBasePath(appKey)}/dashboard`;
}

export function studioPublishCenterPath(appKey: string): string {
  return `${studioBasePath(appKey)}/publish-center`;
}

export function studioAssistantsPath(appKey: string): string {
  return `${studioBasePath(appKey)}/assistants`;
}

export function studioAppsPath(appKey: string): string {
  return `${studioBasePath(appKey)}/apps`;
}

export function studioAppDetailPath(appKey: string, appId: string): string {
  return `${studioAppsPath(appKey)}/${encodeSegment(appId)}`;
}

export function studioAppPublishPath(appKey: string, appId: string): string {
  return `${studioAppDetailPath(appKey, appId)}/publish`;
}

export function studioAssistantDetailPath(appKey: string, assistantId: string): string {
  return `${studioAssistantsPath(appKey)}/${encodeSegment(assistantId)}`;
}

export function studioAssistantPublishPath(appKey: string, assistantId: string): string {
  return `${studioAssistantDetailPath(appKey, assistantId)}/publish`;
}

export function studioLibraryPath(appKey: string): string {
  return `${studioBasePath(appKey)}/library`;
}

export function studioDataPath(appKey: string): string {
  return `${studioBasePath(appKey)}/data`;
}

export function studioPluginsPath(appKey: string): string {
  return `${studioBasePath(appKey)}/plugins`;
}

export function studioPluginDetailPath(appKey: string, pluginId: string | number): string {
  return `${studioPluginsPath(appKey)}/${encodeSegment(pluginId)}`;
}

export function studioAssistantToolsPath(appKey: string): string {
  return `${studioBasePath(appKey)}/assistant-tools`;
}

export function studioKnowledgeBasesPath(appKey: string): string {
  return `${studioBasePath(appKey)}/knowledge-bases`;
}

export function studioKnowledgeBaseDetailPath(appKey: string, knowledgeBaseId: string | number): string {
  return `${studioKnowledgeBasesPath(appKey)}/${encodeSegment(knowledgeBaseId)}`;
}

export function studioKnowledgeBaseUploadPath(
  appKey: string,
  knowledgeBaseId: string | number,
  query?: Record<string, string | null | undefined>
): string {
  return withQuery(`${studioKnowledgeBaseDetailPath(appKey, knowledgeBaseId)}/upload`, query);
}

export function studioDatabasesPath(appKey: string): string {
  return `${studioBasePath(appKey)}/databases`;
}

export function studioDatabaseDetailPath(appKey: string, databaseId: string | number): string {
  return `${studioDatabasesPath(appKey)}/${encodeSegment(databaseId)}`;
}

export function studioVariablesPath(appKey: string): string {
  return `${studioBasePath(appKey)}/variables`;
}

export function workflowsAliasPath(appKey: string): string {
  return `${appRootPath(appKey)}/workflows`;
}

export function workflowEditorAliasPath(appKey: string, workflowId: string): string {
  return `${workflowsAliasPath(appKey)}/${encodeSegment(workflowId)}/editor`;
}

export function chatflowsAliasPath(appKey: string): string {
  return `${appRootPath(appKey)}/chatflows`;
}

export function chatflowEditorAliasPath(appKey: string, workflowId: string): string {
  return `${chatflowsAliasPath(appKey)}/${encodeSegment(workflowId)}/editor`;
}

export function workspaceBotPath(appKey: string, spaceId: string, botId: string): string {
  return `${workspaceBasePath(appKey, spaceId)}/bot/${encodeSegment(botId)}`;
}

export function knowledgeDetailPath(
  appKey: string,
  spaceId: string,
  knowledgeBaseId: string | number,
  query?: Record<string, string | null | undefined>
): string {
  return withQuery(
    `${workspaceBasePath(appKey, spaceId)}/knowledge/${encodeSegment(knowledgeBaseId)}`,
    query
  );
}

export function knowledgeUploadPath(
  appKey: string,
  spaceId: string,
  knowledgeBaseId: string | number,
  query?: Record<string, string | null | undefined>
): string {
  return withQuery(
    `${workspaceBasePath(appKey, spaceId)}/knowledge/${encodeSegment(knowledgeBaseId)}/upload`,
    query
  );
}

export function workflowListPath(appKey: string): string {
  return `${appRootPath(appKey)}/work_flow`;
}

export function workflowEditorPath(appKey: string, workflowId: string): string {
  return `${workflowListPath(appKey)}/${encodeSegment(workflowId)}/editor`;
}

export function explorePath(appKey: string, leaf: ExploreLeaf): string {
  return `${appRootPath(appKey)}/explore/${leaf}`;
}

export function explorePluginDetailPath(appKey: string, productId: string | number): string {
  return `${explorePath(appKey, "plugin")}/${encodeSegment(productId)}`;
}

export function exploreTemplateDetailPath(appKey: string, templateId: string | number): string {
  return `${explorePath(appKey, "template")}/${encodeSegment(templateId)}`;
}

export function searchPath(appKey: string, keyword: string): string {
  return `${appRootPath(appKey)}/search/${encodeSegment(keyword)}`;
}

export function adminPath(appKey: string, leaf: AdminLeaf): string {
  return `${appRootPath(appKey)}/admin/${leaf}`;
}

export function inferPrimaryArea(pathname: string): AppPrimaryArea {
  if (pathname.includes("/explore/") || pathname.includes("/search/")) {
    return "explore";
  }

  if (pathname.includes("/admin/")) {
    return "admin";
  }

  return "workspace";
}
