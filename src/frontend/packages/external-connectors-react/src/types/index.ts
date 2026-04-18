// 与后端 Application.ExternalConnectors 的 DTO 对齐。
// 注意：所有字段使用 PascalCase 与后端 JSON 默认序列化保持一致。
export type ConnectorProviderType =
  | 'Unknown'
  | 'WeCom'
  | 'Feishu'
  | 'DingTalk'
  | 'CustomOidc';

export type IdentityBindingStatus = 'Active' | 'PendingConfirm' | 'Conflict' | 'Revoked';

export type IdentityBindingMatchStrategy = 'Direct' | 'Mobile' | 'Email' | 'NameDept' | 'Manual';

export type IntegrationMode = 'ExternalLed' | 'LocalLed' | 'Hybrid';

export interface ExternalIdentityProviderListItem {
  id: number;
  providerType: ConnectorProviderType;
  code: string;
  displayName: string;
  enabled: boolean;
  updatedAt: string;
}

export interface ExternalIdentityProviderResponse extends ExternalIdentityProviderListItem {
  providerTenantId: string;
  appId: string;
  secretMasked: string;
  trustedDomains: string;
  callbackBaseUrl: string;
  agentId?: string | null;
  visibilityScope?: string | null;
  syncCron?: string | null;
  createdAt: string;
}

export interface ExternalIdentityProviderCreateRequest {
  providerType: ConnectorProviderType;
  code: string;
  displayName: string;
  providerTenantId: string;
  appId: string;
  secretJson: string;
  trustedDomains: string;
  callbackBaseUrl: string;
  agentId?: string;
  visibilityScope?: string;
  syncCron?: string;
}

export interface ExternalIdentityProviderUpdateRequest {
  displayName: string;
  providerTenantId: string;
  appId: string;
  trustedDomains: string;
  callbackBaseUrl: string;
  agentId?: string;
  visibilityScope?: string;
  syncCron?: string;
}

export interface OAuthInitiationRequest {
  providerId: number;
  postLoginRedirect?: string;
}

export interface OAuthInitiationResponse {
  authorizationUrl: string;
  state: string;
  expiresAt: string;
}

export interface OAuthCallbackResult {
  accessToken?: string;
  refreshToken?: string;
  expiresAt?: string;
  localUserId?: number;
  pendingBindingTicket?: string;
  externalUserId: string;
  mobile?: string;
  email?: string;
  displayName?: string;
  redirectTo?: string;
}

export interface ExternalIdentityBindingListItem {
  id: number;
  providerId: number;
  localUserId: number;
  externalUserId: string;
  status: IdentityBindingStatus;
  matchStrategy: IdentityBindingMatchStrategy;
  boundAt: string;
  lastLoginAt?: string | null;
}

export interface ManualBindingRequest {
  providerId: number;
  localUserId: number;
  externalUserId: string;
  openId?: string;
  unionId?: string;
  mobile?: string;
  email?: string;
}

export type BindingConflictResolution = 'KeepCurrent' | 'SwitchToLocalUser' | 'Revoke';

export interface BindingConflictResolutionRequest {
  bindingId: number;
  resolution: BindingConflictResolution;
  newLocalUserId?: number;
}

export interface ExternalDirectorySyncJobResponse {
  id: number;
  providerId: number;
  mode: 'Full' | 'Incremental';
  status: 'Pending' | 'Running' | 'Succeeded' | 'Failed' | 'PartialSucceeded' | 'Canceled';
  triggerSource: string;
  departmentCreated: number;
  departmentUpdated: number;
  departmentDeleted: number;
  userCreated: number;
  userUpdated: number;
  userDeleted: number;
  relationChanged: number;
  failedItems: number;
  errorMessage?: string;
  startedAt: string;
  finishedAt?: string;
}

export interface ExternalApprovalTemplateResponse {
  externalTemplateId: string;
  name: string;
  description?: string;
  controls: Array<{
    controlId: string;
    controlType: string;
    title: string;
    required: boolean;
    options?: Array<{ key: string; text: string }>;
  }>;
  fetchedAt: string;
}

export interface ExternalApprovalTemplateMappingRequest {
  providerId: number;
  flowDefinitionId: number;
  externalTemplateId: string;
  integrationMode: IntegrationMode;
  fieldMappingJson: string;
  enabled: boolean;
}

export interface ExternalApprovalTemplateMappingResponse extends ExternalApprovalTemplateMappingRequest {
  id: number;
  createdAt: string;
  updatedAt: string;
}
