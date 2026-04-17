import React from 'react';
import { Empty } from '@douyinfe/semi-ui';

export const CanvasViewport: React.FC<{ appId: string }> = ({ appId }) => {
  return (
    <div style={{ padding: 24, height: '100%', overflow: 'auto', background: '#fafafa' }}>
      <Empty title={`画布占位 (${appId})`} description="基于 @atlas/lowcode-editor-canvas dnd-kit / 三 LayoutEngine 渲染" />
    </div>
  );
};
