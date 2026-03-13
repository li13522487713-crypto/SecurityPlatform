<template>
  <a-card title="AI 管理配置" :bordered="false">
    <a-form layout="vertical">
      <a-row :gutter="16">
        <a-col :span="12">
          <a-form-item label="启用 AI 平台">
            <a-switch v-model:checked="form.enableAiPlatform" />
          </a-form-item>
        </a-col>
        <a-col :span="12">
          <a-form-item label="启用开放平台">
            <a-switch v-model:checked="form.enableOpenPlatform" />
          </a-form-item>
        </a-col>
        <a-col :span="12">
          <a-form-item label="启用代码沙箱">
            <a-switch v-model:checked="form.enableCodeSandbox" />
          </a-form-item>
        </a-col>
        <a-col :span="12">
          <a-form-item label="启用 AI 市场">
            <a-switch v-model:checked="form.enableMarketplace" />
          </a-form-item>
        </a-col>
        <a-col :span="12">
          <a-form-item label="启用内容审核">
            <a-switch v-model:checked="form.enableContentModeration" />
          </a-form-item>
        </a-col>
        <a-col :span="12">
          <a-form-item label="单用户每日 Token 上限">
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
          <a-form-item label="知识检索最大召回数">
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
      <a-button @click="loadConfig">重置</a-button>
      <a-button type="primary" :loading="submitting" @click="submit">保存配置</a-button>
    </a-space>
  </a-card>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from "vue";
import { message } from "ant-design-vue";
import { getAdminAiConfig, updateAdminAiConfig } from "@/services/api-admin-ai-config";

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
    Object.assign(form, config);
  } catch (error: unknown) {
    message.error((error as Error).message || "加载配置失败");
  }
}

async function submit() {
  submitting.value = true;
  try {
    await updateAdminAiConfig({
      ...form
    });
    message.success("配置已保存");
  } catch (error: unknown) {
    message.error((error as Error).message || "保存配置失败");
  } finally {
    submitting.value = false;
  }
}

onMounted(() => {
  void loadConfig();
});
</script>
