<template>
  <div class="login-wrapper">
    <div class="login-split">
      <aside class="login-split__left">
        <div class="tech-bg">
          <div class="bg-shape shape-1"></div>
          <div class="bg-shape shape-2"></div>
          <div class="bg-shape shape-3"></div>
        </div>
        <div class="login-left-content">
          <div class="login-logo">
            <div class="logo-icon">
              <svg viewBox="0 0 32 32" fill="none" xmlns="http://www.w3.org/2000/svg">
                <path d="M16 2L28 9v14l-12 7L4 23V9l12-7z" fill="rgba(255,255,255,0.15)" stroke="rgba(255,255,255,0.6)" stroke-width="1.5" />
                <path d="M16 8l7 4v8l-7 4-7-4v-8l7-4z" fill="rgba(255,255,255,0.25)" stroke="#fff" stroke-width="1.5" />
                <circle cx="16" cy="16" r="3" fill="#fff" />
              </svg>
            </div>
            <span class="logo-text">{{ t("appLogin.brandTitle") }}</span>
          </div>
          <div class="login-slogan">
            <h2>{{ t("appLogin.heroTitle") }}</h2>
            <p>{{ t("appLogin.heroSubtitle") }}</p>
          </div>
          <div class="login-features">
            <div class="feature-item">
              <safety-certificate-outlined class="feature-icon" />
              <span>{{ t("appLogin.heroPoint1") }}</span>
            </div>
            <div class="feature-item">
              <appstore-outlined class="feature-icon" />
              <span>{{ t("appLogin.heroPoint2") }}</span>
            </div>
            <div class="feature-item">
              <team-outlined class="feature-icon" />
              <span>{{ t("appLogin.heroPoint3") }}</span>
            </div>
          </div>
        </div>
      </aside>

      <main class="login-split__right">
        <div class="locale-switch-wrapper">
          <LocaleSwitch />
        </div>

        <div class="login-card">
          <div class="mobile-logo">
            <div class="logo-icon logo-icon--sm">
              <svg viewBox="0 0 32 32" fill="none" xmlns="http://www.w3.org/2000/svg">
                <path d="M16 2L28 9v14l-12 7L4 23V9l12-7z" fill="rgba(22,119,255,0.1)" stroke="var(--color-primary, #1677ff)" stroke-width="1.5" />
                <path d="M16 8l7 4v8l-7 4-7-4v-8l7-4z" fill="rgba(22,119,255,0.15)" stroke="var(--color-primary, #1677ff)" stroke-width="1.5" />
                <circle cx="16" cy="16" r="3" fill="var(--color-primary, #1677ff)" />
              </svg>
            </div>
            <span>{{ t("appLogin.brandTitle") }}</span>
          </div>

          <h3 class="login-title">{{ t("appLogin.title") }}</h3>
          <div class="login-app-badge">
            <appstore-outlined />
            <span>{{ appKey }}</span>
          </div>

          <a-alert
            v-if="errorMessage"
            type="error"
            show-icon
            :message="errorMessage"
            closable
            class="login-error"
            @close="errorMessage = ''"
          />

          <a-form layout="vertical" :model="form" @finish="handleSubmit">
            <a-form-item
              :label="t('auth.tenantId')"
              name="tenantId"
              :rules="[{ required: true, message: t('appLogin.tenantIdRequired') }]"
            >
              <a-input
                v-model:value="form.tenantId"
                size="large"
                :placeholder="t('appLogin.tenantIdPlaceholder')"
              >
                <template #prefix><bank-outlined /></template>
              </a-input>
            </a-form-item>

            <a-form-item
              :label="t('auth.username')"
              name="username"
              :rules="[{ required: true, message: t('appLogin.usernameRequired') }]"
            >
              <a-input
                v-model:value="form.username"
                size="large"
                :placeholder="t('appLogin.usernamePlaceholder')"
              >
                <template #prefix><user-outlined /></template>
              </a-input>
            </a-form-item>

            <a-form-item
              :label="t('auth.password')"
              name="password"
              :rules="[{ required: true, message: t('appLogin.passwordRequired') }]"
            >
              <a-input-password
                v-model:value="form.password"
                size="large"
                :placeholder="t('appLogin.passwordPlaceholder')"
              >
                <template #prefix><lock-outlined /></template>
              </a-input-password>
            </a-form-item>

            <a-button
              type="primary"
              html-type="submit"
              block
              size="large"
              :loading="submitting"
              class="login-submit-btn"
            >
              {{ t("auth.loginSubmit") }}
            </a-button>
          </a-form>

          <div class="login-footer">
            <span class="login-copyright">Atlas Security Platform</span>
          </div>
        </div>
      </main>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onMounted, onUnmounted, reactive, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useI18n } from "vue-i18n";
import {
  SafetyCertificateOutlined,
  AppstoreOutlined,
  TeamOutlined,
  BankOutlined,
  UserOutlined,
  LockOutlined
} from "@ant-design/icons-vue";
import { clearAuthStorage, getTenantId } from "@atlas/shared-core";
import { useAppUserStore } from "@/stores/user";
import LocaleSwitch from "@/components/layout/LocaleSwitch.vue";

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const userStore = useAppUserStore();
const submitting = ref(false);
const errorMessage = ref("");
const appKey = String(route.params.appKey ?? "");

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

const form = reactive({
  tenantId: getTenantId() ?? "",
  username: "",
  password: ""
});

function normalizeError(error: unknown): string {
  if (!(error instanceof Error)) return t("auth.loginFailed");
  const msg = error.message;
  if (msg.includes("ACCOUNT_LOCKED") || msg.includes("账户已锁定")) {
    return t("appLogin.accountLocked");
  }
  if (msg.includes("PASSWORD_EXPIRED") || msg.includes("密码已过期")) {
    return t("appLogin.passwordExpired");
  }
  if (msg.includes("请输入有效的租户")) {
    return t("appLogin.invalidTenantId");
  }
  return msg || t("auth.loginFailed");
}

async function handleSubmit() {
  submitting.value = true;
  errorMessage.value = "";
  try {
    clearAuthStorage();
    await userStore.login(form.tenantId.trim(), form.username.trim(), form.password);
    if (!isMounted.value) return;

    await userStore.getInfo();
    if (!isMounted.value) return;

    userStore.setAppKey(appKey);
    localStorage.setItem("atlas_app_last_appkey", appKey);

    const rawRedirect = route.query.redirect;
    const fallbackPath = `/apps/${encodeURIComponent(appKey)}/dashboard`;
    const targetPath =
      typeof rawRedirect === "string" && rawRedirect.startsWith("/apps/")
        ? rawRedirect
        : fallbackPath;

    await router.replace(targetPath);
  } catch (error) {
    if (!isMounted.value) return;
    errorMessage.value = normalizeError(error);
  } finally {
    submitting.value = false;
  }
}
</script>

<style scoped>
.login-wrapper {
  min-height: 100vh;
}

.login-split {
  display: flex;
  min-height: 100vh;
}

.login-split__left {
  flex: 0 0 420px;
  background: linear-gradient(135deg, #1677ff 0%, #0958d9 60%, #003eb3 100%);
  display: flex;
  align-items: center;
  justify-content: center;
  position: relative;
  overflow: hidden;
  padding: 48px 40px;
}

.tech-bg {
  position: absolute;
  inset: 0;
  pointer-events: none;
}

.bg-shape {
  position: absolute;
  border-radius: 50%;
  opacity: 0.08;
  background: #fff;
}

.shape-1 {
  width: 300px;
  height: 300px;
  top: -80px;
  left: -60px;
}

.shape-2 {
  width: 200px;
  height: 200px;
  bottom: 60px;
  right: -40px;
}

.shape-3 {
  width: 120px;
  height: 120px;
  bottom: -20px;
  left: 30%;
}

.login-left-content {
  position: relative;
  z-index: 1;
  color: #fff;
}

.login-logo {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 48px;
}

.logo-icon {
  width: 40px;
  height: 40px;
  flex-shrink: 0;
}

.logo-icon svg {
  width: 100%;
  height: 100%;
}

.logo-text {
  font-size: 20px;
  font-weight: 600;
  letter-spacing: 0.5px;
}

.login-slogan h2 {
  font-size: 28px;
  font-weight: 700;
  margin: 0 0 12px;
  line-height: 1.3;
  color: #fff;
}

.login-slogan p {
  font-size: 15px;
  opacity: 0.85;
  margin: 0 0 40px;
  line-height: 1.6;
}

.login-features {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.feature-item {
  display: flex;
  align-items: center;
  gap: 10px;
  font-size: 14px;
  opacity: 0.9;
}

.feature-icon {
  font-size: 18px;
  opacity: 0.85;
}

.login-split__right {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  background: #fafbfd;
  position: relative;
  padding: 48px 40px;
}

.locale-switch-wrapper {
  position: absolute;
  top: 20px;
  right: 24px;
  z-index: 10;
}

.login-card {
  width: min(400px, 100%);
}

.mobile-logo {
  display: none;
  align-items: center;
  gap: 8px;
  font-size: 18px;
  font-weight: 600;
  color: #1a1a2e;
  margin-bottom: 32px;
}

.logo-icon--sm {
  width: 28px;
  height: 28px;
}

.login-title {
  font-size: 22px;
  font-weight: 600;
  color: #1a1a2e;
  margin: 0 0 8px;
}

.login-app-badge {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 4px 12px;
  background: #f0f5ff;
  border: 1px solid #d6e4ff;
  border-radius: 6px;
  color: #1677ff;
  font-size: 13px;
  font-weight: 500;
  margin-bottom: 24px;
}

.login-error {
  margin-bottom: 16px;
}

.login-submit-btn {
  margin-top: 8px;
  height: 44px;
  font-size: 15px;
}

.login-footer {
  text-align: center;
  margin-top: 32px;
}

.login-copyright {
  color: #999;
  font-size: 12px;
}

@media (max-width: 900px) {
  .login-split__left {
    display: none;
  }

  .mobile-logo {
    display: flex;
  }

  .login-split__right {
    padding: 24px 20px;
  }
}
</style>
