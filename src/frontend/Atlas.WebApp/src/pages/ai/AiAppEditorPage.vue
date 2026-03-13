<template>
  <a-card :title="isCreate ? '新建 AI 应用' : `编辑 AI 应用 #${appId}`" :bordered="false">
    <template #extra>
      <a-space>
        <a-button @click="goBack">返回</a-button>
        <a-button type="primary" :loading="submitting" @click="submit">保存</a-button>
      </a-space>
    </template>

    <a-form ref="formRef" :model="form" layout="vertical" :rules="rules">
      <a-form-item label="名称" name="name">
        <a-input v-model:value="form.name" />
      </a-form-item>
      <a-form-item label="描述" name="description">
        <a-textarea v-model:value="form.description" :rows="3" />
      </a-form-item>
      <a-form-item label="图标" name="icon">
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
      message="提示：应用发布、版本检查与资源复制可在列表页“操作”列完成。"
    />
  </a-card>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import type { FormInstance } from "ant-design-vue";
import { message } from "ant-design-vue";
import { createAiApp, getAiAppById, updateAiApp } from "@/services/api-ai-app";

const route = useRoute();
const router = useRouter();
const appId = computed(() => Number(route.params.id));
const isCreate = computed(() => !Number.isFinite(appId.value) || appId.value <= 0);

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

const rules = {
  name: [{ required: true, message: "请输入应用名称" }]
};

function goBack() {
  void router.push("/ai/apps");
}

async function loadDetail() {
  if (isCreate.value) {
    return;
  }

  try {
    const detail = await getAiAppById(appId.value);
    Object.assign(form, {
      name: detail.name,
      description: detail.description ?? "",
      icon: detail.icon ?? "",
      agentId: detail.agentId,
      workflowId: detail.workflowId,
      promptTemplateId: detail.promptTemplateId
    });
  } catch (error: unknown) {
    message.error((error as Error).message || "加载应用详情失败");
  }
}

async function submit() {
  try {
    await formRef.value?.validate();
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
      const newId = await createAiApp(payload);
      message.success("创建成功");
      void router.replace(`/ai/apps/${newId}/edit`);
    } else {
      await updateAiApp(appId.value, payload);
      message.success("更新成功");
    }
  } catch (error: unknown) {
    message.error((error as Error).message || "保存失败");
  } finally {
    submitting.value = false;
  }
}

onMounted(() => {
  void loadDetail();
});
</script>
