import React, { useMemo, useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Tabs, TabPane, Input, List, Typography, Empty, Spin, Tag, Button, Toast, Modal, Form, Space, Select } from '@douyinfe/semi-ui';
import { listShortcuts } from '@atlas/lowcode-editor-canvas';
import { lowcodeApi, type AppVariable } from '../services/api-core';
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
  const qc = useQueryClient();
  const [pageOpen, setPageOpen] = useState(false);
  const [varOpen, setVarOpen] = useState(false);

  const createPageMut = useMutation({
    mutationFn: (vals: { code: string; displayName: string; path: string; targetType?: string; layout?: string }) =>
      lowcodeApi.pages.create(appId, vals),
    onSuccess: () => {
      Toast.success(t('lowcode_studio.common.created'));
      setPageOpen(false);
      qc.invalidateQueries({ queryKey: ['lowcode-pages', appId] });
    },
    onError: (e: Error) => Toast.error(e.message)
  });

  const deletePageMut = useMutation({
    mutationFn: (pageId: string) => lowcodeApi.pages.delete(appId, pageId),
    onSuccess: () => {
      Toast.success(t('lowcode_studio.common.deleted'));
      qc.invalidateQueries({ queryKey: ['lowcode-pages', appId] });
      qc.invalidateQueries({ queryKey: ['lowcode-draft', appId] });
    },
    onError: (e: Error) => Toast.error(e.message)
  });

  const deleteVarMut = useMutation({
    mutationFn: (variableId: string) => lowcodeApi.variables.delete(appId, variableId),
    onSuccess: () => {
      Toast.success(t('lowcode_studio.common.deleted'));
      qc.invalidateQueries({ queryKey: ['lowcode-variables', appId] });
    },
    onError: (e: Error) => Toast.error(e.message)
  });

  const createVarMut = useMutation({
    mutationFn: (vals: { code: string; displayName: string; scope: string; valueType: string; defaultValueJson?: string; description?: string }) =>
      lowcodeApi.variables.create(appId, {
        // 后端会忽略 id/appId 字段，前端传完整壳即可
        id: '',
        appId,
        code: vals.code,
        displayName: vals.displayName,
        scope: vals.scope,
        valueType: vals.valueType,
        isReadOnly: false,
        isPersisted: false,
        defaultValueJson: vals.defaultValueJson || 'null',
        description: vals.description
      } as unknown as AppVariable),
    onSuccess: () => {
      Toast.success(t('lowcode_studio.common.created'));
      setVarOpen(false);
      qc.invalidateQueries({ queryKey: ['lowcode-variables', appId] });
    },
    onError: (e: Error) => Toast.error(e.message)
  });

  // M07 模板应用：apply 后端返 templateJson → 提示用户确认 → 写入 draft
  const applyTplMut = useMutation({
    mutationFn: async (templateId: string) => {
      const r = await lowcodeApi.templates.apply(templateId);
      return r;
    },
    onSuccess: async (r) => {
      Modal.confirm({
        title: t('lowcode_studio.common.applyTemplateConfirm'),
        content: `${r.templateJson.length} 字节 · ${t('lowcode_studio.common.unrecoverable')}`,
        okText: t('lowcode_studio.common.confirm'),
        cancelText: t('lowcode_studio.common.cancel'),
        onOk: async () => {
          try {
            await lowcodeApi.apps.replaceDraft(appId, r.templateJson);
            await qc.invalidateQueries({ queryKey: ['lowcode-draft', appId] });
            await qc.invalidateQueries({ queryKey: ['lowcode-pages', appId] });
            Toast.success(t('lowcode_studio.common.applied'));
          } catch (e) {
            Toast.error((e as Error).message);
          }
        }
      });
    },
    onError: (e: Error) => Toast.error(e.message)
  });

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
              <List.Item
                header={<Typography.Text strong>{tpl.name}</Typography.Text>}
                extra={
                  <span style={{ display: 'inline-flex', gap: 6, alignItems: 'center' }}>
                    <Tag size="small">{tpl.kind}</Tag>
                    <Button size="small" loading={applyTplMut.isPending} onClick={() => applyTplMut.mutate(tpl.id)}>应用</Button>
                  </span>
                }
              >
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
        <Button size="small" style={{ marginBottom: 8 }} onClick={() => setPageOpen(true)}>＋ 新增页面</Button>
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
                  extra={
                    <Space>
                      <Tag size="small">{p.layout}</Tag>
                      <Button
                        size="small"
                        type="danger"
                        loading={deletePageMut.isPending}
                        onClick={(ev) => {
                          ev.stopPropagation();
                          Modal.confirm({
                            title: t('lowcode_studio.pages.delete'),
                            content: `${p.displayName} (${p.code}) · ${t('lowcode_studio.common.unrecoverable')}`,
                            okText: t('lowcode_studio.pages.delete'),
                            cancelText: t('lowcode_studio.common.cancel'),
                            onOk: () => deletePageMut.mutate(p.id)
                          });
                        }}
                      >删除</Button>
                    </Space>
                  }
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
        <Button size="small" style={{ marginBottom: 8 }} onClick={() => setVarOpen(true)}>＋ 新增变量</Button>
        {variablesQuery.isLoading ? <Spin /> : (
          <List
            size="small"
            dataSource={variablesQuery.data ?? []}
            renderItem={(v) => (
              <List.Item extra={
                <Space>
                  <Tag size="small">{v.scope}</Tag>
                  <Button
                    size="small"
                    type="danger"
                    loading={deleteVarMut.isPending}
                    onClick={() => Modal.confirm({
                      title: `${t('lowcode_studio.common.delete')} · ${v.displayName}`,
                      content: `${v.code} · ${t('lowcode_studio.common.unrecoverable')}`,
                      okText: t('lowcode_studio.common.delete'),
                      cancelText: t('lowcode_studio.common.cancel'),
                      onOk: () => deleteVarMut.mutate(v.id)
                    })}
                  >{t('lowcode_studio.common.delete')}</Button>
                </Space>
              }>
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
      <Modal title={t('lowcode_studio.pages.add')} visible={pageOpen} onCancel={() => setPageOpen(false)} footer={null}>
        <Form onSubmit={(vals) => createPageMut.mutate(vals as { code: string; displayName: string; path: string; targetType?: string; layout?: string })}>
          <Form.Input field="code" label={t('lowcode_studio.app.code')} rules={[{ required: true, pattern: /^[a-zA-Z][a-zA-Z0-9_-]{0,127}$/ }]} />
          <Form.Input field="displayName" label={t('lowcode_studio.app.displayName')} rules={[{ required: true }]} />
          <Form.Input field="path" label={t('lowcode_studio.pages.path')} initValue="/" rules={[{ required: true }]} />
          <Form.Select field="targetType" label={t('lowcode_studio.pages.targetType')} initValue="web" optionList={[{ label: 'web', value: 'web' }, { label: 'mini-wx', value: 'mini-wx' }, { label: 'mini-douyin', value: 'mini-douyin' }, { label: 'h5', value: 'h5' }]} />
          <Form.Select field="layout" label={t('lowcode_studio.pages.layout')} initValue="default" optionList={[{ label: 'default', value: 'default' }, { label: 'blank', value: 'blank' }, { label: 'split', value: 'split' }]} />
          <Form.Slot>
            <Space>
              <Button htmlType="submit" type="primary" loading={createPageMut.isPending}>提交</Button>
              <Button onClick={() => setPageOpen(false)}>取消</Button>
            </Space>
          </Form.Slot>
        </Form>
      </Modal>

      <Modal title={t('lowcode_studio.variables.add')} visible={varOpen} onCancel={() => setVarOpen(false)} footer={null}>
        <Form onSubmit={(vals) => createVarMut.mutate(vals as { code: string; displayName: string; scope: string; valueType: string; defaultValueJson?: string; description?: string })}>
          <Form.Input field="code" label={t('lowcode_studio.app.code')} rules={[{ required: true, pattern: /^[a-zA-Z][a-zA-Z0-9_-]{0,127}$/ }]} />
          <Form.Input field="displayName" label={t('lowcode_studio.app.displayName')} rules={[{ required: true }]} />
          <Form.Select field="scope" label="作用域" initValue="page" optionList={[
            { label: t('lowcode_studio.variables.scope.page'), value: 'page' },
            { label: t('lowcode_studio.variables.scope.app'), value: 'app' },
            { label: t('lowcode_studio.variables.scope.system'), value: 'system' }
          ]} />
          <Form.Select field="valueType" label={t('lowcode_studio.variables.valueType')} initValue="string" optionList={[
            { label: 'string', value: 'string' }, { label: 'number', value: 'number' }, { label: 'boolean', value: 'boolean' },
            { label: 'object', value: 'object' }, { label: 'array', value: 'array' }, { label: 'any', value: 'any' }
          ]} />
          <Form.TextArea field="defaultValueJson" label="默认值（JSON）" placeholder="null / 0 / {} / []" />
          <Form.TextArea field="description" label={t('lowcode_studio.app.description')} />
          <Form.Slot>
            <Space>
              <Button htmlType="submit" type="primary" loading={createVarMut.isPending}>提交</Button>
              <Button onClick={() => setVarOpen(false)}>取消</Button>
            </Space>
          </Form.Slot>
        </Form>
      </Modal>
    </Tabs>
  );
};
