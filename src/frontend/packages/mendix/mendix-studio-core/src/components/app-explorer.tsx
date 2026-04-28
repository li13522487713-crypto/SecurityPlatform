import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Modal, Toast } from "@douyinfe/semi-ui";

import { useMendixStudioStore } from "../store";
import type { ActiveTabId, MendixStudioTab } from "../store";
import { SAMPLE_PROCUREMENT_APP } from "../sample-app";
import type { MicroflowAdapterBundle } from "../microflow/adapter/microflow-adapter-factory";
import { getMicroflowApiError } from "../microflow/adapter/http/microflow-api-error";
import type { MicroflowApiError } from "../microflow/contracts/api/api-envelope";
import { canDeleteMicroflowFromReferences, getActiveReferences, resolveReferenceDisplayName } from "../microflow/references/microflow-reference-utils";
import type { StudioMicroflowDefinitionView } from "../microflow/studio/studio-microflow-types";
import { mapMicroflowResourceToStudioDefinitionView } from "../microflow/studio/studio-microflow-mappers";
import { AppExplorerTree } from "./app-explorer-tree";
import { createMicroflowStateChildren, MicroflowsSectionKey } from "./microflow-tree-section";

export type ExplorerTreeNodeKind =
  | "module"
  | "folder"
  | "entity"
  | "page"
  | "microflow"
  | "workflow"
  | "security"
  | "navigation"
  | "constant"
  | "theme"
  | "loading"
  | "empty"
  | "error";

export interface ExplorerTreeNode {
  key: string;
  label: string;
  icon?: string;
  kind?: ExplorerTreeNodeKind;
  tabId?: ActiveTabId;
  studioTab?: MendixStudioTab;
  moduleId?: string;
  microflowId?: string;
  resourceId?: string;
  name?: string;
  displayName?: string;
  qualifiedName?: string;
  status?: StudioMicroflowDefinitionView["status"];
  publishStatus?: StudioMicroflowDefinitionView["publishStatus"];
  referenceCount?: number;
  readonly?: boolean;
  dynamic?: boolean;
  title?: string;
  errorMessage?: string;
  error?: MicroflowApiError;
  action?: "retryMicroflows";
  children?: ExplorerTreeNode[];
  defaultOpen?: boolean;
}

export interface AppExplorerProps {
  adapterBundle?: MicroflowAdapterBundle;
  workspaceId?: string;
  refreshToken?: number;
  onViewMicroflowReferences?: (microflowId: string) => void;
}

export type MicroflowLoadStatus = "idle" | "loading" | "success" | "error";

const SAMPLE_PROCUREMENT_MODULE = SAMPLE_PROCUREMENT_APP.modules[0];
const explorerMicroflowRequests = new Map<string, Promise<StudioMicroflowDefinitionView[]>>();

export function getCurrentExplorerModuleId(node?: Pick<ExplorerTreeNode, "moduleId">): string | undefined {
  return node?.moduleId ?? SAMPLE_PROCUREMENT_MODULE?.moduleId;
}

const STATIC_TREE_DATA: ExplorerTreeNode[] = [
  {
    key: "procurement",
    label: SAMPLE_PROCUREMENT_MODULE?.name ?? "Procurement",
    kind: "module",
    moduleId: SAMPLE_PROCUREMENT_MODULE?.moduleId,
    defaultOpen: true,
    children: [
      {
        key: "domain-model",
        label: "Domain Model",
        kind: "folder",
        defaultOpen: true,
        children: [
          {
            key: "entities",
            label: "实体",
            kind: "folder",
            defaultOpen: true,
            children: [
              { key: "ent_purchase_request", label: "PurchaseRequest", icon: "E", kind: "entity" },
              { key: "ent_department", label: "Department", icon: "E", kind: "entity" },
              { key: "ent_account", label: "Account", icon: "E", kind: "entity" },
              { key: "ent_approval_comment", label: "ApprovalComment", icon: "E", kind: "entity" }
            ]
          },
          {
            key: "enumerations",
            label: "枚举",
            kind: "folder",
            children: [{ key: "enum_purchase_status", label: "PurchaseStatus", icon: "E", kind: "entity" }]
          },
          {
            key: "associations",
            label: "关联",
            kind: "folder",
            children: [
              { key: "assoc_applicant", label: "PurchaseRequest_Applicant", icon: "A", kind: "entity" },
              { key: "assoc_department", label: "PurchaseRequest_Department", icon: "A", kind: "entity" },
              { key: "assoc_approval", label: "PurchaseRequest_ApprovalComment", icon: "A", kind: "entity" }
            ]
          }
        ]
      },
      {
        key: "pages",
        label: "Pages",
        kind: "folder",
        defaultOpen: true,
        children: [
          {
            key: "page_purchase_request_edit",
            label: "PurchaseRequest_EditPage",
            icon: "P",
            kind: "page",
            tabId: "page",
            studioTab: "pageBuilder"
          }
        ]
      },
      {
        key: MicroflowsSectionKey,
        label: "Microflows",
        kind: "folder",
        moduleId: SAMPLE_PROCUREMENT_MODULE?.moduleId,
        defaultOpen: true,
        children: []
      },
      {
        key: "workflows",
        label: "Workflows",
        kind: "folder",
        defaultOpen: true,
        children: [
          {
            key: "wf_purchase_approval",
            label: "WF_PurchaseApproval",
            icon: "W",
            kind: "workflow",
            tabId: "workflow",
            studioTab: "workflowDesigner"
          }
        ]
      },
      {
        key: "security",
        label: "Security",
        kind: "security",
        children: [
          { key: "user-roles", label: "用户角色" },
          { key: "module-roles", label: "模块角色" },
          { key: "permission-matrix", label: "权限矩阵" },
          { key: "entity-access", label: "实体访问" }
        ]
      },
      { key: "navigation", label: "Navigation", kind: "navigation" },
      { key: "constants", label: "Constants", kind: "constant" },
      { key: "theme", label: "Theme", kind: "theme" }
    ]
  }
];

function sortMicroflowViews(items: StudioMicroflowDefinitionView[]): StudioMicroflowDefinitionView[] {
  return [...items].sort((left, right) => {
    const leftLabel = (left.displayName || left.name).toLocaleLowerCase();
    const rightLabel = (right.displayName || right.name).toLocaleLowerCase();
    return leftLabel.localeCompare(rightLabel);
  });
}

function withMicroflowChildren(nodes: ExplorerTreeNode[], children: ExplorerTreeNode[]): ExplorerTreeNode[] {
  return nodes.map(node => ({
    ...node,
    children: node.key === MicroflowsSectionKey
      ? children
      : node.children
        ? withMicroflowChildren(node.children, children)
        : node.children
  }));
}

function formatMicroflowListError(error: MicroflowApiError): string {
  const parts = [error.message || "Load failed"];
  if (error.httpStatus) {
    parts.push(`status ${error.httpStatus}`);
  }
  if (error.code) {
    parts.push(error.code);
  }
  if (error.traceId) {
    parts.push(`traceId ${error.traceId}`);
  }
  return parts.join(" · ");
}

export function AppExplorerContainer({ adapterBundle, workspaceId, refreshToken, onViewMicroflowReferences }: AppExplorerProps) {
  const [searchText, setSearchText] = useState("");
  const [microflowStatus, setMicroflowStatus] = useState<MicroflowLoadStatus>("idle");
  const [microflowError, setMicroflowError] = useState<MicroflowApiError>();
  const [microflows, setMicroflows] = useState<StudioMicroflowDefinitionView[]>([]);
  const lastRequestKeyRef = useRef<string>();

  const selectedExplorerNodeId = useMendixStudioStore(state => state.selectedExplorerNodeId);
  const activeModuleId = useMendixStudioStore(state => state.activeModuleId);
  const setSelectedExplorerNodeId = useMendixStudioStore(state => state.setSelectedExplorerNodeId);
  const setActiveWorkbenchTab = useMendixStudioStore(state => state.setActiveWorkbenchTab);
  const setSelected = useMendixStudioStore(state => state.setSelected);
  const setActiveModuleId = useMendixStudioStore(state => state.setActiveModuleId);
  const setActiveMicroflowId = useMendixStudioStore(state => state.setActiveMicroflowId);
  const setModuleMicroflows = useMendixStudioStore(state => state.setModuleMicroflows);
  const openMicroflowWorkbenchTab = useMendixStudioStore(state => state.openMicroflowWorkbenchTab);
  const removeStudioMicroflow = useMendixStudioStore(state => state.removeStudioMicroflow);

  const moduleNode = useMemo(() => STATIC_TREE_DATA[0], []);
  const moduleId = getCurrentExplorerModuleId(moduleNode);

  const loadMicroflows = useCallback(async (force = false) => {
    if (!workspaceId) {
      setMicroflowStatus("error");
      setMicroflowError({ code: "MICROFLOW_VALIDATION_FAILED", message: "无法加载：缺少 workspaceId" });
      setMicroflows([]);
      return;
    }
    if (!moduleId) {
      setMicroflowStatus("error");
      setMicroflowError({ code: "MICROFLOW_VALIDATION_FAILED", message: "无法加载：缺少 moduleId" });
      setMicroflows([]);
      return;
    }
    if (!adapterBundle?.resourceAdapter) {
      setMicroflowStatus("error");
      setMicroflowError({ code: "MICROFLOW_SERVICE_UNAVAILABLE", message: "无法加载：缺少 microflow resource adapter" });
      setMicroflows([]);
      return;
    }

    const requestKey = `${workspaceId}:${moduleId}:${adapterBundle.mode}`;
    if (force) {
      explorerMicroflowRequests.delete(requestKey);
      lastRequestKeyRef.current = undefined;
    }
    if (!force && lastRequestKeyRef.current === requestKey) {
      return;
    }

    lastRequestKeyRef.current = requestKey;
    setMicroflowStatus("loading");
    setMicroflowError(undefined);

    try {
      const request = explorerMicroflowRequests.get(requestKey) ?? adapterBundle.resourceAdapter
        .listMicroflows({
          workspaceId,
          moduleId,
          status: ["draft", "published"],
          sortBy: "name",
          sortOrder: "asc",
          pageIndex: 1,
          pageSize: 100
        })
        .then(result => sortMicroflowViews(result.items.map(mapMicroflowResourceToStudioDefinitionView)))
        .finally(() => {
          explorerMicroflowRequests.delete(requestKey);
        });

      explorerMicroflowRequests.set(requestKey, request);
      const nextMicroflows = await request;
      setMicroflows(nextMicroflows);
      setModuleMicroflows(moduleId, nextMicroflows);
      if (!activeModuleId) {
        setActiveModuleId(moduleId);
      }
      setMicroflowStatus("success");
    } catch (caught) {
      const apiError = getMicroflowApiError(caught);
      setMicroflowStatus("error");
      setMicroflowError(apiError);
      setMicroflows([]);
    }
  }, [activeModuleId, adapterBundle, moduleId, setActiveModuleId, setModuleMicroflows, workspaceId]);

  useEffect(() => {
    void loadMicroflows();
  }, [loadMicroflows]);

  useEffect(() => {
    if (typeof refreshToken === "number" && refreshToken > 0) {
      void loadMicroflows(true);
    }
  }, [loadMicroflows, refreshToken]);

  const refreshMicroflows = useCallback(async () => {
    await loadMicroflows(true);
    Toast.success("微流列表已刷新");
  }, [loadMicroflows]);

  const viewMicroflowReferences = useCallback((node: ExplorerTreeNode) => {
    if (node.kind !== "microflow" || !node.microflowId || node.readonly || !node.dynamic) {
      return;
    }
    setSelectedExplorerNodeId(node.key);
    onViewMicroflowReferences?.(node.microflowId);
  }, [onViewMicroflowReferences, setSelectedExplorerNodeId]);

  const deleteMicroflow = useCallback((node: ExplorerTreeNode) => {
    if (node.kind !== "microflow" || !node.microflowId || node.readonly || !node.dynamic) {
      return;
    }
    const microflowId = node.microflowId;
    const label = node.displayName || node.name || node.label;
    const adapter = adapterBundle?.resourceAdapter;
    if (!adapter) {
      Toast.error("无法验证引用关系：缺少 microflow resource adapter。");
      return;
    }

    void (async () => {
      let references;
      try {
        references = await adapter.getMicroflowReferences(microflowId, { includeInactive: true });
      } catch (caught) {
        Toast.error(`无法验证引用关系，请稍后重试：${getMicroflowApiError(caught).message}`);
        onViewMicroflowReferences?.(microflowId);
        return;
      }

      if (!canDeleteMicroflowFromReferences(references)) {
        const callers = getActiveReferences(references)
          .slice(0, 5)
          .map(reference => resolveReferenceDisplayName(reference, useMendixStudioStore.getState().microflowResourcesById))
          .join("、");
        Toast.error(`删除被阻止：${label} 仍被 ${callers || "active callers"} 引用。`);
        onViewMicroflowReferences?.(microflowId);
        return;
      }

      Modal.confirm({
        title: "确认删除微流",
        content: `已完成 callers 预检查，未发现 active callers。确认删除 ${label}？`,
        okText: "删除",
        cancelText: "取消",
        onOk: async () => {
          try {
            await adapter.deleteMicroflow(microflowId);
            removeStudioMicroflow(microflowId);
            Toast.success("微流已删除");
            await loadMicroflows(true);
          } catch (caught) {
            const apiError = getMicroflowApiError(caught);
            if (apiError.httpStatus === 409 || apiError.code === "MICROFLOW_REFERENCE_BLOCKED" || apiError.code === "MICROFLOW_VERSION_CONFLICT") {
              Toast.error(`删除被后端引用保护阻止：${apiError.message}`);
              onViewMicroflowReferences?.(microflowId);
              await loadMicroflows(true);
              return;
            }
            Toast.error(apiError.message || "删除微流失败");
          }
        }
      });
    })();
  }, [adapterBundle, loadMicroflows, onViewMicroflowReferences, removeStudioMicroflow]);

  const treeData = useMemo(
    () => withMicroflowChildren(STATIC_TREE_DATA, createMicroflowStateChildren(microflowStatus, microflows, microflowError)),
    [microflowError, microflowStatus, microflows]
  );

  const handleSelect = useCallback((node: ExplorerTreeNode) => {
    if (node.action === "retryMicroflows") {
      void loadMicroflows(true);
      return;
    }

    setSelectedExplorerNodeId(node.key);
    if (node.moduleId) {
      setActiveModuleId(node.moduleId);
    }

    if (node.kind === "microflow" && node.microflowId) {
      setSelected("microflow", node.microflowId);
      setActiveMicroflowId(node.microflowId);
      setActiveModuleId(node.moduleId);
      openMicroflowWorkbenchTab(node.microflowId);
      return;
    }

    if (node.tabId) {
      setActiveWorkbenchTab(node.tabId);
    }
    if (node.icon === "E") {
      setSelected("entity", node.key);
    } else if (node.icon === "P") {
      setSelected("page", node.key);
    } else if (node.icon === "W") {
      setSelected("workflow", node.key);
    }
  }, [
    loadMicroflows,
    setActiveMicroflowId,
    setActiveModuleId,
    setActiveWorkbenchTab,
    openMicroflowWorkbenchTab,
    setSelected,
    setSelectedExplorerNodeId
  ]);

  return (
    <AppExplorerTree
      treeData={treeData}
      selectedId={selectedExplorerNodeId}
      searchText={searchText}
      onSearchTextChange={setSearchText}
      onSelect={handleSelect}
      onRefreshMicroflows={refreshMicroflows}
      onViewMicroflowReferences={viewMicroflowReferences}
      onDeleteMicroflow={deleteMicroflow}
      microflowErrorText={microflowError ? formatMicroflowListError(microflowError) : undefined}
    />
  );
}

export const AppExplorer = AppExplorerContainer;
