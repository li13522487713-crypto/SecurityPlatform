import React, { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Banner, Button, Empty, Input, List, SideSheet, Space, Spin, Tag, Toast, Typography } from '@douyinfe/semi-ui';
import { t } from '../i18n';
import { useLowcodeStudioHost } from '../host';

export interface RuntimeSessionsSheetProps {
  visible: boolean;
  onClose: () => void;
}

export const RuntimeSessionsSheet: React.FC<RuntimeSessionsSheetProps> = ({ visible, onClose }) => {
  const { runtimeSessions } = useLowcodeStudioHost();
  const qc = useQueryClient();
  const [draftTitle, setDraftTitle] = useState('');

  const sessionsQuery = useQuery({
    queryKey: ['lowcode-runtime-sessions'],
    queryFn: async () => {
      if (!runtimeSessions) {
        throw new Error(t('lowcode_studio.sessions.apiMissing'));
      }
      return runtimeSessions.list();
    },
    enabled: visible
  });

  const createMut = useMutation({
    mutationFn: async () => {
      if (!runtimeSessions) {
        throw new Error(t('lowcode_studio.sessions.apiMissing'));
      }
      return runtimeSessions.create({ title: draftTitle.trim() || undefined });
    },
    onSuccess: async () => {
      setDraftTitle('');
      Toast.success(t('lowcode_studio.common.created'));
      await qc.invalidateQueries({ queryKey: ['lowcode-runtime-sessions'] });
    },
    onError: (error: Error) => Toast.error(error.message)
  });

  const switchMut = useMutation({
    mutationFn: async (sessionId: string) => {
      if (!runtimeSessions) {
        throw new Error(t('lowcode_studio.sessions.apiMissing'));
      }
      return runtimeSessions.switchTo(sessionId);
    },
    onSuccess: async () => {
      Toast.success(t('lowcode_studio.sessions.switch'));
      await qc.invalidateQueries({ queryKey: ['lowcode-runtime-sessions'] });
    },
    onError: (error: Error) => Toast.error(error.message)
  });

  const clearMut = useMutation({
    mutationFn: async (sessionId: string) => {
      if (!runtimeSessions) {
        throw new Error(t('lowcode_studio.sessions.apiMissing'));
      }
      return runtimeSessions.clear(sessionId);
    },
    onSuccess: async () => {
      Toast.success(t('lowcode_studio.sessions.clear'));
      await qc.invalidateQueries({ queryKey: ['lowcode-runtime-sessions'] });
    },
    onError: (error: Error) => Toast.error(error.message)
  });

  const pinMut = useMutation({
    mutationFn: async ({ sessionId, pinned }: { sessionId: string; pinned: boolean }) => {
      if (!runtimeSessions) {
        throw new Error(t('lowcode_studio.sessions.apiMissing'));
      }
      return runtimeSessions.pin(sessionId, { pinned });
    },
    onSuccess: async () => {
      Toast.success(t('lowcode_studio.common.updated'));
      await qc.invalidateQueries({ queryKey: ['lowcode-runtime-sessions'] });
    },
    onError: (error: Error) => Toast.error(error.message)
  });

  const archiveMut = useMutation({
    mutationFn: async ({ sessionId, archived }: { sessionId: string; archived: boolean }) => {
      if (!runtimeSessions) {
        throw new Error(t('lowcode_studio.sessions.apiMissing'));
      }
      return runtimeSessions.archive(sessionId, { archived });
    },
    onSuccess: async () => {
      Toast.success(t('lowcode_studio.common.updated'));
      await qc.invalidateQueries({ queryKey: ['lowcode-runtime-sessions'] });
    },
    onError: (error: Error) => Toast.error(error.message)
  });

  return (
    <SideSheet title={t('lowcode_studio.sessions.title')} visible={visible} onCancel={onClose} placement="right" size="medium">
      <Space vertical align="start" style={{ width: '100%' }}>
        <Typography.Paragraph type="tertiary" style={{ marginBottom: 0 }}>
          {t('lowcode_studio.sessions.description')}
        </Typography.Paragraph>

        {!runtimeSessions ? (
          <Banner type="danger" description={t('lowcode_studio.sessions.apiMissing')} closeIcon={null} />
        ) : (
          <>
            <Space align="center" style={{ width: '100%' }}>
              <Input
                value={draftTitle}
                onChange={setDraftTitle}
                placeholder={t('lowcode_studio.sessions.newTitle')}
                style={{ flex: 1 }}
              />
              <Button type="primary" loading={createMut.isPending} onClick={() => createMut.mutate()}>
                {t('lowcode_studio.sessions.create')}
              </Button>
            </Space>

            {sessionsQuery.isLoading ? (
              <Spin />
            ) : sessionsQuery.error ? (
              <Banner type="danger" description={(sessionsQuery.error as Error).message} closeIcon={null} />
            ) : (
              <List
                size="small"
                style={{ width: '100%' }}
                dataSource={sessionsQuery.data ?? []}
                emptyContent={<Empty image={null} title={t('lowcode_studio.sessions.empty')} />}
                renderItem={(session) => (
                  <List.Item
                    style={{ padding: '10px 8px' }}
                    extra={
                      <Space wrap>
                        <Button size="small" onClick={() => switchMut.mutate(session.id)} loading={switchMut.isPending}>
                          {t('lowcode_studio.sessions.switch')}
                        </Button>
                        <Button
                          size="small"
                          theme="borderless"
                          onClick={() => pinMut.mutate({ sessionId: session.id, pinned: !session.pinned })}
                          loading={pinMut.isPending}
                        >
                          {session.pinned ? t('lowcode_studio.sessions.unpin') : t('lowcode_studio.sessions.pin')}
                        </Button>
                        <Button
                          size="small"
                          theme="borderless"
                          onClick={() => archiveMut.mutate({ sessionId: session.id, archived: !session.archived })}
                          loading={archiveMut.isPending}
                        >
                          {session.archived ? t('lowcode_studio.sessions.unarchive') : t('lowcode_studio.sessions.archive')}
                        </Button>
                        <Button
                          size="small"
                          type="danger"
                          theme="borderless"
                          onClick={() => clearMut.mutate(session.id)}
                          loading={clearMut.isPending}
                        >
                          {t('lowcode_studio.sessions.clear')}
                        </Button>
                      </Space>
                    }
                  >
                    <Space vertical align="start" spacing={4}>
                      <Space>
                        <Typography.Text strong>{session.title?.trim() || session.id}</Typography.Text>
                        {session.pinned ? <Tag color="amber" size="small">{t('lowcode_studio.sessions.pinned')}</Tag> : null}
                        {session.archived ? <Tag size="small">{t('lowcode_studio.sessions.archived')}</Tag> : null}
                      </Space>
                      <Typography.Text type="tertiary" style={{ fontSize: 12 }}>
                        {session.id}
                      </Typography.Text>
                      <Typography.Text type="tertiary" style={{ fontSize: 12 }}>
                        {new Date(session.updatedAt).toLocaleString()}
                      </Typography.Text>
                    </Space>
                  </List.Item>
                )}
              />
            )}
          </>
        )}
      </Space>
    </SideSheet>
  );
};
