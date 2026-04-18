import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { IdentityBindingConflictCenter } from './IdentityBindingConflictCenter';
import type { ConnectorApi } from '../api';
import type { ExternalIdentityBindingListItem } from '../types';

function buildApi(overrides: Partial<ConnectorApi> = {}): ConnectorApi {
  const stub = vi.fn().mockResolvedValue(undefined);
  return {
    listProviders: stub,
    getProvider: stub,
    createProvider: stub,
    updateProvider: stub,
    enableProvider: stub,
    disableProvider: stub,
    deleteProvider: stub,
    rotateSecret: stub,
    startOAuth: stub,
    completeOAuth: stub,
    listBindings: stub,
    createManualBinding: stub,
    resolveConflict: stub,
    deleteBinding: stub,
    runFullSync: stub,
    applyIncrementalSync: stub,
    listSyncJobs: stub,
    getSyncJob: stub,
    listSyncDiffs: stub,
    listApprovalTemplates: stub,
    refreshApprovalTemplate: stub,
    listApprovalTemplateMappings: stub,
    getApprovalTemplateMapping: stub,
    upsertApprovalTemplateMapping: stub,
    deleteApprovalTemplateMapping: stub,
    sendMessage: stub,
    updateMessageCard: stub,
    ...overrides,
  } as unknown as ConnectorApi;
}

const conflicts: ExternalIdentityBindingListItem[] = [
  {
    id: 1,
    providerId: 7,
    localUserId: 100,
    externalUserId: 'wxZhangsan',
    status: 'Conflict',
    matchStrategy: 'Mobile',
    boundAt: '2026-04-18T00:00:00Z',
  },
];

describe('IdentityBindingConflictCenter', () => {
  it('forwards SwitchToLocalUser resolution with newLocalUserId', async () => {
    const resolveConflict = vi.fn().mockResolvedValue(undefined);
    const onResolved = vi.fn();
    const api = buildApi({ resolveConflict });

    render(
      <IdentityBindingConflictCenter
        api={api}
        providerId={7}
        conflicts={conflicts}
        onResolved={onResolved}
      />,
    );

    const select = screen.getByDisplayValue('保留当前') as HTMLSelectElement;
    fireEvent.change(select, { target: { value: 'SwitchToLocalUser' } });

    const newLocalInput = screen.getByPlaceholderText('新本地用户 ID') as HTMLInputElement;
    fireEvent.change(newLocalInput, { target: { value: '200' } });

    fireEvent.click(screen.getByText('应用'));

    await vi.waitFor(() => expect(resolveConflict).toHaveBeenCalledTimes(1));
    expect(resolveConflict).toHaveBeenCalledWith({
      bindingId: 1,
      resolution: 'SwitchToLocalUser',
      newLocalUserId: 200,
    });
    await vi.waitFor(() => expect(onResolved).toHaveBeenCalled());
  });

  it('creates manual binding with required fields', async () => {
    const createManualBinding = vi.fn().mockResolvedValue(undefined);
    const onResolved = vi.fn();
    const api = buildApi({ createManualBinding });

    render(
      <IdentityBindingConflictCenter
        api={api}
        providerId={7}
        conflicts={[]}
        onResolved={onResolved}
      />,
    );

    fireEvent.click(screen.getByText('创建手动绑定'));
    expect(createManualBinding).not.toHaveBeenCalled();

    const inputs = screen.getAllByRole('spinbutton') as HTMLInputElement[];
    fireEvent.change(inputs[0], { target: { value: '500' } });
    const externalInput = screen.getAllByRole('textbox') as HTMLInputElement[];
    fireEvent.change(externalInput[0], { target: { value: 'wxLisi' } });

    fireEvent.click(screen.getByText('创建手动绑定'));

    await vi.waitFor(() => expect(createManualBinding).toHaveBeenCalledTimes(1));
    expect(createManualBinding).toHaveBeenCalledWith(expect.objectContaining({
      providerId: 7,
      localUserId: 500,
      externalUserId: 'wxLisi',
    }));
  });
});
