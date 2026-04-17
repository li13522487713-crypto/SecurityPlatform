import React from 'react';
import { Empty, Typography } from '@douyinfe/semi-ui';

export const RightInspector: React.FC<{ appId: string; kind: 'property' | 'style' | 'events' }> = ({ appId, kind }) => {
  return (
    <div style={{ padding: 12 }}>
      <Typography.Title heading={6}>{kind} - {appId}</Typography.Title>
      <Empty title="选中组件后显示属性面板" description="基于 @atlas/lowcode-property-forms 元数据驱动渲染" />
    </div>
  );
};
