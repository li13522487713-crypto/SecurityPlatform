import React from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render } from '@testing-library/react';

import { WorkflowPage } from '../src/page';

const mockScrollToNode = vi.fn();
const mockShowTestRunResult = vi.fn();
const mockTriggerFitView = vi.fn();
const mockResetToHistory = vi.fn();
const mockNavigateBack = vi.fn();

let latestOnInit: ((workflowState: unknown) => void) | undefined;

vi.mock('@coze-workflow/playground/workflow-playground', () => {
  return {
    WorkflowPlayground: React.forwardRef(
      (
        props: {
          onInit?: (workflowState: unknown) => void;
        },
        ref: React.Ref<unknown>,
      ) => {
        latestOnInit = props.onInit;
        React.useImperativeHandle(ref, () => ({
          scrollToNode: mockScrollToNode,
          showTestRunResult: mockShowTestRunResult,
          triggerFitView: mockTriggerFitView,
          resetToHistory: mockResetToHistory,
        }));
        return <div data-testid="workflow-playground" />;
      },
    ),
  };
});

vi.mock('../src/hooks/use-page-params', () => ({
  usePageParams: (props: {
    nodeId?: string;
    executeId?: string;
    subExecuteId?: string;
  }) => ({
    spaceId: 'space-1',
    workflowId: 'workflow-1',
    version: undefined,
    setVersion: false,
    optType: undefined,
    from: undefined,
    nodeId: props.nodeId,
    executeId: props.executeId,
    subExecuteId: props.subExecuteId,
    returnUrl: undefined,
  }),
}));

vi.mock('../src/hooks', () => ({
  useNavigateBack: () => ({
    navigateBack: mockNavigateBack,
  }),
}));

describe('WorkflowPage 首屏初始化', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.stubGlobal('requestAnimationFrame', (cb: FrameRequestCallback) => {
      return window.setTimeout(() => cb(performance.now()), 0);
    });
    latestOnInit = undefined;
    mockScrollToNode.mockReset();
    mockShowTestRunResult.mockReset();
    mockTriggerFitView.mockReset();
    mockResetToHistory.mockReset();
    mockNavigateBack.mockReset();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.useRealTimers();
  });

  it('普通进入时会触发首屏 fitView 调度', async () => {
    render(<WorkflowPage />);
    expect(latestOnInit).toBeTypeOf('function');

    act(() => {
      latestOnInit?.({});
    });
    expect(mockTriggerFitView).toHaveBeenCalledTimes(0);

    await act(async () => {
      vi.runAllTimers();
    });

    expect(mockTriggerFitView).toHaveBeenCalledTimes(2);
  });

  it('带 nodeId 时仅滚动节点，不触发全局 fitView', async () => {
    render(<WorkflowPage nodeId="node-1" />);
    expect(latestOnInit).toBeTypeOf('function');

    act(() => {
      latestOnInit?.({});
    });
    await act(async () => {
      vi.runAllTimers();
    });

    expect(mockScrollToNode).toHaveBeenCalledWith('node-1');
    expect(mockTriggerFitView).not.toHaveBeenCalled();
  });
});
