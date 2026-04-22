// @vitest-environment jsdom

import { fireEvent, render, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { LowcodeStudioApp } from './studio-app';
import type { LowcodeStudioHostConfig } from '../host';

const {
  acquireMock,
  renewMock,
  releaseMock,
  getDraftMock,
  autosaveMock,
  toastSuccessMock,
  toastErrorMock,
  toastWarningMock
} = vi.hoisted(() => ({
  acquireMock: vi.fn(),
  renewMock: vi.fn(),
  releaseMock: vi.fn(),
  getDraftMock: vi.fn(),
  autosaveMock: vi.fn(),
  toastSuccessMock: vi.fn(),
  toastErrorMock: vi.fn(),
  toastWarningMock: vi.fn()
}));

vi.mock('@douyinfe/semi-ui', () => {
  const Layout = ({ children }: { children?: ReactNode }) => <div data-testid="layout">{children}</div>;
  Layout.Header = ({ children }: { children?: ReactNode }) => <div data-testid="layout-header">{children}</div>;
  Layout.Sider = ({ children }: { children?: ReactNode }) => <div data-testid="layout-sider">{children}</div>;
  Layout.Content = ({ children }: { children?: ReactNode }) => <div data-testid="layout-content">{children}</div>;

  return {
    Layout,
    Tabs: ({ children }: { children?: ReactNode }) => <div data-testid="tabs">{children}</div>,
    TabPane: ({ children }: { children?: ReactNode }) => <div data-testid="tab-pane">{children}</div>,
    Empty: ({ title }: { title?: ReactNode }) => <div data-testid="empty">{title}</div>,
    Toast: {
      success: toastSuccessMock,
      error: toastErrorMock,
      warning: toastWarningMock
    }
  };
});

vi.mock('../panels/left-panel', () => ({
  LeftPanel: () => <div data-testid="left-panel" />
}));

vi.mock('../panels/right-inspector', () => ({
  RightInspector: () => <div data-testid="right-inspector" />
}));

vi.mock('../panels/top-toolbar', () => ({
  TopToolbar: () => <div data-testid="top-toolbar" />
}));

vi.mock('../panels/canvas-viewport', () => ({
  CanvasViewport: () => <div data-testid="canvas-viewport" />
}));

vi.mock('../panels/workflow-left-panel', () => ({
  WorkflowLeftPanel: () => <div data-testid="workflow-left-panel" />
}));

vi.mock('../panels/workflow-canvas', () => ({
  WorkflowCanvas: () => <div data-testid="workflow-canvas" />
}));

vi.mock('../panels/shortcut-panel', () => ({
  ShortcutPanel: () => <div data-testid="shortcut-panel" />
}));

vi.mock('../i18n', () => ({
  setLocale: vi.fn(),
  t: (key: string) => key
}));

describe('LowcodeStudioApp', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    acquireMock.mockResolvedValue({ acquired: true, lock: null });
    renewMock.mockResolvedValue(undefined);
    releaseMock.mockResolvedValue(undefined);
    getDraftMock.mockResolvedValue({
      schemaJson: '{"pages":[]}',
      schemaVersion: '1',
      updatedAt: '2026-04-22T00:00:00Z'
    });
    autosaveMock.mockResolvedValue(undefined);
  });

  function createHost(): LowcodeStudioHostConfig {
    return {
      api: {
        apps: {
          getDraft: getDraftMock,
          autosave: autosaveMock
        },
        draftLock: {
          acquire: acquireMock,
          renew: renewMock,
          release: releaseMock
        }
      } as unknown as LowcodeStudioHostConfig['api'],
      auth: {
        accessTokenFactory: () => 'token-from-host',
        tenantIdFactory: () => 'tenant-from-host',
        userIdFactory: () => 'user-from-host'
      }
    };
  }

  it('在 host provider 内执行草稿锁与快捷键逻辑', async () => {
    const host = createHost();
    const { unmount } = render(
      <LowcodeStudioApp
        appId="1496214388233736192"
        locale="zh-CN"
        workspaceId="ws-1"
        workspaceLabel="工作空间"
        host={host}
      />
    );

    await waitFor(() => {
      expect(acquireMock).toHaveBeenCalledWith(
        '1496214388233736192',
        expect.stringMatching(/^studio-/)
      );
    });

    fireEvent.keyDown(window, { key: 's', ctrlKey: true });

    await waitFor(() => {
      expect(getDraftMock).toHaveBeenCalledWith('1496214388233736192');
      expect(autosaveMock).toHaveBeenCalledWith('1496214388233736192', '{"pages":[]}');
    });

    unmount();
  });
});
