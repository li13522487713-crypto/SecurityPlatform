import { useState } from "react";
import { Input } from "@douyinfe/semi-ui";
import { IconSearch, IconChevronDown, IconChevronRight } from "@douyinfe/semi-icons";
import { useMendixStudioStore } from "../store";
import type { ActiveTabId } from "../store";

interface TreeNode {
  key: string;
  label: string;
  icon?: string;
  tabId?: ActiveTabId;
  studioTab?: import("../store").MendixStudioTab;
  children?: TreeNode[];
  defaultOpen?: boolean;
}

const TREE_DATA: TreeNode[] = [
  {
    key: "procurement",
    label: "Procurement",
    defaultOpen: true,
    children: [
      {
        key: "domain-model",
        label: "Domain Model",
        defaultOpen: true,
        children: [
          {
            key: "entities",
            label: "实体",
            defaultOpen: true,
            children: [
              { key: "ent_purchase_request", label: "PurchaseRequest", icon: "E" },
              { key: "ent_department", label: "Department", icon: "E" },
              { key: "ent_account", label: "Account", icon: "E" },
              { key: "ent_approval_comment", label: "ApprovalComment", icon: "E" }
            ]
          },
          {
            key: "enumerations",
            label: "枚举",
            children: [
              { key: "enum_purchase_status", label: "PurchaseStatus", icon: "E" }
            ]
          },
          {
            key: "associations",
            label: "关联",
            children: [
              { key: "assoc_applicant", label: "PurchaseRequest_Applicant", icon: "A" },
              { key: "assoc_department", label: "PurchaseRequest_Department", icon: "A" },
              { key: "assoc_approval", label: "PurchaseRequest_ApprovalComment", icon: "A" }
            ]
          }
        ]
      },
      {
        key: "pages",
        label: "Pages",
        defaultOpen: true,
        children: [
          {
            key: "page_purchase_request_edit",
            label: "PurchaseRequest_EditPage",
            icon: "P",
            tabId: "page",
            studioTab: "pageBuilder"
          }
        ]
      },
      {
        key: "microflows",
        label: "Microflows",
        defaultOpen: true,
        children: [
          {
            key: "mf_submit_purchase_request",
            label: "MF_SubmitPurchaseRequest",
            icon: "M",
            tabId: "microflow",
            studioTab: "microflowDesigner"
          }
        ]
      },
      {
        key: "workflows",
        label: "Workflows",
        defaultOpen: true,
        children: [
          {
            key: "wf_purchase_approval",
            label: "WF_PurchaseApproval",
            icon: "W",
            tabId: "workflow",
            studioTab: "workflowDesigner"
          }
        ]
      },
      {
        key: "security",
        label: "Security",
        children: [
          { key: "user-roles", label: "用户角色" },
          { key: "module-roles", label: "模块角色" },
          { key: "permission-matrix", label: "权限矩阵" },
          { key: "entity-access", label: "实体访问" }
        ]
      },
      { key: "navigation", label: "Navigation" },
      { key: "constants", label: "Constants" },
      { key: "theme", label: "Theme" }
    ]
  }
];

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

export function AppExplorer() {
  const [searchText, setSearchText] = useState("");
  const selectedExplorerNodeId = useMendixStudioStore(state => state.selectedExplorerNodeId);
  const setSelectedExplorerNodeId = useMendixStudioStore(state => state.setSelectedExplorerNodeId);
  const setActiveTab = useMendixStudioStore(state => state.setActiveTab);
  const setActiveTabId = useMendixStudioStore(state => state.setActiveTabId);
  const setSelected = useMendixStudioStore(state => state.setSelected);

  const handleSelect = (node: TreeNode) => {
    setSelectedExplorerNodeId(node.key);
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
        {TREE_DATA.map(node => (
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
