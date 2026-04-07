<template>
  <div class="app-home-gateway">
    <a-spin :spinning="resolving" :tip="t('home.resolving')">
      <div v-if="!resolving" class="gateway-content">
        <div class="brand-header">
          <div class="brand-icon">
            <svg viewBox="0 0 32 32" fill="none" xmlns="http://www.w3.org/2000/svg">
              <path d="M16 2L28 9v14l-12 7L4 23V9l12-7z" fill="rgba(22,119,255,0.08)" stroke="#1677ff" stroke-width="1.5" />
              <path d="M16 8l7 4v8l-7 4-7-4v-8l7-4z" fill="rgba(22,119,255,0.12)" stroke="#1677ff" stroke-width="1.5" />
              <circle cx="16" cy="16" r="3" fill="#1677ff" />
            </svg>
          </div>
          <h1 class="brand-title">Atlas AppWeb</h1>
          <p class="brand-subtitle">{{ t("home.subtitle") }}</p>
        </div>

        <a-form v-if="!hasToken" layout="vertical" class="appkey-form" @finish="enterApp">
          <a-form-item :label="t('home.appKeyLabel')">
            <a-input
              v-model:value="inputAppKey"
              size="large"
              :placeholder="t('home.appKeyPlaceholder')"
              @press-enter="enterApp"
            />
          </a-form-item>
          <a-button type="primary" size="large" block @click="enterApp" :disabled="!inputAppKey.trim()">
            {{ t("home.enterApp") }}
          </a-button>
        </a-form>
      </div>
    </a-spin>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from "vue";
import { useRouter } from "vue-router";
import { useI18n } from "vue-i18n";
import { getAccessToken } from "@atlas/shared-core";
import { useAppUserStore } from "@/stores/user";
import { getSetupState } from "@/services/api-setup";

const { t } = useI18n();
const router = useRouter();
const userStore = useAppUserStore();

const resolving = ref(true);
const hasToken = ref(false);
const inputAppKey = ref("");

const APP_KEY_STORAGE = "atlas_app_last_appkey";

function getStoredAppKey(): string {
  return localStorage.getItem(APP_KEY_STORAGE) ?? "";
}

function enterApp() {
  const appKey = inputAppKey.value.trim();
  if (!appKey) return;
  localStorage.setItem(APP_KEY_STORAGE, appKey);
  void router.push(`/apps/${encodeURIComponent(appKey)}/login`);
}

onMounted(async () => {
  const token = getAccessToken();
  hasToken.value = Boolean(token);

  let configuredAppKey = "";
  try {
    const resp = await getSetupState();
    if (resp.success && resp.data?.appKey) {
      configuredAppKey = resp.data.appKey;
      localStorage.setItem(APP_KEY_STORAGE, configuredAppKey);
    }
  } catch {
    // setup state 不可用时回退到 localStorage
  }

  const effectiveAppKey =
    (token ? userStore.appKey : "") || configuredAppKey || getStoredAppKey();

  if (effectiveAppKey) {
    if (token) {
      void router.replace(
        `/apps/${encodeURIComponent(effectiveAppKey)}/dashboard`
      );
    } else {
      void router.replace(
        `/apps/${encodeURIComponent(effectiveAppKey)}/login`
      );
    }
    return;
  }

  resolving.value = false;
});
</script>

<style scoped>
.app-home-gateway {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(180deg, #f5f7fb 0%, #e8edf7 100%);
  padding: 24px;
}

.gateway-content {
  text-align: center;
  max-width: 420px;
  width: 100%;
}

.brand-header {
  margin-bottom: 40px;
}

.brand-icon {
  width: 64px;
  height: 64px;
  margin: 0 auto 16px;
}

.brand-icon svg {
  width: 100%;
  height: 100%;
}

.brand-title {
  font-size: 28px;
  font-weight: 600;
  color: #1a1a2e;
  margin: 0 0 8px;
}

.brand-subtitle {
  color: #666;
  font-size: 15px;
  margin: 0;
}

.appkey-form {
  text-align: left;
  background: #fff;
  border-radius: 12px;
  padding: 32px;
  box-shadow: 0 4px 24px rgba(0, 0, 0, 0.08);
}
</style>
