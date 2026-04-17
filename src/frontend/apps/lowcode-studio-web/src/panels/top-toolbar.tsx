import React from 'react';
import { Button, Space, RadioGroup, Radio } from '@douyinfe/semi-ui';
import { t } from '../i18n';

export interface TopToolbarProps {
  appId: string;
  mode: 'business' | 'ui';
  onModeChange: (m: 'business' | 'ui') => void;
}

export const TopToolbar: React.FC<TopToolbarProps> = ({ appId, mode, onModeChange }) => {
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
        <Button>{t('lowcode_studio.toolbar.preview')}</Button>
        <Button>{t('lowcode_studio.toolbar.debug')}</Button>
        <Button>{t('lowcode_studio.toolbar.versions')}</Button>
        <Button>{t('lowcode_studio.toolbar.collab')}</Button>
        <Button type="primary">{t('lowcode_studio.toolbar.publish')}</Button>
      </Space>
    </Space>
  );
};
