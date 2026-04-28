import { useEffect, useMemo, useState } from 'react';
import type { FormEvent } from 'react';
import { Banner, Button, Input, Modal, Select, SideSheet, Space, TextArea, Typography } from '@douyinfe/semi-ui';
import type { ConnectorApi } from '../api';
import type {
  ConnectorProviderType,
  ExternalIdentityProviderCreateRequest,
  ExternalIdentityProviderResponse,
  ExternalIdentityProviderUpdateRequest,
} from '../types';
import {
  ConnectorOAuthConfigForm,
  type ConnectorOAuthConfigFormLabels,
  type ConnectorOAuthConfigValue,
} from './ConnectorOAuthConfigForm';

export type ConnectorProviderEditDrawerLabelsKey =
  | 'titleCreate'
  | 'titleEdit'
  | 'providerType'
  | 'code'
  | 'displayName'
  | 'providerTenantId'
  | 'appId'
  | 'secretJson'
  | 'secretJsonHelp'
  | 'rotateSecret'
  | 'rotateConfirm'
  | 'rotateConfirmTitle'
  | 'rotateBeforeFillError'
  | 'submitCreate'
  | 'submitUpdate'
  | 'cancel'
  | 'closeDrawer'
  | 'codePlaceholder'
  | 'displayNamePlaceholder'
  | 'providerTenantIdPlaceholder'
  | 'appIdPlaceholder'
  | 'providerTypeWeCom'
  | 'providerTypeFeishu'
  | 'providerTypeDingTalk'
  | 'providerTypeCustomOidc';

export type ConnectorProviderEditDrawerLabels = Record<ConnectorProviderEditDrawerLabelsKey, string>;

export const CONNECTOR_PROVIDER_EDIT_DRAWER_LABELS_KEYS = [
  'titleCreate',
  'titleEdit',
  'providerType',
  'code',
  'displayName',
  'providerTenantId',
  'appId',
  'secretJson',
  'secretJsonHelp',
  'rotateSecret',
  'rotateConfirm',
  'rotateConfirmTitle',
  'rotateBeforeFillError',
  'submitCreate',
  'submitUpdate',
  'cancel',
  'closeDrawer',
  'codePlaceholder',
  'displayNamePlaceholder',
  'providerTenantIdPlaceholder',
  'appIdPlaceholder',
  'providerTypeWeCom',
  'providerTypeFeishu',
  'providerTypeDingTalk',
  'providerTypeCustomOidc',
] as const satisfies readonly ConnectorProviderEditDrawerLabelsKey[];

export const defaultConnectorProviderEditDrawerLabels: ConnectorProviderEditDrawerLabels = {
  titleCreate: 'Create external connector',
  titleEdit: 'Edit external connector',
  providerType: 'Provider type',
  code: 'Unique code',
  displayName: 'Display name',
  providerTenantId: 'External tenant id (CorpId / TenantKey)',
  appId: 'App AppId',
  secretJson: 'Secret JSON (encrypted at rest)',
  secretJsonHelp:
    'Examples — WeCom: {"corpSecret":"xxx","callbackToken":"xxx","callbackEncodingAesKey":"xxx"} | Feishu: {"corpSecret":"appSecret","eventVerificationToken":"xxx","eventEncryptKey":"xxx"} | DingTalk: {"appSecret":"xxx","callbackToken":"xxx","callbackAesKey":"xxx"}',
  rotateSecret: 'Rotate secret',
  rotateConfirm: 'Rotate the secret? Old secret will be invalidated.',
  rotateConfirmTitle: 'Rotate connector secret',
  rotateBeforeFillError: 'Please fill in the new secretJson before rotating.',
  submitCreate: 'Create',
  submitUpdate: 'Save',
  cancel: 'Cancel',
  closeDrawer: 'Close',
  codePlaceholder: 'wecom-default',
  displayNamePlaceholder: 'Group WeCom',
  providerTenantIdPlaceholder: 'wxCorpId123 / fsTenantKey / dingCorpId',
  appIdPlaceholder: 'wxAppId / cli_xxx / dingxxxAppKey',
  providerTypeWeCom: 'WeCom',
  providerTypeFeishu: 'Feishu',
  providerTypeDingTalk: 'DingTalk',
  providerTypeCustomOidc: 'Custom OIDC',
};

export interface ConnectorProviderEditDrawerProps {
  api: ConnectorApi;
  /** Edit mode when not null; create mode when null. */
  editProviderId?: number | null;
  open: boolean;
  onClose: () => void;
  onSaved: (saved: ExternalIdentityProviderResponse) => void;
  labels: ConnectorProviderEditDrawerLabels;
  oauthFormLabels: ConnectorOAuthConfigFormLabels;
}

/**
 * Connector create / edit / secret-rotate side-sheet, built with Semi SideSheet so it
 * matches the rest of the app's drawer affordance.
 */
export function ConnectorProviderEditDrawer(props: ConnectorProviderEditDrawerProps) {
  const { labels, oauthFormLabels } = props;
  const isEdit = props.editProviderId != null;

  const [providerType, setProviderType] = useState<ConnectorProviderType>('WeCom');
  const [code, setCode] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [providerTenantId, setProviderTenantId] = useState('');
  const [appId, setAppId] = useState('');
  const [secretJson, setSecretJson] = useState('');
  const [oauthConfig, setOAuthConfig] = useState<ConnectorOAuthConfigValue>({
    callbackBaseUrl: '',
    trustedDomains: '',
    visibilityScope: '',
    syncCron: '',
    agentId: '',
  });

  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const showAgentId = useMemo(() => providerType === 'WeCom' || providerType === 'DingTalk', [providerType]);

  useEffect(() => {
    if (!props.open) {
      return;
    }
    if (!isEdit) {
      setProviderType('WeCom');
      setCode('');
      setDisplayName('');
      setProviderTenantId('');
      setAppId('');
      setSecretJson('');
      setOAuthConfig({ callbackBaseUrl: '', trustedDomains: '', visibilityScope: '', syncCron: '', agentId: '' });
      setError(null);
      return;
    }
    void (async () => {
      try {
        const detail = await props.api.getProvider(props.editProviderId!);
        setProviderType(detail.providerType);
        setCode(detail.code);
        setDisplayName(detail.displayName);
        setProviderTenantId(detail.providerTenantId);
        setAppId(detail.appId);
        setSecretJson('');
        setOAuthConfig({
          callbackBaseUrl: detail.callbackBaseUrl,
          trustedDomains: detail.trustedDomains,
          visibilityScope: detail.visibilityScope ?? '',
          syncCron: detail.syncCron ?? '',
          agentId: detail.agentId ?? '',
        });
        setError(null);
      } catch (err) {
        setError(err instanceof Error ? err.message : String(err));
      }
    })();
  }, [props.open, isEdit, props.editProviderId, props.api]);

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setSubmitting(true);
    setError(null);
    try {
      let saved: ExternalIdentityProviderResponse;
      if (isEdit) {
        const payload: ExternalIdentityProviderUpdateRequest = {
          displayName,
          providerTenantId,
          appId,
          trustedDomains: oauthConfig.trustedDomains,
          callbackBaseUrl: oauthConfig.callbackBaseUrl,
          agentId: oauthConfig.agentId || undefined,
          visibilityScope: oauthConfig.visibilityScope || undefined,
          syncCron: oauthConfig.syncCron || undefined,
        };
        saved = await props.api.updateProvider(props.editProviderId!, payload);
      } else {
        const payload: ExternalIdentityProviderCreateRequest = {
          providerType,
          code,
          displayName,
          providerTenantId,
          appId,
          secretJson,
          trustedDomains: oauthConfig.trustedDomains,
          callbackBaseUrl: oauthConfig.callbackBaseUrl,
          agentId: oauthConfig.agentId || undefined,
          visibilityScope: oauthConfig.visibilityScope || undefined,
          syncCron: oauthConfig.syncCron || undefined,
        };
        saved = await props.api.createProvider(payload);
      }
      props.onSaved(saved);
      props.onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setSubmitting(false);
    }
  };

  const rotateSecret = () => {
    if (!isEdit || !secretJson) {
      setError(labels.rotateBeforeFillError);
      return;
    }
    Modal.confirm({
      title: labels.rotateConfirmTitle,
      content: labels.rotateConfirm,
      onOk: async () => {
        setSubmitting(true);
        setError(null);
        try {
          const saved = await props.api.rotateSecret(props.editProviderId!, secretJson);
          props.onSaved(saved);
          setSecretJson('');
        } catch (err) {
          setError(err instanceof Error ? err.message : String(err));
        } finally {
          setSubmitting(false);
        }
      },
    });
  };

  const fieldLabel = (text: string, required = false) => (
    <Typography.Text strong style={{ display: 'block', marginBottom: 4 }}>
      {text}
      {required ? ' *' : ''}
    </Typography.Text>
  );

  return (
    <SideSheet
      data-testid="connector-provider-edit-drawer"
      title={isEdit ? labels.titleEdit : labels.titleCreate}
      visible={props.open}
      onCancel={props.onClose}
      width={520}
      closable
      maskClosable={false}
      bodyStyle={{ padding: 16 }}
    >
      <form onSubmit={submit} style={{ display: 'grid', rowGap: 12 }}>
        <label>
          {fieldLabel(labels.providerType, true)}
          <Select
            value={providerType}
            disabled={isEdit}
            onChange={(v) => setProviderType((v as ConnectorProviderType) ?? 'WeCom')}
            style={{ width: '100%' }}
            optionList={[
              { value: 'WeCom', label: labels.providerTypeWeCom },
              { value: 'Feishu', label: labels.providerTypeFeishu },
              { value: 'DingTalk', label: labels.providerTypeDingTalk },
              { value: 'CustomOidc', label: labels.providerTypeCustomOidc },
            ]}
          />
        </label>

        <label>
          {fieldLabel(labels.code, true)}
          <Input value={code} disabled={isEdit} onChange={setCode} placeholder={labels.codePlaceholder} />
        </label>

        <label>
          {fieldLabel(labels.displayName, true)}
          <Input value={displayName} onChange={setDisplayName} placeholder={labels.displayNamePlaceholder} />
        </label>

        <label>
          {fieldLabel(labels.providerTenantId, true)}
          <Input value={providerTenantId} onChange={setProviderTenantId} placeholder={labels.providerTenantIdPlaceholder} />
        </label>

        <label>
          {fieldLabel(labels.appId, true)}
          <Input value={appId} onChange={setAppId} placeholder={labels.appIdPlaceholder} />
        </label>

        <label>
          {fieldLabel(labels.secretJson, !isEdit)}
          <TextArea
            rows={4}
            value={secretJson}
            onChange={setSecretJson}
            placeholder={labels.secretJsonHelp}
            style={{ fontFamily: 'monospace' }}
          />
          <Typography.Text type="tertiary" size="small" style={{ display: 'block', marginTop: 4, whiteSpace: 'pre-wrap' }}>
            {labels.secretJsonHelp}
          </Typography.Text>
        </label>

        <ConnectorOAuthConfigForm value={oauthConfig} onChange={setOAuthConfig} showAgentId={showAgentId} labels={oauthFormLabels} />

        {error && <Banner type="danger" fullMode={false} description={error} closeIcon={null} />}

        <Space spacing="medium" style={{ marginTop: 8, width: '100%', justifyContent: 'space-between' }}>
          {isEdit ? (
            <Button onClick={rotateSecret} disabled={submitting}>
              {labels.rotateSecret}
            </Button>
          ) : (
            <span />
          )}
          <Space>
            <Button onClick={props.onClose} disabled={submitting}>
              {labels.cancel}
            </Button>
            <Button type="primary" htmlType="submit" loading={submitting}>
              {isEdit ? labels.submitUpdate : labels.submitCreate}
            </Button>
          </Space>
        </Space>
      </form>
    </SideSheet>
  );
}
