import React from 'react';
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';

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
  Tooltip: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="tooltip">{children}</div>
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
    GuidingPopover: ({ children }: { children: React.ReactNode }) => (
      <div data-testid="guiding-popover">{children}</div>
    ),
    MousePadSelector: () => <div data-testid="mouse-pad-selector" />,
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
    render(<Interactive />);
    expect(screen.queryByTestId('guiding-popover')).toBeNull();
    expect(screen.queryByTestId('mouse-pad-selector')).not.toBeNull();
  });

  it('完成视口校准后才挂载 GuidingPopover', () => {
    calibrated = true;
    render(<Interactive />);
    expect(screen.queryByTestId('guiding-popover')).not.toBeNull();
  });
});
