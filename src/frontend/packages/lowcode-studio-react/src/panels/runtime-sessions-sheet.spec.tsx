// @vitest-environment jsdom

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { RuntimeSessionsSheet } from './runtime-sessions-sheet';
import { LowcodeStudioHostProvider, type LowcodeStudioHostConfig } from '../host';

const { runtimeSessionsListMock } = vi.hoisted(() => ({
  runtimeSessionsListMock: vi.fn()
}));

vi.mock('@douyinfe/semi-ui', () => ({
  Banner: ({ description }: { description?: ReactNode }) => <div>{description}</div>,
  Button: ({ children }: { children?: ReactNode }) => <button type="button">{children}</button>,
  Empty: ({ title }: { title?: ReactNode }) => <div>{title}</div>,
  Input: ({ value }: { value?: string }) => <input value={value} readOnly />,
  List: Object.assign(
    ({ dataSource, renderItem, emptyContent }: { dataSource?: unknown[]; renderItem: (item: any) => ReactNode; emptyContent?: ReactNode }) => (
      <div>{dataSource && dataSource.length > 0 ? dataSource.map((item, index) => <div key={index}>{renderItem(item)}</div>) : emptyContent}</div>
    ),
    {
      Item: ({ children, extra }: { children?: ReactNode; extra?: ReactNode }) => <div>{children}{extra}</div>
    }
  ),
  SideSheet: ({ visible, children }: { visible?: boolean; children?: ReactNode }) => visible ? <div>{children}</div> : null,
  Space: ({ children }: { children?: ReactNode }) => <div>{children}</div>,
  Spin: () => <div>loading</div>,
  Tag: ({ children }: { children?: ReactNode }) => <span>{children}</span>,
  Toast: {
    success: vi.fn(),
    error: vi.fn()
  },
  Typography: {
    Paragraph: ({ children }: { children?: ReactNode }) => <p>{children}</p>,
    Text: ({ children }: { children?: ReactNode }) => <span>{children}</span>
  }
}));

function buildHost(): LowcodeStudioHostConfig {
  return {
    api: {} as LowcodeStudioHostConfig['api'],
    auth: {
      accessTokenFactory: () => 'token',
      tenantIdFactory: () => 'tenant',
      userIdFactory: () => 'user'
    },
    runtimeSessions: {
      list: runtimeSessionsListMock,
      create: vi.fn(),
      clear: vi.fn(),
      pin: vi.fn(),
      archive: vi.fn(),
      switchTo: vi.fn()
    }
  };
}

describe('RuntimeSessionsSheet', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    runtimeSessionsListMock.mockResolvedValue([
      { id: 'sess-1', title: '最近会话', pinned: false, archived: false, updatedAt: '2026-04-22T08:00:00Z' }
    ]);
  });

  it('在 visible=true 时读取 runtime sessions', async () => {
    const client = new QueryClient({
      defaultOptions: {
        queries: { retry: false }
      }
    });

    render(
      <QueryClientProvider client={client}>
        <LowcodeStudioHostProvider host={buildHost()}>
          <RuntimeSessionsSheet visible onClose={() => undefined} />
        </LowcodeStudioHostProvider>
      </QueryClientProvider>
    );

    await waitFor(() => {
      expect(runtimeSessionsListMock).toHaveBeenCalledTimes(1);
    });
  });
});
