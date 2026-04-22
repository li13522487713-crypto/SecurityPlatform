import { createContext, useContext, useMemo, type ReactNode } from 'react';
import {
  lowcodeApi,
  type LowcodeApi,
  type LowCodeAssetDescriptor,
  type ProjectIdeBootstrap,
  type ProjectIdeGraph,
  type ProjectIdePublishPreview,
  type ProjectIdePublishRequest,
  type ProjectIdePublishResult,
  type ProjectIdeValidationResult,
  type RuntimeDispatchResponse,
  type RuntimeTrace
} from './services/api-core';

export interface LowcodeStudioAuth {
  accessTokenFactory: () => string;
  tenantIdFactory: () => string;
  userIdFactory: () => string;
}

export interface ProjectIdeBootstrapApi {
  getBootstrap: (appId: string) => Promise<ProjectIdeBootstrap>;
  getGraph: (appId: string) => Promise<ProjectIdeGraph>;
}

export interface LowcodeValidationApi {
  validate: (appId: string, schemaJson?: string) => Promise<ProjectIdeValidationResult>;
}

export interface LowcodePublishApi {
  listArtifacts: (appId: string) => Promise<import('./services/api-core').PublishArtifact[]>;
  getPreview: (appId: string) => Promise<ProjectIdePublishPreview>;
  publish: (appId: string, request: ProjectIdePublishRequest) => Promise<ProjectIdePublishResult>;
}

export interface LowcodeAssetApi {
  prepareUpload: (request: { fileName: string; contentType: string; size: number; sha256?: string }) => Promise<{
    token: string;
    uploadUrl: string;
    instantHit?: boolean | null;
    fileHandle?: string | null;
  }>;
  getAsset: (id: string) => Promise<LowCodeAssetDescriptor>;
  deleteAsset: (id: string) => Promise<void>;
}

export interface LowcodeDispatchApi {
  dispatch: (request: Record<string, unknown>) => Promise<RuntimeDispatchResponse>;
  getTrace: (traceId: string) => Promise<RuntimeTrace>;
  queryTraces: (query?: Record<string, string | number | undefined>) => Promise<RuntimeTrace[]>;
}

export interface LowcodeCollabConfig {
  hubUrl?: string;
  reconnectDelaysMs?: number[];
}

export interface LowcodeStudioHostConfig {
  api: LowcodeApi;
  auth: LowcodeStudioAuth;
  bootstrapApi?: ProjectIdeBootstrapApi;
  validationApi?: LowcodeValidationApi;
  publishApi?: LowcodePublishApi;
  assetApi?: LowcodeAssetApi;
  dispatchApi?: LowcodeDispatchApi;
  collabConfig?: LowcodeCollabConfig;
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
