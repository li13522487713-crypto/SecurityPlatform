import { useEffect, useMemo, useState } from 'react';
import { Banner, Button, Checkbox, Input, Select, Space, Table, Typography } from '@douyinfe/semi-ui';
import type { ColumnProps } from '@douyinfe/semi-ui/lib/es/table';
import type {
  ExternalApprovalTemplateMappingRequest,
  ExternalApprovalTemplateMappingResponse,
  ExternalApprovalTemplateResponse,
  IntegrationMode,
} from '../types';

/** Local form-field descriptor (provided by the host; usually from `ApprovalFlowDefinition.formMeta`). */
export interface LocalFormField {
  key: string;
  label: string;
  valueType: 'string' | 'number' | 'boolean' | 'date' | 'select' | 'multiSelect' | 'file' | 'image' | 'object';
  required?: boolean;
}

/** A single mapping row: local field <-> external template control (+ optional enum mapping). */
export interface MappingRow {
  localFieldKey: string;
  externalControlId: string;
  valueType: string;
  /** Enum mapping: local option key -> external option key. */
  enumMapping?: Record<string, string>;
}

export type ConnectorTemplateMappingDesignerLabelsKey =
  | 'title'
  | 'templateLabelPrefix'
  | 'localField'
  | 'externalControl'
  | 'valueType'
  | 'integrationMode'
  | 'enabled'
  | 'enumMapping'
  | 'enumMappingPlaceholder'
  | 'addRow'
  | 'removeRow'
  | 'save'
  | 'delete'
  | 'noTemplate'
  | 'unmappedRequired'
  | 'selectLocalFieldPlaceholder'
  | 'selectExternalControlPlaceholder'
  | 'integrationModeExternalLed'
  | 'integrationModeLocalLed'
  | 'integrationModeHybrid';

export type ConnectorTemplateMappingDesignerLabels = Record<ConnectorTemplateMappingDesignerLabelsKey, string>;

export const CONNECTOR_TEMPLATE_MAPPING_DESIGNER_LABELS_KEYS = [
  'title',
  'templateLabelPrefix',
  'localField',
  'externalControl',
  'valueType',
  'integrationMode',
  'enabled',
  'enumMapping',
  'enumMappingPlaceholder',
  'addRow',
  'removeRow',
  'save',
  'delete',
  'noTemplate',
  'unmappedRequired',
  'selectLocalFieldPlaceholder',
  'selectExternalControlPlaceholder',
  'integrationModeExternalLed',
  'integrationModeLocalLed',
  'integrationModeHybrid',
] as const satisfies readonly ConnectorTemplateMappingDesignerLabelsKey[];

export const defaultConnectorTemplateMappingDesignerLabels: ConnectorTemplateMappingDesignerLabels = {
  title: 'Approval field mapping designer',
  templateLabelPrefix: 'Template:',
  localField: 'Local form field',
  externalControl: 'External template control',
  valueType: 'Value type',
  integrationMode: 'Integration mode',
  enabled: 'Enable this mapping',
  enumMapping: 'Enum mapping (JSON)',
  enumMappingPlaceholder: '{"high":"H","low":"L"}',
  addRow: '+ Add mapping row',
  removeRow: 'Remove',
  save: 'Save',
  delete: 'Delete entire mapping',
  noTemplate: 'Current template has no mappable controls',
  unmappedRequired: 'Required local fields are not mapped; please complete before saving',
  selectLocalFieldPlaceholder: '-- Select local field --',
  selectExternalControlPlaceholder: '-- Select external control --',
  integrationModeExternalLed: 'A. External-led',
  integrationModeLocalLed: 'B. Local-led',
  integrationModeHybrid: 'C. Hybrid (dual-master)',
};

export interface ConnectorTemplateMappingDesignerProps {
  template: ExternalApprovalTemplateResponse;
  localFields: LocalFormField[];
  /** Existing mapping (edit mode); omit for create mode. */
  existing?: ExternalApprovalTemplateMappingResponse;
  flowDefinitionId: number;
  providerId: number;
  onSave: (payload: ExternalApprovalTemplateMappingRequest) => Promise<void>;
  onDelete?: () => Promise<void>;
  labels: ConnectorTemplateMappingDesignerLabels;
}

/**
 * Field-mapping designer: left local fields / right external controls / center per-row mapping
 * with optional enum mapping and integration-mode select. Emits ExternalApprovalTemplateMapping
 * which the surrounding ConnectorApprovalMappingPage submits.
 */
export function ConnectorTemplateMappingDesigner(props: ConnectorTemplateMappingDesignerProps) {
  const { labels } = props;

  const initialRows: MappingRow[] = useMemo(() => {
    if (!props.existing?.fieldMappingJson) {
      return [];
    }
    try {
      const parsed = JSON.parse(props.existing.fieldMappingJson) as MappingRow[];
      return Array.isArray(parsed) ? parsed : [];
    } catch {
      return [];
    }
  }, [props.existing?.fieldMappingJson]);

  const [rows, setRows] = useState<MappingRow[]>(initialRows);
  const [mode, setMode] = useState<IntegrationMode>(props.existing?.integrationMode ?? 'Hybrid');
  const [enabled, setEnabled] = useState<boolean>(props.existing?.enabled ?? true);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    setRows(initialRows);
  }, [initialRows]);

  const unmappedRequired = useMemo(() => {
    const mappedKeys = new Set(rows.map((r) => r.localFieldKey));
    return props.localFields.filter((f) => f.required && !mappedKeys.has(f.key));
  }, [rows, props.localFields]);

  const addRow = () => {
    setRows((prev) => [...prev, { localFieldKey: '', externalControlId: '', valueType: 'string' }]);
  };

  const removeRow = (idx: number) => {
    setRows((prev) => prev.filter((_, i) => i !== idx));
  };

  const updateRow = (idx: number, patch: Partial<MappingRow>) => {
    setRows((prev) => prev.map((r, i) => (i === idx ? { ...r, ...patch } : r)));
  };

  const onSubmit = async () => {
    setError(null);
    if (unmappedRequired.length > 0) {
      setError(`${labels.unmappedRequired}: ${unmappedRequired.map((f) => f.label).join(', ')}`);
      return;
    }
    setSaving(true);
    try {
      await props.onSave({
        providerId: props.providerId,
        flowDefinitionId: props.flowDefinitionId,
        externalTemplateId: props.template.externalTemplateId,
        integrationMode: mode,
        fieldMappingJson: JSON.stringify(rows),
        enabled,
      });
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setSaving(false);
    }
  };

  if (props.template.controls.length === 0) {
    return <Typography.Text type="tertiary">{labels.noTemplate}</Typography.Text>;
  }

  const localFieldOptions = props.localFields.map((f) => ({
    value: f.key,
    label: `${f.label}${f.required ? ' *' : ''} (${f.valueType})`,
  }));
  const externalControlOptions = props.template.controls.map((c) => ({
    value: c.controlId,
    label: `${c.title}${c.required ? ' *' : ''} (${c.controlType})`,
    controlType: c.controlType,
  }));

  const columns: ColumnProps<MappingRow & { __idx: number }>[] = [
    {
      title: labels.localField,
      dataIndex: 'localFieldKey',
      render: (_text, record) => (
        <Select
          value={record.localFieldKey || undefined}
          placeholder={labels.selectLocalFieldPlaceholder}
          optionList={localFieldOptions}
          onChange={(v) => updateRow(record.__idx, { localFieldKey: String(v ?? '') })}
          style={{ width: '100%', minWidth: 180 }}
        />
      ),
    },
    {
      title: labels.externalControl,
      dataIndex: 'externalControlId',
      render: (_text, record) => (
        <Select
          value={record.externalControlId || undefined}
          placeholder={labels.selectExternalControlPlaceholder}
          optionList={externalControlOptions}
          onChange={(v) => {
            const value = String(v ?? '');
            const ctl = externalControlOptions.find((c) => c.value === value);
            updateRow(record.__idx, { externalControlId: value, valueType: ctl?.controlType ?? record.valueType });
          }}
          style={{ width: '100%', minWidth: 180 }}
        />
      ),
    },
    {
      title: labels.valueType,
      dataIndex: 'valueType',
      width: 110,
      render: (_text, record) => <Typography.Text>{record.valueType}</Typography.Text>,
    },
    {
      title: labels.enumMapping,
      dataIndex: 'enumMapping',
      render: (_text, record) => (
        <Input
          placeholder={labels.enumMappingPlaceholder}
          value={record.enumMapping ? JSON.stringify(record.enumMapping) : ''}
          onChange={(value) => {
            const text = value.trim();
            if (!text) {
              updateRow(record.__idx, { enumMapping: undefined });
              return;
            }
            try {
              updateRow(record.__idx, { enumMapping: JSON.parse(text) as Record<string, string> });
            } catch {
              // Keep typing — only update once parses successfully
            }
          }}
        />
      ),
    },
    {
      title: '',
      dataIndex: '__action',
      width: 90,
      render: (_text, record) => (
        <Button type="danger" theme="borderless" onClick={() => removeRow(record.__idx)}>
          {labels.removeRow}
        </Button>
      ),
    },
  ];

  const dataSource = rows.map((r, idx) => ({ ...r, __idx: idx, __key: idx }));

  return (
    <section data-testid="connector-template-mapping-designer">
      <header style={{ marginBottom: 12 }}>
        <Typography.Title heading={5} style={{ margin: 0 }}>
          {labels.title}{' '}
          <Typography.Text type="tertiary" size="small">
            ({labels.templateLabelPrefix} {props.template.name})
          </Typography.Text>
        </Typography.Title>
      </header>

      <Space spacing="loose" style={{ marginBottom: 12 }}>
        <Space>
          <Typography.Text>{labels.integrationMode}:</Typography.Text>
          <Select
            value={mode}
            onChange={(v) => setMode((v as IntegrationMode) ?? 'Hybrid')}
            optionList={[
              { value: 'ExternalLed', label: labels.integrationModeExternalLed },
              { value: 'LocalLed', label: labels.integrationModeLocalLed },
              { value: 'Hybrid', label: labels.integrationModeHybrid },
            ]}
            style={{ minWidth: 200 }}
          />
        </Space>
        <Checkbox checked={enabled} onChange={(e) => setEnabled(Boolean(e.target.checked))}>
          {labels.enabled}
        </Checkbox>
      </Space>

      <Table
        rowKey="__key"
        size="small"
        pagination={false}
        columns={columns}
        dataSource={dataSource}
        style={{ marginBottom: 12 }}
      />

      <Button type="primary" theme="light" onClick={addRow}>
        {labels.addRow}
      </Button>

      {error && (
        <Banner
          type="danger"
          fullMode={false}
          description={error}
          closeIcon={null}
          style={{ marginTop: 12 }}
        />
      )}
      {unmappedRequired.length > 0 && (
        <Banner
          type="warning"
          fullMode={false}
          description={`${labels.unmappedRequired}: ${unmappedRequired.map((f) => f.label).join(', ')}`}
          closeIcon={null}
          style={{ marginTop: 12 }}
        />
      )}

      <Space spacing="medium" style={{ marginTop: 12, width: '100%', justifyContent: 'space-between' }}>
        <Button type="primary" loading={saving} onClick={() => void onSubmit()}>
          {labels.save}
        </Button>
        {props.onDelete && (
          <Button
            type="danger"
            theme="borderless"
            disabled={saving}
            onClick={() => void props.onDelete?.()}
          >
            {labels.delete}
          </Button>
        )}
      </Space>
    </section>
  );
}
