<template>
  <div class="model-configs-page">
    <div class="page-header">
      <div>
        <h2 class="page-title">{{ t("modelConfig.pageTitle") }}</h2>
      </div>
      <div class="header-actions">
        <a-input-search
          v-model:value="keyword"
          :placeholder="t('modelConfig.searchPlaceholder')"
          style="width: 280px"
          allow-clear
          @search="handleSearch"
        />
        <a-button type="primary" @click="openCreate">
          <template #icon><PlusOutlined /></template>
          {{ t("modelConfig.newModel") }}
        </a-button>
      </div>
    </div>

    <a-row :gutter="16" class="stats-row">
      <a-col :span="6">
        <div class="stat-card">
          <div class="stat-value">{{ stats.total }}</div>
          <div class="stat-label">{{ t("modelConfig.statTotal") }}</div>
        </div>
      </a-col>
      <a-col :span="6">
        <div class="stat-card stat-card--success">
          <div class="stat-value">{{ stats.enabled }}</div>
          <div class="stat-label">{{ t("modelConfig.statEnabled") }}</div>
        </div>
      </a-col>
      <a-col :span="6">
        <div class="stat-card stat-card--warning">
          <div class="stat-value">{{ stats.disabled }}</div>
          <div class="stat-label">{{ t("modelConfig.statDisabled") }}</div>
        </div>
      </a-col>
      <a-col :span="6">
        <div class="stat-card stat-card--info">
          <div class="stat-value">{{ stats.embeddingCount }}</div>
          <div class="stat-label">{{ t("modelConfig.statEmbedding") }}</div>
        </div>
      </a-col>
    </a-row>

    <a-table
      row-key="id"
      :columns="columns"
      :data-source="dataList"
      :loading="loading"
      :pagination="pagination"
      size="middle"
      @change="onTableChange"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'name'">
          <div class="cell-name">
            <span class="name-text">{{ record.name }}</span>
            <a-tag v-if="record.supportsEmbedding" color="purple" size="small">
              {{ t("modelConfig.badgeEmbedding") }}
            </a-tag>
          </div>
        </template>

        <template v-if="column.key === 'providerType'">
          <a-tag :color="providerColor(record.providerType)">
            {{ record.providerType }}
          </a-tag>
        </template>

        <template v-if="column.key === 'defaultModel'">
          <a-tag>{{ record.defaultModel }}</a-tag>
        </template>

        <template v-if="column.key === 'apiKeyMasked'">
          <span class="api-key-masked">{{ record.apiKeyMasked || "••••••••" }}</span>
        </template>

        <template v-if="column.key === 'isEnabled'">
          <a-badge
            :status="record.isEnabled ? 'success' : 'default'"
            :text="record.isEnabled ? t('modelConfig.enabled') : t('modelConfig.disabled')"
          />
        </template>

        <template v-if="column.key === 'createdAt'">
          <span class="date-text">{{ formatDate(record.createdAt) }}</span>
        </template>

        <template v-if="column.key === 'actions'">
          <a-space>
            <a-tooltip :title="t('modelConfig.editTooltip')">
              <a-button type="link" size="small" @click="openEdit(record)">
                <template #icon><EditOutlined /></template>
              </a-button>
            </a-tooltip>
            <a-popconfirm
              :title="t('modelConfig.deleteConfirm')"
              :description="t('modelConfig.deleteRiskTip')"
              :ok-text="t('modelConfig.ok')"
              :cancel-text="t('common.cancel')"
              @confirm="handleDelete(record.id)"
            >
              <a-tooltip :title="t('modelConfig.deleteTooltip')">
                <a-button type="link" danger size="small">
                  <template #icon><DeleteOutlined /></template>
                </a-button>
              </a-tooltip>
            </a-popconfirm>
          </a-space>
        </template>
      </template>
    </a-table>

    <a-drawer
      v-model:open="drawerVisible"
      :title="drawerTitle"
      :width="520"
      :body-style="{ paddingBottom: '80px' }"
      @close="closeDrawer"
    >
      <a-form ref="formRef" :model="form" layout="vertical" :rules="currentRules">
        <a-divider orientation="left" style="margin-top: 0">{{ t("modelConfig.sectionBasic") }}</a-divider>
        <a-form-item :label="t('modelConfig.labelName')" name="name">
          <a-input v-model:value="form.name" :placeholder="t('modelConfig.namePlaceholder')" />
        </a-form-item>
        <a-form-item :label="t('modelConfig.labelProvider')" name="providerType">
          <a-select v-model:value="form.providerType" :options="providerOptions" :disabled="!!editingId" @change="onProviderChange" />
        </a-form-item>

        <a-divider orientation="left">{{ t("modelConfig.sectionConnection") }}</a-divider>
        <a-form-item :label="t('modelConfig.labelApiKey')" name="apiKey">
          <a-input-password
            v-model:value="form.apiKey"
            :placeholder="editingId ? t('modelConfig.apiKeyPlaceholderEdit') : t('modelConfig.apiKeyPlaceholderCreate')"
          />
        </a-form-item>
        <a-form-item :label="t('modelConfig.labelBaseUrl')" name="baseUrl">
          <a-input v-model:value="form.baseUrl" />
        </a-form-item>
        <a-form-item :label="t('modelConfig.labelDefaultModel')" name="defaultModel">
          <a-auto-complete
            v-model:value="form.defaultModel"
            :options="suggestedModels"
            :placeholder="t('modelConfig.defaultModelPlaceholder')"
            :filter-option="filterModelOption"
          />
        </a-form-item>

        <a-divider orientation="left">{{ t("modelConfig.sectionFeatures") }}</a-divider>
        <a-row :gutter="24">
          <a-col :span="12">
            <a-form-item :label="t('modelConfig.labelEmbedding')">
              <a-switch v-model:checked="form.supportsEmbedding" />
              <span class="switch-label">{{ form.supportsEmbedding ? t("modelConfig.switchOn") : t("modelConfig.switchOff") }}</span>
            </a-form-item>
          </a-col>
          <a-col v-if="editingId" :span="12">
            <a-form-item :label="t('modelConfig.labelEnabled')">
              <a-switch v-model:checked="form.isEnabled" />
              <span class="switch-label">{{ form.isEnabled ? t("modelConfig.enabledOn") : t("modelConfig.enabledOff") }}</span>
            </a-form-item>
          </a-col>
        </a-row>

        <a-divider orientation="left">{{ t("modelConfig.sectionTest") }}</a-divider>
        <div class="test-section">
          <a-button :loading="testing" @click="handleTestConnection">
            <template #icon><ApiOutlined /></template>
            {{ t("modelConfig.testConnection") }}
          </a-button>
          <a-alert
            v-if="testResult"
            :type="testResult.success ? 'success' : 'error'"
            :message="testResult.success ? t('modelConfig.connectOk') : t('modelConfig.connectFail')"
            show-icon
            closable
            class="test-result"
            @close="testResult = null"
          >
            <template #description>
              <span v-if="testResult.success">{{ t("modelConfig.latency", { ms: testResult.latencyMs ?? 0 }) }}</span>
              <span v-else>{{ testResult.errorMessage || t("modelConfig.connectUnreachable") }}</span>
            </template>
          </a-alert>
        </div>
      </a-form>

      <template #footer>
        <div style="display: flex; justify-content: flex-end; gap: 8px">
          <a-button @click="closeDrawer">{{ t("common.cancel") }}</a-button>
          <a-button type="primary" :loading="submitting" @click="submitForm">{{ t("common.save") }}</a-button>
        </div>
      </template>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, reactive, ref } from "vue";
import { useI18n } from "vue-i18n";
import type { FormInstance, TablePaginationConfig } from "ant-design-vue";
import { message } from "ant-design-vue";
import { PlusOutlined, EditOutlined, DeleteOutlined, ApiOutlined } from "@ant-design/icons-vue";
import {
  createModelConfig,
  deleteModelConfig,
  getModelConfigById,
  getModelConfigsPaged,
  getModelConfigStats,
  testModelConfigConnection,
  updateModelConfig,
  type ModelConfigDto,
  type ModelConfigStatsDto,
  type ModelConfigTestResult
} from "@/services/api-model-config";

const { t } = useI18n();

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

const keyword = ref("");
const dataList = ref<ModelConfigDto[]>([]);
const loading = ref(false);
const testing = ref(false);
const submitting = ref(false);
const testResult = ref<ModelConfigTestResult | null>(null);
const statsData = ref<ModelConfigStatsDto>({ total: 0, enabled: 0, disabled: 0, embeddingCount: 0 });

const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: true,
  pageSizeOptions: ["10", "20", "50"],
  showTotal: (total: number) => t("crud.totalItems", { total })
});

const stats = computed(() => statsData.value);

const columns = computed(() => [
  { title: t("modelConfig.labelName"), dataIndex: "name", key: "name", width: 200 },
  { title: t("modelConfig.colProvider"), dataIndex: "providerType", key: "providerType", width: 120 },
  { title: t("modelConfig.labelBaseUrl"), dataIndex: "baseUrl", key: "baseUrl", ellipsis: true },
  { title: t("modelConfig.labelDefaultModel"), dataIndex: "defaultModel", key: "defaultModel", width: 160 },
  { title: t("modelConfig.colApiKey"), dataIndex: "apiKeyMasked", key: "apiKeyMasked", width: 120 },
  { title: t("modelConfig.labelEnabled"), key: "isEnabled", width: 90 },
  { title: t("common.actions"), key: "actions", width: 100, fixed: "right" as const }
]);

const providerOptions = [
  { label: "OpenAI", value: "openai" },
  { label: "DeepSeek", value: "deepseek" },
  { label: "Ollama", value: "ollama" },
  { label: "Custom", value: "custom" }
];

const providerModelSuggestions: Record<string, string[]> = {
  openai: ["gpt-4o", "gpt-4o-mini", "gpt-4-turbo", "gpt-3.5-turbo", "text-embedding-3-small"],
  deepseek: ["deepseek-chat", "deepseek-coder", "deepseek-reasoner"],
  ollama: ["llama3", "mistral", "codellama", "nomic-embed-text"],
  custom: []
};

const providerBaseUrls: Record<string, string> = {
  openai: "https://api.openai.com/v1",
  deepseek: "https://api.deepseek.com/v1",
  ollama: "http://localhost:11434/v1",
  custom: ""
};

function providerColor(type: string): string {
  const map: Record<string, string> = { openai: "green", deepseek: "blue", ollama: "orange", custom: "default" };
  return map[type] ?? "default";
}

const suggestedModels = computed(() =>
  (providerModelSuggestions[form.providerType] ?? []).map((m) => ({ value: m }))
);

function filterModelOption(input: string, option: { value: string }) {
  return option.value.toLowerCase().includes(input.toLowerCase());
}

const drawerVisible = ref(false);
const editingId = ref<number | null>(null);
const drawerTitle = computed(() => editingId.value ? t("modelConfig.drawerEdit") : t("modelConfig.drawerCreate"));
const formRef = ref<FormInstance>();

const form = reactive({
  name: "",
  providerType: "openai",
  apiKey: "",
  baseUrl: "",
  defaultModel: "",
  supportsEmbedding: true,
  isEnabled: true
});

const baseRules = computed(() => ({
  name: [{ required: true, message: t("modelConfig.ruleName") }],
  providerType: [{ required: true, message: t("modelConfig.ruleProvider") }],
  baseUrl: [{ required: true, message: t("modelConfig.ruleBaseUrl") }],
  defaultModel: [{ required: true, message: t("modelConfig.ruleDefaultModel") }]
}));

const createRules = computed(() => ({
  ...baseRules.value,
  apiKey: [{ required: true, message: t("modelConfig.ruleApiKey") }]
}));

const currentRules = computed(() => (editingId.value ? baseRules.value : createRules.value));

function formatDate(dateStr: string) {
  if (!dateStr) return "-";
  const d = new Date(dateStr);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
}

function onProviderChange(value: string) {
  const suggestedUrl = providerBaseUrls[value];
  if (!form.baseUrl || Object.values(providerBaseUrls).includes(form.baseUrl)) {
    form.baseUrl = suggestedUrl ?? "";
  }
  form.defaultModel = "";
  testResult.value = null;
}

async function loadData() {
  loading.value = true;
  try {
    const kw = keyword.value || undefined;
    const [pagedResult, statsResult] = await Promise.all([
      getModelConfigsPaged({ pageIndex: pagination.current ?? 1, pageSize: pagination.pageSize ?? 20, keyword: kw }),
      getModelConfigStats(kw)
    ]);
    if (!isMounted.value) return;
    dataList.value = pagedResult.items;
    pagination.total = Number(pagedResult.total);
    statsData.value = statsResult;
  } catch (error: unknown) {
    message.error((error as Error).message || t("crud.queryFailed"));
  } finally {
    loading.value = false;
  }
}

function onTableChange(page: TablePaginationConfig) {
  pagination.current = page.current ?? 1;
  pagination.pageSize = page.pageSize ?? 20;
  void loadData();
}

function handleSearch() {
  pagination.current = 1;
  void loadData();
}

function resetForm() {
  Object.assign(form, { name: "", providerType: "openai", apiKey: "", baseUrl: "", defaultModel: "", supportsEmbedding: true, isEnabled: true });
  testResult.value = null;
}

function openCreate() {
  editingId.value = null;
  resetForm();
  drawerVisible.value = true;
}

async function openEdit(record: ModelConfigDto) {
  try {
    const detail = await getModelConfigById(record.id);
    if (!isMounted.value) return;
    editingId.value = record.id;
    Object.assign(form, {
      name: detail.name,
      providerType: detail.providerType,
      apiKey: "",
      baseUrl: detail.baseUrl,
      defaultModel: detail.defaultModel,
      supportsEmbedding: detail.supportsEmbedding,
      isEnabled: detail.isEnabled
    });
    testResult.value = null;
    drawerVisible.value = true;
  } catch (error: unknown) {
    message.error((error as Error).message || t("crud.queryFailed"));
  }
}

function closeDrawer() {
  drawerVisible.value = false;
  formRef.value?.resetFields();
  testResult.value = null;
}

async function submitForm() {
  if (submitting.value) return;
  try {
    await formRef.value?.validate();
    if (!isMounted.value) return;
  } catch { return; }

  submitting.value = true;
  try {
    if (editingId.value) {
      await updateModelConfig(editingId.value, {
        name: form.name,
        apiKey: form.apiKey,
        baseUrl: form.baseUrl,
        defaultModel: form.defaultModel,
        isEnabled: form.isEnabled,
        supportsEmbedding: form.supportsEmbedding
      });
      if (!isMounted.value) return;
      message.success(t("crud.updateSuccess"));
    } else {
      await createModelConfig({
        name: form.name,
        providerType: form.providerType,
        apiKey: form.apiKey,
        baseUrl: form.baseUrl,
        defaultModel: form.defaultModel,
        supportsEmbedding: form.supportsEmbedding
      });
      if (!isMounted.value) return;
      message.success(t("crud.createSuccess"));
    }
    drawerVisible.value = false;
    await loadData();
    if (!isMounted.value) return;
  } catch (error: unknown) {
    message.error((error as Error).message || t("crud.submitFailed"));
  } finally {
    submitting.value = false;
  }
}

async function handleDelete(id: number) {
  try {
    await deleteModelConfig(id);
    if (!isMounted.value) return;
    message.success(t("crud.deleteSuccess"));
    await loadData();
    if (!isMounted.value) return;
  } catch (error: unknown) {
    message.error((error as Error).message || t("crud.deleteFailed"));
  }
}

async function handleTestConnection() {
  testing.value = true;
  testResult.value = null;
  try {
    testResult.value = await testModelConfigConnection({
      modelConfigId: editingId.value ?? undefined,
      providerType: form.providerType,
      apiKey: form.apiKey,
      baseUrl: form.baseUrl,
      model: form.defaultModel
    });
    if (!isMounted.value) return;
  } catch (error: unknown) {
    testResult.value = { success: false, errorMessage: (error as Error).message || t("modelConfig.testFailed") };
  } finally {
    testing.value = false;
  }
}

onMounted(() => { void loadData(); });
</script>

<style scoped>
.model-configs-page {
  padding: 24px;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 20px;
}

.page-title {
  font-size: 18px;
  font-weight: 600;
  color: #1e2939;
  margin: 0;
}

.header-actions {
  display: flex;
  gap: 12px;
  align-items: center;
}

.stats-row {
  margin-bottom: 16px;
}

.stat-card {
  background: #fff;
  border: 1px solid #e5e7eb;
  border-radius: 12px;
  padding: 12px 16px;
  text-align: center;
}

.stat-card--success { border-left: 3px solid #22c55e; }
.stat-card--warning { border-left: 3px solid #f59e0b; }
.stat-card--info { border-left: 3px solid #8b5cf6; }

.stat-value {
  font-size: 22px;
  font-weight: 600;
  color: #1e2939;
}

.stat-label {
  font-size: 13px;
  color: #9ca3af;
  margin-top: 2px;
}

.cell-name {
  display: flex;
  align-items: center;
  gap: 6px;
}

.name-text { font-weight: 500; }

.api-key-masked {
  font-family: "Consolas", "SF Mono", monospace;
  color: #9ca3af;
  font-size: 12px;
  letter-spacing: 1px;
}

.date-text {
  font-size: 13px;
  color: #9ca3af;
}

.switch-label {
  margin-left: 8px;
  color: #6a7282;
  font-size: 13px;
}

.test-section {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.test-result {
  margin-top: 4px;
}
</style>
