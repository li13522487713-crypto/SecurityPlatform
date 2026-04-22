/*
 * Copyright 2025 coze-dev Authors
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

import { useEffect, useState } from 'react';

import { isEqual } from 'lodash-es';
import { type FlowNodeEntity } from '@flowgram-adapter/free-layout-editor';
import { usePlayground } from '@flowgram-adapter/free-layout-editor';
import {
  WorkflowNodeData,
  type CommonNodeData,
  type NodeData,
} from '@coze-workflow/nodes';
import {
  IconCode,
  IconEdit,
  IconLink,
  IconBranch,
  IconSearch,
  IconSave,
  IconSync,
  IconCopy,
  IconComment,
  IconPlayCircle,
  IconStopCircle,
  IconComponent,
  IconBox,
  IconList,
  IconFile
} from '@douyinfe/semi-icons';

import { type ProblemItem } from '../../types';
import { BaseItem } from './base-item';

interface NodeItemProps {
  problem: ProblemItem;
  onClick: (p: ProblemItem) => void;
}

// Avoid losing icon and title information after node deletion
const useMetaMemo = (nodeId: string) => {
  const [nodeMeta, setNodeMeta] = useState<CommonNodeData>();
  const playground = usePlayground();

  const node = playground.entityManager.getEntityById<FlowNodeEntity>(nodeId);
  const nodeData = node?.getData<WorkflowNodeData>(WorkflowNodeData);
  const meta = nodeData?.getNodeData<keyof NodeData>();

  useEffect(() => {
    if (meta && !isEqual(nodeMeta, meta)) {
      setNodeMeta(meta);
    }
  }, [meta]);

  return nodeMeta;
};

const getIconByName = (nodeName?: string) => {
  if (!nodeName) return <IconComponent />;
  if (nodeName.includes('大模型')) return <IconBox />;
  if (nodeName.includes('意图')) return <IconBranch />;
  if (nodeName.includes('问答')) return <IconComment />;
  if (nodeName.includes('代码')) return <IconCode />;
  if (nodeName.includes('文本')) return <IconFile />;
  if (nodeName.includes('JSON')) return <IconCode />;
  if (nodeName.includes('聚合')) return <IconList />;
  if (nodeName.includes('赋值')) return <IconEdit />;
  if (nodeName.includes('插件')) return <IconComponent />;
  if (nodeName.includes('HTTP')) return <IconLink />;
  if (nodeName.includes('工作流')) return <IconBranch />;
  if (nodeName.includes('检索')) return <IconSearch />;
  if (nodeName.includes('写入')) return <IconSave />;
  if (nodeName.includes('循环')) return <IconSync />;
  if (nodeName.includes('批处理')) return <IconCopy />;
  if (nodeName.includes('注释')) return <IconComment />;
  if (nodeName.includes('开始')) return <IconPlayCircle />;
  if (nodeName.includes('结束')) return <IconStopCircle />;
  return <IconComponent />;
};

export const NodeItem: React.FC<NodeItemProps> = ({ problem, onClick }) => {
  const meta = useMetaMemo(problem.nodeId);

  return (
    <BaseItem
      problem={problem}
      title={meta?.title || ''}
      icon={
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', width: 20, height: 20, borderRadius: 4, background: 'rgba(22, 93, 255, 0.1)', color: 'var(--semi-color-primary)' }}>
          {getIconByName(meta?.title)}
        </div>
      }
      onClick={onClick}
    />
  );
};
