import type {
  BindingConflictResolutionRequest,
  ExternalApprovalTemplateMappingRequest,
  ExternalApprovalTemplateMappingResponse,
  ExternalApprovalTemplateResponse,
  ExternalDirectorySyncJobResponse,
  ExternalIdentityBindingListItem,
  ExternalIdentityProviderCreateRequest,
  ExternalIdentityProviderListItem,
  ExternalIdentityProviderResponse,
  ExternalIdentityProviderUpdateRequest,
  IdentityBindingStatus,
  ManualBindingRequest,
  OAuthCallbackResult,
  OAuthInitiationRequest,
  OAuthInitiationResponse,
} from '../types';

/**
 * Connector REST 客户端的最小抽象：宿主（app-web）注入实际的 fetch wrapper（带 token / X-Tenant-Id），
 * 这样 @atlas/external-connectors-react 不依赖具体 host 的 api-core。
 */
export interface ConnectorHttpClient {
  get<T>(url: string, query?: Record<string, unknown>): Promise<T>;
  post<T>(url: string, body?: unknown): Promise<T>;
  put<T>(url: string, body?: unknown): Promise<T>;
  patch<T>(url: string, body?: unknown): Promise<T>;
  delete<T>(url: string): Promise<T>;
}

const PROVIDERS = '/api/v1/connectors/providers';
const OAUTH = '/api/v1/connectors/oauth';
const BINDINGS = '/api/v1/connectors/identity-bindings';

export interface PagedResult<T> {
  items: T[];
  total: number;
  pageIndex: number;
  pageSize: number;
}

export function createConnectorApi(http: ConnectorHttpClient) {
  return {
    listProviders(includeDisabled = false) {
      return http.get<ExternalIdentityProviderListItem[]>(PROVIDERS, { includeDisabled });
    },
    getProvider(id: number) {
      return http.get<ExternalIdentityProviderResponse>(`${PROVIDERS}/${id}`);
    },
    createProvider(payload: ExternalIdentityProviderCreateRequest) {
      return http.post<ExternalIdentityProviderResponse>(PROVIDERS, payload);
    },
    updateProvider(id: number, payload: ExternalIdentityProviderUpdateRequest) {
      return http.put<ExternalIdentityProviderResponse>(`${PROVIDERS}/${id}`, payload);
    },
    enableProvider(id: number) {
      return http.post<ExternalIdentityProviderResponse>(`${PROVIDERS}/${id}:enable`);
    },
    disableProvider(id: number) {
      return http.post<ExternalIdentityProviderResponse>(`${PROVIDERS}/${id}:disable`);
    },
    deleteProvider(id: number) {
      return http.delete<void>(`${PROVIDERS}/${id}`);
    },
    rotateSecret(id: number, secretJson: string) {
      return http.post<ExternalIdentityProviderResponse>(`${PROVIDERS}/${id}/secret:rotate`, { secretJson });
    },
    startOAuth(payload: OAuthInitiationRequest) {
      return http.post<OAuthInitiationResponse>(`${OAUTH}/start`, payload);
    },
    completeOAuth(state: string, code: string) {
      return http.post<OAuthCallbackResult>(`${OAUTH}/callback`, { state, code });
    },
    listBindings(providerId: number, status?: IdentityBindingStatus, pageIndex = 1, pageSize = 20) {
      return http.get<PagedResult<ExternalIdentityBindingListItem>>(BINDINGS, {
        providerId,
        status,
        pageIndex,
        pageSize,
      });
    },
    createManualBinding(payload: ManualBindingRequest) {
      return http.post<ExternalIdentityBindingListItem>(`${BINDINGS}/manual`, payload);
    },
    resolveConflict(payload: BindingConflictResolutionRequest) {
      return http.post<ExternalIdentityBindingListItem>(`${BINDINGS}/conflicts:resolve`, payload);
    },
    deleteBinding(bindingId: number) {
      return http.delete<void>(`${BINDINGS}/${bindingId}`);
    },
    runFullSync(providerId: number) {
      return http.post<ExternalDirectorySyncJobResponse>(`${PROVIDERS}/${providerId}/directory/sync/full`);
    },
    listSyncJobs(providerId: number, take = 20) {
      return http.get<ExternalDirectorySyncJobResponse[]>(`${PROVIDERS}/${providerId}/directory/sync/jobs`, { take });
    },
    listSyncDiffs(providerId: number, jobId: number, pageIndex = 1, pageSize = 50) {
      return http.get<{ items: unknown[]; total: number }>(`${PROVIDERS}/${providerId}/directory/sync/jobs/${jobId}/diffs`, { pageIndex, pageSize });
    },
    listApprovalTemplates(providerId: number) {
      return http.get<ExternalApprovalTemplateResponse[]>(`${PROVIDERS}/${providerId}/approvals/templates`);
    },
    refreshApprovalTemplate(providerId: number, externalTemplateId: string) {
      return http.post<ExternalApprovalTemplateResponse>(`${PROVIDERS}/${providerId}/approvals/templates/${encodeURIComponent(externalTemplateId)}:refresh`);
    },
    listApprovalTemplateMappings(providerId: number) {
      return http.get<ExternalApprovalTemplateMappingResponse[]>(`${PROVIDERS}/${providerId}/approvals/template-mappings`);
    },
    upsertApprovalTemplateMapping(providerId: number, flowDefinitionId: number, payload: ExternalApprovalTemplateMappingRequest) {
      return http.put<ExternalApprovalTemplateMappingResponse>(`${PROVIDERS}/${providerId}/approvals/template-mappings/${flowDefinitionId}`, payload);
    },
  };
}

export type ConnectorApi = ReturnType<typeof createConnectorApi>;
