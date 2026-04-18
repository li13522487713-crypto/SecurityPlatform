import { useEffect, useState } from 'react';
import type { ConnectorApi } from '../api';
import type { ExternalDirectorySyncJobResponse } from '../types';

export interface ConnectorDirectorySyncPageProps {
  api: ConnectorApi;
  providerId: number;
}

export function ConnectorDirectorySyncPage({ api, providerId }: ConnectorDirectorySyncPageProps) {
  const [jobs, setJobs] = useState<ExternalDirectorySyncJobResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

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

  return (
    <section data-testid="connector-directory-sync-page">
      <header style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
        <h3>通讯录同步对账</h3>
        <button type="button" disabled={busy} onClick={triggerFull}>
          {busy ? '同步中...' : '立即全量同步'}
        </button>
      </header>
      {loading && <p>Loading...</p>}
      {error && <p style={{ color: 'red' }}>{error}</p>}
      {!loading && jobs.length > 0 && (
        <table style={{ width: '100%', borderCollapse: 'collapse' }}>
          <thead>
            <tr>
              <th align="left">JobId</th>
              <th align="left">模式</th>
              <th align="left">状态</th>
              <th align="left">触发源</th>
              <th align="right">用户增删改</th>
              <th align="right">部门增删改</th>
              <th align="left">开始时间</th>
              <th align="left">结束时间</th>
            </tr>
          </thead>
          <tbody>
            {jobs.map((j) => (
              <tr key={j.id}>
                <td>{j.id}</td>
                <td>{j.mode}</td>
                <td>{j.status}</td>
                <td>{j.triggerSource}</td>
                <td align="right">{j.userCreated} / {j.userUpdated} / {j.userDeleted}</td>
                <td align="right">{j.departmentCreated} / {j.departmentUpdated} / {j.departmentDeleted}</td>
                <td>{new Date(j.startedAt).toLocaleString()}</td>
                <td>{j.finishedAt ? new Date(j.finishedAt).toLocaleString() : '-'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}
