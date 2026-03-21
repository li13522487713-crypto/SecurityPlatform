<template>
  <div class="login-wrapper">
    <div class="login-split">
      <!-- Left Panel (Aliyun style) -->
      <aside class="login-split__left">
        <div class="tech-bg">
          <div class="bg-shape shape-1"></div>
          <div class="bg-shape shape-2"></div>
          <div class="bg-shape shape-3"></div>
        </div>
        <div class="login-left-content">
          <div class="login-logo">
            <div class="logo-icon">
              <!-- Inline brand logo SVG -->
              <svg viewBox="0 0 32 32" fill="none" xmlns="http://www.w3.org/2000/svg">
                <path d="M16 2L28 9v14l-12 7L4 23V9l12-7z" fill="rgba(255,255,255,0.15)" stroke="rgba(255,255,255,0.6)" stroke-width="1.5" />
                <path d="M16 8l7 4v8l-7 4-7-4v-8l7-4z" fill="rgba(255,255,255,0.25)" stroke="#fff" stroke-width="1.5" />
                <circle cx="16" cy="16" r="3" fill="#fff" />
              </svg>
            </div>
            <span class="logo-text">{{ t("authPage.brandTitle") }}</span>
          </div>
          <div class="login-slogan">
            <h2>{{ t("authPage.heroTitle") }}</h2>
            <p>{{ t("authPage.brandSubtitle") }}</p>
          </div>
          <div class="login-features">
            <div class="feature-item">
              <safety-certificate-outlined class="feature-icon" />
              <span>{{ t("authPage.heroPoint1") }}</span>
            </div>
            <div class="feature-item">
              <appstore-outlined class="feature-icon" />
              <span>{{ t("authPage.heroPoint2") }}</span>
            </div>
            <div class="feature-item">
              <team-outlined class="feature-icon" />
              <span>{{ t("authPage.heroPoint3") }}</span>
            </div>
          </div>
        </div>
      </aside>

      <!-- Right Panel -->
      <main class="login-split__right">
        <div class="locale-switch-wrapper">
          <LocaleSwitch />
        </div>

        <div class="login-card">
          <div class="mobile-logo">
            <div class="logo-icon logo-icon--sm">
              <svg viewBox="0 0 32 32" fill="none" xmlns="http://www.w3.org/2000/svg">
                <path d="M16 2L28 9v14l-12 7L4 23V9l12-7z" fill="rgba(22,119,255,0.1)" stroke="var(--color-primary)" stroke-width="1.5" />
                <path d="M16 8l7 4v8l-7 4-7-4v-8l7-4z" fill="rgba(22,119,255,0.15)" stroke="var(--color-primary)" stroke-width="1.5" />
                <circle cx="16" cy="16" r="3" fill="var(--color-primary)" />
              </svg>
            </div>
            <span>{{ t("authPage.brandTitle") }}</span>
          </div>

          <a-tabs v-model:activeKey="activeTab" class="login-tabs" :animated="false">
            <a-tab-pane key="system" :tab="t('authPage.loginTitle')">
              
              <div class="license-wrapper" v-if="!allowLoginForm">
                <div v-if="licenseLoading" class="license-status">
                  <a-spin size="small" />
                  <span>{{ t("authPage.loadingLicense") }}</span>
                </div>
                <template v-else>
                  <div class="license-status" :class="licenseStatusCode === 'Expired' ? 'license-status--expired' : 'license-status--none'">
                    <span class="license-dot" :class="licenseStatusCode === 'Expired' ? 'license-dot--expired' : 'license-dot--none'"></span>
                    <span>{{ licenseStatusCode === "Expired" ? t("authPage.licenseExpired") : t("authPage.licenseInactive") }}</span>
                  </div>
                  <a-alert v-if="licenseActivateResult" :type="licenseActivateResult.success ? 'success' : 'error'" :message="licenseActivateResult.message" closable show-icon style="margin: 12px 0" @close="licenseActivateResult = null" />
                  <a-upload :before-upload="handleLicenseFileSelect" :show-upload-list="false" accept=".atlaslicense,.lic,.txt">
                    <a-button type="primary" size="large" block :loading="licenseActivating">
                      <template #icon><upload-outlined /></template>
                      {{ licenseActivating ? t("authPage.activating") : t("authPage.uploadCertificate") }}
                    </a-button>
                  </a-upload>
                  <p class="license-tip">{{ t("authPage.uploadTip") }}</p>
                </template>
              </div>

              <template v-else>
                <!-- Render modern Multi-Tenant / License UI -->
                <div class="tenant-display">
                  <div class="tenant-info">
                    <bank-outlined class="tenant-icon" />
                    <div class="tenant-name">{{ licenseStatusInfo?.tenantName || 'Atlas Security' }}</div>
                  </div>
                  <div class="tenant-meta">
                    <a-tag color="blue" size="small">{{ licenseEditionText }}</a-tag>
                    <span class="tenant-expire">{{ licenseExpireText }}</span>
                    <a class="tenant-switch-btn" @click="showRenewArea = !showRenewArea">{{ showRenewArea ? t("authPage.collapse") : t("authPage.switchCertificate") }}</a>
                  </div>
                </div>

                <div v-if="showRenewArea" class="license-upload">
                  <a-alert v-if="licenseActivateResult" :type="licenseActivateResult.success ? 'success' : 'error'" :message="licenseActivateResult.message" closable show-icon style="margin-bottom: 8px" @close="licenseActivateResult = null" />
                  <a-upload :before-upload="handleLicenseFileSelect" :show-upload-list="false" accept=".atlaslicense,.lic,.txt">
                    <a-button size="middle" block :loading="licenseActivating">
                      <template #icon><upload-outlined /></template>
                      {{ t("authPage.selectCertificate") }}
                    </a-button>
                  </a-upload>
                </div>

                <div v-if="errorMessage" class="error-banner">
                  <span class="error-icon">!</span>
                  <span>{{ errorMessage }}</span>
                  <span v-if="cooldownSeconds > 0" class="cooldown-text">{{ t("authPage.cooldown", { seconds: cooldownSeconds }) }}</span>
                </div>

                <a-form layout="vertical" :model="form" class="login-form" :disabled="loading" @finish="handleSubmit">
                  <a-form-item name="username" :rules="[{ required: true, message: t('authPage.usernameRequired') }]">
                    <a-input ref="usernameInputRef" v-model:value="form.username" size="large" :placeholder="t('authPage.usernamePlaceholder')" allow-clear autocomplete="username" @focus="errorMessage = ''" aria-label="用户名">
                      <template #prefix><user-outlined class="input-icon" /></template>
                    </a-input>
                  </a-form-item>

                  <a-form-item name="password" :rules="[{ required: true, message: t('authPage.passwordRequired') }]">
                    <a-input-password v-model:value="form.password" size="large" :placeholder="t('authPage.passwordPlaceholder')" autocomplete="current-password" aria-label="密码" @keydown="handleCapsLockEvent" @keyup="handleCapsLockEvent" @blur="capsLockOn = false" @focus="errorMessage = ''">
                      <template #prefix><lock-outlined class="input-icon" /></template>
                    </a-input-password>
                    <div v-if="capsLockOn" class="caps-tip">{{ t("authPage.capsLockOn") }}</div>
                  </a-form-item>

                  <a-form-item v-if="needsCaptcha" name="captchaCode" :rules="[{ required: true, message: t('authPage.captchaRequired') }]">
                    <div class="captcha-row">
                      <a-input v-model:value="form.captchaCode" size="large" :placeholder="t('authPage.captchaPlaceholder')" autocomplete="off" @focus="errorMessage = ''" aria-label="验证码">
                        <template #prefix><safety-certificate-outlined class="input-icon" /></template>
                      </a-input>
                      <div class="captcha-img-wrap">
                        <img v-if="captchaImage" :src="captchaImage" :alt="t('authPage.captchaAlt')" :title="t('authPage.captchaRefresh')" class="captcha-image" @click="loadCaptcha" />
                      </div>
                      <a class="captcha-refresh" @click="loadCaptcha">{{ t('authPage.captchaRefresh') }}</a>
                    </div>
                  </a-form-item>

                  <a-form-item v-if="needsMfa" name="totpCode" :rules="[{ required: true, message: t('authPage.mfaRequired') }]" :help="t('authPage.mfaHelp')">
                    <a-input v-model:value="form.totpCode" size="large" :placeholder="t('authPage.mfaPlaceholder')" :maxlength="6" autocomplete="off" @focus="errorMessage = ''" aria-label="MFA校验码">
                      <template #prefix><safety-certificate-outlined class="input-icon" /></template>
                    </a-input>
                  </a-form-item>

                  <div class="form-extra">
                    <a-checkbox v-model:checked="form.rememberMe">{{ t("authPage.rememberMe") }}</a-checkbox>
                  </div>

                  <a-button type="primary" block html-type="submit" size="large" :loading="loading" :disabled="isSubmitDisabled" class="submit-btn" aria-label="登录">
                    <span v-if="!loading">{{ cooldownSeconds > 0 ? t("authPage.submitWaiting", { seconds: cooldownSeconds }) : t("auth.login") }}</span>
                    <span v-else>{{ t("authPage.submitting") }}</span>
                  </a-button>

                  <div class="form-bottom-links">
                    <a href="/password-reset" class="forgot-link">{{ t("authPage.forgotPassword") }}</a>
                    <span class="register-link">
                      {{ t("authPage.noAccount") }} <router-link to="/register">{{ t("authPage.registerNow") }}</router-link>
                    </span>
                  </div>
                </a-form>
              </template>
            </a-tab-pane>
          </a-tabs>
        </div>

        <footer class="form-footer">
          <span>{{ t("authPage.privacyPolicy") }}</span>
          <span class="sep">|</span>
          <span>{{ t("authPage.userAgreement") }}</span>
          <span class="sep">|</span>
          <span>v1.0.2</span>
        </footer>
      </main>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { 
  UploadOutlined, 
  UserOutlined, 
  LockOutlined, 
  SafetyCertificateOutlined, 
  AppstoreOutlined, 
  TeamOutlined, 
  BankOutlined 
} from "@ant-design/icons-vue";
import { useI18n } from "vue-i18n";
import LocaleSwitch from "@/components/layout/LocaleSwitch.vue";
import { activateLicense, getCaptcha, getLicenseStatus } from "@/services/api";
import { usePermissionStore } from "@/stores/permission";
import { useUserStore } from "@/stores/user";
import type { RequestOptions } from "@/services/api";
import type { LicenseStatus } from "@/types/api";
import { clearAuthStorage, getTenantId } from "@/utils/auth";

interface LoginApiError extends Error {
  status?: number;
  payload?: {
    code?: string;
    message?: string;
    traceId?: string;
  } | null;
}

const TENANT_ID_REGEX =
  /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;
const COOLDOWN_THRESHOLD = 5;
const COOLDOWN_DURATION = 30;
const REMEMBER_ME_KEY = "atlas-login-remember-me";

const { t } = useI18n();
const userStore = useUserStore();
const permissionStore = usePermissionStore();
const route = useRoute();
const router = useRouter();

const loading = ref(false);
const errorMessage = ref("");
const failedAttempts = ref(0);
const capsLockOn = ref(false);
const cooldownSeconds = ref(0);
const needsCaptcha = ref(false);
const captchaImage = ref("");
const needsMfa = ref(false);
const licenseLoading = ref(true);
const licenseActivating = ref(false);
const licenseActivateResult = ref<{ success: boolean; message: string } | null>(null);
const licenseStatusCode = ref<string>("None");
const licenseStatusInfo = ref<LicenseStatus | null>(null);
const showRenewArea = ref(false);
const activeTab = ref("system");
const usernameInputRef = ref();

let cooldownTimer: number | undefined;

const form = reactive({
  tenantId: getTenantId() ?? "",
  username: "",
  password: "",
  rememberMe: localStorage.getItem(REMEMBER_ME_KEY) === "true",
  captchaKey: "",
  captchaCode: "",
  totpCode: ""
});

const licenseEditionText = computed(() => licenseStatusInfo.value?.edition ?? t("authPage.licenseEditionUnknown"));

const licenseExpireText = computed(() => {
  const info = licenseStatusInfo.value;
  if (!info) {
    return "";
  }
  if (info.isPermanent) {
    return t("authPage.license.permanent");
  }
  if (info.remainingDays !== null && info.remainingDays !== undefined) {
    return t("authPage.license.remainingDays", { days: info.remainingDays });
  }
  if (info.expiresAt) {
    return t("authPage.license.expiresAt", { date: info.expiresAt.substring(0, 10) });
  }
  return "";
});

const allowLoginForm = computed(() => licenseStatusCode.value === "Active");

watch(allowLoginForm, (val) => {
  if (val) {
    setTimeout(() => {
      usernameInputRef.value?.focus();
    }, 100);
  }
}, { immediate: true });

const isSubmitDisabled = computed(
  () =>
    loading.value ||
    cooldownSeconds.value > 0 ||
    !hasValidTenantId(form.tenantId.trim()) ||
    !form.username.trim() ||
    !form.password ||
    !allowLoginForm.value
);

function hasValidTenantId(tenantId: string): boolean {
  return TENANT_ID_REGEX.test(tenantId);
}

function handleCapsLockEvent(event: KeyboardEvent): void {
  if (typeof event.getModifierState === "function") {
    capsLockOn.value = event.getModifierState("CapsLock");
  }
}

function startCooldown(): void {
  cooldownSeconds.value = COOLDOWN_DURATION;
  window.clearInterval(cooldownTimer);
  cooldownTimer = window.setInterval(() => {
    cooldownSeconds.value -= 1;
    if (cooldownSeconds.value <= 0) {
      window.clearInterval(cooldownTimer);
      cooldownSeconds.value = 0;
    }
  }, 1000);
}

function appendTraceId(message: string, traceId?: string): string {
  return traceId ? `${message} (traceId: ${traceId})` : message;
}

function normalizeError(error: unknown): string {
  const loginError = error as LoginApiError;
  const code = loginError?.payload?.code ?? "";
  const traceId = loginError?.payload?.traceId;
  const raw = error instanceof Error ? error.message : t("authPage.errors.loginFailed");

  if (code === "INVALID_CREDENTIALS" || raw.toLowerCase().includes("credential")) {
    return t("authPage.errors.invalidCredentials");
  }
  if (code === "ACCOUNT_LOCKED") {
    return t("authPage.errors.accountLocked");
  }
  if (code === "PASSWORD_EXPIRED") {
    return t("authPage.errors.passwordExpired");
  }
  if (code === "TENANT_NOT_FOUND") {
    return t("authPage.errors.tenantNotFound");
  }
  if (code === "VALIDATION_ERROR") {
    return raw || t("authPage.errors.validation");
  }
  if (raw.toLowerCase().includes("network")) {
    return t("authPage.errors.network");
  }

  return appendTraceId(raw, traceId);
}

function shouldLoadCaptcha(code: string, rawMessage: string): boolean {
  return code === "CAPTCHA_INVALID"
    || rawMessage.toLowerCase().includes("captcha")
    || failedAttempts.value >= COOLDOWN_THRESHOLD;
}

async function loadCaptcha(): Promise<void> {
  if (!form.tenantId) {
    return;
  }
  try {
    const res = await getCaptcha(form.tenantId.trim());
    form.captchaKey = res.captchaKey;
    captchaImage.value = res.captchaImage;
    form.captchaCode = "";
  } catch {
    captchaImage.value = "";
  }
}

async function handleSubmit(): Promise<void> {
  errorMessage.value = "";
  loading.value = true;
  try {
    clearAuthStorage();
    const tenantId = form.tenantId.trim();
    const tokenOptions: RequestOptions = { suppressErrorMessage: true };

    await userStore.login(tenantId, form.username.trim(), form.password, tokenOptions, {
      rememberMe: form.rememberMe,
      captchaKey: form.captchaKey || undefined,
      captchaCode: form.captchaCode || undefined,
      totpCode: form.totpCode || undefined
    });
    await userStore.getInfo();
    const routes = await permissionStore.generateRoutes();
    permissionStore.registerRoutes(router);
    localStorage.setItem(REMEMBER_ME_KEY, String(form.rememberMe));
    failedAttempts.value = 0;
    cooldownSeconds.value = 0;
    errorMessage.value = "";

    const rawRedirect = route.query.redirect;
    const redirect =
      typeof rawRedirect === "string"
      && rawRedirect.startsWith("/")
      && !rawRedirect.startsWith("//")
        ? rawRedirect
        : null;
    const targetPath = redirect ?? "/console";
    const canNavigate = routes.some((item) => typeof item.path === "string" && targetPath.startsWith(item.path));
    const fallbackPath = "/console";
    const staticAllowedTargets = new Set(["/console"]);
    const isUnsafeRedirect =
      targetPath === "/"
      || targetPath.startsWith("/login")
      || targetPath.startsWith("/register");

    void router.push(!isUnsafeRedirect && (canNavigate || staticAllowedTargets.has(targetPath)) ? targetPath : fallbackPath);
  } catch (error) {
    clearAuthStorage();
    failedAttempts.value += 1;

    const loginError = error as LoginApiError;
    const code = loginError?.payload?.code ?? "";
    const rawMessage = error instanceof Error ? error.message : "";

    if (code === "MFA_REQUIRED") {
      needsMfa.value = true;
      errorMessage.value = t("authPage.errors.mfaRequired");
      return;
    }

    if (shouldLoadCaptcha(code, rawMessage)) {
      needsCaptcha.value = true;
      void loadCaptcha();
    }

    errorMessage.value = normalizeError(error);
    if (failedAttempts.value >= COOLDOWN_THRESHOLD) {
      startCooldown();
    }
  } finally {
    loading.value = false;
  }
}

async function handleLicenseFileSelect(file: File): Promise<false> {
  licenseActivating.value = true;
  licenseActivateResult.value = null;

  let content = "";
  try {
    content = await readFileAsText(file);
  } catch (error) {
    licenseActivateResult.value = {
      success: false,
      message: error instanceof Error ? error.message : t("authPage.errors.fileReadFailed")
    };
    licenseActivating.value = false;
    return false;
  }

  try {
    const resp = await activateLicense(content);
    if (resp.success) {
      licenseActivateResult.value = {
        success: true,
        message: resp.data?.message ?? resp.message ?? t("common.success")
      };
      showRenewArea.value = false;
      await loadLicenseStatus();
    } else {
      licenseActivateResult.value = {
        success: false,
        message: resp.message || t("authPage.errors.licenseActivateFailed")
      };
    }
  } catch (error) {
    const requestError = error as LoginApiError;
    const detailMessage = requestError?.payload?.message ?? (error instanceof Error ? error.message : "");
    licenseActivateResult.value = {
      success: false,
      message: detailMessage || t("authPage.errors.licenseActivateFailed")
    };
  } finally {
    licenseActivating.value = false;
  }

  return false;
}

function readFileAsText(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = (event) => resolve((event.target?.result as string) ?? "");
    reader.onerror = () => reject(new Error(t("authPage.errors.fileReadFailed")));
    reader.readAsText(file);
  });
}

async function loadLicenseStatus(): Promise<void> {
  licenseLoading.value = true;
  try {
    const status = await getLicenseStatus();
    licenseStatusCode.value = status.status;
    licenseStatusInfo.value = status;

    if (status.status === "Active" && status.tenantId && hasValidTenantId(status.tenantId)) {
      if (!form.tenantId || !hasValidTenantId(form.tenantId)) {
        form.tenantId = status.tenantId;
      }
    }
  } catch {
    licenseStatusCode.value = "None";
  } finally {
    licenseLoading.value = false;
  }
}

onMounted(() => {
  void loadLicenseStatus();
});

onBeforeUnmount(() => {
  window.clearInterval(cooldownTimer);
});
</script>

<style scoped>
.login-wrapper {
  min-height: 100vh;
  display: flex;
  background: var(--color-bg-base, #f0f2f5);
}

.login-split {
  display: flex;
  width: 100vw;
  height: 100vh;
  overflow: hidden;
}

/* Left Panel */
.login-split__left {
  flex: 1;
  background: linear-gradient(145deg, #001529 0%, #0052cc 100%);
  position: relative;
  overflow: hidden;
  display: flex;
  flex-direction: column;
  color: #fff;
  padding: 60px 80px;
}

.tech-bg {
  position: absolute;
  top: 0; left: 0; right: 0; bottom: 0;
  z-index: 0;
  background-image: 
    linear-gradient(rgba(255, 255, 255, 0.03) 1px, transparent 1px),
    linear-gradient(90deg, rgba(255, 255, 255, 0.03) 1px, transparent 1px);
  background-size: 30px 30px;
}

.bg-shape {
  position: absolute;
  border-radius: 50%;
  background: rgba(255, 255, 255, 0.03);
}
.shape-1 { width: 500px; height: 500px; bottom: -100px; right: -100px; }
.shape-2 { width: 300px; height: 300px; top: -50px; left: -50px; }
.shape-3 { width: 150px; height: 150px; bottom: 30%; right: 20%; background: rgba(255,255,255,0.05); }

.login-left-content {
  position: relative;
  z-index: 1;
  max-width: 600px;
  margin: auto 0;
}

.login-logo {
  display: flex;
  align-items: center;
  gap: 16px;
  margin-bottom: 60px;
}
.logo-icon { width: 44px; height: 44px; }
.logo-icon svg { width: 100%; height: 100%; }
.logo-text { font-size: 26px; font-weight: 600; letter-spacing: 0.5px; }

.login-slogan h2 { font-size: 36px; font-weight: 600; margin: 0 0 16px; line-height: 1.4; color: #fff;}
.login-slogan p { font-size: 16px; opacity: 0.8; margin-bottom: 60px; letter-spacing: 1px; }

.login-features { display: flex; flex-direction: column; gap: 24px; }
.feature-item { display: flex; align-items: center; gap: 16px; font-size: 16px; opacity: 0.9; }
.feature-icon { font-size: 24px; display: inline-flex; align-items: center; justify-content: center; width: 40px; height: 40px; background: rgba(255, 255, 255, 0.1); border-radius: 8px; }

/* Right Panel */
.login-split__right {
  width: 520px;
  background: var(--color-bg-container, #fff);
  display: flex;
  flex-direction: column;
  position: relative;
  box-shadow: -10px 0 30px rgba(0,0,0,0.05);
}

.locale-switch-wrapper { position: absolute; top: 24px; right: 24px; z-index: 10;}

.login-card {
  flex: 1;
  display: flex;
  flex-direction: column;
  justify-content: center;
  padding: 0 60px;
  max-width: 520px;
  margin: 0 auto;
  width: 100%;
}

.mobile-logo { display: none; }

.login-tabs :deep(.ant-tabs-nav) {
  margin-bottom: 32px;
}
.login-tabs :deep(.ant-tabs-tab) {
  font-size: 18px;
  padding: 12px 0;
}

.tenant-display {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  background: var(--color-bg-subtle, #f5f5f5);
  border: 1px solid var(--color-border);
  border-radius: 8px;
  margin-bottom: 24px;
}
.tenant-info { display: flex; align-items: center; gap: 8px; }
.tenant-icon { font-size: 20px; color: var(--color-primary); }
.tenant-name { font-size: 15px; font-weight: 600; color: var(--color-text-primary); }
.tenant-meta { display: flex; flex-direction: column; align-items: flex-end; gap: 4px; }
.tenant-switch-btn { font-size: 12px; color: var(--color-primary); cursor: pointer; }
.tenant-expire { font-size: 12px; color: var(--color-text-tertiary); margin-top: 2px; }

.license-wrapper { padding: 24px; background: var(--color-bg-layout, #fafafa); border-radius: 8px; text-align: center; }
.license-status { font-size: 16px; font-weight: 500; margin-bottom: 16px; display: flex; align-items: center; justify-content: center; gap: 8px; color: var(--color-text-primary);}
.license-dot { width: 8px; height: 8px; border-radius: 50%; }
.license-dot--expired { background: var(--color-error); }
.license-dot--none { background: var(--color-warning); }
.license-tip { margin-top: 12px; font-size: 13px; color: var(--color-text-tertiary); }
.license-upload { margin-bottom: 24px; }

.login-form :deep(.ant-form-item) { margin-bottom: 24px; }
.login-form :deep(.ant-input-affix-wrapper) { padding: 0 11px; border-radius: 6px; }
.input-icon { color: var(--color-text-tertiary); font-size: 16px; margin-right: 4px; }

.captcha-row { display: flex; gap: 12px; align-items: center; }
.captcha-img-wrap { width: 120px; height: 40px; border-radius: 6px; overflow: hidden; background: #f0f0f0; border: 1px solid var(--color-border); }
.captcha-image { width: 100%; height: 100%; object-fit: cover; cursor: pointer; }
.captcha-refresh { font-size: 13px; color: var(--color-primary); white-space: nowrap; cursor: pointer;}

.form-extra { display: flex; margin-bottom: 24px; align-items: center; }

.submit-btn { height: 44px; font-size: 16px; border-radius: 6px; margin-bottom: 24px; }

.form-bottom-links { display: flex; justify-content: space-between; align-items: center; font-size: 14px;}
.forgot-link { color: var(--color-text-secondary); transition: color 0.3s; }
.forgot-link:hover { color: var(--color-primary); }
.register-link { color: var(--color-text-secondary); }

.error-banner { display: flex; align-items: center; gap: 8px; background: var(--color-error-bg, #fff2f0); border: 1px solid var(--color-error-border, #ffccc7); color: var(--color-error-text, #ff4d4f); padding: 10px 14px; border-radius: 6px; margin-bottom: 24px; font-size: 14px; }
.error-icon { width: 18px; height: 18px; border-radius: 50%; background: var(--color-error-text, #ff4d4f); color: #fff; display: inline-flex; align-items: center; justify-content: center; font-size: 12px; font-weight: bold;}

.caps-tip { font-size: 12px; color: var(--color-warning); margin-top: 4px; }
.cooldown-text { margin-left: auto; color: var(--color-text-tertiary); font-size: 12px; }

.form-footer { text-align: center; padding: 24px; font-size: 13px; color: var(--color-text-tertiary); }
.form-footer .sep { margin: 0 8px; opacity: 0.5;}

@media screen and (max-width: 1024px) {
  .login-split__left { flex: 0 0 40%; padding: 40px; }
  .login-split__right { flex: 0 0 60%; width: auto; }
}

@media screen and (max-width: 768px) {
  .login-split__left { display: none; }
  .login-split__right { width: 100vw; }
  .login-card { padding: 0 24px; max-width: 480px; margin: 0 auto;}
  .mobile-logo { display: flex; align-items: center; gap: 12px; margin-bottom: 40px; font-size: 22px; font-weight: 600; color: var(--color-text-primary); }
  .logo-icon--sm { width: 36px; height: 36px; }
  .logo-icon--sm svg { width: 100%; height: 100%; }
}
</style>
