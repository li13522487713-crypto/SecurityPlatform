import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Modal, Toast } from "@douyinfe/semi-ui";

import { useMendixStudioStore } from "../../store";
import type { ActiveTabId, MendixStudioTab, MicroflowValidationSummary } from "../../store";
import type { MicroflowAdapterBundle } from "../../microflow/adapter/microflow-adapter-factory";
import { getMicroflowApiError } from "../../microflow/adapter/http/microflow-api-error";
import type { MicroflowApiError } from "../../microflow/contracts/api/api-envelope";
import type { MicroflowFolder } from "../../microflow/folders/microflow-folder-types";
import { canDeleteMicroflowFromReferences, getActiveReferences, resolveReferenceDisplayName } from "../../microflow/references/microflow-reference-utils";
import type { StudioMicroflowDefinitionView } from "../../microflow/studio/studio-microflow-types";
import { mapMicroflowResourceToStudioDefinitionView } from "../../microflow/studio/studio-microflow-mappers";
import { AppExplorerTree } from "./AppExplorerTree";
import { createMicroflowStateChildren, MicroflowsSectionKey } from "./MicroflowsTreeSection";
import { CreateMicroflowModal, DuplicateMicroflowModal, RenameMicroflowModal } from "../../microflow/resource";
import { CreateMicroflowFolderDialog, RenameMicroflowFolderDialog } from "../../microflow/tree-crud";
import type { MicroflowCreateInput, MicroflowDuplicateInput, MicroflowModuleAsset, MicroflowResource } from "../../microflow/resource";

export type ExplorerTreeNodeKind =
  | "module"
  | "folder"
  | "microflowFolder"
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
  folderId?: string;
  folderPath?: string;
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
const explorerFolderRequests = new Map<string, Promise<MicroflowFolder[]>>();
const explorerAppAssetRequests = new Map<string, Promise<MicroflowModuleAsset[]>>();

export function getCurrentExplorerModuleId(node?: Pick<ExplorerTreeNode, "moduleId">, fallbackModuleId?: string): string | undefined {
  return node?.moduleId ?? fallbackModuleId;
}

function createAppAssetTree(modules: MicroflowModuleAsset[]): ExplorerTreeNode[] {
  if (modules.length === 0) {
    return [{
      key: "module:unloaded",
      label: "Module",
      kind: "module",
      defaultOpen: true,
      children: [{
        key: MicroflowsSectionKey,
        label: "Microflows",
        kind: "folder",
        defaultOpen: true,
        children: []
      }]
    }];
  }

  return modules.map(module => {
    const moduleId = module.moduleId;
    const moduleName = module?.name || module?.qualifiedName || "Module";
    return {
      key: moduleId ? `module:${moduleId}` : "module:unloaded",
      label: moduleName,
      kind: "module",
      moduleId,
      defaultOpen: true,
      children: [
        {
          key: `domain-model:${moduleId}`,
          label: "Domain Model",
          kind: "folder",
          defaultOpen: true,
          children: [{ key: `domain-model-placeholder:${moduleId}`, label: "Domain metadata is loaded in Microflow editor", kind: "empty" }]
        },
        {
          key: `pages:${moduleId}`,
          label: "Pages",
          kind: "folder",
          defaultOpen: true,
          children: [{ key: `pages-placeholder:${moduleId}`, label: "Pages asset tree is not connected in this release", kind: "empty" }]
        },
        {
          key: `${MicroflowsSectionKey}:${moduleId}`,
          label: "Microflows",
          kind: "folder",
          moduleId,
          defaultOpen: true,
          children: []
        },
        {
          key: `workflows:${moduleId}`,
          label: "Workflows",
          kind: "folder",
          defaultOpen: true,
          children: [{ key: `workflows-placeholder:${moduleId}`, label: "Workflows asset tree is not connected in this release", kind: "empty" }]
        },
        {
          key: `security:${moduleId}`,
          label: "Security",
          kind: "security",
          children: [
            { key: `user-roles:${moduleId}`, label: "用户角色" },
            { key: `module-roles:${moduleId}`, label: "模块角色" },
            { key: `permission-matrix:${moduleId}`, label: "权限矩阵" },
            { key: `entity-access:${moduleId}`, label: "实体访问" }
          ]
        },
        { key: `navigation:${moduleId}`, label: "Navigation", kind: "navigation" },
        { key: `constants:${moduleId}`, label: "Constants", kind: "constant" },
        { key: `theme:${moduleId}`, label: "Theme", kind: "theme" }
      ]
    };
  });
}

function sortMicroflowViews(items: StudioMicroflowDefinitionView[]): StudioMicroflowDefinitionView[] {
  return [...items].sort((left, right) => {
    const leftLabel = (left.displayName || left.name).toLocaleLowerCase();
    const rightLabel = (right.displayName || right.name).toLocaleLowerCase();
    return leftLabel.localeCompare(rightLabel);
  });
}

function withMicroflowChildren(
  nodes: ExplorerTreeNode[],
  childrenByModuleId: Record<string, ExplorerTreeNode[]>
): ExplorerTreeNode[] {
  return nodes.map(node => ({
    ...node,
    children: node.key.startsWith(`${MicroflowsSectionKey}:`) && node.moduleId
      ? childrenByModuleId[node.moduleId] ?? []
      : node.children
        ? withMicroflowChildren(node.children, childrenByModuleId)
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
  const [modules, setModules] = useState<MicroflowModuleAsset[]>([]);
  const [createModalVisible, setCreateModalVisible] = useState(false);
  const [createContext, setCreateContext] = useState<{ moduleId?: string; folderId?: string; folderPath?: string }>();
  const [renameMicroflowId, setRenameMicroflowId] = useState<string>();
  const [duplicateMicroflowId, setDuplicateMicroflowId] = useState<string>();
  const [createFolderContext, setCreateFolderContext] = useState<{ moduleId?: string; parentFolderId?: string; parentPath?: string }>();
  const [renameFolderId, setRenameFolderId] = useState<string>();
  const lastRequestKeyByModuleRef = useRef<Record<string, string>>({});

  const selectedExplorerNodeId = useMendixStudioStore(state => state.selectedExplorerNodeId);
  const activeModuleId = useMendixStudioStore(state => state.activeModuleId);
  const setSelectedExplorerNodeId = useMendixStudioStore(state => state.setSelectedExplorerNodeId);
  const setActiveWorkbenchTab = useMendixStudioStore(state => state.setActiveWorkbenchTab);
  const setSelected = useMendixStudioStore(state => state.setSelected);
  const setActiveModuleId = useMendixStudioStore(state => state.setActiveModuleId);
  const setActiveMicroflowId = useMendixStudioStore(state => state.setActiveMicroflowId);
  const setModuleMicroflows = useMendixStudioStore(state => state.setModuleMicroflows);
  const setFoldersForModule = useMendixStudioStore(state => state.setFoldersForModule);
  const setFolderLoading = useMendixStudioStore(state => state.setFolderLoading);
  const setFolderError = useMendixStudioStore(state => state.setFolderError);
  const setModuleMicroflowsLoadState = useMendixStudioStore(state => state.setModuleMicroflowsLoadState);
  const openMicroflowWorkbenchTab = useMendixStudioStore(state => state.openMicroflowWorkbenchTab);
  const removeStudioMicroflow = useMendixStudioStore(state => state.removeStudioMicroflow);
  const upsertStudioMicroflow = useMendixStudioStore(state => state.upsertStudioMicroflow);
  const updateMicroflowWorkbenchTabFromResource = useMendixStudioStore(state => state.updateMicroflowWorkbenchTabFromResource);
  const validationSummaryByMicroflowId = useMendixStudioStore(state => state.validationSummaryByMicroflowId);
  const microflowResourcesById = useMendixStudioStore(state => state.microflowResourcesById);
  const microflowIdsByModuleId = useMendixStudioStore(state => state.microflowIdsByModuleId);
  const foldersByModuleId = useMendixStudioStore(state => state.foldersByModuleId);
  const moduleLoadStateByModuleId = useMendixStudioStore(state => state.microflowsLoadStateByModuleId);
  const folderErrorByModuleId = useMendixStudioStore(state => state.folderErrorByModuleId);

  const primaryModule = modules[0];
  const fallbackModuleId = primaryModule?.moduleId ?? "mod_procurement";
  const moduleId = activeModuleId ?? fallbackModuleId;

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

  const loadModuleMicroflows = useCallback(async (targetModuleId: string, force = false) => {
    if (!workspaceId) {
      setMicroflowStatus("error");
      setMicroflowError({ code: "MICROFLOW_VALIDATION_FAILED", message: "无法加载：缺少 workspaceId" });
      setMicroflows([]);
      return;
    }
    if (!targetModuleId) {
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

    const requestKey = `${workspaceId}:${targetModuleId}:${adapterBundle.mode}`;
    if (force) {
      explorerMicroflowRequests.delete(requestKey);
      lastRequestKeyRef.current = undefined;
    }
    if (!force && lastRequestKeyRef.current === requestKey) {
      return;
    }

    lastRequestKeyRef.current = requestKey;
    setMicroflowStatus("loading");
    setModuleMicroflowsLoadState(targetModuleId, "loading");
    setMicroflowError(undefined);
    setFolderLoading(targetModuleId, true);
    setFolderError(targetModuleId, undefined);

    try {
      const microflowRequest = explorerMicroflowRequests.get(requestKey) ?? adapterBundle.resourceAdapter
        .listMicroflows({
          workspaceId,
          moduleId: targetModuleId,
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
      const folderRequestKey = `${requestKey}:folders`;
      const folderRequest = explorerFolderRequests.get(folderRequestKey) ?? adapterBundle.resourceAdapter
        .listMicroflowFolders({ workspaceId, moduleId: targetModuleId })
        .finally(() => {
          explorerFolderRequests.delete(folderRequestKey);
        });

      explorerMicroflowRequests.set(requestKey, microflowRequest);
      explorerFolderRequests.set(folderRequestKey, folderRequest);
      const [nextMicroflows, nextFolders] = await Promise.all([microflowRequest, folderRequest]);
      setMicroflows(nextMicroflows);
      setModuleMicroflows(targetModuleId, nextMicroflows);
      setFoldersForModule(targetModuleId, nextFolders);
      if (!activeModuleId) {
        setActiveModuleId(targetModuleId);
      }
      setMicroflowStatus("success");
      setModuleMicroflowsLoadState(targetModuleId, "success");
    } catch (caught) {
      const apiError = getMicroflowApiError(caught);
      setMicroflowStatus("error");
      setMicroflowError(apiError);
      setModuleMicroflowsLoadState(targetModuleId, "error");
      setFolderError(targetModuleId, apiError);
      setMicroflows([]);
    } finally {
      setFolderLoading(targetModuleId, false);
    }
  }, [
    activeModuleId,
    adapterBundle,
    setActiveModuleId,
    setFolderError,
    setFolderLoading,
    setFoldersForModule,
    setModuleMicroflows,
    setModuleMicroflowsLoadState,
    workspaceId
  ]);

  const loadMicroflows = useCallback(async (force = false) => {
    await loadModuleMicroflows(moduleId, force);
  }, [loadModuleMicroflows, moduleId]);

  useEffect(() => {
    void loadMicroflows();
  }, [loadMicroflows]);

  useEffect(() => {
    if (modules.length === 0) {
      return;
    }
    for (const module of modules) {
      void loadModuleMicroflows(module.moduleId);
    }
  }, [loadModuleMicroflows, modules]);

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
    const created = await adapter.createMicroflow({
      ...input,
      folderId: input.folderId ?? createContext?.folderId
    });
    const view = mapMicroflowResourceToStudioDefinitionView(created);
    upsertStudioMicroflow(view);
    setActiveModuleId(view.moduleId);
    setActiveMicroflowId(view.id);
    openMicroflowWorkbenchTab(view.id);
    await loadModuleMicroflows(view.moduleId, true);
    return created;
  }, [adapterBundle?.resourceAdapter, createContext?.folderId, loadModuleMicroflows, openMicroflowWorkbenchTab, setActiveMicroflowId, setActiveModuleId, upsertStudioMicroflow]);

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
    await loadModuleMicroflows(view.moduleId, true);
    Toast.success("微流已重命名");
  }, [adapterBundle?.resourceAdapter, loadModuleMicroflows, renameMicroflowId, updateMicroflowWorkbenchTabFromResource, upsertStudioMicroflow]);

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
    await loadModuleMicroflows(view.moduleId, true);
    Toast.success("微流已复制");
  }, [adapterBundle?.resourceAdapter, duplicateMicroflowId, loadModuleMicroflows, upsertStudioMicroflow]);

  const createFolder = useCallback(async (name: string) => {
    const adapter = adapterBundle?.resourceAdapter;
    const targetModuleId = folderCreateContext?.moduleId ?? moduleId;
    if (!adapter || !targetModuleId) {
      throw new Error("无法创建文件夹：缺少微流资源适配器或模块上下文。");
    }
    const folder = await adapter.createMicroflowFolder({
      workspaceId,
      moduleId: targetModuleId,
      parentFolderId: folderCreateContext?.parentFolderId,
      name
    });
    upsertFolder(folder);
    await loadModuleMicroflows(folder.moduleId, true);
    Toast.success("文件夹已创建");
  }, [adapterBundle?.resourceAdapter, folderCreateContext, loadModuleMicroflows, moduleId, upsertFolder, workspaceId]);

  const renameFolder = useCallback(async (name: string) => {
    const adapter = adapterBundle?.resourceAdapter;
    const folderId = renameFolderId;
    if (!adapter || !folderId) {
      throw new Error("无法重命名文件夹：缺少微流资源适配器或文件夹上下文。");
    }
    const folder = await adapter.renameMicroflowFolder(folderId, name);
    upsertFolder(folder);
    await loadModuleMicroflows(folder.moduleId, true);
    Toast.success("文件夹已重命名");
  }, [adapterBundle?.resourceAdapter, loadModuleMicroflows, renameFolderId, upsertFolder]);

  const openCreateMicroflow = useCallback((node: ExplorerTreeNode) => {
    if (!node.key.startsWith(`${MicroflowsSectionKey}:`) && node.kind !== "folder") {
      return;
    }
    if (node.moduleId) {
      setActiveModuleId(node.moduleId);
    }
    setCreateContext({ moduleId: node.moduleId, folderId: node.folderId, folderPath: node.folderPath });
    setCreateModalVisible(true);
  }, [setActiveModuleId]);

  const openCreateFolder = useCallback((node: ExplorerTreeNode) => {
    if (!node.moduleId || (node.kind !== "module" && node.kind !== "folder")) {
      return;
    }
    setActiveModuleId(node.moduleId);
    setCreateFolderContext({
      moduleId: node.moduleId,
      parentFolderId: node.folderId,
      parentPath: node.folderPath
    });
  }, [setActiveModuleId]);

  const openRenameFolder = useCallback((node: ExplorerTreeNode) => {
    if (node.kind !== "folder" || !node.folderId || !node.dynamic) {
      return;
    }
    setRenameFolderId(node.folderId);
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

  const treeData = useMemo(() => {
    const explorerModules = modules.length > 0
      ? modules
      : [{ moduleId: fallbackModuleId, name: "Procurement", qualifiedName: "Procurement" }];
    const childrenByModuleId = Object.fromEntries(explorerModules.map(module => {
      const ids = microflowIdsByModuleId[module.moduleId] ?? [];
      const resources = ids
        .map(id => microflowResourcesById[id])
        .filter((resource): resource is StudioMicroflowDefinitionView => Boolean(resource));
      const status = moduleLoadStateByModuleId[module.moduleId] ?? (module.moduleId === moduleId ? microflowStatus : "idle");
      const error = folderErrorByModuleId[module.moduleId] ?? (module.moduleId === moduleId ? microflowError : undefined);
      return [
        module.moduleId,
        createMicroflowStateChildren(
          status,
          sortMicroflowViews(resources),
          error,
          validationSummaryByMicroflowId,
          foldersByModuleId[module.moduleId] ?? [],
          module.moduleId
        )
      ];
    }));
    return withMicroflowChildren(createAppAssetTree(explorerModules), childrenByModuleId);
  }, [
    fallbackModuleId,
    folderErrorByModuleId,
    foldersByModuleId,
    microflowError,
    microflowIdsByModuleId,
    microflowResourcesById,
    microflowStatus,
    moduleId,
    moduleLoadStateByModuleId,
    modules,
    validationSummaryByMicroflowId
  ]);

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
        onCreateFolder={openCreateFolder}
        onRenameFolder={openRenameFolder}
        onRenameMicroflow={openRenameMicroflow}
        onDuplicateMicroflow={openDuplicateMicroflow}
        onViewMicroflowReferences={viewMicroflowReferences}
        onDeleteMicroflow={deleteMicroflow}
        microflowErrorText={microflowError ? formatMicroflowListError(microflowError) : undefined}
      />
      <CreateMicroflowModal
        visible={createModalVisible}
        existingResources={microflows}
        defaultModuleId={createContext?.moduleId ?? moduleId}
        initialModuleId={createContext?.moduleId ?? moduleId}
        initialModuleName={modules.find(item => item.moduleId === (createContext?.moduleId ?? moduleId))?.name}
        initialFolderId={createContext?.folderId}
        initialFolderPath={createContext?.folderPath}
        moduleOptions={modules.map(module => ({ value: module.moduleId, label: module.name || module.qualifiedName || module.moduleId }))}
        moduleLocked
        onClose={() => {
          setCreateModalVisible(false);
          setCreateContext(undefined);
        }}
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
      <CreateMicroflowFolderDialog
        visible={Boolean(folderCreateContext)}
        parentPath={folderCreateContext?.parentPath}
        onClose={() => setCreateFolderContext(undefined)}
        onSubmit={createFolder}
      />
      <RenameMicroflowFolderDialog
        visible={Boolean(renameFolderId)}
        folder={renameFolderId ? Object.values(foldersByModuleId).flat().find(folder => folder.id === renameFolderId) : undefined}
        onClose={() => setRenameFolderId(undefined)}
        onSubmit={renameFolder}
      />
    </>
  );
}

export const AppExplorer = AppExplorerContainer;
