import React, { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Banner, Button, Collapse, Empty, Form, Input, List, Modal, Space, Spin, Tag, Toast, Typography } from '@douyinfe/semi-ui';
import { useStudioSelection } from '../stores/selection-store';
import { useLowcodeStudioHost } from '../host';
import type { AppVariable } from '../services/api-core';
import { t } from '../i18n';
import { RuntimeSessionsSheet } from './runtime-sessions-sheet';

/**
 * 业务逻辑模式左侧面板 —— 工作流 / 插件 / 数据 / 设置闭环。
 *
 * - 工作流：读取当前 App 绑定的 workflow 资源，并提供新建入口
 * - 插件：读取绑定关系 + 绑定后的真实资源项，支持绑定 / 解绑 / 打开详情
 * - 数据：复用 lowcode app variables CRUD
 * - 设置：接 runtime sessions，变量项作为数据分组入口别名
 */
export interface WorkflowLeftPanelProps {
  appId: string;
  workspaceId?: string;
}

interface VariableFormValues {
  code: string;
  displayName: string;
  scope: string;
  valueType: string;
  defaultValueJson?: string;
  description?: string;
}

interface VariableUpdateFormValues {
  id: string;
  code: string;
  displayName: string;
  valueType: string;
  isReadOnly: boolean;
  isPersisted: boolean;
  defaultValueJson?: string;
  description?: string;
}

export const WorkflowLeftPanel: React.FC<WorkflowLeftPanelProps> = ({ appId, workspaceId }) => {
  const host = useLowcodeStudioHost();
  const { api, createWorkflow, deleteWorkflow, openPluginDetail } = host;
  const { selectedWorkflowId, setSelectedWorkflowId } = useStudioSelection();
  const qc = useQueryClient();
  const [activeKeys, setActiveKeys] = useState<string[]>(['workflow', 'plugin', 'data', 'settings']);
  const [createOpen, setCreateOpen] = useState(false);
  const [workflowKeyword, setWorkflowKeyword] = useState('');
  const [pluginPickerOpen, setPluginPickerOpen] = useState(false);
  const [pluginKeyword, setPluginKeyword] = useState('');
  const [sessionsOpen, setSessionsOpen] = useState(false);
  const [varOpen, setVarOpen] = useState(false);
  const [editingVariable, setEditingVariable] = useState<AppVariable | null>(null);

  const workflowsQuery = useQuery({
    queryKey: ['lowcode-workflows', appId],
    queryFn: () => api.resources.search(appId, { types: 'workflow', pageSize: 200, boundOnly: true })
  });

  const variablesQuery = useQuery({
    queryKey: ['lowcode-variables', appId],
    queryFn: () => api.variables.list(appId)
  });

  const pluginBindingsQuery = useQuery({
    queryKey: ['lowcode-plugin-bindings', appId],
    queryFn: () => api.resources.listBindings(appId, 'plugin')
  });

  const boundPluginsQuery = useQuery({
    queryKey: ['lowcode-bound-plugins', appId],
    queryFn: () => api.resources.search(appId, { types: 'plugin', pageSize: 200, boundOnly: true })
  });

  const pluginSearchQuery = useQuery({
    queryKey: ['lowcode-plugin-search', appId, pluginKeyword],
    queryFn: () => api.resources.search(appId, { types: 'plugin', keyword: pluginKeyword || undefined, pageSize: 20 }),
    enabled: pluginPickerOpen
  });

  const createWorkflowMut = useMutation({
    mutationFn: async (vals: { name: string; description?: string }) => {
      if (!createWorkflow) throw new Error(t('lowcode_studio.workflow.createUnavailable'));
      return createWorkflow({ appId, workspaceId, name: vals.name, description: vals.description });
    },
    onSuccess: async (result) => {
      Toast.success(t('lowcode_studio.common.created'));
      setCreateOpen(false);
      setSelectedWorkflowId(result.workflowId);
      await qc.invalidateQueries({ queryKey: ['lowcode-workflows', appId] });
    },
    onError: (error: Error) => Toast.error(error.message)
  });

  const deleteWorkflowMut = useMutation({
    mutationFn: async (workflow: { id: string; name: string }) => {
      if (!deleteWorkflow) throw new Error(t('lowcode_studio.workflow.deleteUnavailable'));
      await deleteWorkflow({ appId, workspaceId, workflowId: workflow.id });
      return workflow;
    },
    onSuccess: async (workflow) => {
      Toast.success(t('lowcode_studio.common.deleted'));
      if (selectedWorkflowId === workflow.id) {
        setSelectedWorkflowId('');
      }
      await qc.invalidateQueries({ queryKey: ['lowcode-workflows', appId] });
    },
    onError: (error: Error) => Toast.error(error.message)
  });

  const createVariableMut = useMutation({
    mutationFn: (vals: VariableFormValues) =>
      api.variables.create(appId, {
        id: '',
        appId,
        code: vals.code,
        displayName: vals.displayName,
        scope: vals.scope,
        valueType: vals.valueType,
        isReadOnly: vals.scope === 'system',
        isPersisted: false,
        defaultValueJson: vals.defaultValueJson || 'null',
        description: vals.description
      } as AppVariable),
    onSuccess: async () => {
      Toast.success(t('lowcode_studio.common.created'));
      setVarOpen(false);
      await qc.invalidateQueries({ queryKey: ['lowcode-variables', appId] });
    },
    onError: (error: Error) => Toast.error(error.message)
  });

  const updateVariableMut = useMutation({
    mutationFn: (vals: VariableUpdateFormValues) => {
      const { id, ...rest } = vals;
      return api.variables.update(appId, id, rest);
    },
    onSuccess: async () => {
      setEditingVariable(null);
      Toast.success(t('lowcode_studio.common.success'));
      await qc.invalidateQueries({ queryKey: ['lowcode-variables', appId] });
      // Rename is handled in backend, trigger validation to check for obsolete references
      host?.validationApi?.validate(appId).then((res) => {
        const issues = res.Issues || (res as any).issues || [];
        const obsoleteIssues = issues.filter((i: any) => i.Code === 'obsolete_variable_reference' || i.code === 'obsolete_variable_reference');
        if (obsoleteIssues.length > 0) {
          Toast.warning({
            content: obsoleteIssues[0].Message || obsoleteIssues[0].message,
            duration: 5
          });
        }
      }).catch(() => {});
    },
    onError: (error: Error) => Toast.error(error.message)
  });

  const deleteVariableMut = useMutation({
    mutationFn: (variableId: string) => api.variables.delete(appId, variableId),
    onSuccess: async () => {
      Toast.success(t('lowcode_studio.common.deleted'));
      await qc.invalidateQueries({ queryKey: ['lowcode-variables', appId] });
    },
    onError: (error: Error) => Toast.error(error.message)
  });

  const bindPluginMut = useMutation({
    mutationFn: (resourceId: string | number) => api.resources.bind(appId, { resourceType: 'plugin', resourceId }),
    onSuccess: async () => {
      Toast.success(t('lowcode_studio.common.added'));
      await Promise.all([
        qc.invalidateQueries({ queryKey: ['lowcode-plugin-bindings', appId] }),
        qc.invalidateQueries({ queryKey: ['lowcode-bound-plugins', appId] }),
        qc.invalidateQueries({ queryKey: ['lowcode-plugin-search', appId] })
      ]);
    },
    onError: (error: Error) => Toast.error(error.message)
  });

  const unbindPluginMut = useMutation({
    mutationFn: (resourceId: string | number) => api.resources.unbind(appId, 'plugin', resourceId),
    onSuccess: async () => {
      Toast.success(t('lowcode_studio.plugin.unbound'));
      await Promise.all([
        qc.invalidateQueries({ queryKey: ['lowcode-plugin-bindings', appId] }),
        qc.invalidateQueries({ queryKey: ['lowcode-bound-plugins', appId] }),
        qc.invalidateQueries({ queryKey: ['lowcode-plugin-search', appId] })
      ]);
    },
    onError: (error: Error) => Toast.error(error.message)
  });

  const workflows = useMemo(() => workflowsQuery.data?.byType.workflow ?? [], [workflowsQuery.data]);
  const filteredWorkflows = useMemo(() => {
    const keyword = workflowKeyword.trim().toLowerCase();
    if (!keyword) return workflows;
    return workflows.filter((workflow) => workflow.name.toLowerCase().includes(keyword));
  }, [workflowKeyword, workflows]);
  const boundPluginItems = useMemo(() => {
    return new Map((boundPluginsQuery.data?.byType.plugin ?? []).map((item) => [item.id, item]));
  }, [boundPluginsQuery.data]);
  const boundPlugins = useMemo(() => {
    return (pluginBindingsQuery.data ?? []).map((binding) => ({
      binding,
      item: boundPluginItems.get(String(binding.resourceId))
    }));
  }, [boundPluginItems, pluginBindingsQuery.data]);
  const bindablePlugins = useMemo(() => {
    const boundIds = new Set((pluginBindingsQuery.data ?? []).map((binding) => String(binding.resourceId)));
    return (pluginSearchQuery.data?.byType.plugin ?? []).filter(
      (item) => /^\d+$/.test(item.id) && !boundIds.has(String(item.id))
    );
  }, [pluginBindingsQuery.data, pluginSearchQuery.data]);

  const ensureDataPanelVisible = () => {
    setActiveKeys((prev) => (prev.includes('data') ? prev : [...prev, 'data']));
  };

  const confirmDeleteWorkflow = (workflow: { id: string; name: string }) => {
    Modal.confirm({
      title: t('lowcode_studio.workflow.deleteConfirmTitle'),
      content: `${t('lowcode_studio.workflow.deleteConfirmContent')}：${workflow.name}`,
      okText: t('lowcode_studio.common.delete'),
      cancelText: t('lowcode_studio.common.cancel'),
      okButtonProps: { type: 'danger' },
      onOk: () => deleteWorkflowMut.mutateAsync(workflow)
    });
  };

  return (
    <div style={{ padding: 12, height: '100%', overflow: 'hidden', display: 'flex', flexDirection: 'column' }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 8 }}>
        <Typography.Text strong>{t('lowcode_studio.layout.left.resources')}</Typography.Text>
        <Tag size="small">{t('lowcode_studio.toolbar.modeBusinessLogic')}</Tag>
      </div>

      <Collapse
        style={{ flex: 1, minHeight: 0, overflow: 'hidden' }}
        activeKey={activeKeys}
        onChange={(keys) => setActiveKeys(Array.isArray(keys) ? keys.map(String) : [String(keys)])}
        keepDOM
      >
        <Collapse.Panel
          itemKey="workflow"
          header={<PanelHeader title={t('lowcode_studio.resources.workflows')} count={workflows.length} onAdd={() => setCreateOpen(true)} />}
        >
          {workflowsQuery.isLoading ? <Spin /> : workflows.length === 0 ? (
            <Empty image={null} title={t('lowcode_studio.workflow.empty')} description={t('lowcode_studio.workflow.emptyDescription')} />
          ) : (
            <Space vertical spacing={8} align="start" style={{ width: '100%' }}>
              <Input
                value={workflowKeyword}
                onChange={setWorkflowKeyword}
                placeholder={t('lowcode_studio.workflow.search')}
                showClear
              />
              {filteredWorkflows.length === 0 ? (
                <Empty image={null} title={t('lowcode_studio.common.empty')} />
              ) : (
                <div style={{ width: '100%', maxHeight: 'clamp(180px, calc(100vh - 380px), 320px)', overflowY: 'auto' }}>
                  <List
                    size="small"
                    dataSource={filteredWorkflows}
                    renderItem={(workflow) => {
                      const active = workflow.id === selectedWorkflowId;
                      return (
                        <List.Item
                          style={{
                            cursor: 'pointer',
                            background: active ? '#e6f4ff' : undefined,
                            padding: '6px 8px',
                            minHeight: 38,
                            display: 'flex',
                            alignItems: 'center'
                          }}
                          onClick={() => setSelectedWorkflowId(workflow.id)}
                          extra={active && deleteWorkflow ? (
                            <Button
                              size="small"
                              theme="borderless"
                              type="danger"
                              loading={deleteWorkflowMut.isPending}
                              onClick={(event) => {
                                event.stopPropagation();
                                confirmDeleteWorkflow(workflow);
                              }}
                            >
                              {t('lowcode_studio.common.delete')}
                            </Button>
                          ) : null}
                        >
                          <Typography.Text strong={active} ellipsis={{ showTooltip: true }} style={{ fontSize: 13, maxWidth: active ? 150 : 210 }}>
                            ⚙ {workflow.name}
                          </Typography.Text>
                        </List.Item>
                      );
                    }}
                  />
                </div>
              )}
            </Space>
          )}
        </Collapse.Panel>

        <Collapse.Panel
          itemKey="plugin"
          header={<PanelHeader title={t('lowcode_studio.resources.plugins')} count={boundPlugins.length} onAdd={() => setPluginPickerOpen(true)} />}
        >
          {pluginBindingsQuery.isLoading || boundPluginsQuery.isLoading ? <Spin /> : boundPlugins.length === 0 ? (
            <Empty image={null} title={t('lowcode_studio.plugin.empty')} />
          ) : (
            <List
              size="small"
              dataSource={boundPlugins}
              renderItem={({ binding, item }) => (
                <List.Item
                  style={{ padding: '6px 8px' }}
                  extra={(
                    <Space>
                      {openPluginDetail ? (
                        <Button size="small" theme="borderless" onClick={() => openPluginDetail(binding.resourceId)}>
                          {t('lowcode_studio.plugin.detail')}
                        </Button>
                      ) : null}
                      <Button
                        size="small"
                        theme="borderless"
                        type="danger"
                        loading={unbindPluginMut.isPending}
                        onClick={() => unbindPluginMut.mutate(binding.resourceId)}
                      >
                        {t('lowcode_studio.plugin.unbind')}
                      </Button>
                    </Space>
                  )}
                >
                  <Space vertical align="start" spacing={4}>
                    <Typography.Text style={{ fontSize: 13 }}>🧩 {item?.name ?? `#${binding.resourceId}`}</Typography.Text>
                    <Typography.Text type="tertiary" style={{ fontSize: 12 }}>
                      {item?.description || `${binding.resourceType}:${binding.resourceId}`}
                    </Typography.Text>
                  </Space>
                </List.Item>
              )}
            />
          )}
        </Collapse.Panel>

        <Collapse.Panel
          itemKey="data"
          header={<PanelHeader title={t('lowcode_studio.layout.left.data')} count={variablesQuery.data?.length ?? 0} onAdd={() => setVarOpen(true)} />}
        >
          {variablesQuery.isLoading ? <Spin /> : (variablesQuery.data?.length ?? 0) === 0 ? (
            <Empty image={null} title={t('lowcode_studio.variables.empty')} />
          ) : (
            <List
              size="small"
              dataSource={variablesQuery.data ?? []}
              renderItem={(variable) => (
                <List.Item
                  style={{ padding: '6px 8px' }}
                  extra={(
                    <Space>
                      <Tag size="small">{variable.scope}</Tag>
                      <Button size="small" theme="borderless" onClick={() => setEditingVariable(variable)}>
                        {t('lowcode_studio.common.edit')}
                      </Button>
                      <Button
                        size="small"
                        theme="borderless"
                        type="danger"
                        loading={deleteVariableMut.isPending}
                        onClick={() => deleteVariableMut.mutate(variable.id)}
                      >
                        {t('lowcode_studio.common.delete')}
                      </Button>
                    </Space>
                  )}
                >
                  <Space vertical align="start" spacing={4}>
                    <Typography.Text style={{ fontSize: 13 }}>{variable.displayName}</Typography.Text>
                    <Typography.Text type="tertiary" style={{ fontSize: 12 }}>
                      {variable.code} · {variable.valueType}
                    </Typography.Text>
                  </Space>
                </List.Item>
              )}
            />
          )}
        </Collapse.Panel>

        <Collapse.Panel itemKey="settings" header={<PanelHeader title={t('lowcode_studio.settings.title')} />}>
          <List
            size="small"
            dataSource={[
              { key: 'session', icon: '💬', label: t('lowcode_studio.settings.sessions'), onClick: () => setSessionsOpen(true) },
              { key: 'variables', icon: '(x)', label: t('lowcode_studio.settings.variables'), onClick: ensureDataPanelVisible }
            ]}
            renderItem={(item) => (
              <List.Item style={{ padding: '6px 8px' }}>
                <Button theme="borderless" style={{ padding: 0 }} onClick={item.onClick}>
                  <Typography.Text style={{ fontSize: 13 }}>{item.icon} {item.label}</Typography.Text>
                </Button>
              </List.Item>
            )}
          />
        </Collapse.Panel>
      </Collapse>

      <Modal
        title={t('lowcode_studio.workflow.create')}
        visible={createOpen}
        onCancel={() => { if (!createWorkflowMut.isPending) setCreateOpen(false); }}
        footer={null}
        maskClosable={!createWorkflowMut.isPending}
      >
        {createWorkflow ? (
          <Form labelPosition="top" onSubmit={(vals) => createWorkflowMut.mutate(vals as { name: string; description?: string })}>
            <Form.Input
              field="name"
              label={t('lowcode_studio.workflow.name')}
              placeholder={t('lowcode_studio.workflow.namePlaceholder')}
              maxLength={30}
              rules={[{ required: true, message: t('lowcode_studio.workflow.nameRequired') }]}
            />
            <Form.TextArea
              field="description"
              label={t('lowcode_studio.app.description')}
              placeholder={t('lowcode_studio.workflow.descriptionPlaceholder')}
              maxLength={600}
              rows={4}
            />
            <Form.Slot>
              <Space>
                <Button htmlType="submit" type="primary" loading={createWorkflowMut.isPending}>{t('lowcode_studio.common.create')}</Button>
                <Button onClick={() => setCreateOpen(false)} disabled={createWorkflowMut.isPending}>{t('lowcode_studio.common.cancel')}</Button>
              </Space>
            </Form.Slot>
          </Form>
        ) : (
          <Typography.Text type="danger">{t('lowcode_studio.workflow.createUnavailable')}</Typography.Text>
        )}
      </Modal>

      <Modal title={t('lowcode_studio.variables.add')} visible={varOpen} onCancel={() => setVarOpen(false)} footer={null}>
        <Form onSubmit={(vals) => createVariableMut.mutate(vals as VariableFormValues)}>
          <Form.Input field="code" label={t('lowcode_studio.app.code')} rules={[{ required: true, pattern: /^[a-zA-Z][a-zA-Z0-9_-]{0,127}$/ }]} />
          <Form.Input field="displayName" label={t('lowcode_studio.app.displayName')} rules={[{ required: true }]} />
          <Form.Select
            field="scope"
            label={t('lowcode_studio.variables.scopeLabel')}
            initValue="app"
            optionList={[
              { label: t('lowcode_studio.variables.scope.page'), value: 'page' },
              { label: t('lowcode_studio.variables.scope.app'), value: 'app' },
              { label: t('lowcode_studio.variables.scope.system'), value: 'system' }
            ]}
          />
          <Form.Input field="valueType" label={t('lowcode_studio.variables.valueType')} initValue="string" rules={[{ required: true }]} />
          <Form.Input field="defaultValueJson" label={t('lowcode_studio.variables.defaultValue')} initValue="null" />
          <Form.TextArea field="description" label={t('lowcode_studio.app.description')} />
          <Form.Slot>
            <Space>
              <Button htmlType="submit" type="primary" loading={createVariableMut.isPending}>{t('lowcode_studio.common.create')}</Button>
              <Button onClick={() => setVarOpen(false)}>{t('lowcode_studio.common.cancel')}</Button>
            </Space>
          </Form.Slot>
        </Form>
      </Modal>

      <Modal title={t('lowcode_studio.variables.edit')} visible={Boolean(editingVariable)} onCancel={() => setEditingVariable(null)} footer={null}>
        {editingVariable ? (
          <Form
            key={editingVariable.id}
            initValues={{
              code: editingVariable.code,
              displayName: editingVariable.displayName,
              valueType: editingVariable.valueType,
              isReadOnly: editingVariable.isReadOnly,
              isPersisted: editingVariable.isPersisted,
              defaultValueJson: editingVariable.defaultValueJson,
              description: editingVariable.description
            }}
            onSubmit={(vals) => updateVariableMut.mutate({ id: editingVariable.id, ...(vals as Omit<VariableUpdateFormValues, 'id'>) })}
          >
            <Form.Input field="code" label={t('lowcode_studio.app.code')} rules={[{ required: true, pattern: /^[a-zA-Z][a-zA-Z0-9_-]{0,127}$/ }]} />
            <Form.Input field="displayName" label={t('lowcode_studio.app.displayName')} rules={[{ required: true }]} />
            <Form.Input field="valueType" label={t('lowcode_studio.variables.valueType')} rules={[{ required: true }]} />
            <Form.Switch field="isReadOnly" label={t('lowcode_studio.variables.readOnly')} />
            <Form.Switch field="isPersisted" label={t('lowcode_studio.variables.persisted')} />
            <Form.Input field="defaultValueJson" label={t('lowcode_studio.variables.defaultValue')} />
            <Form.TextArea field="description" label={t('lowcode_studio.app.description')} />
            <Form.Slot>
              <Space>
                <Button htmlType="submit" type="primary" loading={updateVariableMut.isPending}>{t('lowcode_studio.common.save')}</Button>
                <Button onClick={() => setEditingVariable(null)}>{t('lowcode_studio.common.cancel')}</Button>
              </Space>
            </Form.Slot>
          </Form>
        ) : null}
      </Modal>

      <Modal title={t('lowcode_studio.plugin.add')} visible={pluginPickerOpen} onCancel={() => setPluginPickerOpen(false)} footer={null}>
        <Space vertical align="start" style={{ width: '100%' }}>
          <Input value={pluginKeyword} onChange={setPluginKeyword} placeholder={t('lowcode_studio.plugin.search')} />
          {pluginSearchQuery.isLoading ? <Spin /> : pluginSearchQuery.error ? (
            <Banner type="danger" description={(pluginSearchQuery.error as Error).message} closeIcon={null} />
          ) : bindablePlugins.length === 0 ? (
            <Empty image={null} title={t('lowcode_studio.plugin.noAvailable')} />
          ) : (
            <List
              size="small"
              dataSource={bindablePlugins}
              renderItem={(plugin) => (
                <List.Item
                  extra={(
                    <Button size="small" type="primary" loading={bindPluginMut.isPending} onClick={() => bindPluginMut.mutate(plugin.id)}>
                      {t('lowcode_studio.common.add')}
                    </Button>
                  )}
                >
                  <Space vertical align="start" spacing={4}>
                    <Typography.Text>{plugin.name}</Typography.Text>
                    <Typography.Text type="tertiary" style={{ fontSize: 12 }}>
                      {plugin.description || plugin.id}
                    </Typography.Text>
                  </Space>
                </List.Item>
              )}
            />
          )}
        </Space>
      </Modal>

      <RuntimeSessionsSheet visible={sessionsOpen} onClose={() => setSessionsOpen(false)} />
    </div>
  );
};

const PanelHeader: React.FC<{ title: string; count?: number; onAdd?: () => void }> = ({ title, count, onAdd }) => (
  <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', width: '100%' }}>
    <span style={{ fontWeight: 500 }}>
      {title}
      {typeof count === 'number' && count > 0 ? <Tag size="small" style={{ marginLeft: 6 }}>{count}</Tag> : null}
    </span>
    {onAdd ? (
      <Button
        size="small"
        theme="borderless"
        onClick={(event) => {
          event.stopPropagation();
          onAdd();
        }}
      >
        ＋
      </Button>
    ) : null}
  </div>
);
