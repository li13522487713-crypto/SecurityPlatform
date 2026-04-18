import { Input, Typography } from '@douyinfe/semi-ui';

export interface ConnectorOAuthConfigValue {
  callbackBaseUrl: string;
  trustedDomains: string;
  visibilityScope?: string;
  syncCron?: string;
  agentId?: string;
}

export type ConnectorOAuthConfigFormLabelsKey =
  | 'callbackBaseUrl'
  | 'trustedDomains'
  | 'visibilityScope'
  | 'syncCron'
  | 'agentId'
  | 'callbackHelp'
  | 'trustedDomainsHelp'
  | 'syncCronHelp'
  | 'trustedDomainsPlaceholder'
  | 'visibilityScopePlaceholder'
  | 'syncCronPlaceholder'
  | 'agentIdPlaceholder';

export type ConnectorOAuthConfigFormLabels = Record<ConnectorOAuthConfigFormLabelsKey, string>;

export const CONNECTOR_OAUTH_CONFIG_FORM_LABELS_KEYS = [
  'callbackBaseUrl',
  'trustedDomains',
  'visibilityScope',
  'syncCron',
  'agentId',
  'callbackHelp',
  'trustedDomainsHelp',
  'syncCronHelp',
  'trustedDomainsPlaceholder',
  'visibilityScopePlaceholder',
  'syncCronPlaceholder',
  'agentIdPlaceholder',
] as const satisfies readonly ConnectorOAuthConfigFormLabelsKey[];

export const defaultConnectorOAuthConfigFormLabels: ConnectorOAuthConfigFormLabels = {
  callbackBaseUrl: 'Callback Base URL',
  trustedDomains: 'Trusted domains (comma-separated)',
  visibilityScope: 'Visibility scope (optional JSON)',
  syncCron: 'Directory sync cron expression',
  agentId: 'AgentId',
  callbackHelp: 'Example: https://platform.example.com/api/v1/connectors/providers/{id}/callbacks',
  trustedDomainsHelp: 'Must match the host of OAuth redirect URL; configure at least 1.',
  syncCronHelp: '6-segment (sec min hour day month week); e.g. 0 0 */1 * * ? hourly',
  trustedDomainsPlaceholder: 'login.example.com,platform.example.com',
  visibilityScopePlaceholder: 'e.g. {"departmentIds":[1,2,3],"includeInherit":true}',
  syncCronPlaceholder: '0 0 */1 * * ?',
  agentIdPlaceholder: 'e.g. 1000003',
};

export interface ConnectorOAuthConfigFormProps {
  value: ConnectorOAuthConfigValue;
  onChange: (next: ConnectorOAuthConfigValue) => void;
  /** Whether AgentId is required (only WeCom / DingTalk). */
  showAgentId?: boolean;
  labels: ConnectorOAuthConfigFormLabels;
}

interface FieldProps {
  label: string;
  required?: boolean;
  helper?: string;
  children: React.ReactNode;
}

function Field({ label, required, helper, children }: FieldProps) {
  return (
    <label style={{ display: 'block' }}>
      <Typography.Text strong style={{ display: 'block', marginBottom: 4 }}>
        {label}
        {required ? ' *' : ''}
      </Typography.Text>
      {children}
      {helper && (
        <Typography.Text type="tertiary" size="small" style={{ display: 'block', marginTop: 4 }}>
          {helper}
        </Typography.Text>
      )}
    </label>
  );
}

/**
 * Connector OAuth + visibility-scope + directory-sync sub-form, embedded inside
 * ConnectorProviderEditDrawer; can also be reused in diagnostic panels.
 */
export function ConnectorOAuthConfigForm(props: ConnectorOAuthConfigFormProps) {
  const { value, onChange, showAgentId = false, labels } = props;
  const update = (patch: Partial<ConnectorOAuthConfigValue>) => onChange({ ...value, ...patch });

  return (
    <div data-testid="connector-oauth-config-form" style={{ display: 'grid', rowGap: 12 }}>
      <Field label={labels.callbackBaseUrl} required>
        <Input
          value={value.callbackBaseUrl}
          placeholder={labels.callbackHelp}
          onChange={(v) => update({ callbackBaseUrl: v })}
        />
      </Field>

      <Field label={labels.trustedDomains} required helper={labels.trustedDomainsHelp}>
        <Input
          value={value.trustedDomains}
          placeholder={labels.trustedDomainsPlaceholder}
          onChange={(v) => update({ trustedDomains: v })}
        />
      </Field>

      {showAgentId && (
        <Field label={labels.agentId}>
          <Input
            value={value.agentId ?? ''}
            placeholder={labels.agentIdPlaceholder}
            onChange={(v) => update({ agentId: v })}
          />
        </Field>
      )}

      <Field label={labels.visibilityScope}>
        <Input
          value={value.visibilityScope ?? ''}
          placeholder={labels.visibilityScopePlaceholder}
          onChange={(v) => update({ visibilityScope: v })}
        />
      </Field>

      <Field label={labels.syncCron} helper={labels.syncCronHelp}>
        <Input
          value={value.syncCron ?? ''}
          placeholder={labels.syncCronPlaceholder}
          onChange={(v) => update({ syncCron: v })}
        />
      </Field>
    </div>
  );
}
