import { clearAuthStorage } from "@atlas/shared-react-core/utils";
import { useState } from "react";
import { Navigate, useNavigate, useParams, useSearchParams } from "react-router-dom";
import { Button, Input, Typography } from "@douyinfe/semi-ui";
import { IconLock, IconUser } from "@douyinfe/semi-icons";
import { orgWorkspacesPath } from "../app-paths";
import { useAuth } from "../auth-context";
import { useAppI18n } from "../i18n";
import { getLoginCaptcha } from "../../services/api-auth";
import { InfoBanner, PageShell, PublicRatioFrame, PublicRatioLayout, PublicRatioSplit } from "../_shared";

const { Title, Text } = Typography;

const FIXED_TENANT_ID = "00000000-0000-0000-0000-000000000001";
const HARDCODED_DEFAULT_USERNAME = "admin";
const HARDCODED_DEFAULT_PASSWORD = "P@ssw0rd!";

const responsiveStyles = `
.app-login-root {
  position: relative;
  min-height: 100vh;
  width: 100%;
  overflow: hidden;
  background:
    radial-gradient(58% 48% at 83% 89%, rgba(20, 161, 255, 0.22) 0%, rgba(4, 12, 28, 0) 100%),
    linear-gradient(90deg, #031630 0%, #071f45 38%, #031229 100%);
}

.app-login-root::before {
  content: "";
  position: absolute;
  inset: 0;
  pointer-events: none;
  opacity: 0.2;
  background-image:
    linear-gradient(rgba(52, 112, 188, 0.26) 1px, transparent 1px),
    linear-gradient(90deg, rgba(52, 112, 188, 0.26) 1px, transparent 1px);
  background-size: 40px 40px;
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
  align-items: center;
  justify-content: center;
  padding: var(--atlas-layout-pane-padding-y) var(--atlas-layout-pane-padding-x);
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
  width: min(100%, 560px);
}

.app-login-main-title {
  font-size: clamp(42px, 5vw, 60px);
  line-height: 1.14;
}

.app-login-main-subtitle {
  margin-top: clamp(10px, 2.4vh, 24px);
  font-size: clamp(17px, 2.2vw, 28px);
  line-height: 1.6;
}

.app-login-feature-list {
  margin-top: clamp(16px, 4vh, 54px);
  gap: clamp(10px, 2vh, 22px);
}

.app-login-feature-title {
  font-size: clamp(18px, 2vw, 24px);
}

.app-login-feature-desc {
  font-size: clamp(14px, 1.5vw, 18px);
}

.app-login-stats {
  gap: clamp(16px, 2.8vw, 42px);
  margin-top: clamp(20px, 4vh, 60px);
}

.app-login-stat-value {
  font-size: clamp(24px, 3vw, 42px);
}

.app-login-card {
  width: 100%;
  max-width: min(520px, 100%);
  border-radius: 22px;
  border: 1px solid rgba(75, 136, 212, 0.25);
  background: linear-gradient(180deg, rgba(8, 27, 56, 0.88) 0%, rgba(5, 20, 42, 0.84) 100%);
  box-shadow: 0 24px 58px rgba(4, 18, 42, 0.44);
  padding: clamp(16px, 2.2vh, 30px);
}

.app-login-tab-btn {
  height: clamp(42px, 5vh, 52px);
  font-size: clamp(16px, 1.8vw, 24px);
}

.app-login-primary-btn {
  margin-top: 8px;
  height: clamp(46px, 5.6vh, 54px);
  border-radius: 12px;
  font-weight: 600;
  background: linear-gradient(90deg, #3578e5 0%, #1eb2f5 100%);
}

.app-login-sso-btn {
  height: clamp(42px, 5.2vh, 50px);
  border-radius: 12px;
  background: rgba(15, 40, 74, 0.72);
  border: 1px solid rgba(74, 130, 205, 0.28);
  color: #b8d8ff;
}

@media (max-width: 1120px) {
  .app-login-left { display: none; }
  .app-login-right { padding: clamp(12px, 3vh, 24px) 16px; }
}

@media (max-height: 860px) {
  .app-login-feature-list {
    margin-top: 14px;
    gap: 8px;
  }

  .app-login-stats {
    margin-top: 14px;
    gap: 14px;
  }

  .app-login-card {
    padding: 14px;
  }

  .app-login-main-subtitle {
    line-height: 1.45;
  }
}

.app-login-input .semi-input-wrapper {
  color: #e8f2ff;
}

.app-login-input .semi-input {
  color: #e8f2ff;
  font-size: 16px;
}

.app-login-input .semi-input::placeholder {
  color: rgba(167, 193, 228, 0.78);
}

.app-login-input .semi-input-prefix {
  color: #75a8e2;
}
`;

function LocaleSwitchButton() {
  const { locale, setLocale, t } = useAppI18n();

  return (
    <Button
      theme="borderless"
      type="tertiary"
      onClick={() => setLocale(locale === "zh-CN" ? "en-US" : "zh-CN")}
    >
      {locale === "zh-CN" ? t("switchToEnglish") : t("switchToChinese")}
    </Button>
  );
}

function LogoMark({ tone }: { tone: "light" | "dark" }) {
  const isLight = tone === "light";
  const fillOuter = isLight ? "rgba(255,255,255,0.15)" : "rgba(22,119,255,0.10)";
  const strokeOuter = isLight ? "rgba(255,255,255,0.6)" : "var(--semi-color-primary)";
  const fillInner = isLight ? "rgba(255,255,255,0.25)" : "rgba(22,119,255,0.15)";
  const strokeInner = isLight ? "#fff" : "var(--semi-color-primary)";
  const dotFill = isLight ? "#fff" : "var(--semi-color-primary)";

  return (
    <span
      aria-hidden="true"
      style={{ width: 32, height: 32, display: "inline-flex", alignItems: "center" }}
    >
      <svg viewBox="0 0 32 32" fill="none" xmlns="http://www.w3.org/2000/svg">
        <path
          d="M16 2L28 9v14l-12 7L4 23V9l12-7z"
          fill={fillOuter}
          stroke={strokeOuter}
          strokeWidth="1.5"
        />
        <path
          d="M16 8l7 4v8l-7 4-7-4v-8l7-4z"
          fill={fillInner}
          stroke={strokeInner}
          strokeWidth="1.5"
        />
        <circle cx="16" cy="16" r="3" fill={dotFill} />
      </svg>
    </span>
  );
}

function FeatureGlyph({ kind }: { kind: "shield" | "apps" | "team" }) {
  const baseStyle = { width: 22, height: 22, color: "currentColor" } as const;
  if (kind === "shield") {
    return (
      <svg viewBox="0 0 24 24" style={baseStyle} aria-hidden="true">
        <path d="M12 2l7 3v6c0 5-3.2 8.8-7 11-3.8-2.2-7-6-7-11V5l7-3z" fill="currentColor" opacity="0.92" />
      </svg>
    );
  }

  if (kind === "apps") {
    return (
      <svg viewBox="0 0 24 24" style={baseStyle} aria-hidden="true">
        <rect x="3" y="3" width="7" height="7" rx="1.5" fill="currentColor" />
        <rect x="14" y="3" width="7" height="7" rx="1.5" fill="currentColor" opacity="0.72" />
        <rect x="3" y="14" width="7" height="7" rx="1.5" fill="currentColor" opacity="0.72" />
        <rect x="14" y="14" width="7" height="7" rx="1.5" fill="currentColor" />
      </svg>
    );
  }

  return (
    <svg viewBox="0 0 24 24" style={baseStyle} aria-hidden="true">
      <circle cx="9" cy="8" r="3" fill="currentColor" />
      <circle cx="16.5" cy="9.5" r="2.5" fill="currentColor" opacity="0.72" />
      <path d="M4 19c0-2.8 2.7-5 6-5s6 2.2 6 5H4z" fill="currentColor" />
      <path d="M13.5 19c.2-1.9 2-3.4 4.2-3.4 1.4 0 2.7.6 3.5 1.6V19h-7.7z" fill="currentColor" opacity="0.72" />
    </svg>
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

  const redirectTarget = searchParams.get("redirect");
  const workspaceTarget = orgWorkspacesPath(FIXED_TENANT_ID);

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
      const target =
        typeof redirectTarget === "string" && redirectTarget.startsWith("/")
          ? redirectTarget
          : workspaceTarget;
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
        <div style={{ position: "absolute", top: 22, right: 24, zIndex: 2 }}>
          <LocaleSwitchButton />
        </div>
        <PublicRatioFrame className="app-login-frame">
          <PublicRatioSplit className="app-login-grid">
          <aside className="app-login-left">
            <div className="app-login-left-panel" style={{ color: "#ebf4ff" }}>
              <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: "clamp(24px, 11vh, 128px)" }}>
                <LogoMark tone="light" />
                <Text style={{ color: "#ebf4ff", fontSize: "clamp(24px, 2.4vw, 34px)", fontWeight: 700, letterSpacing: "-0.02em" }}>
                  {t("appLoginBrandTitle")}
                </Text>
              </div>
              <div
                style={{
                  display: "inline-flex",
                  alignItems: "center",
                  border: "1px solid rgba(71, 153, 255, 0.45)",
                  background: "rgba(16, 67, 138, 0.25)",
                  color: "#8fc2ff",
                  padding: "8px 14px",
                  borderRadius: 10,
                  fontSize: 14,
                  marginBottom: 26
                }}
              >
                已通过等保2.0三级认证
              </div>
              <Title heading={1} className="app-login-main-title" style={{ color: "#d6e8ff", margin: 0 }}>
                安全可靠的
                <br />
                应用运行平台
              </Title>
              <Text className="app-login-main-subtitle" style={{ display: "block", color: "rgba(173, 204, 244, 0.78)" }}>
                {t("appLoginHeroSubtitle")}
              </Text>
              <div className="app-login-feature-list" style={{ display: "flex", flexDirection: "column" }}>
                <div style={{ display: "flex", alignItems: "flex-start", gap: 16, color: "#cfe4ff" }}>
                  <FeatureGlyph kind="shield" />
                  <div>
                    <div className="app-login-feature-title" style={{ fontWeight: 600 }}>{t("appLoginHeroPoint1")}</div>
                    <Text className="app-login-feature-desc" style={{ color: "rgba(165, 191, 228, 0.72)" }}>覆盖五大安全层，开箱即用</Text>
                  </div>
                </div>
                <div style={{ display: "flex", alignItems: "flex-start", gap: 16, color: "#cfe4ff" }}>
                  <FeatureGlyph kind="apps" />
                  <div>
                    <div className="app-login-feature-title" style={{ fontWeight: 600 }}>{t("appLoginHeroPoint2")}</div>
                    <Text className="app-login-feature-desc" style={{ color: "rgba(165, 191, 228, 0.72)" }}>容器级强隔离，故障域最小化</Text>
                  </div>
                </div>
                <div style={{ display: "flex", alignItems: "flex-start", gap: 16, color: "#cfe4ff" }}>
                  <FeatureGlyph kind="team" />
                  <div>
                    <div className="app-login-feature-title" style={{ fontWeight: 600 }}>{t("appLoginHeroPoint3")}</div>
                    <Text className="app-login-feature-desc" style={{ color: "rgba(165, 191, 228, 0.72)" }}>全量操作日志，事后可溯源</Text>
                  </div>
                </div>
              </div>
              <div className="app-login-stats" style={{ display: "flex", alignItems: "center" }}>
                <div>
                  <div className="app-login-stat-value" style={{ fontWeight: 700, color: "#f5f8ff" }}>2,400+</div>
                  <Text style={{ color: "rgba(165, 191, 228, 0.72)", fontSize: 16 }}>企业客户</Text>
                </div>
                <div>
                  <div className="app-login-stat-value" style={{ fontWeight: 700, color: "#f5f8ff" }}>99.99%</div>
                  <Text style={{ color: "rgba(165, 191, 228, 0.72)", fontSize: 16 }}>服务可用性</Text>
                </div>
                <div>
                  <div className="app-login-stat-value" style={{ fontWeight: 700, color: "#f5f8ff" }}>0次</div>
                  <Text style={{ color: "rgba(165, 191, 228, 0.72)", fontSize: 16 }}>安全违规</Text>
                </div>
              </div>
            </div>
          </aside>

          <main className="app-login-right">
            <div className="app-login-card">
              <div
                style={{
                  display: "grid",
                  gridTemplateColumns: "1fr 1fr",
                  borderRadius: 12,
                  border: "1px solid rgba(74, 130, 205, 0.24)",
                  background: "rgba(3, 21, 45, 0.8)",
                  marginBottom: 28,
                  padding: 4
                }}
              >
                <button
                  type="button"
                  className="app-login-tab-btn"
                  style={{
                    border: "none",
                    borderRadius: 9,
                    background: "rgba(53, 119, 212, 0.4)",
                    color: "#8bc3ff",
                    fontWeight: 600,
                    cursor: "default"
                  }}
                >
                  账号登录
                </button>
                <button
                  type="button"
                  className="app-login-tab-btn"
                  style={{
                    border: "none",
                    borderRadius: 9,
                    background: "transparent",
                    color: "rgba(111, 143, 184, 0.82)",
                    fontWeight: 500
                  }}
                >
                  注册账号
                </button>
              </div>

              {errorMessage ? (
                <div style={{ marginBottom: 14 }}>
                  <InfoBanner variant="danger" description={errorMessage} testId="app-login-error" />
                </div>
              ) : null}

              <form
                style={{ display: "flex", flexDirection: "column", gap: 18 }}
                onSubmit={(event) => {
                  event.preventDefault();
                  void handleSubmit();
                }}
              >
                <label style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                  <Text style={{ color: "rgba(143, 173, 214, 0.85)", fontSize: 16 }}>账号 / 手机号 / 邮箱</Text>
                  <Input
                    data-testid="app-login-username"
                    className="app-login-input"
                    prefix={<IconUser />}
                    placeholder={t("appLoginUsernamePlaceholder")}
                    value={username}
                    onChange={(value) => setUsername(value)}
                    size="large"
                    style={{
                      borderRadius: 12,
                      background: "rgba(6, 24, 50, 0.92)",
                      border: "1px solid rgba(63, 124, 204, 0.26)"
                    }}
                    inputStyle={{ color: "#e8f2ff", fontWeight: 500 }}
                  />
                </label>

                <label style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                  <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
                    <Text style={{ color: "rgba(143, 173, 214, 0.85)", fontSize: 16 }}>{t("password")}</Text>
                    <button
                      type="button"
                      style={{
                        border: "none",
                        background: "transparent",
                        color: "#4fa7ff",
                        fontSize: 14,
                        cursor: "pointer"
                      }}
                    >
                      忘记密码？
                    </button>
                  </div>
                  <Input
                    data-testid="app-login-password"
                    className="app-login-input"
                    prefix={<IconLock />}
                    placeholder={t("appLoginPasswordPlaceholder")}
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
                          color: "#7dc0ff",
                          fontSize: 13,
                          cursor: "pointer"
                        }}
                      >
                        {showPassword ? "隐藏" : "显示"}
                      </button>
                    )}
                    style={{
                      borderRadius: 12,
                      background: "rgba(6, 24, 50, 0.92)",
                      border: "1px solid rgba(63, 124, 204, 0.26)"
                    }}
                    inputStyle={{ color: "#e8f2ff", fontWeight: 500 }}
                  />
                </label>

                {shouldRequireCaptcha ? (
                  <label style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                    <Text style={{ color: "rgba(143, 173, 214, 0.85)", fontSize: 16 }}>
                      {t("appLoginCaptchaPlaceholder")}
                    </Text>
                    <Input
                      data-testid="app-login-captcha"
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
                          style={{ height: 38, borderRadius: 8, border: "1px solid rgba(63, 124, 204, 0.26)" }}
                        />
                      ) : null}
                      <Button
                        theme="borderless"
                        type="tertiary"
                        onClick={() => {
                          void fetchCaptcha();
                        }}
                        loading={loadingCaptcha}
                        style={{ color: "#67b4ff" }}
                      >
                        {loadingCaptcha ? t("loading") : t("appLoginCaptchaRefresh")}
                      </Button>
                    </div>
                  </label>
                ) : null}

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
                  {auth.loading ? t("loading") : "安全登录"}
                </Button>

                <div style={{ display: "flex", alignItems: "center", gap: 16, margin: "2px 0" }}>
                  <div style={{ flex: 1, height: 1, background: "rgba(74, 112, 157, 0.25)" }} />
                  <Text style={{ color: "rgba(121, 149, 190, 0.8)", fontSize: 14 }}>或通过以下方式登录</Text>
                  <div style={{ flex: 1, height: 1, background: "rgba(74, 112, 157, 0.25)" }} />
                </div>

                <Button
                  theme="light"
                  block
                  size="large"
                  className="app-login-sso-btn"
                >
                  企业 SSO 单点登录
                </Button>
              </form>

              <div style={{ marginTop: 24, textAlign: "center" }}>
                <Text style={{ color: "rgba(121, 149, 190, 0.8)" }}>还没有账号？</Text>
                <button
                  type="button"
                  style={{
                    border: "none",
                    background: "transparent",
                    color: "#49adff",
                    fontWeight: 600,
                    fontSize: 16,
                    cursor: "pointer",
                    marginLeft: 8
                  }}
                >
                  立即注册
                </button>
              </div>
            </div>
            <div style={{ marginTop: 20, display: "flex", alignItems: "center", gap: 8, color: "rgba(121, 149, 190, 0.7)", fontSize: 12 }}>
              <span>全程 TLS 1.3 加密传输 · 等保2.0 三级合规</span>
            </div>
          </main>
          </PublicRatioSplit>
        </PublicRatioFrame>
      </PublicRatioLayout>
    </PageShell>
  );
}
