import { useEffect, useState } from "react";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";
import { Button, Card, Space, Table, Toast, Typography } from "@douyinfe/semi-ui";
import type { PublishCenterItem, StudioLocale, StudioModuleApi } from "../types";

export interface TokenManagementProps {
  api: Pick<StudioModuleApi, "regenerateAgentEmbedToken">;
  locale: StudioLocale;
  items: PublishCenterItem[];
  testId?: string;
}

export function TokenManagement({ api, locale, items, testId = "studio-publish-token-management" }: TokenManagementProps) {
  const [rows, setRows] = useState<PublishCenterItem[]>(items);
  const [busyKey, setBusyKey] = useState<string | null>(null);

  useEffect(() => {
    setRows(items);
  }, [items]);

  const title = locale === "en-US" ? "Embed tokens" : "嵌入令牌";
  const hint =
    locale === "en-US"
      ? "Copy embed tokens for hosted widgets. Regenerate invalidates the previous token for that agent."
      : "复制嵌入令牌用于前端托管组件。轮换令牌会使旧令牌失效（按智能体维度）。";

  async function handleRegenerate(item: PublishCenterItem) {
    if (item.resourceType !== "agent") {
      Toast.warning(locale === "en-US" ? "Only agents support regenerate here." : "当前仅支持对智能体轮换嵌入令牌。");
      return;
    }

    const key = `${item.resourceType}:${item.resourceId}`;
    setBusyKey(key);
    try {
      const result = await api.regenerateAgentEmbedToken(item.resourceId);
      setRows((prev) =>
        prev.map((row) =>
          row.resourceType === "agent" && row.resourceId === item.resourceId ? { ...row, embedToken: result.embedToken } : row
        )
      );
      Toast.success(locale === "en-US" ? "Token regenerated." : "已重新生成令牌。");
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : locale === "en-US" ? "Regenerate failed." : "轮换失败。");
    } finally {
      setBusyKey(null);
    }
  }

  const columns: ColumnProps<PublishCenterItem>[] = [
    {
      title: locale === "en-US" ? "Resource" : "资源",
      dataIndex: "resourceName",
      render: (_v, record) => (
        <Space vertical align="start" spacing={4}>
          <Typography.Text strong>{record.resourceName}</Typography.Text>
          <Typography.Text type="tertiary" size="small">
            {record.resourceType} · {record.resourceId}
          </Typography.Text>
        </Space>
      )
    },
    {
      title: locale === "en-US" ? "Embed token" : "嵌入令牌",
      dataIndex: "embedToken",
      render: (_v, record) =>
        record.embedToken ? (
          <Typography.Text copyable={{ content: record.embedToken }} ellipsis={{ showTooltip: true, rows: 2 }}>
            {record.embedToken}
          </Typography.Text>
        ) : (
          <Typography.Text type="tertiary">{locale === "en-US" ? "Not issued" : "未下发"}</Typography.Text>
        )
    },
    {
      title: locale === "en-US" ? "Actions" : "操作",
      key: "actions",
      width: 140,
      render: (_v, record) => {
        const key = `${record.resourceType}:${record.resourceId}`;
        const loading = busyKey === key;
        return (
          <Button disabled={record.resourceType !== "agent"} loading={loading} onClick={() => void handleRegenerate(record)}>
            {locale === "en-US" ? "Regenerate" : "重新生成"}
          </Button>
        );
      }
    }
  ];

  const withToken = rows.filter((r) => r.resourceType === "agent" || r.embedToken);

  return (
    <Card data-testid={testId} title={title} bordered>
      <Typography.Paragraph type="tertiary">{hint}</Typography.Paragraph>
      {withToken.length === 0 ? (
        <Typography.Text type="tertiary">
          {locale === "en-US" ? "No embed tokens in the publish list yet." : "发布清单中暂无可用的嵌入令牌。"}
        </Typography.Text>
      ) : (
        <Table<PublishCenterItem>
          rowKey={(r) => (r ? `${r.resourceType}:${r.resourceId}` : "row")}
          columns={columns}
          dataSource={withToken}
          pagination={false}
          size="small"
        />
      )}
    </Card>
  );
}
