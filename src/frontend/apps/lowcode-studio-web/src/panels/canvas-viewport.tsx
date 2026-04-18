import React, { useMemo } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Empty, Spin, Tag, Typography, Banner, Toast } from '@douyinfe/semi-ui';
import type { AppSchema, ComponentSchema } from '@atlas/lowcode-schema';
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
  const qc = useQueryClient();
  const { selectedComponentId, setSelectedComponentId, currentPageCode } = useStudioSelection();

  /**
   * 拖入新组件：把 dataTransfer 中的 atlas/component-type 作为新节点 type，
   * 追加到目标父节点的 children 末尾；目标父节点由 onDrop 回调传入（默认 page.root）。
   * 完成后 POST autosave → ILowCodePreviewSignal 触发 HMR。
   */
  const dropMut = useMutation({
    mutationFn: async (vals: { parentId: string; type: string }) => {
      if (!draftQuery.data) throw new Error('草稿未加载');
      const app = JSON.parse(draftQuery.data.schemaJson) as AppSchema;
      let touched = false;
      for (const page of app.pages ?? []) {
        if (mutateById(page.root, vals.parentId, (n) => {
          n.children = n.children ?? [];
          const newId = `c_${Math.random().toString(36).slice(2, 10)}`;
          n.children.push({ id: newId, type: vals.type });
        })) {
          touched = true;
          break;
        }
      }
      if (!touched) throw new Error(`未找到父节点 ${vals.parentId}`);
      await lowcodeApi.apps.autosave(appId, JSON.stringify(app));
    },
    onSuccess: async () => {
      Toast.success('已添加组件');
      await qc.invalidateQueries({ queryKey: ['lowcode-draft', appId] });
    },
    onError: (e: Error) => Toast.error(e.message)
  });

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
        onDropComponent={(parentId, type) => dropMut.mutate({ parentId, type })}
        depth={0}
      />
    </div>
  );
};

/** 在 ComponentSchema 树中按 id 找到节点并执行 mutation；返回是否命中。*/
function mutateById(node: ComponentSchema, id: string, fn: (n: ComponentSchema) => void): boolean {
  if (node.id === id) {
    fn(node);
    return true;
  }
  for (const c of node.children ?? []) {
    if (mutateById(c, id, fn)) return true;
  }
  for (const list of Object.values(node.slots ?? {})) {
    for (const c of list) {
      if (mutateById(c, id, fn)) return true;
    }
  }
  return false;
}

interface ComponentNodeProps {
  node: ComponentSchema;
  selectedId: string | null;
  onSelect: (id: string) => void;
  onDropComponent: (parentId: string, type: string) => void;
  depth: number;
}

const ComponentNode: React.FC<ComponentNodeProps> = ({ node, selectedId, onSelect, onDropComponent, depth }) => {
  const [isDragOver, setIsDragOver] = React.useState(false);
  const isSelected = selectedId === node.id;
  const isHidden = node.visible === false;
  const isLocked = node.locked === true;
  const display = (node.metadata && typeof node.metadata['displayName'] === 'string')
    ? (node.metadata['displayName'] as string)
    : node.type;

  return (
    <div
      role="button"
      tabIndex={0}
      onClick={(e) => { e.stopPropagation(); onSelect(node.id); }}
      onKeyDown={(e) => { if (e.key === 'Enter') { e.stopPropagation(); onSelect(node.id); } }}
      onDragOver={(e) => {
        if (isLocked) return;
        const types = Array.from(e.dataTransfer.types);
        if (!types.includes('atlas/component-type')) return;
        e.preventDefault();
        e.dataTransfer.dropEffect = 'copy';
        if (!isDragOver) setIsDragOver(true);
      }}
      onDragLeave={() => setIsDragOver(false)}
      onDrop={(e) => {
        e.preventDefault();
        e.stopPropagation();
        setIsDragOver(false);
        if (isLocked) return;
        const type = e.dataTransfer.getData('atlas/component-type');
        if (type) onDropComponent(node.id, type);
      }}
      style={{
        marginLeft: depth * 12,
        marginBottom: 8,
        padding: '8px 12px',
        background: isHidden ? 'repeating-linear-gradient(45deg, #fafafa, #fafafa 6px, #f0f0f0 6px, #f0f0f0 12px)' : '#fff',
        border: isDragOver ? '2px solid #52c41a' : isSelected ? '2px solid #1677ff' : '1px dashed #d8d8d8',
        borderRadius: 4,
        cursor: 'pointer',
        opacity: isHidden ? 0.6 : 1
      }}
      data-component-id={node.id}
    >
      <Typography.Text strong style={{ fontSize: 13 }}>{display}</Typography.Text>
      <Tag size="small" style={{ marginLeft: 8 }}>{node.type}</Tag>
      {isLocked && <Tag size="small" color="amber" style={{ marginLeft: 4 }}>locked</Tag>}
      {(node.children ?? []).length > 0 && (
        <div style={{ marginTop: 8 }}>
          {(node.children ?? []).map((c) => (
            <ComponentNode key={c.id} node={c} selectedId={selectedId} onSelect={onSelect} onDropComponent={onDropComponent} depth={depth + 1} />
          ))}
        </div>
      )}
    </div>
  );
};
