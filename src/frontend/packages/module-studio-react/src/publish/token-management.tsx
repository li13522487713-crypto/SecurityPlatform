import { useEffect, useState } from "react";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";
import { Button, Card, Space, Table, Toast, Typography } from "@douyinfe/semi-ui";
import type { PublishCenterItem, StudioLocale, StudioModuleApi } from "../types";
import { getStudioCopy } from "../copy";

export interface TokenManagementProps {
  api: Pick<StudioModuleApi, "regenerateAgentEmbedToken">;
  locale: StudioLocale;
  items: PublishCenterItem[];
  testId?: string;
}

export function TokenManagement({ api, locale, items, testId = "studio-publish-token-management" }: TokenManagementProps) {
  const copy = getStudioCopy(locale);
  const [rows, setRows] = useState<PublishCenterItem[]>(items);
  const [busyKey, setBusyKey] = useState<string | null>(null);

  useEffect(() => {
    setRows(items);
  }, [items]);

  async function handleRegenerate(item: PublishCenterItem) {
    if (item.resourceType !== "agent") {
      Toast.warning(copy.tokenManagement.onlyAgentSupportRegenerate);
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
      Toast.success(copy.tokenManagement.tokenRegenerated);
    } catch (error) {
      Toast.error(error instanceof Error ? error.message : copy.tokenManagement.regenerateFailed);
    } finally {
      setBusyKey(null);
    }
  }

  const columns: ColumnProps<PublishCenterItem>[] = [
    {
      title: copy.tokenManagement.columnResource,
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
      title: copy.tokenManagement.columnEmbedToken,
      dataIndex: "embedToken",
      render: (_v, record) =>
        record.embedToken ? (
          <Typography.Text copyable={{ content: record.embedToken }} ellipsis={{ showTooltip: true, rows: 2 }}>
            {record.embedToken}
          </Typography.Text>
        ) : (
          <Typography.Text type="tertiary">{copy.tokenManagement.notIssued}</Typography.Text>
        )
    },
    {
      title: copy.tokenManagement.columnActions,
      key: "actions",
      width: 140,
      render: (_v, record) => {
        const key = `${record.resourceType}:${record.resourceId}`;
        const loading = busyKey === key;
        return (
          <Button disabled={record.resourceType !== "agent"} loading={loading} onClick={() => void handleRegenerate(record)}>
            {copy.tokenManagement.regenerate}
          </Button>
        );
      }
    }
  ];

  const withToken = rows.filter((r) => r.resourceType === "agent" || r.embedToken);

  return (
    <Card data-testid={testId} title={copy.tokenManagement.title} bordered>
      <Typography.Paragraph type="tertiary">{copy.tokenManagement.hint}</Typography.Paragraph>
      {withToken.length === 0 ? (
        <Typography.Text type="tertiary">{copy.tokenManagement.emptyHint}</Typography.Text>
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
