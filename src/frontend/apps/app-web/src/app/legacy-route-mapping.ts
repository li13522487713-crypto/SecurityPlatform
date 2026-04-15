import type {
  WorkflowWorkbenchContentMode,
  WorkflowResourceMode
} from "@atlas/module-workflow-react";
import {
  orgWorkspaceAgentDetailPath,
  orgWorkspaceAgentPublishPath,
  orgWorkspaceAppDetailPath,
  orgWorkspaceAppPublishPath,
  orgWorkspaceAssistantToolsPath,
  orgWorkspaceChatPath,
  orgWorkspaceChatflowPath,
  orgWorkspaceChatflowsPath,
  orgWorkspaceDashboardPath,
  orgWorkspaceDataPath,
  orgWorkspaceDatabaseDetailPath,
  orgWorkspaceDevelopPath,
  orgWorkspaceKnowledgeBaseDetailPath,
  orgWorkspaceKnowledgeBaseUploadPath,
  orgWorkspaceLibraryPath,
  orgWorkspaceManagePath,
  orgWorkspaceModelConfigsPath,
  orgWorkspacePluginDetailPath,
  orgWorkspacePublishCenterPath,
  orgWorkspaceSettingsPath,
  orgWorkspaceVariablesPath,
  orgWorkspaceWorkflowPath,
  orgWorkspaceWorkflowsPath
} from "@atlas/app-shell-shared";

export interface LegacyAppRedirectContext {
  orgId: string;
  workspaceId: string;
  relativePath: string;
  searchText: string;
}

export function normalizeWorkbenchContentMode(value?: string | null): WorkflowWorkbenchContentMode {
  return value === "session" || value === "variables" ? value : "canvas";
}

export function buildWorkspaceWorkbenchPath(
  orgId: string,
  workspaceId: string,
  mode: WorkflowResourceMode,
  workflowId?: string,
  contentMode: WorkflowWorkbenchContentMode = "canvas",
  searchParams?: URLSearchParams
): string {
  const pathname = workflowId
    ? mode === "chatflow"
      ? orgWorkspaceChatflowPath(orgId, workspaceId, workflowId)
      : orgWorkspaceWorkflowPath(orgId, workspaceId, workflowId)
    : mode === "chatflow"
      ? orgWorkspaceChatflowsPath(orgId, workspaceId)
      : orgWorkspaceWorkflowsPath(orgId, workspaceId);

  const nextSearch = new URLSearchParams(searchParams ?? undefined);
  if (contentMode === "canvas") {
    nextSearch.delete("contentMode");
  } else {
    nextSearch.set("contentMode", contentMode);
  }

  if (workflowId) {
    nextSearch.delete("workflow_id");
  }

  const query = nextSearch.toString();
  return query ? `${pathname}?${query}` : pathname;
}

export function resolveLegacyWorkbenchRedirectTarget({
  orgId,
  workspaceId,
  relativePath,
  searchText
}: LegacyAppRedirectContext): string | null {
  const searchParams = new URLSearchParams(searchText);
  const workflowEditorMatch = relativePath.match(/^\/work_flow\/([^/]+)\/editor$/);
  const chatflowEditorMatch = relativePath.match(/^\/chat_flow\/([^/]+)\/editor$/);
  const workflowAliasEditorMatch = relativePath.match(/^\/workflows\/([^/]+)\/editor$/);
  const chatflowAliasEditorMatch = relativePath.match(/^\/chatflows\/([^/]+)\/editor$/);
  const workflowId = workflowEditorMatch?.[1]
    ?? chatflowEditorMatch?.[1]
    ?? workflowAliasEditorMatch?.[1]
    ?? chatflowAliasEditorMatch?.[1]
    ?? searchParams.get("workflow_id")
    ?? undefined;

  if (
    relativePath.startsWith("/work_flow")
    || relativePath.startsWith("/workflows")
    || relativePath.startsWith("/chat_flow")
    || relativePath.startsWith("/chatflows")
  ) {
    const mode: WorkflowResourceMode =
      relativePath.startsWith("/chat_flow") || relativePath.startsWith("/chatflows")
        ? "chatflow"
        : "workflow";
    const contentMode: WorkflowWorkbenchContentMode = relativePath.includes("/session")
      ? "session"
      : relativePath.includes("/variables")
        ? "variables"
        : normalizeWorkbenchContentMode(searchParams.get("contentMode"));

    return buildWorkspaceWorkbenchPath(
      orgId,
      workspaceId,
      mode,
      workflowId,
      contentMode,
      searchParams
    );
  }

  return null;
}

export function resolveLegacyAppRedirectTarget(context: LegacyAppRedirectContext): string {
  const { orgId, workspaceId, relativePath, searchText } = context;
  const searchParams = new URLSearchParams(searchText);

  const workbenchTarget = resolveLegacyWorkbenchRedirectTarget(context);
  if (workbenchTarget) {
    return workbenchTarget;
  }

  const knowledgeUploadMatch = relativePath.match(/^\/studio\/knowledge-bases\/([^/]+)\/upload(\?.*)?$/);
  if (knowledgeUploadMatch) {
    return `${orgWorkspaceKnowledgeBaseUploadPath(orgId, workspaceId, decodeURIComponent(knowledgeUploadMatch[1]))}${knowledgeUploadMatch[2] ?? ""}`;
  }

  const studioKnowledgeDetailMatch = relativePath.match(/^\/studio\/knowledge-bases\/([^/?]+)$/);
  if (studioKnowledgeDetailMatch) {
    return orgWorkspaceKnowledgeBaseDetailPath(orgId, workspaceId, decodeURIComponent(studioKnowledgeDetailMatch[1]));
  }

  const studioAgentPublishMatch = relativePath.match(/^\/studio\/assistants\/([^/]+)\/publish$/);
  if (studioAgentPublishMatch) {
    return orgWorkspaceAgentPublishPath(orgId, workspaceId, decodeURIComponent(studioAgentPublishMatch[1]));
  }

  const studioAgentDetailMatch = relativePath.match(/^\/studio\/assistants\/([^/?]+)$/);
  if (studioAgentDetailMatch) {
    return orgWorkspaceAgentDetailPath(orgId, workspaceId, decodeURIComponent(studioAgentDetailMatch[1]));
  }

  const studioAppPublishMatch = relativePath.match(/^\/studio\/apps\/([^/]+)\/publish$/);
  if (studioAppPublishMatch) {
    return orgWorkspaceAppPublishPath(orgId, workspaceId, decodeURIComponent(studioAppPublishMatch[1]));
  }

  const studioAppDetailMatch = relativePath.match(/^\/studio\/apps\/([^/?]+)$/);
  if (studioAppDetailMatch) {
    return orgWorkspaceAppDetailPath(orgId, workspaceId, decodeURIComponent(studioAppDetailMatch[1]));
  }

  const studioPluginDetailMatch = relativePath.match(/^\/studio\/plugins\/([^/?]+)$/);
  if (studioPluginDetailMatch) {
    return orgWorkspacePluginDetailPath(orgId, workspaceId, decodeURIComponent(studioPluginDetailMatch[1]));
  }

  const studioDatabaseDetailMatch = relativePath.match(/^\/studio\/databases\/([^/?]+)$/);
  if (studioDatabaseDetailMatch) {
    return orgWorkspaceDatabaseDetailPath(orgId, workspaceId, decodeURIComponent(studioDatabaseDetailMatch[1]));
  }

  const workspaceBotMatch = relativePath.match(/^\/space\/[^/]+\/bot\/([^/?]+)$/);
  if (workspaceBotMatch) {
    return orgWorkspaceAgentDetailPath(orgId, workspaceId, decodeURIComponent(workspaceBotMatch[1]));
  }

  const workspaceBotPublishMatch = relativePath.match(/^\/space\/[^/]+\/bot\/([^/]+)\/publish$/);
  if (workspaceBotPublishMatch) {
    return orgWorkspaceAgentPublishPath(orgId, workspaceId, decodeURIComponent(workspaceBotPublishMatch[1]));
  }

  const workspaceKnowledgeUploadMatch = relativePath.match(/^\/space\/[^/]+\/knowledge\/([^/]+)\/upload$/);
  if (workspaceKnowledgeUploadMatch) {
    return orgWorkspaceKnowledgeBaseUploadPath(orgId, workspaceId, decodeURIComponent(workspaceKnowledgeUploadMatch[1]), {
      type: searchParams.get("type")
    });
  }

  const workspaceKnowledgeDetailMatch = relativePath.match(/^\/space\/[^/]+\/knowledge\/([^/?]+)$/);
  if (workspaceKnowledgeDetailMatch) {
    return orgWorkspaceKnowledgeBaseDetailPath(orgId, workspaceId, decodeURIComponent(workspaceKnowledgeDetailMatch[1]));
  }

  if (relativePath.startsWith("/studio/dashboard")) {
    return orgWorkspaceDashboardPath(orgId, workspaceId);
  }

  if (relativePath.startsWith("/studio/publish-center")) {
    return orgWorkspacePublishCenterPath(orgId, workspaceId);
  }

  if (relativePath.startsWith("/studio/assistant-tools")) {
    return orgWorkspaceAssistantToolsPath(orgId, workspaceId);
  }

  if (relativePath.startsWith("/studio/data")) {
    return orgWorkspaceDataPath(orgId, workspaceId);
  }

  if (relativePath.startsWith("/studio/variables")) {
    return orgWorkspaceVariablesPath(orgId, workspaceId);
  }

  if (
    relativePath.startsWith("/studio/library")
    || relativePath.startsWith("/studio/knowledge-bases")
    || relativePath.startsWith("/studio/plugins")
    || relativePath.startsWith("/studio/databases")
  ) {
    return orgWorkspaceLibraryPath(orgId, workspaceId);
  }

  if (
    relativePath.startsWith("/studio/develop")
    || relativePath.startsWith("/studio/apps")
    || relativePath.startsWith("/studio/assistants")
  ) {
    return orgWorkspaceDevelopPath(orgId, workspaceId);
  }

  if (relativePath.startsWith("/admin/profile")) {
    return orgWorkspaceSettingsPath(orgId, workspaceId, "profile");
  }

  if (relativePath.startsWith("/admin/settings")) {
    return orgWorkspaceSettingsPath(orgId, workspaceId, "system");
  }

  const adminLeafMatch = relativePath.match(/^\/admin\/([^/?]+)/);
  if (adminLeafMatch) {
    const leaf = adminLeafMatch[1];
    switch (leaf) {
      case "overview":
      case "users":
      case "roles":
      case "departments":
      case "positions":
      case "approval":
      case "reports":
      case "dashboards":
      case "visualization":
        return orgWorkspaceManagePath(orgId, workspaceId, leaf);
      default:
        break;
    }
  }

  if (relativePath.startsWith("/space/")) {
    if (relativePath.includes("/chat")) {
      return orgWorkspaceChatPath(orgId, workspaceId);
    }

    if (relativePath.includes("/model-configs")) {
      return orgWorkspaceModelConfigsPath(orgId, workspaceId);
    }

    if (relativePath.includes("/assistant")) {
      return orgWorkspaceAssistantToolsPath(orgId, workspaceId);
    }

    if (relativePath.includes("/library")) {
      return orgWorkspaceLibraryPath(orgId, workspaceId);
    }

    if (relativePath.includes("/dashboard")) {
      return orgWorkspaceDashboardPath(orgId, workspaceId);
    }

    return orgWorkspaceDevelopPath(orgId, workspaceId);
  }

  return orgWorkspaceDashboardPath(orgId, workspaceId);
}
