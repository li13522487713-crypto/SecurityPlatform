import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Button, Space, RadioGroup, Radio, Modal, Form, Toast, Typography, Dropdown } from '@douyinfe/semi-ui';
import { IconEdit, IconMore, IconPlayCircle, IconSave, IconCode } from '@douyinfe/semi-icons';
import { lowcodeAppPreviewPath, appPublishPath } from '@atlas/app-shell-shared';
import { t } from '../i18n';
import { VersionDrawer } from './version-drawer';
import { DebugDrawer } from './debug-drawer';
import { FaqDrawer } from './faq-drawer';
import { CollabDrawer } from './collab-drawer';
import { useLowcodeStudioHost } from '../host';

export interface TopToolbarProps {
  appId: string;
  mode: 'business' | 'ui';
  onModeChange: (m: 'business' | 'ui') => void;
}

export const TopToolbar: React.FC<TopToolbarProps> = ({ appId, mode, onModeChange }) => {
  const nav = useNavigate();
  const qc = useQueryClient();
  const { api, auth } = useLowcodeStudioHost();
  const [versionOpen, setVersionOpen] = useState(false);
  const [debugOpen, setDebugOpen] = useState(false);
  const [snapshotOpen, setSnapshotOpen] = useState(false);
  const [faqOpen, setFaqOpen] = useState(false);
  const [collabOpen, setCollabOpen] = useState(false);
  const userId = auth.userIdFactory();

  /** 保存版本快照（M14 S14-1 用户主动版本，与 M16 协同的系统快照区分）。*/
  const snapshotMut = useMutation({
    mutationFn: (vals: { versionLabel: string; note?: string }) => api.apps.snapshot(appId, vals.versionLabel, vals.note),
    onSuccess: (r) => {
      Toast.success(`已保存版本（${r.versionId}）`);
      setSnapshotOpen(false);
      qc.invalidateQueries({ queryKey: ['lowcode-versions', appId] });
    },
    onError: (e: Error) => Toast.error(e.message)
  });

  return (
    <header style={{ height: 60, display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '0 24px', backgroundColor: '#fff', borderBottom: '1px solid var(--semi-color-border)' }}>
      {/* Left side */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
        <div style={{ width: 28, height: 28, borderRadius: 6, background: 'linear-gradient(135deg, #FF9A44 0%, #FC6076 100%)', display: 'flex', alignItems: 'center', justifyContent: 'center', color: '#fff', fontWeight: 'bold' }}>
          T
        </div>
        <Typography.Title heading={5} style={{ margin: 0 }}>测试工作流系统</Typography.Title>
        <Button icon={<IconEdit />} theme="borderless" type="tertiary" size="small" />
      </div>

      {/* Middle */}
      <div>
        <RadioGroup
          type="button"
          value={mode}
          onChange={(e) => onModeChange(e.target.value as 'business' | 'ui')}
          style={{ backgroundColor: 'var(--semi-color-fill-0)', borderRadius: 6, padding: 2 }}
        >
          <Radio value="business" style={{ borderRadius: 4, padding: '4px 24px', fontWeight: mode === 'business' ? 600 : 400 }}>{t('lowcode_studio.toolbar.modeBusinessLogic')}</Radio>
          <Radio value="ui" style={{ borderRadius: 4, padding: '4px 24px', fontWeight: mode === 'ui' ? 600 : 400 }}>{t('lowcode_studio.toolbar.modeUserInterface')}</Radio>
        </RadioGroup>
      </div>

      {/* Right side */}
      <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
        <Button icon={<IconPlayCircle />} theme="light" type="tertiary" onClick={() => nav(lowcodeAppPreviewPath(appId))}>{t('lowcode_studio.toolbar.preview')}</Button>
        <Button icon={<IconCode />} theme="light" type="tertiary" onClick={() => setDebugOpen(true)}>{t('lowcode_studio.toolbar.debug')}</Button>
        <Button icon={<IconSave />} theme="light" type="tertiary" onClick={() => setSnapshotOpen(true)}>{t('lowcode_studio.toolbar.save')}</Button>
        
        <Button type="primary" theme="solid" onClick={() => nav(appPublishPath(appId))} style={{ marginLeft: 8 }}>{t('lowcode_studio.toolbar.publish')}</Button>
        
        <Dropdown
          render={
            <Dropdown.Menu>
              <Dropdown.Item onClick={() => setVersionOpen(true)}>{t('lowcode_studio.toolbar.versions')}</Dropdown.Item>
              <Dropdown.Item onClick={() => setCollabOpen(true)}>{t('lowcode_studio.toolbar.collab')}</Dropdown.Item>
              <Dropdown.Item onClick={() => setFaqOpen(true)}>FAQ</Dropdown.Item>
            </Dropdown.Menu>
          }
        >
          <Button icon={<IconMore />} theme="borderless" type="tertiary" />
        </Dropdown>
      </div>

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
      <DebugDrawer appId={appId} visible={debugOpen} onClose={() => setDebugOpen(false)} />
      <FaqDrawer visible={faqOpen} onClose={() => setFaqOpen(false)} />
      <CollabDrawer appId={appId} userId={userId} visible={collabOpen} onClose={() => setCollabOpen(false)} />
    </header>
  );
};
