import { useEffect, useMemo, useState } from "react";
import { Button, Dropdown, Input, Space, Typography } from "@douyinfe/semi-ui";
import { IconChevronDown, IconChevronRight, IconSearch } from "@douyinfe/semi-icons";

import type { ExplorerTreeNode } from "./app-explorer";
import { MicroflowsSectionKey } from "./microflow-tree-section";

const { Text } = Typography;

interface AppExplorerTreeProps {
  treeData: ExplorerTreeNode[];
  selectedId: string;
  searchText: string;
  microflowErrorText?: string;
  onSearchTextChange: (value: string) => void;
  onSelect: (node: ExplorerTreeNode) => void;
  onRefreshMicroflows: () => Promise<void>;
  onViewMicroflowReferences?: (node: ExplorerTreeNode) => void;
  onDeleteMicroflow?: (node: ExplorerTreeNode) => void;
}

interface ExplorerTreeNodeProps {
  node: ExplorerTreeNode;
  depth: number;
  selectedId: string;
  searchText: string;
  microflowErrorText?: string;
  onSelect: (node: ExplorerTreeNode) => void;
  onRefreshMicroflows: () => Promise<void>;
  onViewMicroflowReferences?: (node: ExplorerTreeNode) => void;
  onDeleteMicroflow?: (node: ExplorerTreeNode) => void;
  initialOpen?: boolean;
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

function matchesNode(node: ExplorerTreeNode, normalizedSearch: string): boolean {
  if (!normalizedSearch) {
    return true;
  }

  const searchable = [
    node.label,
    node.name,
    node.displayName,
    node.qualifiedName
  ]
    .filter(Boolean)
    .join(" ")
    .toLocaleLowerCase();

  return searchable.includes(normalizedSearch) || Boolean(node.children?.some(child => matchesNode(child, normalizedSearch)));
}

function renderContextMenu(input: {
  node: ExplorerTreeNode;
  onSelect: (node: ExplorerTreeNode) => void;
  onRefreshMicroflows: () => Promise<void>;
  onViewMicroflowReferences?: (node: ExplorerTreeNode) => void;
  onDeleteMicroflow?: (node: ExplorerTreeNode) => void;
}): JSX.Element | undefined {
  const { node, onSelect, onRefreshMicroflows, onViewMicroflowReferences, onDeleteMicroflow } = input;

  if (node.key === MicroflowsSectionKey) {
    return (
      <Dropdown.Menu>
        <Dropdown.Item onClick={() => void onRefreshMicroflows()}>
          Refresh
        </Dropdown.Item>
        <Dropdown.Item disabled title="Release Stage 3 接入 Create API">
          New Microflow
        </Dropdown.Item>
        <Dropdown.Item disabled title="完整属性面板将在后续轮次接入">
          Properties
        </Dropdown.Item>
      </Dropdown.Menu>
    );
  }

  if (node.kind !== "microflow" || node.readonly || !node.dynamic) {
    return undefined;
  }

  return (
    <Dropdown.Menu>
      <Dropdown.Item onClick={() => onSelect(node)}>
        Open / Select
      </Dropdown.Item>
      <Dropdown.Item disabled title="完整属性面板将在后续轮次接入">
        View Properties
      </Dropdown.Item>
      <Dropdown.Item onClick={() => onViewMicroflowReferences?.(node)}>
        View References
      </Dropdown.Item>
      <Dropdown.Item disabled title="Release Stage 3 接入 Rename API">
        Rename
      </Dropdown.Item>
      <Dropdown.Item disabled title="Release Stage 3 接入 Duplicate API">
        Duplicate
      </Dropdown.Item>
      <Dropdown.Item type="danger" onClick={() => onDeleteMicroflow?.(node)}>
        Delete
      </Dropdown.Item>
      <Dropdown.Item onClick={() => void onRefreshMicroflows()}>
        Refresh
      </Dropdown.Item>
    </Dropdown.Menu>
  );
}

function ExplorerTreeNodeView({
  node,
  depth,
  selectedId,
  searchText,
  microflowErrorText,
  onSelect,
  onRefreshMicroflows,
  onViewMicroflowReferences,
  onDeleteMicroflow,
  initialOpen
}: ExplorerTreeNodeProps) {
  const normalizedSearch = searchText.trim().toLocaleLowerCase();
  const [open, setOpen] = useState(initialOpen ?? node.defaultOpen ?? false);
  const hasChildren = (node.children?.length ?? 0) > 0;

  useEffect(() => {
    if (normalizedSearch && hasChildren) {
      setOpen(true);
    }
  }, [hasChildren, normalizedSearch]);

  if (!matchesNode(node, normalizedSearch)) {
    return null;
  }

  const isSelected = selectedId === node.key;
  const contextMenu = renderContextMenu({ node, onSelect, onRefreshMicroflows, onViewMicroflowReferences, onDeleteMicroflow });
  const nodeTitle = node.errorMessage ?? node.title ?? node.qualifiedName ?? node.label;
  const nodeContent = (
    <div
      className={"studio-structure-node" + (isSelected ? " studio-structure-node--selected" : "")}
      style={{ paddingLeft: 8 + depth * 14 }}
      title={nodeTitle}
      onClick={() => {
        if (hasChildren) {
          setOpen(current => !current);
        }
        onSelect(node);
      }}
    >
      {hasChildren ? (
        <span style={{ width: 14, flexShrink: 0, color: "#9ca3af", display: "flex", alignItems: "center" }}>
          {open ? <IconChevronDown style={{ fontSize: 12 }} /> : <IconChevronRight style={{ fontSize: 12 }} />}
        </span>
      ) : (
        <span style={{ width: 14, flexShrink: 0 }} />
      )}

      {node.icon && (
        <span
          className="studio-structure-node__type-badge"
          style={{ background: getIconBg(node.icon), color: getIconColor(node.icon), marginRight: 4, fontSize: 9 }}
        >
          {node.icon}
        </span>
      )}

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

      {node.kind === "microflow" && node.status ? (
        <span style={{ marginLeft: 4, fontSize: 10, color: "var(--semi-color-text-2)" }} title={node.publishStatus}>
          {node.status}
        </span>
      ) : null}
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
      {node.kind === "microflow" && node.problemSummary && (node.problemSummary.errorCount > 0 || node.problemSummary.warningCount > 0) ? (
        <span
          style={{
            marginLeft: 4,
            padding: "0 5px",
            borderRadius: 10,
            fontSize: 10,
            lineHeight: "16px",
            background: node.problemSummary.errorCount > 0 ? "var(--semi-color-danger-light-default)" : "var(--semi-color-warning-light-default)",
            color: node.problemSummary.errorCount > 0 ? "var(--semi-color-danger)" : "var(--semi-color-warning)"
          }}
          title={`Validation status from last check. ${node.problemSummary.errorCount} errors, ${node.problemSummary.warningCount} warnings.`}
        >
          {node.problemSummary.errorCount > 0 ? `err ${node.problemSummary.errorCount}` : `warn ${node.problemSummary.warningCount}`}
        </span>
      ) : null}

      {node.action === "retryMicroflows" ? (
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
      ) : null}
    </div>
  );

  return (
    <div>
      {contextMenu ? (
        <Dropdown trigger="contextMenu" position="bottomLeft" render={contextMenu}>
          {nodeContent}
        </Dropdown>
      ) : nodeContent}

      {node.kind === "error" && microflowErrorText ? (
        <div style={{ paddingLeft: 22 + depth * 14, paddingRight: 8, paddingBottom: 4 }}>
          <Text type="danger" size="small">{microflowErrorText}</Text>
        </div>
      ) : null}

      {open && hasChildren && node.children?.map(child => (
        <ExplorerTreeNodeView
          key={child.key}
          node={child}
          depth={depth + 1}
          selectedId={selectedId}
          searchText={searchText}
          microflowErrorText={microflowErrorText}
          onSelect={onSelect}
          onRefreshMicroflows={onRefreshMicroflows}
          onViewMicroflowReferences={onViewMicroflowReferences}
          onDeleteMicroflow={onDeleteMicroflow}
          initialOpen={child.defaultOpen}
        />
      ))}
    </div>
  );
}

export function AppExplorerTree({
  treeData,
  selectedId,
  searchText,
  microflowErrorText,
  onSearchTextChange,
  onSelect,
  onRefreshMicroflows,
  onViewMicroflowReferences,
  onDeleteMicroflow
}: AppExplorerTreeProps) {
  const [draftSearchText, setDraftSearchText] = useState(searchText);

  useEffect(() => {
    const timer = window.setTimeout(() => {
      onSearchTextChange(draftSearchText);
    }, 200);
    return () => window.clearTimeout(timer);
  }, [draftSearchText, onSearchTextChange]);

  const visibleTreeData = useMemo(() => treeData, [treeData]);

  return (
    <div className="studio-explorer">
      <div className="studio-explorer__header">
        <Space style={{ width: "100%", justifyContent: "space-between" }}>
          <span>App Explorer</span>
          <Button
            theme="borderless"
            size="small"
            onClick={() => void onRefreshMicroflows()}
            title="Refresh Microflows"
          >
            Refresh
          </Button>
        </Space>
      </div>

      <div className="studio-explorer__search">
        <Input
          prefix={<IconSearch style={{ fontSize: 13, color: "#9ca3af" }} />}
          placeholder="搜索（⌘K）"
          value={draftSearchText}
          onChange={value => setDraftSearchText(value)}
          style={{ height: 28, fontSize: 12 }}
        />
      </div>

      <div className="studio-explorer__tree">
        {visibleTreeData.map(node => (
          <ExplorerTreeNodeView
            key={node.key}
            node={node}
            depth={0}
            selectedId={selectedId}
            searchText={searchText}
            microflowErrorText={microflowErrorText}
            onSelect={onSelect}
            onRefreshMicroflows={onRefreshMicroflows}
            onViewMicroflowReferences={onViewMicroflowReferences}
            onDeleteMicroflow={onDeleteMicroflow}
            initialOpen
          />
        ))}
      </div>
    </div>
  );
}
