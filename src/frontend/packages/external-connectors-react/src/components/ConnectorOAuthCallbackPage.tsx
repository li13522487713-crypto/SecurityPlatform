import { useEffect, useState } from 'react';
import { Banner, Spin, Typography } from '@douyinfe/semi-ui';
import type { ConnectorApi } from '../api';
import type { OAuthCallbackResult } from '../types';

export type ConnectorOAuthCallbackPageLabelsKey =
  | 'loginFailed'
  | 'loadingText'
  | 'externalUserPrefix'
  | 'alreadyBoundLocalUserPrefix'
  | 'needBindingHint';

export type ConnectorOAuthCallbackPageLabels = Record<ConnectorOAuthCallbackPageLabelsKey, string>;

export const CONNECTOR_OAUTH_CALLBACK_PAGE_LABELS_KEYS = [
  'loginFailed',
  'loadingText',
  'externalUserPrefix',
  'alreadyBoundLocalUserPrefix',
  'needBindingHint',
] as const satisfies readonly ConnectorOAuthCallbackPageLabelsKey[];

export const defaultConnectorOAuthCallbackPageLabels: ConnectorOAuthCallbackPageLabels = {
  loginFailed: 'Login failed',
  loadingText: 'Completing external login...',
  externalUserPrefix: 'External user:',
  alreadyBoundLocalUserPrefix: 'Already bound to local user',
  needBindingHint: 'Manual binding by an admin or yourself is required before sign-in.',
};

export interface ConnectorOAuthCallbackPageProps {
  api: ConnectorApi;
  /** state / code parsed from the browser URL by the host router. */
  state: string;
  code: string;
  /** Persist token when the user is already bound. */
  onAuthenticated?: (token: { accessToken: string; refreshToken?: string; expiresAt?: string; localUserId?: number }) => void;
  /** Redirect to PendingBinding page on conflict / not-yet-bound. */
  onPending?: (info: { kind: 'manual' | 'conflict'; ticket?: string; redirectTo?: string }) => void;
  labels: ConnectorOAuthCallbackPageLabels;
}

export function ConnectorOAuthCallbackPage({
  api,
  state,
  code,
  onAuthenticated,
  onPending,
  labels,
}: ConnectorOAuthCallbackPageProps) {
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
        <Typography.Title heading={5}>{labels.loginFailed}</Typography.Title>
        <Banner type="danger" fullMode={false} description={error} closeIcon={null} style={{ marginTop: 12 }} />
      </div>
    );
  }

  if (!result) {
    return (
      <div data-testid="connector-oauth-callback-loading" style={{ padding: 24 }}>
        <Spin tip={labels.loadingText} />
      </div>
    );
  }

  return (
    <div data-testid="connector-oauth-callback-result" style={{ padding: 24 }}>
      <Typography.Paragraph>
        {labels.externalUserPrefix} {result.displayName ?? result.externalUserId} ({result.externalUserId})
      </Typography.Paragraph>
      {result.localUserId && (
        <Typography.Paragraph>
          {labels.alreadyBoundLocalUserPrefix} #{result.localUserId}
        </Typography.Paragraph>
      )}
      {!result.accessToken && <Typography.Paragraph>{labels.needBindingHint}</Typography.Paragraph>}
    </div>
  );
}
