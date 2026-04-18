import React from 'react';
import { useNavigate } from 'react-router-dom';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Button, Card, Empty, List, Space, Modal, Form, Toast } from '@douyinfe/semi-ui';
import { lowcodeAppStudioPath } from '@atlas/app-shell-shared';
import { lowcodeApi, type AppListItem } from '../services/api-core';
import { t } from '../i18n';

export const AppListPage: React.FC = () => {
  const nav = useNavigate();
  const qc = useQueryClient();
  const { data, isLoading } = useQuery({ queryKey: ['lowcode-apps'], queryFn: () => lowcodeApi.apps.list() });

  const createMut = useMutation({
    mutationFn: (vals: { code: string; displayName: string; description?: string }) =>
      lowcodeApi.apps.create({ code: vals.code, displayName: vals.displayName, description: vals.description, targetTypes: 'web', defaultLocale: 'zh-CN' }),
    onSuccess: (r) => {
      Toast.success(t('lowcode_studio.common.created'));
      qc.invalidateQueries({ queryKey: ['lowcode-apps'] });
      nav(lowcodeAppStudioPath(r.id));
    },
    onError: (e: Error) => Toast.error(e.message)
  });

  const deleteMut = useMutation({
    mutationFn: (id: string) => lowcodeApi.apps.delete(id),
    onSuccess: () => {
      Toast.success(t('lowcode_studio.common.deleted'));
      qc.invalidateQueries({ queryKey: ['lowcode-apps'] });
    },
    onError: (e: Error) => Toast.error(e.message)
  });

  const confirmDelete = (app: AppListItem) => {
    Modal.confirm({
      title: t('lowcode_studio.app.delete'),
      content: `将永久删除应用 ${app.displayName}（${app.code}）。该操作不可撤销，相关草稿与历史版本仍按归档保留。`,
      okText: t('lowcode_studio.app.delete'),
      cancelText: t('lowcode_studio.common.cancel'),
      onOk: () => deleteMut.mutate(app.id)
    });
  };

  const [open, setOpen] = React.useState(false);

  return (
    <div style={{ padding: 24, maxWidth: 1080, margin: '0 auto' }}>
      <Space style={{ width: '100%', justifyContent: 'space-between', marginBottom: 16 }}>
        <h2>{t('lowcode_studio.app.list')}</h2>
        <Button type="primary" onClick={() => setOpen(true)}>{t('lowcode_studio.app.create')}</Button>
      </Space>
      {isLoading ? <Empty title="加载中..." /> : (
        <List
          dataSource={data?.items ?? []}
          renderItem={(app: AppListItem) => (
            <List.Item
              header={<Card.Meta title={app.displayName} description={app.code} />}
              extra={
                <Space>
                  <Button onClick={() => nav(lowcodeAppStudioPath(app.id))}>打开</Button>
                  <Button type="danger" onClick={() => confirmDelete(app)} loading={deleteMut.isPending}>{t('lowcode_studio.app.delete')}</Button>
                </Space>
              }
            >
              <Space>
                <span>状态：{app.status}</span>
                <span>更新时间：{app.updatedAt}</span>
              </Space>
            </List.Item>
          )}
        />
      )}

      <Modal title={t('lowcode_studio.app.create')} visible={open} onCancel={() => setOpen(false)} footer={null}>
        <Form onSubmit={(vals) => createMut.mutate(vals as { code: string; displayName: string; description?: string })}>
          <Form.Input field="code" label={t('lowcode_studio.app.code')} rules={[{ required: true, pattern: /^[a-zA-Z][a-zA-Z0-9_-]{0,127}$/ }]} />
          <Form.Input field="displayName" label={t('lowcode_studio.app.displayName')} rules={[{ required: true }]} />
          <Form.TextArea field="description" label={t('lowcode_studio.app.description')} />
          <Form.Slot>
            <Button htmlType="submit" type="primary" loading={createMut.isPending}>提交</Button>
          </Form.Slot>
        </Form>
      </Modal>
    </div>
  );
};
