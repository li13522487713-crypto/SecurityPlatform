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

import { I18n } from '@coze-arch/i18n';
import { Tooltip } from '@coze-arch/coze-design';
import { usePlaygroundTools } from '@flowgram-adapter/free-layout-editor';
import { type InteractiveType as IdeInteractiveType } from '@flowgram-adapter/free-layout-editor';
import {
  GuidingPopover,
  InteractiveType,
  MousePadSelector,
  getPreferInteractiveType,
  setPreferInteractiveType,
} from '@coze-common/mouse-pad-selector';

import { useGlobalState } from '@/hooks';

export const Interactive = () => {
  const tools = usePlaygroundTools();
  const workflowState = useGlobalState();

  const [interactiveType, setInteractiveType] = useState<InteractiveType>(
    () => getPreferInteractiveType() as InteractiveType,
  );

  const [showInteractivePanel, setShowInteractivePanel] = useState(false);
  const [tooltipVisible, setTooltipVisible] = useState(false);

  const mousePadTooltip = I18n.t(
    interactiveType === InteractiveType.Mouse
      ? 'workflow_mouse_friendly'
      : 'workflow_pad_friendly',
  );

  useEffect(() => {
    tools.setMouseScrollDelta(zoom => zoom / 20);

    // Read interactive mode from cache, application takes effect
    const preferInteractiveType = getPreferInteractiveType();
    tools.setInteractiveType(preferInteractiveType as IdeInteractiveType);
    // eslint-disable-next-line react-hooks/exhaustive-deps -- init
  }, []);

  const interactiveSwitcher = (
    <Tooltip
      trigger="custom"
      content={mousePadTooltip}
      visible={tooltipVisible && !showInteractivePanel}
      onVisibleChange={setTooltipVisible}
    >
      <div
        className="workflow-toolbar-interactive"
        data-testid="workflow.detail.toolbar.interactive"
        onMouseEnter={() => {
          setTooltipVisible(true);
        }}
        onMouseLeave={() => {
          setTooltipVisible(false);
        }}
      >
        <MousePadSelector
          value={interactiveType}
          onChange={value => {
            setInteractiveType(value);
            setPreferInteractiveType(value);
            tools.setInteractiveType(value as unknown as IdeInteractiveType);
          }}
          onPopupVisibleChange={visible => {
            setShowInteractivePanel(visible);
            if (visible) {
              setTooltipVisible(false);
            }
          }}
          containerStyle={{
            border: 'none',
            height: '24px',
            width: '38px',
            justifyContent: 'center',
            alignItems: 'center',
            gap: '2px',
            padding: '4px',
            paddingTop: '1px',
            borderRadius: 'var(--small, 6px)',
          }}
          iconStyle={{
            margin: '0',
            width: '16px',
            height: '16px',
          }}
          arrowStyle={{
            width: '12px',
            height: '12px',
          }}
        />
      </div>
    </Tooltip>
  );

  const canShowGuidingPopover = Boolean(
    workflowState.config.initialViewportCalibrated,
  );
  if (!canShowGuidingPopover) {
    return interactiveSwitcher;
  }

  return <GuidingPopover>{interactiveSwitcher}</GuidingPopover>;
};
