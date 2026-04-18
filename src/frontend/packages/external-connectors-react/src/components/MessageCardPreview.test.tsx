import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MessageCardPreview, defaultMessageCardPreviewLabels } from './MessageCardPreview';
import type { ExternalMessageCard } from '../types';

const baseCard: ExternalMessageCard = {
  title: 'Annual leave request pending',
  subtitle: 'Applicant: Zhang San',
  content: 'Days: 3\nStart: 2026-04-20',
  fields: [
    { key: 'Applicant', value: 'Zhang San' },
    { key: 'Days', value: '3' },
  ],
  jumpUrl: 'https://platform.example.com/approval/1',
  cardVersion: 1,
};

describe('MessageCardPreview', () => {
  it('renders WeCom badge for WeCom provider', () => {
    render(
      <MessageCardPreview providerType="WeCom" card={baseCard} labels={defaultMessageCardPreviewLabels} />,
    );
    expect(screen.getByText(defaultMessageCardPreviewLabels.providerWeCom)).toBeTruthy();
    expect(screen.getByTestId('message-card-preview-wecom')).toBeTruthy();
  });

  it('renders Feishu interactive label for Feishu provider', () => {
    render(
      <MessageCardPreview providerType="Feishu" card={baseCard} labels={defaultMessageCardPreviewLabels} />,
    );
    expect(screen.getByText(defaultMessageCardPreviewLabels.providerFeishu)).toBeTruthy();
    expect(screen.getByTestId('message-card-preview-feishu')).toBeTruthy();
  });

  it('renders DingTalk action_card label for DingTalk provider', () => {
    render(
      <MessageCardPreview providerType="DingTalk" card={baseCard} labels={defaultMessageCardPreviewLabels} />,
    );
    expect(screen.getByText(defaultMessageCardPreviewLabels.providerDingTalk)).toBeTruthy();
    expect(screen.getByTestId('message-card-preview-dingtalk')).toBeTruthy();
  });

  it('shows fields and jump action', () => {
    render(
      <MessageCardPreview providerType="WeCom" card={baseCard} labels={defaultMessageCardPreviewLabels} />,
    );
    expect(screen.getByText('Applicant')).toBeTruthy();
    expect(screen.getByText('Zhang San')).toBeTruthy();
    expect(screen.getByText(defaultMessageCardPreviewLabels.viewDetails)).toBeTruthy();
    expect(screen.getByText('v1')).toBeTruthy();
  });
});
