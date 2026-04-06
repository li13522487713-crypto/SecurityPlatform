<template>
  <div class="not-ready-container">
    <a-result status="warning" :title="t('setup.platformNotReady')" :sub-title="t('setup.platformNotReadyDesc')">
      <template #extra>
        <a-button type="primary" :loading="checking" @click="handleRetry">
          {{ t("setup.retry") }}
        </a-button>
      </template>
    </a-result>
  </div>
</template>

<script setup lang="ts">
import { ref } from "vue";
import { useI18n } from "vue-i18n";
import { getSetupState } from "@/services/api-setup";

const { t } = useI18n();
const checking = ref(false);

async function handleRetry() {
  checking.value = true;
  try {
    const resp = await getSetupState();
    if (resp.success && resp.data?.status === "Ready") {
      window.location.reload();
    }
  } finally {
    checking.value = false;
  }
}
</script>

<style scoped>
.not-ready-container {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background: #f5f5f5;
}
</style>
