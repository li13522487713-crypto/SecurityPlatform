import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import {
  ConnectorTemplateMappingDesigner,
  defaultConnectorTemplateMappingDesignerLabels,
  type LocalFormField,
} from './ConnectorTemplateMappingDesigner';
import type { ExternalApprovalTemplateResponse } from '../types';

const baseTemplate: ExternalApprovalTemplateResponse = {
  externalTemplateId: 'tpl-1',
  name: 'Leave request',
  description: 'Employee leave',
  controls: [
    { controlId: 'days', controlType: 'number', title: 'Days', required: true },
    { controlId: 'reason', controlType: 'input', title: 'Reason', required: false },
  ],
  fetchedAt: '2026-04-18T00:00:00Z',
};

const localFields: LocalFormField[] = [
  { key: 'days', label: 'Days', valueType: 'number', required: true },
  { key: 'reason', label: 'Reason', valueType: 'string', required: false },
];

describe('ConnectorTemplateMappingDesigner', () => {
  it('blocks save when required local field is unmapped', async () => {
    const onSave = vi.fn().mockResolvedValue(undefined);
    render(
      <ConnectorTemplateMappingDesigner
        template={baseTemplate}
        localFields={localFields}
        providerId={1}
        flowDefinitionId={100}
        onSave={onSave}
        labels={defaultConnectorTemplateMappingDesignerLabels}
      />,
    );

    fireEvent.click(screen.getByText(defaultConnectorTemplateMappingDesignerLabels.save));

    expect(onSave).not.toHaveBeenCalled();
    expect(
      screen.getAllByText(new RegExp(defaultConnectorTemplateMappingDesignerLabels.unmappedRequired, 'i')).length,
    ).toBeGreaterThan(0);
  });

  it('serializes mapping rows back to fieldMappingJson on save', async () => {
    const onSave = vi.fn().mockResolvedValue(undefined);
    render(
      <ConnectorTemplateMappingDesigner
        template={baseTemplate}
        localFields={localFields}
        providerId={1}
        flowDefinitionId={100}
        existing={{
          id: 1,
          providerId: 1,
          flowDefinitionId: 100,
          externalTemplateId: 'tpl-1',
          integrationMode: 'Hybrid',
          fieldMappingJson: JSON.stringify([
            { localFieldKey: 'days', externalControlId: 'days', valueType: 'number' },
          ]),
          enabled: true,
          createdAt: '2026-04-18T00:00:00Z',
          updatedAt: '2026-04-18T00:00:00Z',
        }}
        onSave={onSave}
        labels={defaultConnectorTemplateMappingDesignerLabels}
      />,
    );

    fireEvent.click(screen.getByText(defaultConnectorTemplateMappingDesignerLabels.save));

    await vi.waitFor(() => expect(onSave).toHaveBeenCalledTimes(1));
    const payload = onSave.mock.calls[0][0];
    expect(payload.externalTemplateId).toBe('tpl-1');
    expect(payload.integrationMode).toBe('Hybrid');
    const rows = JSON.parse(payload.fieldMappingJson) as Array<{ localFieldKey: string; externalControlId: string }>;
    expect(rows).toHaveLength(1);
    expect(rows[0].localFieldKey).toBe('days');
  });

  it('renders empty placeholder when template has no controls', () => {
    render(
      <ConnectorTemplateMappingDesigner
        template={{ ...baseTemplate, controls: [] }}
        localFields={localFields}
        providerId={1}
        flowDefinitionId={100}
        onSave={vi.fn()}
        labels={defaultConnectorTemplateMappingDesignerLabels}
      />,
    );
    expect(screen.getByText(defaultConnectorTemplateMappingDesignerLabels.noTemplate)).toBeTruthy();
  });
});
