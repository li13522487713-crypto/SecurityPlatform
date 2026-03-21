<template>
  <a-card :title="pageTitle" :bordered="false">
    <template #extra>
      <a-space>
        <a-button @click="goBack">{{ t("ai.plugin.back") }}</a-button>
        <a-button type="primary" :loading="submitting" @click="submit">{{ t("common.save") }}</a-button>
      </a-space>
    </template>

    <a-form ref="formRef" :model="form" layout="vertical" :rules="rules">
      <a-form-item :label="t('ai.promptLib.colName')" name="name">
        <a-input v-model:value="form.name" />
      </a-form-item>
      <a-form-item :label="t('ai.promptLib.labelDescription')" name="description">
        <a-textarea v-model:value="form.description" :rows="3" />
      </a-form-item>
      <a-form-item :label="t('ai.plugin.labelIcon')" name="icon">
        <a-input v-model:value="form.icon" />
      </a-form-item>
      <a-form-item label="Agent ID" name="agentId">
        <a-input-number v-model:value="form.agentId" :min="1" style="width: 100%" />
      </a-form-item>
      <a-form-item label="Workflow ID" name="workflowId">
        <a-input-number v-model:value="form.workflowId" :min="1" style="width: 100%" />
      </a-form-item>
      <a-form-item label="PromptTemplate ID" name="promptTemplateId">
        <a-input-number v-model:value="form.promptTemplateId" :min="1" style="width: 100%" />
      </a-form-item>
    </a-form>

    <a-divider />
    <a-alert
      type="info"
      show-icon
      :message="t('ai.app.hintPublish')"
    />
  </a-card>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, onUnmounted } from "vue";
import { useI18n } from "vue-i18n";

const { t } = useI18n();

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { useRoute, useRouter } from "vue-router";
import type { FormInstance } from "ant-design-vue";
import { message } from "ant-design-vue";
import { createAiApp, getAiAppById, updateAiApp } from "@/services/api-ai-app";

const route = useRoute();
const router = useRouter();
const appId = computed(() => Number(route.params.id));
const isCreate = computed(() => !Number.isFinite(appId.value) || appId.value <= 0);

const pageTitle = computed(() =>
  isCreate.value ? t("ai.app.editorCreate") : t("ai.app.editorEdit", { id: appId.value })
);

const formRef = ref<FormInstance>();
const submitting = ref(false);
const form = reactive({
  name: "",
  description: "",
  icon: "",
  agentId: undefined as number | undefined,
  workflowId: undefined as number | undefined,
  promptTemplateId: undefined as number | undefined
});

const rules = computed(() => ({
  name: [{ required: true, message: t("ai.app.ruleName") }]
}));

function goBack() {
  void router.push("/ai/apps");
}

async function loadDetail() {
  if (isCreate.value) {
    return;
  }

  try {
    const detail  = await getAiAppById(appId.value);

    if (!isMounted.value) return;
    Object.assign(form, {
      name: detail.name,
      description: detail.description ?? "",
      icon: detail.icon ?? "",
      agentId: detail.agentId,
      workflowId: detail.workflowId,
      promptTemplateId: detail.promptTemplateId
    });
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.app.loadDetailFailed"));
  }
}

async function submit() {
  try {
    await formRef.value?.validate();

    if (!isMounted.value) return;
  } catch {
    return;
  }

  submitting.value = true;
  try {
    const payload = {
      name: form.name,
      description: form.description || undefined,
      icon: form.icon || undefined,
      agentId: form.agentId,
      workflowId: form.workflowId,
      promptTemplateId: form.promptTemplateId
    };

    if (isCreate.value) {
      const newId  = await createAiApp(payload);

      if (!isMounted.value) return;
      message.success(t("crud.createSuccess"));
      void router.replace(`/ai/apps/${newId}/edit`);
    } else {
      await updateAiApp(appId.value, payload);

      if (!isMounted.value) return;
      message.success(t("crud.updateSuccess"));
    }
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.app.saveFailed"));
  } finally {
    submitting.value = false;
  }
}

onMounted(() => {
  void loadDetail();
});
</script>
