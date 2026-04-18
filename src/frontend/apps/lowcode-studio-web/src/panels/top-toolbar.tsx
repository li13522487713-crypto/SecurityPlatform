import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Button, Space, RadioGroup, Radio, Modal, Form, Toast } from '@douyinfe/semi-ui';
import { lowcodeAppPreviewPath } from '@atlas/app-shell-shared';
import { lowcodeApi } from '../services/api-core';
import { t } from '../i18n';
import { VersionDrawer } from './version-drawer';
import { PublishDrawer } from './publish-drawer';
import { DebugDrawer } from './debug-drawer';

export interface TopToolbarProps {
  appId: string;
  mode: 'business' | 'ui';
  onModeChange: (m: 'business' | 'ui') => void;
}

export const TopToolbar: React.FC<TopToolbarProps> = ({ appId, mode, onModeChange }) => {
  const nav = useNavigate();
  const qc = useQueryClient();
  const [versionOpen, setVersionOpen] = useState(false);
  const [publishOpen, setPublishOpen] = useState(false);
  const [debugOpen, setDebugOpen] = useState(false);
  const [snapshotOpen, setSnapshotOpen] = useState(false);

  /** 保存版本快照（M14 S14-1 用户主动版本，与 M16 协同的系统快照区分）。*/
  const snapshotMut = useMutation({
    mutationFn: (vals: { versionLabel: string; note?: string }) => lowcodeApi.apps.snapshot(appId, vals.versionLabel, vals.note),
    onSuccess: (r) => {
      Toast.success(`已保存版本（${r.versionId}）`);
      setSnapshotOpen(false);
      qc.invalidateQueries({ queryKey: ['lowcode-versions', appId] });
    },
    onError: (e: Error) => Toast.error(e.message)
  });

  return (
    <Space style={{ width: '100%', padding: '0 16px', height: 56, alignItems: 'center', justifyContent: 'space-between' }}>
      <Space>
        <strong>{t('lowcode_studio.app.title')}</strong>
        <span style={{ color: '#999' }}>#{appId}</span>
        <RadioGroup type="button" value={mode} onChange={(e) => onModeChange(e.target.value as 'business' | 'ui')}>
          <Radio value="business">{t('lowcode_studio.toolbar.modeBusinessLogic')}</Radio>
          <Radio value="ui">{t('lowcode_studio.toolbar.modeUserInterface')}</Radio>
        </RadioGroup>
      </Space>
      <Space>
        <Button onClick={() => nav(lowcodeAppPreviewPath(appId))}>{t('lowcode_studio.toolbar.preview')}</Button>
        <Button onClick={() => setSnapshotOpen(true)}>{t('lowcode_studio.toolbar.save')}</Button>
        <Button onClick={() => setDebugOpen(true)}>{t('lowcode_studio.toolbar.debug')}</Button>
        <Button onClick={() => setVersionOpen(true)}>{t('lowcode_studio.toolbar.versions')}</Button>
        <Button>{t('lowcode_studio.toolbar.collab')}</Button>
        <Button type="primary" onClick={() => setPublishOpen(true)}>{t('lowcode_studio.toolbar.publish')}</Button>
      </Space>

      <Modal title={t('lowcode_studio.toolbar.save')} visible={snapshotOpen} onCancel={() => setSnapshotOpen(false)} footer={null}>
        <Form
          initValues={{ versionLabel: `v${new Date().toISOString().slice(0, 10)}-${Math.floor(Date.now() / 1000) % 10000}` }}
          onSubmit={(vals) => snapshotMut.mutate(vals as { versionLabel: string; note?: string })}
        >
          <Form.Input field="versionLabel" label="版本标签" rules={[{ required: true }]} />
          <Form.TextArea field="note" label="备注" placeholder="本次保存做了哪些变更（可选）" />
          <Form.Slot>
            <Space>
              <Button htmlType="submit" type="primary" loading={snapshotMut.isPending}>保存</Button>
              <Button onClick={() => setSnapshotOpen(false)}>取消</Button>
            </Space>
          </Form.Slot>
        </Form>
      </Modal>

      <VersionDrawer appId={appId} visible={versionOpen} onClose={() => setVersionOpen(false)} />
      <PublishDrawer appId={appId} visible={publishOpen} onClose={() => setPublishOpen(false)} />
      <DebugDrawer appId={appId} visible={debugOpen} onClose={() => setDebugOpen(false)} />
    </Space>
  );
};
