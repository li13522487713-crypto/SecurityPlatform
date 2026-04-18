import React, { useMemo } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Empty, Spin, Typography, Tag, List, Banner, Space, Switch, Toast, Button, Modal } from '@douyinfe/semi-ui';
import type { AppSchema, ComponentSchema } from '@atlas/lowcode-schema';
import { lowcodeApi, type ComponentMetaWire } from '../services/api-core';
import { useStudioSelection } from '../stores/selection-store';
import { t } from '../i18n';

/**
 * 右侧三 Tab 检视器（M07 C07-3）。
 *
 * - property：列出选中组件的 props key/value（区分 binding vs 字面值）
 * - style：从 props 中筛选 className / style / theme 相关项展示
 * - events：列出 events 数组，每条事件含 actions 链概要
 *
 * 真实属性表单由 @atlas/lowcode-property-forms 在 M07 完整装配阶段接入；本检视器
 * 提供"元数据驱动只读视图"，确保 PLAN.md §M07 C07-3 三 Tab 形态完整可见。
 */
export const RightInspector: React.FC<{ appId: string; kind: 'property' | 'style' | 'events' }> = ({ appId, kind }) => {
  const { selectedComponentId, setSelectedComponentId } = useStudioSelection();
  const qc = useQueryClient();
  const draftQuery = useQuery({
    queryKey: ['lowcode-draft', appId],
    queryFn: () => lowcodeApi.apps.getDraft(appId)
  });
  const registryQuery = useQuery({
    queryKey: ['lowcode-components', 'web'],
    queryFn: () => lowcodeApi.components.registry('web')
  });

  /**
   * 把当前选中节点的部分字段（visible / locked）应用回 schema 并 autosave 触发 HMR。
   * 走 GET draft → 改 → POST autosave 的最小可逆方式（不改 ComponentSchema 其它字段）。
   */
  const patchMut = useMutation({
    mutationFn: async (patch: { visible?: boolean; locked?: boolean }) => {
      if (!selectedComponentId || !draftQuery.data) throw new Error('未选中或未加载草稿');
      const app = JSON.parse(draftQuery.data.schemaJson) as AppSchema;
      let touched = false;
      for (const page of app.pages ?? []) {
        if (mutateById(page.root, selectedComponentId, (n) => {
          if (patch.visible !== undefined) n.visible = patch.visible;
          if (patch.locked !== undefined) n.locked = patch.locked;
        })) {
          touched = true;
          break;
        }
      }
      if (!touched) throw new Error('节点未在任意页面命中');
      await lowcodeApi.apps.autosave(appId, JSON.stringify(app));
    },
    onSuccess: async () => {
      Toast.success(t('lowcode_studio.common.applied'));
      await qc.invalidateQueries({ queryKey: ['lowcode-draft', appId] });
    },
    onError: (e: Error) => Toast.error(e.message)
  });

  /** 从 schema 中删除当前选中节点（不允许删除根）。*/
  const deleteMut = useMutation({
    mutationFn: async () => {
      if (!selectedComponentId || !draftQuery.data) throw new Error('未选中或未加载草稿');
      const app = JSON.parse(draftQuery.data.schemaJson) as AppSchema;
      let removed = false;
      for (const page of app.pages ?? []) {
        if (page.root.id === selectedComponentId) {
          throw new Error('禁止删除页面根节点');
        }
        if (deleteById(page.root, selectedComponentId)) {
          removed = true;
          break;
        }
      }
      if (!removed) throw new Error('节点未在任意页面命中');
      await lowcodeApi.apps.autosave(appId, JSON.stringify(app));
    },
    onSuccess: async () => {
      Toast.success(t('lowcode_studio.common.deleted'));
      setSelectedComponentId(null);
      await qc.invalidateQueries({ queryKey: ['lowcode-draft', appId] });
    },
    onError: (e: Error) => Toast.error(e.message)
  });

  const node = useMemo(() => {
    if (!selectedComponentId || !draftQuery.data) return null;
    try {
      const app = JSON.parse(draftQuery.data.schemaJson) as AppSchema;
      for (const page of app.pages ?? []) {
        const found = findById(page.root, selectedComponentId);
        if (found) return found;
      }
    } catch {
      return null;
    }
    return null;
  }, [selectedComponentId, draftQuery.data]);

  const meta = useMemo(() => {
    if (!node) return null;
    return registryQuery.data?.components.find((c) => c.type === node.type) ?? null;
  }, [node, registryQuery.data]);

  if (draftQuery.isLoading) return <Spin style={{ margin: 24 }} />;
  if (draftQuery.error) return <Banner type="danger" description={(draftQuery.error as Error).message} />;
  if (!node) return <Empty title={t('lowcode_studio.layout.right.property')} description="选中画布中的组件后显示属性面板" />;

  return (
    <div style={{ padding: 12 }}>
      <Space style={{ marginBottom: 12, width: '100%', justifyContent: 'space-between' }}>
        <Space>
          <Typography.Text strong>{node.type}</Typography.Text>
          <Tag size="small">{node.id.slice(0, 12)}…</Tag>
          {meta && <Tag size="small" color="blue">{meta.category}</Tag>}
        </Space>
        <Button
          size="small"
          type="danger"
          loading={deleteMut.isPending}
          onClick={() => Modal.confirm({
            title: t('lowcode_studio.common.delete'),
            content: `${t('lowcode_studio.common.unrecoverable')}：${node.type} (${node.id.slice(0, 12)}…)`,
            okText: t('lowcode_studio.common.delete'),
            cancelText: t('lowcode_studio.common.cancel'),
            onOk: () => deleteMut.mutate()
          })}
        >
          {t('lowcode_studio.common.delete')}
        </Button>
      </Space>
      {kind === 'property' && (
        <>
          <Space style={{ marginBottom: 8 }}>
            <Typography.Text type="tertiary" style={{ fontSize: 12 }}>{t('lowcode_studio.common.show')}</Typography.Text>
            <Switch
              size="small"
              checked={node.visible !== false}
              loading={patchMut.isPending}
              onChange={(v) => patchMut.mutate({ visible: v })}
            />
            <Typography.Text type="tertiary" style={{ fontSize: 12, marginLeft: 8 }}>{t('lowcode_studio.common.lock')}</Typography.Text>
            <Switch
              size="small"
              checked={node.locked === true}
              loading={patchMut.isPending}
              onChange={(v) => patchMut.mutate({ locked: v })}
            />
          </Space>
          <PropertyView node={node} meta={meta} filter="all" />
        </>
      )}
      {kind === 'style' && <PropertyView node={node} meta={meta} filter="style" />}
      {kind === 'events' && <EventsView node={node} meta={meta} />}
    </div>
  );
};

/** 从子节点列表中递归删除目标节点。返回是否成功删除。*/
function deleteById(node: ComponentSchema, id: string): boolean {
  if (node.children) {
    const idx = node.children.findIndex((c) => c.id === id);
    if (idx >= 0) {
      node.children.splice(idx, 1);
      return true;
    }
    for (const c of node.children) {
      if (deleteById(c, id)) return true;
    }
  }
  if (node.slots) {
    for (const list of Object.values(node.slots)) {
      const idx = list.findIndex((c) => c.id === id);
      if (idx >= 0) {
        list.splice(idx, 1);
        return true;
      }
      for (const c of list) {
        if (deleteById(c, id)) return true;
      }
    }
  }
  return false;
}

/** 在 ComponentSchema 树中按 id 找到节点并执行 mutation（in-place 修改）。返回是否命中。*/
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


const STYLE_KEYS = new Set(['className', 'class', 'style', 'theme', 'size', 'color', 'width', 'height', 'padding', 'margin', 'background']);

const PropertyView: React.FC<{ node: ComponentSchema; meta: ComponentMetaWire | null; filter: 'all' | 'style' }> = ({ node, meta, filter }) => {
  const entries = Object.entries(node.props ?? {}).filter(([k]) => filter === 'all' ? !STYLE_KEYS.has(k) : STYLE_KEYS.has(k));
  // 元数据驱动：列出已声明的 bindableProps 中尚未使用的项（提示用户可绑定哪些 prop）
  const usedKeys = new Set(Object.keys(node.props ?? {}));
  const unusedBindable = meta && filter === 'all'
    ? meta.bindableProps.filter((p) => !usedKeys.has(p) && !STYLE_KEYS.has(p))
    : [];

  if (entries.length === 0 && unusedBindable.length === 0) {
    return <Empty title={filter === 'style' ? '无样式属性' : '无业务属性'} />;
  }

  return (
    <>
      {entries.length > 0 && (
        <List
          size="small"
          dataSource={entries}
          renderItem={([k, v]) => {
            const isBinding = isBindingValue(v);
            const inMeta = meta?.bindableProps.includes(k) ?? false;
            return (
              <List.Item extra={
                <Space>
                  {isBinding ? <Tag color="blue" size="small">binding</Tag> : <Tag color="grey" size="small">literal</Tag>}
                  {!inMeta && filter === 'all' && <Tag color="amber" size="small">未声明</Tag>}
                </Space>
              }>
                <div style={{ width: '100%' }}>
                  <Typography.Text strong style={{ fontSize: 12 }}>{k}</Typography.Text>
                  <pre style={{ margin: 4, padding: 6, background: '#f7f7f9', borderRadius: 3, fontSize: 11, maxHeight: 120, overflow: 'auto' }}>
                    {JSON.stringify(v, null, 2)}
                  </pre>
                </div>
              </List.Item>
            );
          }}
        />
      )}
      {unusedBindable.length > 0 && (
        <div style={{ marginTop: 12 }}>
          <Typography.Text type="tertiary" style={{ fontSize: 12 }}>可绑定但尚未使用的 prop：</Typography.Text>
          <Space wrap style={{ marginTop: 6 }}>
            {unusedBindable.map((p) => <Tag key={p} size="small" color="grey">{p}</Tag>)}
          </Space>
        </div>
      )}
    </>
  );
};

const EventsView: React.FC<{ node: ComponentSchema; meta: ComponentMetaWire | null }> = ({ node, meta }) => {
  const events = node.events ?? [];
  const usedNames = new Set<string>(events.map((e) => String(e.name)));
  const unusedEvents = meta ? meta.supportedEvents.filter((e) => !usedNames.has(e)) : [];

  if (events.length === 0 && unusedEvents.length === 0) return <Empty title="该组件未绑定事件" />;

  return (
    <>
      <List
        size="small"
        dataSource={events}
        renderItem={(ev) => (
          <List.Item>
            <div style={{ width: '100%' }}>
              <Space style={{ marginBottom: 4 }}>
                <Typography.Text strong>{ev.name}</Typography.Text>
                <Tag size="small">{(ev.actions ?? []).length} actions</Tag>
              </Space>
              <List
                size="small"
                dataSource={ev.actions ?? []}
                renderItem={(a) => (
                  <List.Item style={{ paddingLeft: 12 }}>
                    <Tag size="small" color="amber">{a.kind}</Tag>
                    {a.id && <Typography.Text type="tertiary" style={{ marginLeft: 6, fontSize: 11 }}>id={a.id}</Typography.Text>}
                    {a.when && <Typography.Text type="tertiary" style={{ marginLeft: 6, fontSize: 11 }}>when={a.when}</Typography.Text>}
                  </List.Item>
                )}
              />
            </div>
          </List.Item>
        )}
      />
      {unusedEvents.length > 0 && (
        <div style={{ marginTop: 12 }}>
          <Typography.Text type="tertiary" style={{ fontSize: 12 }}>支持但尚未绑定的事件：</Typography.Text>
          <Space wrap style={{ marginTop: 6 }}>
            {unusedEvents.map((e) => <Tag key={e} size="small" color="grey">{e}</Tag>)}
          </Space>
        </div>
      )}
    </>
  );
};

function findById(node: ComponentSchema, id: string): ComponentSchema | null {
  if (node.id === id) return node;
  for (const c of node.children ?? []) {
    const r = findById(c, id);
    if (r) return r;
  }
  for (const list of Object.values(node.slots ?? {})) {
    for (const c of list) {
      const r = findById(c, id);
      if (r) return r;
    }
  }
  return null;
}

function isBindingValue(v: unknown): boolean {
  return typeof v === 'object' && v !== null && 'kind' in (v as Record<string, unknown>) && typeof (v as Record<string, unknown>).kind === 'string';
}
