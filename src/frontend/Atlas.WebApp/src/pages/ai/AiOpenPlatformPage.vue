<template>
  <a-space direction="vertical" style="width: 100%" :size="16">
    <a-card :title="t('ai.openPlatform.portal.title')" :bordered="false">
      <a-tabs v-model:active-key="activeTab">
        <a-tab-pane key="apps" :tab="t('ai.openPlatform.portal.tabs.apps')">
          <a-space style="margin-bottom: 12px">
            <a-input v-model:value="projectKeyword" style="width: 260px" :placeholder="t('ai.openPlatform.portal.apps.searchPlaceholder')" />
            <a-button @click="loadProjects">{{ t("common.search") }}</a-button>
            <a-button @click="openCreateProjectModal">{{ t("ai.openPlatform.portal.apps.create") }}</a-button>
          </a-space>

          <a-table
            row-key="id"
            :loading="projectsLoading"
            :columns="projectColumns"
            :data-source="projectRows"
            :pagination="projectPagination"
            @change="handleProjectTableChange"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'scopes'">
                <a-space wrap>
                  <a-tag v-for="scope in record.scopes" :key="scope">{{ scope }}</a-tag>
                </a-space>
              </template>
              <template v-else-if="column.key === 'status'">
                <a-tag :color="record.isActive ? 'green' : 'default'">
                  {{ record.isActive ? t("common.enabled") : t("common.disabled") }}
                </a-tag>
              </template>
              <template v-else-if="column.key === 'lastUsedAt'">
                {{ formatDateTime(record.lastUsedAt) }}
              </template>
              <template v-else-if="column.key === 'actions'">
                <a-space>
                  <a-button type="link" @click="exchangeToken(record)">
                    {{ t("ai.openPlatform.portal.apps.exchangeToken") }}
                  </a-button>
                  <a-button type="link" @click="rotateSecret(record)">
                    {{ t("ai.openPlatform.portal.apps.rotateSecret") }}
                  </a-button>
                  <a-button type="link" danger @click="deleteProject(record)">
                    {{ t("common.delete") }}
                  </a-button>
                </a-space>
              </template>
            </template>
          </a-table>
        </a-tab-pane>

        <a-tab-pane key="stats" :tab="t('ai.openPlatform.portal.tabs.stats')">
          <a-space wrap style="margin-bottom: 12px">
            <a-select
              v-model:value="statsProjectId"
              style="width: 320px"
              :options="projectSelectOptions"
              :placeholder="t('ai.openPlatform.portal.stats.projectPlaceholder')"
              allow-clear
              show-search
            />
            <a-button type="primary" :loading="statsLoading" @click="loadStats">
              {{ t("common.search") }}
            </a-button>
          </a-space>

          <a-descriptions bordered :column="2" size="small">
            <a-descriptions-item :label="t('ai.openPlatform.portal.stats.totalCalls')">{{ statsSummary.totalCalls }}</a-descriptions-item>
            <a-descriptions-item :label="t('ai.openPlatform.portal.stats.successCalls')">{{ statsSummary.successCalls }}</a-descriptions-item>
            <a-descriptions-item :label="t('ai.openPlatform.portal.stats.failedCalls')">{{ statsSummary.failedCalls }}</a-descriptions-item>
            <a-descriptions-item :label="t('ai.openPlatform.portal.stats.successRate')">{{ (statsSummary.successRate * 100).toFixed(2) }}%</a-descriptions-item>
            <a-descriptions-item :label="t('ai.openPlatform.portal.stats.avgDuration')">{{ statsSummary.averageDurationMs.toFixed(2) }} ms</a-descriptions-item>
            <a-descriptions-item :label="t('ai.openPlatform.portal.stats.maxDuration')">{{ statsSummary.maxDurationMs }} ms</a-descriptions-item>
          </a-descriptions>
        </a-tab-pane>

        <a-tab-pane key="webhooks" :tab="t('ai.openPlatform.portal.tabs.webhooks')">
          <a-space style="margin-bottom: 12px">
            <a-button @click="loadWebhooks">{{ t("common.refresh") }}</a-button>
            <a-button type="primary" @click="openCreateWebhookModal">{{ t("ai.openPlatform.portal.webhooks.create") }}</a-button>
          </a-space>

          <a-table row-key="id" :loading="webhooksLoading" :columns="webhookColumns" :data-source="webhookRows" :pagination="false">
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'eventTypes'">
                <a-space wrap>
                  <a-tag v-for="eventType in parseEventTypes(record.eventTypes)" :key="eventType">{{ eventType }}</a-tag>
                </a-space>
              </template>
              <template v-else-if="column.key === 'lastTriggeredAt'">
                {{ formatDateTime(record.lastTriggeredAt) }}
              </template>
              <template v-else-if="column.key === 'actions'">
                <a-space>
                  <a-button type="link" @click="testWebhook(record)">
                    {{ t("ai.openPlatform.portal.webhooks.test") }}
                  </a-button>
                  <a-button type="link" @click="showDeliveries(record)">
                    {{ t("ai.openPlatform.portal.webhooks.deliveries") }}
                  </a-button>
                  <a-button type="link" danger @click="deleteWebhook(record)">
                    {{ t("common.delete") }}
                  </a-button>
                </a-space>
              </template>
            </template>
          </a-table>
        </a-tab-pane>

        <a-tab-pane key="sdk" :tab="t('ai.openPlatform.portal.tabs.sdk')">
          <a-space>
            <a-button @click="downloadSpec">{{ t("ai.openPlatform.portal.sdk.openapi") }}</a-button>
            <a-button @click="downloadSdk('typescript')">{{ t("ai.openPlatform.portal.sdk.typescript") }}</a-button>
            <a-button @click="downloadSdk('csharp')">{{ t("ai.openPlatform.portal.sdk.csharp") }}</a-button>
          </a-space>
        </a-tab-pane>
      </a-tabs>
    </a-card>
  </a-space>

  <a-modal
    v-model:open="projectCreateVisible"
    :title="t('ai.openPlatform.portal.apps.create')"
    :confirm-loading="projectCreateLoading"
    @ok="submitProjectCreate"
  >
    <a-form layout="vertical">
      <a-form-item :label="t('ai.openPlatform.portal.apps.name')">
        <a-input v-model:value="projectCreateForm.name" />
      </a-form-item>
      <a-form-item :label="t('ai.openPlatform.portal.apps.description')">
        <a-textarea v-model:value="projectCreateForm.description" :rows="3" />
      </a-form-item>
      <a-form-item :label="t('ai.openPlatform.portal.apps.scopes')">
        <a-input v-model:value="projectCreateForm.scopesInput" :placeholder="t('ai.openPlatform.portal.apps.scopesPlaceholder')" />
      </a-form-item>
    </a-form>
  </a-modal>

  <a-modal v-model:open="tokenModalVisible" :title="t('ai.openPlatform.portal.apps.tokenResult')" :footer="null">
    <a-typography-paragraph copyable :content="tokenModalText" />
  </a-modal>

  <a-modal
    v-model:open="webhookCreateVisible"
    :title="t('ai.openPlatform.portal.webhooks.create')"
    :confirm-loading="webhookCreateLoading"
    @ok="submitWebhookCreate"
  >
    <a-form layout="vertical">
      <a-form-item :label="t('ai.openPlatform.portal.webhooks.name')">
        <a-input v-model:value="webhookCreateForm.name" />
      </a-form-item>
      <a-form-item :label="t('ai.openPlatform.portal.webhooks.targetUrl')">
        <a-input v-model:value="webhookCreateForm.targetUrl" />
      </a-form-item>
      <a-form-item :label="t('ai.openPlatform.portal.webhooks.secret')">
        <a-input-password v-model:value="webhookCreateForm.secret" />
      </a-form-item>
      <a-form-item :label="t('ai.openPlatform.portal.webhooks.events')">
        <a-select v-model:value="webhookCreateForm.eventTypes" mode="multiple" :options="webhookEventOptions" />
      </a-form-item>
    </a-form>
  </a-modal>

  <a-drawer v-model:open="deliveryDrawerVisible" :title="t('ai.openPlatform.portal.webhooks.deliveries')" width="720">
    <a-table row-key="id" :columns="deliveryColumns" :data-source="deliveryRows" :pagination="false">
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'success'">
          <a-tag :color="record.success ? 'green' : 'red'">{{ record.success ? t("common.success") : t("common.failed") }}</a-tag>
        </template>
        <template v-else-if="column.key === 'createdAt'">
          {{ formatDateTime(record.createdAt) }}
        </template>
      </template>
    </a-table>
  </a-drawer>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from "vue";
import { useI18n } from "vue-i18n";
import { message } from "ant-design-vue";
import type { TablePaginationConfig } from "ant-design-vue";
import type { OpenApiCallStatsSummary, OpenApiProjectListItem, WebhookDeliveryLog, WebhookSubscription } from "@/services/api-open-platform";
import {
  createOpenApiProject,
  createOpenApiWebhook,
  deleteOpenApiProject,
  deleteOpenApiWebhook,
  downloadOpenApiSdk,
  downloadOpenApiSpec,
  exchangeOpenApiProjectToken,
  getOpenApiProjectsPaged,
  getOpenApiStatsSummary,
  getOpenApiWebhookDeliveries,
  listOpenApiWebhooks,
  rotateOpenApiProjectSecret,
  testOpenApiWebhook
} from "@/services/api-open-platform";

const { t } = useI18n();

const activeTab = ref("apps");
const projectKeyword = ref("");
const projectRows = ref<OpenApiProjectListItem[]>([]);
const projectsLoading = ref(false);
const projectPagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 10,
  total: 0
});

const statsProjectId = ref<number | undefined>(undefined);
const statsLoading = ref(false);
const statsSummary = reactive<OpenApiCallStatsSummary>({
  totalCalls: 0,
  successCalls: 0,
  failedCalls: 0,
  successRate: 0,
  averageDurationMs: 0,
  maxDurationMs: 0
});

const webhookRows = ref<WebhookSubscription[]>([]);
const webhooksLoading = ref(false);
const deliveryRows = ref<WebhookDeliveryLog[]>([]);
const deliveryDrawerVisible = ref(false);

const projectCreateVisible = ref(false);
const projectCreateLoading = ref(false);
const projectCreateForm = reactive({
  name: "",
  description: "",
  scopesInput: "open:*"
});

const tokenModalVisible = ref(false);
const tokenModalText = ref("");

const webhookCreateVisible = ref(false);
const webhookCreateLoading = ref(false);
const webhookCreateForm = reactive({
  name: "",
  targetUrl: "",
  secret: "",
  eventTypes: ["workflow.completed", "agent.message"]
});

const webhookEventOptions = [
  { label: "workflow.completed", value: "workflow.completed" },
  { label: "agent.message", value: "agent.message" }
];

const projectSelectOptions = computed(() =>
  projectRows.value.map(item => ({
    label: `${item.name} (${item.appId})`,
    value: item.id
  }))
);

const projectColumns = [
  { title: () => t("ai.openPlatform.portal.apps.name"), dataIndex: "name", key: "name" },
  { title: () => t("ai.openPlatform.portal.apps.appId"), dataIndex: "appId", key: "appId" },
  { title: () => t("ai.openPlatform.portal.apps.scopes"), dataIndex: "scopes", key: "scopes" },
  { title: () => t("ai.openPlatform.portal.apps.status"), key: "status", width: 120 },
  { title: () => t("ai.openPlatform.portal.apps.lastUsedAt"), key: "lastUsedAt", width: 180 },
  { title: () => t("common.actions"), key: "actions", width: 260 }
];

const webhookColumns = [
  { title: () => t("ai.openPlatform.portal.webhooks.name"), dataIndex: "name", key: "name" },
  { title: () => t("ai.openPlatform.portal.webhooks.events"), dataIndex: "eventTypes", key: "eventTypes" },
  { title: () => t("ai.openPlatform.portal.webhooks.targetUrl"), dataIndex: "targetUrl", key: "targetUrl" },
  { title: () => t("ai.openPlatform.portal.webhooks.lastTriggeredAt"), key: "lastTriggeredAt", width: 180 },
  { title: () => t("common.actions"), key: "actions", width: 240 }
];

const deliveryColumns = [
  { title: () => t("ai.openPlatform.portal.webhooks.eventType"), dataIndex: "eventType", key: "eventType" },
  { title: () => t("ai.openPlatform.portal.webhooks.status"), key: "success", width: 120 },
  { title: () => t("ai.openPlatform.portal.webhooks.responseCode"), dataIndex: "responseCode", key: "responseCode", width: 120 },
  { title: () => t("ai.openPlatform.portal.webhooks.durationMs"), dataIndex: "durationMs", key: "durationMs", width: 120 },
  { title: () => t("common.createdAt"), key: "createdAt", width: 180 }
];

function formatDateTime(value?: string) {
  if (!value) {
    return "-";
  }
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }
  return date.toLocaleString();
}

function parseScopes(text: string) {
  return text
    .split(",")
    .map(item => item.trim())
    .filter(item => item.length > 0);
}

function parseEventTypes(raw: string) {
  try {
    const parsed = JSON.parse(raw) as string[];
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

async function loadProjects() {
  projectsLoading.value = true;
  try {
    const pageIndex = Number(projectPagination.current ?? 1);
    const pageSize = Number(projectPagination.pageSize ?? 10);
    const result = await getOpenApiProjectsPaged({
      pageIndex,
      pageSize,
      keyword: projectKeyword.value.trim()
    });
    projectRows.value = result.items;
    projectPagination.total = result.total;
    projectPagination.current = result.pageIndex;
    projectPagination.pageSize = result.pageSize;
  } catch (error) {
    message.error((error as Error).message || t("common.loadFailed"));
  } finally {
    projectsLoading.value = false;
  }
}

async function handleProjectTableChange(pagination: TablePaginationConfig) {
  projectPagination.current = pagination.current ?? 1;
  projectPagination.pageSize = pagination.pageSize ?? 10;
  await loadProjects();
}

function openCreateProjectModal() {
  projectCreateForm.name = "";
  projectCreateForm.description = "";
  projectCreateForm.scopesInput = "open:*";
  projectCreateVisible.value = true;
}

async function submitProjectCreate() {
  if (!projectCreateForm.name.trim()) {
    message.warning(t("ai.openPlatform.portal.apps.nameRequired"));
    return;
  }
  const scopes = parseScopes(projectCreateForm.scopesInput);
  if (scopes.length === 0) {
    message.warning(t("ai.openPlatform.portal.apps.scopeRequired"));
    return;
  }

  projectCreateLoading.value = true;
  try {
    const result = await createOpenApiProject({
      name: projectCreateForm.name.trim(),
      description: projectCreateForm.description.trim(),
      scopes
    });
    tokenModalText.value = `AppId: ${result.appId}\nAppSecret: ${result.appSecret}`;
    tokenModalVisible.value = true;
    projectCreateVisible.value = false;
    await loadProjects();
    message.success(t("common.createSuccess"));
  } catch (error) {
    message.error((error as Error).message || t("common.createFailed"));
  } finally {
    projectCreateLoading.value = false;
  }
}

async function rotateSecret(record: OpenApiProjectListItem) {
  try {
    const result = await rotateOpenApiProjectSecret(record.id);
    tokenModalText.value = `AppId: ${result.appId}\nAppSecret: ${result.appSecret}`;
    tokenModalVisible.value = true;
    message.success(t("common.updateSuccess"));
  } catch (error) {
    message.error((error as Error).message || t("common.updateFailed"));
  }
}

async function deleteProject(record: OpenApiProjectListItem) {
  try {
    await deleteOpenApiProject(record.id);
    message.success(t("common.deleteSuccess"));
    await loadProjects();
  } catch (error) {
    message.error((error as Error).message || t("common.deleteFailed"));
  }
}

async function exchangeToken(record: OpenApiProjectListItem) {
  const appSecret = window.prompt(t("ai.openPlatform.portal.apps.enterSecretPrompt"), "");
  if (!appSecret) {
    return;
  }
  try {
    const result = await exchangeOpenApiProjectToken(record.appId, appSecret);
    tokenModalText.value = result.accessToken;
    tokenModalVisible.value = true;
  } catch (error) {
    message.error((error as Error).message || t("ai.openPlatform.portal.apps.exchangeFailed"));
  }
}

async function loadStats() {
  statsLoading.value = true;
  try {
    const result = await getOpenApiStatsSummary({
      projectId: statsProjectId.value
    });
    Object.assign(statsSummary, result);
  } catch (error) {
    message.error((error as Error).message || t("common.loadFailed"));
  } finally {
    statsLoading.value = false;
  }
}

async function loadWebhooks() {
  webhooksLoading.value = true;
  try {
    webhookRows.value = await listOpenApiWebhooks();
  } catch (error) {
    message.error((error as Error).message || t("common.loadFailed"));
  } finally {
    webhooksLoading.value = false;
  }
}

function openCreateWebhookModal() {
  webhookCreateForm.name = "";
  webhookCreateForm.targetUrl = "";
  webhookCreateForm.secret = "";
  webhookCreateForm.eventTypes = ["workflow.completed", "agent.message"];
  webhookCreateVisible.value = true;
}

async function submitWebhookCreate() {
  if (!webhookCreateForm.name.trim() || !webhookCreateForm.targetUrl.trim() || !webhookCreateForm.secret.trim()) {
    message.warning(t("ai.openPlatform.portal.webhooks.required"));
    return;
  }
  webhookCreateLoading.value = true;
  try {
    await createOpenApiWebhook({
      name: webhookCreateForm.name.trim(),
      targetUrl: webhookCreateForm.targetUrl.trim(),
      secret: webhookCreateForm.secret.trim(),
      eventTypes: webhookCreateForm.eventTypes,
      headers: null
    });
    webhookCreateVisible.value = false;
    message.success(t("common.createSuccess"));
    await loadWebhooks();
  } catch (error) {
    message.error((error as Error).message || t("common.createFailed"));
  } finally {
    webhookCreateLoading.value = false;
  }
}

async function testWebhook(record: WebhookSubscription) {
  try {
    await testOpenApiWebhook(record.id);
    message.success(t("ai.openPlatform.portal.webhooks.testSuccess"));
  } catch (error) {
    message.error((error as Error).message || t("ai.openPlatform.portal.webhooks.testFailed"));
  }
}

async function deleteWebhook(record: WebhookSubscription) {
  try {
    await deleteOpenApiWebhook(record.id);
    message.success(t("common.deleteSuccess"));
    await loadWebhooks();
  } catch (error) {
    message.error((error as Error).message || t("common.deleteFailed"));
  }
}

async function showDeliveries(record: WebhookSubscription) {
  try {
    deliveryRows.value = await getOpenApiWebhookDeliveries(record.id, 20);
    deliveryDrawerVisible.value = true;
  } catch (error) {
    message.error((error as Error).message || t("common.loadFailed"));
  }
}

async function downloadBlobFile(fetcher: () => Promise<Blob>, filename: string) {
  const blob = await fetcher();
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = filename;
  anchor.click();
  URL.revokeObjectURL(url);
}

async function downloadSpec() {
  try {
    await downloadBlobFile(downloadOpenApiSpec, "openapi.json");
  } catch (error) {
    message.error((error as Error).message || t("ai.openPlatform.portal.sdk.downloadFailed"));
  }
}

async function downloadSdk(language: "typescript" | "csharp") {
  try {
    const filename = language === "typescript" ? "atlas-sdk-typescript.zip" : "atlas-sdk-csharp.zip";
    await downloadBlobFile(() => downloadOpenApiSdk(language), filename);
  } catch (error) {
    message.error((error as Error).message || t("ai.openPlatform.portal.sdk.downloadFailed"));
  }
}

onMounted(async () => {
  await Promise.all([loadProjects(), loadWebhooks()]);
});
</script>
