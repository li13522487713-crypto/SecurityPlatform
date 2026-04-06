<template>
  <div class="app-login">
    <div class="locale-switch-wrapper">
      <LocaleSwitch />
    </div>
    <a-card class="app-login-card" :title="`${t('appLogin.title')} · ${appKey}`">
      <a-alert
        v-if="errorMessage"
        type="error"
        show-icon
        :message="errorMessage"
        class="app-login-error"
      />
      <a-form layout="vertical" :model="form" @finish="handleSubmit">
        <a-form-item
          :label="t('auth.tenantId')"
          name="tenantId"
          :rules="[{ required: true, message: t('appLogin.tenantIdRequired') }]"
        >
          <a-input
            v-model:value="form.tenantId"
            :placeholder="t('appLogin.tenantIdPlaceholder')"
          />
        </a-form-item>
        <a-form-item
          :label="t('auth.username')"
          name="username"
          :rules="[{ required: true, message: t('appLogin.usernameRequired') }]"
        >
          <a-input
            v-model:value="form.username"
            :placeholder="t('appLogin.usernamePlaceholder')"
          />
        </a-form-item>
        <a-form-item
          :label="t('auth.password')"
          name="password"
          :rules="[{ required: true, message: t('appLogin.passwordRequired') }]"
        >
          <a-input-password
            v-model:value="form.password"
            :placeholder="t('appLogin.passwordPlaceholder')"
          />
        </a-form-item>
        <a-button type="primary" html-type="submit" block :loading="submitting">
          {{ t("auth.loginSubmit") }}
        </a-button>
      </a-form>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useI18n } from "vue-i18n";
import { getTenantId } from "@atlas/shared-core";
import { useAppUserStore } from "@/stores/user";
import LocaleSwitch from "@/components/layout/LocaleSwitch.vue";

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const userStore = useAppUserStore();
const submitting = ref(false);
const errorMessage = ref("");
const appKey = String(route.params.appKey ?? "");

const form = reactive({
  tenantId: getTenantId() ?? "",
  username: "",
  password: ""
});

async function handleSubmit() {
  submitting.value = true;
  errorMessage.value = "";
  try {
    await userStore.login(form.tenantId.trim(), form.username.trim(), form.password);
    userStore.setAppKey(appKey);

    const rawRedirect = route.query.redirect;
    const redirect =
      typeof rawRedirect === "string" && rawRedirect.startsWith("/")
        ? rawRedirect
        : `/apps/${encodeURIComponent(appKey)}/dashboard`;
    await router.replace(redirect);
  } catch (error) {
    errorMessage.value =
      error instanceof Error ? error.message : t("auth.loginFailed");
  } finally {
    submitting.value = false;
  }
}
</script>

<style scoped>
.app-login {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(180deg, #f5f7fb 0%, #e8edf7 100%);
  padding: 24px;
  position: relative;
}

.locale-switch-wrapper {
  position: absolute;
  top: 20px;
  right: 20px;
  z-index: 10;
}

.app-login-card {
  width: min(420px, 100%);
}

.app-login-error {
  margin-bottom: 16px;
}
</style>
