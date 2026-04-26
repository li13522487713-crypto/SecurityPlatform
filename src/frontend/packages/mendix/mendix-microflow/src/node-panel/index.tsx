import { useEffect, useMemo, useState, type CSSProperties, type ReactNode } from "react";
import { Button, Empty, Input, Popover, Space, Tabs, Tag, Toast, Tooltip, Typography } from "@douyinfe/semi-ui";
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
import type { LegacyMicroflowNodeType, MicroflowActivityType } from "../schema";
import {
  defaultMicroflowNodePanelRegistry,
  canDragRegistryItem,
  createDragPayloadFromRegistryItem,
  getDisabledDragReason,
  getMicroflowNodeRegistryKey,
  groupMicroflowNodesByCategory,
  searchMicroflowNodes,
  type MicroflowNodeDragPayload,
  type MicroflowNodeFilterKey,
  type MicroflowNodePanelCategoryKey,
  type MicroflowNodeRegistryEntry,
  type MicroflowNodeRegistryItem
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
  inputs: "Inputs",
  outputs: "Outputs",
  useCases: "Use cases"
};

export interface MicroflowNodePanelProps {
  registry?: MicroflowNodeRegistryItem[];
  favoriteNodeKeys: string[];
  onFavoriteChange: (keys: string[]) => void;
  onAddNode: (item: MicroflowNodeRegistryItem, options?: { source: "doubleClick" | "contextMenu" | "drop"; position?: { x: number; y: number } }) => void;
  onStartDrag?: (payload: MicroflowNodeDragPayload) => void;
  onShowDocumentation?: (item: MicroflowNodeRegistryItem) => void;
  labels?: Partial<MicroflowNodePanelLabels>;
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
  decisions: "Decisions",
  activities: "Activities",
  loop: "Loop",
  parameters: "Parameters",
  annotations: "Annotations"
};

const categoryStyle: CSSProperties = {
  border: "1px solid rgba(78, 89, 105, 0.12)",
  borderRadius: 12,
  background: "rgba(255, 255, 255, 0.9)",
  overflow: "hidden"
};

const cardBaseStyle: CSSProperties = {
  position: "relative",
  display: "grid",
  gridTemplateColumns: "26px minmax(0, 1fr) 24px",
  gap: 8,
  alignItems: "center",
  width: "100%",
  minHeight: 48,
  padding: "8px 8px",
  border: "1px solid rgba(78, 89, 105, 0.14)",
  borderRadius: 10,
  background: "var(--semi-color-bg-2, #fff)",
  boxSizing: "border-box",
  textAlign: "left",
  transition: "background 120ms ease, border-color 120ms ease, box-shadow 120ms ease",
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
  return ["events", "decisions", "activities", "loop", "parameters", "annotations"].includes(filterKey);
}

function categoryKeyFromEntry(entry: MicroflowNodeRegistryEntry): MicroflowNodePanelCategoryKey {
  if (entry.group === "Events") {
    return "events";
  }
  if (entry.group === "Decisions") {
    return "decisions";
  }
  if (entry.group === "Activities") {
    return "activities";
  }
  if (entry.group === "Loop") {
    return "loop";
  }
  if (entry.group === "Parameters") {
    return "parameters";
  }
  return "annotations";
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
        width: 24,
        height: 24,
        borderRadius: item.render.shape === "diamond" ? 7 : item.render.shape === "event" ? 999 : 8,
        display: "inline-flex",
        alignItems: "center",
        justifyContent: "center",
        background: tone.background,
        color: tone.color,
        fontSize: 12,
        fontWeight: 700,
        border: `1px solid ${tone.color}22`
      }}
    >
      {label.toUpperCase()}
    </span>
  );
}

function TooltipContent({ item, labels }: { item: MicroflowNodeRegistryItem; labels: MicroflowNodePanelLabels }) {
  return (
    <Space vertical align="start" spacing={6} style={{ maxWidth: 280 }}>
      <Text strong>{item.title}</Text>
      <Text type="tertiary">{item.titleZh}</Text>
      <Text>{item.description}</Text>
      <Text type="tertiary">{labels.inputs}: {(item.inputs ?? []).map(input => input.title).join(", ") || "-"}</Text>
      <Text type="tertiary">{labels.outputs}: {(item.outputs ?? []).map(output => output.title).join(", ") || "-"}</Text>
      {item.useCases?.length ? <Text type="tertiary">{labels.useCases}: {item.useCases.join(" ")}</Text> : null}
      {item.availability !== "supported" ? <Tag color={item.availability === "deprecated" ? "orange" : item.availability === "beta" ? "blue" : "grey"}>{item.availabilityReason ?? item.availability}</Tag> : null}
      {!item.enabled && item.disabledReason ? <Tag color="grey">{item.disabledReason}</Tag> : null}
    </Space>
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
      return item.enabled;
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
    ...Object.entries(categoryFilterLabels).map(([key, label]) => ({ key: key as MicroflowNodePanelCategoryKey, label }))
  ];

  return (
    <div style={{ display: "grid", gridTemplateColumns: "minmax(0, 1fr) 36px", gap: 8 }}>
      <Input className="microflow-node-search-input" prefix={<IconSearch />} value={value} onChange={onChange} placeholder={labels.searchPlaceholder} showClear />
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
  labels,
  compact,
  onAdd,
  onFavoriteToggle,
  onContextMenu,
  onStartDrag
}: {
  item: MicroflowNodeRegistryItem;
  favorite: boolean;
  labels: MicroflowNodePanelLabels;
  compact?: boolean;
  onAdd: (item: MicroflowNodeRegistryItem) => void;
  onFavoriteToggle: (item: MicroflowNodeRegistryItem) => void;
  onContextMenu: (item: MicroflowNodeRegistryItem, point: { x: number; y: number }) => void;
  onStartDrag?: (payload: MicroflowNodeDragPayload) => void;
}) {
  const disabledReason = getDisabledDragReason(item);
  const disabled = Boolean(disabledReason);
  const key = getMicroflowNodeRegistryKey(item);
  const cardStyle: CSSProperties = {
    ...cardBaseStyle,
    minHeight: compact ? 42 : 52,
    opacity: disabled ? 0.58 : 1,
    cursor: disabled ? "not-allowed" : "grab",
    background: favorite ? "rgba(255, 250, 232, 0.95)" : cardBaseStyle.background,
    borderColor: favorite ? "rgba(255, 177, 0, 0.48)" : cardBaseStyle.border as string
  };

  return (
    <Tooltip content={<TooltipContent item={item} labels={labels} />} position="right">
      <div
        role="button"
        tabIndex={disabled ? -1 : 0}
        draggable={!disabled}
        data-registry-key={key}
        style={cardStyle}
        onDoubleClick={() => {
          if (!disabled) {
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
          const payload = createDragPayloadFromRegistryItem(item);
          event.dataTransfer.effectAllowed = "copy";
          event.dataTransfer.setData("application/x-atlas-microflow-node", JSON.stringify(payload));
          event.dataTransfer.setData("application/json", JSON.stringify(payload));
          event.dataTransfer.setData("text/plain", payload.registryKey);
          onStartDrag?.(payload);
        }}
      >
        <MicroflowNodeIcon item={item} />
        <div style={{ minWidth: 0 }}>
          <Space spacing={4} style={{ maxWidth: "100%" }}>
            <Text strong ellipsis={{ showTooltip: true }} style={{ maxWidth: 128 }}>
              {item.titleZh}
            </Text>
            {item.availability === "beta" ? <Tag size="small" color="blue">Beta</Tag> : null}
            {item.availability === "deprecated" ? <Tag size="small" color="orange">Deprecated</Tag> : null}
            {item.availability === "requiresConnector" ? <Tag size="small" color="grey">Connector Required</Tag> : null}
            {item.availability === "nanoflowOnlyDisabled" ? <Tag size="small" color="grey">Nanoflow Only</Tag> : null}
          </Space>
          <Text type="tertiary" size="small" ellipsis={{ showTooltip: true }} style={{ display: "block", maxWidth: "100%" }}>
            {item.title}
          </Text>
          {!compact ? (
            <Text type="tertiary" size="small" ellipsis={{ showTooltip: true }} style={{ display: "block", maxWidth: "100%" }}>
              {disabled && item.disabledReason ? item.disabledReason : item.description}
            </Text>
          ) : null}
        </div>
        <Button
          size="small"
          type={favorite ? "warning" : "tertiary"}
          theme="borderless"
          disabled={disabled && !favorite}
          icon={favorite ? <IconStar /> : disabled ? <IconStop /> : <IconStarStroked />}
          onClick={event => {
            event.stopPropagation();
            onFavoriteToggle(item);
          }}
        />
      </div>
    </Tooltip>
  );
}

export function MicroflowNodeFavorites({
  items,
  favoriteNodeKeys,
  labels,
  onAdd,
  onFavoriteToggle,
  onContextMenu,
  onStartDrag
}: {
  items: MicroflowNodeRegistryItem[];
  favoriteNodeKeys: string[];
  labels: MicroflowNodePanelLabels;
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
        <div style={{ display: "grid", gridTemplateColumns: "1fr", gap: 6 }}>
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
  onToggle: (key: string) => void;
  onToggleGroup: (key: string) => void;
  onAdd: (item: MicroflowNodeRegistryItem) => void;
  onFavoriteToggle: (item: MicroflowNodeRegistryItem) => void;
  onContextMenu: (item: MicroflowNodeRegistryItem, point: { x: number; y: number }) => void;
  onStartDrag?: (payload: MicroflowNodeDragPayload) => void;
}) {
  const favoriteSet = new Set(favoriteNodeKeys);

  return (
    <section style={categoryStyle}>
      <button
        type="button"
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
        <div style={{ padding: "8px 8px 10px", display: "grid", gap: 8 }}>
          {category.groups.length > 0 ? category.groups.map(group => {
            const groupPanelKey = `${category.category.key}:${group.key}`;
            const groupOpen = expandedGroups.includes(groupPanelKey);
            return (
              <div key={group.key} style={{ display: "grid", gap: 6 }}>
                <button
                  type="button"
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
  onToggleCategory: (key: string) => void;
  onToggleGroup: (key: string) => void;
  onAdd: (item: MicroflowNodeRegistryItem) => void;
  onFavoriteToggle: (item: MicroflowNodeRegistryItem) => void;
  onContextMenu: (item: MicroflowNodeRegistryItem, point: { x: number; y: number }) => void;
  onStartDrag?: (payload: MicroflowNodeDragPayload) => void;
}) {
  return (
    <div style={{ display: "grid", gap: 8 }}>
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
  onAdd,
  onFavoriteToggle,
  onShowDocumentation,
  onClose
}: {
  state: ContextMenuState | undefined;
  labels: MicroflowNodePanelLabels;
  favorite: boolean;
  onAdd: (item: MicroflowNodeRegistryItem) => void;
  onFavoriteToggle: (item: MicroflowNodeRegistryItem) => void;
  onShowDocumentation: (item: MicroflowNodeRegistryItem) => void;
  onClose: () => void;
}) {
  if (!state) {
    return null;
  }
  const disabled = Boolean(getDisabledDragReason(state.item));
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
  onStartDrag,
  onShowDocumentation,
  labels: labelOverrides
}: MicroflowNodePanelProps) {
  const labels = { ...defaultMicroflowNodePanelLabels, ...labelOverrides };
  const [activeTab, setActiveTab] = useState<"nodes" | "components" | "templates">("nodes");
  const [keyword, setKeyword] = useState("");
  const [filterKey, setFilterKey] = useState<MicroflowNodeFilterKey>("all");
  const [expandedCategories, setExpandedCategories] = useState<string[]>(() => readStoredStringList(nodePanelCategoryStorageKey, ["events", "decisions", "activities"]));
  const [expandedGroups, setExpandedGroups] = useState<string[]>(() => readStoredStringList(nodePanelGroupStorageKey, ["activities:object"]));
  const [contextMenu, setContextMenu] = useState<ContextMenuState>();

  const grouped = useMemo(() => MicroflowNodePanelRegistryAdapter({
    registry,
    keyword,
    filterKey,
    favoriteNodeKeys
  }), [favoriteNodeKeys, filterKey, keyword, registry]);

  const favoriteSet = useMemo(() => new Set(favoriteNodeKeys), [favoriteNodeKeys]);

  useEffect(() => {
    if (!keyword.trim()) {
      return;
    }
    setExpandedCategories(grouped.map(category => category.category.key));
    setExpandedGroups(grouped.flatMap(category => category.groups.map(group => `${category.category.key}:${group.key}`)));
  }, [grouped, keyword]);

  useEffect(() => {
    if (keyword.trim()) {
      return;
    }
    writeStoredStringList(nodePanelCategoryStorageKey, expandedCategories);
    writeStoredStringList(nodePanelGroupStorageKey, expandedGroups);
  }, [expandedCategories, expandedGroups, keyword]);

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
    if (!canDragRegistryItem(item)) {
      Toast.warning(getDisabledDragReason(item) ?? labels.disabled);
      return;
    }
    onAddNode(item, { source });
  }

  return (
    <div style={{ height: "100%", display: "grid", gridTemplateRows: "auto auto minmax(0, 1fr) auto", gap: 10 }}>
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
                onToggleCategory={key => setExpandedCategories(current => current.includes(key) ? current.filter(item => item !== key) : [...current, key])}
                onToggleGroup={key => setExpandedGroups(current => current.includes(key) ? current.filter(item => item !== key) : [...current, key])}
                onAdd={item => handleAdd(item, "doubleClick")}
                onFavoriteToggle={toggleFavorite}
                onContextMenu={(item, point) => setContextMenu({ item, ...point })}
                onStartDrag={onStartDrag}
              />
            )}
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
        onAdd={item => handleAdd(item, "contextMenu")}
        onFavoriteToggle={toggleFavorite}
        onShowDocumentation={item => onShowDocumentation?.(item)}
        onClose={() => setContextMenu(undefined)}
      />
    </div>
  );
}

export type MicroflowNodePanelRegistryItem = MicroflowNodeRegistryItem;
export type MicroflowNodePanelNodeKind = LegacyMicroflowNodeType;
export type MicroflowNodePanelActivityType = MicroflowActivityType;
