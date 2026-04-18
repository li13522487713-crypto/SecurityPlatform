import { useEffect, useMemo, useState } from 'react';
import { Banner, Select, Space, Spin, Table, Tag, Typography } from '@douyinfe/semi-ui';
import type { ColumnProps } from '@douyinfe/semi-ui/lib/es/table';
import type { ConnectorApi } from '../api';
import type { ExternalIdentityBindingListItem, IdentityBindingStatus } from '../types';
import {
  IdentityBindingConflictCenter,
  defaultIdentityBindingConflictCenterLabels,
  type IdentityBindingConflictCenterLabels,
} from './IdentityBindingConflictCenter';

export type ConnectorBindingsPageLabelsKey =
  | 'title'
  | 'statusFilter'
  | 'all'
  | 'columnLocalUser'
  | 'columnExternalUser'
  | 'columnStatus'
  | 'columnStrategy'
  | 'columnLastLogin'
  | 'totalSuffix'
  | 'conflictsHint'
  | 'loadingText'
  | 'dashPlaceholder';

export type ConnectorBindingsPageLabels = Record<ConnectorBindingsPageLabelsKey, string>;

export const CONNECTOR_BINDINGS_PAGE_LABELS_KEYS = [
  'title',
  'statusFilter',
  'all',
  'columnLocalUser',
  'columnExternalUser',
  'columnStatus',
  'columnStrategy',
  'columnLastLogin',
  'totalSuffix',
  'conflictsHint',
  'loadingText',
  'dashPlaceholder',
] as const satisfies readonly ConnectorBindingsPageLabelsKey[];

export const defaultConnectorBindingsPageLabels: ConnectorBindingsPageLabels = {
  title: 'Identity bindings',
  statusFilter: 'Status filter',
  all: 'All',
  columnLocalUser: 'Local user',
  columnExternalUser: 'External user id',
  columnStatus: 'Status',
  columnStrategy: 'Match strategy',
  columnLastLogin: 'Last login',
  totalSuffix: 'records',
  conflictsHint: '{count} pending conflicts',
  loadingText: 'Loading...',
  dashPlaceholder: '-',
};

export interface ConnectorBindingsPageProps {
  api: ConnectorApi;
  providerId: number;
  labels: ConnectorBindingsPageLabels;
  conflictCenterLabels: IdentityBindingConflictCenterLabels;
}

const STATUS_TAG_COLORS: Record<IdentityBindingStatus, 'green' | 'amber' | 'red' | 'grey'> = {
  Active: 'green',
  PendingConfirm: 'amber',
  Conflict: 'red',
  Revoked: 'grey',
};

export function ConnectorBindingsPage({ api, providerId, labels, conflictCenterLabels }: ConnectorBindingsPageProps) {
  const [items, setItems] = useState<ExternalIdentityBindingListItem[]>([]);
  const [conflicts, setConflicts] = useState<ExternalIdentityBindingListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [statusFilter, setStatusFilter] = useState<IdentityBindingStatus | undefined>(undefined);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const reload = async () => {
    setLoading(true);
    setError(null);
    try {
      const [paged, conflictsPage] = await Promise.all([
        api.listBindings(providerId, statusFilter),
        api.listBindings(providerId, 'Conflict', 1, 100),
      ]);
      setItems(paged.items);
      setTotal(paged.total);
      setConflicts(conflictsPage.items);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void reload();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [api, providerId, statusFilter]);

  const conflictCount = useMemo(() => conflicts.length, [conflicts]);
  const conflictHint = conflictCount > 0
    ? `, ${labels.conflictsHint.replace('{count}', String(conflictCount))}`
    : '';

  const columns: ColumnProps<ExternalIdentityBindingListItem & { __key: number }>[] = [
    { title: labels.columnLocalUser, dataIndex: 'localUserId', width: 120 },
    { title: labels.columnExternalUser, dataIndex: 'externalUserId' },
    {
      title: labels.columnStatus,
      dataIndex: 'status',
      width: 140,
      render: (_, record) => <Tag color={STATUS_TAG_COLORS[record.status] ?? 'grey'}>{record.status}</Tag>,
    },
    { title: labels.columnStrategy, dataIndex: 'matchStrategy', width: 140 },
    {
      title: labels.columnLastLogin,
      dataIndex: 'lastLoginAt',
      width: 200,
      render: (_, record) =>
        record.lastLoginAt ? new Date(record.lastLoginAt).toLocaleString() : labels.dashPlaceholder,
    },
  ];

  const dataSource = items.map((b) => ({ ...b, __key: b.id }));
  const conflictCenterEffectiveLabels = conflictCenterLabels ?? defaultIdentityBindingConflictCenterLabels;

  return (
    <section data-testid="connector-bindings-page">
      <Typography.Title heading={4}>
        {labels.title} ({total} {labels.totalSuffix}{conflictHint})
      </Typography.Title>

      <Space style={{ marginBottom: 12 }}>
        <Typography.Text>{labels.statusFilter}:</Typography.Text>
        <Select
          style={{ width: 180 }}
          value={statusFilter ?? ''}
          onChange={(v) => {
            const value = v as string;
            setStatusFilter(value === '' ? undefined : (value as IdentityBindingStatus));
          }}
          optionList={[
            { value: '', label: labels.all },
            { value: 'Active', label: 'Active' },
            { value: 'PendingConfirm', label: 'PendingConfirm' },
            { value: 'Conflict', label: 'Conflict' },
            { value: 'Revoked', label: 'Revoked' },
          ]}
        />
      </Space>

      {loading && <Spin tip={labels.loadingText} />}
      {error && <Banner type="danger" fullMode={false} description={error} closeIcon={null} />}

      {!loading && items.length > 0 && (
        <Table rowKey="__key" columns={columns} dataSource={dataSource} pagination={false} size="small" />
      )}

      <IdentityBindingConflictCenter
        api={api}
        providerId={providerId}
        conflicts={conflicts}
        onResolved={() => void reload()}
        labels={conflictCenterEffectiveLabels}
      />
    </section>
  );
}
