<template>
  <div class="not-ready-container">
    <a-result status="warning" :title="t('setup.platformNotReady')" :sub-title="t('setup.platformNotReadyDesc')">
      <template #extra>
        <a-space>
          <a-button type="primary" @click="goToAppSetup">
            {{ t("setup.goToAppSetup") }}
          </a-button>
          <a-button :loading="checking" @click="handleRetry">
            {{ t("setup.retry") }}
          </a-button>
        </a-space>
      </template>
    </a-result>
  </div>
</template>

<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import { getSetupState } from "@/services/api-setup";

const { t } = useI18n();
const checking = ref(false);
let retryTimer: number | null = null;

onMounted(() => {
  scheduleAutoRetry();
});

onBeforeUnmount(() => {
  clearRetryTimer();
});

async function handleRetry() {
  clearRetryTimer();
  checking.value = true;
  try {
    const resp = await getSetupState();
    if (resp.success && resp.data?.platformStatus === "Ready") {
      window.location.reload();
      return;
    }
  } finally {
    checking.value = false;
    scheduleAutoRetry();
  }
}

function scheduleAutoRetry() {
  if (retryTimer !== null) {
    return;
  }

  retryTimer = window.setTimeout(async () => {
    retryTimer = null;
    await handleRetry();
  }, 2000);
}

function clearRetryTimer() {
  if (retryTimer !== null) {
    window.clearTimeout(retryTimer);
    retryTimer = null;
  }
}

function goToAppSetup() {
  window.location.href = "/app-setup";
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
