import { useEffect, useMemo, useState } from 'react';
import type { ChangeEvent } from 'react';
import type { ConnectorApi } from '../api';
import type { ExternalIdentityBindingListItem, IdentityBindingStatus } from '../types';
import { IdentityBindingConflictCenter } from './IdentityBindingConflictCenter';

export interface ConnectorBindingsPageProps {
  api: ConnectorApi;
  providerId: number;
  labels?: Partial<Record<
    | 'title'
    | 'statusFilter'
    | 'all'
    | 'columnLocalUser'
    | 'columnExternalUser'
    | 'columnStatus'
    | 'columnStrategy'
    | 'columnLastLogin'
    | 'totalSuffix',
    string
  >>;
}

const defaultLabels = {
  title: '身份绑定',
  statusFilter: '状态筛选',
  all: '全部',
  columnLocalUser: '本地用户',
  columnExternalUser: '外部 user id',
  columnStatus: '状态',
  columnStrategy: '匹配策略',
  columnLastLogin: '最后登录',
  totalSuffix: '条',
};

export function ConnectorBindingsPage({ api, providerId, labels }: ConnectorBindingsPageProps) {
  const text = { ...defaultLabels, ...labels };
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
  }, [api, providerId, statusFilter]);

  const conflictCount = useMemo(() => conflicts.length, [conflicts]);

  return (
    <section data-testid="connector-bindings-page">
      <h3>{text.title}（{total}{text.totalSuffix}{conflictCount > 0 ? `，其中 ${conflictCount} 条冲突待处理` : ''}）</h3>
      <div style={{ marginBottom: 8 }}>
        <label>
          {text.statusFilter}：
          <select
            value={statusFilter ?? ''}
            onChange={(e: ChangeEvent<HTMLSelectElement>) => setStatusFilter((e.target.value || undefined) as IdentityBindingStatus | undefined)}
          >
            <option value="">{text.all}</option>
            <option value="Active">Active</option>
            <option value="PendingConfirm">PendingConfirm</option>
            <option value="Conflict">Conflict</option>
            <option value="Revoked">Revoked</option>
          </select>
        </label>
      </div>

      {loading && <p>Loading...</p>}
      {error && <p style={{ color: 'red' }}>{error}</p>}

      {!loading && items.length > 0 && (
        <table style={{ width: '100%', borderCollapse: 'collapse' }}>
          <thead>
            <tr style={{ borderBottom: '1px solid #ddd' }}>
              <th align="left">{text.columnLocalUser}</th>
              <th align="left">{text.columnExternalUser}</th>
              <th align="left">{text.columnStatus}</th>
              <th align="left">{text.columnStrategy}</th>
              <th align="left">{text.columnLastLogin}</th>
            </tr>
          </thead>
          <tbody>
            {items.map((b) => (
              <tr key={b.id} style={{ borderBottom: '1px solid #f0f0f0' }}>
                <td>{b.localUserId}</td>
                <td>{b.externalUserId}</td>
                <td style={{ color: b.status === 'Conflict' ? '#c00' : undefined }}>{b.status}</td>
                <td>{b.matchStrategy}</td>
                <td>{b.lastLoginAt ? new Date(b.lastLoginAt).toLocaleString() : '-'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      <IdentityBindingConflictCenter
        api={api}
        providerId={providerId}
        conflicts={conflicts}
        onResolved={() => void reload()}
      />
    </section>
  );
}
