import React from 'react';
import { Empty, Typography } from '@douyinfe/semi-ui';
import { useStudioSelection } from '../stores/selection-store';
import { useLowcodeStudioHost } from '../host';

/**
 * 业务逻辑画布：嵌入宿主注入的 DAG 编辑器（通常为 Coze workflow playground）。
 *
 * 为什么通过 host 注入而不是直接 import：
 *   lowcode-studio-react 是纯壳层包，不允许依赖 app-web 专属的 WorkflowRuntimeBoundary
 *   （其中包含 Atlas 鉴权 / Bootstrap / i18n 初始化）。
 */
export interface WorkflowCanvasProps {
  appId: string;
  workspaceId?: string;
  workspaceLabel?: string;
}

export const WorkflowCanvas: React.FC<WorkflowCanvasProps> = ({ appId, workspaceId, workspaceLabel }) => {
  const { selectedWorkflowId } = useStudioSelection();
  const { renderWorkflowEditor } = useLowcodeStudioHost();

  if (!selectedWorkflowId) {
    return (
      <div style={{ height: '100%', display: 'flex', alignItems: 'center', justifyContent: 'center', background: '#fafafa' }}>
        <Empty
          title="请选择工作流"
          description={<Typography.Text type="tertiary">在左侧"工作流"分组中选择已有条目，或新建一个以开始编辑</Typography.Text>}
        />
      </div>
    );
  }

  if (!renderWorkflowEditor) {
    return (
      <div style={{ height: '100%', display: 'flex', alignItems: 'center', justifyContent: 'center', background: '#fafafa' }}>
        <Empty
          title="宿主未注入工作流编辑器"
          description={<Typography.Text type="tertiary">请在 LowcodeStudioHostConfig.renderWorkflowEditor 中提供实现</Typography.Text>}
        />
      </div>
    );
  }

  return (
    <div style={{ height: '100%', width: '100%', overflow: 'hidden' }}>
      {renderWorkflowEditor({ appId, workflowId: selectedWorkflowId, workspaceId, workspaceLabel })}
    </div>
  );
};
