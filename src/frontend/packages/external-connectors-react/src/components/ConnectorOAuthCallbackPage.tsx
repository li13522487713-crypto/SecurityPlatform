import { useEffect, useState } from 'react';
import type { ConnectorApi } from '../api';
import type { OAuthCallbackResult } from '../types';

export interface ConnectorOAuthCallbackPageProps {
  api: ConnectorApi;
  /** 从浏览器 URL 解析出的 state / code，由宿主路由层注入。 */
  state: string;
  code: string;
  /** 已绑定情况下持久化 token 的回调。 */
  onAuthenticated?: (token: { accessToken: string; refreshToken?: string; expiresAt?: string; localUserId?: number }) => void;
  /** 未绑定 / 冲突情况下让宿主跳到 PendingBinding 页。 */
  onPending?: (info: { kind: 'manual' | 'conflict'; ticket?: string; redirectTo?: string }) => void;
}

export function ConnectorOAuthCallbackPage({ api, state, code, onAuthenticated, onPending }: ConnectorOAuthCallbackPageProps) {
  const [result, setResult] = useState<OAuthCallbackResult | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    api
      .completeOAuth(state, code)
      .then((r) => {
        if (!active) return;
        setResult(r);
        if (r.accessToken) {
          onAuthenticated?.({
            accessToken: r.accessToken,
            refreshToken: r.refreshToken,
            expiresAt: r.expiresAt,
            localUserId: r.localUserId,
          });
        } else {
          onPending?.({
            kind: r.redirectTo?.includes('bindingConflict') ? 'conflict' : 'manual',
            ticket: r.pendingBindingTicket,
            redirectTo: r.redirectTo ?? undefined,
          });
        }
      })
      .catch((err: unknown) => {
        if (!active) return;
        setError(err instanceof Error ? err.message : String(err));
      });
    return () => {
      active = false;
    };
  }, [api, state, code, onAuthenticated, onPending]);

  if (error) {
    return (
      <div data-testid="connector-oauth-callback-error" style={{ padding: 24 }}>
        <h3>登录失败</h3>
        <p style={{ color: 'red' }}>{error}</p>
      </div>
    );
  }

  if (!result) {
    return <p data-testid="connector-oauth-callback-loading">正在完成外部登录...</p>;
  }

  return (
    <div data-testid="connector-oauth-callback-result" style={{ padding: 24 }}>
      <p>
        外部用户：{result.displayName ?? result.externalUserId}（{result.externalUserId}）
      </p>
      {result.localUserId && <p>已绑定本地用户 #{result.localUserId}</p>}
      {!result.accessToken && <p>需要管理员或本人完成绑定后才能登录。</p>}
    </div>
  );
}
