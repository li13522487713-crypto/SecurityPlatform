import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Button, Dropdown, Input, Modal, Space, Toast, Typography } from "@douyinfe/semi-ui";
import { IconSearch, IconChevronDown, IconChevronRight } from "@douyinfe/semi-icons";
import { useMendixStudioStore } from "../store";
import type { ActiveTabId } from "../store";
import { SAMPLE_PROCUREMENT_APP } from "../sample-app";
import type { MicroflowAdapterBundle } from "../microflow/adapter/microflow-adapter-factory";
import { getMicroflowApiError, getMicroflowErrorUserMessage } from "../microflow/adapter/http/microflow-api-error";
import type { StudioMicroflowDefinitionView } from "../microflow/studio/studio-microflow-types";
import { mapMicroflowResourceToStudioDefinitionView } from "../microflow/studio/studio-microflow-mappers";
import { CreateMicroflowModal } from "../microflow/resource/CreateMicroflowModal";
import { RenameMicroflowModal } from "../microflow/resource/RenameMicroflowModal";
import { DuplicateMicroflowModal } from "../microflow/resource/DuplicateMicroflowModal";
import type { MicroflowCreateInput, MicroflowDuplicateInput, MicroflowReference } from "../microflow/resource/resource-types";
import { MicroflowReferencesDrawer } from "../microflow/references/MicroflowReferencesDrawer";

interface TreeNode {
  key: string;
  label: string;
  icon?: string;
  kind?:
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
  tabId?: ActiveTabId;
  studioTab?: import("../store").MendixStudioTab;
  moduleId?: string;
  microflowId?: string;
  resourceId?: string;
  referenceCount?: number;
  readonly?: boolean;
  dynamic?: boolean;
  title?: string;
  errorMessage?: string;
  action?: "retryMicroflows";
  children?: TreeNode[];
  defaultOpen?: boolean;
}

interface AppExplorerProps {
  adapterBundle?: MicroflowAdapterBundle;
  workspaceId?: string;
  refreshToken?: number;
}

type MicroflowLoadStatus = "idle" | "loading" | "success" | "error";
type MicroflowCrudAction = "create" | "rename" | "duplicate" | "delete" | "refresh";

const SAMPLE_PROCUREMENT_MODULE = SAMPLE_PROCUREMENT_APP.modules[0];
const EXPLORER_MICROFLOWS_KEY = "microflows";
const explorerMicroflowRequests = new Map<string, Promise<StudioMicroflowDefinitionView[]>>();
const { Text } = Typography;

function getExplorerModuleId(node?: TreeNode): string | undefined {
  return node?.moduleId ?? SAMPLE_PROCUREMENT_MODULE?.moduleId;
}

function getMissingCrudReason(input: {
  workspaceId?: string;
  moduleId?: string;
  adapterBundle?: MicroflowAdapterBundle;
}): string | undefined {
  if (!input.workspaceId) {
    return "缺少 workspaceId";
  }
  if (!input.moduleId) {
    return "缺少 moduleId";
  }
  if (!input.adapterBundle?.resourceAdapter) {
    return "缺少 microflow resource adapter";
  }
  return undefined;
}

function isDuplicateMicroflowName(
  microflows: StudioMicroflowDefinitionView[],
  moduleId: string | undefined,
  name: string,
  excludeId?: string
): boolean {
  const normalizedName = name.trim().toLocaleLowerCase();
  if (!normalizedName) {
    return false;
  }
  return microflows.some(resource =>
    resource.moduleId === moduleId &&
    resource.id !== excludeId &&
    resource.name.toLocaleLowerCase() === normalizedName
  );
}

function validateMicroflowName(
  microflows: StudioMicroflowDefinitionView[],
  moduleId: string | undefined,
  name: string,
  excludeId?: string
): string | undefined {
  const trimmedName = name.trim();
  if (!trimmedName) {
    return "微流 name 不能为空";
  }
  if (trimmedName.includes(" ")) {
    return "微流 name 不能包含空格，请使用下划线";
  }
  if (isDuplicateMicroflowName(microflows, moduleId, trimmedName, excludeId)) {
    return "同一模块下已存在同名微流";
  }
  return undefined;
}

const TREE_DATA: TreeNode[] = [
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
            children: [
              { key: "enum_purchase_status", label: "PurchaseStatus", icon: "E", kind: "entity" }
            ]
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
        key: EXPLORER_MICROFLOWS_KEY,
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

function createMicroflowStateChildren(
  status: MicroflowLoadStatus,
  microflows: StudioMicroflowDefinitionView[],
  error?: string
): TreeNode[] {
  if (status === "loading" || status === "idle") {
    return [{ key: "microflows:loading", label: "Loading microflows...", kind: "loading", readonly: true }];
  }

  if (status === "error") {
    return [{
      key: "microflows:error",
      label: "Load failed / Retry",
      kind: "error",
      readonly: true,
      errorMessage: error,
      action: "retryMicroflows"
    }];
  }

  if (microflows.length === 0) {
    return [{ key: "microflows:empty", label: "No microflows", kind: "empty", readonly: true }];
  }

  return microflows.map(resource => ({
    key: `microflow:${resource.id}`,
    label: resource.displayName || resource.name,
    icon: "M",
    kind: "microflow",
    moduleId: resource.moduleId,
    microflowId: resource.id,
    resourceId: resource.id,
    referenceCount: resource.referenceCount,
    dynamic: true,
    title: resource.qualifiedName
  }));
}

function withMicroflowChildren(nodes: TreeNode[], children: TreeNode[]): TreeNode[] {
  return nodes.map(node => ({
    ...node,
    children: node.key === EXPLORER_MICROFLOWS_KEY
      ? children
      : node.children
        ? withMicroflowChildren(node.children, children)
        : node.children
  }));
}

function getIconColor(icon?: string): string {
  switch (icon) {
    case "P": return "#0958d9";
    case "M": return "#389e0d";
    case "W": return "#d46b08";
    case "E": return "#722ed1";
    case "A": return "#08979c";
    default: return "#6b7280";
  }
}

function getIconBg(icon?: string): string {
  switch (icon) {
    case "P": return "#e6f4ff";
    case "M": return "#f6ffed";
    case "W": return "#fff7e6";
    case "E": return "#f9f0ff";
    case "A": return "#e6fffb";
    default: return "#f0f2f5";
  }
}

interface TreeNodeProps {
  node: TreeNode;
  depth: number;
  selectedId: string;
  searchText: string;
  onSelect: (node: TreeNode) => void;
  renderContextMenu?: (node: TreeNode) => JSX.Element | undefined;
  initialOpen?: boolean;
}

function ExplorerTreeNode({ node, depth, selectedId, searchText, onSelect, renderContextMenu, initialOpen }: TreeNodeProps) {
  const [open, setOpen] = useState(initialOpen ?? node.defaultOpen ?? false);
  const hasChildren = (node.children?.length ?? 0) > 0;

  const labelLower = node.label.toLowerCase();
  const searchLower = searchText.toLowerCase();
  if (searchText && !labelLower.includes(searchLower) && !node.children?.some(c => c.label.toLowerCase().includes(searchLower))) {
    return null;
  }

  const isSelected = selectedId === node.key;
  const contextMenu = renderContextMenu?.(node);
  const nodeContent = (
    <div
      className={"studio-structure-node" + (isSelected ? " studio-structure-node--selected" : "")}
      style={{ paddingLeft: 8 + depth * 14 }}
      title={node.errorMessage ?? node.title ?? node.label}
      onClick={() => {
        if (hasChildren) {
          setOpen(o => !o);
        }
        onSelect(node);
      }}
    >
      {/* 展开箭头 */}
      {hasChildren ? (
        <span style={{ width: 14, flexShrink: 0, color: "#9ca3af", display: "flex", alignItems: "center" }}>
          {open ? <IconChevronDown style={{ fontSize: 12 }} /> : <IconChevronRight style={{ fontSize: 12 }} />}
        </span>
      ) : (
        <span style={{ width: 14, flexShrink: 0 }} />
      )}

      {/* 类型图标 */}
      {node.icon && (
        <span
          className="studio-structure-node__type-badge"
          style={{
            background: getIconBg(node.icon),
            color: getIconColor(node.icon),
            marginRight: 4,
            fontSize: 9
          }}
        >
          {node.icon}
        </span>
      )}

      {/* 标签 */}
      <span
        style={{
          fontSize: 12,
          flex: 1,
          overflow: "hidden",
          textOverflow: "ellipsis",
          whiteSpace: "nowrap",
          color: isSelected ? "var(--studio-blue)" : "var(--studio-text-primary)"
        }}
      >
        {node.label}
      </span>
      {node.kind === "microflow" && typeof node.referenceCount === "number" && node.referenceCount > 0 ? (
        <span
          style={{
            marginLeft: 4,
            padding: "0 5px",
            borderRadius: 10,
            fontSize: 10,
            lineHeight: "16px",
            background: "var(--semi-color-warning-light-default)",
            color: "var(--semi-color-warning)"
          }}
          title={`${node.referenceCount} active or indexed references`}
        >
          ref {node.referenceCount}
        </span>
      ) : null}

      {node.action === "retryMicroflows" && (
        <Button
          theme="borderless"
          type="primary"
          size="small"
          onClick={event => {
            event.stopPropagation();
            onSelect(node);
          }}
        >
          Retry
        </Button>
      )}
    </div>
  );

  return (
    <div>
      {contextMenu ? (
        <Dropdown trigger="contextMenu" position="bottomLeft" render={contextMenu}>
          {nodeContent}
        </Dropdown>
      ) : nodeContent}

      {open && hasChildren && node.children?.map(child => (
        <ExplorerTreeNode
          key={child.key}
          node={child}
          depth={depth + 1}
          selectedId={selectedId}
          searchText={searchText}
          onSelect={onSelect}
          renderContextMenu={renderContextMenu}
          initialOpen={child.defaultOpen}
        />
      ))}
    </div>
  );
}

export function AppExplorer({ adapterBundle, workspaceId, refreshToken }: AppExplorerProps) {
  const [searchText, setSearchText] = useState("");
  const [microflowStatus, setMicroflowStatus] = useState<MicroflowLoadStatus>("idle");
  const [microflowError, setMicroflowError] = useState<string>();
  const [microflows, setMicroflows] = useState<StudioMicroflowDefinitionView[]>([]);
  const [createOpen, setCreateOpen] = useState(false);
  const [renamingMicroflow, setRenamingMicroflow] = useState<StudioMicroflowDefinitionView>();
  const [duplicatingMicroflow, setDuplicatingMicroflow] = useState<StudioMicroflowDefinitionView>();
  const [deleteTarget, setDeleteTarget] = useState<StudioMicroflowDefinitionView>();
  const [deleteReferences, setDeleteReferences] = useState<MicroflowReference[]>();
  const [deleteReferenceError, setDeleteReferenceError] = useState<string>();
  const [deleteReferencesLoading, setDeleteReferencesLoading] = useState(false);
  const [referencesTarget, setReferencesTarget] = useState<StudioMicroflowDefinitionView>();
  const [referencesDrawerSeed, setReferencesDrawerSeed] = useState(0);
  const [crudAction, setCrudAction] = useState<MicroflowCrudAction>();
  const lastRequestKeyRef = useRef<string>();
  const selectedExplorerNodeId = useMendixStudioStore(state => state.selectedExplorerNodeId);
  const activeModuleId = useMendixStudioStore(state => state.activeModuleId);
  const microflowResourcesById = useMendixStudioStore(state => state.microflowResourcesById);
  const setSelectedExplorerNodeId = useMendixStudioStore(state => state.setSelectedExplorerNodeId);
  const setActiveWorkbenchTab = useMendixStudioStore(state => state.setActiveWorkbenchTab);
  const setSelected = useMendixStudioStore(state => state.setSelected);
  const setActiveModuleId = useMendixStudioStore(state => state.setActiveModuleId);
  const openMicroflowWorkbenchTab = useMendixStudioStore(state => state.openMicroflowWorkbenchTab);
  const setModuleMicroflows = useMendixStudioStore(state => state.setModuleMicroflows);
  const upsertStudioMicroflow = useMendixStudioStore(state => state.upsertStudioMicroflow);
  const removeStudioMicroflow = useMendixStudioStore(state => state.removeStudioMicroflow);
  const renameMicroflowWorkbenchTab = useMendixStudioStore(state => state.renameMicroflowWorkbenchTab);
  const moduleNode = useMemo(() => TREE_DATA[0], []);
  const moduleId = getExplorerModuleId(moduleNode);

  const loadMicroflows = useCallback(async (force = false) => {
    if (!workspaceId) {
      setMicroflowStatus("error");
      setMicroflowError("无法加载：缺少 workspaceId");
      setMicroflows([]);
      return;
    }
    if (!moduleId) {
      setMicroflowStatus("error");
      setMicroflowError("无法加载：缺少 moduleId");
      setMicroflows([]);
      return;
    }
    if (!adapterBundle) {
      setMicroflowStatus("error");
      setMicroflowError("无法加载：缺少 microflow adapter");
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
      setMicroflowStatus("error");
      setMicroflowError(caught instanceof Error ? caught.message : String(caught));
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

  const syncResourceToExplorer = useCallback((resource: Parameters<typeof mapMicroflowResourceToStudioDefinitionView>[0]) => {
    const view = mapMicroflowResourceToStudioDefinitionView(resource);
    upsertStudioMicroflow(view);
    setMicroflows(current => sortMicroflowViews([...current.filter(item => item.id !== view.id), view]));
    return view;
  }, [upsertStudioMicroflow]);

  const refreshMicroflows = useCallback(async () => {
    setCrudAction("refresh");
    try {
      await loadMicroflows(true);
      Toast.success("微流列表已刷新");
    } catch (caught) {
      Toast.error(getMicroflowErrorUserMessage(caught));
    } finally {
      setCrudAction(undefined);
    }
  }, [loadMicroflows]);

  const handleCreateMicroflow = useCallback(async (input: MicroflowCreateInput) => {
    const targetModuleId = input.moduleId || moduleId;
    const validationError = validateMicroflowName(microflows, targetModuleId, input.name);
    if (validationError) {
      throw new Error(validationError);
    }
    if (!adapterBundle?.resourceAdapter || !workspaceId || !targetModuleId) {
      throw new Error(getMissingCrudReason({ adapterBundle, moduleId: targetModuleId, workspaceId }) ?? "微流资源服务未就绪");
    }
    setCrudAction("create");
    try {
      const created = await adapterBundle.resourceAdapter.createMicroflow({
        ...input,
        moduleId: targetModuleId,
        moduleName: input.moduleName || targetModuleId,
        displayName: input.displayName?.trim() || input.name.trim()
      });
      syncResourceToExplorer(created);
      Toast.success("微流已创建");
      await loadMicroflows(true);
      return created;
    } catch (caught) {
      Toast.error(getMicroflowErrorUserMessage(caught));
      throw caught;
    } finally {
      setCrudAction(undefined);
    }
  }, [adapterBundle, loadMicroflows, microflows, moduleId, syncResourceToExplorer, workspaceId]);

  const handleRenameMicroflow = useCallback(async (name: string, displayName?: string) => {
    if (!renamingMicroflow) {
      return;
    }
    const validationError = validateMicroflowName(microflows, renamingMicroflow.moduleId, name, renamingMicroflow.id);
    if (validationError) {
      throw new Error(validationError);
    }
    if (!adapterBundle?.resourceAdapter) {
      throw new Error("缺少 microflow resource adapter");
    }
    setCrudAction("rename");
    try {
      const renamed = await adapterBundle.resourceAdapter.renameMicroflow(renamingMicroflow.id, name.trim(), displayName?.trim() || name.trim());
      const view = syncResourceToExplorer(renamed);
      renameMicroflowWorkbenchTab(view.id, view.displayName || view.name);
      Toast.success("微流已重命名");
      await loadMicroflows(true);
      if (referencesTarget?.id === view.id) {
        setReferencesTarget(view);
        setReferencesDrawerSeed(seed => seed + 1);
      }
    } finally {
      setCrudAction(undefined);
    }
  }, [adapterBundle, loadMicroflows, microflows, referencesTarget?.id, renameMicroflowWorkbenchTab, renamingMicroflow, syncResourceToExplorer]);

  const handleDuplicateMicroflow = useCallback(async (input: MicroflowDuplicateInput) => {
    if (!duplicatingMicroflow) {
      return;
    }
    const targetModuleId = input.moduleId || duplicatingMicroflow.moduleId;
    const targetName = input.name?.trim() ?? "";
    const validationError = validateMicroflowName(microflows, targetModuleId, targetName);
    if (validationError) {
      throw new Error(validationError);
    }
    if (!adapterBundle?.resourceAdapter) {
      throw new Error("缺少 microflow resource adapter");
    }
    setCrudAction("duplicate");
    try {
      const duplicated = await adapterBundle.resourceAdapter.duplicateMicroflow(duplicatingMicroflow.id, {
        ...input,
        name: targetName,
        displayName: input.displayName?.trim() || targetName,
        moduleId: targetModuleId,
        moduleName: input.moduleName || duplicatingMicroflow.moduleName || targetModuleId
      });
      syncResourceToExplorer(duplicated);
      Toast.success("微流已复制");
      await loadMicroflows(true);
      setReferencesDrawerSeed(seed => seed + 1);
    } finally {
      setCrudAction(undefined);
    }
  }, [adapterBundle, duplicatingMicroflow, loadMicroflows, microflows, syncResourceToExplorer]);

  const openDeleteDialog = useCallback((resource: StudioMicroflowDefinitionView) => {
    setDeleteTarget(resource);
    setDeleteReferences(undefined);
    setDeleteReferenceError(undefined);
    if (!adapterBundle?.resourceAdapter) {
      setDeleteReferenceError("缺少 microflow resource adapter，无法预检查引用关系。");
      return;
    }
    setDeleteReferencesLoading(true);
    void adapterBundle.resourceAdapter.getMicroflowReferences(resource.id, { includeInactive: false })
      .then(references => {
        setDeleteReferences(references);
      })
      .catch(caught => {
        setDeleteReferenceError(getMicroflowErrorUserMessage(caught));
      })
      .finally(() => {
        setDeleteReferencesLoading(false);
      });
  }, [adapterBundle]);

  const handleDeleteMicroflow = useCallback(async () => {
    if (!deleteTarget) {
      return;
    }
    const activeReferences = (deleteReferences ?? []).filter(reference => reference.active !== false);
    if (activeReferences.length > 0) {
      Toast.warning("该微流正在被其他对象引用，不能直接删除。");
      setReferencesTarget(deleteTarget);
      setReferencesDrawerSeed(seed => seed + 1);
      setDeleteTarget(undefined);
      return;
    }
    if (deleteReferenceError) {
      Toast.error("无法验证引用关系，请稍后重试。");
      return;
    }
    if (!adapterBundle?.resourceAdapter) {
      Toast.error("缺少 microflow resource adapter");
      return;
    }
    setCrudAction("delete");
    try {
      await adapterBundle.resourceAdapter.deleteMicroflow(deleteTarget.id);
      removeStudioMicroflow(deleteTarget.id);
      setMicroflows(current => current.filter(item => item.id !== deleteTarget.id));
      setSelectedExplorerNodeId(EXPLORER_MICROFLOWS_KEY);
      setDeleteTarget(undefined);
      Toast.success("微流已删除");
      await loadMicroflows(true);
    } catch (caught) {
      const apiError = getMicroflowApiError(caught);
      if (apiError.code === "MICROFLOW_REFERENCE_BLOCKED" || apiError.httpStatus === 409) {
        Toast.error(apiError.message || "该微流正在被其他对象引用，不能直接删除。");
        setReferencesTarget(deleteTarget);
        setReferencesDrawerSeed(seed => seed + 1);
        void adapterBundle.resourceAdapter.getMicroflowReferences(deleteTarget.id, { includeInactive: true })
          .then(setDeleteReferences)
          .catch(error => setDeleteReferenceError(getMicroflowErrorUserMessage(error)));
      } else {
        Toast.error(getMicroflowErrorUserMessage(caught));
      }
    } finally {
      setCrudAction(undefined);
    }
  }, [
    adapterBundle,
    deleteReferences,
    deleteTarget,
    loadMicroflows,
    removeStudioMicroflow,
    setSelectedExplorerNodeId
  ]);

  const openReferencesDrawer = useCallback((resource: StudioMicroflowDefinitionView) => {
    setReferencesTarget(resource);
    setReferencesDrawerSeed(seed => seed + 1);
  }, []);

  const treeData = useMemo(
    () => withMicroflowChildren(TREE_DATA, createMicroflowStateChildren(microflowStatus, microflows, microflowError)),
    [microflowError, microflowStatus, microflows]
  );

  const handleSelect = (node: TreeNode) => {
    if (node.action === "retryMicroflows") {
      lastRequestKeyRef.current = undefined;
      void loadMicroflows(true);
      return;
    }

    setSelectedExplorerNodeId(node.key);
    if (node.kind === "microflow" && node.microflowId) {
      const resource = microflowResourcesById[node.microflowId];
      if (!resource) {
        console.warn(`[AppExplorer] Cannot open missing microflow resource: ${node.microflowId}`);
        Toast.warning("微流资源尚未加载完成，请刷新 Microflows 后重试。");
        return;
      }
      setSelected("microflow", node.microflowId);
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
    } else if (node.icon === "M") {
      setSelected("microflow", node.key);
    } else if (node.icon === "W") {
      setSelected("workflow", node.key);
    }
  };

  const renderContextMenu = (node: TreeNode): JSX.Element | undefined => {
    const targetModuleId = getExplorerModuleId(node);
    const missingReason = getMissingCrudReason({ adapterBundle, moduleId: targetModuleId, workspaceId });
    const busy = Boolean(crudAction);

    if (node.key === EXPLORER_MICROFLOWS_KEY) {
      return (
        <Dropdown.Menu>
          {missingReason ? <Dropdown.Item disabled>{missingReason}</Dropdown.Item> : null}
          <Dropdown.Item
            disabled={Boolean(missingReason) || busy}
            onClick={() => setCreateOpen(true)}
          >
            New Microflow
          </Dropdown.Item>
          <Dropdown.Item
            disabled={Boolean(missingReason) || busy}
            onClick={() => void refreshMicroflows()}
          >
            Refresh
          </Dropdown.Item>
        </Dropdown.Menu>
      );
    }

    if (node.kind !== "microflow" || node.readonly || !node.microflowId || !node.dynamic) {
      return undefined;
    }

    const resource = microflows.find(item => item.id === node.microflowId);
    if (!resource) {
      return undefined;
    }

    return (
      <Dropdown.Menu>
        {missingReason ? <Dropdown.Item disabled>{missingReason}</Dropdown.Item> : null}
        <Dropdown.Item
          disabled={Boolean(missingReason) || busy}
          onClick={() => handleSelect(node)}
        >
          Open / Select
        </Dropdown.Item>
        <Dropdown.Item
          disabled={Boolean(missingReason) || busy}
          onClick={() => setRenamingMicroflow(resource)}
        >
          Rename
        </Dropdown.Item>
        <Dropdown.Item
          disabled={Boolean(missingReason) || busy}
          onClick={() => setDuplicatingMicroflow(resource)}
        >
          Duplicate
        </Dropdown.Item>
        <Dropdown.Item
          disabled={Boolean(missingReason) || busy}
          onClick={() => void refreshMicroflows()}
        >
          Refresh
        </Dropdown.Item>
        <Dropdown.Item
          disabled={Boolean(missingReason) || busy}
          onClick={() => openReferencesDrawer(resource)}
        >
          View References
        </Dropdown.Item>
        <Dropdown.Item
          type="danger"
          disabled={Boolean(missingReason) || busy}
          onClick={() => openDeleteDialog(resource)}
        >
          Delete
        </Dropdown.Item>
      </Dropdown.Menu>
    );
  };

  return (
    <div className="studio-explorer">
      <div className="studio-explorer__header">App Explorer</div>

      <div className="studio-explorer__search">
        <Input
          prefix={<IconSearch style={{ fontSize: 13, color: "#9ca3af" }} />}
          placeholder="搜索（⌘K）"
          value={searchText}
          onChange={v => setSearchText(v)}
          style={{ height: 28, fontSize: 12 }}
        />
      </div>

      <div className="studio-explorer__tree">
        {treeData.map(node => (
          <ExplorerTreeNode
            key={node.key}
            node={node}
            depth={0}
            selectedId={selectedExplorerNodeId}
            searchText={searchText}
            onSelect={handleSelect}
            renderContextMenu={renderContextMenu}
            initialOpen={true}
          />
        ))}
      </div>
      <CreateMicroflowModal
        visible={createOpen}
        existingResources={microflows}
        initialModuleId={moduleId}
        initialModuleName={SAMPLE_PROCUREMENT_MODULE?.name}
        moduleLocked
        onClose={() => setCreateOpen(false)}
        onSubmit={handleCreateMicroflow}
      />
      <RenameMicroflowModal
        visible={Boolean(renamingMicroflow)}
        resource={renamingMicroflow}
        onClose={() => setRenamingMicroflow(undefined)}
        onSubmit={handleRenameMicroflow}
      />
      <DuplicateMicroflowModal
        visible={Boolean(duplicatingMicroflow)}
        resource={duplicatingMicroflow}
        onClose={() => setDuplicatingMicroflow(undefined)}
        onSubmit={handleDuplicateMicroflow}
      />
      <Modal
        visible={Boolean(deleteTarget)}
        title="确认删除微流"
        okText="删除"
        cancelText="取消"
        okButtonProps={{
          disabled: deleteReferencesLoading || (deleteReferences ?? []).some(reference => reference.active !== false),
          type: "danger"
        }}
        confirmLoading={crudAction === "delete"}
        onCancel={() => setDeleteTarget(undefined)}
        onOk={() => void handleDeleteMicroflow()}
      >
        <Space vertical align="start" spacing={12} style={{ width: "100%" }}>
          <Text strong>{deleteTarget?.qualifiedName ?? deleteTarget?.displayName}</Text>
          <Text type="danger">删除后不可恢复。</Text>
          {deleteReferencesLoading ? (
            <Text type="tertiary">正在检查引用关系...</Text>
          ) : (deleteReferences ?? []).some(reference => reference.active !== false) ? (
            <Space vertical align="start" spacing={6} style={{ width: "100%" }}>
              <Text type="danger">该微流正在被其他对象引用，不能直接删除。</Text>
              {(deleteReferences ?? []).filter(reference => reference.active !== false).map(reference => (
                <Text key={reference.id} size="small">
                  {reference.sourceType} · {reference.sourceName || reference.sourceId || reference.id}
                  {reference.sourcePath ? ` · ${reference.sourcePath}` : ""}
                </Text>
              ))}
            </Space>
          ) : deleteReferenceError ? (
            <Text type="warning">
              引用关系预检查失败：{deleteReferenceError}。为避免误删，请稍后重试；最终以后端引用保护校验为准。
            </Text>
          ) : (
            <Text type="tertiary">未发现 active 引用，删除时仍以后端校验为准。</Text>
          )}
        </Space>
      </Modal>
      {referencesTarget && adapterBundle?.resourceAdapter ? (
        <MicroflowReferencesDrawer
          key={`${referencesTarget.id}:${referencesDrawerSeed}`}
          visible={Boolean(referencesTarget)}
          resource={referencesTarget}
          adapter={adapterBundle.resourceAdapter}
          resourceIndex={microflowResourcesById}
          onOpenMicroflow={openMicroflowWorkbenchTab}
          onRefreshResourceList={() => loadMicroflows(true)}
          onClose={() => setReferencesTarget(undefined)}
        />
      ) : null}
    </div>
  );
}
