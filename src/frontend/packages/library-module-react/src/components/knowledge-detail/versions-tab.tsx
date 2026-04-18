import { useEffect, useMemo, useState } from "react";
import {
  Banner,
  Button,
  Empty,
  Input,
  Modal,
  Select,
  SideSheet,
  Space,
  Table,
  Tag,
  Toast,
  Typography
} from "@douyinfe/semi-ui";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";
import { IconHistory, IconPlus, IconRefresh } from "@douyinfe/semi-icons";
import type {
  KnowledgeBaseDto,
  KnowledgeVersion,
  KnowledgeVersionDiff,
  LibraryKnowledgeApi,
  SupportedLocale
} from "../../types";
import { getLibraryCopy } from "../../copy";
import { formatDateTime } from "../../utils";

export interface VersionsTabProps {
  api: LibraryKnowledgeApi;
  locale: SupportedLocale;
  knowledge: KnowledgeBaseDto;
}

export function VersionsTab({ api, locale, knowledge }: VersionsTabProps) {
  const copy = getLibraryCopy(locale);
  const [items, setItems] = useState<KnowledgeVersion[]>([]);
  const [loading, setLoading] = useState(false);
  const [createVisible, setCreateVisible] = useState(false);
  const [label, setLabel] = useState("");
  const [note, setNote] = useState("");
  const [diff, setDiff] = useState<KnowledgeVersionDiff | null>(null);
  const [diffSheetVisible, setDiffSheetVisible] = useState(false);
  const [fromVersionId, setFromVersionId] = useState<number | null>(null);
  const [toVersionId, setToVersionId] = useState<number | null>(null);

  async function refresh() {
    if (!api.listVersions) return;
    setLoading(true);
    try {
      const response = await api.listVersions(knowledge.id, { pageIndex: 1, pageSize: 100 });
      setItems(response.items);
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void refresh();
    return undefined;
  }, [knowledge.id]);

  const statusColor: Record<KnowledgeVersion["status"], "green" | "orange" | "grey"> = {
    draft: "orange",
    released: "green",
    archived: "grey"
  };
  const statusLabel: Record<KnowledgeVersion["status"], string> = {
    draft: copy.versionsStatusDraft,
    released: copy.versionsStatusReleased,
    archived: copy.versionsStatusArchived
  };

  const columns = useMemo<ColumnProps<KnowledgeVersion>[]>(() => [
    {
      title: "#",
      dataIndex: "id",
      width: 70
    },
    {
      title: copy.versionsLabelPlaceholder,
      dataIndex: "label",
      render: (_value: unknown, record: KnowledgeVersion) => (
        <Space spacing={6}>
          <strong>{record.label}</strong>
          <Tag color={statusColor[record.status]} size="small">{statusLabel[record.status]}</Tag>
        </Space>
      )
    },
    {
      title: copy.documents,
      dataIndex: "documentCount",
      width: 100
    },
    {
      title: copy.chunks,
      dataIndex: "chunkCount",
      width: 100
    },
    {
      title: copy.updatedAt,
      dataIndex: "createdAt",
      width: 200,
      render: (value: unknown, record: KnowledgeVersion) => (
        <div>
          <div>{formatDateTime(typeof value === "string" ? value : undefined)}</div>
          {record.releasedAt ? (
            <Typography.Text type="tertiary" size="small">
              {copy.versionsStatusReleased}: {formatDateTime(record.releasedAt)}
            </Typography.Text>
          ) : null}
        </div>
      )
    },
    {
      title: copy.actions,
      width: 280,
      render: (_value: unknown, record: KnowledgeVersion) => (
        <Space spacing={4}>
          {record.status !== "released" ? (
            <Button
              theme="borderless"
              onClick={async () => {
                if (!api.releaseVersion) return;
                try {
                  await api.releaseVersion(knowledge.id, record.id);
                  await refresh();
                } catch (error) {
                  Toast.error((error as Error).message);
                }
              }}
            >
              {copy.versionsRelease}
            </Button>
          ) : null}
          <Button
            theme="borderless"
            icon={<IconRefresh />}
            onClick={async () => {
              if (!api.rollbackToVersion) return;
              try {
                await api.rollbackToVersion(knowledge.id, record.id);
                Toast.success(copy.versionsRolledBack);
                await refresh();
              } catch (error) {
                Toast.error((error as Error).message);
              }
            }}
          >
            {copy.versionsRollback}
          </Button>
          <Button
            theme="borderless"
            icon={<IconHistory />}
            onClick={() => {
              setFromVersionId(record.id);
              setToVersionId(items.find(v => v.id !== record.id)?.id ?? null);
              setDiff(null);
              setDiffSheetVisible(true);
            }}
          >
            {copy.versionsDiff}
          </Button>
        </Space>
      )
    }
  ], [api, copy, items, knowledge.id, statusColor, statusLabel]);

  async function handleCreate() {
    if (!api.createVersionSnapshot) return;
    if (!label.trim()) {
      Toast.warning(copy.versionsLabelPlaceholder);
      return;
    }
    try {
      await api.createVersionSnapshot(knowledge.id, { label: label.trim(), note: note.trim() || undefined });
      Toast.success(copy.versionsCreateSuccess);
      setCreateVisible(false);
      setLabel("");
      setNote("");
      await refresh();
    } catch (error) {
      Toast.error((error as Error).message);
    }
  }

  async function handleDiff() {
    if (!api.diffVersions || fromVersionId == null || toVersionId == null) return;
    try {
      const result = await api.diffVersions(knowledge.id, fromVersionId, toVersionId);
      setDiff(result);
    } catch (error) {
      Toast.error((error as Error).message);
    }
  }

  return (
    <div className="atlas-table-card semi-card semi-card-bordered semi-card-shadow">
      <div className="semi-card-header">
        <div className="semi-card-header-wrapper">
          <div>
            <Typography.Title heading={5} style={{ margin: 0 }}>{copy.versionsTitle}</Typography.Title>
            <Typography.Text type="tertiary">{copy.versionsSubtitle}</Typography.Text>
          </div>
          <Button type="primary" icon={<IconPlus />} onClick={() => setCreateVisible(true)}>
            {copy.versionsCreateTitle}
          </Button>
        </div>
      </div>
      <div className="semi-card-body" style={{ padding: 0 }}>
        {items.length === 0 ? (
          <div style={{ padding: 32 }}>
            <Empty description={copy.versionsEmpty} />
          </div>
        ) : (
          <Table rowKey="id" loading={loading} columns={columns} dataSource={items} pagination={false} />
        )}
      </div>

      <Modal
        title={copy.versionsCreateTitle}
        visible={createVisible}
        onOk={handleCreate}
        onCancel={() => setCreateVisible(false)}
        okText={copy.create}
        cancelText={copy.cancel}
      >
        <Space vertical align="start" style={{ width: "100%" }}>
          <Banner type="info" description={copy.versionsSubtitle} />
          <Typography.Text strong>{copy.versionsLabelPlaceholder}</Typography.Text>
          <Input value={label} onChange={value => setLabel(value)} placeholder={copy.versionsLabelPlaceholder} />
          <Typography.Text strong>{copy.versionsNotePlaceholder}</Typography.Text>
          <Input value={note} onChange={value => setNote(value)} placeholder={copy.versionsNotePlaceholder} />
        </Space>
      </Modal>

      <SideSheet
        title={copy.versionsDiffTitle}
        visible={diffSheetVisible}
        width={520}
        onCancel={() => setDiffSheetVisible(false)}
      >
        <Space vertical align="start" style={{ width: "100%" }}>
          <Banner type="info" description={copy.versionsDiffTitle} />
          <Typography.Text strong>{copy.versionsLabelPlaceholder} A</Typography.Text>
          <Select
            value={fromVersionId ?? undefined}
            style={{ width: "100%" }}
            optionList={items.map(v => ({ label: v.label, value: v.id }))}
            onChange={value => setFromVersionId(Number(value) || null)}
          />
          <Typography.Text strong>{copy.versionsLabelPlaceholder} B</Typography.Text>
          <Select
            value={toVersionId ?? undefined}
            style={{ width: "100%" }}
            optionList={items.map(v => ({ label: v.label, value: v.id }))}
            onChange={value => setToVersionId(Number(value) || null)}
          />
          <Button type="primary" onClick={handleDiff} disabled={fromVersionId == null || toVersionId == null}>
            {copy.versionsDiff}
          </Button>
          {diff ? (
            <div style={{ width: "100%", marginTop: 12 }}>
              {diff.entries.map((entry, idx) => (
                <Typography.Paragraph key={idx} type={entry.changeType === "removed" ? "danger" : "success"}>
                  [{entry.kind}] {entry.changeType.toUpperCase()} — {entry.summary}
                </Typography.Paragraph>
              ))}
            </div>
          ) : null}
        </Space>
      </SideSheet>
    </div>
  );
}
