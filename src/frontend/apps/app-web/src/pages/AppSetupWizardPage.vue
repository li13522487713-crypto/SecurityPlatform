<template>
  <div class="app-setup-container">
    <div class="app-setup-card">
      <h1 class="setup-title">{{ t("setup.appSetupTitle") }}</h1>
      <p class="setup-subtitle">{{ t("setup.appSetupSubtitle") }}</p>

      <div v-if="!completed && !setupError" class="setup-form">
        <a-form :label-col="{ span: 6 }" :wrapper-col="{ span: 16 }">
          <a-form-item :label="t('setup.appName')" required>
            <a-input v-model:value="form.appName" :placeholder="t('setup.appNamePlaceholder')" />
          </a-form-item>
          <a-form-item :label="t('setup.adminUsername')" required>
            <a-input v-model:value="form.adminUsername" :placeholder="t('setup.adminUsernamePlaceholder')" />
          </a-form-item>
        </a-form>

        <div class="form-actions">
          <a-button
            type="primary"
            size="large"
            :loading="initializing"
            :disabled="!formValid"
            @click="handleInitialize"
          >
            {{ initializing ? t("setup.initializing") : t("setup.startSetup") }}
          </a-button>
        </div>
      </div>

      <a-result
        v-if="completed"
        status="success"
        :title="t('setup.appSetupComplete')"
        :sub-title="t('setup.appSetupCompleteDesc')"
      >
        <template #extra>
          <a-button type="primary" size="large" @click="enterWorkspace">
            {{ t("setup.enterWorkspace") }}
          </a-button>
        </template>
      </a-result>

      <a-result
        v-if="setupError"
        status="error"
        :title="t('setup.appSetupFailed')"
        :sub-title="setupError"
      >
        <template #extra>
          <a-button type="primary" @click="setupError = null">{{ t("setup.retry") }}</a-button>
        </template>
      </a-result>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from "vue";
import { useI18n } from "vue-i18n";
import { initializeApp } from "@/services/api-setup";

const { t } = useI18n();

const form = ref({
  appName: "",
  adminUsername: "admin"
});

const initializing = ref(false);
const completed = ref(false);
const setupError = ref<string | null>(null);

const formValid = computed(
  () => form.value.appName.trim() !== "" && form.value.adminUsername.trim() !== "" && !initializing.value
);

async function handleInitialize() {
  initializing.value = true;
  setupError.value = null;
  try {
    const resp = await initializeApp({
      appName: form.value.appName,
      adminUsername: form.value.adminUsername
    });
    if (resp.success) {
      completed.value = true;
    } else {
      setupError.value = resp.message || t("setup.appSetupFailed");
    }
  } catch (e: unknown) {
    setupError.value = e instanceof Error ? e.message : String(e);
  } finally {
    initializing.value = false;
  }
}

function enterWorkspace() {
  window.location.href = "/";
}
</script>

<style scoped>
.app-setup-container {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: linear-gradient(135deg, #43e97b 0%, #38f9d7 100%);
  padding: 24px;
}

.app-setup-card {
  background: #fff;
  border-radius: 12px;
  padding: 48px;
  max-width: 640px;
  width: 100%;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.12);
}

.setup-title {
  text-align: center;
  font-size: 24px;
  font-weight: 600;
  margin-bottom: 8px;
  color: #1a1a2e;
}

.setup-subtitle {
  text-align: center;
  color: #666;
  margin-bottom: 32px;
}

.form-actions {
  display: flex;
  justify-content: center;
  margin-top: 24px;
}
</style>
