import { useEffect, useMemo, useState } from "react";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";
import { Empty, Space, Spin, Table, Tag, Toast, Typography } from "@douyinfe/semi-ui";
import type { PublishChannelListItem, StudioLocale } from "../types";
import { getStudioCopy } from "../copy";

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
  onLoaded?: (channels: PublishChannelListItem[]) => void;
  reloadKey?: string | number;
  testId?: string;
}

function typeLabel(locale: StudioLocale, type: string): string {
  const lower = type.toLowerCase();
  /* "Web SDK" / "Open API" / "Lark" 是国际通用产品名，不走 i18n 字典；
     其余渠道（飞书 / 微信 / 企微 / 自定义）走包级 copy 字典。 */
  const sharedNames: Record<string, string> = {
    "web-sdk": "Web SDK",
    "open-api": "Open API",
    lark: "Lark"
  };
  if (sharedNames[lower]) return sharedNames[lower];

  const copy = getStudioCopy(locale);
  switch (lower) {
    case "feishu":
      return copy.channelsList.typeFeishu;
    case "wechat-mp":
      return copy.channelsList.typeWechatMp;
    case "wechat-miniapp":
      return copy.channelsList.typeWechatMiniapp;
    case "wechat-cs":
      return copy.channelsList.typeWechatCs;
    case "wechat":
      return copy.channelsList.typeWechat;
    case "custom":
      return copy.channelsList.typeCustom;
    default:
      return type;
  }
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
  onLoaded,
  reloadKey,
  testId = "studio-publish-channels-panel"
}: ChannelsListPanelProps) {
  const copy = getStudioCopy(locale);
  const [items, setItems] = useState<PublishChannelListItem[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let disposed = false;
    setLoading(true);
    void loader(workspaceId)
      .then((list) => {
        if (!disposed) {
          setItems(list);
          onLoaded?.(list);
        }
      })
      .catch((e: unknown) => {
        if (!disposed) {
          Toast.error(e instanceof Error ? e.message : copy.channelsList.loadFailed);
          setItems([]);
        }
      })
      .finally(() => {
        if (!disposed) setLoading(false);
      });
    return () => {
      disposed = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [loader, workspaceId, locale, reloadKey]);

  const columns: ColumnProps<PublishChannelListItem>[] = useMemo(
    () => [
      {
        title: copy.channelsList.columnName,
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
        title: copy.channelsList.columnType,
        dataIndex: "type",
        width: 140,
        render: (value: unknown) => <Tag>{typeLabel(locale, String(value ?? ""))}</Tag>
      },
      {
        title: copy.channelsList.columnStatus,
        dataIndex: "status",
        width: 100,
        render: (value: unknown) => <Tag color={statusColor(String(value ?? ""))}>{String(value ?? "—")}</Tag>
      },
      {
        title: copy.channelsList.columnAuth,
        dataIndex: "authStatus",
        width: 120,
        render: (value: unknown) =>
          value ? <Tag color={statusColor(String(value))}>{String(value)}</Tag> : <Typography.Text type="tertiary">—</Typography.Text>
      },
      {
        title: copy.channelsList.columnLastSync,
        dataIndex: "lastSyncAt",
        width: 180,
        render: (value: unknown) => (value ? String(value) : "—")
      }
    ],
    [locale, copy]
  );

  if (loading) {
    return <Spin />;
  }
  if (items.length === 0) {
    return <Empty title={copy.channelsList.emptyTitle} description={copy.channelsList.emptyHint} />;
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
