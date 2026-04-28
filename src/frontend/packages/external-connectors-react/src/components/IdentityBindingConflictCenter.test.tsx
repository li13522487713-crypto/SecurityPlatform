import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import {
  IdentityBindingConflictCenter,
  defaultIdentityBindingConflictCenterLabels,
} from './IdentityBindingConflictCenter';
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
  it('renders conflict list rows', () => {
    const api = buildApi();
    render(
      <IdentityBindingConflictCenter
        api={api}
        providerId={7}
        conflicts={conflicts}
        onResolved={vi.fn()}
        labels={defaultIdentityBindingConflictCenterLabels}
      />,
    );
    expect(screen.getByText('wxZhangsan')).toBeTruthy();
    expect(screen.getByText('Conflict')).toBeTruthy();
  });

  it('renders empty state when no conflicts', () => {
    const api = buildApi();
    render(
      <IdentityBindingConflictCenter
        api={api}
        providerId={7}
        conflicts={[]}
        onResolved={vi.fn()}
        labels={defaultIdentityBindingConflictCenterLabels}
      />,
    );
    expect(
      screen.getByText(defaultIdentityBindingConflictCenterLabels.noConflicts),
    ).toBeTruthy();
  });

  it('rejects manual binding submit when required fields are blank', () => {
    const createManualBinding = vi.fn().mockResolvedValue(undefined);
    const api = buildApi({ createManualBinding });
    render(
      <IdentityBindingConflictCenter
        api={api}
        providerId={7}
        conflicts={[]}
        onResolved={vi.fn()}
        labels={defaultIdentityBindingConflictCenterLabels}
      />,
    );

    fireEvent.click(screen.getByText(defaultIdentityBindingConflictCenterLabels.submitManualBind));
    expect(createManualBinding).not.toHaveBeenCalled();
    expect(
      screen.getByText(defaultIdentityBindingConflictCenterLabels.requiredFieldsMissing),
    ).toBeTruthy();
  });
});
