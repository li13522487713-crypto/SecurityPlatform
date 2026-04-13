import type { NodeSchema, NodeTypeMetadata, WorkflowDetailResponse, WorkflowVersionItem } from "@atlas/workflow-core-react";
import type {
  AiDatabaseListItem,
  AiPluginListItem,
  ConversationListItem,
  KnowledgeBaseListItem,
  ResourceIdeItem,
  ResourceIdeTab,
  WorkflowListItem,
  WorkflowResourceMode
} from "./types";
import type { WorkflowModuleCopy } from "./copy";

export type WorkflowSidebarAction =
  | { type: "route"; workflowId: string; mode: WorkflowResourceMode }
  | { type: "tab"; tab: ResourceIdeTab }
  | { type: "noop" };

export interface WorkflowSidebarItem extends ResourceIdeItem {
  key: string;
  hint?: string;
  action: WorkflowSidebarAction;
}

export interface WorkflowSidebarSection {
  key: string;
  title: string;
  emptyText?: string;
  items: WorkflowSidebarItem[];
}

export function normalizeWorkflowMode(item: Pick<WorkflowListItem, "mode">): WorkflowResourceMode {
  return item.mode === 1 ? "chatflow" : "workflow";
}

export function filterWorkflowItems(
  items: WorkflowListItem[],
  mode: WorkflowResourceMode,
  keyword: string
): WorkflowListItem[] {
  const normalizedKeyword = keyword.trim().toLowerCase();
  return items
    .filter(item => normalizeWorkflowMode(item) === mode)
    .filter(item => {
      if (!normalizedKeyword) {
        return true;
      }

      return `${item.name} ${item.description ?? ""}`.toLowerCase().includes(normalizedKeyword);
    })
    .slice(0, 20);
}

function buildWorkflowSection(params: {
  copy: WorkflowModuleCopy;
  mode: WorkflowResourceMode;
  currentWorkflowId: string;
  workflowItems: WorkflowListItem[];
  keyword: string;
}): WorkflowSidebarSection {
  const filteredItems = filterWorkflowItems(params.workflowItems, params.mode, params.keyword);

  return {
    key: "workflow",
    title: params.copy.sectionWorkflow,
    items: filteredItems.map(item => ({
      key: item.id,
      id: item.id,
      resourceType: normalizeWorkflowMode(item),
      name: item.name,
      description: item.description,
      hint: item.description,
      status: item.status === 1 ? params.copy.publishedStatus : params.copy.draftStatus,
      badge: item.id === params.currentWorkflowId ? params.copy.currentWorkflowLabel : undefined,
      active: item.id === params.currentWorkflowId,
      canDelete: true,
      canRename: true,
      mode: normalizeWorkflowMode(item),
      action: {
        type: "route",
        workflowId: item.id,
        mode: normalizeWorkflowMode(item)
      }
    })),
    emptyText: params.copy.emptyWorkflow
  };
}

function buildPluginSection(copy: WorkflowModuleCopy, items: AiPluginListItem[], keyword: string): WorkflowSidebarSection {
  const normalizedKeyword = keyword.trim().toLowerCase();
  const filteredItems = items
    .filter(item => `${item.name} ${item.description ?? ""}`.toLowerCase().includes(normalizedKeyword))
    .slice(0, 20);

  return {
    key: "plugin",
    title: copy.sectionPlugin,
    items: filteredItems.map(item => ({
      key: `plugin-${item.id}`,
      id: String(item.id),
      resourceType: "plugin",
      name: item.name,
      description: item.description,
      hint: item.description,
      status: item.status === 1 ? copy.publishedStatus : copy.draftStatus,
      canDelete: true,
      canRename: true,
      action: {
        type: "tab",
        tab: {
          key: `plugin-${item.id}`,
          kind: "plugin",
          resourceId: String(item.id),
          title: item.name,
          closable: true
        }
      }
    })),
    emptyText: copy.emptyPlugin
  };
}

function buildDataSection(
  copy: WorkflowModuleCopy,
  knowledgeItems: KnowledgeBaseListItem[],
  databaseItems: AiDatabaseListItem[],
  keyword: string
): WorkflowSidebarSection {
  const normalizedKeyword = keyword.trim().toLowerCase();
  const dataItems: WorkflowSidebarItem[] = [
    ...knowledgeItems
      .filter(item => `${item.name} ${item.description ?? ""}`.toLowerCase().includes(normalizedKeyword))
      .slice(0, 10)
      .map(item => ({
        key: `knowledge-${item.id}`,
        id: String(item.id),
        resourceType: "knowledge-base" as const,
        name: item.name,
        description: item.description,
        hint: item.description,
        status: `${item.documentCount}${copy.documentUnit} / ${item.chunkCount}${copy.chunkUnit}`,
        canDelete: true,
        canRename: true,
        action: {
          type: "tab" as const,
          tab: {
            key: `knowledge-${item.id}`,
            kind: "knowledge" as const,
            resourceId: String(item.id),
            title: item.name,
            closable: true
          }
        }
      })),
    ...databaseItems
      .filter(item => `${item.name} ${item.description ?? ""}`.toLowerCase().includes(normalizedKeyword))
      .slice(0, 10)
      .map(item => ({
        key: `database-${item.id}`,
        id: String(item.id),
        resourceType: "database" as const,
        name: item.name,
        description: item.description,
        hint: item.description,
        status: `${item.recordCount}${copy.recordUnit}`,
        canDelete: true,
        canRename: true,
        action: {
          type: "tab" as const,
          tab: {
            key: `database-${item.id}`,
            kind: "database" as const,
            resourceId: String(item.id),
            title: item.name,
            closable: true
          }
        }
      }))
  ];

  return {
    key: "data",
    title: copy.sectionData,
    items: dataItems,
    emptyText: copy.emptyData
  };
}

function buildSettingsSection(copy: WorkflowModuleCopy, conversations: ConversationListItem[]): WorkflowSidebarSection {
  const items: WorkflowSidebarItem[] = [
    {
      key: "conversations",
      id: "conversations",
      resourceType: "conversations",
      name: copy.conversationManagement,
      description: copy.conversationDescription,
      hint: copy.conversationDescription,
      status: conversations.length > 0 ? `${conversations.length}${copy.conversationUnit}` : undefined,
      action: {
        type: "tab",
        tab: {
          key: "conversations",
          kind: "conversations",
          title: copy.conversationManagement,
          closable: true
        }
      }
    },
    {
      key: "variables",
      id: "variables",
      resourceType: "variables",
      name: copy.variablesLabel,
      description: copy.variablesDescription,
      hint: copy.variablesDescription,
      action: {
        type: "tab",
        tab: {
          key: "variables",
          kind: "variables",
          title: copy.variablesLabel,
          closable: true
        }
      }
    }
  ];

  return {
    key: "settings",
    title: copy.sectionSettings,
    items
  };
}

export function buildResourceSidebarSections(params: {
  copy: WorkflowModuleCopy;
  mode: WorkflowResourceMode;
  currentWorkflowId: string;
  workflowItems: WorkflowListItem[];
  pluginItems: AiPluginListItem[];
  knowledgeItems: KnowledgeBaseListItem[];
  databaseItems: AiDatabaseListItem[];
  conversations: ConversationListItem[];
  keyword: string;
}): WorkflowSidebarSection[] {
  return [
    buildWorkflowSection(params),
    buildPluginSection(params.copy, params.pluginItems, params.keyword),
    buildDataSection(params.copy, params.knowledgeItems, params.databaseItems, params.keyword),
    buildSettingsSection(params.copy, params.conversations)
  ];
}

export function buildReferenceSidebarSections(params: {
  copy: WorkflowModuleCopy;
  detail: WorkflowDetailResponse | null;
  versions: WorkflowVersionItem[];
  nodes: NodeSchema[];
  nodeTypes: NodeTypeMetadata[];
}): WorkflowSidebarSection[] {
  const nodeMap = new Map(params.nodeTypes.map(item => [item.key, item.name]));
  const nodeItems = params.nodes.slice(0, 12).map(node => ({
    key: `node-${node.key}`,
    id: node.key,
    resourceType: "workflow" as const,
    name: node.title || node.key,
    hint: nodeMap.get(node.type) ?? String(node.type),
    badge: nodeMap.get(node.type),
    action: { type: "noop" } as const
  }));

  const versionItems = params.versions.slice(0, 8).map(version => ({
    key: version.id,
    id: version.id,
    resourceType: "workflow" as const,
    name: `v${version.versionNumber}`,
    hint: version.publishedAt,
    action: { type: "noop" } as const
  }));

  return [
    {
      key: "reference-nodes",
      title: params.copy.sectionReferences,
      items: nodeItems.length > 0
        ? nodeItems
        : [
            {
              key: "reference-empty",
              id: "reference-empty",
              resourceType: "workflow",
              name: params.copy.emptyReferences,
              action: { type: "noop" }
            }
          ],
      emptyText: params.copy.emptyReferences
    },
    {
      key: "reference-versions",
      title: params.copy.relatedVersionsLabel,
      items: versionItems
    }
  ];
}

export function buildInitialWorkflowTab(workflowId: string, title: string, mode: WorkflowResourceMode): ResourceIdeTab {
  return {
    key: `${mode}-${workflowId}`,
    kind: mode === "chatflow" ? "chatflow-editor" : "workflow-editor",
    resourceId: workflowId,
    title,
    closable: false,
    mode
  };
}

export function ensureWorkflowTab(
  tabs: ResourceIdeTab[],
  workflowId: string,
  title: string,
  mode: WorkflowResourceMode
): ResourceIdeTab[] {
  const nextKey = `${mode}-${workflowId}`;
  const existingIndex = tabs.findIndex(tab => tab.key === nextKey);
  if (existingIndex >= 0) {
    return tabs.map(tab => tab.key === nextKey ? { ...tab, title } : tab);
  }

  return [buildInitialWorkflowTab(workflowId, title, mode), ...tabs];
}
