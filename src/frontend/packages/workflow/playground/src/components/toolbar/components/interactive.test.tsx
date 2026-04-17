import React from 'react';
import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { Interactive } from './interactive';

const mockSetMouseScrollDelta = vi.fn();
const mockSetInteractiveType = vi.fn();

let calibrated = false;

vi.mock('@flowgram-adapter/free-layout-editor', () => ({
  usePlaygroundTools: () => ({
    setMouseScrollDelta: mockSetMouseScrollDelta,
    setInteractiveType: mockSetInteractiveType,
  }),
}));

vi.mock('@coze-arch/i18n', () => ({
  I18n: {
    t: (key: string) => key,
  },
}));

vi.mock('@coze-arch/coze-design', () => ({
  Tooltip: ({
    children,
    visible,
  }: {
    children: React.ReactNode;
    visible?: boolean;
  }) =>
    React.createElement(
      'div',
      {
        'data-testid': 'tooltip',
        'data-visible': String(Boolean(visible)),
      },
      children,
    ),
}));

vi.mock('@coze-common/mouse-pad-selector', () => {
  enum InteractiveType {
    Mouse = 'mouse',
    Pad = 'pad',
  }

  return {
    InteractiveType,
    getPreferInteractiveType: () => InteractiveType.Mouse,
    setPreferInteractiveType: vi.fn(),
    GuidingPopover: ({ children }: { children: React.ReactNode }) =>
      React.createElement(
        'div',
        { 'data-testid': 'guiding-popover' },
        children,
      ),
    MousePadSelector: ({
      onPopupVisibleChange,
    }: {
      onPopupVisibleChange?: (visible: boolean) => void;
    }) =>
      React.createElement('button', {
        'data-testid': 'mouse-pad-selector',
        onClick: () => {
          onPopupVisibleChange?.(true);
        },
        type: 'button',
      }),
  };
});

vi.mock('@/hooks', () => ({
  useGlobalState: () => ({
    config: {
      initialViewportCalibrated: calibrated,
    },
  }),
}));

describe('Interactive 引导挂载门槛', () => {
  it('首屏未完成视口校准时不挂载 GuidingPopover', () => {
    calibrated = false;
    render(React.createElement(Interactive));
    expect(screen.queryByTestId('guiding-popover')).toBeNull();
    expect(screen.queryByTestId('mouse-pad-selector')).not.toBeNull();
  });

  it('完成视口校准后才挂载 GuidingPopover', () => {
    calibrated = true;
    render(React.createElement(Interactive));
    expect(screen.queryByTestId('guiding-popover')).not.toBeNull();
  });

  it('打开交互面板时会隐藏 tooltip', () => {
    calibrated = false;
    render(React.createElement(Interactive));

    const interactive = screen.getByTestId(
      'workflow.detail.toolbar.interactive',
    );

    fireEvent.mouseEnter(interactive);
    expect(screen.getByTestId('tooltip')).toHaveAttribute(
      'data-visible',
      'true',
    );

    fireEvent.click(screen.getByTestId('mouse-pad-selector'));
    expect(screen.getByTestId('tooltip')).toHaveAttribute(
      'data-visible',
      'false',
    );
  });
});
