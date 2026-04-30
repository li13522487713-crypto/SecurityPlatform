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
import { resolveExplorerCreateContext, type ExplorerCreateContext } from "./app-explorer-create-context";
import { CreateMicroflowModal, DuplicateMicroflowModal, RenameMicroflowModal } from "../../microflow/resource";
import { CreateMicroflowFolderDialog, RenameMicroflowFolderDialog } from "../../microflow/tree-crud";
import type {
  MicroflowCreateInput,
  MicroflowDomainEntitySummary,
  MicroflowDuplicateInput,
  MicroflowModuleAsset,
  MicroflowPageAssetSummary,
  MicroflowResource,
  MicroflowSecurityAssetSummary,
  MicroflowWorkflowAssetSummary
} from "../../microflow/resource";
import type { OpenWorkbenchResourceInput } from "../../store";
import { getMendixStudioCopy, type MendixStudioCopy } from "../../i18n/copy";

export type ExplorerTreeNodeKind =
  | "module"
  | "folder"
  | "microflowFolder"
  | "entity"
  | "page"
  | "microflow"
  | "workflow"
  | "domainModel"
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
  resourceKind?: OpenWorkbenchResourceInput["kind"];
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
  page?: MicroflowPageAssetSummary;
  workflow?: MicroflowWorkflowAssetSummary;
  entity?: MicroflowDomainEntitySummary;
  securitySummary?: MicroflowSecurityAssetSummary;
  children?: ExplorerTreeNode[];
  defaultOpen?: boolean;
}

export interface AppExplorerProps {
  adapterBundle?: MicroflowAdapterBundle;
  appId?: string;
  workspaceId?: string;
  refreshToken?: number;
  onViewMicroflowReferences?: (microflowId: string) => void;
  onOpenMicroflow?: (microflowId: string) => void;
  onOpenResource?: (resource: OpenWorkbenchResourceInput) => void;
}

export type MicroflowLoadStatus = "idle" | "loading" | "success" | "error";

const explorerMicroflowRequests = new Map<string, Promise<StudioMicroflowDefinitionView[]>>();
const explorerFolderRequests = new Map<string, Promise<MicroflowFolder[]>>();
const explorerAppAssetRequests = new Map<string, Promise<MicroflowModuleAsset[]>>();

export function getCurrentExplorerModuleId(node?: Pick<ExplorerTreeNode, "key" | "moduleId">, fallbackModuleId?: string): string | undefined {
  if (node?.moduleId) {
    return node.moduleId;
  }
  if (node?.key.startsWith(`${MicroflowsSectionKey}:`)) {
    const [, moduleIdFromKey] = node.key.split(":");
    return moduleIdFromKey || fallbackModuleId;
  }
  return fallbackModuleId;
}

function createAppAssetTree(modules: MicroflowModuleAsset[], copy: MendixStudioCopy): ExplorerTreeNode[] {
  if (modules.length === 0) {
    return [{
      key: "module:empty",
      label: "(empty)",
      kind: "module",
      defaultOpen: true,
      children: [
        {
          key: "modules-empty",
          label: "当前应用未加载到任何模块。请检查 appId/workspaceId 或刷新。",
          kind: "empty"
        },
        {
          key: MicroflowsSectionKey,
          label: "Microflows",
          kind: "folder",
          defaultOpen: true,
          children: []
        }
      ]
    }];
  }

  return modules.map(module => {
    const moduleId = module.moduleId;
    const moduleName = module?.name || module?.qualifiedName || "Module";
    const pages = module.pages ?? [];
    const workflows = module.workflows ?? [];
    const entities = module.entities ?? [];
    const security = module.security;
    return {
      key: moduleId ? `module:${moduleId}` : "module:unloaded",
      label: moduleName,
      kind: "module",
      moduleId,
      defaultOpen: true,
      children: [
        {
          key: `domain-model:${moduleId}`,
          label: copy.explorer.domainModel,
          kind: "domainModel",
          icon: "E",
          moduleId,
          resourceId: moduleId,
          resourceKind: "domainModel",
          title: `${moduleName} Domain Model`,
          defaultOpen: true,
          children: entities.length > 0
            ? entities.map(entity => ({
                key: `entity:${entity.id}`,
                label: entity.name || entity.qualifiedName,
                kind: "entity" as const,
                icon: "E",
                moduleId,
                resourceId: moduleId,
                resourceKind: "domainModel" as const,
                name: entity.name,
                displayName: entity.name,
                qualifiedName: entity.qualifiedName,
                title: `${entity.qualifiedName} · ${entity.attributeCount} attributes`,
                entity
              }))
            : [{ key: `domain-model-empty:${moduleId}`, label: copy.explorer.noDomainEntities, kind: "empty" }]
        },
        {
          key: `pages:${moduleId}`,
          label: copy.explorer.pages,
          kind: "folder",
          defaultOpen: true,
          children: pages.length > 0
            ? pages.map(page => ({
                key: `page:${page.id}`,
                label: page.name || page.qualifiedName,
                kind: "page" as const,
                icon: "P",
                moduleId,
                resourceId: page.id,
                resourceKind: "page" as const,
                name: page.name,
                displayName: page.name,
                qualifiedName: page.qualifiedName,
                title: page.description ?? page.qualifiedName,
                page
              }))
            : [{ key: `pages-empty:${moduleId}`, label: copy.explorer.noPages, kind: "empty" }]
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
          label: copy.explorer.workflows,
          kind: "folder",
          defaultOpen: true,
          children: workflows.length > 0
            ? workflows.map(workflow => ({
                key: `workflow:${workflow.id}`,
                label: workflow.name || workflow.qualifiedName,
                kind: "workflow" as const,
                icon: "W",
                moduleId,
                resourceId: workflow.id,
                resourceKind: "workflow" as const,
                name: workflow.name,
                displayName: workflow.name,
                qualifiedName: workflow.qualifiedName,
                title: workflow.description ?? workflow.qualifiedName,
                workflow
              }))
            : [{ key: `workflows-empty:${moduleId}`, label: copy.explorer.noWorkflows, kind: "empty" }]
        },
        {
          key: `security:${moduleId}`,
          label: copy.explorer.security,
          kind: "security",
          icon: "A",
          moduleId,
          resourceId: moduleId,
          resourceKind: "security",
          title: `${moduleName} Security`,
          securitySummary: security,
          children: [
            {
              key: `security-entity-access:${moduleId}`,
              label: copy.explorer.entityAccessRules(security?.entityAccessCount ?? 0),
              kind: "empty",
            },
            {
              key: `security-readonly:${moduleId}`,
              label: security?.readonly === false ? copy.explorer.securityEditable : copy.explorer.securityReadonly,
              kind: "empty",
            }
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

function createRecentlyOpenedTree(tabs: Array<{ id: string; kind: string; title: string; moduleId?: string; resourceId?: string; microflowId?: string; qualifiedName?: string; openedAt: string }>, copy: MendixStudioCopy): ExplorerTreeNode[] {
  const recent = [...tabs]
    .sort((left, right) => right.openedAt.localeCompare(left.openedAt))
    .slice(0, 8)
    .map(tab => ({
      key: `recent:${tab.id}`,
      label: tab.title,
      kind: tab.kind === "microflow" ? "microflow" as const : tab.kind as ExplorerTreeNodeKind,
      icon: tab.kind === "microflow" ? "M" : tab.kind === "page" ? "P" : tab.kind === "workflow" ? "W" : tab.kind === "domainModel" ? "E" : "A",
      moduleId: tab.moduleId,
      microflowId: tab.microflowId,
      resourceId: tab.kind === "microflow" ? tab.microflowId ?? tab.resourceId : tab.resourceId,
      resourceKind: tab.kind === "microflow" ? undefined : tab.kind as OpenWorkbenchResourceInput["kind"],
      qualifiedName: tab.qualifiedName,
      title: tab.qualifiedName ?? tab.title
    }));

  if (recent.length === 0) {
    return [];
  }

  return [{
    key: "recently-opened",
    label: copy.explorer.recentlyOpened,
    kind: "folder",
    defaultOpen: true,
    children: recent
  }];
}

function includesSearch(values: Array<string | undefined>, normalizedSearch: string): boolean {
  return values.some(value => value?.toLocaleLowerCase().includes(normalizedSearch));
}

function createSearchResultsTree(
  modules: MicroflowModuleAsset[],
  microflowResources: StudioMicroflowDefinitionView[],
  searchText: string,
  copy: MendixStudioCopy
): ExplorerTreeNode[] {
  const normalizedSearch = searchText.trim().toLocaleLowerCase();
  if (!normalizedSearch) {
    return [];
  }

  const results: ExplorerTreeNode[] = [];
  for (const module of modules) {
    if (includesSearch([module.name, module.qualifiedName, module.description], normalizedSearch)) {
      results.push({
        key: `search-module:${module.moduleId}`,
        label: module.name || module.qualifiedName,
        kind: "module",
        moduleId: module.moduleId,
        title: module.description ?? module.qualifiedName
      });
    }

    for (const page of module.pages ?? []) {
      if (includesSearch([page.name, page.qualifiedName, page.description], normalizedSearch)) {
        results.push({
          key: `search-page:${page.id}`,
          label: page.name || page.qualifiedName,
          kind: "page",
          icon: "P",
          moduleId: module.moduleId,
          resourceId: page.id,
          resourceKind: "page",
          qualifiedName: page.qualifiedName,
          page
        });
      }
    }

    for (const workflow of module.workflows ?? []) {
      if (includesSearch([workflow.name, workflow.qualifiedName, workflow.description, workflow.contextEntityQualifiedName], normalizedSearch)) {
        results.push({
          key: `search-workflow:${workflow.id}`,
          label: workflow.name || workflow.qualifiedName,
          kind: "workflow",
          icon: "W",
          moduleId: module.moduleId,
          resourceId: workflow.id,
          resourceKind: "workflow",
          qualifiedName: workflow.qualifiedName,
          workflow
        });
      }
    }

    for (const entity of module.entities ?? []) {
      if (includesSearch([entity.name, entity.qualifiedName], normalizedSearch)) {
        results.push({
          key: `search-entity:${entity.id}`,
          label: entity.name || entity.qualifiedName,
          kind: "entity",
          icon: "E",
          moduleId: module.moduleId,
          resourceId: module.moduleId,
          resourceKind: "domainModel",
          qualifiedName: entity.qualifiedName,
          entity
        });
      }
    }
  }

  for (const resource of microflowResources) {
    if (includesSearch([resource.name, resource.displayName, resource.qualifiedName, resource.description], normalizedSearch)) {
      results.push({
        key: `search-microflow:${resource.id}`,
        label: resource.displayName || resource.name,
        kind: "microflow",
        icon: "M",
        moduleId: resource.moduleId,
        microflowId: resource.id,
        resourceId: resource.id,
        qualifiedName: resource.qualifiedName
      });
    }
  }

  return [{
    key: "search-results",
    label: copy.explorer.searchResults,
    kind: "folder",
    defaultOpen: true,
    children: results.length > 0
      ? results
      : [{ key: "search-results-empty", label: copy.explorer.noMatchingResources, kind: "empty" }]
  }];
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

export function AppExplorerContainer({ adapterBundle, appId, workspaceId, refreshToken, onViewMicroflowReferences, onOpenMicroflow, onOpenResource }: AppExplorerProps) {
  const copy = getMendixStudioCopy();
  const [searchText, setSearchText] = useState("");
  const [microflowStatus, setMicroflowStatus] = useState<MicroflowLoadStatus>("idle");
  const [microflowError, setMicroflowError] = useState<MicroflowApiError>();
  const [microflows, setMicroflows] = useState<StudioMicroflowDefinitionView[]>([]);
  const [modules, setModules] = useState<MicroflowModuleAsset[]>([]);
  const [createModalVisible, setCreateModalVisible] = useState(false);
  const [createContext, setCreateContext] = useState<ExplorerCreateContext>();
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
  const setAppAssetModules = useMendixStudioStore(state => state.setAppAssetModules);
  const setFoldersForModule = useMendixStudioStore(state => state.setFoldersForModule);
  const upsertFolder = useMendixStudioStore(state => state.upsertFolder);
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
  const workbenchTabs = useMendixStudioStore(state => state.workbenchTabs);

  const primaryModule = modules[0];
  const moduleId = activeModuleId ?? primaryModule?.moduleId ?? "";

  const loadAppAssetTree = useCallback(async (force = false) => {
    if (!workspaceId || !appId || !adapterBundle?.resourceAdapter?.getMicroflowApp) {
      setModules([]);
      setAppAssetModules([]);
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
      setAppAssetModules(nextModules);
      if (!activeModuleId && nextModules[0]?.moduleId) {
        setActiveModuleId(nextModules[0].moduleId);
      }
    } catch (caught) {
      setModules([]);
      setAppAssetModules([]);
      setMicroflowStatus("error");
      setMicroflowError(getMicroflowApiError(caught));
    }
  }, [activeModuleId, adapterBundle, appId, setActiveModuleId, setAppAssetModules, workspaceId]);

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
      delete lastRequestKeyByModuleRef.current[targetModuleId];
    }
    if (!force && lastRequestKeyByModuleRef.current[targetModuleId] === requestKey) {
      return;
    }

    lastRequestKeyByModuleRef.current[targetModuleId] = requestKey;
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
      const folderRequest = explorerFolderRequests.get(folderRequestKey) ?? (
        typeof adapterBundle.resourceAdapter.listMicroflowFolders === "function"
          ? adapterBundle.resourceAdapter
            .listMicroflowFolders({ workspaceId, moduleId: targetModuleId })
            .finally(() => {
              explorerFolderRequests.delete(folderRequestKey);
            })
          : Promise.resolve([])
      );

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
    if (onOpenMicroflow) {
      onOpenMicroflow(view.id);
    } else {
      setActiveMicroflowId(view.id);
      openMicroflowWorkbenchTab(view.id);
    }
    await loadModuleMicroflows(view.moduleId, true);
    return created;
  }, [adapterBundle?.resourceAdapter, createContext?.folderId, loadModuleMicroflows, onOpenMicroflow, openMicroflowWorkbenchTab, setActiveMicroflowId, setActiveModuleId, upsertStudioMicroflow]);

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
    const targetModuleId = createFolderContext?.moduleId ?? moduleId;
    if (!adapter || !targetModuleId) {
      throw new Error("无法创建文件夹：缺少微流资源适配器或模块上下文。");
    }
    const folder = await adapter.createMicroflowFolder({
      workspaceId,
      moduleId: targetModuleId,
      parentFolderId: createFolderContext?.parentFolderId,
      name
    });
    upsertFolder(folder);
    await loadModuleMicroflows(folder.moduleId, true);
    Toast.success("文件夹已创建");
  }, [adapterBundle?.resourceAdapter, createFolderContext, loadModuleMicroflows, moduleId, upsertFolder, workspaceId]);

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
    const supportedSource = node.key === MicroflowsSectionKey
      || node.key.startsWith(`${MicroflowsSectionKey}:`)
      || node.kind === "microflowFolder"
      || node.kind === "folder"
      || node.kind === "module";
    if (!supportedSource) {
      return;
    }
    const nextContext = resolveExplorerCreateContext({
      node,
      modules,
      appId,
      workspaceId,
      fallbackModuleId: activeModuleId
    });
    if (nextContext.moduleId) {
      setActiveModuleId(nextContext.moduleId);
    }
    setCreateContext(nextContext);
    setCreateModalVisible(true);
  }, [activeModuleId, appId, modules, setActiveModuleId, workspaceId]);

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
    const explorerModules = modules;
    const allMicroflowResources = Object.values(microflowResourcesById);
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
    const appTree = withMicroflowChildren(createAppAssetTree(explorerModules, copy), childrenByModuleId);
    return [
      ...createRecentlyOpenedTree(workbenchTabs, copy),
      ...createSearchResultsTree(explorerModules, allMicroflowResources, searchText, copy),
      ...appTree
    ];
  }, [
    folderErrorByModuleId,
    foldersByModuleId,
    microflowError,
    microflowIdsByModuleId,
    microflowResourcesById,
    microflowStatus,
    moduleId,
    moduleLoadStateByModuleId,
    modules,
    copy,
    searchText,
    validationSummaryByMicroflowId,
    workbenchTabs
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
      setActiveModuleId(node.moduleId);
      if (onOpenMicroflow) {
        onOpenMicroflow(node.microflowId);
      } else {
        setActiveMicroflowId(node.microflowId);
        openMicroflowWorkbenchTab(node.microflowId);
      }
      return;
    }

    if (node.resourceKind && node.resourceId) {
      setSelected(node.resourceKind, node.resourceId);
      onOpenResource?.({
        kind: node.resourceKind,
        resourceId: node.resourceId,
        moduleId: node.moduleId,
        title: node.displayName || node.name || node.label,
        qualifiedName: node.qualifiedName,
        subtitle: node.qualifiedName ?? node.title
      });
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
    onOpenMicroflow,
    onOpenResource,
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
      {createModalVisible ? (
        <CreateMicroflowModal
          visible={createModalVisible}
          existingResources={microflows}
          defaultModuleId={createContext?.moduleId}
          initialModuleId={createContext?.moduleId}
          initialModuleName={createContext?.moduleName}
          initialFolderId={createContext?.folderId}
          initialFolderPath={createContext?.folderPath}
          moduleOptions={modules.map(module => ({ value: module.moduleId, label: module.name || module.qualifiedName || module.moduleId }))}
          moduleLocked={Boolean(createContext?.moduleId)}
          onClose={() => {
            setCreateModalVisible(false);
            setCreateContext(undefined);
          }}
          onSubmit={createMicroflow}
        />
      ) : null}
      {renameMicroflowId ? (
        <RenameMicroflowModal
          visible
          resource={microflowResourcesById[renameMicroflowId]}
          onClose={() => setRenameMicroflowId(undefined)}
          onSubmit={renameMicroflow}
        />
      ) : null}
      {duplicateMicroflowId ? (
        <DuplicateMicroflowModal
          visible
          resource={microflowResourcesById[duplicateMicroflowId]}
          onClose={() => setDuplicateMicroflowId(undefined)}
          onSubmit={duplicateMicroflow}
        />
      ) : null}
      {createFolderContext ? (
        <CreateMicroflowFolderDialog
          visible
          parentPath={createFolderContext.parentPath}
          onClose={() => setCreateFolderContext(undefined)}
          onSubmit={createFolder}
        />
      ) : null}
      {renameFolderId ? (
        <RenameMicroflowFolderDialog
          visible
          folder={Object.values(foldersByModuleId).flat().find(folder => folder.id === renameFolderId)}
          onClose={() => setRenameFolderId(undefined)}
          onSubmit={renameFolder}
        />
      ) : null}
    </>
  );
}

export const AppExplorer = AppExplorerContainer;
