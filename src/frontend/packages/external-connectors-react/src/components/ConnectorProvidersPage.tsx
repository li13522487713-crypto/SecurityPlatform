import { useEffect, useState } from 'react';
import { Banner, Button, Empty, Modal, Space, Spin, Table, Tag, Typography } from '@douyinfe/semi-ui';
import type { ColumnProps } from '@douyinfe/semi-ui/lib/es/table';
import type { ConnectorApi } from '../api';
import type { ExternalIdentityProviderListItem } from '../types';
import {
  ConnectorProviderEditDrawer,
  defaultConnectorProviderEditDrawerLabels,
  type ConnectorProviderEditDrawerLabels,
} from './ConnectorProviderEditDrawer';
import {
  defaultConnectorOAuthConfigFormLabels,
  type ConnectorOAuthConfigFormLabels,
} from './ConnectorOAuthConfigForm';

export type ConnectorProvidersPageLabelsKey =
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
  | 'confirmDelete'
  | 'confirmDeleteTitle'
  | 'loadingText';

export type ConnectorProvidersPageLabels = Record<ConnectorProvidersPageLabelsKey, string>;

export const CONNECTOR_PROVIDERS_PAGE_LABELS_KEYS = [
  'title',
  'enable',
  'disable',
  'delete',
  'edit',
  'add',
  'refresh',
  'empty',
  'statusOn',
  'statusOff',
  'columnProvider',
  'columnCode',
  'columnName',
  'columnEnabled',
  'columnUpdatedAt',
  'columnActions',
  'confirmDelete',
  'confirmDeleteTitle',
  'loadingText',
] as const satisfies readonly ConnectorProvidersPageLabelsKey[];

export const defaultConnectorProvidersPageLabels: ConnectorProvidersPageLabels = {
  title: 'External connectors',
  enable: 'Enable',
  disable: 'Disable',
  delete: 'Delete',
  edit: 'Edit',
  add: 'Add connector',
  refresh: 'Refresh',
  empty: 'No external connectors configured yet',
  statusOn: 'On',
  statusOff: 'Off',
  columnProvider: 'Provider',
  columnCode: 'Code',
  columnName: 'Display name',
  columnEnabled: 'Enabled',
  columnUpdatedAt: 'Updated at',
  columnActions: 'Actions',
  confirmDelete: 'Delete this connector? All bindings and sync jobs will be invalidated.',
  confirmDeleteTitle: 'Delete connector',
  loadingText: 'Loading...',
};

export interface ConnectorProvidersPageProps {
  api: ConnectorApi;
  labels: ConnectorProvidersPageLabels;
  /** Triggered when a row is clicked; host can navigate to detail page. */
  onRowClick?: (item: ExternalIdentityProviderListItem) => void;
  drawerLabels: ConnectorProviderEditDrawerLabels;
  oauthFormLabels: ConnectorOAuthConfigFormLabels;
}

export function ConnectorProvidersPage({
  api,
  labels,
  onRowClick,
  drawerLabels,
  oauthFormLabels,
}: ConnectorProvidersPageProps) {
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
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [api]);

  const openCreate = () => {
    setEditProviderId(null);
    setDrawerOpen(true);
  };

  const openEdit = (id: number) => {
    setEditProviderId(id);
    setDrawerOpen(true);
  };

  const onDelete = (item: ExternalIdentityProviderListItem) => {
    Modal.confirm({
      title: labels.confirmDeleteTitle,
      content: labels.confirmDelete,
      onOk: async () => {
        await api.deleteProvider(item.id);
        await reload();
      },
    });
  };

  const drawerEffectiveLabels = drawerLabels ?? defaultConnectorProviderEditDrawerLabels;
  const oauthEffectiveLabels = oauthFormLabels ?? defaultConnectorOAuthConfigFormLabels;

  const columns: ColumnProps<ExternalIdentityProviderListItem & { __key: number }>[] = [
    {
      title: labels.columnProvider,
      dataIndex: 'providerType',
      width: 140,
      render: (_, record) =>
        onRowClick ? (
          <Button theme="borderless" type="primary" onClick={() => onRowClick(record)}>
            {record.providerType}
          </Button>
        ) : (
          <Typography.Text>{record.providerType}</Typography.Text>
        ),
    },
    { title: labels.columnCode, dataIndex: 'code', width: 200 },
    { title: labels.columnName, dataIndex: 'displayName' },
    {
      title: labels.columnEnabled,
      dataIndex: 'enabled',
      width: 100,
      render: (_, record) => <Tag color={record.enabled ? 'green' : 'grey'}>{record.enabled ? labels.statusOn : labels.statusOff}</Tag>,
    },
    {
      title: labels.columnUpdatedAt,
      dataIndex: 'updatedAt',
      width: 200,
      render: (_, record) => new Date(record.updatedAt).toLocaleString(),
    },
    {
      title: labels.columnActions,
      dataIndex: '__actions',
      width: 280,
      render: (_, record) => (
        <Space>
          <Button size="small" onClick={() => openEdit(record.id)}>
            {labels.edit}
          </Button>
          {record.enabled ? (
            <Button size="small" onClick={() => api.disableProvider(record.id).then(reload)}>
              {labels.disable}
            </Button>
          ) : (
            <Button size="small" type="primary" onClick={() => api.enableProvider(record.id).then(reload)}>
              {labels.enable}
            </Button>
          )}
          <Button size="small" type="danger" theme="borderless" onClick={() => onDelete(record)}>
            {labels.delete}
          </Button>
        </Space>
      ),
    },
  ];

  const dataSource = items.map((item) => ({ ...item, __key: item.id }));

  return (
    <section data-testid="connector-providers-page">
      <Space spacing="medium" style={{ width: '100%', justifyContent: 'space-between', marginBottom: 12 }}>
        <Typography.Title heading={4} style={{ margin: 0 }}>
          {labels.title}
        </Typography.Title>
        <Space>
          <Button onClick={() => void reload()} disabled={loading}>
            {labels.refresh}
          </Button>
          <Button type="primary" onClick={openCreate}>
            {labels.add}
          </Button>
        </Space>
      </Space>

      {loading && <Spin tip={labels.loadingText} />}
      {error && <Banner type="danger" fullMode={false} description={error} closeIcon={null} />}
      {!loading && items.length === 0 && <Empty description={labels.empty} />}
      {!loading && items.length > 0 && (
        <Table rowKey="__key" columns={columns} dataSource={dataSource} pagination={false} size="small" />
      )}

      <ConnectorProviderEditDrawer
        api={api}
        open={drawerOpen}
        editProviderId={editProviderId}
        onClose={() => setDrawerOpen(false)}
        onSaved={() => {
          void reload();
        }}
        labels={drawerEffectiveLabels}
        oauthFormLabels={oauthEffectiveLabels}
      />
    </section>
  );
}
