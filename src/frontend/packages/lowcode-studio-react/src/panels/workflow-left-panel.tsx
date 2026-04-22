import React, { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Collapse, List, Typography, Empty, Spin, Tag, Button, Modal, Form, Toast, Space } from '@douyinfe/semi-ui';
import { useStudioSelection } from '../stores/selection-store';
import { useLowcodeStudioHost } from '../host';
import { t } from '../i18n';

/**
 * 业务逻辑模式左侧面板 —— 资源 / 引用关系。
 *
 * 分组：工作流 / 插件 / 数据 / 设置。
 * 数据来源：
 *  - 工作流 / 插件 → GET /apps/{id}/resources（resources.search，按 type 过滤）
 *  - 数据          → GET /apps/{id}/variables（变量列表，与 UI 模式共享）
 *  - 设置          → 固定导航项，不调后端
 */
export interface WorkflowLeftPanelProps {
  appId: string;
  workspaceId?: string;
}

export const WorkflowLeftPanel: React.FC<WorkflowLeftPanelProps> = ({ appId, workspaceId }) => {
  const host = useLowcodeStudioHost();
  const { api, createWorkflow } = host;
  const { selectedWorkflowId, setSelectedWorkflowId } = useStudioSelection();
  const qc = useQueryClient();
  const [createOpen, setCreateOpen] = useState(false);

  const resourcesQuery = useQuery({
    queryKey: ['lowcode-resources-workflow', appId],
    queryFn: () => api.resources.search(appId, { pageSize: 50 })
  });

  const createMut = useMutation({
    mutationFn: async (vals: { name: string; description?: string }) => {
      if (!createWorkflow) throw new Error('宿主未注入 createWorkflow');
      return createWorkflow({ appId, workspaceId, name: vals.name, description: vals.description });
    },
    onSuccess: (r) => {
      Toast.success(`已创建工作流（${r.workflowId}）`);
      setCreateOpen(false);
      setSelectedWorkflowId(r.workflowId);
      qc.invalidateQueries({ queryKey: ['lowcode-resources-workflow', appId] });
    },
    onError: (e: Error) => Toast.error(e.message)
  });

  const variablesQuery = useQuery({
    queryKey: ['lowcode-variables', appId],
    queryFn: () => api.variables.list(appId)
  });

  const workflows = useMemo(() => resourcesQuery.data?.byType?.['workflow'] ?? [], [resourcesQuery.data]);
  const plugins = useMemo(() => resourcesQuery.data?.byType?.['plugin'] ?? [], [resourcesQuery.data]);

  return (
    <div style={{ padding: 12, height: '100%', overflow: 'auto' }}>
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 8 }}>
        <Typography.Text strong>{t('lowcode_studio.layout.left.resources')}</Typography.Text>
        <Tag size="small">{t('lowcode_studio.toolbar.modeBusinessLogic')}</Tag>
      </div>

      <Collapse defaultActiveKey={['workflow', 'plugin', 'data', 'settings']} keepDOM>
        <Collapse.Panel
          header={
            <PanelHeader
              title="工作流"
              count={workflows.length}
              onAdd={() => setCreateOpen(true)}
            />
          }
          itemKey="workflow"
        >
          {resourcesQuery.isLoading ? <Spin /> : workflows.length === 0 ? (
            <Empty image={null} title="暂无工作流" description="点击右侧 + 新建" />
          ) : (
            <List
              size="small"
              dataSource={workflows}
              renderItem={(r) => {
                const active = r.id === selectedWorkflowId;
                return (
                  <List.Item
                    style={{ cursor: 'pointer', background: active ? '#e6f4ff' : undefined, padding: '6px 8px' }}
                    onClick={() => setSelectedWorkflowId(r.id)}
                  >
                    <Typography.Text strong={active} style={{ fontSize: 13 }}>⚙ {r.name}</Typography.Text>
                  </List.Item>
                );
              }}
            />
          )}
        </Collapse.Panel>

        <Collapse.Panel
          header={<PanelHeader title="插件" count={plugins.length} />}
          itemKey="plugin"
        >
          {plugins.length === 0 ? (
            <Empty image={null} title="还未添加插件" />
          ) : (
            <List
              size="small"
              dataSource={plugins}
              renderItem={(r) => (
                <List.Item style={{ padding: '6px 8px' }}>
                  <Typography.Text style={{ fontSize: 13 }}>🧩 {r.name}</Typography.Text>
                </List.Item>
              )}
            />
          )}
        </Collapse.Panel>

        <Collapse.Panel
          header={<PanelHeader title="数据" count={variablesQuery.data?.length ?? 0} />}
          itemKey="data"
        >
          {variablesQuery.isLoading ? <Spin /> : (variablesQuery.data?.length ?? 0) === 0 ? (
            <Empty image={null} title="还未添加数据" />
          ) : (
            <List
              size="small"
              dataSource={variablesQuery.data ?? []}
              renderItem={(v) => (
                <List.Item style={{ padding: '6px 8px' }}>
                  <Typography.Text style={{ fontSize: 13 }}>{v.displayName}</Typography.Text>
                  <Typography.Text type="tertiary" style={{ fontSize: 12, marginLeft: 8 }}>
                    {v.code} · {v.scope}
                  </Typography.Text>
                </List.Item>
              )}
            />
          )}
        </Collapse.Panel>

        <Collapse.Panel header={<PanelHeader title="设置" />} itemKey="settings">
          <List
            size="small"
            dataSource={[
              { key: 'session', icon: '💬', label: '会话管理' },
              { key: 'variables', icon: '(x)', label: '变量' }
            ]}
            renderItem={(item) => (
              <List.Item style={{ padding: '6px 8px' }}>
                <Typography.Text style={{ fontSize: 13 }}>{item.icon} {item.label}</Typography.Text>
              </List.Item>
            )}
          />
        </Collapse.Panel>
      </Collapse>

      <Modal
        title="新建工作流"
        visible={createOpen}
        onCancel={() => { if (!createMut.isPending) setCreateOpen(false); }}
        footer={null}
        maskClosable={!createMut.isPending}
      >
        {createWorkflow ? (
          <Form
            labelPosition="top"
            onSubmit={(vals) => createMut.mutate(vals as { name: string; description?: string })}
          >
            <Form.Input
              field="name"
              label="工作流名称"
              placeholder="只能包含字母、数字和下划线"
              maxLength={30}
              rules={[{ required: true, message: '请输入工作流名称' }]}
            />
            <Form.TextArea
              field="description"
              label="描述"
              placeholder="简要描述工作流功能（可选）"
              maxLength={600}
              rows={4}
            />
            <Form.Slot>
              <Space>
                <Button htmlType="submit" type="primary" loading={createMut.isPending}>创建</Button>
                <Button onClick={() => setCreateOpen(false)} disabled={createMut.isPending}>取消</Button>
              </Space>
            </Form.Slot>
          </Form>
        ) : (
          <Typography.Text type="danger">
            宿主未注入 createWorkflow 能力，无法在此创建工作流。
          </Typography.Text>
        )}
      </Modal>
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
        onClick={(e) => {
          e.stopPropagation();
          onAdd();
        }}
      >
        ＋
      </Button>
    ) : null}
  </div>
);
