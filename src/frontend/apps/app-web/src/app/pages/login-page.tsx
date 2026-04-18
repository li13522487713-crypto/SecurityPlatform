import { clearAuthStorage, getTenantId } from "@atlas/shared-react-core/utils";
import { useState } from "react";
import { Navigate, useNavigate, useParams, useSearchParams } from "react-router-dom";
import { Button, Input, Typography } from "@douyinfe/semi-ui";
import { IconBriefStroked, IconLock, IconUser } from "@douyinfe/semi-icons";
import { orgWorkspacesPath } from "../app-paths";
import { useAuth } from "../auth-context";
import { useAppI18n } from "../i18n";
import { getLoginCaptcha } from "../../services/api-auth";
import { FormCard, InfoBanner, PageShell } from "../_shared";

const { Title, Text } = Typography;

const HARDCODED_DEFAULT_TENANT_ID = "00000000-0000-0000-0000-000000000001";
const HARDCODED_DEFAULT_USERNAME = "admin";
const HARDCODED_DEFAULT_PASSWORD = "P@ssw0rd!";

const HERO_BACKGROUND =
  "linear-gradient(135deg, #1554ff 0%, #2d7bff 50%, #38bdf8 100%)";

/**
 * 登录页响应式样式：宽屏渲染左侧 hero + 右侧表单的双栏布局；
 * 窄屏（<= 960px）只保留右侧表单，hero 整体隐藏。
 * 用 inline `<style>` 注入避免向 app.css 新增遗留类，便于 M7 一次性清理 atlas-*。
 */
const responsiveStyles = `
.app-login-grid { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); min-height: 100vh; }
.app-login-aside { display: none; }
@media (min-width: 961px) {
  .app-login-aside { display: flex; }
}
@media (max-width: 960px) {
  .app-login-grid { grid-template-columns: minmax(0, 1fr); }
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
  const defaultTenantId =
    String(runtimeEnv?.VITE_DEFAULT_TENANT_ID ?? "").trim() || HARDCODED_DEFAULT_TENANT_ID;
  const defaultUsername =
    String(runtimeEnv?.VITE_DEFAULT_USERNAME ?? "").trim() || HARDCODED_DEFAULT_USERNAME;
  const defaultPassword = HARDCODED_DEFAULT_PASSWORD;
  const [tenantId, setTenantId] = useState(getTenantId() || defaultTenantId);
  const [username, setUsername] = useState(defaultUsername);
  const [password, setPassword] = useState(defaultPassword);
  const [errorMessage, setErrorMessage] = useState("");
  const [captchaKey, setCaptchaKey] = useState("");
  const [captchaCode, setCaptchaCode] = useState("");
  const [captchaImage, setCaptchaImage] = useState("");
  const [loadingCaptcha, setLoadingCaptcha] = useState(false);

  const redirectTarget = searchParams.get("redirect");
  const workspaceTarget = orgWorkspacesPath(tenantId.trim() || defaultTenantId);

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
        tenantId.trim(),
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
      <div className="app-login-grid">
        <aside
          className="app-login-aside"
          style={{
            position: "relative",
            padding: 48,
            color: "#fff",
            background: HERO_BACKGROUND,
            overflow: "hidden",
            flexDirection: "column"
          }}
        >
          <span
            style={{
              position: "absolute",
              top: -120,
              left: -80,
              width: 320,
              height: 320,
              borderRadius: "50%",
              background: "rgba(255,255,255,0.12)"
            }}
          />
          <span
            style={{
              position: "absolute",
              bottom: -120,
              right: -100,
              width: 360,
              height: 360,
              borderRadius: "50%",
              background: "rgba(255,255,255,0.08)"
            }}
          />
          <div style={{ position: "relative", display: "flex", flexDirection: "column", flex: 1, gap: 32 }}>
            <div style={{ display: "flex", alignItems: "center", gap: 12, fontWeight: 700, fontSize: 18 }}>
              <LogoMark tone="light" />
              <span>{t("appLoginBrandTitle")}</span>
            </div>
            <div style={{ flex: 1, display: "flex", flexDirection: "column", justifyContent: "center", gap: 16 }}>
              <Title heading={2} style={{ color: "#fff", margin: 0 }}>
                {t("appLoginHeroTitle")}
              </Title>
              <Text style={{ color: "rgba(255,255,255,0.85)", fontSize: 16 }}>
                {t("appLoginHeroSubtitle")}
              </Text>
            </div>
            <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
              <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
                <FeatureGlyph kind="shield" />
                <Text style={{ color: "#fff" }}>{t("appLoginHeroPoint1")}</Text>
              </div>
              <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
                <FeatureGlyph kind="apps" />
                <Text style={{ color: "#fff" }}>{t("appLoginHeroPoint2")}</Text>
              </div>
              <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
                <FeatureGlyph kind="team" />
                <Text style={{ color: "#fff" }}>{t("appLoginHeroPoint3")}</Text>
              </div>
            </div>
          </div>
        </aside>

        <main
          style={{
            display: "flex",
            flexDirection: "column",
            justifyContent: "center",
            alignItems: "center",
            padding: "48px 24px",
            position: "relative"
          }}
        >
          <div style={{ position: "absolute", top: 24, right: 24 }}>
            <LocaleSwitchButton />
          </div>

          <div style={{ width: "100%", maxWidth: 420 }}>
            <div
              style={{
                display: "flex",
                alignItems: "center",
                gap: 8,
                marginBottom: 24,
                fontWeight: 700,
                color: "var(--semi-color-text-0)"
              }}
            >
              <LogoMark tone="dark" />
              <span>{t("appLoginBrandTitle")}</span>
            </div>

            <FormCard
              title={t("appLoginTitle")}
              subtitle={
                <span style={{ display: "inline-flex", alignItems: "center", gap: 6 }}>
                  <FeatureGlyph kind="apps" />
                  <span>{appKey || t("workspaceListWorkspaceTag")}</span>
                </span>
              }
            >
              {defaultTenantId ? (
                <div style={{ marginBottom: 12 }}>
                  <InfoBanner
                    variant="info"
                    compact
                    description={
                      <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
                        <strong>{t("appLoginDefaultCredentialHint")}</strong>
                        <div style={{ display: "flex", flexWrap: "wrap", gap: 12, fontSize: 12 }}>
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
                    }
                  />
                </div>
              ) : null}

              {errorMessage ? (
                <div style={{ marginBottom: 12 }}>
                  <InfoBanner variant="danger" description={errorMessage} testId="app-login-error" />
                </div>
              ) : null}

              <form
                style={{ display: "flex", flexDirection: "column", gap: 16 }}
                onSubmit={(event) => {
                  event.preventDefault();
                  void handleSubmit();
                }}
              >
                <label style={{ display: "flex", flexDirection: "column", gap: 6 }}>
                  <Text strong>{t("tenantId")}</Text>
                  <Input
                    data-testid="app-login-tenant"
                    prefix={<IconBriefStroked />}
                    placeholder={t("appLoginTenantIdPlaceholder")}
                    value={tenantId}
                    onChange={(value) => setTenantId(value)}
                    size="large"
                  />
                </label>

                <label style={{ display: "flex", flexDirection: "column", gap: 6 }}>
                  <Text strong>{t("username")}</Text>
                  <Input
                    data-testid="app-login-username"
                    prefix={<IconUser />}
                    placeholder={t("appLoginUsernamePlaceholder")}
                    value={username}
                    onChange={(value) => setUsername(value)}
                    size="large"
                  />
                </label>

                <label style={{ display: "flex", flexDirection: "column", gap: 6 }}>
                  <Text strong>{t("password")}</Text>
                  <Input
                    data-testid="app-login-password"
                    prefix={<IconLock />}
                    placeholder={t("appLoginPasswordPlaceholder")}
                    type="password"
                    value={password}
                    onChange={(value) => setPassword(value)}
                    size="large"
                  />
                </label>

                {shouldRequireCaptcha ? (
                  <label style={{ display: "flex", flexDirection: "column", gap: 6 }}>
                    <Text strong>{t("appLoginCaptchaPlaceholder")}</Text>
                    <Input
                      data-testid="app-login-captcha"
                      placeholder={t("appLoginCaptchaPlaceholder")}
                      value={captchaCode}
                      onChange={(value) => setCaptchaCode(value)}
                      size="large"
                    />
                    <div style={{ display: "flex", gap: 8, alignItems: "center", marginTop: 4 }}>
                      {captchaImage ? (
                        <img
                          src={captchaImage}
                          alt={t("appLoginCaptchaPlaceholder")}
                          style={{
                            height: 40,
                            borderRadius: 6,
                            border: "1px solid var(--semi-color-border)"
                          }}
                        />
                      ) : null}
                      <Button
                        theme="borderless"
                        type="tertiary"
                        onClick={() => {
                          void fetchCaptcha();
                        }}
                        loading={loadingCaptcha}
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
                  style={{ marginTop: 8 }}
                >
                  {auth.loading ? t("loading") : t("login")}
                </Button>
              </form>
            </FormCard>

            <div
              style={{
                marginTop: 16,
                textAlign: "center",
                color: "var(--semi-color-text-2)",
                fontSize: 12
              }}
            >
              {t("appLoginFooter")}
            </div>
          </div>
        </main>
      </div>
    </PageShell>
  );
}
