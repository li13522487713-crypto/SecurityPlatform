import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button, Space, RadioGroup, Radio } from '@douyinfe/semi-ui';
import { lowcodeAppPreviewPath } from '@atlas/app-shell-shared';
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
  const [versionOpen, setVersionOpen] = useState(false);
  const [publishOpen, setPublishOpen] = useState(false);
  const [debugOpen, setDebugOpen] = useState(false);

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
        <Button onClick={() => setDebugOpen(true)}>{t('lowcode_studio.toolbar.debug')}</Button>
        <Button onClick={() => setVersionOpen(true)}>{t('lowcode_studio.toolbar.versions')}</Button>
        <Button>{t('lowcode_studio.toolbar.collab')}</Button>
        <Button type="primary" onClick={() => setPublishOpen(true)}>{t('lowcode_studio.toolbar.publish')}</Button>
      </Space>

      <VersionDrawer appId={appId} visible={versionOpen} onClose={() => setVersionOpen(false)} />
      <PublishDrawer appId={appId} visible={publishOpen} onClose={() => setPublishOpen(false)} />
      <DebugDrawer appId={appId} visible={debugOpen} onClose={() => setDebugOpen(false)} />
    </Space>
  );
};
