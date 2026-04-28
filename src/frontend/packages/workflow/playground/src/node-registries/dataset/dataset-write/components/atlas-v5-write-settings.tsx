/*
 * Atlas v5 §35 / 计划 G7：dataset-write 节点扩展，承载 ParsingStrategy / ChunkingProfile / mode（append/overwrite）。
 *
 * 这些字段独立于 Coze 的 datasetSetting，节点 config 中以 `atlasV5Write` 命名空间存放：
 *   atlasV5Write.parsingStrategy: { parsingType, extractImage, extractTable, imageOcr, ... }
 *   atlasV5Write.chunkingProfile: { mode, size, overlap, separators, indexColumns }
 *   atlasV5Write.mode: 'append' | 'overwrite'
 *
 * 后端 KnowledgeIndexerNodeExecutor 会按这些字段调用 IKnowledgeIndexJobService。
 */

import React from 'react';

import { useField, withField } from '@/form';

const PARSING_TYPE_OPTIONS = [
  { value: 0, label: 'Quick (轻量文本)' },
  { value: 1, label: 'Precise (含图表 OCR)' },
];

const CHUNK_MODE_OPTIONS = [
  { value: 0, label: 'Fixed (定长切片)' },
  { value: 1, label: 'Semantic (语义切片)' },
  { value: 2, label: 'TableRow (表格行)' },
  { value: 3, label: 'ImageItem (图片单元)' },
];

const CAPTION_TYPE_OPTIONS = [
  { value: 0, label: 'AutoVlm (自动 VLM caption)' },
  { value: 1, label: 'Manual' },
  { value: 2, label: 'Filename' },
];

export interface AtlasV5WriteSettings {
  parsingStrategy?: {
    parsingType?: number;
    extractImage?: boolean;
    extractTable?: boolean;
    imageOcr?: boolean;
    filterPages?: string;
    sheetId?: string;
    headerLine?: number;
    dataStartLine?: number;
    rowsCount?: number;
    captionType?: number;
  };
  chunkingProfile?: {
    mode?: number;
    size?: number;
    overlap?: number;
    separators?: string[];
    indexColumns?: string[];
  };
  mode?: 'append' | 'overwrite';
}

const Settings: React.FC = () => {
  const { value, onChange, onBlur, readonly } = useField<AtlasV5WriteSettings>();
  const settings = (value ?? {}) as AtlasV5WriteSettings;
  const parsing = settings.parsingStrategy ?? {};
  const chunking = settings.chunkingProfile ?? {};

  const update = (next: AtlasV5WriteSettings) => {
    onChange(next);
    onBlur?.();
  };
  const updateParsing = (patch: Partial<AtlasV5WriteSettings['parsingStrategy']>) =>
    update({ ...settings, parsingStrategy: { ...parsing, ...patch } });
  const updateChunking = (patch: Partial<AtlasV5WriteSettings['chunkingProfile']>) =>
    update({ ...settings, chunkingProfile: { ...chunking, ...patch } });

  const separatorsText = chunking.separators?.join('\n') ?? '';
  const indexColumnsText = chunking.indexColumns?.join('\n') ?? '';

  return (
    <div className="atlas-v5-write-settings flex flex-col gap-3" data-testid="atlas-v5-write-settings">
      <div className="font-semibold text-sm">Atlas v5 ParsingStrategy</div>
      <div className="grid grid-cols-2 gap-2">
        <label className="flex flex-col gap-1 text-xs">
          parsingType
          <select
            disabled={readonly}
            value={parsing.parsingType ?? 0}
            onChange={(e) => updateParsing({ parsingType: Number(e.target.value) })}
            className="border rounded px-2 py-1"
          >
            {PARSING_TYPE_OPTIONS.map((opt) => (
              <option key={opt.value} value={opt.value}>{opt.label}</option>
            ))}
          </select>
        </label>
        <label className="flex flex-col gap-1 text-xs">
          captionType (image kb)
          <select
            disabled={readonly}
            value={parsing.captionType ?? 0}
            onChange={(e) => updateParsing({ captionType: Number(e.target.value) })}
            className="border rounded px-2 py-1"
          >
            {CAPTION_TYPE_OPTIONS.map((opt) => (
              <option key={opt.value} value={opt.value}>{opt.label}</option>
            ))}
          </select>
        </label>
        <label className="flex items-center gap-2 text-xs">
          <input
            type="checkbox"
            disabled={readonly}
            checked={Boolean(parsing.extractImage)}
            onChange={(e) => updateParsing({ extractImage: e.target.checked })}
          />
          extractImage
        </label>
        <label className="flex items-center gap-2 text-xs">
          <input
            type="checkbox"
            disabled={readonly}
            checked={Boolean(parsing.extractTable)}
            onChange={(e) => updateParsing({ extractTable: e.target.checked })}
          />
          extractTable
        </label>
        <label className="flex items-center gap-2 text-xs">
          <input
            type="checkbox"
            disabled={readonly}
            checked={Boolean(parsing.imageOcr)}
            onChange={(e) => updateParsing({ imageOcr: e.target.checked })}
          />
          imageOcr
        </label>
        <label className="flex flex-col gap-1 text-xs">
          filterPages
          <input
            type="text"
            disabled={readonly}
            value={parsing.filterPages ?? ''}
            placeholder="e.g. 1-5,10,12-20"
            onChange={(e) => updateParsing({ filterPages: e.target.value })}
            className="border rounded px-2 py-1"
          />
        </label>
        <label className="flex flex-col gap-1 text-xs">
          sheetId (table kb)
          <input
            type="text"
            disabled={readonly}
            value={parsing.sheetId ?? ''}
            onChange={(e) => updateParsing({ sheetId: e.target.value })}
            className="border rounded px-2 py-1"
          />
        </label>
        <label className="flex flex-col gap-1 text-xs">
          headerLine
          <input
            type="number"
            min={0}
            disabled={readonly}
            value={parsing.headerLine ?? 0}
            onChange={(e) => updateParsing({ headerLine: Number(e.target.value) })}
            className="border rounded px-2 py-1"
          />
        </label>
        <label className="flex flex-col gap-1 text-xs">
          dataStartLine
          <input
            type="number"
            min={0}
            disabled={readonly}
            value={parsing.dataStartLine ?? 0}
            onChange={(e) => updateParsing({ dataStartLine: Number(e.target.value) })}
            className="border rounded px-2 py-1"
          />
        </label>
        <label className="flex flex-col gap-1 text-xs">
          rowsCount
          <input
            type="number"
            min={0}
            disabled={readonly}
            value={parsing.rowsCount ?? 0}
            onChange={(e) => updateParsing({ rowsCount: Number(e.target.value) })}
            className="border rounded px-2 py-1"
          />
        </label>
      </div>

      <div className="font-semibold text-sm mt-2">Atlas v5 ChunkingProfile</div>
      <div className="grid grid-cols-2 gap-2">
        <label className="flex flex-col gap-1 text-xs">
          mode
          <select
            disabled={readonly}
            value={chunking.mode ?? 0}
            onChange={(e) => updateChunking({ mode: Number(e.target.value) })}
            className="border rounded px-2 py-1"
          >
            {CHUNK_MODE_OPTIONS.map((opt) => (
              <option key={opt.value} value={opt.value}>{opt.label}</option>
            ))}
          </select>
        </label>
        <label className="flex flex-col gap-1 text-xs">
          size
          <input
            type="number"
            min={50}
            max={4000}
            disabled={readonly}
            value={chunking.size ?? 512}
            onChange={(e) => updateChunking({ size: Number(e.target.value) })}
            className="border rounded px-2 py-1"
          />
        </label>
        <label className="flex flex-col gap-1 text-xs">
          overlap
          <input
            type="number"
            min={0}
            max={1000}
            disabled={readonly}
            value={chunking.overlap ?? 64}
            onChange={(e) => updateChunking({ overlap: Number(e.target.value) })}
            className="border rounded px-2 py-1"
          />
        </label>
        <label className="flex flex-col gap-1 text-xs">
          separators (每行一个)
          <textarea
            rows={3}
            disabled={readonly}
            value={separatorsText}
            placeholder={'\\n\\n\n。\n！'}
            onChange={(e) => updateChunking({
              separators: e.target.value.split('\n').filter((s) => s.length > 0),
            })}
            className="border rounded px-2 py-1"
          />
        </label>
        <label className="flex flex-col gap-1 text-xs">
          indexColumns (table kb，每行一个列名)
          <textarea
            rows={3}
            disabled={readonly}
            value={indexColumnsText}
            placeholder={'name\ndepartment'}
            onChange={(e) => updateChunking({
              indexColumns: e.target.value.split('\n').filter((s) => s.length > 0),
            })}
            className="border rounded px-2 py-1"
          />
        </label>
      </div>

      <div className="font-semibold text-sm mt-2">写入模式</div>
      <div className="flex gap-4">
        <label className="flex items-center gap-2 text-xs">
          <input
            type="radio"
            name="atlas-v5-write-mode"
            disabled={readonly}
            checked={(settings.mode ?? 'append') === 'append'}
            onChange={() => update({ ...settings, mode: 'append' })}
          />
          append (保留旧 chunks)
        </label>
        <label className="flex items-center gap-2 text-xs">
          <input
            type="radio"
            name="atlas-v5-write-mode"
            disabled={readonly}
            checked={settings.mode === 'overwrite'}
            onChange={() => update({ ...settings, mode: 'overwrite' })}
          />
          overwrite (先 GC 旧 chunks)
        </label>
      </div>
    </div>
  );
};

export const AtlasV5WriteSettingsField = withField(Settings);
