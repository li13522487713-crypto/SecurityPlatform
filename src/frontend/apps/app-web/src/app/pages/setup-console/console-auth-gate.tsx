import { useState, type ChangeEvent, type FormEvent, type ReactNode } from "react";
import { useAppI18n } from "../../i18n";
import { authenticateSetupConsole } from "../../../services/mock";
import { writeConsoleToken } from "./console-token-storage";

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

  const updateField = (field: keyof AuthFormState) => (event: ChangeEvent<HTMLInputElement>) => {
    setForm((previous) => ({ ...previous, [field]: event.target.value }));
  };

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setSubmitting(true);
    setErrorMessage(null);
    try {
      const response = await authenticateSetupConsole({
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
    <div className="atlas-setup-page" data-testid="setup-console-auth-gate">
      <div className="atlas-setup-card">
        <h1 className="atlas-setup-card__title">{t("setupConsoleAuthTitle")}</h1>
        <p className="atlas-setup-card__subtitle">{t("setupConsoleAuthSubtitle")}</p>

        <form className="atlas-setup-panel" onSubmit={(event) => void handleSubmit(event)}>
          <label className="atlas-form-field atlas-form-field--full">
            <span className="atlas-form-field__label">{t("setupConsoleAuthRecoveryKeyLabel")}</span>
            <input
              className="atlas-input"
              data-testid="setup-console-auth-recovery-key"
              placeholder={t("setupConsoleAuthRecoveryKeyPlaceholder")}
              value={form.recoveryKey}
              onChange={updateField("recoveryKey")}
            />
          </label>

          <div className="atlas-info-banner atlas-info-banner--compact" aria-hidden="true">
            {t("setupConsoleAuthOrSeparator")}
          </div>

          <label className="atlas-form-field atlas-form-field--full">
            <span className="atlas-form-field__label">{t("setupConsoleAuthBootstrapUsernameLabel")}</span>
            <input
              className="atlas-input"
              data-testid="setup-console-auth-bootstrap-username"
              autoComplete="username"
              value={form.bootstrapAdminUsername}
              onChange={updateField("bootstrapAdminUsername")}
            />
          </label>

          <label className="atlas-form-field atlas-form-field--full">
            <span className="atlas-form-field__label">{t("setupConsoleAuthBootstrapPasswordLabel")}</span>
            <input
              className="atlas-input"
              data-testid="setup-console-auth-bootstrap-password"
              type="password"
              autoComplete="current-password"
              value={form.bootstrapAdminPassword}
              onChange={updateField("bootstrapAdminPassword")}
            />
          </label>

          {errorMessage ? (
            <div className="atlas-pill is-error" data-testid="setup-console-auth-error">
              {errorMessage}
            </div>
          ) : null}

          <div className="atlas-setup-actions">
            <span />
            <button
              type="submit"
              className="atlas-button atlas-button--primary"
              data-testid="setup-console-auth-submit"
              disabled={submitting}
            >
              {submitting ? t("setupConsoleAuthSubmitting") : t("setupConsoleAuthSubmit")}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
