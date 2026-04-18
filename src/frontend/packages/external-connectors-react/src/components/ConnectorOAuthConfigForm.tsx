import type { ChangeEvent } from 'react';

export interface ConnectorOAuthConfigValue {
  callbackBaseUrl: string;
  trustedDomains: string;
  visibilityScope?: string;
  syncCron?: string;
  agentId?: string;
}

export interface ConnectorOAuthConfigFormProps {
  value: ConnectorOAuthConfigValue;
  onChange: (next: ConnectorOAuthConfigValue) => void;
  /** 是否需要 AgentId（仅 WeCom/DingTalk 需要）。 */
  showAgentId?: boolean;
  labels?: Partial<Record<
    | 'callbackBaseUrl'
    | 'trustedDomains'
    | 'visibilityScope'
    | 'syncCron'
    | 'agentId'
    | 'callbackHelp'
    | 'trustedDomainsHelp'
    | 'syncCronHelp',
    string
  >>;
}

const defaultLabels = {
  callbackBaseUrl: 'Callback Base URL',
  trustedDomains: '可信域名（逗号分隔）',
  visibilityScope: '可见范围（可选 JSON）',
  syncCron: '通讯录同步 Cron 表达式',
  agentId: 'AgentId',
  callbackHelp: '示例：https://platform.example.com/api/v1/connectors/providers/{id}/callbacks',
  trustedDomainsHelp: '与 OAuth 重定向 URL 的 host 必须匹配，至少配置 1 个',
  syncCronHelp: '6 段式（秒 分 时 日 月 周）；示例 0 0 */1 * * ? 每小时同步一次',
};

/**
 * Connector OAuth + 可见范围 + 通讯录同步统一配置子表单。
 * 由 ConnectorProviderEditDrawer 嵌入；也可独立用于诊断面板。
 */
export function ConnectorOAuthConfigForm(props: ConnectorOAuthConfigFormProps) {
  const text = { ...defaultLabels, ...props.labels };
  const { value, onChange, showAgentId = false } = props;

  const update = (patch: Partial<ConnectorOAuthConfigValue>) => onChange({ ...value, ...patch });

  return (
    <div data-testid="connector-oauth-config-form" style={{ display: 'grid', rowGap: 12 }}>
      <label style={{ display: 'block' }}>
        <span style={{ display: 'block', marginBottom: 4 }}>{text.callbackBaseUrl} *</span>
        <input
          type="url"
          required
          style={{ width: '100%', padding: 6 }}
          value={value.callbackBaseUrl}
          placeholder={text.callbackHelp}
          onChange={(e: ChangeEvent<HTMLInputElement>) => update({ callbackBaseUrl: e.target.value })}
        />
      </label>

      <label style={{ display: 'block' }}>
        <span style={{ display: 'block', marginBottom: 4 }}>{text.trustedDomains} *</span>
        <input
          type="text"
          required
          style={{ width: '100%', padding: 6 }}
          value={value.trustedDomains}
          placeholder="login.example.com,platform.example.com"
          onChange={(e: ChangeEvent<HTMLInputElement>) => update({ trustedDomains: e.target.value })}
        />
        <small style={{ color: '#888' }}>{text.trustedDomainsHelp}</small>
      </label>

      {showAgentId && (
        <label style={{ display: 'block' }}>
          <span style={{ display: 'block', marginBottom: 4 }}>{text.agentId}</span>
          <input
            type="text"
            style={{ width: '100%', padding: 6 }}
            value={value.agentId ?? ''}
            placeholder="例如 1000003"
            onChange={(e: ChangeEvent<HTMLInputElement>) => update({ agentId: e.target.value })}
          />
        </label>
      )}

      <label style={{ display: 'block' }}>
        <span style={{ display: 'block', marginBottom: 4 }}>{text.visibilityScope}</span>
        <input
          type="text"
          style={{ width: '100%', padding: 6 }}
          value={value.visibilityScope ?? ''}
          placeholder='示例：{"departmentIds":[1,2,3],"includeInherit":true}'
          onChange={(e: ChangeEvent<HTMLInputElement>) => update({ visibilityScope: e.target.value })}
        />
      </label>

      <label style={{ display: 'block' }}>
        <span style={{ display: 'block', marginBottom: 4 }}>{text.syncCron}</span>
        <input
          type="text"
          style={{ width: '100%', padding: 6 }}
          value={value.syncCron ?? ''}
          placeholder="0 0 */1 * * ?"
          onChange={(e: ChangeEvent<HTMLInputElement>) => update({ syncCron: e.target.value })}
        />
        <small style={{ color: '#888' }}>{text.syncCronHelp}</small>
      </label>
    </div>
  );
}
