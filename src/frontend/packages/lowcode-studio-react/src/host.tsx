import { createContext, useContext, useMemo, type ReactNode } from 'react';
import { lowcodeApi, type LowcodeApi } from './services/api-core';

export interface LowcodeStudioAuth {
  accessTokenFactory: () => string;
  tenantIdFactory: () => string;
  userIdFactory: () => string;
}

export interface LowcodeStudioHostConfig {
  api: LowcodeApi;
  auth: LowcodeStudioAuth;
}

function readStorageValue(key: string): string {
  if (typeof localStorage === 'undefined') {
    return '';
  }
  return localStorage.getItem(key) ?? '';
}

const defaultHostConfig: LowcodeStudioHostConfig = {
  api: lowcodeApi,
  auth: {
    accessTokenFactory: () => readStorageValue('atlas_access_token'),
    tenantIdFactory: () => readStorageValue('atlas_tenant_id') || '00000000-0000-0000-0000-000000000001',
    userIdFactory: () => readStorageValue('atlas_user_id') || 'me'
  }
};

const LowcodeStudioHostContext = createContext<LowcodeStudioHostConfig>(defaultHostConfig);

export function LowcodeStudioHostProvider({
  host,
  children
}: {
  host?: LowcodeStudioHostConfig;
  children: ReactNode;
}) {
  const value = useMemo(() => host ?? defaultHostConfig, [host]);
  return (
    <LowcodeStudioHostContext.Provider value={value}>
      {children}
    </LowcodeStudioHostContext.Provider>
  );
}

export function useLowcodeStudioHost(): LowcodeStudioHostConfig {
  return useContext(LowcodeStudioHostContext);
}
