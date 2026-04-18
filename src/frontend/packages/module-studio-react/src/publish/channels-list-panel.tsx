import { useEffect, useMemo, useState } from "react";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";
import { Empty, Space, Spin, Table, Tag, Toast, Typography } from "@douyinfe/semi-ui";
import type { PublishChannelListItem, StudioLocale } from "../types";

/**
 * 治理 R1-F1：发布渠道列表 Panel。
 *
 * 仅渲染 Table + 选中态；详情交给 ChannelDetailRouter。
 * 加载方式：上层注入 `loader`，组件不知道 fetch 的具体形态。
 */
export interface ChannelsListPanelProps {
  workspaceId: string;
  locale: StudioLocale;
  loader: (workspaceId: string) => Promise<PublishChannelListItem[]>;
  selectedChannelId?: string | null;
  onSelect: (channel: PublishChannelListItem | null) => void;
  testId?: string;
}

function typeLabel(locale: StudioLocale, type: string): string {
  const lower = type.toLowerCase();
  if (locale === "en-US") {
    const en: Record<string, string> = {
      "web-sdk": "Web SDK",
      "open-api": "Open API",
      feishu: "Feishu (Lark)",
      "wechat-mp": "WeChat MP",
      wechat: "WeChat",
      lark: "Lark",
      custom: "Custom"
    };
    return en[lower] ?? type;
  }
  const zh: Record<string, string> = {
    "web-sdk": "Web SDK",
    "open-api": "Open API",
    feishu: "飞书",
    "wechat-mp": "微信公众号",
    wechat: "企业微信",
    lark: "Lark",
    custom: "自定义"
  };
  return zh[lower] ?? type;
}

function statusColor(status: string): "green" | "amber" | "red" | "grey" {
  switch (status?.toLowerCase()) {
    case "active":
    case "published":
      return "green";
    case "pending":
    case "syncing":
      return "amber";
    case "failed":
      return "red";
    default:
      return "grey";
  }
}

export function ChannelsListPanel({
  workspaceId,
  locale,
  loader,
  selectedChannelId,
  onSelect,
  testId = "studio-publish-channels-panel"
}: ChannelsListPanelProps) {
  const [items, setItems] = useState<PublishChannelListItem[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let disposed = false;
    setLoading(true);
    void loader(workspaceId)
      .then((list) => {
        if (!disposed) setItems(list);
      })
      .catch((e: unknown) => {
        if (!disposed) {
          Toast.error(e instanceof Error ? e.message : locale === "en-US" ? "Failed to load channels." : "加载渠道失败。");
          setItems([]);
        }
      })
      .finally(() => {
        if (!disposed) setLoading(false);
      });
    return () => {
      disposed = true;
    };
  }, [loader, workspaceId, locale]);

  const columns: ColumnProps<PublishChannelListItem>[] = useMemo(
    () => [
      {
        title: locale === "en-US" ? "Name" : "渠道名",
        dataIndex: "name",
        render: (value: unknown, record) => (
          <Space vertical align="start" spacing={2}>
            <Typography.Text strong>{String(value ?? record.id)}</Typography.Text>
            <Typography.Text type="tertiary" size="small">
              {record.id}
            </Typography.Text>
          </Space>
        )
      },
      {
        title: locale === "en-US" ? "Type" : "类型",
        dataIndex: "type",
        width: 140,
        render: (value: unknown) => <Tag>{typeLabel(locale, String(value ?? ""))}</Tag>
      },
      {
        title: locale === "en-US" ? "Status" : "状态",
        dataIndex: "status",
        width: 100,
        render: (value: unknown) => <Tag color={statusColor(String(value ?? ""))}>{String(value ?? "—")}</Tag>
      },
      {
        title: locale === "en-US" ? "Auth" : "认证",
        dataIndex: "authStatus",
        width: 120,
        render: (value: unknown) =>
          value ? <Tag color={statusColor(String(value))}>{String(value)}</Tag> : <Typography.Text type="tertiary">—</Typography.Text>
      },
      {
        title: locale === "en-US" ? "Last sync" : "上次同步",
        dataIndex: "lastSyncAt",
        width: 180,
        render: (value: unknown) => (value ? String(value) : "—")
      }
    ],
    [locale]
  );

  if (loading) {
    return <Spin />;
  }
  if (items.length === 0) {
    return (
      <Empty
        title={locale === "en-US" ? "No publish channels" : "暂无发布渠道"}
        description={locale === "en-US" ? "Create a channel from publish workflow first." : "请在发布流程中先创建渠道。"}
      />
    );
  }

  return (
    <Table<PublishChannelListItem>
      data-testid={testId}
      rowKey={(r) => r?.id ?? "row"}
      columns={columns}
      dataSource={items}
      pagination={false}
      size="small"
      rowSelection={{
        type: "radio",
        selectedRowKeys: selectedChannelId ? [selectedChannelId] : [],
        onChange: (_keys, rows) => {
          const next = rows && rows.length > 0 ? rows[0] ?? null : null;
          onSelect(next);
        }
      }}
      onRow={(record) => ({
        onClick: () => {
          if (!record) return;
          onSelect(record);
        }
      })}
    />
  );
}
