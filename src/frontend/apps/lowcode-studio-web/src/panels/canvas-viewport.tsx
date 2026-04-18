import React, { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Empty, Spin, Tag, Typography, Banner } from '@douyinfe/semi-ui';
import type { AppSchema, ComponentSchema, PageSchema } from '@atlas/lowcode-schema';
import { lowcodeApi } from '../services/api-core';
import { useStudioSelection } from '../stores/selection-store';

/**
 * 设计期画布（M07 C07-2 / C07-8 / C07-9）。
 *
 * 渲染策略：
 *  - 从 GET /apps/{appId}/draft 拉取 schema，反序列化为 AppSchema
 *  - 取当前 currentPageCode 对应的 PageSchema（默认第一个）
 *  - 递归渲染 ComponentSchema 树：以"线框预览"形式（不真正执行 props 绑定）
 *  - 点击组件 → 写入 useStudioSelection.selectedComponentId（右侧 inspector 联动）
 *
 * 这是设计期"线框预览"，不调用任何运行时 dispatch；真实渲染由 lowcode-runtime-web
 * 在 lowcode-preview-web (M08) 装配，与本视口完全隔离。
 */
export const CanvasViewport: React.FC<{ appId: string }> = ({ appId }) => {
  const draftQuery = useQuery({
    queryKey: ['lowcode-draft', appId],
    queryFn: () => lowcodeApi.apps.getDraft(appId)
  });
  const { selectedComponentId, setSelectedComponentId, currentPageCode } = useStudioSelection();

  const { app, page, parseError } = useMemo(() => {
    const raw = draftQuery.data?.schemaJson;
    if (!raw) return { app: null, page: null, parseError: null as string | null };
    try {
      const a = JSON.parse(raw) as AppSchema;
      const p = (a.pages ?? []).find((x) => x.code === currentPageCode) ?? a.pages?.[0] ?? null;
      return { app: a, page: p, parseError: null };
    } catch (e) {
      return { app: null, page: null, parseError: (e as Error).message };
    }
  }, [draftQuery.data, currentPageCode]);

  if (draftQuery.isLoading) return <Spin style={{ marginTop: 80 }} />;
  if (draftQuery.error) return <Banner type="danger" description={`加载草稿失败：${(draftQuery.error as Error).message}`} />;
  if (parseError) return <Banner type="danger" description={`schema 解析失败：${parseError}`} />;
  if (!app || !page) return <Empty title="该应用暂无页面" description="请在左侧 结构 Tab 创建页面后再返回画布" />;

  return (
    <div style={{ padding: 24, height: '100%', overflow: 'auto', background: '#fafafa' }}>
      <Typography.Title heading={6} style={{ margin: '0 0 12px' }}>
        {page.displayName} <Tag size="small" style={{ marginLeft: 8 }}>{page.path}</Tag>
      </Typography.Title>
      <ComponentNode
        node={page.root}
        selectedId={selectedComponentId}
        onSelect={setSelectedComponentId}
        depth={0}
      />
    </div>
  );
};

interface ComponentNodeProps {
  node: ComponentSchema;
  selectedId: string | null;
  onSelect: (id: string) => void;
  depth: number;
}

const ComponentNode: React.FC<ComponentNodeProps> = ({ node, selectedId, onSelect, depth }) => {
  const isSelected = selectedId === node.id;
  const isHidden = node.visible === false;
  const display = (node.metadata && typeof node.metadata['displayName'] === 'string')
    ? (node.metadata['displayName'] as string)
    : node.type;

  return (
    <div
      role="button"
      tabIndex={0}
      onClick={(e) => { e.stopPropagation(); onSelect(node.id); }}
      onKeyDown={(e) => { if (e.key === 'Enter') { e.stopPropagation(); onSelect(node.id); } }}
      style={{
        marginLeft: depth * 12,
        marginBottom: 8,
        padding: '8px 12px',
        background: isHidden ? 'repeating-linear-gradient(45deg, #fafafa, #fafafa 6px, #f0f0f0 6px, #f0f0f0 12px)' : '#fff',
        border: isSelected ? '2px solid #1677ff' : '1px dashed #d8d8d8',
        borderRadius: 4,
        cursor: 'pointer',
        opacity: isHidden ? 0.6 : 1
      }}
      data-component-id={node.id}
    >
      <Typography.Text strong style={{ fontSize: 13 }}>{display}</Typography.Text>
      <Tag size="small" style={{ marginLeft: 8 }}>{node.type}</Tag>
      {node.locked && <Tag size="small" color="amber" style={{ marginLeft: 4 }}>locked</Tag>}
      {(node.children ?? []).length > 0 && (
        <div style={{ marginTop: 8 }}>
          {(node.children ?? []).map((c) => (
            <ComponentNode key={c.id} node={c} selectedId={selectedId} onSelect={onSelect} depth={depth + 1} />
          ))}
        </div>
      )}
    </div>
  );
};
