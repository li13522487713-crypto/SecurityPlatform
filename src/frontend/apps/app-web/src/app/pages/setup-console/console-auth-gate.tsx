import { useState, type ChangeEvent, type FormEvent, type ReactNode } from "react";
import { Button, Divider, Input, Typography } from "@douyinfe/semi-ui";
import { useAppI18n } from "../../i18n";
import { setupConsoleApi } from "../../../services/api-setup-console";
import { FormCard, InfoBanner, PageShell } from "../../_shared";
import { writeConsoleToken } from "./console-token-storage";

const { Text } = Typography;

interface ConsoleAuthGateProps {
  children: ReactNode;
  authenticated: boolean;
  onAuthenticated: () => void;
}

interface AuthFormState {
  recoveryKey: string;
  bootstrapAdminUsername: string;
  bootstrapAdminPassword: string;
}

const INITIAL_FORM: AuthFormState = {
  recoveryKey: "",
  bootstrapAdminUsername: "",
  bootstrapAdminPassword: ""
};

/**
 * 控制台二次认证门：恢复密钥 + BootstrapAdmin 凭证（双因子任一可通过）。
 *
 * 已通过认证（authenticated=true）时直接渲染 children；
 * 否则展示二次认证表单。
 */
export function ConsoleAuthGate({ children, authenticated, onAuthenticated }: ConsoleAuthGateProps) {
  const { t } = useAppI18n();
  const [form, setForm] = useState<AuthFormState>(INITIAL_FORM);
  const [submitting, setSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  if (authenticated) {
    return <>{children}</>;
  }

  const updateField =
    (field: keyof AuthFormState) => (valueOrEvent: string | ChangeEvent<HTMLInputElement>) => {
      const nextValue = typeof valueOrEvent === "string" ? valueOrEvent : valueOrEvent.target.value;
      setForm((previous) => ({ ...previous, [field]: nextValue }));
    };

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setSubmitting(true);
    setErrorMessage(null);
    try {
      const response = await setupConsoleApi.authenticate({
        recoveryKey: form.recoveryKey.trim() || undefined,
        bootstrapAdminUsername: form.bootstrapAdminUsername.trim() || undefined,
        bootstrapAdminPassword: form.bootstrapAdminPassword || undefined
      });
      if (!response.success || !response.data) {
        setErrorMessage(t("setupConsoleAuthFailed"));
        return;
      }
      writeConsoleToken({
        token: response.data.consoleToken,
        expiresAt: response.data.expiresAt
      });
      setForm(INITIAL_FORM);
      onAuthenticated();
    } catch (error) {
      const message = error instanceof Error ? error.message : "";
      setErrorMessage(message ? `${t("setupConsoleAuthFailed")} (${message})` : t("setupConsoleAuthFailed"));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <PageShell centered maxWidth={520} testId="setup-console-auth-gate">
      <FormCard title={t("setupConsoleAuthTitle")} subtitle={t("setupConsoleAuthSubtitle")}>
        <form
          style={{ display: "flex", flexDirection: "column", gap: 16 }}
          onSubmit={(event) => void handleSubmit(event)}
        >
          <label style={{ display: "flex", flexDirection: "column", gap: 6 }}>
            <Text strong>{t("setupConsoleAuthRecoveryKeyLabel")}</Text>
            <Input
              data-testid="setup-console-auth-recovery-key"
              placeholder={t("setupConsoleAuthRecoveryKeyPlaceholder")}
              value={form.recoveryKey}
              onChange={updateField("recoveryKey")}
            />
          </label>

          <Divider margin="4px">{t("setupConsoleAuthOrSeparator")}</Divider>

          <label style={{ display: "flex", flexDirection: "column", gap: 6 }}>
            <Text strong>{t("setupConsoleAuthBootstrapUsernameLabel")}</Text>
            <Input
              data-testid="setup-console-auth-bootstrap-username"
              autoComplete="username"
              value={form.bootstrapAdminUsername}
              onChange={updateField("bootstrapAdminUsername")}
            />
          </label>

          <label style={{ display: "flex", flexDirection: "column", gap: 6 }}>
            <Text strong>{t("setupConsoleAuthBootstrapPasswordLabel")}</Text>
            <Input
              mode="password"
              data-testid="setup-console-auth-bootstrap-password"
              autoComplete="current-password"
              value={form.bootstrapAdminPassword}
              onChange={updateField("bootstrapAdminPassword")}
            />
          </label>

          {errorMessage ? (
            <InfoBanner
              variant="danger"
              compact
              description={errorMessage}
              testId="setup-console-auth-error"
            />
          ) : null}

          <div style={{ display: "flex", justifyContent: "flex-end" }}>
            <Button
              type="primary"
              theme="solid"
              htmlType="submit"
              data-testid="setup-console-auth-submit"
              loading={submitting}
            >
              {submitting ? t("setupConsoleAuthSubmitting") : t("setupConsoleAuthSubmit")}
            </Button>
          </div>
        </form>
      </FormCard>
    </PageShell>
  );
}
