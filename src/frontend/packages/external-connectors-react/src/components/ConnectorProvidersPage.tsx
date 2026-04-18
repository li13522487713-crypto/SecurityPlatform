import { useEffect, useState } from 'react';
import type { ConnectorApi } from '../api';
import type { ExternalIdentityProviderListItem } from '../types';
import { ConnectorProviderEditDrawer } from './ConnectorProviderEditDrawer';

export interface ConnectorProvidersPageProps {
  api: ConnectorApi;
  /** 平台外可指定 i18n 词表覆盖。 */
  labels?: Partial<Record<
    | 'title'
    | 'enable'
    | 'disable'
    | 'delete'
    | 'edit'
    | 'add'
    | 'refresh'
    | 'empty'
    | 'statusOn'
    | 'statusOff'
    | 'columnProvider'
    | 'columnCode'
    | 'columnName'
    | 'columnEnabled'
    | 'columnUpdatedAt'
    | 'columnActions'
    | 'confirmDelete',
    string
  >>;
  /** 当用户点击行时触发；宿主可用于跳转到详情页。 */
  onRowClick?: (item: ExternalIdentityProviderListItem) => void;
}

const defaultLabels = {
  title: '外部连接器',
  enable: '启用',
  disable: '停用',
  delete: '删除',
  edit: '编辑',
  add: '新建连接器',
  refresh: '刷新',
  empty: '尚未配置任何外部连接器',
  statusOn: '启用',
  statusOff: '停用',
  columnProvider: 'Provider',
  columnCode: 'Code',
  columnName: 'Display Name',
  columnEnabled: 'Enabled',
  columnUpdatedAt: 'UpdatedAt',
  columnActions: 'Actions',
  confirmDelete: '确定删除该连接器？关联的所有绑定与同步任务将一并失效。',
};

export function ConnectorProvidersPage({ api, labels, onRowClick }: ConnectorProvidersPageProps) {
  const text = { ...defaultLabels, ...labels };
  const [items, setItems] = useState<ExternalIdentityProviderListItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [drawerOpen, setDrawerOpen] = useState(false);
  const [editProviderId, setEditProviderId] = useState<number | null>(null);

  const reload = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await api.listProviders(true);
      setItems(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void reload();
  }, [api]);

  const openCreate = () => {
    setEditProviderId(null);
    setDrawerOpen(true);
  };

  const openEdit = (id: number) => {
    setEditProviderId(id);
    setDrawerOpen(true);
  };

  const onDelete = async (item: ExternalIdentityProviderListItem) => {
    if (!confirm(text.confirmDelete)) {
      return;
    }
    await api.deleteProvider(item.id);
    await reload();
  };

  return (
    <section data-testid="connector-providers-page">
      <header style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
        <h2 style={{ margin: 0 }}>{text.title}</h2>
        <div style={{ display: 'flex', gap: 8 }}>
          <button type="button" onClick={() => void reload()} disabled={loading}>{text.refresh}</button>
          <button type="button" onClick={openCreate}>{text.add}</button>
        </div>
      </header>
      {loading && <p>Loading...</p>}
      {error && <p style={{ color: 'red' }}>{error}</p>}
      {!loading && items.length === 0 && <p>{text.empty}</p>}
      {!loading && items.length > 0 && (
        <table style={{ width: '100%', borderCollapse: 'collapse' }}>
          <thead>
            <tr style={{ borderBottom: '1px solid #ddd' }}>
              <th align="left">{text.columnProvider}</th>
              <th align="left">{text.columnCode}</th>
              <th align="left">{text.columnName}</th>
              <th align="left">{text.columnEnabled}</th>
              <th align="left">{text.columnUpdatedAt}</th>
              <th align="left">{text.columnActions}</th>
            </tr>
          </thead>
          <tbody>
            {items.map((item) => (
              <tr key={item.id} data-testid={`connector-row-${item.id}`} style={{ borderBottom: '1px solid #f0f0f0' }}>
                <td>
                  {onRowClick ? (
                    <button type="button" style={{ background: 'none', border: 'none', color: '#0a66c2', cursor: 'pointer', padding: 0 }} onClick={() => onRowClick(item)}>
                      {item.providerType}
                    </button>
                  ) : (
                    item.providerType
                  )}
                </td>
                <td>{item.code}</td>
                <td>{item.displayName}</td>
                <td>{item.enabled ? text.statusOn : text.statusOff}</td>
                <td>{new Date(item.updatedAt).toLocaleString()}</td>
                <td style={{ whiteSpace: 'nowrap' }}>
                  <button type="button" onClick={() => openEdit(item.id)}>{text.edit}</button>{' '}
                  {item.enabled ? (
                    <button type="button" onClick={() => api.disableProvider(item.id).then(reload)}>{text.disable}</button>
                  ) : (
                    <button type="button" onClick={() => api.enableProvider(item.id).then(reload)}>{text.enable}</button>
                  )}{' '}
                  <button type="button" onClick={() => void onDelete(item)} style={{ color: '#c00' }}>{text.delete}</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      <ConnectorProviderEditDrawer
        api={api}
        open={drawerOpen}
        editProviderId={editProviderId}
        onClose={() => setDrawerOpen(false)}
        onSaved={() => {
          void reload();
        }}
      />
    </section>
  );
}
