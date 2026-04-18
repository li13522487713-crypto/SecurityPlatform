import { useEffect, useMemo, useState } from "react";
import {
  Banner,
  Button,
  Empty,
  Space,
  Table,
  Tag,
  Toast,
  Typography
} from "@douyinfe/semi-ui";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";
import { IconArrowLeft } from "@douyinfe/semi-icons";
import type {
  KnowledgeProviderConfig,
  KnowledgeProviderRole,
  LibraryKnowledgeApi,
  SupportedLocale
} from "../types";
import { getLibraryCopy } from "../copy";
import { formatDateTime } from "../utils";

export interface KnowledgeProviderConfigPageProps {
  api: LibraryKnowledgeApi;
  locale: SupportedLocale;
  appKey: string;
  onNavigate: (path: string) => void;
}

export function KnowledgeProviderConfigPage({ api, locale, appKey, onNavigate }: KnowledgeProviderConfigPageProps) {
  const copy = getLibraryCopy(locale);
  const [items, setItems] = useState<KnowledgeProviderConfig[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    let disposed = false;
    if (!api.listProviderConfigs) {
      return undefined;
    }
    setLoading(true);
    void api.listProviderConfigs().then(list => {
      if (!disposed) setItems(list);
    }).catch(error => {
      if (!disposed) Toast.error((error as Error).message);
    }).finally(() => {
      if (!disposed) setLoading(false);
    });
    return () => {
      disposed = true;
    };
  }, [api]);

  const roleLabel = useMemo<Record<KnowledgeProviderRole, string>>(() => ({
    upload: copy.providerRoleUpload,
    storage: copy.providerRoleStorage,
    vector: copy.providerRoleVector,
    embedding: copy.providerRoleEmbedding,
    generation: copy.providerRoleGeneration
  }), [copy]);

  const statusColor: Record<KnowledgeProviderConfig["status"], "green" | "orange" | "grey"> = {
    active: "green",
    degraded: "orange",
    inactive: "grey"
  };
  const statusLabel: Record<KnowledgeProviderConfig["status"], string> = {
    active: copy.providerStatusActive,
    degraded: copy.providerStatusDegraded,
    inactive: copy.providerStatusInactive
  };

  const columns: ColumnProps<KnowledgeProviderConfig>[] = [
    {
      title: copy.resourceType,
      dataIndex: "role",
      width: 120,
      render: (value: unknown) => <Tag color="violet">{roleLabel[value as KnowledgeProviderRole]}</Tag>
    },
    { title: copy.resourceType, dataIndex: "displayName" },
    {
      title: copy.resourceStatus,
      dataIndex: "status",
      width: 110,
      render: (_value: unknown, record: KnowledgeProviderConfig) => (
        <Space spacing={4}>
          {record.isDefault ? <Tag color="cyan" size="small">default</Tag> : null}
          <Tag color={statusColor[record.status]} size="small">{statusLabel[record.status]}</Tag>
        </Space>
      )
    },
    {
      title: "Endpoint",
      dataIndex: "endpoint",
      render: (value: unknown) => value ? String(value) : "-"
    },
    {
      title: "Bucket / Index / Model",
      dataIndex: "bucketOrIndex",
      render: (value: unknown) => value ? String(value) : "-"
    },
    {
      title: copy.updatedAt,
      dataIndex: "updatedAt",
      width: 180,
      render: (value: unknown) => formatDateTime(typeof value === "string" ? value : undefined)
    }
  ];

  return (
    <div className="atlas-library-page" data-testid="app-knowledge-provider-center">
      <div className="atlas-page-header">
        <Space spacing={8}>
          <Button icon={<IconArrowLeft />} onClick={() => onNavigate(`/apps/${encodeURIComponent(appKey)}/studio/knowledge-bases`)}>
            {copy.backToLibrary}
          </Button>
          <div>
            <Typography.Title heading={3} style={{ margin: 0 }}>{copy.providerCenterTitle}</Typography.Title>
            <Typography.Text type="tertiary">{copy.providerCenterSubtitle}</Typography.Text>
          </div>
        </Space>
      </div>

      <Banner type="info" description={copy.providerCenterSubtitle} />

      <div className="atlas-table-card semi-card semi-card-bordered semi-card-shadow">
        <div className="semi-card-body" style={{ padding: 0 }}>
          {items.length === 0 ? (
            <div style={{ padding: 32 }}><Empty description={copy.providerCenterTitle} /></div>
          ) : (
            <Table rowKey="id" loading={loading} columns={columns} dataSource={items} pagination={false} />
          )}
        </div>
      </div>
    </div>
  );
}
