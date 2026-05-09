import { useEffect, useMemo, useRef, useState, type CSSProperties, type ReactNode } from "react";
import { Button, Card, Empty, Input, Popover, Space, Tabs, Tag, Toast, Tooltip, Typography } from "@douyinfe/semi-ui";
import {
  IconChevronDown,
  IconChevronRight,
  IconCopy,
  IconFilter,
  IconInfoCircle,
  IconPlus,
  IconSearch,
  IconStar,
  IconStarStroked,
  IconStop
} from "@douyinfe/semi-icons";
import type { MicroflowRegistryNodeType, MicroflowActivityType } from "../schema";
import {
  defaultMicroflowNodePanelRegistry,
  canDragRegistryItem,
  canCreateRegistryItem,
  createDragPayloadFromRegistryItem,
  beginMicroflowNodePointerDrag,
  getMicroflowNodeDisabledReason,
  getMicroflowNodeRegistryKey,
  groupMicroflowNodesByCategory,
  searchMicroflowNodes,
  type MicroflowNodeDragPayload,
  type MicroflowNodeCreateContext,
  type MicroflowNodeFilterKey,
  type MicroflowNodePanelCategoryKey,
  type MicroflowNodeRegistryEntry,
  type MicroflowNodeRegistryItem,
  MICROFLOW_NODE_DND_TYPE
} from "../node-registry";

const { Text } = Typography;
const nodePanelCategoryStorageKey = "atlas_microflow_node_panel_categories";
const nodePanelGroupStorageKey = "atlas_microflow_node_panel_groups";

export interface MicroflowNodePanelLabels {
  nodesTab: string;
  componentsTab: string;
  templatesTab: string;
  searchPlaceholder: string;
  filterTitle: string;
  filterAll: string;
  filterFavorites: string;
  filterEnabled: string;
  filterSupported: string;
  favoritesTitle: string;
  favoritesEmpty: string;
  addToCanvas: string;
  favorite: string;
  unfavorite: string;
  viewDocumentation: string;
  copyNodeType: string;
  copied: string;
  disabled: string;
  emptyTitle: string;
  emptyDescription: string;
  clearSearch: string;
  footerHint: string;
  componentsPlaceholder: string;
  templatesPlaceholder: string;
  insertTemplate: string;
  inputs: string;
  outputs: string;
  useCases: string;
}

export const defaultMicroflowNodePanelLabels: MicroflowNodePanelLabels = {
  nodesTab: "Nodes",
  componentsTab: "Components",
  templatesTab: "Templates",
  searchPlaceholder: "Search node name or capability",
  filterTitle: "Filters",
  filterAll: "All nodes",
  filterFavorites: "Favorites only",
  filterEnabled: "Enabled only",
  filterSupported: "Runtime supported only",
  favoritesTitle: "My favorites",
  favoritesEmpty: "Favorite frequently used nodes for quick access.",
  addToCanvas: "Add to canvas",
  favorite: "Favorite",
  unfavorite: "Remove favorite",
  viewDocumentation: "View documentation",
  copyNodeType: "Copy node type",
  copied: "Node type copied",
  disabled: "Disabled",
  emptyTitle: "No matching nodes",
  emptyDescription: "Try another keyword or clear filters.",
  clearSearch: "Clear search",
  footerHint: "Drag nodes to the canvas, or double-click to add quickly.",
  componentsPlaceholder: "Component assets will be available here.",
  templatesPlaceholder: "Microflow templates will be available here.",
  insertTemplate: "Insert",
  inputs: "Inputs",
  outputs: "Outputs",
  useCases: "Use cases"
};

export interface MicroflowNodePanelTemplate {
  id: string;
  name: string;
  description: string;
  category: "component" | "template";
  nodeKeys: string[];
  flowPairs?: Array<{ from: number; to: number; label?: string }>;
  defaultOffset?: { x: number; y: number };
  requiredCapabilities?: string[];
}

const defaultMicroflowTemplates: MicroflowNodePanelTemplate[] = [
  {
    id: "component-decision-merge",
    name: "Decision branch",
    description: "Decision, two actions, and merge.",
    category: "component",
    nodeKeys: ["decision", "activity:changeVariable", "activity:logMessage", "merge"],
    flowPairs: [{ from: 0, to: 1, label: "true" }, { from: 0, to: 2, label: "false" }, { from: 1, to: 3 }, { from: 2, to: 3 }],
  },
  {
    id: "template-rest-validate-log",
    name: "REST with validation",
    description: "Retrieve inputs, call REST, then log the outcome.",
    category: "template",
    nodeKeys: ["activity:retrieve", "activity:restCall", "activity:logMessage"],
    flowPairs: [{ from: 0, to: 1 }, { from: 1, to: 2 }],
    requiredCapabilities: ["restCall"],
  },
];

export interface MicroflowNodePanelProps {
  registry?: MicroflowNodeRegistryItem[];
  favoriteNodeKeys: string[];
  onFavoriteChange: (keys: string[]) => void;
  onAddNode: (item: MicroflowNodeRegistryItem, options?: { source: "doubleClick" | "contextMenu" | "drop"; position?: { x: number; y: number } }) => void;
  onInsertTemplate?: (template: MicroflowNodePanelTemplate) => void;
  onStartDrag?: (payload: MicroflowNodeDragPayload) => void;
  onShowDocumentation?: (item: MicroflowNodeRegistryItem) => void;
  labels?: Partial<MicroflowNodePanelLabels>;
  createContext?: MicroflowNodeCreateContext;
}

interface ContextMenuState {
  item: MicroflowNodeRegistryItem;
  x: number;
  y: number;
}

interface RegistryAdapterOptions {
  registry: MicroflowNodeRegistryItem[];
  keyword: string;
  filterKey: MicroflowNodeFilterKey;
  favoriteNodeKeys: string[];
}

const categoryFilterLabels: Record<MicroflowNodePanelCategoryKey, string> = {
  events: "Events",
  inputs: "Inputs",
  flowControl: "Flow Control",
  loops: "Loops",
  variables: "Variables",
  objects: "Objects",
  lists: "Lists / Collections",
  integration: "Integration",
  documentation: "Documentation",
  other: "Other"
};

const categoryStyle: CSSProperties = {
  borderBottom: "1px solid var(--semi-color-border)",
  background: "transparent",
  overflow: "hidden"
};

const cardBaseStyle: CSSProperties = {
  position: "relative",
  display: "flex",
  gap: 8,
  alignItems: "center",
  width: "100%",
  minHeight: 34,
  padding: "6px 8px",
  border: "1px solid transparent",
  borderRadius: 6,
  background: "transparent",
  boxSizing: "border-box",
  textAlign: "left",
  transition: "background 120ms ease, border-color 120ms ease",
  userSelect: "none"
};

function readStoredStringList(key: string, fallback: string[]): string[] {
  if (typeof window === "undefined") {
    return fallback;
  }
  try {
    const parsed = JSON.parse(window.localStorage.getItem(key) ?? "null") as unknown;
    return Array.isArray(parsed) && parsed.every(item => typeof item === "string") ? parsed : fallback;
  } catch {
    return fallback;
  }
}

function writeStoredStringList(key: string, value: string[]): void {
  if (typeof window === "undefined") {
    return;
  }
  try {
    window.localStorage.setItem(key, JSON.stringify(value));
  } catch {
    /* ignore */
  }
}

function isCategoryFilter(filterKey: MicroflowNodeFilterKey): filterKey is MicroflowNodePanelCategoryKey {
  return ["events", "inputs", "flowControl", "loops", "variables", "objects", "lists", "integration", "documentation", "other"].includes(filterKey);
}

function categoryKeyFromEntry(entry: MicroflowNodeRegistryEntry): MicroflowNodePanelCategoryKey {
  if (entry.group === "Events" && entry.type !== "breakEvent" && entry.type !== "continueEvent") {
    return "events";
  }
  if (entry.group === "Parameters") {
    return "inputs";
  }
  if (entry.group === "Loop" || entry.type === "breakEvent" || entry.type === "continueEvent") {
    return "loops";
  }
  if (entry.group === "Decisions") {
    return "flowControl";
  }
  if (entry.subgroup === "variable") {
    return "variables";
  }
  if (entry.subgroup === "object") {
    return "objects";
  }
  if (entry.subgroup === "list") {
    return "lists";
  }
  if (entry.subgroup === "call" || entry.subgroup === "integration") {
    return "integration";
  }
  if (entry.group === "Annotations") {
    return "documentation";
  }
  return "other";
}

function iconTone(item: MicroflowNodeRegistryItem): { background: string; color: string } {
  if (item.group === "Events") {
    return { background: "#e8f8ef", color: "#12b886" };
  }
  if (item.group === "Decisions") {
    return { background: "#fff7e8", color: "#ff8800" };
  }
  if (item.subgroup === "object") {
    return { background: "#eef4ff", color: "#165dff" };
  }
  if (item.subgroup === "list") {
    return { background: "#fff9db", color: "#d48806" };
  }
  if (item.subgroup === "call") {
    return { background: "#f2edff", color: "#722ed1" };
  }
  if (item.subgroup === "variable") {
    return { background: "#e6fffb", color: "#13a8a8" };
  }
  if (item.subgroup === "client") {
    return { background: "#f0f8e8", color: "#52c41a" };
  }
  if (item.subgroup === "integration") {
    return { background: "#fff1f0", color: "#f93920" };
  }
  return { background: "#f2f3f5", color: "#4e5969" };
}

function MicroflowNodeIcon({ item }: { item: MicroflowNodeRegistryItem }) {
  const tone = iconTone(item);
  const label = item.activityType?.slice(0, 1) ?? item.type.slice(0, 1);
  return (
    <span
      style={{
        width: 22,
        height: 22,
        borderRadius: item.render.shape === "diamond" ? 7 : item.render.shape === "event" ? 999 : 8,
        display: "inline-flex",
        alignItems: "center",
        justifyContent: "center",
        background: tone.background,
        color: tone.color,
        fontSize: 11,
        fontWeight: 700,
        border: `1px solid ${tone.color}22`
      }}
    >
      {label.toUpperCase()}
    </span>
  );
}

export function MicroflowNodePanelRegistryAdapter({ registry, keyword, filterKey, favoriteNodeKeys }: RegistryAdapterOptions) {
  const favoriteSet = new Set(favoriteNodeKeys);
  const searched = searchMicroflowNodes(keyword, registry);
  const filtered = searched.filter(item => {
    if (filterKey === "favorites") {
      return favoriteSet.has(getMicroflowNodeRegistryKey(item));
    }
    if (filterKey === "enabled") {
      return canDragRegistryItem(item);
    }
    if (filterKey === "supported") {
      return item.engineSupport?.level === "supported";
    }
    if (isCategoryFilter(filterKey)) {
      return categoryKeyFromEntry(item) === filterKey;
    }
    return true;
  });
  return groupMicroflowNodesByCategory(filtered);
}

export function MicroflowNodePanelTabs({
  activeKey,
  onChange,
  labels
}: {
  activeKey: "nodes" | "components" | "templates";
  onChange: (key: "nodes" | "components" | "templates") => void;
  labels: MicroflowNodePanelLabels;
}) {
  return (
    <Tabs type="button" size="small" activeKey={activeKey} onChange={key => onChange(key as "nodes" | "components" | "templates")}>
      <Tabs.TabPane tab={labels.nodesTab} itemKey="nodes" />
      <Tabs.TabPane tab={labels.componentsTab} itemKey="components" />
      <Tabs.TabPane tab={labels.templatesTab} itemKey="templates" />
    </Tabs>
  );
}

export function MicroflowNodeSearch({
  value,
  onChange,
  filterKey,
  onFilterChange,
  labels
}: {
  value: string;
  onChange: (value: string) => void;
  filterKey: MicroflowNodeFilterKey;
  onFilterChange: (key: MicroflowNodeFilterKey) => void;
  labels: MicroflowNodePanelLabels;
}) {
  const filterOptions: Array<{ key: MicroflowNodeFilterKey; label: string }> = [
    { key: "all", label: labels.filterAll },
    { key: "favorites", label: labels.filterFavorites },
    { key: "enabled", label: labels.filterEnabled },
    { key: "supported", label: labels.filterSupported },
    ...Object.entries(categoryFilterLabels).map(([key, label]) => ({ key: key as MicroflowNodePanelCategoryKey, label }))
  ];

  return (
    <div style={{ display: "grid", gridTemplateColumns: "minmax(0, 1fr) 36px", gap: 8 }}>
      <div data-testid="microflow-node-panel-search">
        <Input
          className="microflow-node-search-input"
          prefix={<IconSearch />}
          value={value}
          onChange={onChange}
          placeholder={labels.searchPlaceholder}
          showClear
        />
      </div>
      <Popover
        trigger="click"
        position="bottomRight"
        content={(
          <Space vertical align="start" spacing={6} style={{ width: 164, padding: 4 }}>
            <Text strong>{labels.filterTitle}</Text>
            {filterOptions.map(option => (
              <Button
                key={option.key}
                block
                size="small"
                theme={filterKey === option.key ? "solid" : "borderless"}
                type={filterKey === option.key ? "primary" : "tertiary"}
                onClick={() => onFilterChange(option.key)}
              >
                {option.label}
              </Button>
            ))}
          </Space>
        )}
      >
        <Button icon={<IconFilter />} theme={filterKey === "all" ? "light" : "solid"} type={filterKey === "all" ? "tertiary" : "primary"} />
      </Popover>
    </div>
  );
}

export function MicroflowNodeCard({
  item,
  favorite,
  compact,
  onAdd,
  onContextMenu,
  onStartDrag,
  createContext
}: {
  item: MicroflowNodeRegistryItem;
  favorite: boolean;
  labels: MicroflowNodePanelLabels;
  compact?: boolean;
  onAdd: (item: MicroflowNodeRegistryItem) => void;
  onFavoriteToggle: (item: MicroflowNodeRegistryItem) => void;
  onContextMenu: (item: MicroflowNodeRegistryItem, point: { x: number; y: number }) => void;
  onStartDrag?: (payload: MicroflowNodeDragPayload) => void;
  createContext?: MicroflowNodeCreateContext;
}) {
  const disabledReason = getMicroflowNodeDisabledReason(item, createContext);
  const disabled = Boolean(disabledReason);
  const key = getMicroflowNodeRegistryKey(item);
  const draggingRef = useRef(false);
  const [active, setActive] = useState(false);
  const [keyboardFocus, setKeyboardFocus] = useState(false);
  const [dragging, setDragging] = useState(false);
  const cardActive = active || keyboardFocus;
  const cardStyle: CSSProperties = {
    ...cardBaseStyle,
    minHeight: compact ? 30 : 34,
    padding: compact ? "4px 8px" : cardBaseStyle.padding,
    opacity: disabled ? 0.58 : 1,
    cursor: disabled ? "not-allowed" : dragging ? "grabbing" : "grab",
    background: cardActive
      ? favorite ? "var(--semi-color-warning-light-default, rgba(255, 247, 217, 0.98))" : "var(--semi-color-fill-0)"
      : favorite ? "var(--semi-color-warning-light-default, rgba(255, 250, 232, 0.4))" : cardBaseStyle.background,
    borderColor: cardActive
      ? favorite ? "rgba(255, 177, 0, 0.66)" : "var(--semi-color-border)"
      : favorite ? "rgba(255, 177, 0, 0.38)" : cardBaseStyle.border as string
  };

  return (
    <div
      role="button"
      tabIndex={disabled ? -1 : 0}
      draggable={!disabled}
      data-testid={`microflow-node-panel-item-${key.replace(/[^A-Za-z0-9_-]+/gu, "-")}`}
      data-registry-key={key}
      data-node-type={item.type}
      data-action-kind={item.actionKind}
      style={cardStyle}
      onMouseEnter={() => setActive(true)}
      onMouseLeave={() => setActive(false)}
      onFocus={() => setKeyboardFocus(true)}
      onBlur={() => setKeyboardFocus(false)}
      onMouseDown={event => {
        if (disabled || event.button !== 0) {
          return;
        }
        beginMicroflowNodePointerDrag(createDragPayloadFromRegistryItem(item));
      }}
      onClick={event => {
        if (draggingRef.current) {
          event.preventDefault();
          event.stopPropagation();
        }
      }}
      onDoubleClick={() => {
        if (!disabled) {
          onAdd(item);
        }
      }}
      onKeyDown={event => {
        if (disabled) {
          return;
        }
        if (event.key === "Enter") {
          event.preventDefault();
          onAdd(item);
        }
      }}
      onContextMenu={event => {
        event.preventDefault();
        onContextMenu(item, { x: event.clientX, y: event.clientY });
      }}
      onDragStart={event => {
        if (disabled) {
          event.preventDefault();
          return;
        }
        draggingRef.current = true;
        setDragging(true);
        const payload = createDragPayloadFromRegistryItem(item);
        event.dataTransfer.effectAllowed = "copy";
        event.dataTransfer.setData(MICROFLOW_NODE_DND_TYPE, JSON.stringify(payload));
        event.dataTransfer.setData("application/json", JSON.stringify(payload));
        event.dataTransfer.setData("text/plain", payload.registryKey);
        onStartDrag?.(payload);
      }}
      onDragEnd={() => {
        window.setTimeout(() => {
          draggingRef.current = false;
          setDragging(false);
        }, 0);
      }}
    >
      <MicroflowNodeIcon item={item} />
      <div
        style={{
          flex: 1,
          minWidth: 0,
          display: "flex",
          alignItems: "center",
          gap: 6
        }}
      >
        <Text
          strong
          title={item.titleZh}
          style={{ flexShrink: 0, maxWidth: "100%", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap", lineHeight: "18px" }}
        >
          {item.titleZh}
        </Text>
        <Text
          type="tertiary"
          size="small"
          title={item.title}
          style={{ flex: 1, minWidth: 0, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap", lineHeight: "17px", textAlign: "right" }}
        >
          {item.title}
        </Text>
      </div>
    </div>
  );
}

export function MicroflowNodeFavorites({
  items,
  favoriteNodeKeys,
  labels,
  createContext,
  onAdd,
  onFavoriteToggle,
  onContextMenu,
  onStartDrag
}: {
  items: MicroflowNodeRegistryItem[];
  favoriteNodeKeys: string[];
  labels: MicroflowNodePanelLabels;
  createContext?: MicroflowNodeCreateContext;
  onAdd: (item: MicroflowNodeRegistryItem) => void;
  onFavoriteToggle: (item: MicroflowNodeRegistryItem) => void;
  onContextMenu: (item: MicroflowNodeRegistryItem, point: { x: number; y: number }) => void;
  onStartDrag?: (payload: MicroflowNodeDragPayload) => void;
}) {
  const favoriteSet = new Set(favoriteNodeKeys);
  const favoriteItems = favoriteNodeKeys
    .map(key => items.find(item => getMicroflowNodeRegistryKey(item) === key))
    .filter((item): item is MicroflowNodeRegistryItem => Boolean(item));

  return (
    <section style={{ padding: "10px 0 2px" }}>
      <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: 8 }}>
        <Text strong>{labels.favoritesTitle}</Text>
        <Tag size="small">{favoriteItems.length}</Tag>
      </div>
      {favoriteItems.length === 0 ? (
        <Text type="tertiary" size="small">{labels.favoritesEmpty}</Text>
      ) : (
        <div style={{ display: "flex", flexDirection: "column", gap: 2 }}>
          {favoriteItems.map(item => (
            <MicroflowNodeCard
              key={getMicroflowNodeRegistryKey(item)}
              item={item}
              compact
              favorite={favoriteSet.has(getMicroflowNodeRegistryKey(item))}
              labels={labels}
              onAdd={onAdd}
              onFavoriteToggle={onFavoriteToggle}
              onContextMenu={onContextMenu}
              onStartDrag={onStartDrag}
              createContext={createContext}
            />
          ))}
        </div>
      )}
    </section>
  );
}

export function MicroflowNodeCategorySection({
  category,
  open,
  expandedGroups,
  favoriteNodeKeys,
  labels,
  createContext,
  onToggle,
  onToggleGroup,
  onAdd,
  onFavoriteToggle,
  onContextMenu,
  onStartDrag
}: {
  category: ReturnType<typeof groupMicroflowNodesByCategory>[number];
  open: boolean;
  expandedGroups: string[];
  favoriteNodeKeys: string[];
  labels: MicroflowNodePanelLabels;
  createContext?: MicroflowNodeCreateContext;
  onToggle: (key: string) => void;
  onToggleGroup: (key: string) => void;
  onAdd: (item: MicroflowNodeRegistryItem) => void;
  onFavoriteToggle: (item: MicroflowNodeRegistryItem) => void;
  onContextMenu: (item: MicroflowNodeRegistryItem, point: { x: number; y: number }) => void;
  onStartDrag?: (payload: MicroflowNodeDragPayload) => void;
}) {
  const favoriteSet = new Set(favoriteNodeKeys);

  return (
    <section data-testid={`microflow-node-panel-category-${category.category.key}`} style={categoryStyle}>
      <button
        type="button"
        data-testid={`microflow-node-panel-category-toggle-${category.category.key}`}
        style={{
          width: "100%",
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
          gap: 8,
          border: 0,
          background: open ? "rgba(22, 93, 255, 0.05)" : "transparent",
          padding: "9px 10px",
          cursor: "pointer"
        }}
        onClick={() => onToggle(category.category.key)}
      >
        <span style={{ display: "inline-flex", alignItems: "center", gap: 6 }}>
          {open ? <IconChevronDown size="small" /> : <IconChevronRight size="small" />}
          <Text strong>{category.category.label}</Text>
        </span>
        <Tag size="small">{category.entries.length}</Tag>
      </button>
      {open ? (
        <div style={{ padding: "4px 4px 8px", display: "flex", flexDirection: "column", gap: 2 }}>
          {category.groups.length > 0 ? category.groups.map(group => {
            const groupPanelKey = `${category.category.key}:${group.key}`;
            const groupOpen = expandedGroups.includes(groupPanelKey);
            return (
              <div key={group.key} data-testid={`microflow-node-panel-group-${group.key}`} style={{ display: "flex", flexDirection: "column", gap: 2, paddingBottom: 4 }}>
                <button
                  type="button"
                  data-testid={`microflow-node-panel-group-toggle-${group.key}`}
                  style={{
                    border: 0,
                    background: "transparent",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "space-between",
                    padding: "2px 2px",
                    cursor: "pointer"
                  }}
                  onClick={() => onToggleGroup(groupPanelKey)}
                >
                  <span style={{ display: "inline-flex", alignItems: "center", gap: 5 }}>
                    {groupOpen ? <IconChevronDown size="small" /> : <IconChevronRight size="small" />}
                    <Text size="small">{group.label}</Text>
                  </span>
                  <Text type="tertiary" size="small">{group.entries.length}</Text>
                </button>
                {groupOpen ? group.entries.map(item => (
                  <MicroflowNodeCard
                    key={getMicroflowNodeRegistryKey(item)}
                    item={item}
                    favorite={favoriteSet.has(getMicroflowNodeRegistryKey(item))}
                    labels={labels}
                    onAdd={onAdd}
                    onFavoriteToggle={onFavoriteToggle}
                    onContextMenu={onContextMenu}
                    onStartDrag={onStartDrag}
                    createContext={createContext}
                  />
                )) : null}
              </div>
            );
          }) : category.entries.map(item => (
            <MicroflowNodeCard
              key={getMicroflowNodeRegistryKey(item)}
              item={item}
              favorite={favoriteSet.has(getMicroflowNodeRegistryKey(item))}
              labels={labels}
              onAdd={onAdd}
              onFavoriteToggle={onFavoriteToggle}
              onContextMenu={onContextMenu}
              onStartDrag={onStartDrag}
              createContext={createContext}
            />
          ))}
        </div>
      ) : null}
    </section>
  );
}

export function MicroflowNodeCategoryList({
  grouped,
  expandedCategories,
  expandedGroups,
  favoriteNodeKeys,
  labels,
  createContext,
  onToggleCategory,
  onToggleGroup,
  onAdd,
  onFavoriteToggle,
  onContextMenu,
  onStartDrag
}: {
  grouped: ReturnType<typeof groupMicroflowNodesByCategory>;
  expandedCategories: string[];
  expandedGroups: string[];
  favoriteNodeKeys: string[];
  labels: MicroflowNodePanelLabels;
  createContext?: MicroflowNodeCreateContext;
  onToggleCategory: (key: string) => void;
  onToggleGroup: (key: string) => void;
  onAdd: (item: MicroflowNodeRegistryItem) => void;
  onFavoriteToggle: (item: MicroflowNodeRegistryItem) => void;
  onContextMenu: (item: MicroflowNodeRegistryItem, point: { x: number; y: number }) => void;
  onStartDrag?: (payload: MicroflowNodeDragPayload) => void;
}) {
  return (
    <div style={{ display: "flex", flexDirection: "column" }}>
      {grouped.map(category => (
        <MicroflowNodeCategorySection
          key={category.category.key}
          category={category}
          open={expandedCategories.includes(category.category.key)}
          expandedGroups={expandedGroups}
          favoriteNodeKeys={favoriteNodeKeys}
          labels={labels}
          onToggle={onToggleCategory}
          onToggleGroup={onToggleGroup}
          onAdd={onAdd}
          onFavoriteToggle={onFavoriteToggle}
          onContextMenu={onContextMenu}
          onStartDrag={onStartDrag}
          createContext={createContext}
        />
      ))}
    </div>
  );
}

export function MicroflowNodePanelEmpty({ labels, onClear }: { labels: MicroflowNodePanelLabels; onClear: () => void }) {
  return (
    <Empty
      title={labels.emptyTitle}
      description={labels.emptyDescription}
      image={<IconSearch />}
      style={{ padding: "32px 8px" }}
    >
      <Button onClick={onClear}>{labels.clearSearch}</Button>
    </Empty>
  );
}

export function MicroflowNodePanelFooter({ labels }: { labels: MicroflowNodePanelLabels }) {
  return (
    <div
      style={{
        display: "flex",
        gap: 6,
        alignItems: "center",
        padding: "8px 10px",
        borderTop: "1px solid rgba(78, 89, 105, 0.12)",
        color: "var(--semi-color-text-2, #86909c)",
        background: "rgba(255, 255, 255, 0.88)"
      }}
    >
      <IconInfoCircle />
      <Text type="tertiary" size="small">{labels.footerHint}</Text>
    </div>
  );
}

export function MicroflowNodeFilterPopover({ children }: { children: ReactNode }) {
  return <>{children}</>;
}

export function MicroflowNodeContextMenu({
  state,
  labels,
  favorite,
  createContext,
  onAdd,
  onFavoriteToggle,
  onShowDocumentation,
  onClose
}: {
  state: ContextMenuState | undefined;
  labels: MicroflowNodePanelLabels;
  favorite: boolean;
  createContext?: MicroflowNodeCreateContext;
  onAdd: (item: MicroflowNodeRegistryItem) => void;
  onFavoriteToggle: (item: MicroflowNodeRegistryItem) => void;
  onShowDocumentation: (item: MicroflowNodeRegistryItem) => void;
  onClose: () => void;
}) {
  if (!state) {
    return null;
  }
  const disabled = Boolean(getMicroflowNodeDisabledReason(state.item, createContext));
  const menuItems: Array<{ key: string; label: string; icon: ReactNode; disabled?: boolean; onClick: () => void }> = [
    { key: "add", label: labels.addToCanvas, icon: <IconPlus />, disabled, onClick: () => onAdd(state.item) },
    { key: "favorite", label: favorite ? labels.unfavorite : labels.favorite, icon: favorite ? <IconStar /> : <IconStarStroked />, disabled, onClick: () => onFavoriteToggle(state.item) },
    { key: "docs", label: labels.viewDocumentation, icon: <IconInfoCircle />, onClick: () => onShowDocumentation(state.item) },
    {
      key: "copy",
      label: labels.copyNodeType,
      icon: <IconCopy />,
      disabled,
      onClick: () => {
        const text = getMicroflowNodeRegistryKey(state.item);
        if (typeof navigator !== "undefined" && navigator.clipboard) {
          void navigator.clipboard.writeText(text);
        }
        Toast.success(labels.copied);
      }
    }
  ];

  return (
    <div
      style={{
        position: "fixed",
        left: state.x,
        top: state.y,
        zIndex: 1000,
        minWidth: 168,
        padding: 6,
        borderRadius: 10,
        border: "1px solid var(--semi-color-border, #e5e6eb)",
        background: "var(--semi-color-bg-2, #fff)",
        boxShadow: "0 10px 28px rgba(31, 35, 41, 0.16)"
      }}
      onClick={event => event.stopPropagation()}
    >
      {menuItems.map(item => (
        <Button
          key={item.key}
          block
          size="small"
          theme="borderless"
          type="tertiary"
          disabled={item.disabled}
          icon={item.icon}
          style={{ justifyContent: "flex-start", marginBottom: 2 }}
          onClick={() => {
            item.onClick();
            onClose();
          }}
        >
          {item.label}
        </Button>
      ))}
    </div>
  );
}

export function MicroflowNodePanel({
  registry = defaultMicroflowNodePanelRegistry,
  favoriteNodeKeys,
  onFavoriteChange,
  onAddNode,
  onInsertTemplate,
  onStartDrag,
  onShowDocumentation,
  labels: labelOverrides,
  createContext
}: MicroflowNodePanelProps) {
  const labels = { ...defaultMicroflowNodePanelLabels, ...labelOverrides };
  const [activeTab, setActiveTab] = useState<"nodes" | "components" | "templates">("nodes");
  const [keyword, setKeyword] = useState("");
  const [debouncedKeyword, setDebouncedKeyword] = useState("");
  const [filterKey, setFilterKey] = useState<MicroflowNodeFilterKey>("all");
  const [expandedCategories, setExpandedCategories] = useState<string[]>(() => readStoredStringList(nodePanelCategoryStorageKey, ["events", "inputs", "flowControl", "loops", "objects", "integration"]));
  const [expandedGroups, setExpandedGroups] = useState<string[]>(() => readStoredStringList(nodePanelGroupStorageKey, []));
  const [contextMenu, setContextMenu] = useState<ContextMenuState>();

  const grouped = useMemo(() => MicroflowNodePanelRegistryAdapter({
    registry,
    keyword: debouncedKeyword,
    filterKey,
    favoriteNodeKeys
  }), [debouncedKeyword, favoriteNodeKeys, filterKey, registry]);

  const favoriteSet = useMemo(() => new Set(favoriteNodeKeys), [favoriteNodeKeys]);

  useEffect(() => {
    const timer = window.setTimeout(() => setDebouncedKeyword(keyword), 200);
    return () => window.clearTimeout(timer);
  }, [keyword]);

  useEffect(() => {
    if (!debouncedKeyword.trim()) {
      return;
    }
    setExpandedCategories(grouped.map(category => category.category.key));
    setExpandedGroups(grouped.flatMap(category => category.groups.map(group => `${category.category.key}:${group.key}`)));
  }, [debouncedKeyword, grouped]);

  useEffect(() => {
    if (debouncedKeyword.trim()) {
      return;
    }
    writeStoredStringList(nodePanelCategoryStorageKey, expandedCategories);
    writeStoredStringList(nodePanelGroupStorageKey, expandedGroups);
  }, [debouncedKeyword, expandedCategories, expandedGroups]);

  useEffect(() => {
    if (!contextMenu) {
      return undefined;
    }
    const close = () => setContextMenu(undefined);
    document.addEventListener("click", close);
    return () => document.removeEventListener("click", close);
  }, [contextMenu]);

  function toggleFavorite(item: MicroflowNodeRegistryItem) {
    const key = getMicroflowNodeRegistryKey(item);
    onFavoriteChange(favoriteSet.has(key) ? favoriteNodeKeys.filter(value => value !== key) : [...favoriteNodeKeys, key]);
  }

  function handleAdd(item: MicroflowNodeRegistryItem, source: "doubleClick" | "contextMenu" = "doubleClick") {
    if (!canCreateRegistryItem(item, createContext)) {
      Toast.warning(getMicroflowNodeDisabledReason(item, createContext) ?? labels.disabled);
      return;
    }
    onAddNode(item, { source });
  }

  return (
    <div data-testid="microflow-node-panel" style={{ height: "100%", display: "grid", gridTemplateRows: "auto auto minmax(0, 1fr) auto", gap: 10 }}>
      <MicroflowNodePanelTabs activeKey={activeTab} onChange={setActiveTab} labels={labels} />
      {activeTab === "nodes" ? (
        <MicroflowNodeSearch
          value={keyword}
          onChange={setKeyword}
          filterKey={filterKey}
          onFilterChange={setFilterKey}
          labels={labels}
        />
      ) : <div />}
      <div style={{ minHeight: 0, overflow: "auto", paddingRight: 2 }}>
        {activeTab === "nodes" ? (
          <Space vertical align="start" spacing={10} style={{ width: "100%" }}>
            <MicroflowNodeFavorites
              items={registry}
              favoriteNodeKeys={favoriteNodeKeys}
              labels={labels}
              createContext={createContext}
              onAdd={item => handleAdd(item, "doubleClick")}
              onFavoriteToggle={toggleFavorite}
              onContextMenu={(item, point) => setContextMenu({ item, ...point })}
              onStartDrag={onStartDrag}
            />
            {grouped.length === 0 ? (
              <MicroflowNodePanelEmpty
                labels={labels}
                onClear={() => {
                  setKeyword("");
                  setFilterKey("all");
                }}
              />
            ) : (
              <MicroflowNodeCategoryList
                grouped={grouped}
                expandedCategories={expandedCategories}
                expandedGroups={expandedGroups}
                favoriteNodeKeys={favoriteNodeKeys}
                labels={labels}
                createContext={createContext}
                onToggleCategory={key => setExpandedCategories(current => current.includes(key) ? current.filter(item => item !== key) : [...current, key])}
                onToggleGroup={key => setExpandedGroups(current => current.includes(key) ? current.filter(item => item !== key) : [...current, key])}
                onAdd={item => handleAdd(item, "doubleClick")}
                onFavoriteToggle={toggleFavorite}
                onContextMenu={(item, point) => setContextMenu({ item, ...point })}
                onStartDrag={onStartDrag}
              />
            )}
          </Space>
        ) : defaultMicroflowTemplates.filter(template => template.category === (activeTab === "components" ? "component" : "template")).length > 0 ? (
          <Space vertical align="start" spacing={8} style={{ width: "100%" }}>
            {defaultMicroflowTemplates
              .filter(template => template.category === (activeTab === "components" ? "component" : "template"))
              .map(template => (
                <Card key={template.id} shadows="hover" bodyStyle={{ padding: 10 }} style={{ width: "100%" }}>
                  <Space vertical align="start" spacing={6} style={{ width: "100%" }}>
                    <Space style={{ width: "100%", justifyContent: "space-between" }}>
                      <Text strong>{template.name}</Text>
                      <Tag>{template.nodeKeys.length}</Tag>
                    </Space>
                    <Text size="small" type="tertiary">{template.description}</Text>
                    <Tooltip content={onInsertTemplate ? labels.insertTemplate : "Current editor cannot insert templates in this context."}>
                      <span style={{ display: "inline-flex" }}>
                        <Button size="small" icon={<IconPlus />} disabled={!onInsertTemplate} onClick={() => onInsertTemplate?.(template)}>
                          {labels.insertTemplate}
                        </Button>
                      </span>
                    </Tooltip>
                  </Space>
                </Card>
              ))}
          </Space>
        ) : (
          <Empty
            image={<IconInfoCircle />}
            title={activeTab === "components" ? labels.componentsTab : labels.templatesTab}
            description={activeTab === "components" ? labels.componentsPlaceholder : labels.templatesPlaceholder}
            style={{ paddingTop: 42 }}
          />
        )}
      </div>
      <MicroflowNodePanelFooter labels={labels} />
      <MicroflowNodeContextMenu
        state={contextMenu}
        labels={labels}
        favorite={contextMenu ? favoriteSet.has(getMicroflowNodeRegistryKey(contextMenu.item)) : false}
        createContext={createContext}
        onAdd={item => handleAdd(item, "contextMenu")}
        onFavoriteToggle={toggleFavorite}
        onShowDocumentation={item => onShowDocumentation?.(item)}
        onClose={() => setContextMenu(undefined)}
      />
    </div>
  );
}

export type MicroflowNodePanelRegistryItem = MicroflowNodeRegistryItem;
export type MicroflowNodePanelNodeKind = MicroflowRegistryNodeType;
export type MicroflowNodePanelActivityType = MicroflowActivityType;
