<template>
  <a-card :title="t('adminAi.cardTitle')" :bordered="false">
    <a-form layout="vertical">
      <a-row :gutter="16">
        <a-col :span="12">
          <a-form-item :label="t('adminAi.enableAiPlatform')">
            <a-switch v-model:checked="form.enableAiPlatform" />
          </a-form-item>
        </a-col>
        <a-col :span="12">
          <a-form-item :label="t('adminAi.enableOpenPlatform')">
            <a-switch v-model:checked="form.enableOpenPlatform" />
          </a-form-item>
        </a-col>
        <a-col :span="12">
          <a-form-item :label="t('adminAi.enableSandbox')">
            <a-switch v-model:checked="form.enableCodeSandbox" />
          </a-form-item>
        </a-col>
        <a-col :span="12">
          <a-form-item :label="t('adminAi.enableMarket')">
            <a-switch v-model:checked="form.enableMarketplace" />
          </a-form-item>
        </a-col>
        <a-col :span="12">
          <a-form-item :label="t('adminAi.enableModeration')">
            <a-switch v-model:checked="form.enableContentModeration" />
          </a-form-item>
        </a-col>
        <a-col :span="12">
          <a-form-item :label="t('adminAi.dailyTokenLimit')">
            <a-input-number
              v-model:value="form.maxDailyTokensPerUser"
              :min="1000"
              :max="10000000"
              :step="1000"
              style="width: 100%"
            />
          </a-form-item>
        </a-col>
        <a-col :span="12">
          <a-form-item :label="t('adminAi.maxRecall')">
            <a-input-number
              v-model:value="form.maxKnowledgeRetrievalCount"
              :min="1"
              :max="100"
              style="width: 100%"
            />
          </a-form-item>
        </a-col>
      </a-row>
    </a-form>

    <a-space>
      <a-button @click="loadConfig">{{ t("common.reset") }}</a-button>
      <a-button type="primary" :loading="submitting" @click="submit">{{ t("adminAi.saveConfig") }}</a-button>
    </a-space>
  </a-card>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref, onUnmounted } from "vue";
import { useI18n } from "vue-i18n";

const isMounted = ref(false);
onMounted(() => {
  isMounted.value = true;
});
onUnmounted(() => {
  isMounted.value = false;
});

import { message } from "ant-design-vue";
import { getAdminAiConfig, updateAdminAiConfig } from "@/services/api-admin-ai-config";

const { t } = useI18n();

const form = reactive({
  enableAiPlatform: true,
  enableOpenPlatform: true,
  enableCodeSandbox: true,
  enableMarketplace: true,
  enableContentModeration: true,
  maxDailyTokensPerUser: 500000,
  maxKnowledgeRetrievalCount: 8
});
const submitting = ref(false);

async function loadConfig() {
  try {
    const config = await getAdminAiConfig();

    if (!isMounted.value) return;
    Object.assign(form, config);
  } catch (error: unknown) {
    message.error((error as Error).message || t("adminAi.loadFailed"));
  }
}

async function submit() {
  submitting.value = true;
  try {
    await updateAdminAiConfig({
      ...form
    });

    if (!isMounted.value) return;
    message.success(t("adminAi.saveOk"));
  } catch (error: unknown) {
    message.error((error as Error).message || t("adminAi.saveFailed"));
  } finally {
    submitting.value = false;
  }
}

onMounted(() => {
  void loadConfig();
});
</script>
