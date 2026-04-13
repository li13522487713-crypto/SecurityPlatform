import type { NodeSchema, NodeTypeMetadata, WorkflowDetailResponse, WorkflowVersionItem } from "@atlas/workflow-core-react";
import type { WorkflowListItem, WorkflowResourceMode } from "./types";
import type { WorkflowModuleCopy } from "./copy";

export type WorkflowSidebarAction =
  | { type: "workflow"; workflowId: string }
  | { type: "command"; command: "openVariables" | "openTrace" | "openTestRun" | "openProblems" | "openNodePanel" | "openDebug" }
  | { type: "noop" };

export interface WorkflowSidebarItem {
  key: string;
  label: string;
  badge?: string;
  hint?: string;
  disabled?: boolean;
  active?: boolean;
  action: WorkflowSidebarAction;
}

export interface WorkflowSidebarSection {
  key: string;
  title: string;
  items: WorkflowSidebarItem[];
  emptyText?: string;
}

export function filterWorkflowItems(
  items: WorkflowListItem[],
  mode: WorkflowResourceMode,
  keyword: string
): WorkflowListItem[] {
  const normalizedKeyword = keyword.trim().toLowerCase();
  return items
    .filter(item => (item.mode === 1 ? "chatflow" : "workflow") === mode)
    .filter(item => {
      if (!normalizedKeyword) {
        return true;
      }
      return `${item.name} ${item.description ?? ""}`.toLowerCase().includes(normalizedKeyword);
    })
    .slice(0, 20);
}

export function buildResourceSidebarSections(params: {
  copy: WorkflowModuleCopy;
  mode: WorkflowResourceMode;
  currentWorkflowId: string;
  workflowItems: WorkflowListItem[];
  keyword: string;
}): WorkflowSidebarSection[] {
  const filteredItems = filterWorkflowItems(params.workflowItems, params.mode, params.keyword);
  const workflowItems = filteredItems.map(item => ({
    key: item.id,
    label: item.name,
    badge: item.id === params.currentWorkflowId ? params.copy.currentWorkflowLabel : undefined,
    hint: item.description,
    active: item.id === params.currentWorkflowId,
    action: { type: "workflow", workflowId: item.id } as const
  }));

  return [
    {
      key: "workflow",
      title: params.mode === "chatflow" ? params.copy.chatflowLabel : params.copy.sectionWorkflow,
      items: workflowItems
    },
    {
      key: "plugin",
      title: params.copy.sectionPlugin,
      items: [
        {
          key: "plugin-empty",
          label: params.copy.emptyPlugin,
          disabled: true,
          action: { type: "noop" }
        }
      ],
      emptyText: params.copy.emptyPlugin
    },
    {
      key: "data",
      title: params.copy.sectionData,
      items: [
        {
          key: "data-empty",
          label: params.copy.emptyData,
          disabled: true,
          action: { type: "noop" }
        }
      ],
      emptyText: params.copy.emptyData
    },
    {
      key: "settings",
      title: params.copy.sectionSettings,
      items: [
        {
          key: "settings-conversation",
          label: params.copy.conversationManagement,
          disabled: params.mode !== "chatflow",
          action: { type: "command", command: "openDebug" }
        },
        {
          key: "settings-variables",
          label: params.copy.variablesLabel,
          action: { type: "command", command: "openVariables" }
        },
        {
          key: "settings-trace",
          label: params.copy.traceLabel,
          action: { type: "command", command: "openTrace" }
        },
        {
          key: "settings-test-run",
          label: params.copy.testRunLabel,
          action: { type: "command", command: "openTestRun" }
        },
        {
          key: "settings-problems",
          label: params.copy.problemsLabel,
          action: { type: "command", command: "openProblems" }
        },
        {
          key: "settings-add-node",
          label: params.copy.addNodeLabel,
          action: { type: "command", command: "openNodePanel" }
        }
      ]
    }
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
    label: node.title || node.key,
    hint: nodeMap.get(node.type) ?? String(node.type),
    badge: nodeMap.get(node.type),
    action: { type: "noop" } as const
  }));

  const versionItems = params.versions.slice(0, 8).map(version => ({
    key: version.id,
    label: `v${version.versionNumber}`,
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
              label: params.copy.emptyReferences,
              disabled: true,
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

