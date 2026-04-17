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

import 'reflect-metadata';
import React, { useEffect, useRef } from 'react';

import { WorkflowPlayground } from '@coze-workflow/playground/workflow-playground';
import {
  type AddNodeRef,
  type WorkflowPlaygroundRef,
} from '@coze-workflow/playground/typing';

import { usePageParams } from './hooks/use-page-params';
import { useNavigateBack } from './hooks';

// The added node is placed in the toolbar, but the original sidebar is no longer needed.
const EmptySidebar = React.forwardRef<AddNodeRef, unknown>(
  (_props, _addNodeRef) => null,
);

export interface WorkflowPageProps {
  workflowId?: string;
  spaceId?: string;
  version?: string;
  setVersion?: boolean;
  from?: string;
  optType?: number;
  nodeId?: string;
  executeId?: string;
  subExecuteId?: string;
  returnUrl?: string;
  mode?: string;
  onAtlasBack?: () => void;
}

export function WorkflowPage(props: WorkflowPageProps = {}): React.ReactNode {
  const workflowPlaygroundRef = useRef<WorkflowPlaygroundRef>(null);
  const {
    spaceId,
    workflowId,
    version,
    setVersion,
    from,
    optType,
    nodeId,
    executeId,
    subExecuteId,
    returnUrl,
  } = usePageParams(props);

  const initOnceRef = useRef(false);
  const fitViewScheduleTimerRef = useRef<number>();
  const { navigateBack } = useNavigateBack();

  useEffect(
    () => () => {
      if (fitViewScheduleTimerRef.current) {
        window.clearTimeout(fitViewScheduleTimerRef.current);
      }
    },
    [],
  );

  /** Whether it is read-only mode, derived from the process exploration module */
  const readonly = from === 'explore';

  if (!workflowId || !spaceId) {
    return null;
  }

  return (
    <>
      <WorkflowPlayground
        ref={workflowPlaygroundRef}
        sidebar={EmptySidebar}
        workflowId={workflowId}
        spaceId={spaceId}
        commitId={setVersion ? undefined : version}
        commitOptType={setVersion ? undefined : optType}
        readonly={readonly}
        executeId={executeId}
        subExecuteId={subExecuteId}
        onInit={_workflowState => {
          if (setVersion && version) {
            workflowPlaygroundRef.current?.resetToHistory({
              commitId: version,
              optType,
            });
          }

          // onInit may be called multiple times, it only needs to be executed once
          if (initOnceRef.current) {
            return;
          }

          // Read the node_id parameters on the link and scroll to the corresponding node
          if (nodeId) {
            workflowPlaygroundRef.current?.scrollToNode(nodeId);
          } else {
            // The first screen may be initialized in a hidden container, so run fitView twice.
            // The second run serves as one-time compensation when layout becomes available later.
            window.requestAnimationFrame(() => {
              workflowPlaygroundRef.current?.triggerFitView();
              fitViewScheduleTimerRef.current = window.setTimeout(() => {
                workflowPlaygroundRef.current?.triggerFitView();
              }, 120);
            });
          }

          // Read execute_id show the corresponding execution result
          if (executeId) {
            workflowPlaygroundRef.current?.showTestRunResult(
              executeId,
              subExecuteId,
            );
          }

          initOnceRef.current = true;
        }}
        from={from}
        onBackClick={workflowState => {
          if (props.onAtlasBack) {
            props.onAtlasBack();
            return;
          }
          navigateBack(workflowState, 'exit', returnUrl);
        }}
        onPublish={workflowState => {
          navigateBack(workflowState, 'publish', returnUrl);
        }}
      />
    </>
  );
}
