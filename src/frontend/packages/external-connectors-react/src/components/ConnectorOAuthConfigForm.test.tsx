import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import {
  ConnectorOAuthConfigForm,
  defaultConnectorOAuthConfigFormLabels,
  type ConnectorOAuthConfigValue,
} from './ConnectorOAuthConfigForm';

const baseValue: ConnectorOAuthConfigValue = {
  callbackBaseUrl: '',
  trustedDomains: '',
  visibilityScope: '',
  syncCron: '',
  agentId: '',
};

describe('ConnectorOAuthConfigForm', () => {
  it('hides agentId field when showAgentId is false', () => {
    render(
      <ConnectorOAuthConfigForm
        value={baseValue}
        onChange={vi.fn()}
        showAgentId={false}
        labels={defaultConnectorOAuthConfigFormLabels}
      />,
    );
    expect(screen.queryByText(defaultConnectorOAuthConfigFormLabels.agentId)).toBeNull();
  });

  it('shows agentId field when showAgentId is true', () => {
    render(
      <ConnectorOAuthConfigForm
        value={baseValue}
        onChange={vi.fn()}
        showAgentId
        labels={defaultConnectorOAuthConfigFormLabels}
      />,
    );
    expect(screen.getByText(defaultConnectorOAuthConfigFormLabels.agentId)).toBeTruthy();
  });

  it('emits onChange with merged value when callbackBaseUrl typed', () => {
    const onChange = vi.fn();
    render(
      <ConnectorOAuthConfigForm
        value={baseValue}
        onChange={onChange}
        labels={defaultConnectorOAuthConfigFormLabels}
      />,
    );
    const input = screen.getByPlaceholderText(/api\/v1\/connectors\/providers/) as HTMLInputElement;
    fireEvent.change(input, { target: { value: 'https://platform.example.com/cb' } });
    expect(onChange).toHaveBeenCalledWith(
      expect.objectContaining({ callbackBaseUrl: 'https://platform.example.com/cb' }),
    );
  });
});
