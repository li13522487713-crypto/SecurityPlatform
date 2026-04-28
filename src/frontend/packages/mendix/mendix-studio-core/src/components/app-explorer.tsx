import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Modal, Toast } from "@douyinfe/semi-ui";

import { useMendixStudioStore } from "../store";
import type { ActiveTabId, MendixStudioTab, MicroflowValidationSummary } from "../store";
import type { MicroflowAdapterBundle } from "../microflow/adapter/microflow-adapter-factory";
import { getMicroflowApiError } from "../microflow/adapter/http/microflow-api-error";
import type { MicroflowApiError } from "../microflow/contracts/api/api-envelope";
import { canDeleteMicroflowFromReferences, getActiveReferences, resolveReferenceDisplayName } from "../microflow/references/microflow-reference-utils";
import type { StudioMicroflowDefinitionView } from "../microflow/studio/studio-microflow-types";
import { mapMicroflowResourceToStudioDefinitionView } from "../microflow/studio/studio-microflow-mappers";
import { AppExplorerTree } from "./app-explorer-tree";
import { createMicroflowStateChildren, MicroflowsSectionKey } from "./microflow-tree-section";
import { CreateMicroflowModal, DuplicateMicroflowModal, RenameMicroflowModal } from "../microflow/resource";
import type { MicroflowCreateInput, MicroflowDuplicateInput, MicroflowModuleAsset, MicroflowResource } from "../microflow/resource";

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
  problemSummary?: MicroflowValidationSummary;
  action?: "retryMicroflows";
  children?: ExplorerTreeNode[];
  defaultOpen?: boolean;
}

export interface AppExplorerProps {
  adapterBundle?: MicroflowAdapterBundle;
  appId?: string;
  workspaceId?: string;
  refreshToken?: number;
  onViewMicroflowReferences?: (microflowId: string) => void;
}

export type MicroflowLoadStatus = "idle" | "loading" | "success" | "error";

const explorerMicroflowRequests = new Map<string, Promise<StudioMicroflowDefinitionView[]>>();
const explorerAppAssetRequests = new Map<string, Promise<MicroflowModuleAsset[]>>();

export function getCurrentExplorerModuleId(node?: Pick<ExplorerTreeNode, "moduleId">, fallbackModuleId?: string): string | undefined {
  return node?.moduleId ?? fallbackModuleId;
}

function createAppAssetTree(module?: MicroflowModuleAsset): ExplorerTreeNode[] {
  const moduleId = module?.moduleId ?? "";
  const moduleName = module?.name || module?.qualifiedName || "Module";
  return [{
    key: moduleId ? `module:${moduleId}` : "module:unloaded",
    label: moduleName,
    kind: "module",
    moduleId,
    defaultOpen: true,
    children: [
      {
        key: "domain-model",
        label: "Domain Model",
        kind: "folder",
        defaultOpen: true,
        children: [{ key: "domain-model-placeholder", label: "Domain metadata is loaded in Microflow editor", kind: "empty" }]
      },
      {
        key: "pages",
        label: "Pages",
        kind: "folder",
        defaultOpen: true,
        children: [{ key: "pages-placeholder", label: "Pages asset tree is not connected in this release", kind: "empty" }]
      },
      {
        key: MicroflowsSectionKey,
        label: "Microflows",
        kind: "folder",
        moduleId,
        defaultOpen: true,
        children: []
      },
      {
        key: "workflows",
        label: "Workflows",
        kind: "folder",
        defaultOpen: true,
        children: [{ key: "workflows-placeholder", label: "Workflows asset tree is not connected in this release", kind: "empty" }]
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
  }];
}

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

export function AppExplorerContainer({ adapterBundle, appId, workspaceId, refreshToken, onViewMicroflowReferences }: AppExplorerProps) {
  const [searchText, setSearchText] = useState("");
  const [microflowStatus, setMicroflowStatus] = useState<MicroflowLoadStatus>("idle");
  const [microflowError, setMicroflowError] = useState<MicroflowApiError>();
  const [microflows, setMicroflows] = useState<StudioMicroflowDefinitionView[]>([]);
  const [modules, setModules] = useState<MicroflowModuleAsset[]>([]);
  const [createModalVisible, setCreateModalVisible] = useState(false);
  const [renameMicroflowId, setRenameMicroflowId] = useState<string>();
  const [duplicateMicroflowId, setDuplicateMicroflowId] = useState<string>();
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
  const upsertStudioMicroflow = useMendixStudioStore(state => state.upsertStudioMicroflow);
  const updateMicroflowWorkbenchTabFromResource = useMendixStudioStore(state => state.updateMicroflowWorkbenchTabFromResource);
  const validationSummaryByMicroflowId = useMendixStudioStore(state => state.validationSummaryByMicroflowId);
  const microflowResourcesById = useMendixStudioStore(state => state.microflowResourcesById);

  const primaryModule = modules[0];
  const moduleNode = useMemo(() => createAppAssetTree(primaryModule)[0], [primaryModule]);
  const moduleId = getCurrentExplorerModuleId(moduleNode, primaryModule?.moduleId);

  const loadAppAssetTree = useCallback(async (force = false) => {
    if (!workspaceId || !appId || !adapterBundle?.resourceAdapter?.getMicroflowApp) {
      setModules([]);
      return;
    }
    const requestKey = `${workspaceId}:${appId}:${adapterBundle.mode}`;
    if (force) {
      explorerAppAssetRequests.delete(requestKey);
    }
    try {
      const request = explorerAppAssetRequests.get(requestKey) ?? adapterBundle.resourceAdapter
        .getMicroflowApp(appId, { workspaceId })
        .then(asset => asset.modules)
        .finally(() => explorerAppAssetRequests.delete(requestKey));
      explorerAppAssetRequests.set(requestKey, request);
      const nextModules = await request;
      setModules(nextModules);
      if (!activeModuleId && nextModules[0]?.moduleId) {
        setActiveModuleId(nextModules[0].moduleId);
      }
    } catch (caught) {
      setModules([]);
      setMicroflowStatus("error");
      setMicroflowError(getMicroflowApiError(caught));
    }
  }, [activeModuleId, adapterBundle, appId, setActiveModuleId, workspaceId]);

  useEffect(() => {
    void loadAppAssetTree();
  }, [loadAppAssetTree]);

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
      void loadAppAssetTree(true);
      void loadMicroflows(true);
    }
  }, [loadAppAssetTree, loadMicroflows, refreshToken]);

  const refreshMicroflows = useCallback(async () => {
    await loadMicroflows(true);
    Toast.success("微流列表已刷新");
  }, [loadMicroflows]);

  const createMicroflow = useCallback(async (input: MicroflowCreateInput): Promise<MicroflowResource> => {
    const adapter = adapterBundle?.resourceAdapter;
    if (!adapter) {
      throw new Error("无法创建微流：缺少 microflow resource adapter。");
    }
    const created = await adapter.createMicroflow(input);
    const view = mapMicroflowResourceToStudioDefinitionView(created);
    upsertStudioMicroflow(view);
    setActiveModuleId(view.moduleId);
    setActiveMicroflowId(view.id);
    openMicroflowWorkbenchTab(view.id);
    await loadMicroflows(true);
    return created;
  }, [adapterBundle?.resourceAdapter, loadMicroflows, openMicroflowWorkbenchTab, setActiveMicroflowId, setActiveModuleId, upsertStudioMicroflow]);

  const renameMicroflow = useCallback(async (name: string, displayName?: string) => {
    if (!renameMicroflowId) {
      return;
    }
    const adapter = adapterBundle?.resourceAdapter;
    if (!adapter) {
      throw new Error("无法重命名微流：缺少 microflow resource adapter。");
    }
    const renamed = await adapter.renameMicroflow(renameMicroflowId, name, displayName);
    const view = mapMicroflowResourceToStudioDefinitionView(renamed);
    upsertStudioMicroflow(view);
    updateMicroflowWorkbenchTabFromResource(view);
    await loadMicroflows(true);
    Toast.success("微流已重命名");
  }, [adapterBundle?.resourceAdapter, loadMicroflows, renameMicroflowId, updateMicroflowWorkbenchTabFromResource, upsertStudioMicroflow]);

  const duplicateMicroflow = useCallback(async (input: MicroflowDuplicateInput) => {
    if (!duplicateMicroflowId) {
      return;
    }
    const adapter = adapterBundle?.resourceAdapter;
    if (!adapter) {
      throw new Error("无法复制微流：缺少 microflow resource adapter。");
    }
    const duplicated = await adapter.duplicateMicroflow(duplicateMicroflowId, input);
    const view = mapMicroflowResourceToStudioDefinitionView(duplicated);
    upsertStudioMicroflow(view);
    await loadMicroflows(true);
    Toast.success("微流已复制");
  }, [adapterBundle?.resourceAdapter, duplicateMicroflowId, loadMicroflows, upsertStudioMicroflow]);

  const openCreateMicroflow = useCallback((node: ExplorerTreeNode) => {
    if (node.key !== MicroflowsSectionKey) {
      return;
    }
    setCreateModalVisible(true);
  }, []);

  const openRenameMicroflow = useCallback((node: ExplorerTreeNode) => {
    if (node.kind !== "microflow" || !node.microflowId || node.readonly || !node.dynamic) {
      return;
    }
    setRenameMicroflowId(node.microflowId);
  }, []);

  const openDuplicateMicroflow = useCallback((node: ExplorerTreeNode) => {
    if (node.kind !== "microflow" || !node.microflowId || node.readonly || !node.dynamic) {
      return;
    }
    setDuplicateMicroflowId(node.microflowId);
  }, []);

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
    const saveState = useMendixStudioStore.getState().saveStateByMicroflowId[microflowId];
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
        content: `${saveState?.dirty ? "该微流有未保存更改，删除后本地更改会丢失。" : ""}已完成 callers 预检查，未发现 active callers。确认删除 ${label}？`,
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
    () => withMicroflowChildren(createAppAssetTree(primaryModule), createMicroflowStateChildren(microflowStatus, microflows, microflowError, validationSummaryByMicroflowId)),
    [microflowError, microflowStatus, microflows, primaryModule, validationSummaryByMicroflowId]
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
    <>
      <AppExplorerTree
        treeData={treeData}
        selectedId={selectedExplorerNodeId}
        searchText={searchText}
        onSearchTextChange={setSearchText}
        onSelect={handleSelect}
        onRefreshMicroflows={refreshMicroflows}
        onCreateMicroflow={openCreateMicroflow}
        onRenameMicroflow={openRenameMicroflow}
        onDuplicateMicroflow={openDuplicateMicroflow}
        onViewMicroflowReferences={viewMicroflowReferences}
        onDeleteMicroflow={deleteMicroflow}
        microflowErrorText={microflowError ? formatMicroflowListError(microflowError) : undefined}
      />
      <CreateMicroflowModal
        visible={createModalVisible}
        existingResources={microflows}
        defaultModuleId={moduleId}
        initialModuleId={moduleId}
        initialModuleName={primaryModule?.name}
        moduleOptions={modules.map(module => ({ value: module.moduleId, label: module.name || module.qualifiedName || module.moduleId }))}
        moduleLocked
        onClose={() => setCreateModalVisible(false)}
        onSubmit={createMicroflow}
      />
      <RenameMicroflowModal
        visible={Boolean(renameMicroflowId)}
        resource={renameMicroflowId ? microflowResourcesById[renameMicroflowId] : undefined}
        onClose={() => setRenameMicroflowId(undefined)}
        onSubmit={renameMicroflow}
      />
      <DuplicateMicroflowModal
        visible={Boolean(duplicateMicroflowId)}
        resource={duplicateMicroflowId ? microflowResourcesById[duplicateMicroflowId] : undefined}
        onClose={() => setDuplicateMicroflowId(undefined)}
        onSubmit={duplicateMicroflow}
      />
    </>
  );
}

export const AppExplorer = AppExplorerContainer;
