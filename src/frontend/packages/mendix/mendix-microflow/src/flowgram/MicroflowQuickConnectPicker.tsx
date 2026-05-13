import { useMemo, useState } from "react";
import { Input, Popover, Space, Tooltip, Typography } from "@douyinfe/semi-ui";
import { IconSearch } from "@douyinfe/semi-icons";
import {
  defaultMicroflowNodePanelRegistry,
  canCreateRegistryItem,
  getMicroflowNodeRegistryKey,
  type MicroflowNodeRegistryItem,
} from "../node-registry";
import { NodeIcon } from "./NodeIcon";

const { Text } = Typography;

/** Node types that are most commonly used as the next step after any node. */
const QUICK_CONNECT_ORDER = [
  "activity:objectCreate",
  "activity:objectChange",
  "activity:objectRetrieve",
  "activity:callMicroflow",
  "activity:callRest",
  "activity:variableCreate",
  "activity:variableChange",
  "activity:logMessage",
  "decision",
  "loop",
  "endEvent",
];

function iconColorForItem(item: MicroflowNodeRegistryItem): { bg: string; color: string } {
  if (item.group === "Events") return { bg: "#0d3a20", color: "#4ade80" };
  if (item.group === "Decisions") return { bg: "#3a2800", color: "#f59e0b" };
  if (item.subgroup === "object") return { bg: "#1e3a70", color: "#93c5fd" };
  if (item.subgroup === "list") return { bg: "#0d3824", color: "#6ee7b7" };
  if (item.subgroup === "call") return { bg: "#321e5a", color: "#c4b5fd" };
  if (item.subgroup === "variable") return { bg: "#3a2800", color: "#fcd34d" };
  return { bg: "#1e2640", color: "#94a3b8" };
}

interface MicroflowQuickConnectPickerProps {
  visible: boolean;
  onVisibleChange: (v: boolean) => void;
  onPick: (item: MicroflowNodeRegistryItem) => void;
  children: React.ReactElement;
}

export function MicroflowQuickConnectPicker({
  visible,
  onVisibleChange,
  onPick,
  children,
}: MicroflowQuickConnectPickerProps) {
  const [search, setSearch] = useState("");
  const allItems = useMemo(
    () => defaultMicroflowNodePanelRegistry.filter(item => canCreateRegistryItem(item)),
    [],
  );
  const sorted = useMemo(() => {
    const orderMap = new Map(QUICK_CONNECT_ORDER.map((k, i) => [k, i]));
    const searchLower = search.toLowerCase();
    return [...allItems]
      .filter(item =>
        !searchLower
        || item.title.toLowerCase().includes(searchLower)
        || (item.titleZh ?? "").toLowerCase().includes(searchLower),
      )
      .sort((a, b) => {
        const ka = getMicroflowNodeRegistryKey(a);
        const kb = getMicroflowNodeRegistryKey(b);
        const ia = orderMap.get(ka) ?? 999;
        const ib = orderMap.get(kb) ?? 999;
        return ia - ib;
      })
      .slice(0, 16);
  }, [allItems, search]);

  const content = (
    <div style={{ width: 240, padding: 8 }}>
      <div style={{ marginBottom: 6 }}>
        <Input
          prefix={<IconSearch />}
          size="small"
          placeholder="搜索节点…"
          value={search}
          onChange={setSearch}
          autoFocus
        />
      </div>
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 4 }}>
        {sorted.map(item => {
          const key = getMicroflowNodeRegistryKey(item);
          const colors = iconColorForItem(item);
          const iconKind = item.activityType ?? item.type;
          return (
            <Tooltip key={key} content={item.description ?? item.title} position="right">
              <button
                type="button"
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: 6,
                  padding: "4px 6px",
                  border: "1px solid transparent",
                  borderRadius: 6,
                  background: "transparent",
                  cursor: "pointer",
                  textAlign: "left",
                  minWidth: 0,
                }}
                onMouseEnter={e => { (e.currentTarget as HTMLElement).style.background = "rgba(255,255,255,0.06)"; }}
                onMouseLeave={e => { (e.currentTarget as HTMLElement).style.background = "transparent"; }}
                onClick={() => {
                  onPick(item);
                  onVisibleChange(false);
                  setSearch("");
                }}
              >
                <span style={{
                  width: 22,
                  height: 22,
                  borderRadius: 5,
                  display: "inline-flex",
                  alignItems: "center",
                  justifyContent: "center",
                  background: colors.bg,
                  color: colors.color,
                  border: `1px solid ${colors.color}33`,
                  flexShrink: 0,
                }}>
                  <NodeIcon kind={iconKind} size={13} />
                </span>
                <Space vertical spacing={0} style={{ minWidth: 0 }}>
                  <Text size="small" strong style={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                    {item.titleZh ?? item.title}
                  </Text>
                </Space>
              </button>
            </Tooltip>
          );
        })}
        {sorted.length === 0 ? (
          <Text type="tertiary" size="small" style={{ gridColumn: "1 / -1", padding: "4px 6px" }}>
            未找到匹配节点
          </Text>
        ) : null}
      </div>
    </div>
  );

  return (
    <Popover
      visible={visible}
      onVisibleChange={onVisibleChange}
      trigger="custom"
      content={content}
      position="bottom"
      showArrow={false}
    >
      {children}
    </Popover>
  );
}
