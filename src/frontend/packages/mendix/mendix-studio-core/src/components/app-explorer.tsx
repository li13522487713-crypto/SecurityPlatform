import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Button, Input } from "@douyinfe/semi-ui";
import { IconSearch, IconChevronDown, IconChevronRight } from "@douyinfe/semi-icons";
import { useMendixStudioStore } from "../store";
import type { ActiveTabId } from "../store";
import { SAMPLE_PROCUREMENT_APP } from "../sample-app";
import type { MicroflowAdapterBundle } from "../microflow/adapter/microflow-adapter-factory";
import type { StudioMicroflowDefinitionView } from "../microflow/studio/studio-microflow-types";
import { mapMicroflowResourceToStudioDefinitionView } from "../microflow/studio/studio-microflow-mappers";

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
}

type MicroflowLoadStatus = "idle" | "loading" | "success" | "error";

const SAMPLE_PROCUREMENT_MODULE = SAMPLE_PROCUREMENT_APP.modules[0];
const EXPLORER_MICROFLOWS_KEY = "microflows";
const explorerMicroflowRequests = new Map<string, Promise<StudioMicroflowDefinitionView[]>>();

function getExplorerModuleId(node?: TreeNode): string | undefined {
  return node?.moduleId ?? SAMPLE_PROCUREMENT_MODULE?.moduleId;
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
  initialOpen?: boolean;
}

function ExplorerTreeNode({ node, depth, selectedId, searchText, onSelect, initialOpen }: TreeNodeProps) {
  const [open, setOpen] = useState(initialOpen ?? node.defaultOpen ?? false);
  const hasChildren = (node.children?.length ?? 0) > 0;

  const labelLower = node.label.toLowerCase();
  const searchLower = searchText.toLowerCase();
  if (searchText && !labelLower.includes(searchLower) && !node.children?.some(c => c.label.toLowerCase().includes(searchLower))) {
    return null;
  }

  const isSelected = selectedId === node.key;

  return (
    <div>
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

      {open && hasChildren && node.children?.map(child => (
        <ExplorerTreeNode
          key={child.key}
          node={child}
          depth={depth + 1}
          selectedId={selectedId}
          searchText={searchText}
          onSelect={onSelect}
          initialOpen={child.defaultOpen}
        />
      ))}
    </div>
  );
}

export function AppExplorer({ adapterBundle, workspaceId }: AppExplorerProps) {
  const [searchText, setSearchText] = useState("");
  const [microflowStatus, setMicroflowStatus] = useState<MicroflowLoadStatus>("idle");
  const [microflowError, setMicroflowError] = useState<string>();
  const [microflows, setMicroflows] = useState<StudioMicroflowDefinitionView[]>([]);
  const lastRequestKeyRef = useRef<string>();
  const selectedExplorerNodeId = useMendixStudioStore(state => state.selectedExplorerNodeId);
  const activeModuleId = useMendixStudioStore(state => state.activeModuleId);
  const setSelectedExplorerNodeId = useMendixStudioStore(state => state.setSelectedExplorerNodeId);
  const setActiveTab = useMendixStudioStore(state => state.setActiveTab);
  const setActiveTabId = useMendixStudioStore(state => state.setActiveTabId);
  const setSelected = useMendixStudioStore(state => state.setSelected);
  const setActiveModuleId = useMendixStudioStore(state => state.setActiveModuleId);
  const setActiveMicroflowId = useMendixStudioStore(state => state.setActiveMicroflowId);
  const setModuleMicroflows = useMendixStudioStore(state => state.setModuleMicroflows);
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
      setSelected("microflow", node.microflowId);
      setActiveMicroflowId(node.microflowId);
      setActiveModuleId(node.moduleId);
      return;
    }

    if (node.studioTab) {
      setActiveTab(node.studioTab);
    }
    if (node.tabId) {
      setActiveTabId(node.tabId);
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
            initialOpen={true}
          />
        ))}
      </div>
    </div>
  );
}
