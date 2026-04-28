import React, { useState } from 'react';
import { useQuery, useMutation } from '@tanstack/react-query';
import { SideSheet, Button, List, Tag, Typography, Spin, Toast, Space, Empty, Checkbox, CheckboxGroup } from '@douyinfe/semi-ui';
import type { PublishArtifact } from '../services/api-core';

import { t } from '../i18n';
import { useLowcodeStudioHost } from '../host';

const RENDERER_OPTIONS = [
  { label: 'web', value: 'web' },
  { label: t('lowcode_studio.publish.miniWx'), value: 'mini-wx' },
  { label: t('lowcode_studio.publish.miniDouyin'), value: 'mini-douyin' },
  { label: 'h5', value: 'h5' }
];

/**
 * 发布抽屉（M17 C17-4 / C17-5 / C17-6 / C17-7）。
 * 三类产物：hosted / embedded-sdk / preview。
 * 渲染器矩阵：用户选择目标渲染器子集，传入 rendererMatrixJson；preview 默认仅 web。
 */
export const PublishDrawer: React.FC<{ appId: string; visible: boolean; onClose: () => void }> = ({ appId, visible, onClose }) => {
  const [renderers, setRenderers] = useState<string[]>(['web']);
  const { api, publishApi } = useLowcodeStudioHost();

  const previewQuery = useQuery({
    queryKey: ['project-ide-publish-preview', appId],
    queryFn: () => publishApi?.getPreview(appId),
    enabled: visible && Boolean(publishApi)
  });

  const artifactsQuery = useQuery({
    queryKey: ['lowcode-artifacts', appId],
    queryFn: async () => {
      if (publishApi) {
        return publishApi.listArtifacts(appId);
      }
      return api.publish.list(appId);
    },
    enabled: visible
  });

  const publishMut = useMutation({
    mutationFn: (kind: 'hosted' | 'embedded-sdk' | 'preview') => {
      // preview 仅 web；hosted / embedded-sdk 按用户选择的 renderers 子集
      const matrix = kind === 'preview'
        ? { web: true }
        : Object.fromEntries((renderers.length > 0 ? renderers : ['web']).map((r) => [r, true]));
      if (publishApi) {
        return publishApi.publish(appId, {
          kind,
          versionLabel: previewQuery.data?.suggestedVersionLabel,
          rendererMatrixJson: JSON.stringify(matrix)
        });
      }
      return api.publish.publish(appId, kind, { rendererMatrixJson: JSON.stringify(matrix) });
    },
    onSuccess: (payload: PublishArtifact | { artifact: PublishArtifact }) => {
      const artifact = 'artifact' in payload ? payload.artifact : payload;
      Toast.success(`发布成功（${artifact.kind}）`);
      artifactsQuery.refetch();
      previewQuery.refetch();
    },
    onError: (e: Error) => Toast.error(e.message)
  });

  const rollbackMut = useMutation({
    mutationFn: (artifactId: string) => api.publish.rollback(appId, artifactId),
    onSuccess: () => {
      Toast.success(t('lowcode_studio.common.revoked'));
      artifactsQuery.refetch();
    },
    onError: (e: Error) => Toast.error(e.message)
  });

  return (
    <SideSheet title="发布管理" visible={visible} onCancel={onClose} placement="right" size="large">
      <Typography.Text strong style={{ display: 'block', marginBottom: 6 }}>
        渲染器矩阵（hosted / embedded-sdk 生效；preview 固定 web）
      </Typography.Text>
      <CheckboxGroup
        value={renderers}
        onChange={(v) => setRenderers(v as string[])}
        style={{ marginBottom: 12 }}
      >
        {RENDERER_OPTIONS.map((o) => <Checkbox key={o.value} value={o.value}>{o.label}</Checkbox>)}
      </CheckboxGroup>
      <Space style={{ marginBottom: 16 }}>
        <Button type="primary" loading={publishMut.isPending} onClick={() => publishMut.mutate('hosted')}>发布 Hosted App</Button>
        <Button loading={publishMut.isPending} onClick={() => publishMut.mutate('embedded-sdk')}>发布 Embedded SDK</Button>
        <Button loading={publishMut.isPending} onClick={() => publishMut.mutate('preview')}>构建 Preview</Button>
      </Space>
      {publishApi && previewQuery.data?.warnings && previewQuery.data.warnings.length > 0 && (
        <div style={{ marginBottom: 16, padding: 12, borderRadius: 6, background: '#fffbe6', border: '1px solid #ffe58f' }}>
          {previewQuery.data.warnings.map((warning) => (
            <Typography.Paragraph key={warning} style={{ margin: 0, fontSize: 12, color: '#8c6d1f' }}>
              {warning}
            </Typography.Paragraph>
          ))}
        </div>
      )}

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
