import { clearAuthStorage, getTenantId } from "@atlas/shared-react-core/utils";
import { useState } from "react";
import { Navigate, useNavigate, useParams, useSearchParams } from "react-router-dom";
import { Button, Checkbox, Input, Typography } from "@douyinfe/semi-ui";
import { IconEyeClosedSolid, IconEyeOpened } from "@douyinfe/semi-icons";
import { selectWorkspacePath } from "../app-paths";
import { useAuth } from "../auth-context";
import { useAppI18n } from "../i18n";
import { getLoginCaptcha } from "../../services/api-auth";
import { getWorkspaces } from "../../services/api-org-workspaces";
import { readLastWorkspaceId, rememberLastWorkspaceId } from "../layouts/workspace-shell";
import { InfoBanner, PageShell, PublicRatioFrame, PublicRatioLayout, PublicRatioSplit } from "../_shared";
import { resolveWorkspaceEntryTarget } from "./login-entry-helpers";

const { Text } = Typography;

const FIXED_TENANT_ID = "00000000-0000-0000-0000-000000000001";
const HARDCODED_DEFAULT_USERNAME = "admin";
const HARDCODED_DEFAULT_PASSWORD = "P@ssw0rd!";

const responsiveStyles = `
.app-login-root {
  position: relative;
  min-height: 100vh;
  width: 100%;
  overflow: hidden;
  background-color: #f7f7f9;
}

.app-login-root::before {
  content: "";
  position: absolute;
  inset: 0;
  pointer-events: none;
  background-image: radial-gradient(#e5e6eb 1px, transparent 1px);
  background-size: 20px 20px;
}

.app-login-grid {
  position: relative;
  z-index: 1;
  min-height: 100vh;
  align-items: stretch;
  overflow: hidden;
}

.app-login-frame {
  min-height: 100vh;
}

.app-login-left {
  display: flex;
  flex-direction: column;
  padding: 40px 60px;
  min-width: 0;
  min-height: 0;
}

.app-login-right {
  display: flex;
  align-items: center;
  justify-content: center;
  padding: var(--atlas-layout-pane-padding-y) var(--atlas-layout-pane-padding-x);
  min-width: 0;
  min-height: 0;
}

.app-login-left-panel {
  width: 100%;
  max-width: 600px;
  margin: 0 auto;
  flex: 1;
  display: flex;
  flex-direction: column;
  justify-content: center;
}

.app-login-main-title {
  font-size: clamp(32px, 4vw, 48px);
  line-height: 1.2;
  color: #1d2129;
  font-weight: 500;
  margin-bottom: 24px;
}

.app-login-badge {
  display: inline-flex;
  align-items: center;
  padding: 8px 16px;
  background: #fff;
  border: 1px solid #e5e6eb;
  border-radius: 20px;
  font-size: 14px;
  color: #4e5969;
  box-shadow: 0 4px 10px rgba(0,0,0,0.03);
  margin-bottom: 32px;
}

.app-login-hero-img {
  width: 100%;
  max-width: 520px;
  background: #fff;
  border-radius: 16px;
  border: 1px solid #e5e6eb;
  box-shadow: 0 12px 32px rgba(0,0,0,0.05);
  padding: 24px;
}

.app-login-card {
  width: 100%;
  max-width: min(440px, 100%);
  border-radius: 16px;
  background: #fff;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.08);
  padding: clamp(24px, 4vh, 40px);
}

.app-login-tab-btn {
  font-size: 15px;
  background: transparent;
  border: none;
  border-bottom: 2px solid transparent;
  cursor: pointer;
  padding: 8px 4px;
  color: #4e5969;
  transition: color 0.2s, border-color 0.2s;
}

.app-login-tab-btn.active {
  color: #1664ff;
  border-bottom-color: #1664ff;
  font-weight: 500;
}

.app-login-primary-btn {
  margin-top: 8px;
  height: 44px;
  border-radius: 4px;
  font-weight: 500;
  background: #1664ff;
  font-size: 16px;
}

.app-login-input .semi-input-wrapper {
  background: #fff;
  border: 1px solid #e5e6eb;
  border-radius: 4px;
}

.app-login-input .semi-input-wrapper:hover,
.app-login-input .semi-input-wrapper-focus {
  border-color: #1664ff;
}

.app-login-input .semi-input {
  color: #1d2129;
  font-size: 14px;
}

.app-login-input .semi-input::placeholder {
  color: #86909c;
}

@media (max-width: 1120px) {
  .app-login-left { display: none; }
  .app-login-right { padding: clamp(12px, 3vh, 24px) 16px; }
}
`;

function LocaleSwitchButton() {
  const { locale, setLocale, t } = useAppI18n();

  return (
    <Button
      theme="borderless"
      type="tertiary"
      onClick={() => setLocale(locale === "zh-CN" ? "en-US" : "zh-CN")}
      style={{ color: "#4e5969" }}
    >
      {locale === "zh-CN" ? t("switchToEnglish") : t("switchToChinese")}
    </Button>
  );
}

function LogoMark() {
  return (
    <span
      aria-hidden="true"
      style={{ display: "inline-flex", alignItems: "center" }}
    >
      <svg width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
        <path d="M12 2L21 7V17L12 22L3 17V7L12 2Z" fill="#1664ff"/>
        <path d="M12 6L16.5 8.5V13.5L12 16L7.5 13.5V8.5L12 6Z" fill="#fff"/>
      </svg>
    </span>
  );
}

function normalizeLoginError(
  error: unknown,
  t: (
    key:
      | "appLoginAccountLocked"
      | "appLoginPasswordExpired"
      | "appLoginInvalidTenantId"
      | "appLoginCaptchaNeeded"
      | "loginFailed"
  ) => string
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

  if (
    typedError.code === "LoginFailedTooManyTimes" ||
    message.includes("LoginFailedTooManyTimes") ||
    message.includes("登录失败次数过多")
  ) {
    return t("appLoginCaptchaNeeded");
  }

  return message || t("loginFailed");
}

export function LoginPage() {
  const { appKey = "" } = useParams();
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const auth = useAuth();
  const { t } = useAppI18n();
  const runtimeEnv = import.meta.env;
  const defaultUsername =
    String(runtimeEnv?.VITE_DEFAULT_USERNAME ?? "").trim() || HARDCODED_DEFAULT_USERNAME;
  const defaultPassword = HARDCODED_DEFAULT_PASSWORD;
  const [username, setUsername] = useState(defaultUsername);
  const [password, setPassword] = useState(defaultPassword);
  const [showPassword, setShowPassword] = useState(false);
  const [errorMessage, setErrorMessage] = useState("");
  const [captchaKey, setCaptchaKey] = useState("");
  const [captchaCode, setCaptchaCode] = useState("");
  const [captchaImage, setCaptchaImage] = useState("");
  const [loadingCaptcha, setLoadingCaptcha] = useState(false);
  const [agreed, setAgreed] = useState(false);

  const redirectTarget = searchParams.get("redirect");
  const workspaceTarget = selectWorkspacePath();

  if (auth.isAuthenticated) {
    const nextTarget =
      typeof redirectTarget === "string" && redirectTarget.startsWith("/")
        ? redirectTarget
        : workspaceTarget;
    return <Navigate to={nextTarget} replace />;
  }

  const fetchCaptcha = async () => {
    setLoadingCaptcha(true);
    try {
      const payload = await getLoginCaptcha();
      setCaptchaKey(String(payload.captchaKey ?? "").trim());
      setCaptchaImage(String(payload.captchaImage ?? "").trim());
    } catch {
      setCaptchaKey("");
      setCaptchaImage("");
      setErrorMessage(t("appLoginCaptchaLoadFailed"));
    } finally {
      setLoadingCaptcha(false);
    }
  };

  const shouldRequireCaptcha = captchaKey.length > 0;

  const handleSubmit = async () => {
    try {
      setErrorMessage("");
      if (!agreed) {
        setErrorMessage(t("appLoginAgreementRequired"));
        return;
      }
      clearAuthStorage();
      if (shouldRequireCaptcha && !captchaCode.trim()) {
        setErrorMessage(t("appLoginCaptchaRequired"));
        return;
      }

      await auth.login(
        appKey || "",
        FIXED_TENANT_ID,
        username.trim(),
        password,
        undefined,
        captchaKey || undefined,
        captchaCode.trim() || undefined
      );
      const target = typeof redirectTarget === "string" && redirectTarget.startsWith("/")
        ? redirectTarget
        : await (async () => {
            try {
              const tenantId = getTenantId();
              if (!tenantId) {
                return workspaceTarget;
              }

              const workspaces = await getWorkspaces(tenantId);
              const workspaceIds = workspaces.map(item => item.id).filter(Boolean);
              const nextTarget = resolveWorkspaceEntryTarget(workspaceIds, readLastWorkspaceId());
              if (workspaceIds.length === 1) {
                rememberLastWorkspaceId(workspaceIds[0]);
              }
              return nextTarget;
            } catch {
              return workspaceTarget;
            }
          })();
      navigate(target, { replace: true });
    } catch (error) {
      setErrorMessage(normalizeLoginError(error, t));
      const typedError = error as Error & { code?: string };
      const message = typedError.message || "";
      if (
        typedError.code === "LoginFailedTooManyTimes" ||
        typedError.code === "CaptchaExpired" ||
        message.includes("LoginFailedTooManyTimes") ||
        message.includes("CaptchaExpired")
      ) {
        setCaptchaCode("");
        await fetchCaptcha();
      }
    }
  };

  return (
    <PageShell testId="app-login-page">
      <style>{responsiveStyles}</style>
      <PublicRatioLayout mode="full" className="app-login-root">
        {/* Top bar */}
        <div style={{ position: "absolute", top: 24, left: 32, zIndex: 2, display: "flex", alignItems: "center", gap: 10 }}>
          <LogoMark />
          <Text style={{ fontSize: 18, fontWeight: 600, color: "#1d2129" }}>{t("appLoginBrandName")}</Text>
        </div>
        <div style={{ position: "absolute", top: 22, right: 24, zIndex: 2 }}>
          <LocaleSwitchButton />
        </div>

        <PublicRatioFrame className="app-login-frame">
          <PublicRatioSplit className="app-login-grid">
            {/* Left panel */}
            <aside className="app-login-left">
              <div className="app-login-left-panel">
                <h1 className="app-login-main-title">{t("appLoginMainTitle")}</h1>
                <div style={{ display: "flex", justifyContent: "center" }}>
                  <div className="app-login-badge">
                    <span style={{ marginRight: 6 }}>✨</span> {t("appLoginBadge")}
                  </div>
                </div>
                <div style={{ display: "flex", justifyContent: "center" }}>
                  <div className="app-login-hero-img">
                    <div style={{ background: "#f7f8fa", borderRadius: 8, padding: 24, textAlign: "left", minHeight: 280 }}>
                      <div style={{ fontSize: 20, fontWeight: 500, color: "#1d2129", marginBottom: 16 }}>
                        {t("appLoginHeroEmptyTitle")}
                      </div>
                      <div style={{ height: 48, background: "#fff", borderRadius: 8, border: "1px solid #e5e6eb", marginBottom: 24 }} />
                      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr 1fr", gap: 12 }}>
                        <div style={{ height: 80, background: "#e8f3ff", borderRadius: 8 }} />
                        <div style={{ height: 80, background: "#f2f3f5", borderRadius: 8 }} />
                        <div style={{ height: 80, background: "#f2f3f5", borderRadius: 8 }} />
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </aside>

            {/* Right panel - login card */}
            <main className="app-login-right">
              <div className="app-login-card">
                <h2 style={{ fontSize: 22, fontWeight: 500, color: "#1d2129", marginBottom: 24, marginTop: 0 }}>
                  {t("appLoginWelcome")}
                </h2>

                {/* Tabs */}
                <div
                  style={{
                    display: "flex",
                    gap: 16,
                    borderBottom: "1px solid #e5e6eb",
                    marginBottom: 24
                  }}
                >
                  <button type="button" className="app-login-tab-btn">{t("appLoginTabPhone")}</button>
                  <button type="button" className="app-login-tab-btn active">{t("appLoginTabAccount")}</button>
                </div>

                {/* Error banner */}
                {errorMessage ? (
                  <div style={{ marginBottom: 14 }}>
                    <InfoBanner variant="danger" description={errorMessage} testId="app-login-error" />
                  </div>
                ) : null}

                <form
                  style={{ display: "flex", flexDirection: "column", gap: 20 }}
                  onSubmit={(event) => {
                    event.preventDefault();
                    void handleSubmit();
                  }}
                >
                  <Input
                    data-testid="app-login-username"
                    className="app-login-input"
                    placeholder={t("appLoginUsernamePlaceholderNew")}
                    value={username}
                    onChange={(value) => setUsername(value)}
                    size="large"
                  />

                  <Input
                    data-testid="app-login-password"
                    className="app-login-input"
                    placeholder={t("appLoginPasswordPlaceholderNew")}
                    type={showPassword ? "text" : "password"}
                    value={password}
                    onChange={(value) => setPassword(value)}
                    size="large"
                    suffix={(
                      <button
                        type="button"
                        onClick={() => setShowPassword((prev) => !prev)}
                        style={{
                          border: "none",
                          background: "transparent",
                          color: "#86909c",
                          display: "flex",
                          alignItems: "center",
                          justifyContent: "center",
                          padding: "0 8px",
                          cursor: "pointer"
                        }}
                      >
                        {showPassword ? <IconEyeOpened /> : <IconEyeClosedSolid />}
                      </button>
                    )}
                  />

                  {shouldRequireCaptcha ? (
                    <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                      <Input
                        data-testid="app-login-captcha"
                        className="app-login-input"
                        placeholder={t("appLoginCaptchaPlaceholder")}
                        value={captchaCode}
                        onChange={(value) => setCaptchaCode(value)}
                        size="large"
                      />
                      <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
                        {captchaImage ? (
                          <img
                            src={captchaImage}
                            alt={t("appLoginCaptchaPlaceholder")}
                            style={{ height: 38, borderRadius: 4, border: "1px solid #e5e6eb" }}
                          />
                        ) : null}
                        <Button
                          theme="borderless"
                          type="tertiary"
                          onClick={() => { void fetchCaptcha(); }}
                          loading={loadingCaptcha}
                        >
                          {loadingCaptcha ? t("loading") : t("appLoginCaptchaRefresh")}
                        </Button>
                      </div>
                    </div>
                  ) : null}

                  {/* Agreement checkbox */}
                  <div style={{ display: "flex", alignItems: "flex-start", gap: 8 }}>
                    <Checkbox checked={agreed} onChange={(e) => setAgreed(e.target.checked)} />
                    <Text style={{ fontSize: 12, color: "#4e5969", lineHeight: 1.6 }}>
                      {t("appLoginAgreementText")}{" "}
                      <a href="#" style={{ color: "#1664ff", textDecoration: "none" }}>{t("appLoginAgreementTerms")}</a>、
                      <a href="#" style={{ color: "#1664ff", textDecoration: "none" }}>{t("appLoginAgreementPrivacy")}</a>、
                      <a href="#" style={{ color: "#1664ff", textDecoration: "none" }}>{t("appLoginAgreementUser")}</a>{" "}
                      和{" "}
                      <a href="#" style={{ color: "#1664ff", textDecoration: "none" }}>{t("appLoginAgreementPolicy")}</a>
                    </Text>
                  </div>

                  <Button
                    data-testid="app-login-submit"
                    type="primary"
                    theme="solid"
                    htmlType="submit"
                    block
                    size="large"
                    loading={auth.loading}
                    className="app-login-primary-btn"
                  >
                    {auth.loading ? t("appLoginSubmitting") : t("appLoginSubmit")}
                  </Button>

                  {/* Bottom links */}
                  <div style={{ display: "flex", justifyContent: "space-between", fontSize: 14, color: "#4e5969" }}>
                    <div style={{ display: "flex", gap: 8 }}>
                      <button
                        type="button"
                        style={{ border: "none", background: "transparent", color: "#4e5969", cursor: "pointer", padding: 0, fontSize: 14 }}
                      >
                        {t("appLoginForgotAccount")}
                      </button>
                      <span style={{ color: "#e5e6eb" }}>|</span>
                      <button
                        type="button"
                        style={{ border: "none", background: "transparent", color: "#4e5969", cursor: "pointer", padding: 0, fontSize: 14 }}
                      >
                        {t("appLoginForgotPassword")}
                      </button>
                    </div>
                    <button
                      type="button"
                      style={{ border: "none", background: "transparent", color: "#4e5969", cursor: "pointer", padding: 0, fontSize: 14 }}
                    >
                      {t("appLoginSwitchEnterprise")}
                    </button>
                  </div>

                  {/* Other login methods divider */}
                  <div style={{ display: "flex", alignItems: "center", gap: 16, margin: "8px 0" }}>
                    <div style={{ flex: 1, height: 1, background: "#f2f3f5" }} />
                    <Text style={{ color: "#86909c", fontSize: 12 }}>{t("appLoginOtherMethods")}</Text>
                    <div style={{ flex: 1, height: 1, background: "#f2f3f5" }} />
                  </div>

                  {/* Social icons */}
                  <div style={{ display: "flex", justifyContent: "center", gap: 16 }}>
                    <div style={{ width: 36, height: 36, borderRadius: "50%", background: "#f2f3f5", display: "flex", alignItems: "center", justifyContent: "center", color: "#1d2129", fontWeight: "bold", cursor: "pointer" }}>
                      d
                    </div>
                    <div style={{ width: 36, height: 36, borderRadius: "50%", background: "#f2f3f5", display: "flex", alignItems: "center", justifyContent: "center", color: "#1664ff", fontWeight: "bold", cursor: "pointer" }}>
                      F
                    </div>
                  </div>

                  {/* Register link */}
                  <div style={{ textAlign: "center" }}>
                    <Text style={{ color: "#4e5969", fontSize: 14 }}>{t("appLoginNoAccount")}</Text>
                    <button
                      type="button"
                      style={{
                        border: "none",
                        background: "transparent",
                        color: "#1664ff",
                        fontSize: 14,
                        cursor: "pointer",
                        marginLeft: 4
                      }}
                    >
                      {t("appLoginRegisterNow")}
                    </button>
                  </div>
                </form>
              </div>

              {/* Encryption hint */}
              <div style={{ marginTop: 16, textAlign: "center", color: "#86909c", fontSize: 12 }}>
                {t("appLoginEncryptionHint")}
              </div>
            </main>
          </PublicRatioSplit>
        </PublicRatioFrame>
      </PublicRatioLayout>
    </PageShell>
  );
}
