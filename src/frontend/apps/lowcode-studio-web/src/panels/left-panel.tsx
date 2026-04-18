import React, { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Tabs, TabPane, Input, List, Typography, Empty, Spin, Tag } from '@douyinfe/semi-ui';
import { listShortcuts } from '@atlas/lowcode-editor-canvas';
import { lowcodeApi } from '../services/api-core';
import { useStudioSelection } from '../stores/selection-store';
import { t } from '../i18n';

/**
 * 左侧 5 Tab 面板（M07 C07-2 / C07-5 / C07-6 / C07-7）。
 *
 * 全部接通真实后端 API（PlatformHost /api/v1/lowcode/*）：
 *  - 组件 → GET /components/registry
 *  - 模板 → GET /templates
 *  - 结构 → GET /apps/{id}/pages
 *  - 数据 → GET /apps/{id}/variables（page/app/system 三作用域）
 *  - 资源 → GET /apps/{id}/resources（投射模式：8 类资源聚合）
 */
export const LeftPanel: React.FC<{ appId: string }> = ({ appId }) => {
  const [keyword, setKeyword] = useState('');
  const { currentPageCode, setCurrentPageCode } = useStudioSelection();

  // Tab 1：组件
  const componentsQuery = useQuery({
    queryKey: ['lowcode-components', 'web'],
    queryFn: () => lowcodeApi.components.registry('web')
  });
  const filteredComponents = useMemo(() => {
    const all = componentsQuery.data?.components ?? [];
    if (!keyword) return all.slice(0, 60);
    const k = keyword.toLowerCase();
    return all.filter((c) => c.type.toLowerCase().includes(k) || c.displayName.includes(keyword)).slice(0, 60);
  }, [componentsQuery.data, keyword]);

  // Tab 2：模板
  const templatesQuery = useQuery({
    queryKey: ['lowcode-templates', keyword],
    queryFn: () => lowcodeApi.templates.search({ keyword, pageSize: 20 })
  });

  // Tab 3：结构（页面列表）
  const pagesQuery = useQuery({
    queryKey: ['lowcode-pages', appId],
    queryFn: () => lowcodeApi.pages.list(appId)
  });

  // Tab 4：数据（变量列表）
  const variablesQuery = useQuery({
    queryKey: ['lowcode-variables', appId],
    queryFn: () => lowcodeApi.variables.list(appId)
  });

  // Tab 5：资源（投射模式聚合 8 类）
  const resourcesQuery = useQuery({
    queryKey: ['lowcode-resources', appId, keyword],
    queryFn: () => lowcodeApi.resources.search(appId, { keyword, pageSize: 20 })
  });

  return (
    <Tabs tabPosition="left" type="line" defaultActiveKey="components">
      <TabPane tab={t('lowcode_studio.layout.left.components')} itemKey="components">
        <Input prefix="🔍" placeholder="搜索组件" value={keyword} onChange={setKeyword} />
        {componentsQuery.isLoading ? <Spin /> : (
          <List
            size="small"
            style={{ marginTop: 8, maxHeight: 'calc(100vh - 200px)', overflow: 'auto' }}
            dataSource={filteredComponents}
            renderItem={(item) => (
              <List.Item>
                <span
                  draggable
                  onDragStart={(e) => e.dataTransfer.setData('atlas/component-type', item.type)}
                  style={{ cursor: 'grab', display: 'inline-flex', alignItems: 'center' }}
                >
                  <Typography.Text>{item.displayName}</Typography.Text>
                  <Tag size="small" style={{ marginLeft: 8 }}>{item.category}</Tag>
                </span>
              </List.Item>
            )}
            emptyContent={<Empty title="无匹配组件" />}
          />
        )}
      </TabPane>

      <TabPane tab={t('lowcode_studio.layout.left.templates')} itemKey="templates">
        <Input prefix="🔍" placeholder="搜索模板（page / pattern-A..D / industry）" value={keyword} onChange={setKeyword} />
        {templatesQuery.isLoading ? <Spin /> : (
          <List
            size="small"
            style={{ marginTop: 8 }}
            dataSource={templatesQuery.data ?? []}
            renderItem={(tpl) => (
              <List.Item header={<Typography.Text strong>{tpl.name}</Typography.Text>} extra={<Tag size="small">{tpl.kind}</Tag>}>
                <Typography.Paragraph style={{ margin: 0, fontSize: 12, color: '#666' }}>
                  ⭐ {tpl.stars} · 使用 {tpl.useCount}
                </Typography.Paragraph>
              </List.Item>
            )}
            emptyContent={<Empty title="无模板" />}
          />
        )}
      </TabPane>

      <TabPane tab={t('lowcode_studio.layout.left.outline')} itemKey="outline">
        {pagesQuery.isLoading ? <Spin /> : (
          <List
            size="small"
            dataSource={pagesQuery.data ?? []}
            renderItem={(p) => {
              const active = p.code === currentPageCode;
              return (
                <List.Item
                  style={{ cursor: 'pointer', background: active ? '#e6f4ff' : undefined }}
                  onClick={() => setCurrentPageCode(p.code)}
                  extra={<Tag size="small">{p.layout}</Tag>}
                >
                  <Typography.Text strong={active}>{p.displayName}</Typography.Text>
                  <Typography.Text type="tertiary" style={{ marginLeft: 8, fontSize: 12 }}>{p.path}</Typography.Text>
                </List.Item>
              );
            }}
            emptyContent={<Empty title="暂无页面" />}
          />
        )}
      </TabPane>

      <TabPane tab={t('lowcode_studio.layout.left.data')} itemKey="data">
        {variablesQuery.isLoading ? <Spin /> : (
          <List
            size="small"
            dataSource={variablesQuery.data ?? []}
            renderItem={(v) => (
              <List.Item extra={<Tag size="small">{v.scope}</Tag>}>
                <Typography.Text>{v.displayName}</Typography.Text>
                <Typography.Text type="tertiary" style={{ marginLeft: 8, fontSize: 12 }}>{v.code} · {v.valueType}</Typography.Text>
              </List.Item>
            )}
            emptyContent={<Empty title="暂无变量（点击 + 创建 page/app/system 三作用域变量）" />}
          />
        )}
      </TabPane>

      <TabPane tab={t('lowcode_studio.layout.left.resources')} itemKey="resources">
        <Input prefix="🔍" placeholder={t('lowcode_studio.resources.search')} value={keyword} onChange={setKeyword} />
        {resourcesQuery.isLoading ? <Spin /> : (
          <div style={{ marginTop: 8 }}>
            {Object.entries(resourcesQuery.data?.byType ?? {}).map(([type, items]) => (
              <div key={type} style={{ marginBottom: 12 }}>
                <Typography.Text strong style={{ fontSize: 12, color: '#666' }}>{type}</Typography.Text>
                <List
                  size="small"
                  dataSource={items.slice(0, 5)}
                  renderItem={(r) => (
                    <List.Item>
                      <Typography.Text style={{ fontSize: 12 }}>{r.name}</Typography.Text>
                    </List.Item>
                  )}
                />
              </div>
            ))}
          </div>
        )}
        <Typography.Paragraph type="secondary" style={{ marginTop: 12, fontSize: 12 }}>
          快捷键 ≥ {listShortcuts().length} 项；按 Mod+/ 打开面板
        </Typography.Paragraph>
      </TabPane>
    </Tabs>
  );
};
