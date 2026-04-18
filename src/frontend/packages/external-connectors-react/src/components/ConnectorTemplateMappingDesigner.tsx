import { useEffect, useMemo, useState } from 'react';
import type { ChangeEvent } from 'react';
import type {
  ExternalApprovalTemplateMappingRequest,
  ExternalApprovalTemplateMappingResponse,
  ExternalApprovalTemplateResponse,
  IntegrationMode,
} from '../types';

/**
 * 单个本地表单字段的描述（由调用方传入；通常来自 ApprovalFlowDefinition.formMeta）。
 */
export interface LocalFormField {
  key: string;
  label: string;
  valueType: 'string' | 'number' | 'boolean' | 'date' | 'select' | 'multiSelect' | 'file' | 'image' | 'object';
  required?: boolean;
}

/** 一行映射：本地字段 ↔ 外部模板控件（+ 可选枚举映射）。 */
export interface MappingRow {
  localFieldKey: string;
  externalControlId: string;
  valueType: string;
  /** 枚举映射：本地选项 key → 外部 option key */
  enumMapping?: Record<string, string>;
}

export interface ConnectorTemplateMappingDesignerProps {
  template: ExternalApprovalTemplateResponse;
  localFields: LocalFormField[];
  /** 已存在的映射（编辑模式）；缺省即创建模式。 */
  existing?: ExternalApprovalTemplateMappingResponse;
  flowDefinitionId: number;
  providerId: number;
  onSave: (payload: ExternalApprovalTemplateMappingRequest) => Promise<void>;
  onDelete?: () => Promise<void>;
  labels?: Partial<Record<
    | 'title'
    | 'localField'
    | 'externalControl'
    | 'valueType'
    | 'integrationMode'
    | 'enabled'
    | 'enumMapping'
    | 'addRow'
    | 'removeRow'
    | 'save'
    | 'delete'
    | 'noTemplate'
    | 'unmappedRequired',
    string
  >>;
}

const defaultLabels = {
  title: '审批字段映射设计器',
  localField: '本地表单字段',
  externalControl: '外部模板控件',
  valueType: '值类型',
  integrationMode: '集成模式',
  enabled: '启用此映射',
  enumMapping: '枚举映射（JSON）',
  addRow: '+ 新增一行映射',
  removeRow: '删除',
  save: '保存',
  delete: '删除整条映射',
  noTemplate: '当前模板没有可映射的控件',
  unmappedRequired: '存在未映射的本地必填字段，保存前请补齐',
};

/**
 * 字段映射设计器：左本地字段 / 右外部控件 / 中行级映射 + 枚举映射 + 集成模式选择。
 * 写出 ExternalApprovalTemplateMapping，由 ConnectorApprovalMappingPage 提交。
 */
export function ConnectorTemplateMappingDesigner(props: ConnectorTemplateMappingDesignerProps) {
  const text = { ...defaultLabels, ...props.labels };

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
      setError(`${text.unmappedRequired}: ${unmappedRequired.map((f) => f.label).join(', ')}`);
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
    return <p style={{ color: '#888' }}>{text.noTemplate}</p>;
  }

  return (
    <section data-testid="connector-template-mapping-designer">
      <header style={{ marginBottom: 12 }}>
        <h4 style={{ margin: 0 }}>
          {text.title} <small style={{ color: '#888' }}>（模板：{props.template.name}）</small>
        </h4>
      </header>

      <div style={{ display: 'flex', gap: 16, marginBottom: 12 }}>
        <label>
          {text.integrationMode}：
          <select value={mode} onChange={(e: ChangeEvent<HTMLSelectElement>) => setMode(e.target.value as IntegrationMode)}>
            <option value="ExternalLed">A 外部主导</option>
            <option value="LocalLed">B 本地主导</option>
            <option value="Hybrid">C 双中心混合</option>
          </select>
        </label>
        <label>
          <input type="checkbox" checked={enabled} onChange={(e) => setEnabled(e.target.checked)} /> {text.enabled}
        </label>
      </div>

      <table style={{ width: '100%', borderCollapse: 'collapse', marginBottom: 12 }}>
        <thead>
          <tr style={{ borderBottom: '1px solid #ddd' }}>
            <th align="left">{text.localField}</th>
            <th align="left">{text.externalControl}</th>
            <th align="left">{text.valueType}</th>
            <th align="left">{text.enumMapping}</th>
            <th />
          </tr>
        </thead>
        <tbody>
          {rows.map((row, idx) => (
            <tr key={idx} style={{ borderBottom: '1px solid #f0f0f0' }}>
              <td>
                <select value={row.localFieldKey} onChange={(e) => updateRow(idx, { localFieldKey: e.target.value })}>
                  <option value="">-- 选择本地字段 --</option>
                  {props.localFields.map((f) => (
                    <option key={f.key} value={f.key}>
                      {f.label}{f.required ? ' *' : ''} ({f.valueType})
                    </option>
                  ))}
                </select>
              </td>
              <td>
                <select value={row.externalControlId} onChange={(e) => {
                  const ctl = props.template.controls.find((c) => c.controlId === e.target.value);
                  updateRow(idx, { externalControlId: e.target.value, valueType: ctl?.controlType ?? row.valueType });
                }}>
                  <option value="">-- 选择外部控件 --</option>
                  {props.template.controls.map((c) => (
                    <option key={c.controlId} value={c.controlId}>
                      {c.title}{c.required ? ' *' : ''} ({c.controlType})
                    </option>
                  ))}
                </select>
              </td>
              <td>{row.valueType}</td>
              <td>
                <input
                  type="text"
                  style={{ width: 220 }}
                  placeholder='{"high":"高","low":"低"}'
                  value={row.enumMapping ? JSON.stringify(row.enumMapping) : ''}
                  onChange={(e) => {
                    const text = e.target.value.trim();
                    if (!text) {
                      updateRow(idx, { enumMapping: undefined });
                      return;
                    }
                    try {
                      updateRow(idx, { enumMapping: JSON.parse(text) as Record<string, string> });
                    } catch {
                      // Keep typing — only update once parses successfully
                    }
                  }}
                />
              </td>
              <td>
                <button type="button" onClick={() => removeRow(idx)}>{text.removeRow}</button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      <button type="button" onClick={addRow}>{text.addRow}</button>

      {error && <p style={{ color: 'red' }}>{error}</p>}
      {unmappedRequired.length > 0 && (
        <p style={{ color: '#c80' }}>
          {text.unmappedRequired}：{unmappedRequired.map((f) => f.label).join(', ')}
        </p>
      )}

      <footer style={{ marginTop: 12, display: 'flex', gap: 8 }}>
        <button type="button" onClick={() => void onSubmit()} disabled={saving}>{text.save}</button>
        {props.onDelete && (
          <button type="button" disabled={saving} onClick={() => void props.onDelete?.()} style={{ color: '#c00', marginLeft: 'auto' }}>
            {text.delete}
          </button>
        )}
      </footer>
    </section>
  );
}
