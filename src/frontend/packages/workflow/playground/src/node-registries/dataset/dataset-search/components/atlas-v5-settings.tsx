/*
 * Atlas v5 §38 / 计划 G7：扩展 dataset-search 节点支持 RetrievalProfile / filters /
 * callerContextOverride / debug 字段。
 *
 * 这些字段在节点 config 中以 `atlasV5` 命名空间存放：
 *   atlasV5.retrievalProfile: { topK, minScore, enableRerank, rerankModel, enableHybrid,
 *                                weights: {vector, bm25, table, image}, enableQueryRewrite }
 *   atlasV5.filters: { [key: string]: string }
 *   atlasV5.callerContextOverride: { callerType, userId, preset, ... }
 *   atlasV5.debug: boolean
 *
 * data-transformer 会把这些字段透传给后端 KnowledgeRetrieverNodeExecutor 节点 config。
 */

import React, { useMemo } from 'react';

import { useField, withField } from '@/form';

interface KeyValueRow {
  key: string;
  value: string;
}

export interface AtlasV5RetrievalProfile {
  topK?: number;
  minScore?: number;
  enableRerank?: boolean;
  rerankModel?: string;
  enableHybrid?: boolean;
  enableQueryRewrite?: boolean;
  weights?: {
    vector?: number;
    bm25?: number;
    table?: number;
    image?: number;
  };
}

export interface AtlasV5Settings {
  retrievalProfile?: AtlasV5RetrievalProfile;
  filters?: Record<string, string>;
  callerContextOverride?: {
    callerType?: number;
    callerId?: string;
    callerName?: string;
    userId?: string;
    tenantId?: string;
    preset?: number;
  };
  debug?: boolean;
}

const PRESET_LABELS: Array<{ value: number; label: string }> = [
  { value: 0, label: 'Assistant' },
  { value: 1, label: 'WorkflowDebug' },
  { value: 2, label: 'ExternalApi' },
  { value: 3, label: 'System' },
];

const CALLER_TYPE_LABELS: Array<{ value: number; label: string }> = [
  { value: 0, label: 'Studio' },
  { value: 1, label: 'Agent' },
  { value: 2, label: 'Workflow' },
  { value: 3, label: 'App' },
  { value: 4, label: 'Chatflow' },
];

const filtersToRows = (filters?: Record<string, string>): KeyValueRow[] => {
  if (!filters) return [];
  return Object.entries(filters).map(([key, value]) => ({ key, value }));
};

const rowsToFilters = (rows: KeyValueRow[]): Record<string, string> | undefined => {
  const filtered = rows.filter((row) => row.key.trim().length > 0);
  if (filtered.length === 0) return undefined;
  return filtered.reduce<Record<string, string>>((acc, row) => {
    acc[row.key.trim()] = row.value;
    return acc;
  }, {});
};

const Settings: React.FC = () => {
  const { value, onChange, onBlur, readonly } = useField<AtlasV5Settings>();
  const settings = (value ?? {}) as AtlasV5Settings;
  const profile = settings.retrievalProfile ?? {};
  const weights = profile.weights ?? {};
  const callerOverride = settings.callerContextOverride ?? {};
  const filterRows = useMemo(() => filtersToRows(settings.filters), [settings.filters]);

  const update = (next: AtlasV5Settings) => {
    onChange(next);
    onBlur?.();
  };

  const updateProfile = (patch: Partial<AtlasV5RetrievalProfile>) => {
    update({
      ...settings,
      retrievalProfile: { ...profile, ...patch },
    });
  };

  const updateWeights = (patch: Partial<NonNullable<AtlasV5RetrievalProfile['weights']>>) => {
    updateProfile({ weights: { ...weights, ...patch } });
  };

  const updateOverride = (patch: Partial<AtlasV5Settings['callerContextOverride']>) => {
    update({
      ...settings,
      callerContextOverride: { ...callerOverride, ...patch },
    });
  };

  const updateFilters = (rows: KeyValueRow[]) => {
    update({ ...settings, filters: rowsToFilters(rows) });
  };

  const addFilterRow = () => updateFilters([...filterRows, { key: '', value: '' }]);
  const setFilterRow = (idx: number, patch: Partial<KeyValueRow>) => {
    const next = filterRows.map((row, i) => (i === idx ? { ...row, ...patch } : row));
    updateFilters(next);
  };
  const removeFilterRow = (idx: number) =>
    updateFilters(filterRows.filter((_, i) => i !== idx));

  return (
    <div className="atlas-v5-settings flex flex-col gap-3" data-testid="atlas-v5-settings">
      <div className="font-semibold text-sm">Atlas v5 RetrievalProfile</div>
      <div className="grid grid-cols-2 gap-2">
        <label className="flex flex-col gap-1 text-xs">
          TopK
          <input
            type="number"
            min={1}
            max={50}
            disabled={readonly}
            value={profile.topK ?? 5}
            onChange={(e) => updateProfile({ topK: Number(e.target.value) })}
            className="border rounded px-2 py-1"
          />
        </label>
        <label className="flex flex-col gap-1 text-xs">
          MinScore
          <input
            type="number"
            min={0}
            max={1}
            step={0.05}
            disabled={readonly}
            value={profile.minScore ?? 0}
            onChange={(e) => updateProfile({ minScore: Number(e.target.value) })}
            className="border rounded px-2 py-1"
          />
        </label>
        <label className="flex items-center gap-2 text-xs">
          <input
            type="checkbox"
            disabled={readonly}
            checked={Boolean(profile.enableRerank)}
            onChange={(e) => updateProfile({ enableRerank: e.target.checked })}
          />
          enableRerank
        </label>
        <label className="flex items-center gap-2 text-xs">
          <input
            type="checkbox"
            disabled={readonly}
            checked={Boolean(profile.enableHybrid ?? true)}
            onChange={(e) => updateProfile({ enableHybrid: e.target.checked })}
          />
          enableHybrid
        </label>
        <label className="flex items-center gap-2 text-xs">
          <input
            type="checkbox"
            disabled={readonly}
            checked={Boolean(profile.enableQueryRewrite)}
            onChange={(e) => updateProfile({ enableQueryRewrite: e.target.checked })}
          />
          enableQueryRewrite
        </label>
        <label className="flex flex-col gap-1 text-xs">
          rerankModel
          <input
            type="text"
            disabled={readonly}
            value={profile.rerankModel ?? ''}
            onChange={(e) => updateProfile({ rerankModel: e.target.value })}
            className="border rounded px-2 py-1"
            placeholder="bge-reranker-v2 / cross-encoder-..."
          />
        </label>
      </div>
      <div className="text-xs text-gray-600">混合检索权重 (vector / bm25 / table / image)</div>
      <div className="grid grid-cols-4 gap-2">
        {(['vector', 'bm25', 'table', 'image'] as const).map((k) => (
          <input
            key={k}
            type="number"
            step={0.1}
            min={0}
            max={1}
            disabled={readonly}
            value={(weights[k] as number | undefined) ?? (k === 'vector' ? 0.6 : k === 'bm25' ? 0.4 : 0)}
            onChange={(e) => updateWeights({ [k]: Number(e.target.value) } as never)}
            className="border rounded px-2 py-1 text-xs"
            placeholder={k}
          />
        ))}
      </div>

      <div className="font-semibold text-sm mt-2">Filters (Metadata key/value)</div>
      <div className="flex flex-col gap-1">
        {filterRows.map((row, idx) => (
          <div key={idx} className="flex gap-2">
            <input
              className="border rounded px-2 py-1 text-xs flex-1"
              placeholder="key (tag/namespace/...)"
              value={row.key}
              disabled={readonly}
              onChange={(e) => setFilterRow(idx, { key: e.target.value })}
            />
            <input
              className="border rounded px-2 py-1 text-xs flex-1"
              placeholder="value"
              value={row.value}
              disabled={readonly}
              onChange={(e) => setFilterRow(idx, { value: e.target.value })}
            />
            <button
              type="button"
              className="text-xs text-red-500"
              disabled={readonly}
              onClick={() => removeFilterRow(idx)}
            >
              移除
            </button>
          </div>
        ))}
        <button
          type="button"
          className="text-xs text-blue-500 self-start"
          disabled={readonly}
          onClick={addFilterRow}
        >
          + 添加 filter
        </button>
      </div>

      <div className="font-semibold text-sm mt-2">CallerContextOverride</div>
      <div className="grid grid-cols-2 gap-2">
        <label className="flex flex-col gap-1 text-xs">
          callerType
          <select
            disabled={readonly}
            value={callerOverride.callerType ?? 2}
            onChange={(e) => updateOverride({ callerType: Number(e.target.value) })}
            className="border rounded px-2 py-1"
          >
            {CALLER_TYPE_LABELS.map((opt) => (
              <option key={opt.value} value={opt.value}>{opt.label}</option>
            ))}
          </select>
        </label>
        <label className="flex flex-col gap-1 text-xs">
          preset
          <select
            disabled={readonly}
            value={callerOverride.preset ?? 1}
            onChange={(e) => updateOverride({ preset: Number(e.target.value) })}
            className="border rounded px-2 py-1"
          >
            {PRESET_LABELS.map((opt) => (
              <option key={opt.value} value={opt.value}>{opt.label}</option>
            ))}
          </select>
        </label>
        <label className="flex flex-col gap-1 text-xs">
          callerId
          <input
            type="text"
            disabled={readonly}
            value={callerOverride.callerId ?? ''}
            onChange={(e) => updateOverride({ callerId: e.target.value })}
            className="border rounded px-2 py-1"
          />
        </label>
        <label className="flex flex-col gap-1 text-xs">
          userId
          <input
            type="text"
            disabled={readonly}
            value={callerOverride.userId ?? ''}
            onChange={(e) => updateOverride({ userId: e.target.value })}
            className="border rounded px-2 py-1"
          />
        </label>
      </div>

      <label className="flex items-center gap-2 text-xs mt-2">
        <input
          type="checkbox"
          disabled={readonly}
          checked={Boolean(settings.debug)}
          onChange={(e) => update({ ...settings, debug: e.target.checked })}
        />
        debug（返回完整 traceId / finalContext / candidates / metadata）
      </label>
    </div>
  );
};

export const AtlasV5SettingsField = withField(Settings);
