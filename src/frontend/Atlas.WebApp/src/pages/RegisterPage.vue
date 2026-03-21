<template>
  <div class="register-page">
    <aside class="brand-panel">
      <div class="brand-content">
        <div class="brand-logo">
          <div class="logo-icon">
            <svg viewBox="0 0 32 32" fill="none" xmlns="http://www.w3.org/2000/svg">
              <path
                d="M16 2L28 9v14l-12 7L4 23V9l12-7z"
                fill="rgba(255,255,255,0.15)"
                stroke="rgba(255,255,255,0.6)"
                stroke-width="1.5"
              />
              <path
                d="M16 8l7 4v8l-7 4-7-4v-8l7-4z"
                fill="rgba(255,255,255,0.25)"
                stroke="#fff"
                stroke-width="1.5"
              />
              <circle cx="16" cy="16" r="3" fill="#fff" />
            </svg>
          </div>
          <div class="brand-text">
            <h1>{{ t("pages.register.brandTitle") }}</h1>
            <p>Security Platform</p>
          </div>
        </div>
        <div class="brand-desc">
          <h2>{{ t("pages.register.brandSubtitle") }}</h2>
          <ul>
            <li>{{ t("pages.register.bullet1") }}</li>
            <li>{{ t("pages.register.bullet2") }}</li>
            <li>{{ t("pages.register.bullet3") }}</li>
          </ul>
        </div>
      </div>
      <div class="brand-footer">
        <span>{{ t("pages.register.complianceBadge") }}</span>
      </div>
      <div class="decor decor-1" aria-hidden="true"></div>
      <div class="decor decor-2" aria-hidden="true"></div>
      <div class="decor decor-3" aria-hidden="true"></div>
    </aside>

    <main class="form-panel">
      <div class="form-wrapper">
        <div class="mobile-logo">
          <div class="logo-icon logo-icon--sm">
            <svg viewBox="0 0 32 32" fill="none" xmlns="http://www.w3.org/2000/svg">
              <path
                d="M16 2L28 9v14l-12 7L4 23V9l12-7z"
                fill="rgba(22,119,255,0.1)"
                stroke="var(--color-primary)"
                stroke-width="1.5"
              />
              <path
                d="M16 8l7 4v8l-7 4-7-4v-8l7-4z"
                fill="rgba(22,119,255,0.15)"
                stroke="var(--color-primary)"
                stroke-width="1.5"
              />
              <circle cx="16" cy="16" r="3" fill="var(--color-primary)" />
            </svg>
          </div>
          <span>{{ t("pages.register.formBrand") }}</span>
        </div>

        <h3 class="form-title">{{ t("pages.register.formTitle") }}</h3>
        <p class="form-subtitle">{{ t("pages.register.formSubtitle") }}</p>

        <a-form layout="vertical" :model="form" :rules="rules" class="register-form" @finish="onSubmit">
          <a-form-item :label="t('pages.register.labelTenantId')" name="tenantId">
            <a-input v-model:value="form.tenantId" :placeholder="t('pages.register.phTenantId')" autocomplete="off" />
          </a-form-item>

          <a-form-item :label="t('pages.register.labelUsername')" name="username">
            <a-input
              v-model:value="form.username"
              :placeholder="t('pages.register.phUsername')"
              allow-clear
              autocomplete="username"
            />
          </a-form-item>

          <a-form-item :label="t('pages.register.labelPassword')" name="password">
            <a-input-password
              v-model:value="form.password"
              :placeholder="t('pages.register.phPassword')"
              autocomplete="new-password"
            />
          </a-form-item>

          <a-form-item :label="t('pages.register.labelConfirm')" name="confirmPassword">
            <a-input-password
              v-model:value="form.confirmPassword"
              :placeholder="t('pages.register.phConfirm')"
              autocomplete="new-password"
            />
          </a-form-item>

          <a-form-item :label="t('pages.register.labelCaptcha')" name="captchaCode">
            <a-input v-model:value="form.captchaCode" :placeholder="t('pages.register.phCaptcha')" />
          </a-form-item>

          <a-button type="primary" block html-type="submit" size="large" :loading="loading" class="submit-btn">
            {{ t("pages.register.submit") }}
          </a-button>

          <div class="login-link">
            {{ t("pages.register.hasAccount") }}<router-link to="/login">{{ t("pages.register.backLogin") }}</router-link>
          </div>
        </a-form>
      </div>

      <footer class="form-footer">
        <span>{{ t("pages.register.privacy") }}</span>
        <span class="sep">·</span>
        <span>{{ t("pages.register.terms") }}</span>
        <span class="sep">·</span>
        <span>v1.0.2</span>
      </footer>
    </main>
  </div>
</template>

<script setup lang="ts">
import { computed, reactive, ref } from "vue";
import { useI18n } from "vue-i18n";
import { message } from "ant-design-vue";
import { useRouter } from "vue-router";
import { register } from "@/services/api";
import type { RuleObject } from "ant-design-vue/es/form";

const { t } = useI18n();
const router = useRouter();
const loading = ref(false);
const form = reactive({
  tenantId: "00000000-0000-0000-0000-000000000001",
  username: "",
  password: "",
  confirmPassword: "",
  captchaKey: "",
  captchaCode: ""
});

const validateConfirmPassword = async (_rule: RuleObject, value: string) => {
  if (value === "") {
    return Promise.reject(t("pages.register.errConfirmRequired"));
  }
  if (value !== form.password) {
    return Promise.reject(t("pages.register.errPasswordMismatch"));
  }
  return Promise.resolve();
};

const rules = computed(() => ({
  tenantId: [
    { required: true, message: t("pages.register.errTenantRequired") },
    { pattern: /^[0-9a-fA-F-]{36}$/, message: t("pages.register.errTenantFormat") }
  ],
  username: [
    { required: true, message: t("pages.register.errUsernameRequired") },
    { min: 2, max: 64, message: t("pages.register.errUsernameLength") }
  ],
  password: [
    { required: true, message: t("pages.register.errPasswordRequired") },
    { min: 8, max: 128, message: t("pages.register.errPasswordLength") }
  ],
  confirmPassword: [{ required: true, validator: validateConfirmPassword }]
}));

async function onSubmit() {
  loading.value = true;
  try {
    await register(form.tenantId.trim(), {
      username: form.username.trim(),
      password: form.password,
      confirmPassword: form.confirmPassword,
      captchaKey: form.captchaKey || undefined,
      captchaCode: form.captchaCode || undefined
    });
    message.success(t("pages.register.success", { username: form.username }));
    router.push("/login");
  } catch (error) {
    message.error((error as Error).message || t("pages.register.failed"));
  } finally {
    loading.value = false;
  }
}
</script>

<style scoped>
.register-page {
  min-height: 100vh;
  display: flex;
  background: #fff;
}

.brand-panel {
  width: 440px;
  min-height: 100vh;
  background: linear-gradient(160deg, #0d47a1 0%, #1565c0 40%, #1e88e5 100%);
  color: #fff;
  display: flex;
  flex-direction: column;
  position: relative;
  overflow: hidden;
}

.brand-content {
  flex: 1;
  padding: 48px 40px;
  position: relative;
  z-index: 1;
}

.brand-logo {
  display: flex;
  align-items: center;
  gap: 16px;
  margin-bottom: 40px;
}

.logo-icon {
  width: 56px;
  height: 56px;
  flex-shrink: 0;
}

.logo-icon--sm {
  width: 40px;
  height: 40px;
}

.brand-text h1 {
  font-size: 24px;
  font-weight: 600;
  margin: 0;
  line-height: 1.3;
}

.brand-text p {
  margin: 4px 0 0;
  font-size: 13px;
  opacity: 0.85;
  letter-spacing: 0.5px;
}

.brand-desc h2 {
  font-size: 18px;
  font-weight: 500;
  margin: 0 0 20px;
  line-height: 1.5;
}

.brand-desc ul {
  margin: 0;
  padding-left: 20px;
  font-size: 14px;
  line-height: 2;
  opacity: 0.95;
}

.brand-footer {
  padding: 20px 40px;
  font-size: 12px;
  opacity: 0.75;
  position: relative;
  z-index: 1;
}

.decor {
  position: absolute;
  border-radius: 50%;
  background: rgba(255, 255, 255, 0.06);
  pointer-events: none;
}

.decor-1 {
  width: 280px;
  height: 280px;
  top: -80px;
  right: -100px;
}

.decor-2 {
  width: 160px;
  height: 160px;
  bottom: 120px;
  left: -40px;
}

.decor-3 {
  width: 100px;
  height: 100px;
  bottom: 40px;
  right: 40px;
}

.form-panel {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.form-wrapper {
  flex: 1;
  display: flex;
  flex-direction: column;
  justify-content: center;
  max-width: 400px;
  width: 100%;
  margin: 0 auto;
  padding: 48px 24px;
}

.mobile-logo {
  display: none;
  align-items: center;
  gap: 12px;
  margin-bottom: 24px;
  font-weight: 600;
  font-size: 18px;
  color: var(--color-text);
}

.form-title {
  font-size: 22px;
  font-weight: 600;
  margin: 0 0 8px;
  color: var(--color-text);
}

.form-subtitle {
  margin: 0 0 32px;
  color: var(--color-text-secondary);
  font-size: 14px;
}

.register-form :deep(.ant-form-item-label > label) {
  font-weight: 500;
}

.submit-btn {
  margin-top: 8px;
  height: 44px;
  font-size: 15px;
}

.login-link {
  margin-top: 24px;
  text-align: center;
  color: var(--color-text-secondary);
  font-size: 14px;
}

.login-link a {
  margin-left: 4px;
}

.form-footer {
  padding: 16px 24px;
  text-align: center;
  font-size: 12px;
  color: var(--color-text-tertiary);
}

.form-footer .sep {
  margin: 0 8px;
  opacity: 0.5;
}

@media (max-width: 960px) {
  .brand-panel {
    display: none;
  }

  .mobile-logo {
    display: flex;
  }
}
</style>
