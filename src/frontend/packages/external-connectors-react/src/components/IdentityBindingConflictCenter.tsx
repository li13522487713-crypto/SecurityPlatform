import { useState } from 'react';
import { Banner, Button, Card, Empty, Input, InputNumber, Modal, Select, Space, Table, Tag, Typography } from '@douyinfe/semi-ui';
import type { ColumnProps } from '@douyinfe/semi-ui/lib/es/table';
import type { ConnectorApi } from '../api';
import type {
  BindingConflictResolution,
  BindingConflictResolutionRequest,
  ExternalIdentityBindingListItem,
  ManualBindingRequest,
} from '../types';

export type IdentityBindingConflictCenterLabelsKey =
  | 'title'
  | 'manualBindHeader'
  | 'localUserId'
  | 'externalUserId'
  | 'mobile'
  | 'email'
  | 'submitManualBind'
  | 'noConflicts'
  | 'columnId'
  | 'columnLocalUser'
  | 'columnExternalUser'
  | 'columnStatus'
  | 'columnAction'
  | 'resolutionLabel'
  | 'resolutionKeepCurrent'
  | 'resolutionSwitchToLocalUser'
  | 'resolutionRevoke'
  | 'newLocalUserIdLabel'
  | 'apply'
  | 'revoke'
  | 'revokeConfirm'
  | 'revokeConfirmTitle'
  | 'requiredFieldsMissing';

export type IdentityBindingConflictCenterLabels = Record<IdentityBindingConflictCenterLabelsKey, string>;

export const IDENTITY_BINDING_CONFLICT_CENTER_LABELS_KEYS = [
  'title',
  'manualBindHeader',
  'localUserId',
  'externalUserId',
  'mobile',
  'email',
  'submitManualBind',
  'noConflicts',
  'columnId',
  'columnLocalUser',
  'columnExternalUser',
  'columnStatus',
  'columnAction',
  'resolutionLabel',
  'resolutionKeepCurrent',
  'resolutionSwitchToLocalUser',
  'resolutionRevoke',
  'newLocalUserIdLabel',
  'apply',
  'revoke',
  'revokeConfirm',
  'revokeConfirmTitle',
  'requiredFieldsMissing',
] as const satisfies readonly IdentityBindingConflictCenterLabelsKey[];

export const defaultIdentityBindingConflictCenterLabels: IdentityBindingConflictCenterLabels = {
  title: 'Identity binding conflict center',
  manualBindHeader: 'Manual binding (rename / switch number / rebind)',
  localUserId: 'Local user ID',
  externalUserId: 'External user id',
  mobile: 'Mobile (optional)',
  email: 'Email (optional)',
  submitManualBind: 'Create manual binding',
  noConflicts: 'No pending conflicts',
  columnId: 'BindingId',
  columnLocalUser: 'Local user',
  columnExternalUser: 'External user id',
  columnStatus: 'Status',
  columnAction: 'Action',
  resolutionLabel: 'Resolution',
  resolutionKeepCurrent: 'Keep current',
  resolutionSwitchToLocalUser: 'Switch to specified local user',
  resolutionRevoke: 'Revoke binding',
  newLocalUserIdLabel: 'New local user ID',
  apply: 'Apply',
  revoke: 'Revoke',
  revokeConfirm: 'Confirm revoking this binding?',
  revokeConfirmTitle: 'Revoke binding',
  requiredFieldsMissing: 'Local user id and external user id are required.',
};

export interface IdentityBindingConflictCenterProps {
  api: ConnectorApi;
  /** Current provider id, used when filling manual binding form. */
  providerId: number;
  /** Conflict bindings (parent already filtered by status=Conflict). */
  conflicts: ExternalIdentityBindingListItem[];
  /** Refresh hook supplied by parent. */
  onResolved: () => void;
  labels: IdentityBindingConflictCenterLabels;
}

/**
 * Identity-binding conflict center: lists status=Conflict bindings, supports
 * "Keep / Switch / Revoke" resolutions and a manual-binding form.
 */
export function IdentityBindingConflictCenter(props: IdentityBindingConflictCenterProps) {
  const { labels } = props;

  const [bindLocalUserId, setBindLocalUserId] = useState<string>('');
  const [bindExternalUserId, setBindExternalUserId] = useState<string>('');
  const [bindMobile, setBindMobile] = useState<string>('');
  const [bindEmail, setBindEmail] = useState<string>('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [resolutions, setResolutions] = useState<
    Record<number, { resolution: BindingConflictResolution; newLocalUserId?: string }>
  >({});

  const onCreateManual = async () => {
    setError(null);
    if (!bindLocalUserId.trim() || !bindExternalUserId.trim()) {
      setError(labels.requiredFieldsMissing);
      return;
    }
    setSubmitting(true);
    try {
      const payload: ManualBindingRequest = {
        providerId: props.providerId,
        localUserId: Number(bindLocalUserId),
        externalUserId: bindExternalUserId.trim(),
        mobile: bindMobile.trim() || undefined,
        email: bindEmail.trim() || undefined,
      };
      await props.api.createManualBinding(payload);
      setBindLocalUserId('');
      setBindExternalUserId('');
      setBindMobile('');
      setBindEmail('');
      props.onResolved();
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setSubmitting(false);
    }
  };

  const applyResolution = async (binding: ExternalIdentityBindingListItem) => {
    const cfg = resolutions[binding.id] ?? { resolution: 'KeepCurrent' as BindingConflictResolution };
    const payload: BindingConflictResolutionRequest = {
      bindingId: binding.id,
      resolution: cfg.resolution,
      newLocalUserId: cfg.newLocalUserId ? Number(cfg.newLocalUserId) : undefined,
    };
    setSubmitting(true);
    setError(null);
    try {
      await props.api.resolveConflict(payload);
      props.onResolved();
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setSubmitting(false);
    }
  };

  const onRevoke = (binding: ExternalIdentityBindingListItem) => {
    Modal.confirm({
      title: labels.revokeConfirmTitle,
      content: labels.revokeConfirm,
      onOk: async () => {
        setSubmitting(true);
        setError(null);
        try {
          await props.api.deleteBinding(binding.id);
          props.onResolved();
        } catch (err) {
          setError(err instanceof Error ? err.message : String(err));
        } finally {
          setSubmitting(false);
        }
      },
    });
  };

  const columns: ColumnProps<ExternalIdentityBindingListItem & { __key: number }>[] = [
    { title: labels.columnId, dataIndex: 'id', width: 100 },
    { title: labels.columnLocalUser, dataIndex: 'localUserId', width: 120 },
    { title: labels.columnExternalUser, dataIndex: 'externalUserId' },
    {
      title: labels.columnStatus,
      dataIndex: 'status',
      width: 120,
      render: (_, record) => <Tag color="red">{record.status}</Tag>,
    },
    {
      title: labels.columnAction,
      dataIndex: '__action',
      render: (_, record) => {
        const cfg = resolutions[record.id] ?? { resolution: 'KeepCurrent' as BindingConflictResolution };
        return (
          <Space>
            <Select
              size="small"
              value={cfg.resolution}
              style={{ width: 200 }}
              onChange={(v) =>
                setResolutions((prev) => ({
                  ...prev,
                  [record.id]: { ...cfg, resolution: v as BindingConflictResolution },
                }))
              }
              optionList={[
                { value: 'KeepCurrent', label: labels.resolutionKeepCurrent },
                { value: 'SwitchToLocalUser', label: labels.resolutionSwitchToLocalUser },
                { value: 'Revoke', label: labels.resolutionRevoke },
              ]}
            />
            {cfg.resolution === 'SwitchToLocalUser' && (
              <Input
                size="small"
                placeholder={labels.newLocalUserIdLabel}
                value={cfg.newLocalUserId ?? ''}
                onChange={(v) =>
                  setResolutions((prev) => ({
                    ...prev,
                    [record.id]: { ...cfg, newLocalUserId: v },
                  }))
                }
                style={{ width: 140 }}
              />
            )}
            <Button size="small" type="primary" disabled={submitting} onClick={() => void applyResolution(record)}>
              {labels.apply}
            </Button>
            <Button size="small" type="danger" theme="borderless" disabled={submitting} onClick={() => onRevoke(record)}>
              {labels.revoke}
            </Button>
          </Space>
        );
      },
    },
  ];

  const dataSource = props.conflicts.map((b) => ({ ...b, __key: b.id }));

  return (
    <section data-testid="identity-binding-conflict-center" style={{ marginTop: 16 }}>
      <Typography.Title heading={5} style={{ marginBottom: 12 }}>
        {labels.title}
      </Typography.Title>

      <Card title={labels.manualBindHeader} style={{ marginBottom: 16 }} bordered>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', columnGap: 12, rowGap: 8 }}>
          <label>
            <Typography.Text type="tertiary" size="small" style={{ display: 'block' }}>
              {labels.localUserId}
            </Typography.Text>
            <InputNumber
              value={bindLocalUserId === '' ? undefined : Number(bindLocalUserId)}
              onChange={(v) => setBindLocalUserId(v === undefined || v === '' ? '' : String(v))}
              style={{ width: '100%' }}
            />
          </label>
          <label>
            <Typography.Text type="tertiary" size="small" style={{ display: 'block' }}>
              {labels.externalUserId}
            </Typography.Text>
            <Input value={bindExternalUserId} onChange={setBindExternalUserId} />
          </label>
          <label>
            <Typography.Text type="tertiary" size="small" style={{ display: 'block' }}>
              {labels.mobile}
            </Typography.Text>
            <Input value={bindMobile} onChange={setBindMobile} />
          </label>
          <label>
            <Typography.Text type="tertiary" size="small" style={{ display: 'block' }}>
              {labels.email}
            </Typography.Text>
            <Input value={bindEmail} onChange={setBindEmail} type="email" />
          </label>
        </div>
        <Button type="primary" disabled={submitting} onClick={() => void onCreateManual()} style={{ marginTop: 12 }}>
          {labels.submitManualBind}
        </Button>
      </Card>

      {error && (
        <Banner type="danger" fullMode={false} description={error} closeIcon={null} style={{ marginBottom: 12 }} />
      )}

      {props.conflicts.length === 0 ? (
        <Empty description={labels.noConflicts} />
      ) : (
        <Table rowKey="__key" columns={columns} dataSource={dataSource} pagination={false} size="small" />
      )}
    </section>
  );
}
