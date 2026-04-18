import { useState } from 'react';
import type { ConnectorApi } from '../api';
import type {
  BindingConflictResolution,
  BindingConflictResolutionRequest,
  ExternalIdentityBindingListItem,
  ManualBindingRequest,
} from '../types';

export interface IdentityBindingConflictCenterProps {
  api: ConnectorApi;
  /** 当前 provider id，用于手动绑定时填充。 */
  providerId: number;
  /** Conflict 状态的绑定列表（由父组件按 status=Conflict 过滤后传入）。 */
  conflicts: ExternalIdentityBindingListItem[];
  /** 父组件的刷新钩子。 */
  onResolved: () => void;
  labels?: Partial<Record<
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
    | 'revokeConfirm',
    string
  >>;
}

const defaultLabels = {
  title: '身份绑定冲突中心',
  manualBindHeader: '手动绑定（重名 / 换号 / 重绑）',
  localUserId: '本地用户 ID',
  externalUserId: '外部 user id',
  mobile: '手机号（可选）',
  email: '邮箱（可选）',
  submitManualBind: '创建手动绑定',
  noConflicts: '当前没有需要处理的冲突',
  columnId: 'BindingId',
  columnLocalUser: '本地用户',
  columnExternalUser: '外部 user id',
  columnStatus: '状态',
  columnAction: '处理',
  resolutionLabel: '解决方式',
  resolutionKeepCurrent: '保留当前',
  resolutionSwitchToLocalUser: '切换到指定本地用户',
  resolutionRevoke: '撤销绑定',
  newLocalUserIdLabel: '新本地用户 ID',
  apply: '应用',
  revoke: '撤销',
  revokeConfirm: '确认撤销该绑定？',
};

/**
 * 身份绑定冲突中心：展示 status=Conflict 的绑定，支持「保留 / 换号 / 撤销」三种解决方式 + 手动绑定。
 * 配合 ConnectorBindingsPage 使用。
 */
export function IdentityBindingConflictCenter(props: IdentityBindingConflictCenterProps) {
  const text = { ...defaultLabels, ...props.labels };

  // 手动绑定子表单
  const [bindLocalUserId, setBindLocalUserId] = useState<string>('');
  const [bindExternalUserId, setBindExternalUserId] = useState<string>('');
  const [bindMobile, setBindMobile] = useState<string>('');
  const [bindEmail, setBindEmail] = useState<string>('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // 单行解决子状态
  const [resolutions, setResolutions] = useState<Record<number, { resolution: BindingConflictResolution; newLocalUserId?: string }>>({});

  const onCreateManual = async () => {
    setError(null);
    if (!bindLocalUserId.trim() || !bindExternalUserId.trim()) {
      setError(`${text.localUserId} / ${text.externalUserId} 必填`);
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
    const cfg = resolutions[binding.id] ?? { resolution: 'KeepCurrent' };
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

  const onRevoke = async (binding: ExternalIdentityBindingListItem) => {
    if (!confirm(text.revokeConfirm)) return;
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
  };

  return (
    <section data-testid="identity-binding-conflict-center" style={{ marginTop: 16 }}>
      <h3>{text.title}</h3>

      <fieldset style={{ border: '1px solid #eee', padding: 12, marginBottom: 16 }}>
        <legend>{text.manualBindHeader}</legend>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', columnGap: 12, rowGap: 8 }}>
          <label>
            <span style={{ display: 'block', fontSize: 12, color: '#888' }}>{text.localUserId}</span>
            <input value={bindLocalUserId} onChange={(e) => setBindLocalUserId(e.target.value)} type="number" style={{ width: '100%', padding: 4 }} />
          </label>
          <label>
            <span style={{ display: 'block', fontSize: 12, color: '#888' }}>{text.externalUserId}</span>
            <input value={bindExternalUserId} onChange={(e) => setBindExternalUserId(e.target.value)} style={{ width: '100%', padding: 4 }} />
          </label>
          <label>
            <span style={{ display: 'block', fontSize: 12, color: '#888' }}>{text.mobile}</span>
            <input value={bindMobile} onChange={(e) => setBindMobile(e.target.value)} style={{ width: '100%', padding: 4 }} />
          </label>
          <label>
            <span style={{ display: 'block', fontSize: 12, color: '#888' }}>{text.email}</span>
            <input value={bindEmail} onChange={(e) => setBindEmail(e.target.value)} type="email" style={{ width: '100%', padding: 4 }} />
          </label>
        </div>
        <div style={{ marginTop: 12 }}>
          <button type="button" disabled={submitting} onClick={() => void onCreateManual()}>{text.submitManualBind}</button>
        </div>
      </fieldset>

      {error && <p style={{ color: 'red' }}>{error}</p>}

      {props.conflicts.length === 0 ? (
        <p style={{ color: '#888' }}>{text.noConflicts}</p>
      ) : (
        <table style={{ width: '100%', borderCollapse: 'collapse' }}>
          <thead>
            <tr style={{ borderBottom: '1px solid #ddd' }}>
              <th align="left">{text.columnId}</th>
              <th align="left">{text.columnLocalUser}</th>
              <th align="left">{text.columnExternalUser}</th>
              <th align="left">{text.columnStatus}</th>
              <th align="left">{text.columnAction}</th>
            </tr>
          </thead>
          <tbody>
            {props.conflicts.map((b) => {
              const cfg = resolutions[b.id] ?? { resolution: 'KeepCurrent' as BindingConflictResolution };
              return (
                <tr key={b.id} style={{ borderBottom: '1px solid #f0f0f0' }}>
                  <td>{b.id}</td>
                  <td>{b.localUserId}</td>
                  <td>{b.externalUserId}</td>
                  <td style={{ color: '#c00' }}>{b.status}</td>
                  <td>
                    <select
                      value={cfg.resolution}
                      onChange={(e) => setResolutions((prev) => ({ ...prev, [b.id]: { ...cfg, resolution: e.target.value as BindingConflictResolution } }))}
                    >
                      <option value="KeepCurrent">{text.resolutionKeepCurrent}</option>
                      <option value="SwitchToLocalUser">{text.resolutionSwitchToLocalUser}</option>
                      <option value="Revoke">{text.resolutionRevoke}</option>
                    </select>
                    {cfg.resolution === 'SwitchToLocalUser' && (
                      <>
                        {' '}
                        <input
                          type="number"
                          placeholder={text.newLocalUserIdLabel}
                          value={cfg.newLocalUserId ?? ''}
                          onChange={(e) => setResolutions((prev) => ({ ...prev, [b.id]: { ...cfg, newLocalUserId: e.target.value } }))}
                          style={{ width: 120 }}
                        />
                      </>
                    )}{' '}
                    <button type="button" disabled={submitting} onClick={() => void applyResolution(b)}>{text.apply}</button>{' '}
                    <button type="button" disabled={submitting} onClick={() => void onRevoke(b)} style={{ color: '#c00' }}>{text.revoke}</button>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      )}
    </section>
  );
}
