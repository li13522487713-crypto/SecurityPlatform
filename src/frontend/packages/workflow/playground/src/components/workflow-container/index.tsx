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

import { Helmet } from 'react-helmet';
import { useDrop } from 'react-dnd';
import {
  forwardRef,
  useCallback,
  useEffect,
  useImperativeHandle,
  useMemo,
  useRef,
  type DragEventHandler,
} from 'react';

import classnames from 'classnames';
import { QueryClientProvider } from '@tanstack/react-query';
import {
  PlaygroundReactRenderer,
  useService,
} from '@flowgram-adapter/free-layout-editor';
import { EncapsulatePanel } from '@coze-workflow/feature-encapsulate';
import { workflowQueryClient } from '@coze-workflow/base';
import { I18n } from '@coze-arch/i18n';
import { Spin } from '@coze-arch/bot-semi';
import { getFlags } from '@coze-arch/bot-flags';
import { CustomError } from '@coze-arch/bot-error';
import { type BotSpace } from '@coze-arch/bot-api/developer_api';

import { AddNodeModalProvider } from '@/contexts/add-node-modal-context';

import { WorkflowRefreshModal } from '../workflow-refresh-modal';
import { WorkflowOuterSideSheetHolder } from '../workflow-outer-side-sheet';
import { WorkflowInnerSideSheetHolder } from '../workflow-inner-side-sheet';
import { useCommitAction } from '../workflow-header/components/history-button/components/history-drawer/use-commit-action';
import WorkflowHeader from '../workflow-header';
import { Toolbar } from '../toolbar';
import { useResultSideSheetVisible } from '../test-run/execute-result/execute-result-side-sheet/hooks/use-result-side-sheet-visible';
import { TemplatePanel, TemplatePreview } from '../template-panel';
import RetrieveBanner from '../retrieve-banner';
import { ProblemPanel } from '../problem-panel';
import { ModifyBanner } from '../modify-banner';
import { DragTooltip } from '../drag-tooltip';
import { DatabaseDetailModal } from '../database-detail-modal';
import { ChatTestRunPauseSideSheet } from '../chat-testrun-pause-side-sheet';
import {
  type AddNodeRef,
  type WorkflowPlaygroundProps,
  type WorkflowPlaygroundRef,
  type DragObject,
} from '../../typing';
import { WorkflowCustomDragService, WorkflowSaveService } from '../../services';
import { useTestRun } from '../../hooks/use-test-run';
import {
  useFloatLayoutService,
  useGlobalState,
  useScrollToNode,
  useWorkflowRunService,
  useDependencyService,
} from '../../hooks';
import {
  DND_ACCEPT_KEY,
  WORKFLOW_PLAYGROUND_CONTENT_ID,
} from '../../constants';
import { WorkflowFloatLayout } from './workflow-float-layout';
import { useNodesMount } from './use-nodes-mount';
import { useDataCompensation } from './use-data-compensation';

import styles from './index.module.less';

/**
 * Process Canvas
 */
const WorkflowContainer = forwardRef<
  WorkflowPlaygroundRef,
  WorkflowPlaygroundProps & {
    spaceList: BotSpace[];
  }
>((props, ref) => {
  const workflowState = useGlobalState();
  const workflowSaveService =
    useService<WorkflowSaveService>(WorkflowSaveService);
  const dependencyService = useDependencyService();
  const floatLayoutService = useFloatLayoutService();
  const runService = useWorkflowRunService();
  const { handleTestRun, cancelTestRun } = useTestRun({ callbacks: props });
  const { resetToCommitById } = useCommitAction();
  const { closeSideSheetAndHideResult, showResult } =
    useResultSideSheetVisible();
  const { loading, loadingError, readonly, isBindDouyin } = workflowState;
  let playgroundContent;
  const addNodeRef = useRef<AddNodeRef>(null);
  const workflowPlaygroundContentRef = useRef<HTMLDivElement | null>(null);
  const initialViewportCalibratedRef = useRef(false);
  const fallbackFitViewTriggeredRef = useRef(false);
  const isNodesMount = useNodesMount();
  // Synchronize component properties to globalStatus
  useMemo(() => {
    const { spaceList, ...playgroundProps } = props;

    workflowState.updateConfig({
      playgroundProps,
      spaceList,
    });
  }, [props]);

  // Initialization successful
  useEffect(() => {
    if (!loading && !loadingError && isNodesMount) {
      props.onInit?.(workflowState);
    }
  }, [loading, isNodesMount, loadingError, workflowState]);

  // Listen for TTI events, perform data compensation operations, and save drafts
  useDataCompensation(workflowState);

  const markInitialViewportCalibrated = useCallback(() => {
    if (initialViewportCalibratedRef.current) {
      return;
    }
    initialViewportCalibratedRef.current = true;
    workflowState.updateConfig({
      initialViewportCalibrated: true,
    });
  }, [workflowState]);

  const getWorkflowViewportHost = useCallback(
    () =>
      workflowPlaygroundContentRef.current ??
      document.getElementById(WORKFLOW_PLAYGROUND_CONTENT_ID),
    [],
  );

  const isViewportHostReady = useCallback(() => {
    const viewportHost = getWorkflowViewportHost();
    return Boolean(
      viewportHost &&
        viewportHost.clientWidth > 0 &&
        viewportHost.clientHeight > 0,
    );
  }, [getWorkflowViewportHost]);

  const triggerFitViewIfReady = useCallback(async () => {
    if (initialViewportCalibratedRef.current || !isViewportHostReady()) {
      return false;
    }

    await workflowSaveService.fitView();
    if (isViewportHostReady()) {
      markInitialViewportCalibrated();
      return true;
    }

    return false;
  }, [isViewportHostReady, markInitialViewportCalibrated, workflowSaveService]);

  useEffect(() => {
    initialViewportCalibratedRef.current = false;
    fallbackFitViewTriggeredRef.current = false;
    workflowState.updateConfig({
      initialViewportCalibrated: false,
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps -- reset once when container mounts
  }, []);

  useEffect(() => {
    if (
      loading ||
      loadingError ||
      !isNodesMount ||
      initialViewportCalibratedRef.current ||
      fallbackFitViewTriggeredRef.current ||
      typeof ResizeObserver === 'undefined'
    ) {
      return;
    }

    const viewportHost = getWorkflowViewportHost();
    if (!viewportHost) {
      return;
    }

    let observer: ResizeObserver | undefined;
    const tryCompensateFitView = async () => {
      if (fallbackFitViewTriggeredRef.current) {
        return;
      }
      const fitViewSuccess = await triggerFitViewIfReady();
      if (fitViewSuccess) {
        fallbackFitViewTriggeredRef.current = true;
        observer?.disconnect();
      }
    };

    observer = new ResizeObserver(() => {
      void tryCompensateFitView();
    });
    observer.observe(viewportHost);
    void tryCompensateFitView();

    return () => {
      observer?.disconnect();
    };
  }, [
    getWorkflowViewportHost,
    isNodesMount,
    loading,
    loadingError,
    triggerFitViewIfReady,
  ]);

  const onDragOver: DragEventHandler<HTMLDivElement> = useCallback(event => {
    event.preventDefault();
    if (event.dataTransfer) {
      event.dataTransfer.dropEffect = 'move';
    }
  }, []);

  const dragService = useService<WorkflowCustomDragService>(
    WorkflowCustomDragService,
  );

  const [, drop] = useDrop(() => ({
    accept: DND_ACCEPT_KEY,
    canDrop: (item: DragObject, monitor) =>
      dragService.canDrop({
        coord: monitor.getSourceClientOffset() ?? { x: 0, y: 0 },
        dragNode: {
          type: item.nodeType,
          json: item.nodeJson,
        },
      }),
    drop: (item: DragObject, monitor) => {
      const coord = monitor.getClientOffset() ?? { x: 0, y: 0 };
      addNodeRef.current?.handleAddNode?.(item, coord, true);
    },
  }));

  const scrollToNode = useScrollToNode();

  useImperativeHandle(ref, () => ({
    triggerTestRun: async () => {
      await handleTestRun();
      return true;
    },
    getProcess: async (obj: { executeId?: string }) => {
      await runService.getRTProcessResult(obj);
    },
    reload: async () => {
      floatLayoutService.closeAll();
      await workflowSaveService.reloadDocument();
      props.onInit?.(workflowState);
    },
    cancelTestRun: () => cancelTestRun(),
    showTestRunResult: (executeIdOrResp, subExecuteId) => {
      if (!executeIdOrResp) {
        // Show results of the latest practice run
        showResult();
      } else if (typeof executeIdOrResp === 'string') {
        // Display the result of the specified run ID
        showResult({ executeId: executeIdOrResp, subExecuteId });
      } else {
        // Direct display of results
        showResult({ processResp: executeIdOrResp });
      }
    },
    hideTestRunResult: () => {
      closeSideSheetAndHideResult();
    },
    resetToHistory: ({ commitId, optType }) => {
      resetToCommitById(commitId, optType);
    },
    scrollToNode: (nodeId: string) => {
      if (nodeId) {
        scrollToNode(nodeId);
        markInitialViewportCalibrated();
      }
    },
    triggerFitView: async () => {
      await triggerFitViewIfReady();
    },
    loadGlobalVariables: async () => {
      await workflowSaveService.loadGlobalVariables();
    },
    onResourceChange: (resourceProps, callback) =>
      dependencyService.updateDependencySources(resourceProps, callback),
  }));

  if (loading) {
    playgroundContent = (
      <Spin spinning={true} style={{ height: '100%', width: '100%' }} />
    );
  } else if (loadingError) {
    // Trigger exception, go to the top error boundary fallback
    throw new CustomError('normal_error', loadingError);
  } else {
    const Sidebar = props.sidebar;

    // When the workflow is binded to Douyin Account，the template preview is not displayed
    const showTemplatePreview = !isBindDouyin;
    playgroundContent = (
      <QueryClientProvider client={workflowQueryClient}>
        <div className={styles.workflowLayout}>
          <div className={styles.workflowMainColumn}>
            {props.renderHeader ? (
              props.renderHeader({ handleTestRun })
            ) : (
              <WorkflowHeader />
            )}
            <RetrieveBanner />
            {/* No need to display within the project */}
            {!workflowState.projectId && <ModifyBanner />}
            <div className={`${styles.workflowContent} clean-code`}>
              {!readonly && Sidebar ? <Sidebar ref={addNodeRef} /> : null}
              <AddNodeModalProvider ref={addNodeRef} readonly={readonly}>
                <div
                  id={WORKFLOW_PLAYGROUND_CONTENT_ID}
                  ref={workflowPlaygroundContentRef}
                  className={styles.workflowPlayground}
                >
                  <div
                    ref={drop}
                    className={styles.workflowPlaygroundRender}
                    // onDrop={onDrop}
                    onDragOver={onDragOver}
                  >
                    <PlaygroundReactRenderer />

                    <DragTooltip />

                    <WorkflowFloatLayout
                      components={{
                        problemPanel: () => <ProblemPanel />,
                        templatePanel: () =>
                          showTemplatePreview ? <TemplatePanel /> : null,
                      }}
                    >
                      <Toolbar
                        disableTraceAndTestRun={props?.disableTraceAndTestRun}
                      />
                      {showTemplatePreview ? <TemplatePreview /> : null}
                      {getFlags()['bot.automation.encapsulate'] ? (
                        <EncapsulatePanel />
                      ) : null}
                    </WorkflowFloatLayout>
                    <WorkflowRefreshModal />
                  </div>
                </div>
              </AddNodeModalProvider>
              {/* The space occupied by the local pull window used to render the interior of the canvas will squeeze the canvas */}
              <WorkflowInnerSideSheetHolder />
            </div>
          </div>
          <WorkflowOuterSideSheetHolder />
        </div>
        <ChatTestRunPauseSideSheet />
      </QueryClientProvider>
    );
  }

  return (
    <>
      {!workflowState.projectId && (
        <Helmet>
          <title>
            {I18n.t('workflow_tab_title', {
              name: workflowState.info?.name,
            })}
          </title>
        </Helmet>
      )}

      <div
        className={classnames({
          [styles.workflowContainer]: true,
          [styles.workflowContainerOp]: IS_BOT_OP,
          [props.className || '']: props.className,
        })}
        style={props.style}
      >
        {playgroundContent}

        <DatabaseDetailModal />
      </div>
    </>
  );
});

export default WorkflowContainer;
