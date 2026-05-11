import { useMemo, useState } from "react";
import { Button, Card, Checkbox, Input, List, Space, Tag, Typography } from "@douyinfe/semi-ui";

const { Text } = Typography;

export type DebugBreakpointScope = "node" | "flow" | "expression" | "error" | "gatewayBranch";

export interface DebugBreakpointPanelItem {
  id: string;
  targetId: string;
  scope: DebugBreakpointScope;
  stale?: boolean;
  condition?: string;
  hitTarget?: number;
  logpoint?: boolean;
  enabled?: boolean;
  kind?: "basic" | "conditional";
}

export interface DebugBreakpointsPanelProps {
  title: string;
  breakpoints: DebugBreakpointPanelItem[];
  staleBreakpointLabel: string;
  logpointLabel: string;
  readonly?: boolean;
  onToggleEnabled?: (id: string, enabled: boolean) => void;
  onDelete?: (id: string) => void;
  onChangeCondition?: (id: string, condition: string) => void;
}

export function DebugBreakpointsPanel({
  title,
  breakpoints,
  staleBreakpointLabel,
  logpointLabel,
  readonly = false,
  onToggleEnabled,
  onDelete,
  onChangeCondition,
}: DebugBreakpointsPanelProps) {
  const initialDrafts = useMemo(
    () => Object.fromEntries(breakpoints.map(item => [item.id, item.condition ?? ""])),
    [breakpoints],
  );
  const [conditionDraftById, setConditionDraftById] = useState<Record<string, string>>(initialDrafts);

  const readDraft = (id: string, fallback: string): string => conditionDraftById[id] ?? fallback;
  const writeDraft = (id: string, value: string) => {
    setConditionDraftById(current => ({ ...current, [id]: value }));
  };
  const commitCondition = (item: DebugBreakpointPanelItem) => {
    const draft = readDraft(item.id, item.condition ?? "").trim();
    if (draft === (item.condition ?? "").trim()) {
      return;
    }
    onChangeCondition?.(item.id, draft);
  };

  return (
    <Card title={title}>
      <List
        dataSource={breakpoints}
        renderItem={item => (
          <List.Item style={{ paddingInline: 0 }}>
            <Space vertical align="start" style={{ width: "100%" }}>
              <Space wrap align="center" style={{ width: "100%", justifyContent: "space-between" }}>
                <Space wrap align="center">
                  <Checkbox
                    checked={item.enabled !== false}
                    disabled={readonly}
                    onChange={event => onToggleEnabled?.(item.id, Boolean(event.target.checked))}
                    data-testid={`microflow-breakpoint-enabled-${item.id}`}
                  />
                  <Text type={item.enabled === false ? "tertiary" : "primary"}>
                    {item.scope}: {item.targetId}
                  </Text>
                  {item.hitTarget ? <Tag size="small">#{item.hitTarget}</Tag> : null}
                  {item.logpoint ? <Tag size="small" color="blue">{logpointLabel}</Tag> : null}
                  {item.stale ? <Tag size="small" color="orange">{staleBreakpointLabel}</Tag> : null}
                </Space>
                <Button
                  theme="borderless"
                  type="danger"
                  size="small"
                  disabled={readonly}
                  onClick={() => onDelete?.(item.id)}
                  data-testid={`microflow-breakpoint-delete-${item.id}`}
                >
                  删除
                </Button>
              </Space>
              <Input
                value={readDraft(item.id, item.condition ?? "")}
                placeholder="Condition expression (optional)"
                disabled={readonly}
                onChange={value => writeDraft(item.id, value)}
                onBlur={() => commitCondition(item)}
                onKeyDown={event => {
                  if (event.key === "Enter") {
                    commitCondition(item);
                  }
                }}
                data-testid={`microflow-breakpoint-condition-${item.id}`}
              />
            </Space>
          </List.Item>
        )}
      />
    </Card>
  );
}

