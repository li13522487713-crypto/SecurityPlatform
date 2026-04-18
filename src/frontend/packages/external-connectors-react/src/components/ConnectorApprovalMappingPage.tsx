import { useEffect, useState } from 'react';
import type { ConnectorApi } from '../api';
import type { ExternalApprovalTemplateMappingResponse, ExternalApprovalTemplateResponse, IntegrationMode } from '../types';

export interface ConnectorApprovalMappingPageProps {
  api: ConnectorApi;
  providerId: number;
}

export function ConnectorApprovalMappingPage({ api, providerId }: ConnectorApprovalMappingPageProps) {
  const [templates, setTemplates] = useState<ExternalApprovalTemplateResponse[]>([]);
  const [mappings, setMappings] = useState<ExternalApprovalTemplateMappingResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    setLoading(true);
    Promise.all([api.listApprovalTemplates(providerId), api.listApprovalTemplateMappings(providerId)])
      .then(([tpls, maps]) => {
        if (!active) return;
        setTemplates(tpls);
        setMappings(maps);
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
  }, [api, providerId]);

  const refreshTemplate = async (id: string) => {
    try {
      const fresh = await api.refreshApprovalTemplate(providerId, id);
      setTemplates((prev) => prev.map((t) => (t.externalTemplateId === id ? fresh : t)));
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    }
  };

  return (
    <section data-testid="connector-approval-mapping-page">
      <h3>审批模板与字段映射</h3>
      {error && <p style={{ color: 'red' }}>{error}</p>}
      {loading && <p>Loading...</p>}
      {!loading && (
        <>
          <h4>已缓存的外部审批模板</h4>
          <table style={{ width: '100%', borderCollapse: 'collapse', marginBottom: 24 }}>
            <thead>
              <tr>
                <th align="left">模板 ID</th>
                <th align="left">名称</th>
                <th align="right">控件数</th>
                <th align="left">最后拉取</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {templates.map((t) => (
                <tr key={t.externalTemplateId}>
                  <td>{t.externalTemplateId}</td>
                  <td>{t.name}</td>
                  <td align="right">{t.controls.length}</td>
                  <td>{new Date(t.fetchedAt).toLocaleString()}</td>
                  <td>
                    <button type="button" onClick={() => refreshTemplate(t.externalTemplateId)}>刷新</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          <h4>本地流程定义 ↔ 外部模板映射</h4>
          <table style={{ width: '100%', borderCollapse: 'collapse' }}>
            <thead>
              <tr>
                <th align="left">FlowDefinitionId</th>
                <th align="left">外部模板</th>
                <th align="left">集成模式</th>
                <th align="left">启用</th>
                <th align="left">最后更新</th>
              </tr>
            </thead>
            <tbody>
              {mappings.map((m) => (
                <tr key={m.id}>
                  <td>{m.flowDefinitionId}</td>
                  <td>{m.externalTemplateId}</td>
                  <td>{integrationModeLabel(m.integrationMode)}</td>
                  <td>{m.enabled ? '启用' : '停用'}</td>
                  <td>{new Date(m.updatedAt).toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}
    </section>
  );
}

function integrationModeLabel(mode: IntegrationMode): string {
  switch (mode) {
    case 'ExternalLed':
      return 'A 外部主导';
    case 'LocalLed':
      return 'B 本地主导';
    case 'Hybrid':
      return 'C 双中心';
    default:
      return mode;
  }
}
