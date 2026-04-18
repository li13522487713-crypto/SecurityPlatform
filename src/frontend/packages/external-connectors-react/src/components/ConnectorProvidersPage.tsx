import { useEffect, useState } from 'react';
import type { ConnectorApi } from '../api';
import type { ExternalIdentityProviderListItem } from '../types';

export interface ConnectorProvidersPageProps {
  api: ConnectorApi;
  /** 平台外可指定 i18n 词表覆盖。 */
  labels?: Partial<Record<'title' | 'enable' | 'disable' | 'delete' | 'add' | 'empty' | 'statusOn' | 'statusOff', string>>;
}

const defaultLabels = {
  title: '外部连接器',
  enable: '启用',
  disable: '停用',
  delete: '删除',
  add: '新建连接器',
  empty: '尚未配置任何外部连接器',
  statusOn: '启用',
  statusOff: '停用',
};

export function ConnectorProvidersPage({ api, labels }: ConnectorProvidersPageProps) {
  const text = { ...defaultLabels, ...labels };
  const [items, setItems] = useState<ExternalIdentityProviderListItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

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

  return (
    <section data-testid="connector-providers-page">
      <header style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
        <h2>{text.title}</h2>
      </header>
      {loading && <p>Loading...</p>}
      {error && <p style={{ color: 'red' }}>{error}</p>}
      {!loading && items.length === 0 && <p>{text.empty}</p>}
      {!loading && items.length > 0 && (
        <table style={{ width: '100%', borderCollapse: 'collapse' }}>
          <thead>
            <tr>
              <th align="left">Provider</th>
              <th align="left">Code</th>
              <th align="left">Display Name</th>
              <th align="left">Enabled</th>
              <th align="left">UpdatedAt</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {items.map((item) => (
              <tr key={item.id} data-testid={`connector-row-${item.id}`}>
                <td>{item.providerType}</td>
                <td>{item.code}</td>
                <td>{item.displayName}</td>
                <td>{item.enabled ? text.statusOn : text.statusOff}</td>
                <td>{new Date(item.updatedAt).toLocaleString()}</td>
                <td>
                  {item.enabled ? (
                    <button type="button" onClick={() => api.disableProvider(item.id).then(reload)}>{text.disable}</button>
                  ) : (
                    <button type="button" onClick={() => api.enableProvider(item.id).then(reload)}>{text.enable}</button>
                  )}{' '}
                  <button type="button" onClick={() => api.deleteProvider(item.id).then(reload)}>{text.delete}</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}
