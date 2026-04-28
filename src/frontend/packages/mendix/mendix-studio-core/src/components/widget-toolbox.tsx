import { useState } from "react";
import { Input, Toast } from "@douyinfe/semi-ui";
import {
  IconSearch,
  IconGridSquare,
  IconGridView1,
  IconList,
  IconImage,
  IconBranch,
  IconEdit,
  IconTextStroked,
  IconTick,
  IconChevronDown,
  IconPlus,
  IconLink
} from "@douyinfe/semi-icons";
import { useMendixStudioStore } from "../store";
import type { WidgetType } from "@atlas/mendix-schema";

interface ToolboxItem {
  type: WidgetType | string;
  label: string;
  icon: React.ReactNode;
}

interface ToolboxGroup {
  title: string;
  items: ToolboxItem[];
}

const TOOLBOX_GROUPS: ToolboxGroup[] = [
  {
    title: "容器",
    items: [
      { type: "container", label: "Container", icon: <IconGridSquare style={{ fontSize: 13, color: "#6b7280" }} /> },
      { type: "dataView", label: "Data View", icon: <span style={{ fontSize: 10, fontWeight: 700, color: "#0958d9" }}>DV</span> },
      { type: "layoutGrid", label: "Layout Grid", icon: <IconGridView1 style={{ fontSize: 13, color: "#6b7280" }} /> },
      { type: "tabContainer", label: "Tab Container", icon: <span style={{ fontSize: 10, fontWeight: 700, color: "#6b7280" }}>T</span> }
    ]
  },
  {
    title: "数据展示",
    items: [
      { type: "dataGrid", label: "Data Grid", icon: <IconGridView1 style={{ fontSize: 13, color: "#6b7280" }} /> },
      { type: "listView", label: "List View", icon: <IconList style={{ fontSize: 13, color: "#6b7280" }} /> },
      { type: "gallery", label: "Gallery", icon: <IconImage style={{ fontSize: 13, color: "#6b7280" }} /> },
      { type: "treeView", label: "Tree View", icon: <IconBranch style={{ fontSize: 13, color: "#6b7280" }} /> }
    ]
  },
  {
    title: "输入组件",
    items: [
      { type: "textBox", label: "Text Box", icon: <IconEdit style={{ fontSize: 13, color: "#6b7280" }} /> },
      { type: "textArea", label: "Text Area", icon: <IconTextStroked style={{ fontSize: 13, color: "#6b7280" }} /> },
      { type: "numberInput", label: "Number Input", icon: <span style={{ fontSize: 10, fontWeight: 700, color: "#6b7280" }}>123</span> },
      { type: "dropDown", label: "Drop-down", icon: <IconChevronDown style={{ fontSize: 13, color: "#6b7280" }} /> },
      { type: "referenceSelector", label: "Reference Selector", icon: <IconLink style={{ fontSize: 13, color: "#6b7280" }} /> },
      { type: "checkBox", label: "Check Box", icon: <IconTick style={{ fontSize: 13, color: "#6b7280" }} /> }
    ]
  },
  {
    title: "动作",
    items: [
      { type: "button", label: "Button", icon: <span style={{ fontSize: 10, fontWeight: 700, color: "#1677ff" }}>Btn</span> },
      { type: "linkButton", label: "Link Button", icon: <IconLink style={{ fontSize: 13, color: "#6b7280" }} /> }
    ]
  }
];

export function WidgetToolbox() {
  const [searchText, setSearchText] = useState("");
  const setAppSchema = useMendixStudioStore(state => state.setAppSchema);
  const appSchema = useMendixStudioStore(state => state.appSchema);

  const handleAddWidget = (item: ToolboxItem) => {
    if (!["container", "dataView", "textBox", "textArea", "numberInput", "dropDown", "button", "dataGrid", "listView", "label"].includes(item.type)) {
      Toast.info({ content: `${item.label} 已标记添加（MVP 阶段仅支持核心组件）`, duration: 2 });
      return;
    }
    const next = JSON.parse(JSON.stringify(appSchema)) as typeof appSchema;
    const page = next.modules[0]?.pages[0];
    if (!page) return;

    const root = page.rootWidget;
    if (!root.children) root.children = [];

    const widgetType = item.type as WidgetType;
    root.children.push(
      widgetType === "dataView"
        ? {
            widgetId: `widget_${Date.now()}`,
            widgetType: "dataView",
            props: { caption: item.label },
            dataSource: { sourceType: "entity", entityRef: { kind: "entity", id: "ent_purchase_request" } },
            children: []
          }
        : widgetType === "button"
          ? {
              widgetId: `widget_${Date.now()}`,
              widgetType: "button",
              props: { caption: "按钮", buttonType: "default" }
            }
          : {
              widgetId: `widget_${Date.now()}`,
              widgetType: widgetType,
              props: { caption: item.label },
              children: widgetType === "container" ? [] : undefined
            }
    );

    setAppSchema(next);
    Toast.success({ content: `已添加 ${item.label}`, duration: 1 });
  };

  const allItems = TOOLBOX_GROUPS.flatMap(g => g.items);
  const filtered = searchText
    ? allItems.filter(i => i.label.toLowerCase().includes(searchText.toLowerCase()))
    : null;

  return (
    <div className="studio-toolbox">
      <div className="studio-toolbox__header">工具箱</div>

      <div className="studio-toolbox__search">
        <Input
          prefix={<IconSearch style={{ fontSize: 12, color: "#9ca3af" }} />}
          placeholder="搜索组件"
          value={searchText}
          onChange={v => setSearchText(v)}
          style={{ height: 26, fontSize: 12 }}
        />
      </div>

      <div className="studio-toolbox__body">
        {filtered ? (
          <div>
            {filtered.map(item => (
              <ToolboxItemRow key={item.type} item={item} onAdd={handleAddWidget} />
            ))}
          </div>
        ) : (
          TOOLBOX_GROUPS.map(group => (
            <div key={group.title}>
              <div className="studio-toolbox__group-title">{group.title}</div>
              {group.items.map(item => (
                <ToolboxItemRow key={item.type} item={item} onAdd={handleAddWidget} />
              ))}
            </div>
          ))
        )}

        <div className="studio-toolbox__more">
          <div
            style={{
              display: "flex",
              alignItems: "center",
              gap: 4,
              fontSize: 12,
              color: "#9ca3af",
              cursor: "pointer",
              padding: "4px 0"
            }}
            onClick={() => Toast.info({ content: "更多组件 (扩展中)", duration: 2 })}
          >
            <IconPlus style={{ fontSize: 12 }} />
            <span>更多组件 ▾</span>
          </div>
        </div>
      </div>
    </div>
  );
}

function ToolboxItemRow({ item, onAdd }: { item: ToolboxItem; onAdd: (item: ToolboxItem) => void }) {
  return (
    <div
      className="studio-toolbox__item"
      onClick={() => onAdd(item)}
      title={`点击添加 ${item.label}`}
    >
      <div className="studio-toolbox__item-icon">{item.icon}</div>
      <span style={{ fontSize: 12, color: "var(--studio-text-primary)" }}>{item.label}</span>
    </div>
  );
}
