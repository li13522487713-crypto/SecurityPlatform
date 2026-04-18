import { useEffect, useMemo, useState } from 'react';
import type { ConnectorApi } from '../api';
import type {
  ExternalApprovalTemplateMappingResponse,
  ExternalApprovalTemplateResponse,
  IntegrationMode,
} from '../types';
import { ConnectorTemplateMappingDesigner, type LocalFormField } from './ConnectorTemplateMappingDesigner';

export interface ConnectorApprovalMappingPageProps {
  api: ConnectorApi;
  providerId: number;
  /**
   * 当前流程定义 ID + 该流程已知的本地字段元信息。
   * 由调用方（app-web）通过 ApprovalFlowDefinition.formMeta 解析后传入。
   */
  flowDefinitionId?: number;
  localFields?: LocalFormField[];
  labels?: Partial<Record<
    | 'title'
    | 'templatesHeader'
    | 'mappingsHeader'
    | 'designerEmpty'
    | 'refresh'
    | 'startMapping'
    | 'columnTemplateId'
    | 'columnTemplateName'
    | 'columnControls'
    | 'columnFetchedAt'
    | 'columnFlowId'
    | 'columnExternalTpl'
    | 'columnIntegrationMode'
    | 'columnEnabled'
    | 'columnUpdatedAt'
    | 'enabled'
    | 'disabled',
    string
  >>;
}

const defaultLabels = {
  title: '审批模板与字段映射',
  templatesHeader: '已缓存的外部审批模板',
  mappingsHeader: '本地流程定义 ↔ 外部模板映射',
  designerEmpty: '请选择上方一行模板，并提供 flowDefinitionId / localFields 后启用设计器。',
  refresh: '刷新',
  startMapping: '开始映射',
  columnTemplateId: '模板 ID',
  columnTemplateName: '名称',
  columnControls: '控件数',
  columnFetchedAt: '最后拉取',
  columnFlowId: 'FlowDefinitionId',
  columnExternalTpl: '外部模板',
  columnIntegrationMode: '集成模式',
  columnEnabled: '启用',
  columnUpdatedAt: '最后更新',
  enabled: '启用',
  disabled: '停用',
};

export function ConnectorApprovalMappingPage({ api, providerId, flowDefinitionId, localFields, labels }: ConnectorApprovalMappingPageProps) {
  const text = { ...defaultLabels, ...labels };
  const [templates, setTemplates] = useState<ExternalApprovalTemplateResponse[]>([]);
  const [mappings, setMappings] = useState<ExternalApprovalTemplateMappingResponse[]>([]);
  const [selectedTemplateId, setSelectedTemplateId] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const reloadAll = async () => {
    setLoading(true);
    setError(null);
    try {
      const [tpls, maps] = await Promise.all([
        api.listApprovalTemplates(providerId),
        api.listApprovalTemplateMappings(providerId),
      ]);
      setTemplates(tpls);
      setMappings(maps);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void reloadAll();
  }, [api, providerId]);

  const refreshTemplate = async (id: string) => {
    try {
      const fresh = await api.refreshApprovalTemplate(providerId, id);
      setTemplates((prev) => prev.map((t) => (t.externalTemplateId === id ? fresh : t)));
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    }
  };

  const selectedTemplate = useMemo(
    () => templates.find((t) => t.externalTemplateId === selectedTemplateId) ?? null,
    [templates, selectedTemplateId],
  );

  const existingMapping = useMemo(() => {
    if (!flowDefinitionId || !selectedTemplate) return undefined;
    return mappings.find(
      (m) => m.flowDefinitionId === flowDefinitionId && m.externalTemplateId === selectedTemplate.externalTemplateId,
    );
  }, [mappings, flowDefinitionId, selectedTemplate]);

  return (
    <section data-testid="connector-approval-mapping-page">
      <header style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
        <h3 style={{ margin: 0 }}>{text.title}</h3>
        <button type="button" onClick={() => void reloadAll()}>{text.refresh}</button>
      </header>

      {error && <p style={{ color: 'red' }}>{error}</p>}
      {loading && <p>Loading...</p>}

      {!loading && (
        <>
          <h4>{text.templatesHeader}</h4>
          <table style={{ width: '100%', borderCollapse: 'collapse', marginBottom: 24 }}>
            <thead>
              <tr style={{ borderBottom: '1px solid #ddd' }}>
                <th align="left">{text.columnTemplateId}</th>
                <th align="left">{text.columnTemplateName}</th>
                <th align="right">{text.columnControls}</th>
                <th align="left">{text.columnFetchedAt}</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {templates.map((t) => (
                <tr
                  key={t.externalTemplateId}
                  style={{
                    borderBottom: '1px solid #f0f0f0',
                    background: selectedTemplateId === t.externalTemplateId ? '#fffbe6' : undefined,
                  }}
                >
                  <td>{t.externalTemplateId}</td>
                  <td>{t.name}</td>
                  <td align="right">{t.controls.length}</td>
                  <td>{new Date(t.fetchedAt).toLocaleString()}</td>
                  <td style={{ whiteSpace: 'nowrap' }}>
                    <button type="button" onClick={() => setSelectedTemplateId(t.externalTemplateId)}>
                      {text.startMapping}
                    </button>{' '}
                    <button type="button" onClick={() => void refreshTemplate(t.externalTemplateId)}>{text.refresh}</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          <h4>{text.mappingsHeader}</h4>
          <table style={{ width: '100%', borderCollapse: 'collapse', marginBottom: 24 }}>
            <thead>
              <tr style={{ borderBottom: '1px solid #ddd' }}>
                <th align="left">{text.columnFlowId}</th>
                <th align="left">{text.columnExternalTpl}</th>
                <th align="left">{text.columnIntegrationMode}</th>
                <th align="left">{text.columnEnabled}</th>
                <th align="left">{text.columnUpdatedAt}</th>
              </tr>
            </thead>
            <tbody>
              {mappings.map((m) => (
                <tr key={m.id} style={{ borderBottom: '1px solid #f0f0f0' }}>
                  <td>{m.flowDefinitionId}</td>
                  <td>{m.externalTemplateId}</td>
                  <td>{integrationModeLabel(m.integrationMode)}</td>
                  <td>{m.enabled ? text.enabled : text.disabled}</td>
                  <td>{new Date(m.updatedAt).toLocaleString()}</td>
                </tr>
              ))}
            </tbody>
          </table>

          <h4>映射设计器</h4>
          {selectedTemplate && flowDefinitionId && localFields ? (
            <ConnectorTemplateMappingDesigner
              template={selectedTemplate}
              localFields={localFields}
              existing={existingMapping}
              providerId={providerId}
              flowDefinitionId={flowDefinitionId}
              onSave={async (payload) => {
                await api.upsertApprovalTemplateMapping(providerId, flowDefinitionId, payload);
                await reloadAll();
              }}
              onDelete={existingMapping ? async () => {
                await api.deleteApprovalTemplateMapping(providerId, existingMapping.id);
                await reloadAll();
              } : undefined}
            />
          ) : (
            <p style={{ color: '#888' }}>{text.designerEmpty}</p>
          )}
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
