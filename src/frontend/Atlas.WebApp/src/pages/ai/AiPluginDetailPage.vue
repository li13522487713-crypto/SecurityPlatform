<template>
  <a-space direction="vertical" style="width: 100%" :size="16">
    <a-card :title="`插件详情 #${pluginId}`" :bordered="false">
      <template #extra>
        <a-space>
          <a-button @click="goBack">返回</a-button>
          <a-button :loading="publishing" @click="handlePublish">发布</a-button>
          <a-button :loading="locking" @click="toggleLock">
            {{ detail?.isLocked ? "解锁" : "锁定" }}
          </a-button>
        </a-space>
      </template>

      <a-descriptions :column="2" bordered size="small">
        <a-descriptions-item label="名称">{{ detail?.name ?? "-" }}</a-descriptions-item>
        <a-descriptions-item label="分类">{{ detail?.category ?? "-" }}</a-descriptions-item>
        <a-descriptions-item label="类型">
          {{ detail?.type === 1 ? "内置" : "自定义" }}
        </a-descriptions-item>
        <a-descriptions-item label="状态">
          {{ detail?.status === 1 ? "已发布" : "草稿" }}
        </a-descriptions-item>
        <a-descriptions-item label="描述" :span="2">
          {{ detail?.description ?? "-" }}
        </a-descriptions-item>
      </a-descriptions>

      <a-divider orientation="left">定义 JSON</a-divider>
      <pre class="json-block">{{ detail?.definitionJson }}</pre>
    </a-card>

    <a-card title="接口列表" :bordered="false">
      <template #extra>
        <a-space>
          <a-button @click="openOpenApiImport">导入 OpenAPI</a-button>
          <a-button type="primary" @click="openCreateApi">新增接口</a-button>
        </a-space>
      </template>

      <a-table row-key="id" :columns="columns" :data-source="apis" :loading="apisLoading" :pagination="false">
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'method'">
            <a-tag color="blue">{{ record.method }}</a-tag>
          </template>
          <template v-if="column.key === 'enabled'">
            <a-tag :color="record.isEnabled ? 'green' : 'default'">
              {{ record.isEnabled ? "启用" : "停用" }}
            </a-tag>
          </template>
          <template v-if="column.key === 'action'">
            <a-space>
              <a-button type="link" @click="openEditApi(record)">编辑</a-button>
              <a-button type="link" @click="openDebug(record.id)">调试</a-button>
              <a-popconfirm title="确认删除该接口？" @confirm="handleDeleteApi(record.id)">
                <a-button type="link" danger>删除</a-button>
              </a-popconfirm>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <a-modal
      v-model:open="apiModalOpen"
      :title="editingApiId ? '编辑接口' : '新增接口'"
      :confirm-loading="apiSubmitting"
      width="760px"
      @ok="submitApi"
      @cancel="closeApiModal"
    >
      <a-form ref="apiFormRef" :model="apiForm" layout="vertical" :rules="apiRules">
        <a-form-item label="名称" name="name">
          <a-input v-model:value="apiForm.name" />
        </a-form-item>
        <a-form-item label="描述" name="description">
          <a-textarea v-model:value="apiForm.description" :rows="2" />
        </a-form-item>
        <a-form-item label="Method" name="method">
          <a-select v-model:value="apiForm.method" :options="methodOptions" />
        </a-form-item>
        <a-form-item label="Path" name="path">
          <a-input v-model:value="apiForm.path" />
        </a-form-item>
        <a-form-item label="请求 Schema JSON" name="requestSchemaJson">
          <a-textarea v-model:value="apiForm.requestSchemaJson" :rows="4" />
        </a-form-item>
        <a-form-item label="响应 Schema JSON" name="responseSchemaJson">
          <a-textarea v-model:value="apiForm.responseSchemaJson" :rows="4" />
        </a-form-item>
        <a-form-item label="超时(秒)" name="timeoutSeconds">
          <a-input-number v-model:value="apiForm.timeoutSeconds" :min="1" :max="600" style="width: 100%" />
        </a-form-item>
        <a-form-item v-if="editingApiId" label="启用状态" name="isEnabled">
          <a-switch v-model:checked="apiForm.isEnabled" />
        </a-form-item>
      </a-form>
    </a-modal>

    <a-modal
      v-model:open="openApiModalOpen"
      title="导入 OpenAPI"
      :confirm-loading="openApiSubmitting"
      width="760px"
      @ok="submitOpenApiImport"
      @cancel="openApiModalOpen = false"
    >
      <a-form layout="vertical">
        <a-form-item label="OpenAPI JSON">
          <a-textarea v-model:value="openApiJson" :rows="10" />
        </a-form-item>
        <a-form-item>
          <a-checkbox v-model:checked="openApiOverwrite">覆盖已有接口</a-checkbox>
        </a-form-item>
      </a-form>
    </a-modal>

    <a-modal
      v-model:open="debugModalOpen"
      title="插件调试"
      :confirm-loading="debugSubmitting"
      width="720px"
      ok-text="执行调试"
      @ok="submitDebug"
      @cancel="debugModalOpen = false"
    >
      <PluginDebugPanel
        v-model:api-id="debugApiId"
        v-model:input-json="debugInputJson"
        :api-options="apiOptions"
        :output-json="debugOutputJson"
        :result-title="debugResultTitle"
      />
    </a-modal>
  </a-space>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, onUnmounted } from "vue";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { useRoute, useRouter } from "vue-router";
import type { FormInstance } from "ant-design-vue";
import { message } from "ant-design-vue";
import PluginDebugPanel from "@/components/ai/PluginDebugPanel.vue";
import {
  createAiPluginApi,
  debugAiPlugin,
  deleteAiPluginApi,
  getAiPluginById,
  importAiPluginOpenApi,
  publishAiPlugin,
  setAiPluginLock,
  updateAiPluginApi,
  type AiPluginApiItem,
  type AiPluginDetail
} from "@/services/api-ai-plugin";
import { resolveCurrentAppId } from "@/utils/app-context";

const route = useRoute();
const router = useRouter();
const pluginId = computed(() => Number(route.params.id));

const detail = ref<AiPluginDetail | null>(null);
const apis = ref<AiPluginApiItem[]>([]);
const apisLoading = ref(false);
const publishing = ref(false);
const locking = ref(false);

const columns = [
  { title: "名称", dataIndex: "name", key: "name", width: 220 },
  { title: "Method", key: "method", width: 100 },
  { title: "Path", dataIndex: "path", key: "path" },
  { title: "状态", key: "enabled", width: 100 },
  { title: "操作", key: "action", width: 200 }
];

const apiModalOpen = ref(false);
const apiSubmitting = ref(false);
const editingApiId = ref<number | null>(null);
const apiFormRef = ref<FormInstance>();
const apiForm = reactive({
  name: "",
  description: "",
  method: "GET",
  path: "/",
  requestSchemaJson: "{}",
  responseSchemaJson: "{}",
  timeoutSeconds: 30,
  isEnabled: true
});
const apiRules = {
  name: [{ required: true, message: "请输入接口名称" }],
  method: [{ required: true, message: "请选择 Method" }],
  path: [{ required: true, message: "请输入 Path" }]
};
const methodOptions = ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"].map((x) => ({
  label: x,
  value: x
}));

const openApiModalOpen = ref(false);
const openApiSubmitting = ref(false);
const openApiJson = ref('{"openapi":"3.0.0","paths":{"/ping":{"get":{"summary":"ping","responses":{"200":{"description":"ok"}}}}}}');
const openApiOverwrite = ref(true);

const debugModalOpen = ref(false);
const debugSubmitting = ref(false);
const debugApiId = ref<number | undefined>(undefined);
const debugInputJson = ref("{}");
const debugOutputJson = ref("");
const debugResultTitle = ref("");

const apiOptions = computed(() => apis.value.map((api) => ({ label: `${api.method} ${api.path}`, value: api.id })));

function goBack() {
  const currentAppId = resolveCurrentAppId(route);
  if (!currentAppId) {
    void router.push("/console/apps");
    return;
  }
  void router.push(`/apps/${currentAppId}/plugins`);
}

async function loadDetail() {
  try {
    detail.value = await getAiPluginById(pluginId.value);

    if (!isMounted.value) return;
    apis.value = detail.value.apis;
  } catch (error: unknown) {
    message.error((error as Error).message || "加载插件详情失败");
  }
}

async function handlePublish() {
  publishing.value = true;
  try {
    await publishAiPlugin(pluginId.value);

    if (!isMounted.value) return;
    message.success("发布成功");
    await loadDetail();

    if (!isMounted.value) return;
  } catch (error: unknown) {
    message.error((error as Error).message || "发布失败");
  } finally {
    publishing.value = false;
  }
}

async function toggleLock() {
  if (!detail.value) {
    return;
  }

  locking.value = true;
  try {
    await setAiPluginLock(pluginId.value, !detail.value.isLocked);

    if (!isMounted.value) return;
    message.success("锁状态更新成功");
    await loadDetail();

    if (!isMounted.value) return;
  } catch (error: unknown) {
    message.error((error as Error).message || "更新锁状态失败");
  } finally {
    locking.value = false;
  }
}

function openCreateApi() {
  editingApiId.value = null;
  Object.assign(apiForm, {
    name: "",
    description: "",
    method: "GET",
    path: "/",
    requestSchemaJson: "{}",
    responseSchemaJson: "{}",
    timeoutSeconds: 30,
    isEnabled: true
  });
  apiModalOpen.value = true;
}

function openEditApi(api: AiPluginApiItem) {
  editingApiId.value = api.id;
  Object.assign(apiForm, {
    name: api.name,
    description: api.description ?? "",
    method: api.method,
    path: api.path,
    requestSchemaJson: api.requestSchemaJson,
    responseSchemaJson: api.responseSchemaJson,
    timeoutSeconds: api.timeoutSeconds,
    isEnabled: api.isEnabled
  });
  apiModalOpen.value = true;
}

function closeApiModal() {
  apiModalOpen.value = false;
  apiFormRef.value?.resetFields();
}

async function submitApi() {
  try {
    await apiFormRef.value?.validate();

    if (!isMounted.value) return;
  } catch {
    return;
  }

  apiSubmitting.value = true;
  try {
    const payload = {
      name: apiForm.name,
      description: apiForm.description || undefined,
      method: apiForm.method,
      path: apiForm.path,
      requestSchemaJson: apiForm.requestSchemaJson || undefined,
      responseSchemaJson: apiForm.responseSchemaJson || undefined,
      timeoutSeconds: apiForm.timeoutSeconds
    };
    if (editingApiId.value) {
      await updateAiPluginApi(pluginId.value, editingApiId.value, {
        ...payload,
        isEnabled: apiForm.isEnabled
      });

      if (!isMounted.value) return;
      message.success("接口更新成功");
    } else {
      await createAiPluginApi(pluginId.value, payload);

      if (!isMounted.value) return;
      message.success("接口创建成功");
    }

    apiModalOpen.value = false;
    await loadDetail();

    if (!isMounted.value) return;
  } catch (error: unknown) {
    message.error((error as Error).message || "提交接口失败");
  } finally {
    apiSubmitting.value = false;
  }
}

async function handleDeleteApi(apiId: number) {
  try {
    await deleteAiPluginApi(pluginId.value, apiId);

    if (!isMounted.value) return;
    message.success("删除接口成功");
    await loadDetail();

    if (!isMounted.value) return;
  } catch (error: unknown) {
    message.error((error as Error).message || "删除接口失败");
  }
}

function openOpenApiImport() {
  openApiModalOpen.value = true;
}

async function submitOpenApiImport() {
  openApiSubmitting.value = true;
  try {
    const result  = await importAiPluginOpenApi(pluginId.value, {
      openApiJson: openApiJson.value,
      overwrite: openApiOverwrite.value
    });

    if (!isMounted.value) return;
    message.success(`导入成功，共 ${result.importedCount} 个接口`);
    openApiModalOpen.value = false;
    await loadDetail();

    if (!isMounted.value) return;
  } catch (error: unknown) {
    message.error((error as Error).message || "导入 OpenAPI 失败");
  } finally {
    openApiSubmitting.value = false;
  }
}

function openDebug(apiId?: number) {
  debugApiId.value = apiId;
  debugInputJson.value = "{}";
  debugOutputJson.value = "";
  debugResultTitle.value = "";
  debugModalOpen.value = true;
}

async function submitDebug() {
  debugSubmitting.value = true;
  try {
    const result  = await debugAiPlugin(pluginId.value, {
      apiId: debugApiId.value,
      inputJson: debugInputJson.value
    });

    if (!isMounted.value) return;
    debugOutputJson.value = result.outputJson;
    debugResultTitle.value = result.success
      ? `调试成功（${result.durationMs}ms）`
      : `调试失败（${result.durationMs}ms）`;
  } catch (error: unknown) {
    message.error((error as Error).message || "插件调试失败");
  } finally {
    debugSubmitting.value = false;
  }
}

onMounted(() => {
  void loadDetail();
});
</script>

<style scoped>
.json-block {
  margin: 0;
  padding: 12px;
  border-radius: 8px;
  background: #fafafa;
  max-height: 260px;
  overflow: auto;
  font-size: 12px;
  white-space: pre-wrap;
  word-break: break-all;
}
</style>
