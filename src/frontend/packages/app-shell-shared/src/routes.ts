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
