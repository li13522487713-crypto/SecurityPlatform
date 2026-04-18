import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MessageCardPreview } from './MessageCardPreview';
import type { ExternalMessageCard } from '../types';

const baseCard: ExternalMessageCard = {
  title: '年假申请待审批',
  subtitle: '申请人：张三',
  content: '请假天数：3 天\n开始时间：2026-04-20',
  fields: [
    { key: '申请人', value: '张三' },
    { key: '天数', value: '3' },
  ],
  jumpUrl: 'https://platform.example.com/approval/1',
  cardVersion: 1,
};

describe('MessageCardPreview', () => {
  it('renders WeCom badge for WeCom provider', () => {
    render(<MessageCardPreview providerType="WeCom" card={baseCard} />);
    expect(screen.getByText(/企业微信/)).toBeTruthy();
    expect(screen.getByTestId('message-card-preview-wecom')).toBeTruthy();
  });

  it('renders Feishu interactive label for Feishu provider', () => {
    render(<MessageCardPreview providerType="Feishu" card={baseCard} />);
    expect(screen.getByText(/飞书/)).toBeTruthy();
    expect(screen.getByTestId('message-card-preview-feishu')).toBeTruthy();
  });

  it('renders DingTalk action_card label for DingTalk provider', () => {
    render(<MessageCardPreview providerType="DingTalk" card={baseCard} />);
    expect(screen.getByText(/钉钉/)).toBeTruthy();
    expect(screen.getByTestId('message-card-preview-dingtalk')).toBeTruthy();
  });

  it('shows fields and jump action', () => {
    render(<MessageCardPreview providerType="WeCom" card={baseCard} />);
    expect(screen.getByText('申请人')).toBeTruthy();
    expect(screen.getByText('张三')).toBeTruthy();
    expect(screen.getByText('查看详情 →')).toBeTruthy();
    expect(screen.getByText('v1')).toBeTruthy();
  });
});
