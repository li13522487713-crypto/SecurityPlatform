import { clearAuthStorage, getTenantId } from "@atlas/shared-react-core/utils";
import { useState } from "react";
import { Navigate, useNavigate, useParams, useSearchParams } from "react-router-dom";
import { orgWorkspacesPath } from "../app-paths";
import { useAuth } from "../auth-context";
import { useAppI18n } from "../i18n";

const HARDCODED_DEFAULT_TENANT_ID = "00000000-0000-0000-0000-000000000001";
const HARDCODED_DEFAULT_USERNAME = "admin";
const HARDCODED_DEFAULT_PASSWORD = "P@ssw0rd!";

function LocaleSwitchButton() {
  const { locale, setLocale, t } = useAppI18n();

  return (
    <button
      type="button"
      className="atlas-locale-switch"
      onClick={() => setLocale(locale === "zh-CN" ? "en-US" : "zh-CN")}
    >
      {locale === "zh-CN" ? t("switchToEnglish") : t("switchToChinese")}
    </button>
  );
}

function LogoMark({ compact = false }: { compact?: boolean }) {
  return (
    <div className={`atlas-login-logo-mark ${compact ? "is-compact" : ""}`.trim()} aria-hidden="true">
      <svg viewBox="0 0 32 32" fill="none" xmlns="http://www.w3.org/2000/svg">
        <path
          d="M16 2L28 9v14l-12 7L4 23V9l12-7z"
          fill={compact ? "rgba(22,119,255,0.1)" : "rgba(255,255,255,0.15)"}
          stroke={compact ? "var(--atlas-login-primary)" : "rgba(255,255,255,0.6)"}
          strokeWidth="1.5"
        />
        <path
          d="M16 8l7 4v8l-7 4-7-4v-8l7-4z"
          fill={compact ? "rgba(22,119,255,0.15)" : "rgba(255,255,255,0.25)"}
          stroke={compact ? "var(--atlas-login-primary)" : "#fff"}
          strokeWidth="1.5"
        />
        <circle cx="16" cy="16" r="3" fill={compact ? "var(--atlas-login-primary)" : "#fff"} />
      </svg>
    </div>
  );
}

function FeatureGlyph({ kind }: { kind: "shield" | "apps" | "team" }) {
  if (kind === "shield") {
    return (
      <svg viewBox="0 0 24 24" className="atlas-login-feature__icon" aria-hidden="true">
        <path d="M12 2l7 3v6c0 5-3.2 8.8-7 11-3.8-2.2-7-6-7-11V5l7-3z" fill="currentColor" opacity="0.92" />
      </svg>
    );
  }

  if (kind === "apps") {
    return (
      <svg viewBox="0 0 24 24" className="atlas-login-feature__icon" aria-hidden="true">
        <rect x="3" y="3" width="7" height="7" rx="1.5" fill="currentColor" />
        <rect x="14" y="3" width="7" height="7" rx="1.5" fill="currentColor" opacity="0.72" />
        <rect x="3" y="14" width="7" height="7" rx="1.5" fill="currentColor" opacity="0.72" />
        <rect x="14" y="14" width="7" height="7" rx="1.5" fill="currentColor" />
      </svg>
    );
  }

  return (
    <svg viewBox="0 0 24 24" className="atlas-login-feature__icon" aria-hidden="true">
      <circle cx="9" cy="8" r="3" fill="currentColor" />
      <circle cx="16.5" cy="9.5" r="2.5" fill="currentColor" opacity="0.72" />
      <path d="M4 19c0-2.8 2.7-5 6-5s6 2.2 6 5H4z" fill="currentColor" />
      <path d="M13.5 19c.2-1.9 2-3.4 4.2-3.4 1.4 0 2.7.6 3.5 1.6V19h-7.7z" fill="currentColor" opacity="0.72" />
    </svg>
  );
}

function PrefixGlyph({ kind }: { kind: "bank" | "user" | "lock" }) {
  if (kind === "bank") {
    return (
      <svg viewBox="0 0 20 20" className="atlas-login-input__icon" aria-hidden="true">
        <path d="M2 7l8-4 8 4v2H2V7zm2 4h2v5H4v-5zm5 0h2v5H9v-5zm5 0h2v5h-2v-5zM2 17h16v1H2v-1z" fill="currentColor" />
      </svg>
    );
  }

  if (kind === "user") {
    return (
      <svg viewBox="0 0 20 20" className="atlas-login-input__icon" aria-hidden="true">
        <circle cx="10" cy="6" r="3.2" fill="currentColor" />
        <path d="M3 17c.8-3.1 3.4-5 7-5s6.2 1.9 7 5H3z" fill="currentColor" />
      </svg>
    );
  }

  return (
    <svg viewBox="0 0 20 20" className="atlas-login-input__icon" aria-hidden="true">
      <rect x="4" y="9" width="12" height="8" rx="2" fill="currentColor" />
      <path d="M6.5 9V7.5a3.5 3.5 0 017 0V9h-1.6V7.7a1.9 1.9 0 10-3.8 0V9H6.5z" fill="currentColor" opacity="0.72" />
    </svg>
  );
}

function normalizeLoginError(
  error: unknown,
  t: (key: "appLoginAccountLocked" | "appLoginPasswordExpired" | "appLoginInvalidTenantId" | "loginFailed") => string
): string {
  if (!(error instanceof Error)) {
    return t("loginFailed");
  }

  const typedError = error as Error & { code?: string };
  const message = typedError.message || "";
  if (typedError.code === "ACCOUNT_LOCKED" || message.includes("ACCOUNT_LOCKED") || message.includes("账户已锁定")) {
    return t("appLoginAccountLocked");
  }

  if (typedError.code === "PASSWORD_EXPIRED" || message.includes("PASSWORD_EXPIRED") || message.includes("密码已过期")) {
    return t("appLoginPasswordExpired");
  }

  if (
    typedError.code === "INVALID_TENANT_ID" ||
    message.includes("Invalid tenant ID format") ||
    message.includes("请输入有效的租户")
  ) {
    return t("appLoginInvalidTenantId");
  }

  return message || t("loginFailed");
}

export function LoginPage() {
  const { appKey = "" } = useParams();
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const auth = useAuth();
  const { t } = useAppI18n();
  const defaultTenantId =
    String(import.meta.env.VITE_DEFAULT_TENANT_ID ?? "").trim() || HARDCODED_DEFAULT_TENANT_ID;
  const defaultUsername =
    String(import.meta.env.VITE_DEFAULT_USERNAME ?? "").trim() || HARDCODED_DEFAULT_USERNAME;
  const defaultPassword = HARDCODED_DEFAULT_PASSWORD;
  const [tenantId, setTenantId] = useState(getTenantId() || defaultTenantId);
  const [username, setUsername] = useState(defaultUsername);
  const [password, setPassword] = useState(defaultPassword);
  const [errorMessage, setErrorMessage] = useState("");

  const redirectTarget = searchParams.get("redirect");
  const workspaceTarget = orgWorkspacesPath(tenantId.trim() || defaultTenantId);

  if (auth.isAuthenticated) {
    const nextTarget =
      typeof redirectTarget === "string" && redirectTarget.startsWith("/")
        ? redirectTarget
        : workspaceTarget;
    return <Navigate to={nextTarget} replace />;
  }

  return (
    <div className="atlas-login-page" data-testid="app-login-page">
      <div className="atlas-login-split">
        <aside className="atlas-login-hero">
          <div className="atlas-login-hero__bg">
            <span className="atlas-login-hero__shape atlas-login-hero__shape--one" />
            <span className="atlas-login-hero__shape atlas-login-hero__shape--two" />
            <span className="atlas-login-hero__shape atlas-login-hero__shape--three" />
          </div>

          <div className="atlas-login-hero__content">
            <div className="atlas-login-brand">
              <LogoMark />
              <span>{t("appLoginBrandTitle")}</span>
            </div>

            <div className="atlas-login-slogan">
              <h2>{t("appLoginHeroTitle")}</h2>
              <p>{t("appLoginHeroSubtitle")}</p>
            </div>

            <div className="atlas-login-features">
              <div className="atlas-login-feature">
                <FeatureGlyph kind="shield" />
                <span>{t("appLoginHeroPoint1")}</span>
              </div>
              <div className="atlas-login-feature">
                <FeatureGlyph kind="apps" />
                <span>{t("appLoginHeroPoint2")}</span>
              </div>
              <div className="atlas-login-feature">
                <FeatureGlyph kind="team" />
                <span>{t("appLoginHeroPoint3")}</span>
              </div>
            </div>
          </div>
        </aside>

        <main className="atlas-login-main">
          <div className="atlas-login-main__locale">
            <LocaleSwitchButton />
          </div>

          <div className="atlas-login-card-shell">
            <div className="atlas-login-mobile-brand">
              <LogoMark compact />
              <span>{t("appLoginBrandTitle")}</span>
            </div>

            <h1 className="atlas-login-card__title">{t("appLoginTitle")}</h1>
            <div className="atlas-login-card__badge">
              <FeatureGlyph kind="apps" />
              <span>{appKey || t("workspaceListWorkspaceTag")}</span>
            </div>

            {defaultTenantId ? (
              <div className="atlas-login-dev-hint">
                <strong>{t("appLoginDefaultCredentialHint")}</strong>
                <div className="atlas-login-dev-hint__items">
                  <span>
                    Tenant: <code>{defaultTenantId}</code>
                  </span>
                  {defaultUsername ? (
                    <span>
                      User: <code>{defaultUsername}</code>
                    </span>
                  ) : null}
                  <span>
                    Password: <code>P@ssw0rd!</code>
                  </span>
                </div>
              </div>
            ) : null}

            {errorMessage ? (
              <div className="atlas-login-error login-error" data-testid="app-login-error">
                {errorMessage}
              </div>
            ) : null}

            <form
              className="atlas-login-form"
              onSubmit={(event) => {
                event.preventDefault();
                void (async () => {
                  try {
                    setErrorMessage("");
                    clearAuthStorage();
                    await auth.login(appKey || "", tenantId.trim(), username.trim(), password);
                    const target =
                      typeof redirectTarget === "string" && redirectTarget.startsWith("/")
                        ? redirectTarget
                        : workspaceTarget;
                    navigate(target, { replace: true });
                  } catch (error) {
                    setErrorMessage(normalizeLoginError(error, t));
                  }
                })();
              }}
            >
              <label className="atlas-login-field">
                <span className="atlas-login-field__label">{t("tenantId")}</span>
                <span className="atlas-login-field__control">
                  <PrefixGlyph kind="bank" />
                  <input
                    className="atlas-input atlas-login-input"
                    data-testid="app-login-tenant"
                    placeholder={t("appLoginTenantIdPlaceholder")}
                    value={tenantId}
                    onChange={(event) => setTenantId(event.target.value)}
                  />
                </span>
              </label>

              <label className="atlas-login-field">
                <span className="atlas-login-field__label">{t("username")}</span>
                <span className="atlas-login-field__control">
                  <PrefixGlyph kind="user" />
                  <input
                    className="atlas-input atlas-login-input"
                    data-testid="app-login-username"
                    placeholder={t("appLoginUsernamePlaceholder")}
                    value={username}
                    onChange={(event) => setUsername(event.target.value)}
                  />
                </span>
              </label>

              <label className="atlas-login-field">
                <span className="atlas-login-field__label">{t("password")}</span>
                <span className="atlas-login-field__control">
                  <PrefixGlyph kind="lock" />
                  <input
                    className="atlas-input atlas-login-input"
                    data-testid="app-login-password"
                    placeholder={t("appLoginPasswordPlaceholder")}
                    type="password"
                    value={password}
                    onChange={(event) => setPassword(event.target.value)}
                  />
                </span>
              </label>

              <button
                data-testid="app-login-submit"
                type="submit"
                className="atlas-button atlas-button--primary atlas-button--block atlas-login-submit"
                disabled={auth.loading}
              >
                {auth.loading ? t("loading") : t("login")}
              </button>
            </form>

            <div className="atlas-login-footer">{t("appLoginFooter")}</div>
          </div>
        </main>
      </div>
    </div>
  );
}
