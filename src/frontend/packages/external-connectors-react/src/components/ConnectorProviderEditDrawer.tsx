import { useEffect, useMemo, useState } from 'react';
import type { ChangeEvent, FormEvent } from 'react';
import type { ConnectorApi } from '../api';
import type {
  ConnectorProviderType,
  ExternalIdentityProviderCreateRequest,
  ExternalIdentityProviderResponse,
  ExternalIdentityProviderUpdateRequest,
} from '../types';
import { ConnectorOAuthConfigForm, type ConnectorOAuthConfigValue } from './ConnectorOAuthConfigForm';

export interface ConnectorProviderEditDrawerProps {
  api: ConnectorApi;
  /** 当存在时表示编辑模式（按 id 拉详情），缺省即为新建。 */
  editProviderId?: number | null;
  open: boolean;
  onClose: () => void;
  onSaved: (saved: ExternalIdentityProviderResponse) => void;
  labels?: Partial<Record<
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
    | 'submitCreate'
    | 'submitUpdate'
    | 'cancel'
    | 'closeDrawer',
    string
  >>;
}

const defaultLabels = {
  titleCreate: '新建外部连接器',
  titleEdit: '编辑外部连接器',
  providerType: 'Provider 类型',
  code: '唯一编码',
  displayName: '显示名称',
  providerTenantId: '外部租户标识（CorpId / TenantKey）',
  appId: '应用 AppId',
  secretJson: '密钥 JSON（加密入库）',
  secretJsonHelp:
    '示例（WeCom）：{"corpSecret":"xxx","callbackToken":"xxx","callbackEncodingAesKey":"xxx"}\n（Feishu）：{"corpSecret":"appSecret","eventVerificationToken":"xxx","eventEncryptKey":"xxx"}\n（DingTalk）：{"appSecret":"xxx","callbackToken":"xxx","callbackAesKey":"xxx"}',
  rotateSecret: '轮换密钥',
  rotateConfirm: '确定要轮换密钥？老密钥将失效。',
  submitCreate: '创建',
  submitUpdate: '保存',
  cancel: '取消',
  closeDrawer: '关闭',
};

/**
 * 外部连接器新建 / 编辑 / 密钥轮换抽屉。
 * 注意：本包不依赖 UI 库，使用原生 HTML + 内联样式实现 drawer 视觉，由宿主可换皮。
 */
export function ConnectorProviderEditDrawer(props: ConnectorProviderEditDrawerProps) {
  const text = { ...defaultLabels, ...props.labels };
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
      // 新建模式：重置默认。
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
    // 编辑模式：拉详情回填（secretMasked 不回填到 secretJson 输入，需走轮换）。
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

  const rotateSecret = async () => {
    if (!isEdit || !secretJson) {
      setError('请先填写新的 secretJson 再点击轮换');
      return;
    }
    if (!confirm(text.rotateConfirm)) {
      return;
    }
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
  };

  if (!props.open) {
    return null;
  }

  return (
    <aside
      data-testid="connector-provider-edit-drawer"
      role="dialog"
      aria-modal="true"
      style={{
        position: 'fixed',
        right: 0,
        top: 0,
        bottom: 0,
        width: 480,
        maxWidth: '90vw',
        background: '#fff',
        boxShadow: '-2px 0 12px rgba(0,0,0,0.12)',
        padding: 16,
        zIndex: 1000,
        overflowY: 'auto',
      }}
    >
      <header style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
        <h3 style={{ margin: 0 }}>{isEdit ? text.titleEdit : text.titleCreate}</h3>
        <button type="button" onClick={props.onClose} aria-label={text.closeDrawer}>×</button>
      </header>

      <form onSubmit={submit} style={{ display: 'grid', rowGap: 12 }}>
        <label>
          <span style={{ display: 'block', marginBottom: 4 }}>{text.providerType} *</span>
          <select
            value={providerType}
            disabled={isEdit}
            onChange={(e: ChangeEvent<HTMLSelectElement>) => setProviderType(e.target.value as ConnectorProviderType)}
            style={{ width: '100%', padding: 6 }}
          >
            <option value="WeCom">WeCom 企业微信</option>
            <option value="Feishu">Feishu 飞书</option>
            <option value="DingTalk">DingTalk 钉钉</option>
            <option value="CustomOidc">CustomOidc 通用 OIDC</option>
          </select>
        </label>

        <label>
          <span style={{ display: 'block', marginBottom: 4 }}>{text.code} *</span>
          <input
            required
            disabled={isEdit}
            value={code}
            onChange={(e) => setCode(e.target.value)}
            style={{ width: '100%', padding: 6 }}
            placeholder="wecom-default"
          />
        </label>

        <label>
          <span style={{ display: 'block', marginBottom: 4 }}>{text.displayName} *</span>
          <input
            required
            value={displayName}
            onChange={(e) => setDisplayName(e.target.value)}
            style={{ width: '100%', padding: 6 }}
            placeholder="集团企业微信"
          />
        </label>

        <label>
          <span style={{ display: 'block', marginBottom: 4 }}>{text.providerTenantId} *</span>
          <input
            required
            value={providerTenantId}
            onChange={(e) => setProviderTenantId(e.target.value)}
            style={{ width: '100%', padding: 6 }}
            placeholder="wxCorpId123 / fsTenantKey / dingCorpId"
          />
        </label>

        <label>
          <span style={{ display: 'block', marginBottom: 4 }}>{text.appId} *</span>
          <input
            required
            value={appId}
            onChange={(e) => setAppId(e.target.value)}
            style={{ width: '100%', padding: 6 }}
            placeholder="wxAppId / cli_xxx / dingxxxAppKey"
          />
        </label>

        <label>
          <span style={{ display: 'block', marginBottom: 4 }}>
            {text.secretJson} {!isEdit && '*'}
          </span>
          <textarea
            required={!isEdit}
            rows={4}
            value={secretJson}
            onChange={(e) => setSecretJson(e.target.value)}
            style={{ width: '100%', padding: 6, fontFamily: 'monospace' }}
            placeholder={text.secretJsonHelp}
          />
          <small style={{ color: '#888', whiteSpace: 'pre-wrap' }}>{text.secretJsonHelp}</small>
        </label>

        <ConnectorOAuthConfigForm value={oauthConfig} onChange={setOAuthConfig} showAgentId={showAgentId} />

        {error && <p style={{ color: 'red' }}>{error}</p>}

        <footer style={{ display: 'flex', justifyContent: 'flex-end', gap: 8, marginTop: 8 }}>
          {isEdit && (
            <button type="button" onClick={rotateSecret} disabled={submitting} style={{ marginRight: 'auto' }}>
              {text.rotateSecret}
            </button>
          )}
          <button type="button" onClick={props.onClose} disabled={submitting}>
            {text.cancel}
          </button>
          <button type="submit" disabled={submitting}>
            {isEdit ? text.submitUpdate : text.submitCreate}
          </button>
        </footer>
      </form>
    </aside>
  );
}
