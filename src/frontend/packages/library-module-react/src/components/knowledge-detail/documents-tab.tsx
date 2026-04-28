import { useEffect, useMemo, useState } from "react";
import {
  Button,
  Empty,
  Modal,
  Space,
  Table,
  Tag,
  Toast,
  Typography
} from "@douyinfe/semi-ui";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";
import { IconDelete, IconRefresh, IconUpload } from "@douyinfe/semi-icons";
import type {
  KnowledgeBaseDto,
  KnowledgeDocumentDto,
  KnowledgeJob,
  LibraryKnowledgeApi,
  SupportedLocale
} from "../../types";
import { getLibraryCopy } from "../../copy";
import { formatDateTime, mapKnowledgeType, resolveDocumentStatus } from "../../utils";
import { KnowledgeStateBadge } from "../knowledge-state-badge";

export interface DocumentsTabProps {
  api: LibraryKnowledgeApi;
  locale: SupportedLocale;
  knowledge: KnowledgeBaseDto;
  appKey: string;
  onSelectDocument: (documentId: number) => void;
  onNavigate: (path: string) => void;
}

export function DocumentsTab({
  api,
  locale,
  knowledge,
  appKey,
  onSelectDocument,
  onNavigate
}: DocumentsTabProps) {
  const copy = getLibraryCopy(locale);
  const [items, setItems] = useState<KnowledgeDocumentDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [pageIndex, setPageIndex] = useState(1);
  const [total, setTotal] = useState(0);
  const pageSize = 20;

  async function refresh() {
    setLoading(true);
    try {
      const response = await api.listDocuments(knowledge.id, { pageIndex, pageSize });
      setItems(response.items);
      setTotal(Number(response.total ?? 0));
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void refresh();
    return undefined;
  }, [knowledge.id, pageIndex]);

  // 订阅 mock scheduler 任务事件，自动刷新文档列表（仅 mock 提供 subscribeJobs）
  useEffect(() => {
    if (!api.subscribeJobs) return undefined;
    const unsubscribe = api.subscribeJobs(knowledge.id, (job: KnowledgeJob) => {
      if (job.documentId) {
        void refresh();
      }
    });
    return unsubscribe;
  }, [api, knowledge.id]);

  const columns = useMemo<ColumnProps<KnowledgeDocumentDto>[]>(() => [
    {
      title: copy.documentList,
      dataIndex: "fileName",
      render: (_value: unknown, record: KnowledgeDocumentDto) => (
        <div>
          <div style={{ fontWeight: 600 }}>{record.fileName}</div>
          <Typography.Text type="tertiary" size="small">
            {record.errorMessage || formatDateTime(record.processedAt ?? record.createdAt)}
          </Typography.Text>
          {record.tagsJson && record.tagsJson !== "[]" ? (
            <Typography.Text type="tertiary" size="small" style={{ display: "block" }}>
              tags: {record.tagsJson}
            </Typography.Text>
          ) : null}
        </div>
      )
    },
    {
      title: copy.resourceStatus,
      dataIndex: "status",
      width: 140,
      render: (_value: unknown, record: KnowledgeDocumentDto) => (
        <Space spacing={6} wrap>
          <Tag color={
            resolveDocumentStatus(record.status) === "ready" ? "green"
              : resolveDocumentStatus(record.status) === "failed" ? "red"
              : resolveDocumentStatus(record.status) === "processing" ? "orange"
              : "grey"
          }>
            {copy.docStatusLabels[record.status]}
          </Tag>
          {record.lifecycleStatus ? (
            <KnowledgeStateBadge locale={locale} lifecycle={record.lifecycleStatus} />
          ) : null}
        </Space>
      )
    },
    {
      title: copy.chunks,
      dataIndex: "chunkCount",
      width: 100
    },
    {
      title: copy.actions,
      width: 220,
      render: (_value: unknown, record: KnowledgeDocumentDto) => (
        <Space spacing={4}>
          <Button
            theme="borderless"
            icon={<IconRefresh />}
            onClick={async event => {
              event.stopPropagation();
              try {
                await api.resegmentDocument(knowledge.id, record.id, {
                  parsingStrategy: record.parsingStrategy
                });
                Toast.success(copy.resegment);
                await refresh();
              } catch (error) {
                Toast.error((error as Error).message);
              }
            }}
          >
            {copy.resegment}
          </Button>
          <Button
            theme="borderless"
            type="danger"
            icon={<IconDelete />}
            onClick={async event => {
              event.stopPropagation();
              const confirmed = await new Promise<boolean>(resolve => {
                Modal.confirm({
                  title: copy.delete,
                  content: copy.delete,
                  onOk: () => resolve(true),
                  onCancel: () => resolve(false)
                });
              });
              if (!confirmed) return;
              try {
                await api.deleteDocument(knowledge.id, record.id);
                Toast.success(copy.delete);
                await refresh();
              } catch (error) {
                Toast.error((error as Error).message);
              }
            }}
          >
            {copy.delete}
          </Button>
        </Space>
      )
    }
  ], [api, copy, knowledge.id, locale]);

  return (
    <div className="atlas-table-card semi-card semi-card-bordered semi-card-shadow">
      <div className="semi-card-header">
        <div className="semi-card-header-wrapper">
          <Typography.Title heading={5} style={{ margin: 0 }}>
            {copy.documentList}
          </Typography.Title>
          <Button
            type="primary"
            icon={<IconUpload />}
            onClick={() => onNavigate(
              `/apps/${encodeURIComponent(appKey)}/studio/knowledge-bases/${knowledge.id}/upload?type=${mapKnowledgeType(knowledge.type)}`
            )}
          >
            {copy.upload}
          </Button>
        </div>
      </div>
      <div className="semi-card-body" style={{ padding: 0 }}>
        <Table
          rowKey="id"
          loading={loading}
          columns={columns}
          dataSource={items}
          empty={<Empty description={copy.listEmpty} />}
          pagination={{
            currentPage: pageIndex,
            pageSize,
            total,
            onPageChange: setPageIndex
          }}
          onRow={(record?: KnowledgeDocumentDto) => ({
            onClick: () => {
              if (!record) return;
              onSelectDocument(record.id);
            }
          })}
        />
      </div>
    </div>
  );
}
