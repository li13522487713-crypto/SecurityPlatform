<template>
  <div class="login-page">
    <div class="login-container">
      <aside class="brand-panel">
        <div class="brand-content">
          <div class="brand-logo">
            <div class="logo-icon">
              <svg viewBox="0 0 32 32" fill="none" xmlns="http://www.w3.org/2000/svg">
                <path d="M16 2L28 9v14l-12 7L4 23V9l12-7z" fill="rgba(255,255,255,0.15)" stroke="rgba(255,255,255,0.6)" stroke-width="1.5" />
                <path d="M16 8l7 4v8l-7 4-7-4v-8l7-4z" fill="rgba(255,255,255,0.25)" stroke="#fff" stroke-width="1.5" />
                <circle cx="16" cy="16" r="3" fill="#fff" />
              </svg>
            </div>
            <div class="brand-text">
              <h1>{{ t("authPage.brandTitle") }}</h1>
              <p>{{ t("authPage.brandSubtitle") }}</p>
            </div>
          </div>
          <div class="brand-desc">
            <h2>{{ t("authPage.heroTitle") }}</h2>
            <ul>
              <li>{{ t("authPage.heroPoint1") }}</li>
              <li>{{ t("authPage.heroPoint2") }}</li>
              <li>{{ t("authPage.heroPoint3") }}</li>
            </ul>
          </div>
        </div>
        <div class="brand-footer">
          <span>{{ t("authPage.compliance") }}</span>
        </div>
        <div class="decor decor-1" aria-hidden="true"></div>
        <div class="decor decor-2" aria-hidden="true"></div>
        <div class="decor decor-3" aria-hidden="true"></div>
      </aside>

      <main class="form-panel">
        <div class="locale-switch-wrapper">
          <LocaleSwitch />
        </div>

        <div class="form-wrapper">
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

          <h3 class="form-title">{{ t("authPage.loginTitle") }}</h3>

          <div class="license-section">
            <div v-if="licenseLoading" class="license-status">
              <a-spin size="small" />
              <span>{{ t("authPage.loadingLicense") }}</span>
            </div>

            <template v-else-if="licenseStatusCode === 'Active'">
              <div class="license-status license-status--active">
                <span class="license-dot license-dot--active"></span>
                <span>{{ t("authPage.licenseActive") }}</span>
                <span v-if="licenseStatusInfo?.tenantName" class="license-org">
                  {{ licenseStatusInfo.tenantName }}
                </span>
                <a-tag color="blue" size="small">{{ licenseEditionText }}</a-tag>
                <span class="license-expire">{{ licenseExpireText }}</span>
                <a class="license-action" @click="showRenewArea = !showRenewArea">
                  {{ showRenewArea ? t("authPage.collapse") : t("authPage.switchCertificate") }}
                </a>
              </div>
              <div v-if="showRenewArea" class="license-upload">
                <a-alert
                  v-if="licenseActivateResult"
                  :type="licenseActivateResult.success ? 'success' : 'error'"
                  :message="licenseActivateResult.message"
                  closable
                  show-icon
                  style="margin-bottom: 8px"
                  @close="licenseActivateResult = null"
                />
                <a-upload
                  :before-upload="handleLicenseFileSelect"
                  :show-upload-list="false"
                  accept=".atlaslicense,.lic,.txt"
                >
                  <a-button size="small" :loading="licenseActivating">
                    <template #icon><upload-outlined /></template>
                    {{ t("authPage.selectCertificate") }}
                  </a-button>
                </a-upload>
              </div>
            </template>

            <template v-else>
              <div class="license-status" :class="licenseStatusCode === 'Expired' ? 'license-status--expired' : 'license-status--none'">
                <span class="license-dot" :class="licenseStatusCode === 'Expired' ? 'license-dot--expired' : 'license-dot--none'"></span>
                <span>{{ licenseStatusCode === "Expired" ? t("authPage.licenseExpired") : t("authPage.licenseInactive") }}</span>
                <span class="license-hint">
                  {{ licenseStatusCode === "Expired" ? t("authPage.license.uploadHintExpired") : t("authPage.license.uploadHintInactive") }}
                </span>
              </div>
              <a-alert
                v-if="licenseActivateResult"
                :type="licenseActivateResult.success ? 'success' : 'error'"
                :message="licenseActivateResult.message"
                closable
                show-icon
                style="margin: 8px 0"
                @close="licenseActivateResult = null"
              />
              <a-upload
                :before-upload="handleLicenseFileSelect"
                :show-upload-list="false"
                accept=".atlaslicense,.lic,.txt"
              >
                <a-button type="primary" size="small" :loading="licenseActivating">
                  <template #icon><upload-outlined /></template>
                  {{ licenseActivating ? t("authPage.activating") : t("authPage.uploadCertificate") }}
                </a-button>
              </a-upload>
              <p class="license-tip">{{ t("authPage.uploadTip") }}</p>
            </template>
          </div>

          <a-divider style="margin: 16px 0" />

          <template v-if="licenseStatusCode === 'Active'">
            <div v-if="errorMessage" class="error-banner">
              <span class="error-icon">!</span>
              <span>{{ errorMessage }}</span>
              <span v-if="cooldownSeconds > 0" class="cooldown-text">
                {{ t("authPage.cooldown", { seconds: cooldownSeconds }) }}
              </span>
            </div>

            <a-form
              layout="vertical"
              :model="form"
              class="login-form"
              :disabled="loading"
              @finish="handleSubmit"
            >
              <a-form-item
                :label="t('authPage.tenantLabel')"
                name="tenantId"
                :rules="[
                  { required: true, message: t('authPage.tenantRequired') },
                  { pattern: TENANT_ID_REGEX, message: t('authPage.tenantInvalid') }
                ]"
              >
                <a-input
                  v-model:value="form.tenantId"
                  :placeholder="t('authPage.tenantPlaceholder')"
                  readonly
                  autocomplete="off"
                  @focus="errorMessage = ''"
                />
                <div class="field-tip">{{ t("authPage.tenantTip") }}</div>
                <div v-if="!hasValidTenantId(form.tenantId.trim())" class="field-error">
                  {{ t("authPage.tenantError") }}
                </div>
              </a-form-item>

              <a-form-item
                :label="t('authPage.usernameLabel')"
                name="username"
                :rules="[{ required: true, message: t('authPage.usernameRequired') }]"
              >
                <a-input
                  v-model:value="form.username"
                  :placeholder="t('authPage.usernamePlaceholder')"
                  allow-clear
                  autocomplete="username"
                  @focus="errorMessage = ''"
                />
              </a-form-item>

              <a-form-item
                :label="t('authPage.passwordLabel')"
                name="password"
                :rules="[{ required: true, message: t('authPage.passwordRequired') }]"
              >
                <a-input-password
                  v-model:value="form.password"
                  :placeholder="t('authPage.passwordPlaceholder')"
                  autocomplete="current-password"
                  @keydown="handleCapsLockEvent"
                  @keyup="handleCapsLockEvent"
                  @blur="capsLockOn = false"
                  @focus="errorMessage = ''"
                />
                <div v-if="capsLockOn" class="caps-tip">{{ t("authPage.capsLockOn") }}</div>
              </a-form-item>

              <a-form-item
                v-if="needsCaptcha"
                :label="t('authPage.captchaLabel')"
                name="captchaCode"
                :rules="[{ required: true, message: t('authPage.captchaRequired') }]"
              >
                <div class="captcha-row">
                  <a-input
                    v-model:value="form.captchaCode"
                    :placeholder="t('authPage.captchaPlaceholder')"
                    autocomplete="off"
                    @focus="errorMessage = ''"
                  />
                  <img
                    v-if="captchaImage"
                    :src="captchaImage"
                    :alt="t('authPage.captchaAlt')"
                    :title="t('authPage.captchaRefresh')"
                    class="captcha-image"
                    @click="loadCaptcha"
                  />
                </div>
              </a-form-item>

              <a-form-item
                v-if="needsMfa"
                :label="t('authPage.mfaLabel')"
                name="totpCode"
                :rules="[{ required: true, message: t('authPage.mfaRequired') }]"
                :help="t('authPage.mfaHelp')"
              >
                <a-input
                  v-model:value="form.totpCode"
                  :placeholder="t('authPage.mfaPlaceholder')"
                  :maxlength="6"
                  autocomplete="off"
                  @focus="errorMessage = ''"
                />
              </a-form-item>

              <div class="form-extra">
                <a-checkbox v-model:checked="form.rememberMe">{{ t("authPage.rememberMe") }}</a-checkbox>
                <a href="/password-reset" class="forgot-link">{{ t("authPage.forgotPassword") }}</a>
              </div>

              <a-button
                type="primary"
                block
                html-type="submit"
                size="large"
                :loading="loading"
                :disabled="isSubmitDisabled"
                class="submit-btn"
              >
                <span v-if="!loading">
                  {{ cooldownSeconds > 0 ? t("authPage.submitWaiting", { seconds: cooldownSeconds }) : t("auth.login") }}
                </span>
                <span v-else>{{ t("authPage.submitting") }}</span>
              </a-button>

              <div class="register-link">
                {{ t("authPage.noAccount") }}
                <router-link to="/register">{{ t("authPage.registerNow") }}</router-link>
              </div>
            </a-form>
          </template>

          <template v-else-if="!licenseLoading">
            <div class="login-locked">
              {{ t("authPage.loginLocked") }}
            </div>
          </template>
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
import { computed, onBeforeUnmount, onMounted, reactive, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { UploadOutlined } from "@ant-design/icons-vue";
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

const isSubmitDisabled = computed(
  () =>
    loading.value ||
    cooldownSeconds.value > 0 ||
    !hasValidTenantId(form.tenantId.trim()) ||
    !form.username.trim() ||
    !form.password ||
    licenseStatusCode.value !== "Active"
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
.login-page {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--color-bg-base);
  background-image: radial-gradient(circle at 15% 50%, rgba(0, 137, 255, 0.08), transparent 25%),
                    radial-gradient(circle at 85% 30%, rgba(0, 137, 255, 0.08), transparent 25%);
}

.login-container {
  display: flex;
  width: 960px;
  max-width: 90%;
  height: 560px;
  background: var(--color-bg-container);
  border-radius: var(--border-radius-lg);
  box-shadow: var(--shadow-lg);
  overflow: hidden;
}

.brand-panel {
  width: 400px;
  background: linear-gradient(145deg, #0052cc 0%, #0089ff 100%);
  color: #fff;
  display: flex;
  flex-direction: column;
  padding: 48px 40px;
  position: relative;
  flex-shrink: 0;
}

.brand-content {
  flex: 1;
  display: flex;
  flex-direction: column;
  justify-content: center;
  position: relative;
  z-index: 1;
}

.brand-logo {
  display: flex;
  align-items: center;
  gap: 14px;
  margin-bottom: 48px;
}

.logo-icon {
  width: 44px;
  height: 44px;
  flex-shrink: 0;
}

.logo-icon svg {
  width: 100%;
  height: 100%;
}

.brand-text h1 {
  margin: 0;
  font-size: 22px;
  font-weight: 600;
  letter-spacing: 0.5px;
}

.brand-text p {
  margin: 2px 0 0;
  font-size: 12px;
  opacity: 0.65;
  letter-spacing: 1px;
}

.brand-desc h2 {
  font-size: 26px;
  font-weight: 600;
  margin: 0 0 24px;
  line-height: 1.4;
}

.brand-desc ul {
  list-style: none;
  padding: 0;
  margin: 0;
}

.brand-desc li {
  padding: 8px 0;
  font-size: 14px;
  opacity: 0.85;
  position: relative;
  padding-left: 20px;
}

.brand-desc li::before {
  content: "";
  position: absolute;
  left: 0;
  top: 50%;
  transform: translateY(-50%);
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: rgba(255, 255, 255, 0.6);
}

.brand-footer {
  position: relative;
  z-index: 1;
  font-size: 12px;
  opacity: 0.5;
}

.decor {
  position: absolute;
  border-radius: 50%;
  background: rgba(255, 255, 255, 0.04);
}

.decor-1 {
  width: 300px;
  height: 300px;
  bottom: -80px;
  right: -80px;
}

.decor-2 {
  width: 180px;
  height: 180px;
  top: -40px;
  right: 60px;
}

.decor-3 {
  width: 100px;
  height: 100px;
  bottom: 120px;
  left: -30px;
}

.form-panel {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 40px;
  background: var(--color-bg-container);
  position: relative;
}

.locale-switch-wrapper {
  position: absolute;
  top: 16px;
  right: 16px;
}

.form-wrapper {
  width: 100%;
  max-width: 400px;
}

.mobile-logo {
  display: none;
  align-items: center;
  gap: 10px;
  margin-bottom: 32px;
  font-size: 18px;
  font-weight: 600;
  color: var(--color-text-primary);
}

.logo-icon--sm {
  width: 32px;
  height: 32px;
}

.form-title {
  font-size: 24px;
  font-weight: 600;
  color: var(--color-text-primary);
  margin: 0 0 24px;
}

.license-section {
  padding: 12px 16px;
  background: var(--color-bg-subtle);
  border-radius: var(--border-radius-md);
  border: 1px solid var(--color-border);
}

.license-status {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
  font-size: 13px;
  color: var(--color-text-secondary);
}

.license-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  flex-shrink: 0;
}

.license-dot--active { background: var(--color-success); }
.license-dot--expired { background: var(--color-warning); }
.license-dot--none { background: var(--color-text-quaternary); }

.license-status--active { color: var(--color-text-primary); }
.license-status--expired { color: var(--color-warning); }

.license-org {
  font-weight: 500;
  color: var(--color-text-primary);
}

.license-expire {
  color: var(--color-text-tertiary);
  font-size: 12px;
}

.license-action {
  margin-left: auto;
  font-size: 12px;
  color: var(--color-primary);
  cursor: pointer;
}

.license-hint {
  color: var(--color-text-tertiary);
  font-size: 12px;
}

.license-upload {
  margin-top: 10px;
  padding: 10px;
  border: 1px dashed var(--color-border-secondary);
  border-radius: 6px;
  background: #fff;
}

.license-tip {
  margin: 8px 0 0;
  font-size: 12px;
  color: var(--color-text-tertiary);
}

.login-form :deep(.ant-form-item) {
  margin-bottom: 20px;
}

.login-form :deep(.ant-input),
.login-form :deep(.ant-input-password .ant-input) {
  height: 40px;
}

.field-tip {
  margin-top: 4px;
  color: var(--color-text-tertiary);
  font-size: 12px;
}

.field-error {
  margin-top: 4px;
  color: var(--color-error-text);
  font-size: 12px;
}

.caps-tip {
  margin-top: 4px;
  color: var(--color-warning);
  font-size: 12px;
}

.captcha-row {
  display: flex;
  gap: 8px;
}

.captcha-image {
  cursor: pointer;
  height: 32px;
  border-radius: 4px;
}

.form-extra {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
}

.forgot-link {
  color: var(--color-primary);
  font-size: 14px;
}

.submit-btn {
  height: 44px;
  font-size: 16px;
  border-radius: var(--border-radius-md);
}

.register-link {
  text-align: center;
  margin-top: 16px;
  font-size: 14px;
  color: var(--color-text-secondary);
}

.register-link a {
  color: var(--color-primary);
  margin-left: 4px;
}

.login-locked {
  padding: 24px 0;
  text-align: center;
  color: var(--color-text-tertiary);
  font-size: 14px;
}

.error-banner {
  display: flex;
  align-items: center;
  gap: 8px;
  background: var(--color-error-bg);
  border: 1px solid var(--color-error-border);
  color: var(--color-error-text);
  padding: 10px 14px;
  border-radius: var(--border-radius-md);
  margin-bottom: 16px;
  font-size: 14px;
}

.error-icon {
  width: 20px;
  height: 20px;
  border-radius: 50%;
  background: var(--color-error-text);
  color: #fff;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-size: 12px;
  font-weight: 700;
  flex-shrink: 0;
}

.cooldown-text {
  margin-left: auto;
  font-size: 12px;
  color: var(--color-text-tertiary);
  white-space: nowrap;
}

.form-footer {
  margin-top: 48px;
  text-align: center;
  font-size: 12px;
  color: var(--color-text-quaternary);
}

.form-footer .sep {
  margin: 0 6px;
}

@media screen and (max-width: 960px) {
  .login-container {
    width: 100%;
    height: 100vh;
    max-width: 100%;
    border-radius: 0;
    box-shadow: none;
    flex-direction: column;
  }

  .brand-panel {
    display: none;
  }

  .mobile-logo {
    display: flex;
  }

  .form-panel {
    padding: 32px 24px;
    height: 100vh;
  }
}

@media screen and (max-width: 480px) {
  .form-panel {
    padding: 24px 16px;
  }

  .form-title {
    font-size: 20px;
  }

  .form-wrapper {
    max-width: 100%;
  }
}
</style>
