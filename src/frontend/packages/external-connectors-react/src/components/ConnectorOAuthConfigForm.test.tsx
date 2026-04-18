import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { ConnectorOAuthConfigForm, type ConnectorOAuthConfigValue } from './ConnectorOAuthConfigForm';

const baseValue: ConnectorOAuthConfigValue = {
  callbackBaseUrl: '',
  trustedDomains: '',
  visibilityScope: '',
  syncCron: '',
  agentId: '',
};

describe('ConnectorOAuthConfigForm', () => {
  it('hides agentId field when showAgentId is false', () => {
    render(<ConnectorOAuthConfigForm value={baseValue} onChange={vi.fn()} showAgentId={false} />);
    expect(screen.queryByText('AgentId')).toBeNull();
  });

  it('shows agentId field when showAgentId is true', () => {
    render(<ConnectorOAuthConfigForm value={baseValue} onChange={vi.fn()} showAgentId={true} />);
    expect(screen.getByText('AgentId')).toBeTruthy();
  });

  it('emits onChange with merged value when callbackBaseUrl typed', () => {
    const onChange = vi.fn();
    render(<ConnectorOAuthConfigForm value={baseValue} onChange={onChange} />);
    const input = screen.getByPlaceholderText(/api\/v1\/connectors\/providers/) as HTMLInputElement;
    fireEvent.change(input, { target: { value: 'https://platform.example.com/cb' } });
    expect(onChange).toHaveBeenCalledWith(expect.objectContaining({ callbackBaseUrl: 'https://platform.example.com/cb' }));
  });
});
