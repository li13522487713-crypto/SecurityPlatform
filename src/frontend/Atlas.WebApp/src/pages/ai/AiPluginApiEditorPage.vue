<template>
  <a-card title="插件 API 编辑器" :bordered="false">
    <a-alert
      type="info"
      show-icon
      message="该页面可通过路由参数 pluginId/apiId 独立编辑插件接口，当前版本提供基础能力。"
      style="margin-bottom: 16px"
    />

    <a-form ref="formRef" :model="form" layout="vertical" :rules="rules">
      <a-form-item label="Plugin ID">
        <a-input-number v-model:value="pluginId" :min="1" style="width: 100%" />
      </a-form-item>
      <a-form-item label="API ID（新建可留空）">
        <a-input-number v-model:value="apiId" :min="1" style="width: 100%" />
      </a-form-item>
      <a-form-item label="名称" name="name">
        <a-input v-model:value="form.name" />
      </a-form-item>
      <a-form-item label="描述" name="description">
        <a-textarea v-model:value="form.description" :rows="3" />
      </a-form-item>
      <a-form-item label="Method" name="method">
        <a-select v-model:value="form.method" :options="methodOptions" />
      </a-form-item>
      <a-form-item label="Path" name="path">
        <a-input v-model:value="form.path" />
      </a-form-item>
      <a-form-item label="请求 Schema JSON">
        <a-textarea v-model:value="form.requestSchemaJson" :rows="5" />
      </a-form-item>
      <a-form-item label="响应 Schema JSON">
        <a-textarea v-model:value="form.responseSchemaJson" :rows="5" />
      </a-form-item>
      <a-form-item label="超时(秒)" name="timeoutSeconds">
        <a-input-number v-model:value="form.timeoutSeconds" :min="1" :max="600" style="width: 100%" />
      </a-form-item>
      <a-form-item>
        <a-space>
          <a-switch v-model:checked="form.isEnabled" />
          <span>启用状态</span>
        </a-space>
      </a-form-item>
      <a-form-item>
        <a-space>
          <a-button @click="loadFromServer">加载</a-button>
          <a-button type="primary" :loading="submitting" @click="submit">保存</a-button>
        </a-space>
      </a-form-item>
    </a-form>
  </a-card>
</template>

<script setup lang="ts">
import { reactive, ref } from "vue";
import type { FormInstance } from "ant-design-vue";
import { message } from "ant-design-vue";
import {
  createAiPluginApi,
  getAiPluginApis,
  updateAiPluginApi
} from "@/services/api-ai-plugin";

const pluginId = ref<number | undefined>(undefined);
const apiId = ref<number | undefined>(undefined);
const formRef = ref<FormInstance>();
const submitting = ref(false);

const form = reactive({
  name: "",
  description: "",
  method: "GET",
  path: "/",
  requestSchemaJson: "{}",
  responseSchemaJson: "{}",
  timeoutSeconds: 30,
  isEnabled: true
});

const rules = {
  name: [{ required: true, message: "请输入名称" }],
  method: [{ required: true, message: "请选择 Method" }],
  path: [{ required: true, message: "请输入 Path" }]
};

const methodOptions = ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"].map((x) => ({
  label: x,
  value: x
}));

async function loadFromServer() {
  if (!pluginId.value || !apiId.value) {
    message.warning("请先输入 Plugin ID 与 API ID");
    return;
  }

  try {
    const apis = await getAiPluginApis(pluginId.value);
    const target = apis.find((x) => x.id === apiId.value);
    if (!target) {
      message.warning("未找到指定接口");
      return;
    }

    Object.assign(form, {
      name: target.name,
      description: target.description ?? "",
      method: target.method,
      path: target.path,
      requestSchemaJson: target.requestSchemaJson,
      responseSchemaJson: target.responseSchemaJson,
      timeoutSeconds: target.timeoutSeconds,
      isEnabled: target.isEnabled
    });
  } catch (error: unknown) {
    message.error((error as Error).message || "加载接口失败");
  }
}

async function submit() {
  if (!pluginId.value) {
    message.warning("请先输入 Plugin ID");
    return;
  }

  try {
    await formRef.value?.validate();
  } catch {
    return;
  }

  submitting.value = true;
  try {
    if (apiId.value) {
      await updateAiPluginApi(pluginId.value, apiId.value, {
        name: form.name,
        description: form.description || undefined,
        method: form.method,
        path: form.path,
        requestSchemaJson: form.requestSchemaJson || undefined,
        responseSchemaJson: form.responseSchemaJson || undefined,
        timeoutSeconds: form.timeoutSeconds,
        isEnabled: form.isEnabled
      });
      message.success("更新成功");
    } else {
      const newId = await createAiPluginApi(pluginId.value, {
        name: form.name,
        description: form.description || undefined,
        method: form.method,
        path: form.path,
        requestSchemaJson: form.requestSchemaJson || undefined,
        responseSchemaJson: form.responseSchemaJson || undefined,
        timeoutSeconds: form.timeoutSeconds
      });
      apiId.value = newId;
      message.success("创建成功");
    }
  } catch (error: unknown) {
    message.error((error as Error).message || "保存失败");
  } finally {
    submitting.value = false;
  }
}
</script>
