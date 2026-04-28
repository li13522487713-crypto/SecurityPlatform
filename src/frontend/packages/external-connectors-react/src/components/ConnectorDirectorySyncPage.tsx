import { Fragment, useEffect, useState } from 'react';
import { Banner, Button, Card, Input, Select, Space, Spin, Table, Tag, Typography } from '@douyinfe/semi-ui';
import type { ColumnProps } from '@douyinfe/semi-ui/lib/es/table';
import type { ConnectorApi } from '../api';
import type {
  ExternalDirectorySyncDiffItem,
  ExternalDirectorySyncIncrementalRequest,
  ExternalDirectorySyncJobResponse,
} from '../types';

export type ConnectorDirectorySyncPageLabelsKey =
  | 'title'
  | 'fullSync'
  | 'incrementalSync'
  | 'syncing'
  | 'jobsHeader'
  | 'diffsHeader'
  | 'retry'
  | 'expandDiffs'
  | 'collapseDiffs'
  | 'noDiffs'
  | 'incrementalKind'
  | 'incrementalEntityId'
  | 'incrementalSubmit'
  | 'incrementalEntityIdRequired'
  | 'incrementalEntityIdPlaceholder'
  | 'jobsColumnJobId'
  | 'jobsColumnMode'
  | 'jobsColumnStatus'
  | 'jobsColumnTriggerSource'
  | 'jobsColumnUserStats'
  | 'jobsColumnDepartmentStats'
  | 'jobsColumnStartedAt'
  | 'jobsColumnFinishedAt'
  | 'jobsColumnAction'
  | 'diffsColumnDiffId'
  | 'diffsColumnType'
  | 'diffsColumnEntityId'
  | 'diffsColumnSummary'
  | 'diffsColumnOccurredAt'
  | 'loadingText'
  | 'loadingDiffsText'
  | 'dashPlaceholder';

export type ConnectorDirectorySyncPageLabels = Record<ConnectorDirectorySyncPageLabelsKey, string>;

export const CONNECTOR_DIRECTORY_SYNC_PAGE_LABELS_KEYS = [
  'title',
  'fullSync',
  'incrementalSync',
  'syncing',
  'jobsHeader',
  'diffsHeader',
  'retry',
  'expandDiffs',
  'collapseDiffs',
  'noDiffs',
  'incrementalKind',
  'incrementalEntityId',
  'incrementalSubmit',
  'incrementalEntityIdRequired',
  'incrementalEntityIdPlaceholder',
  'jobsColumnJobId',
  'jobsColumnMode',
  'jobsColumnStatus',
  'jobsColumnTriggerSource',
  'jobsColumnUserStats',
  'jobsColumnDepartmentStats',
  'jobsColumnStartedAt',
  'jobsColumnFinishedAt',
  'jobsColumnAction',
  'diffsColumnDiffId',
  'diffsColumnType',
  'diffsColumnEntityId',
  'diffsColumnSummary',
  'diffsColumnOccurredAt',
  'loadingText',
  'loadingDiffsText',
  'dashPlaceholder',
] as const satisfies readonly ConnectorDirectorySyncPageLabelsKey[];

export const defaultConnectorDirectorySyncPageLabels: ConnectorDirectorySyncPageLabels = {
  title: 'Directory sync reconciliation',
  fullSync: 'Run full sync now',
  incrementalSync: 'Apply incremental event',
  syncing: 'Syncing...',
  jobsHeader: 'Recent sync jobs',
  diffsHeader: 'Diff rows (click Retry to re-deliver as incremental event)',
  retry: 'Retry',
  expandDiffs: 'Show diffs',
  collapseDiffs: 'Hide diffs',
  noDiffs: 'No diff rows',
  incrementalKind: 'Event type',
  incrementalEntityId: 'Entity ID',
  incrementalSubmit: 'Apply',
  incrementalEntityIdRequired: 'Entity ID is required.',
  incrementalEntityIdPlaceholder: 'zhangsan / dept-100',
  jobsColumnJobId: 'JobId',
  jobsColumnMode: 'Mode',
  jobsColumnStatus: 'Status',
  jobsColumnTriggerSource: 'Trigger',
  jobsColumnUserStats: 'User add/upd/del',
  jobsColumnDepartmentStats: 'Dept add/upd/del',
  jobsColumnStartedAt: 'Started',
  jobsColumnFinishedAt: 'Finished',
  jobsColumnAction: 'Action',
  diffsColumnDiffId: 'DiffId',
  diffsColumnType: 'Type',
  diffsColumnEntityId: 'Entity ID',
  diffsColumnSummary: 'Summary / error',
  diffsColumnOccurredAt: 'Time',
  loadingText: 'Loading...',
  loadingDiffsText: 'Loading diffs...',
  dashPlaceholder: '-',
};

export interface ConnectorDirectorySyncPageProps {
  api: ConnectorApi;
  providerId: number;
  /** Default provider type string used when sending incremental events. */
  providerTypeHint?: string;
  labels: ConnectorDirectorySyncPageLabels;
}

/**
 * Directory sync reconciliation page: full-sync, manual incremental events, jobs list,
 * per-job diffs panel, and retry of failed diff rows.
 */
export function ConnectorDirectorySyncPage({ api, providerId, providerTypeHint, labels }: ConnectorDirectorySyncPageProps) {
  const [jobs, setJobs] = useState<ExternalDirectorySyncJobResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [expandedJobId, setExpandedJobId] = useState<number | null>(null);
  const [diffs, setDiffs] = useState<ExternalDirectorySyncDiffItem[]>([]);
  const [diffLoading, setDiffLoading] = useState(false);

  const [incKind, setIncKind] = useState<ExternalDirectorySyncIncrementalRequest['kind']>('UserUpdated');
  const [incEntityId, setIncEntityId] = useState('');

  const refresh = async () => {
    setLoading(true);
    setError(null);
    try {
      const recent = await api.listSyncJobs(providerId, 20);
      setJobs(recent);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void refresh();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [api, providerId]);

  const triggerFull = async () => {
    setBusy(true);
    setError(null);
    try {
      await api.runFullSync(providerId);
      await refresh();
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setBusy(false);
    }
  };

  const triggerIncremental = async () => {
    if (!incEntityId.trim()) {
      setError(labels.incrementalEntityIdRequired);
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const payload: ExternalDirectorySyncIncrementalRequest = {
        providerType: providerTypeHint ?? '',
        kind: incKind,
        entityId: incEntityId.trim(),
      };
      await api.applyIncrementalSync(providerId, payload);
      setIncEntityId('');
      await refresh();
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setBusy(false);
    }
  };

  const toggleDiffs = async (jobId: number) => {
    if (expandedJobId === jobId) {
      setExpandedJobId(null);
      setDiffs([]);
      return;
    }
    setExpandedJobId(jobId);
    setDiffLoading(true);
    try {
      const data = await api.listSyncDiffs(providerId, jobId, 1, 100);
      setDiffs(data.items ?? []);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setDiffLoading(false);
    }
  };

  const retryDiff = async (diff: ExternalDirectorySyncDiffItem) => {
    setBusy(true);
    setError(null);
    try {
      const payload: ExternalDirectorySyncIncrementalRequest = {
        providerType: providerTypeHint ?? '',
        kind: mapDiffTypeToKind(diff.diffType),
        entityId: diff.entityId,
      };
      await api.applyIncrementalSync(providerId, payload);
      if (expandedJobId !== null) {
        const data = await api.listSyncDiffs(providerId, expandedJobId, 1, 100);
        setDiffs(data.items ?? []);
      }
      await refresh();
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setBusy(false);
    }
  };

  const jobsColumns: ColumnProps<ExternalDirectorySyncJobResponse & { __key: number }>[] = [
    { title: labels.jobsColumnJobId, dataIndex: 'id', width: 80 },
    { title: labels.jobsColumnMode, dataIndex: 'mode', width: 100 },
    {
      title: labels.jobsColumnStatus,
      dataIndex: 'status',
      width: 140,
      render: (_, record) => (
        <Tag color={record.status === 'Failed' ? 'red' : record.status === 'Succeeded' ? 'green' : 'amber'}>
          {record.status}
        </Tag>
      ),
    },
    { title: labels.jobsColumnTriggerSource, dataIndex: 'triggerSource', width: 120 },
    {
      title: labels.jobsColumnUserStats,
      dataIndex: '__userStats',
      align: 'right',
      width: 130,
      render: (_, record) => `${record.userCreated} / ${record.userUpdated} / ${record.userDeleted}`,
    },
    {
      title: labels.jobsColumnDepartmentStats,
      dataIndex: '__deptStats',
      align: 'right',
      width: 130,
      render: (_, record) => `${record.departmentCreated} / ${record.departmentUpdated} / ${record.departmentDeleted}`,
    },
    {
      title: labels.jobsColumnStartedAt,
      dataIndex: 'startedAt',
      width: 180,
      render: (_, record) => new Date(record.startedAt).toLocaleString(),
    },
    {
      title: labels.jobsColumnFinishedAt,
      dataIndex: 'finishedAt',
      width: 180,
      render: (_, record) => (record.finishedAt ? new Date(record.finishedAt).toLocaleString() : labels.dashPlaceholder),
    },
    {
      title: labels.jobsColumnAction,
      dataIndex: '__action',
      width: 120,
      render: (_, record) => (
        <Button size="small" onClick={() => void toggleDiffs(record.id)}>
          {expandedJobId === record.id ? labels.collapseDiffs : labels.expandDiffs}
        </Button>
      ),
    },
  ];

  const diffsColumns: ColumnProps<ExternalDirectorySyncDiffItem & { __key: number }>[] = [
    { title: labels.diffsColumnDiffId, dataIndex: 'id', width: 90 },
    {
      title: labels.diffsColumnType,
      dataIndex: 'diffType',
      width: 160,
      render: (_, record) => (
        <Tag color={record.diffType === 'Failed' ? 'red' : 'blue'}>{record.diffType}</Tag>
      ),
    },
    { title: labels.diffsColumnEntityId, dataIndex: 'entityId', width: 200 },
    {
      title: labels.diffsColumnSummary,
      dataIndex: '__summary',
      render: (_, record) => record.errorMessage ?? record.summary ?? labels.dashPlaceholder,
    },
    {
      title: labels.diffsColumnOccurredAt,
      dataIndex: 'occurredAt',
      width: 180,
      render: (_, record) => new Date(record.occurredAt).toLocaleString(),
    },
    {
      title: '',
      dataIndex: '__retry',
      width: 90,
      render: (_, record) =>
        record.diffType === 'Failed' ? (
          <Button size="small" type="primary" disabled={busy} onClick={() => void retryDiff(record)}>
            {labels.retry}
          </Button>
        ) : null,
    },
  ];

  return (
    <section data-testid="connector-directory-sync-page">
      <Space spacing="medium" style={{ width: '100%', justifyContent: 'space-between', marginBottom: 12 }}>
        <Typography.Title heading={5} style={{ margin: 0 }}>
          {labels.title}
        </Typography.Title>
        <Button type="primary" loading={busy} onClick={() => void triggerFull()}>
          {busy ? labels.syncing : labels.fullSync}
        </Button>
      </Space>

      <Card title={labels.incrementalSync} style={{ marginBottom: 12 }}>
        <Space wrap>
          <Space>
            <Typography.Text>{labels.incrementalKind}:</Typography.Text>
            <Select
              value={incKind}
              style={{ width: 220 }}
              onChange={(v) => setIncKind(v as ExternalDirectorySyncIncrementalRequest['kind'])}
              optionList={[
                { value: 'UserCreated', label: 'UserCreated' },
                { value: 'UserUpdated', label: 'UserUpdated' },
                { value: 'UserDeleted', label: 'UserDeleted' },
                { value: 'DepartmentCreated', label: 'DepartmentCreated' },
                { value: 'DepartmentUpdated', label: 'DepartmentUpdated' },
                { value: 'DepartmentDeleted', label: 'DepartmentDeleted' },
              ]}
            />
          </Space>
          <Space>
            <Typography.Text>{labels.incrementalEntityId}:</Typography.Text>
            <Input
              value={incEntityId}
              onChange={setIncEntityId}
              placeholder={labels.incrementalEntityIdPlaceholder}
              style={{ width: 240 }}
            />
          </Space>
          <Button type="primary" disabled={busy} onClick={() => void triggerIncremental()}>
            {labels.incrementalSubmit}
          </Button>
        </Space>
      </Card>

      {loading && <Spin tip={labels.loadingText} />}
      {error && <Banner type="danger" fullMode={false} description={error} closeIcon={null} style={{ marginBottom: 12 }} />}

      {!loading && jobs.length > 0 && (
        <>
          <Typography.Title heading={6} style={{ marginTop: 16 }}>
            {labels.jobsHeader}
          </Typography.Title>
          <Table
            rowKey="__key"
            size="small"
            pagination={false}
            columns={jobsColumns}
            dataSource={jobs.map((j) => ({ ...j, __key: j.id }))}
            expandedRowRender={(record) =>
              expandedJobId === record.id ? (
                <Fragment>
                  <Typography.Text strong style={{ display: 'block', marginBottom: 8 }}>
                    {labels.diffsHeader}
                  </Typography.Text>
                  {diffLoading && <Spin tip={labels.loadingDiffsText} />}
                  {!diffLoading && diffs.length === 0 && (
                    <Typography.Text type="tertiary">{labels.noDiffs}</Typography.Text>
                  )}
                  {!diffLoading && diffs.length > 0 && (
                    <Table
                      rowKey="__key"
                      size="small"
                      pagination={false}
                      columns={diffsColumns}
                      dataSource={diffs.map((d) => ({ ...d, __key: d.id }))}
                    />
                  )}
                </Fragment>
              ) : null
            }
            expandedRowKeys={expandedJobId !== null ? [expandedJobId] : []}
          />
        </>
      )}
    </section>
  );
}

function mapDiffTypeToKind(diffType: string): ExternalDirectorySyncIncrementalRequest['kind'] {
  switch (diffType) {
    case 'UserCreated':
    case 'UserUpdated':
    case 'UserDeleted':
    case 'DepartmentCreated':
    case 'DepartmentUpdated':
    case 'DepartmentDeleted':
      return diffType;
    default:
      return 'UserUpdated';
  }
}
