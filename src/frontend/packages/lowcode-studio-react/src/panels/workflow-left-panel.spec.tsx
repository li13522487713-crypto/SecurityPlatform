// @vitest-environment jsdom

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { WorkflowLeftPanel } from './workflow-left-panel';
import { LowcodeStudioHostProvider, type LowcodeStudioHostConfig } from '../host';
import { useStudioSelection } from '../stores/selection-store';

const {
  runtimeSessionsListMock,
  resourcesSearchMock,
  listBindingsMock,
  variablesListMock,
  unbindMock,
  runtimeSessionsSheetPropsMock
} = vi.hoisted(() => ({
  runtimeSessionsListMock: vi.fn(),
  resourcesSearchMock: vi.fn(),
  listBindingsMock: vi.fn(),
  variablesListMock: vi.fn(),
  unbindMock: vi.fn(),
  runtimeSessionsSheetPropsMock: vi.fn()
}));

vi.mock('@douyinfe/semi-ui', () => {
  const Collapse = ({ children }: { children?: ReactNode }) => <div>{children}</div>;
  Collapse.Panel = ({ children, header, itemKey }: { children?: ReactNode; header?: ReactNode; itemKey: string }) => (
    <section data-testid={`panel-${itemKey}`}>
      {header}
      {children}
    </section>
  );

  const List = ({ dataSource, renderItem, emptyContent }: { dataSource?: unknown[]; renderItem: (item: any) => ReactNode; emptyContent?: ReactNode }) => (
    <div>{dataSource && dataSource.length > 0 ? dataSource.map((item, index) => <div key={index}>{renderItem(item)}</div>) : emptyContent}</div>
  );
  List.Item = ({ children, extra, onClick }: { children?: ReactNode; extra?: ReactNode; onClick?: () => void }) => (
    <div onClick={onClick}>
      <div>{children}</div>
      <div>{extra}</div>
    </div>
  );

  const Form = ({ children }: { children?: ReactNode }) => <div>{children}</div>;
  Form.Input = ({ label }: { label?: ReactNode }) => <div>{label}</div>;
  Form.Select = ({ label }: { label?: ReactNode }) => <div>{label}</div>;
  Form.TextArea = ({ label }: { label?: ReactNode }) => <div>{label}</div>;
  Form.Switch = ({ label }: { label?: ReactNode }) => <div>{label}</div>;
  Form.Slot = ({ children }: { children?: ReactNode }) => <div>{children}</div>;

  return {
    Banner: ({ description }: { description?: ReactNode }) => <div>{description}</div>,
    Button: ({ children, onClick }: { children?: ReactNode; onClick?: () => void }) => <button type="button" onClick={onClick}>{children}</button>,
    Collapse,
    Empty: ({ title, description }: { title?: ReactNode; description?: ReactNode }) => <div>{title}{description}</div>,
    Form,
    Input: ({ value, onChange, placeholder }: { value?: string; onChange?: (value: string) => void; placeholder?: string }) => (
      <input value={value} placeholder={String(placeholder ?? '')} onChange={(event) => onChange?.(event.target.value)} />
    ),
    List,
    Modal: Object.assign(
      ({ visible, children, title }: { visible?: boolean; children?: ReactNode; title?: ReactNode }) => visible ? <div>{title}{children}</div> : null,
      { confirm: vi.fn() }
    ),
    SideSheet: ({ visible, children, title }: { visible?: boolean; children?: ReactNode; title?: ReactNode }) => visible ? <div>{title}{children}</div> : null,
    Space: ({ children }: { children?: ReactNode }) => <div>{children}</div>,
    Spin: () => <div>loading</div>,
    Tag: ({ children }: { children?: ReactNode }) => <span>{children}</span>,
    Toast: {
      success: vi.fn(),
      error: vi.fn()
    },
    Typography: {
      Text: ({ children }: { children?: ReactNode }) => <span>{children}</span>,
      Paragraph: ({ children }: { children?: ReactNode }) => <p>{children}</p>
    }
  };
});

vi.mock('./runtime-sessions-sheet', () => ({
  RuntimeSessionsSheet: (props: Record<string, unknown>) => {
    runtimeSessionsSheetPropsMock(props);
    return props.visible ? <div data-testid="runtime-sessions-sheet" /> : null;
  }
}));

function buildHost(): LowcodeStudioHostConfig {
  return {
    api: {
      resources: {
        search: resourcesSearchMock,
        listBindings: listBindingsMock,
        unbind: unbindMock,
        bind: vi.fn()
      },
      variables: {
        list: variablesListMock,
        create: vi.fn(),
        update: vi.fn(),
        delete: vi.fn()
      }
    } as unknown as LowcodeStudioHostConfig['api'],
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

function renderPanel() {
  const host = buildHost();
  const client = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false }
    }
  });
  return render(
    <QueryClientProvider client={client}>
      <LowcodeStudioHostProvider host={host}>
        <WorkflowLeftPanel appId="1496214388233736192" workspaceId="ws-1" />
      </LowcodeStudioHostProvider>
    </QueryClientProvider>
  );
}

describe('WorkflowLeftPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useStudioSelection.setState({
      selectedComponentId: null,
      currentPageCode: null,
      selectedWorkflowId: 'wf-1'
    });
    resourcesSearchMock.mockImplementation(async (_appId: string, params?: { types?: string; boundOnly?: boolean }) => {
      if (params?.types === 'workflow') {
        return { byType: { workflow: [{ id: 'wf-1', name: 'ag', resourceType: 'workflow' }] }, total: 1 };
      }
      if (params?.types === 'plugin') {
        return { byType: { plugin: [{ id: '11', name: '绘画插件', description: 'image plugin', resourceType: 'plugin' }] }, total: 1 };
      }
      return { byType: {}, total: 0 };
    });
    listBindingsMock.mockResolvedValue([
      { id: 91, appId: 1496214388233736192, resourceType: 'plugin', resourceId: 11, role: '', displayOrder: 0, configJson: '{}', createdAt: '2026-04-22T00:00:00Z', updatedAt: null }
    ]);
    variablesListMock.mockResolvedValue([
      { id: 'var-1', appId: '1496214388233736192', code: 'avatarPrompt', displayName: '头像提示词', scope: 'app', valueType: 'string', isReadOnly: false, isPersisted: false, defaultValueJson: '"cat"', description: '' }
    ]);
    runtimeSessionsListMock.mockResolvedValue([
      { id: 'sess-1', title: '最近会话', pinned: false, archived: false, updatedAt: '2026-04-22T08:00:00Z' }
    ]);
    unbindMock.mockResolvedValue(undefined);
  });

  it('点击设置中的会话管理会触发 runtime sessions 查询', async () => {
    renderPanel();

    fireEvent.click(screen.getByRole('button', { name: /会话管理/ }));

    await waitFor(() => {
      expect(runtimeSessionsSheetPropsMock).toHaveBeenLastCalledWith(expect.objectContaining({ visible: true }));
    });
  });

  it('插件解绑后不会丢失当前选中的工作流', async () => {
    renderPanel();

    fireEvent.click(screen.getByRole('button', { name: /解绑/ }));

    await waitFor(() => {
      expect(unbindMock).toHaveBeenCalledWith('1496214388233736192', 'plugin', 11);
    });
    expect(useStudioSelection.getState().selectedWorkflowId).toBe('wf-1');
  });
});
