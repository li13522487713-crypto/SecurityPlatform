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

import { type FC, type CSSProperties, useState } from 'react';

import classnames from 'classnames';
import { Image } from '@coze-arch/coze-design';
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

import styles from './node-icon-outlined.module.less';
export interface NodeIconOutlinedProps {
  borderRadius?: CSSProperties['borderRadius'];
  size?: number;
  icon?: string;
  name?: string;
  hideOutline?: boolean;
  outlineColor?: string;
  style?: CSSProperties;
  className?: string;
}
export const NodeIconOutlined: FC<NodeIconOutlinedProps> = ({
  icon,
  name,
  size = 18,
  hideOutline,
  borderRadius = 'var(--coze-3)',
  outlineColor = 'var(--coz-stroke-primary)',
  className,
  style,
}) => {
  const [imgError, setImgError] = useState(false);

  const getIconByName = (nodeName: string) => {
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
  
  return (
  <div
    className={classnames(className, styles['node-icon-wrapper'])}
    style={{ borderRadius, width: size, height: size, ...style }}
  >
    {imgError || !icon || (!icon.startsWith('http') && !icon.startsWith('data:') && !icon.startsWith('/') && !icon.startsWith('<svg') && !icon.includes('/')) ? (
      <div
        className={styles['node-icon']}
        style={{ 
          borderRadius, 
          width: size, 
          height: size,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          background: 'rgba(22, 93, 255, 0.1)',
          fontSize: size * 0.75,
          color: 'var(--semi-color-primary, #165DFF)'
        }}
      >
        {name ? getIconByName(name) : <IconComponent />}
      </div>
    ) : (
      <Image
        className={styles['node-icon']}
        style={{ borderRadius }}
        width={size}
        height={size}
        src={icon}
        preview={false}
        onError={() => setImgError(true)}
      />
    )}
    {hideOutline ? null : (
      <div
        className={styles['node-icon-border']}
        style={{
          borderRadius,
          boxShadow: `inset 0 0 0 1px ${outlineColor}`,
        }}
      ></div>
    )}
  </div>
)};
