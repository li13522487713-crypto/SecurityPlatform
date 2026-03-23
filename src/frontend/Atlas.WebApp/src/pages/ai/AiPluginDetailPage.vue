<template>
  <a-space direction="vertical" style="width: 100%" :size="16">
    <a-card :title="t('ai.plugin.detailTitle', { id: pluginId })" :bordered="false">
      <template #extra>
        <a-space>
          <a-button @click="goBack">{{ t("ai.plugin.back") }}</a-button>
          <a-button :loading="publishing" @click="handlePublish">{{ t("ai.workflow.publish") }}</a-button>
          <a-button :loading="locking" @click="toggleLock">
            {{ detail?.isLocked ? t("ai.plugin.unlock") : t("ai.plugin.lock") }}
          </a-button>
        </a-space>
      </template>

      <a-descriptions :column="2" bordered size="small">
        <a-descriptions-item :label="t('ai.promptLib.colName')">{{ detail?.name ?? "-" }}</a-descriptions-item>
        <a-descriptions-item :label="t('ai.promptLib.labelCategory')">{{ detail?.category ?? "-" }}</a-descriptions-item>
        <a-descriptions-item :label="t('ai.plugin.labelType')">
          {{ detail?.type === 1 ? t("ai.plugin.typeBuiltIn") : t("ai.plugin.typeCustom") }}
        </a-descriptions-item>
        <a-descriptions-item :label="t('ai.plugin.labelSourceType')">
          {{ formatSourceType(detail?.sourceType) }}
        </a-descriptions-item>
        <a-descriptions-item :label="t('ai.plugin.labelAuthType')">
          {{ formatAuthType(detail?.authType) }}
        </a-descriptions-item>
        <a-descriptions-item :label="t('ai.workflow.colStatus')">
          {{ detail?.status === 1 ? t("ai.plugin.statusPublished") : t("ai.plugin.statusDraft") }}
        </a-descriptions-item>
        <a-descriptions-item :label="t('ai.promptLib.labelDescription')" :span="2">
          {{ detail?.description ?? "-" }}
        </a-descriptions-item>
      </a-descriptions>

      <a-divider orientation="left">{{ t("ai.plugin.sectionDefinition") }}</a-divider>
      <pre class="json-block">{{ detail?.definitionJson }}</pre>
      <a-divider orientation="left">{{ t("ai.plugin.labelToolSchemaJson") }}</a-divider>
      <pre class="json-block">{{ detail?.toolSchemaJson }}</pre>
      <a-divider orientation="left">{{ t("ai.plugin.labelAuthConfigJson") }}</a-divider>
      <pre class="json-block">{{ detail?.authConfigJson }}</pre>
    </a-card>

    <a-card :title="t('ai.plugin.apiListTitle')" :bordered="false">
      <template #extra>
        <a-space>
          <a-button @click="openOpenApiImport">{{ t("ai.plugin.importOpenApi") }}</a-button>
          <a-button type="primary" @click="openCreateApi">{{ t("ai.plugin.newApi") }}</a-button>
        </a-space>
      </template>

      <a-table row-key="id" :columns="columns" :data-source="apis" :loading="apisLoading" :pagination="false">
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'method'">
            <a-tag color="blue">{{ record.method }}</a-tag>
          </template>
          <template v-if="column.key === 'enabled'">
            <a-tag :color="record.isEnabled ? 'green' : 'default'">
              {{ record.isEnabled ? t("common.statusEnabled") : t("common.statusDisabled") }}
            </a-tag>
          </template>
          <template v-if="column.key === 'action'">
            <a-space>
              <a-button type="link" @click="openEditApi(record)">{{ t("common.edit") }}</a-button>
              <a-button type="link" @click="openDebug(record.id)">{{ t("ai.plugin.debug") }}</a-button>
              <a-popconfirm :title="t('ai.plugin.deleteApiConfirm')" @confirm="handleDeleteApi(record.id)">
                <a-button type="link" danger>{{ t("common.delete") }}</a-button>
              </a-popconfirm>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <a-modal
      v-model:open="apiModalOpen"
      :title="editingApiId ? t('ai.plugin.modalApiEdit') : t('ai.plugin.modalApiCreate')"
      :confirm-loading="apiSubmitting"
      width="760px"
      @ok="submitApi"
      @cancel="closeApiModal"
    >
      <a-form ref="apiFormRef" :model="apiForm" layout="vertical" :rules="apiRules">
        <a-form-item :label="t('ai.promptLib.colName')" name="name">
          <a-input v-model:value="apiForm.name" />
        </a-form-item>
        <a-form-item :label="t('ai.promptLib.labelDescription')" name="description">
          <a-textarea v-model:value="apiForm.description" :rows="2" />
        </a-form-item>
        <a-form-item label="Method" name="method">
          <a-select v-model:value="apiForm.method" :options="methodOptions" />
        </a-form-item>
        <a-form-item label="Path" name="path">
          <a-input v-model:value="apiForm.path" />
        </a-form-item>
        <a-form-item :label="t('ai.plugin.labelRequestSchema')" name="requestSchemaJson">
          <a-textarea v-model:value="apiForm.requestSchemaJson" :rows="4" />
        </a-form-item>
        <a-form-item :label="t('ai.plugin.labelResponseSchema')" name="responseSchemaJson">
          <a-textarea v-model:value="apiForm.responseSchemaJson" :rows="4" />
        </a-form-item>
        <a-form-item :label="t('ai.plugin.labelTimeout')" name="timeoutSeconds">
          <a-input-number v-model:value="apiForm.timeoutSeconds" :min="1" :max="600" style="width: 100%" />
        </a-form-item>
        <a-form-item v-if="editingApiId" :label="t('ai.plugin.labelEnabledState')" name="isEnabled">
          <a-switch v-model:checked="apiForm.isEnabled" />
        </a-form-item>
      </a-form>
    </a-modal>

    <a-modal
      v-model:open="openApiModalOpen"
      :title="t('ai.plugin.modalImportOpenApi')"
      :confirm-loading="openApiSubmitting"
      width="760px"
      @ok="submitOpenApiImport"
      @cancel="openApiModalOpen = false"
    >
      <a-form layout="vertical">
        <a-form-item :label="t('ai.plugin.labelOpenApiJson')">
          <a-textarea v-model:value="openApiJson" :rows="10" />
        </a-form-item>
        <a-form-item>
          <a-checkbox v-model:checked="openApiOverwrite">{{ t("ai.plugin.overwriteApis") }}</a-checkbox>
        </a-form-item>
      </a-form>
    </a-modal>

    <a-modal
      v-model:open="debugModalOpen"
      :title="t('ai.plugin.modalDebugTitle')"
      :confirm-loading="debugSubmitting"
      width="720px"
      :ok-text="t('ai.plugin.debugOk')"
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
import { useI18n } from "vue-i18n";

const { t } = useI18n();

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

const columns = computed(() => [
  { title: t("ai.promptLib.colName"), dataIndex: "name", key: "name", width: 220 },
  { title: "Method", key: "method", width: 100 },
  { title: "Path", dataIndex: "path", key: "path" },
  { title: t("ai.workflow.colStatus"), key: "enabled", width: 100 },
  { title: t("ai.colActions"), key: "action", width: 200 }
]);

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
const apiRules = computed(() => ({
  name: [{ required: true, message: t("ai.plugin.ruleApiName") }],
  method: [{ required: true, message: t("ai.plugin.ruleMethod") }],
  path: [{ required: true, message: t("ai.plugin.rulePath") }]
}));
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

function formatSourceType(sourceType?: number) {
  if (sourceType === 1) {
    return t("ai.plugin.sourceOpenApi");
  }

  if (sourceType === 2) {
    return t("ai.plugin.sourceBuiltInCatalog");
  }

  return t("ai.plugin.sourceManual");
}

function formatAuthType(authType?: number) {
  switch (authType) {
    case 1:
      return "API Key";
    case 2:
      return "Bearer Token";
    case 3:
      return "Basic";
    case 4:
      return t("ai.plugin.authCustom");
    default:
      return t("ai.plugin.authNone");
  }
}

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
    message.error((error as Error).message || t("ai.plugin.loadDetailFailed"));
  }
}

async function handlePublish() {
  publishing.value = true;
  try {
    await publishAiPlugin(pluginId.value);

    if (!isMounted.value) return;
    message.success(t("ai.plugin.publishSuccess"));
    await loadDetail();

    if (!isMounted.value) return;
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.plugin.publishFailed"));
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
    message.success(t("ai.plugin.lockUpdateOk"));
    await loadDetail();

    if (!isMounted.value) return;
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.plugin.lockUpdateFailed"));
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
      message.success(t("ai.plugin.apiUpdateOk"));
    } else {
      await createAiPluginApi(pluginId.value, payload);

      if (!isMounted.value) return;
      message.success(t("ai.plugin.apiCreateOk"));
    }

    apiModalOpen.value = false;
    await loadDetail();

    if (!isMounted.value) return;
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.plugin.apiSubmitFailed"));
  } finally {
    apiSubmitting.value = false;
  }
}

async function handleDeleteApi(apiId: number) {
  try {
    await deleteAiPluginApi(pluginId.value, apiId);

    if (!isMounted.value) return;
    message.success(t("ai.plugin.apiDeleteOk"));
    await loadDetail();

    if (!isMounted.value) return;
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.plugin.apiDeleteFailed"));
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
    message.success(t("ai.plugin.importOk", { count: result.importedCount }));
    openApiModalOpen.value = false;
    await loadDetail();

    if (!isMounted.value) return;
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.plugin.importFailed"));
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
      ? t("ai.plugin.debugOkFmt", { ms: result.durationMs })
      : t("ai.plugin.debugFailFmt", { ms: result.durationMs });
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.plugin.debugFailed"));
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
