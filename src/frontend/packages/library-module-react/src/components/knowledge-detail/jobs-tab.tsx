import { useEffect, useMemo, useState } from "react";
import {
  Banner,
  Button,
  Empty,
  Modal,
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
import { IconRefresh, IconStop } from "@douyinfe/semi-icons";
import type {
  KnowledgeBaseDto,
  KnowledgeJob,
  KnowledgeJobStatus,
  KnowledgeJobType,
  LibraryKnowledgeApi,
  SupportedLocale
} from "../../types";
import { getLibraryCopy } from "../../copy";
import { formatDateTime } from "../../utils";
import { KnowledgeStateBadge } from "../knowledge-state-badge";

export interface JobsTabProps {
  api: LibraryKnowledgeApi;
  locale: SupportedLocale;
  knowledge: KnowledgeBaseDto;
}

export function JobsTab({ api, locale, knowledge }: JobsTabProps) {
  const copy = getLibraryCopy(locale);
  const [items, setItems] = useState<KnowledgeJob[]>([]);
  const [loading, setLoading] = useState(false);
  const [statusFilter, setStatusFilter] = useState<KnowledgeJobStatus | "all">("all");
  const [typeFilter, setTypeFilter] = useState<KnowledgeJobType | "all">("all");
  const [active, setActive] = useState<KnowledgeJob | null>(null);

  async function refresh() {
    if (!api.listJobs) return;
    setLoading(true);
    try {
      const response = await api.listJobs(knowledge.id, {
        pageIndex: 1,
        pageSize: 100,
        status: statusFilter === "all" ? undefined : statusFilter,
        type: typeFilter === "all" ? undefined : typeFilter
      });
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
  }, [knowledge.id, statusFilter, typeFilter]);

  // 订阅 mock scheduler，实时刷新当前 KB 的任务
  useEffect(() => {
    if (!api.subscribeJobs) return undefined;
    return api.subscribeJobs(knowledge.id, () => {
      void refresh();
    });
  }, [api, knowledge.id, statusFilter, typeFilter]);

  const typeLabel = useMemo<Record<KnowledgeJobType, string>>(() => ({
    parse: copy.jobTypeParse,
    index: copy.jobTypeIndex,
    rebuild: copy.jobTypeRebuild,
    gc: copy.jobTypeGc
  }), [copy]);

  const columns = useMemo<ColumnProps<KnowledgeJob>[]>(() => [
    { title: "#", dataIndex: "id", width: 80 },
    {
      title: copy.resourceType,
      dataIndex: "type",
      width: 100,
      render: (value: unknown) => <Tag color="cyan">{typeLabel[value as KnowledgeJobType]}</Tag>
    },
    {
      title: copy.resourceStatus,
      dataIndex: "status",
      width: 110,
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
      width: 200,
      render: (_value: unknown, record: KnowledgeJob) => (
        <Space spacing={4}>
          <Button theme="borderless" onClick={() => setActive(record)}>
            {copy.open}
          </Button>
          {record.status === "DeadLetter" || record.status === "Failed" ? (
            <Button
              theme="borderless"
              icon={<IconRefresh />}
              onClick={async () => {
                if (!api.retryDeadLetter) return;
                try {
                  await api.retryDeadLetter(knowledge.id, record.id);
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
                  await api.cancelJob(knowledge.id, record.id);
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
  ], [api, copy, knowledge.id, locale, typeLabel]);

  return (
    <div className="atlas-table-card semi-card semi-card-bordered semi-card-shadow">
      <div className="semi-card-header">
        <div className="semi-card-header-wrapper">
          <div>
            <Typography.Title heading={5} style={{ margin: 0 }}>{copy.jobsTitle}</Typography.Title>
            <Typography.Text type="tertiary">{copy.jobsSubtitle}</Typography.Text>
          </div>
          <Space spacing={8}>
            <Select
              value={typeFilter}
              style={{ width: 140 }}
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
              style={{ width: 140 }}
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
            <Button
              icon={<IconRefresh />}
              onClick={async () => {
                if (!api.rebuildIndex) return;
                const confirmed = await new Promise<boolean>(resolve => {
                  Modal.confirm({
                    title: copy.rebuildIndex,
                    content: copy.rebuildIndexConfirm,
                    onOk: () => resolve(true),
                    onCancel: () => resolve(false)
                  });
                });
                if (!confirmed) return;
                try {
                  await api.rebuildIndex(knowledge.id);
                  Toast.success(copy.rebuildIndex);
                  await refresh();
                } catch (error) {
                  Toast.error((error as Error).message);
                }
              }}
            >
              {copy.jobActionRebuildIndex}
            </Button>
          </Space>
        </div>
      </div>
      <div className="semi-card-body" style={{ padding: 0 }}>
        {items.length === 0 ? (
          <div style={{ padding: 32 }}>
            <Empty description={copy.jobsEmpty} />
          </div>
        ) : (
          <Table rowKey="id" loading={loading} columns={columns} dataSource={items} pagination={false} />
        )}
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
                active.status === "Failed" || active.status === "DeadLetter"
                  ? "danger"
                  : active.status === "Succeeded"
                    ? "success"
                    : "info"
              }
              description={`${typeLabel[active.type]} · ${active.status}`}
            />
            <div className="atlas-summary-grid">
              <div className="atlas-summary-tile">
                <span>{copy.jobAttempts}</span>
                <strong>{active.attempts}/{active.maxAttempts}</strong>
              </div>
              <div className="atlas-summary-tile">
                <span>{copy.uploadProgress}</span>
                <strong>{active.progress}%</strong>
              </div>
              <div className="atlas-summary-tile">
                <span>{copy.jobEnqueuedAt}</span>
                <strong>{formatDateTime(active.enqueuedAt)}</strong>
              </div>
              <div className="atlas-summary-tile">
                <span>{copy.jobFinishedAt}</span>
                <strong>{formatDateTime(active.finishedAt)}</strong>
              </div>
            </div>
            <Typography.Title heading={6}>{copy.jobLogsTitle}</Typography.Title>
            {active.logs.map((log, idx) => (
              <Typography.Paragraph key={`${active.id}-${idx}`} type={log.level === "error" ? "danger" : log.level === "warn" ? "warning" : "secondary"}>
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
