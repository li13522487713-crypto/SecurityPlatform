import { useEffect, useMemo, useState } from 'react';
import { Banner, Button, Space, Spin, Table, Tag, Typography } from '@douyinfe/semi-ui';
import type { ColumnProps } from '@douyinfe/semi-ui/lib/es/table';
import type { ConnectorApi } from '../api';
import type {
  ExternalApprovalTemplateMappingResponse,
  ExternalApprovalTemplateResponse,
  IntegrationMode,
} from '../types';
import {
  ConnectorTemplateMappingDesigner,
  defaultConnectorTemplateMappingDesignerLabels,
  type ConnectorTemplateMappingDesignerLabels,
  type LocalFormField,
} from './ConnectorTemplateMappingDesigner';

export type ConnectorApprovalMappingPageLabelsKey =
  | 'title'
  | 'templatesHeader'
  | 'mappingsHeader'
  | 'designerHeader'
  | 'designerEmpty'
  | 'refresh'
  | 'startMapping'
  | 'columnTemplateId'
  | 'columnTemplateName'
  | 'columnControls'
  | 'columnFetchedAt'
  | 'columnTemplateActions'
  | 'columnFlowId'
  | 'columnExternalTpl'
  | 'columnIntegrationMode'
  | 'columnEnabled'
  | 'columnUpdatedAt'
  | 'enabled'
  | 'disabled'
  | 'integrationModeExternalLed'
  | 'integrationModeLocalLed'
  | 'integrationModeHybrid'
  | 'loadingText';

export type ConnectorApprovalMappingPageLabels = Record<ConnectorApprovalMappingPageLabelsKey, string>;

export const CONNECTOR_APPROVAL_MAPPING_PAGE_LABELS_KEYS = [
  'title',
  'templatesHeader',
  'mappingsHeader',
  'designerHeader',
  'designerEmpty',
  'refresh',
  'startMapping',
  'columnTemplateId',
  'columnTemplateName',
  'columnControls',
  'columnFetchedAt',
  'columnTemplateActions',
  'columnFlowId',
  'columnExternalTpl',
  'columnIntegrationMode',
  'columnEnabled',
  'columnUpdatedAt',
  'enabled',
  'disabled',
  'integrationModeExternalLed',
  'integrationModeLocalLed',
  'integrationModeHybrid',
  'loadingText',
] as const satisfies readonly ConnectorApprovalMappingPageLabelsKey[];

export const defaultConnectorApprovalMappingPageLabels: ConnectorApprovalMappingPageLabels = {
  title: 'Approval templates & field mapping',
  templatesHeader: 'Cached external approval templates',
  mappingsHeader: 'Local flow definitions ↔ external templates',
  designerHeader: 'Mapping designer',
  designerEmpty: 'Select a template row above and provide flowDefinitionId / localFields to enable the designer.',
  refresh: 'Refresh',
  startMapping: 'Start mapping',
  columnTemplateId: 'Template ID',
  columnTemplateName: 'Name',
  columnControls: 'Controls',
  columnFetchedAt: 'Last fetched',
  columnTemplateActions: 'Actions',
  columnFlowId: 'FlowDefinitionId',
  columnExternalTpl: 'External template',
  columnIntegrationMode: 'Integration mode',
  columnEnabled: 'Enabled',
  columnUpdatedAt: 'Last updated',
  enabled: 'On',
  disabled: 'Off',
  integrationModeExternalLed: 'A. External-led',
  integrationModeLocalLed: 'B. Local-led',
  integrationModeHybrid: 'C. Hybrid',
  loadingText: 'Loading...',
};

export interface ConnectorApprovalMappingPageProps {
  api: ConnectorApi;
  providerId: number;
  /**
   * Current flow-definition ID + local field metadata of that flow.
   * Caller (app-web) parses these from `ApprovalFlowDefinition.formMeta`.
   */
  flowDefinitionId?: number;
  localFields?: LocalFormField[];
  labels: ConnectorApprovalMappingPageLabels;
  designerLabels: ConnectorTemplateMappingDesignerLabels;
}

export function ConnectorApprovalMappingPage({
  api,
  providerId,
  flowDefinitionId,
  localFields,
  labels,
  designerLabels,
}: ConnectorApprovalMappingPageProps) {
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
    // eslint-disable-next-line react-hooks/exhaustive-deps
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

  const integrationLabel = (mode: IntegrationMode): string => {
    switch (mode) {
      case 'ExternalLed':
        return labels.integrationModeExternalLed;
      case 'LocalLed':
        return labels.integrationModeLocalLed;
      case 'Hybrid':
        return labels.integrationModeHybrid;
      default:
        return mode;
    }
  };

  const templatesColumns: ColumnProps<ExternalApprovalTemplateResponse & { __key: string }>[] = [
    { title: labels.columnTemplateId, dataIndex: 'externalTemplateId' },
    { title: labels.columnTemplateName, dataIndex: 'name' },
    {
      title: labels.columnControls,
      dataIndex: '__controls',
      align: 'right',
      width: 100,
      render: (_, record) => record.controls.length,
    },
    {
      title: labels.columnFetchedAt,
      dataIndex: 'fetchedAt',
      width: 200,
      render: (_, record) => new Date(record.fetchedAt).toLocaleString(),
    },
    {
      title: labels.columnTemplateActions,
      dataIndex: '__actions',
      width: 200,
      render: (_, record) => (
        <Space>
          <Button size="small" type="primary" onClick={() => setSelectedTemplateId(record.externalTemplateId)}>
            {labels.startMapping}
          </Button>
          <Button size="small" onClick={() => void refreshTemplate(record.externalTemplateId)}>
            {labels.refresh}
          </Button>
        </Space>
      ),
    },
  ];

  const mappingsColumns: ColumnProps<ExternalApprovalTemplateMappingResponse & { __key: number }>[] = [
    { title: labels.columnFlowId, dataIndex: 'flowDefinitionId', width: 160 },
    { title: labels.columnExternalTpl, dataIndex: 'externalTemplateId' },
    {
      title: labels.columnIntegrationMode,
      dataIndex: 'integrationMode',
      width: 220,
      render: (_, record) => integrationLabel(record.integrationMode),
    },
    {
      title: labels.columnEnabled,
      dataIndex: 'enabled',
      width: 100,
      render: (_, record) => (
        <Tag color={record.enabled ? 'green' : 'grey'}>{record.enabled ? labels.enabled : labels.disabled}</Tag>
      ),
    },
    {
      title: labels.columnUpdatedAt,
      dataIndex: 'updatedAt',
      width: 200,
      render: (_, record) => new Date(record.updatedAt).toLocaleString(),
    },
  ];

  const designerEffectiveLabels = designerLabels ?? defaultConnectorTemplateMappingDesignerLabels;

  return (
    <section data-testid="connector-approval-mapping-page">
      <Space spacing="medium" style={{ width: '100%', justifyContent: 'space-between', marginBottom: 12 }}>
        <Typography.Title heading={5} style={{ margin: 0 }}>
          {labels.title}
        </Typography.Title>
        <Button onClick={() => void reloadAll()}>{labels.refresh}</Button>
      </Space>

      {error && <Banner type="danger" fullMode={false} description={error} closeIcon={null} style={{ marginBottom: 12 }} />}
      {loading && <Spin tip={labels.loadingText} />}

      {!loading && (
        <>
          <Typography.Title heading={6} style={{ marginTop: 8 }}>
            {labels.templatesHeader}
          </Typography.Title>
          <Table
            rowKey="__key"
            size="small"
            pagination={false}
            columns={templatesColumns}
            dataSource={templates.map((t) => ({ ...t, __key: t.externalTemplateId }))}
            style={{ marginBottom: 24 }}
            rowSelection={undefined}
            onRow={(record) =>
              selectedTemplateId === record?.externalTemplateId
                ? { style: { background: 'var(--semi-color-warning-light-default)' } }
                : {}
            }
          />

          <Typography.Title heading={6}>{labels.mappingsHeader}</Typography.Title>
          <Table
            rowKey="__key"
            size="small"
            pagination={false}
            columns={mappingsColumns}
            dataSource={mappings.map((m) => ({ ...m, __key: m.id }))}
            style={{ marginBottom: 24 }}
          />

          <Typography.Title heading={6}>{labels.designerHeader}</Typography.Title>
          {selectedTemplate && flowDefinitionId && localFields ? (
            <ConnectorTemplateMappingDesigner
              template={selectedTemplate}
              localFields={localFields}
              existing={existingMapping}
              providerId={providerId}
              flowDefinitionId={flowDefinitionId}
              labels={designerEffectiveLabels}
              onSave={async (payload) => {
                await api.upsertApprovalTemplateMapping(providerId, flowDefinitionId, payload);
                await reloadAll();
              }}
              onDelete={
                existingMapping
                  ? async () => {
                      await api.deleteApprovalTemplateMapping(providerId, existingMapping.id);
                      await reloadAll();
                    }
                  : undefined
              }
            />
          ) : (
            <Typography.Text type="tertiary">{labels.designerEmpty}</Typography.Text>
          )}
        </>
      )}
    </section>
  );
}
