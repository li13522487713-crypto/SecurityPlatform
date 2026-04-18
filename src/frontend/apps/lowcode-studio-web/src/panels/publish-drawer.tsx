import React from 'react';
import { useQuery, useMutation } from '@tanstack/react-query';
import { SideSheet, Button, List, Tag, Typography, Spin, Toast, Space, Empty } from '@douyinfe/semi-ui';
import { lowcodeApi, type PublishArtifact } from '../services/api-core';

/**
 * 发布抽屉（M17 C17-4 / C17-5 / C17-6 / C17-7）。
 * 三类产物：hosted / embedded-sdk / preview。
 */
export const PublishDrawer: React.FC<{ appId: string; visible: boolean; onClose: () => void }> = ({ appId, visible, onClose }) => {
  const artifactsQuery = useQuery({
    queryKey: ['lowcode-artifacts', appId],
    queryFn: () => lowcodeApi.publish.list(appId),
    enabled: visible
  });

  const publishMut = useMutation({
    mutationFn: (kind: 'hosted' | 'embedded-sdk' | 'preview') => lowcodeApi.publish.publish(appId, kind),
    onSuccess: (a: PublishArtifact) => {
      Toast.success(`发布成功（${a.kind}）`);
      artifactsQuery.refetch();
    },
    onError: (e: Error) => Toast.error(e.message)
  });

  const rollbackMut = useMutation({
    mutationFn: (artifactId: string) => lowcodeApi.publish.rollback(appId, artifactId),
    onSuccess: () => {
      Toast.success('已撤回');
      artifactsQuery.refetch();
    },
    onError: (e: Error) => Toast.error(e.message)
  });

  return (
    <SideSheet title="发布管理" visible={visible} onCancel={onClose} placement="right" size="large">
      <Space style={{ marginBottom: 16 }}>
        <Button type="primary" loading={publishMut.isPending} onClick={() => publishMut.mutate('hosted')}>发布 Hosted App</Button>
        <Button loading={publishMut.isPending} onClick={() => publishMut.mutate('embedded-sdk')}>发布 Embedded SDK</Button>
        <Button loading={publishMut.isPending} onClick={() => publishMut.mutate('preview')}>构建 Preview</Button>
      </Space>

      <Typography.Title heading={6}>已发布产物</Typography.Title>
      {artifactsQuery.isLoading ? <Spin /> : (
        <List
          dataSource={artifactsQuery.data ?? []}
          emptyContent={<Empty title="尚未发布任何产物" />}
          renderItem={(a) => (
            <List.Item
              extra={
                <Space>
                  <Tag color={a.status === 'ready' ? 'green' : a.status === 'failed' ? 'red' : 'blue'}>{a.status}</Tag>
                  <Tag>{a.kind}</Tag>
                  <Button size="small" onClick={() => rollbackMut.mutate(a.id)}>撤回</Button>
                </Space>
              }
            >
              <Typography.Text>指纹 {a.fingerprint.slice(0, 12)}…</Typography.Text>
              {a.publicUrl && (
                <div>
                  <Typography.Text link={{ href: a.publicUrl, target: '_blank' }} style={{ fontSize: 12 }}>
                    {a.publicUrl}
                  </Typography.Text>
                </div>
              )}
              {a.errorMessage && <Typography.Text type="danger" style={{ fontSize: 12 }}>{a.errorMessage}</Typography.Text>}
            </List.Item>
          )}
        />
      )}
    </SideSheet>
  );
};
