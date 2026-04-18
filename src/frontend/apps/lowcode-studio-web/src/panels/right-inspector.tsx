import React, { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Empty, Spin, Typography, Tag, List, Banner, Space } from '@douyinfe/semi-ui';
import type { AppSchema, ComponentSchema } from '@atlas/lowcode-schema';
import { lowcodeApi } from '../services/api-core';
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
  const { selectedComponentId } = useStudioSelection();
  const draftQuery = useQuery({
    queryKey: ['lowcode-draft', appId],
    queryFn: () => lowcodeApi.apps.getDraft(appId)
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

  if (draftQuery.isLoading) return <Spin style={{ margin: 24 }} />;
  if (draftQuery.error) return <Banner type="danger" description={(draftQuery.error as Error).message} />;
  if (!node) return <Empty title={t('lowcode_studio.layout.right.property')} description="选中画布中的组件后显示属性面板" />;

  return (
    <div style={{ padding: 12 }}>
      <Space style={{ marginBottom: 12 }}>
        <Typography.Text strong>{node.type}</Typography.Text>
        <Tag size="small">{node.id.slice(0, 12)}…</Tag>
      </Space>
      {kind === 'property' && <PropertyView node={node} filter="all" />}
      {kind === 'style' && <PropertyView node={node} filter="style" />}
      {kind === 'events' && <EventsView node={node} />}
    </div>
  );
};

const STYLE_KEYS = new Set(['className', 'class', 'style', 'theme', 'size', 'color', 'width', 'height', 'padding', 'margin', 'background']);

const PropertyView: React.FC<{ node: ComponentSchema; filter: 'all' | 'style' }> = ({ node, filter }) => {
  const entries = Object.entries(node.props ?? {}).filter(([k]) => filter === 'all' ? !STYLE_KEYS.has(k) : STYLE_KEYS.has(k));
  if (entries.length === 0) {
    return <Empty title={filter === 'style' ? '无样式属性' : '无业务属性'} />;
  }
  return (
    <List
      size="small"
      dataSource={entries}
      renderItem={([k, v]) => {
        const isBinding = isBindingValue(v);
        return (
          <List.Item extra={isBinding ? <Tag color="blue" size="small">binding</Tag> : <Tag color="grey" size="small">literal</Tag>}>
            <div style={{ width: '100%' }}>
              <Typography.Text strong style={{ fontSize: 12 }}>{k}</Typography.Text>
              <pre style={{
                margin: 4,
                padding: 6,
                background: '#f7f7f9',
                borderRadius: 3,
                fontSize: 11,
                maxHeight: 120,
                overflow: 'auto'
              }}>{JSON.stringify(v, null, 2)}</pre>
            </div>
          </List.Item>
        );
      }}
    />
  );
};

const EventsView: React.FC<{ node: ComponentSchema }> = ({ node }) => {
  const events = node.events ?? [];
  if (events.length === 0) return <Empty title="该组件未绑定事件" />;
  return (
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
