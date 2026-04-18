import { useEffect, useMemo, useState } from "react";
import {
  Banner,
  Button,
  Empty,
  Progress,
  Select,
  SideSheet,
  Space,
  Table,
  Tag,
  Toast,
  Typography
} from "@douyinfe/semi-ui";
import type { ColumnProps } from "@douyinfe/semi-ui/lib/es/table";
import { IconArrowLeft, IconRefresh, IconStop } from "@douyinfe/semi-icons";
import type {
  KnowledgeBaseDto,
  KnowledgeJob,
  KnowledgeJobStatus,
  KnowledgeJobType,
  LibraryKnowledgeApi,
  SupportedLocale
} from "../types";
import { getLibraryCopy } from "../copy";
import { formatDateTime } from "../utils";
import { KnowledgeStateBadge } from "./knowledge-state-badge";

export interface KnowledgeJobsCenterPageProps {
  api: LibraryKnowledgeApi;
  locale: SupportedLocale;
  appKey: string;
  spaceId: string;
  onNavigate: (path: string) => void;
}

/**
 * 全局任务中心：跨知识库统一查看 parse / index / rebuild / GC 任务，
 * 支持死信重投与基于 traceId 的调用链追踪入口。
 */
export function KnowledgeJobsCenterPage({ api, locale, appKey, onNavigate }: KnowledgeJobsCenterPageProps) {
  const copy = getLibraryCopy(locale);
  const [items, setItems] = useState<KnowledgeJob[]>([]);
  const [kbMap, setKbMap] = useState<Map<number, KnowledgeBaseDto>>(new Map());
  const [loading, setLoading] = useState(false);
  const [statusFilter, setStatusFilter] = useState<KnowledgeJobStatus | "all">("all");
  const [typeFilter, setTypeFilter] = useState<KnowledgeJobType | "all">("all");
  const [active, setActive] = useState<KnowledgeJob | null>(null);

  async function refresh() {
    if (!api.listJobsAcrossKnowledgeBases) {
      Toast.warning(copy.jobsEmpty);
      return;
    }
    setLoading(true);
    try {
      const [jobsRes, kbRes] = await Promise.all([
        api.listJobsAcrossKnowledgeBases({
          pageIndex: 1,
          pageSize: 200,
          status: statusFilter === "all" ? undefined : statusFilter,
          type: typeFilter === "all" ? undefined : typeFilter
        }),
        api.listKnowledgeBases({ pageIndex: 1, pageSize: 200 })
      ]);
      setItems(jobsRes.items);
      const map = new Map<number, KnowledgeBaseDto>();
      kbRes.items.forEach(kb => map.set(kb.id, kb));
      setKbMap(map);
    } catch (error) {
      Toast.error((error as Error).message);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void refresh();
    return undefined;
  }, [statusFilter, typeFilter]);

  const typeLabel = useMemo<Record<KnowledgeJobType, string>>(() => ({
    parse: copy.jobTypeParse,
    index: copy.jobTypeIndex,
    rebuild: copy.jobTypeRebuild,
    gc: copy.jobTypeGc
  }), [copy]);

  const columns = useMemo<ColumnProps<KnowledgeJob>[]>(() => [
    { title: "#", dataIndex: "id", width: 80 },
    {
      title: copy.knowledgeBase,
      dataIndex: "knowledgeBaseId",
      width: 200,
      render: (value: unknown) => {
        const kb = kbMap.get(Number(value));
        if (!kb) return `KB #${value}`;
        return (
          <Button
            theme="borderless"
            onClick={() => onNavigate(`/apps/${encodeURIComponent(appKey)}/studio/knowledge-bases/${kb.id}?tab=jobs`)}
          >
            {kb.name}
          </Button>
        );
      }
    },
    {
      title: copy.resourceType,
      dataIndex: "type",
      width: 100,
      render: (value: unknown) => <Tag color="cyan">{typeLabel[value as KnowledgeJobType]}</Tag>
    },
    {
      title: copy.resourceStatus,
      dataIndex: "status",
      width: 130,
      render: (value: unknown) => (
        <KnowledgeStateBadge locale={locale} jobStatus={value as KnowledgeJobStatus} />
      )
    },
    {
      title: copy.uploadProgress,
      dataIndex: "progress",
      width: 160,
      render: (value: unknown) => (
        <Progress percent={Number(value) || 0} size="small" stroke={(Number(value) ?? 0) >= 100 ? "#22c55e" : "#f59e0b"} />
      )
    },
    {
      title: copy.jobAttempts,
      dataIndex: "attempts",
      width: 90,
      render: (_value: unknown, record: KnowledgeJob) => `${record.attempts}/${record.maxAttempts}`
    },
    {
      title: copy.jobEnqueuedAt,
      dataIndex: "enqueuedAt",
      width: 180,
      render: (value: unknown) => formatDateTime(typeof value === "string" ? value : undefined)
    },
    {
      title: copy.actions,
      width: 220,
      render: (_value: unknown, record: KnowledgeJob) => (
        <Space spacing={4}>
          <Button theme="borderless" onClick={() => setActive(record)}>{copy.open}</Button>
          {record.status === "DeadLetter" || record.status === "Failed" ? (
            <Button
              theme="borderless"
              icon={<IconRefresh />}
              onClick={async () => {
                if (!api.retryDeadLetter) return;
                try {
                  await api.retryDeadLetter(record.knowledgeBaseId, record.id);
                  await refresh();
                } catch (error) {
                  Toast.error((error as Error).message);
                }
              }}
            >
              {copy.jobActionRetry}
            </Button>
          ) : null}
          {record.status === "Queued" || record.status === "Running" || record.status === "Retrying" ? (
            <Button
              theme="borderless"
              type="warning"
              icon={<IconStop />}
              onClick={async () => {
                if (!api.cancelJob) return;
                try {
                  await api.cancelJob(record.knowledgeBaseId, record.id);
                  await refresh();
                } catch (error) {
                  Toast.error((error as Error).message);
                }
              }}
            >
              {copy.jobActionCancel}
            </Button>
          ) : null}
        </Space>
      )
    }
  ], [api, copy, kbMap, locale, onNavigate, appKey, typeLabel]);

  return (
    <div className="atlas-library-page" data-testid="app-knowledge-jobs-center">
      <div className="atlas-page-header">
        <Space spacing={8}>
          <Button icon={<IconArrowLeft />} onClick={() => onNavigate(`/apps/${encodeURIComponent(appKey)}/studio/knowledge-bases`)}>
            {copy.backToLibrary}
          </Button>
          <div>
            <Typography.Title heading={3} style={{ margin: 0 }}>{copy.jobsCenterTitle}</Typography.Title>
            <Typography.Text type="tertiary">{copy.jobsCenterSubtitle}</Typography.Text>
          </div>
        </Space>
      </div>

      <div className="atlas-filter-card semi-card semi-card-bordered semi-card-shadow">
        <div className="semi-card-body">
          <Space spacing={8}>
            <Select
              value={typeFilter}
              style={{ width: 160 }}
              optionList={[
                { label: copy.allTypes, value: "all" },
                { label: copy.jobTypeParse, value: "parse" },
                { label: copy.jobTypeIndex, value: "index" },
                { label: copy.jobTypeRebuild, value: "rebuild" },
                { label: copy.jobTypeGc, value: "gc" }
              ]}
              onChange={value => setTypeFilter(value as KnowledgeJobType | "all")}
            />
            <Select
              value={statusFilter}
              style={{ width: 160 }}
              optionList={[
                { label: copy.allStatus, value: "all" },
                { label: copy.jobStatusQueued, value: "Queued" },
                { label: copy.jobStatusRunning, value: "Running" },
                { label: copy.jobStatusSucceeded, value: "Succeeded" },
                { label: copy.jobStatusFailed, value: "Failed" },
                { label: copy.jobStatusRetrying, value: "Retrying" },
                { label: copy.jobStatusDeadLetter, value: "DeadLetter" },
                { label: copy.jobStatusCanceled, value: "Canceled" }
              ]}
              onChange={value => setStatusFilter(value as KnowledgeJobStatus | "all")}
            />
            <Button icon={<IconRefresh />} onClick={() => void refresh()}>
              {copy.runTest}
            </Button>
          </Space>
        </div>
      </div>

      <div className="atlas-table-card semi-card semi-card-bordered semi-card-shadow">
        <div className="semi-card-body" style={{ padding: 0 }}>
          {items.length === 0 ? (
            <div style={{ padding: 32 }}><Empty description={copy.jobsEmpty} /></div>
          ) : (
            <Table rowKey="id" loading={loading} columns={columns} dataSource={items} pagination={false} />
          )}
        </div>
      </div>

      <SideSheet
        title={`Job #${active?.id ?? ""}`}
        visible={!!active}
        width={520}
        onCancel={() => setActive(null)}
      >
        {active ? (
          <Space vertical align="start" style={{ width: "100%" }}>
            <Banner
              type={
                active.status === "Failed" || active.status === "DeadLetter" ? "danger"
                  : active.status === "Succeeded" ? "success"
                  : "info"
              }
              description={`${typeLabel[active.type]} · ${active.status}`}
            />
            <Typography.Text>knowledgeBaseId={active.knowledgeBaseId}</Typography.Text>
            {active.documentId ? <Typography.Text>documentId={active.documentId}</Typography.Text> : null}
            <Typography.Title heading={6} style={{ marginTop: 12 }}>{copy.jobLogsTitle}</Typography.Title>
            {active.logs.map((log, idx) => (
              <Typography.Paragraph
                key={`${active.id}-${idx}`}
                type={log.level === "error" ? "danger" : log.level === "warn" ? "warning" : "secondary"}
              >
                [{formatDateTime(log.ts)}] {log.message}
              </Typography.Paragraph>
            ))}
            {active.errorMessage ? (
              <Banner type="danger" description={active.errorMessage} />
            ) : null}
          </Space>
        ) : null}
      </SideSheet>
    </div>
  );
}
