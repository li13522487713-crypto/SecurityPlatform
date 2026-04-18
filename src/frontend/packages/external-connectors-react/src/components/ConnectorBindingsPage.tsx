import { useEffect, useState } from 'react';
import type { ConnectorApi } from '../api';
import type { ExternalIdentityBindingListItem, IdentityBindingStatus } from '../types';

export interface ConnectorBindingsPageProps {
  api: ConnectorApi;
  providerId: number;
}

export function ConnectorBindingsPage({ api, providerId }: ConnectorBindingsPageProps) {
  const [items, setItems] = useState<ExternalIdentityBindingListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [statusFilter, setStatusFilter] = useState<IdentityBindingStatus | undefined>(undefined);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    setLoading(true);
    setError(null);
    api
      .listBindings(providerId, statusFilter)
      .then((paged) => {
        if (!active) return;
        setItems(paged.items);
        setTotal(paged.total);
      })
      .catch((err: unknown) => {
        if (!active) return;
        setError(err instanceof Error ? err.message : String(err));
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, [api, providerId, statusFilter]);

  return (
    <section data-testid="connector-bindings-page">
      <h3>身份绑定（{total}）</h3>
      <div style={{ marginBottom: 8 }}>
        <label>
          状态筛选：
          <select value={statusFilter ?? ''} onChange={(e) => setStatusFilter((e.target.value || undefined) as IdentityBindingStatus | undefined)}>
            <option value="">全部</option>
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
            <tr>
              <th align="left">本地用户</th>
              <th align="left">外部 user id</th>
              <th align="left">状态</th>
              <th align="left">匹配策略</th>
              <th align="left">最后登录</th>
            </tr>
          </thead>
          <tbody>
            {items.map((b) => (
              <tr key={b.id}>
                <td>{b.localUserId}</td>
                <td>{b.externalUserId}</td>
                <td>{b.status}</td>
                <td>{b.matchStrategy}</td>
                <td>{b.lastLoginAt ? new Date(b.lastLoginAt).toLocaleString() : '-'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}
