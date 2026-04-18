import { Fragment, useEffect, useState } from 'react';
import type { ChangeEvent } from 'react';
import type { ConnectorApi } from '../api';
import type {
  ExternalDirectorySyncDiffItem,
  ExternalDirectorySyncIncrementalRequest,
  ExternalDirectorySyncJobResponse,
} from '../types';

export interface ConnectorDirectorySyncPageProps {
  api: ConnectorApi;
  providerId: number;
  /** 默认 provider 类型字符串，用于增量事件填充。 */
  providerTypeHint?: string;
  labels?: Partial<Record<
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
    | 'incrementalSubmit',
    string
  >>;
}

const defaultLabels = {
  title: '通讯录同步对账',
  fullSync: '立即全量同步',
  incrementalSync: '应用增量事件',
  syncing: '同步中...',
  jobsHeader: '近期同步任务',
  diffsHeader: '差异行（点击「重试」会以增量事件形式重投）',
  retry: '重试',
  expandDiffs: '查看差异',
  collapseDiffs: '收起差异',
  noDiffs: '没有差异行',
  incrementalKind: '事件类型',
  incrementalEntityId: '实体 ID',
  incrementalSubmit: '应用',
};

/**
 * 通讯录同步对账页：全量同步 + 增量手动事件 + 任务列表 + 差异面板（按 jobId 展开）+ 失败行重试。
 */
export function ConnectorDirectorySyncPage({ api, providerId, providerTypeHint, labels }: ConnectorDirectorySyncPageProps) {
  const text = { ...defaultLabels, ...labels };

  const [jobs, setJobs] = useState<ExternalDirectorySyncJobResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [expandedJobId, setExpandedJobId] = useState<number | null>(null);
  const [diffs, setDiffs] = useState<ExternalDirectorySyncDiffItem[]>([]);
  const [diffLoading, setDiffLoading] = useState(false);

  // 增量事件子表单
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
      setError('实体 ID 必填');
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

  return (
    <section data-testid="connector-directory-sync-page">
      <header style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
        <h3 style={{ margin: 0 }}>{text.title}</h3>
        <div style={{ display: 'flex', gap: 8 }}>
          <button type="button" disabled={busy} onClick={() => void triggerFull()}>
            {busy ? text.syncing : text.fullSync}
          </button>
        </div>
      </header>

      <fieldset style={{ border: '1px solid #eee', padding: 8, marginBottom: 12 }}>
        <legend>{text.incrementalSync}</legend>
        <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
          <label>
            {text.incrementalKind}：
            <select value={incKind} onChange={(e: ChangeEvent<HTMLSelectElement>) => setIncKind(e.target.value as ExternalDirectorySyncIncrementalRequest['kind'])}>
              <option value="UserCreated">UserCreated</option>
              <option value="UserUpdated">UserUpdated</option>
              <option value="UserDeleted">UserDeleted</option>
              <option value="DepartmentCreated">DepartmentCreated</option>
              <option value="DepartmentUpdated">DepartmentUpdated</option>
              <option value="DepartmentDeleted">DepartmentDeleted</option>
            </select>
          </label>
          <label>
            {text.incrementalEntityId}：
            <input type="text" value={incEntityId} onChange={(e) => setIncEntityId(e.target.value)} placeholder="zhangsan / dept-100" />
          </label>
          <button type="button" disabled={busy} onClick={() => void triggerIncremental()}>
            {text.incrementalSubmit}
          </button>
        </div>
      </fieldset>

      {loading && <p>Loading...</p>}
      {error && <p style={{ color: 'red' }}>{error}</p>}

      {!loading && jobs.length > 0 && (
        <>
          <h4 style={{ marginTop: 16 }}>{text.jobsHeader}</h4>
          <table style={{ width: '100%', borderCollapse: 'collapse' }}>
            <thead>
              <tr style={{ borderBottom: '1px solid #ddd' }}>
                <th align="left">JobId</th>
                <th align="left">模式</th>
                <th align="left">状态</th>
                <th align="left">触发源</th>
                <th align="right">用户增删改</th>
                <th align="right">部门增删改</th>
                <th align="left">开始</th>
                <th align="left">结束</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {jobs.map((j) => (
                <Fragment key={j.id}>
                  <tr style={{ borderBottom: expandedJobId === j.id ? 'none' : '1px solid #f0f0f0' }}>
                    <td>{j.id}</td>
                    <td>{j.mode}</td>
                    <td style={{ color: j.status === 'Failed' ? '#c00' : undefined }}>{j.status}</td>
                    <td>{j.triggerSource}</td>
                    <td align="right">{j.userCreated} / {j.userUpdated} / {j.userDeleted}</td>
                    <td align="right">{j.departmentCreated} / {j.departmentUpdated} / {j.departmentDeleted}</td>
                    <td>{new Date(j.startedAt).toLocaleString()}</td>
                    <td>{j.finishedAt ? new Date(j.finishedAt).toLocaleString() : '-'}</td>
                    <td>
                      <button type="button" onClick={() => void toggleDiffs(j.id)}>
                        {expandedJobId === j.id ? text.collapseDiffs : text.expandDiffs}
                      </button>
                    </td>
                  </tr>
                  {expandedJobId === j.id && (
                    <tr>
                      <td colSpan={9} style={{ background: '#fafafa', padding: 8 }}>
                        <strong>{text.diffsHeader}</strong>
                        {diffLoading && <p>Loading diffs...</p>}
                        {!diffLoading && diffs.length === 0 && <p>{text.noDiffs}</p>}
                        {!diffLoading && diffs.length > 0 && (
                          <table style={{ width: '100%', borderCollapse: 'collapse', marginTop: 8 }}>
                            <thead>
                              <tr style={{ borderBottom: '1px solid #ddd' }}>
                                <th align="left">DiffId</th>
                                <th align="left">类型</th>
                                <th align="left">实体 ID</th>
                                <th align="left">摘要 / 错误</th>
                                <th align="left">时间</th>
                                <th />
                              </tr>
                            </thead>
                            <tbody>
                              {diffs.map((d) => (
                                <tr key={d.id} style={{ borderBottom: '1px solid #eee' }}>
                                  <td>{d.id}</td>
                                  <td style={{ color: d.diffType === 'Failed' ? '#c00' : undefined }}>{d.diffType}</td>
                                  <td>{d.entityId}</td>
                                  <td>{d.errorMessage ?? d.summary ?? '-'}</td>
                                  <td>{new Date(d.occurredAt).toLocaleString()}</td>
                                  <td>
                                    {d.diffType === 'Failed' && (
                                      <button type="button" disabled={busy} onClick={() => void retryDiff(d)}>{text.retry}</button>
                                    )}
                                  </td>
                                </tr>
                              ))}
                            </tbody>
                          </table>
                        )}
                      </td>
                    </tr>
                  )}
                </Fragment>
              ))}
            </tbody>
          </table>
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
