<template>
  <CrudPageLayout
    v-model:keyword="keyword"
    :title="t('ai.modelConfig.pageTitle')"
    :search-placeholder="t('ai.modelConfig.searchPlaceholder')"
    :drawer-open="drawerVisible"
    :drawer-title="drawerTitle"
    :drawer-width="560"
    :submit-loading="submitting"
    :submit-disabled="submitting"
    @update:drawer-open="drawerVisible = $event"
    @search="handleSearch"
    @reset="handleReset"
    @close-form="closeDrawer"
    @submit="submitForm"
  >
    <template #toolbar-actions>
      <a-button type="primary" @click="openCreate">
        <template #icon><PlusOutlined /></template>
        {{ t("ai.modelConfig.newModel") }}
      </a-button>
    </template>

    <template #table>
      <!-- Stats overview -->
      <a-row :gutter="16" class="stats-row">
        <a-col :span="6">
          <div class="stat-card">
            <div class="stat-value">{{ stats.total }}</div>
            <div class="stat-label">{{ t("ai.modelConfig.statTotal") }}</div>
          </div>
        </a-col>
        <a-col :span="6">
          <div class="stat-card stat-card--success">
            <div class="stat-value">{{ stats.enabled }}</div>
            <div class="stat-label">{{ t("ai.modelConfig.statEnabled") }}</div>
          </div>
        </a-col>
        <a-col :span="6">
          <div class="stat-card stat-card--warning">
            <div class="stat-value">{{ stats.disabled }}</div>
            <div class="stat-label">{{ t("ai.modelConfig.statDisabled") }}</div>
          </div>
        </a-col>
        <a-col :span="6">
          <div class="stat-card stat-card--info">
            <div class="stat-value">{{ stats.embeddingCount }}</div>
            <div class="stat-label">{{ t("ai.modelConfig.statEmbedding") }}</div>
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
              <a-tag v-if="record.supportsEmbedding" color="purple" class="embed-tag">
                Embedding
              </a-tag>
            </div>
          </template>

          <template v-if="column.key === 'providerType'">
            <a-tag :color="providerMeta(record.providerType).color">
              <component :is="providerMeta(record.providerType).icon" style="margin-right: 4px" />
              {{ providerMeta(record.providerType).label }}
            </a-tag>
          </template>

          <template v-if="column.key === 'baseUrl'">
            <a-typography-text :content="record.baseUrl" :ellipsis="{ tooltip: record.baseUrl }" class="url-text" />
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
              :text="record.isEnabled ? t('ai.modelConfig.enabled') : t('ai.modelConfig.disabled')"
            />
          </template>

          <template v-if="column.key === 'createdAt'">
            <span class="date-text">{{ formatDate(record.createdAt) }}</span>
          </template>

          <template v-if="column.key === 'actions'">
            <a-space>
              <a-tooltip :title="t('ai.modelConfig.editTooltip')">
                <a-button type="link" size="small" @click="openEdit(record)">
                  <template #icon><EditOutlined /></template>
                </a-button>
              </a-tooltip>
              <a-popconfirm
                :title="t('ai.modelConfig.deleteConfirm')"
                :ok-text="t('ai.modelConfig.ok')"
                :cancel-text="t('common.cancel')"
                @confirm="handleDelete(record.id)"
              >
                <a-tooltip :title="t('ai.modelConfig.deleteTooltip')">
                  <a-button type="link" danger size="small">
                    <template #icon><DeleteOutlined /></template>
                  </a-button>
                </a-tooltip>
              </a-popconfirm>
            </a-space>
          </template>
        </template>
      </a-table>
    </template>

    <template #form>
      <a-form ref="formRef" :model="form" layout="vertical" :rules="currentRules">
        <!-- Basic -->
        <a-divider orientation="left" style="margin-top: 0">{{ t("ai.modelConfig.sectionBasic") }}</a-divider>

        <a-form-item :label="t('ai.modelConfig.labelName')" name="name">
          <a-input v-model:value="form.name" :placeholder="t('ai.modelConfig.namePlaceholder')" />
        </a-form-item>

        <a-form-item :label="t('ai.modelConfig.labelProvider')" name="providerType">
          <a-select
            v-model:value="form.providerType"
            :options="providerOptions"
            :disabled="!!editingId"
            @change="onProviderChange"
          >
            <template #option="{ label, value: val }">
              <a-space>
                <component :is="providerMeta(val).icon" />
                <span>{{ label }}</span>
              </a-space>
            </template>
          </a-select>
        </a-form-item>

        <!-- Connection -->
        <a-divider orientation="left">{{ t("ai.modelConfig.sectionConnection") }}</a-divider>

        <a-form-item label="API Key" name="apiKey">
          <a-input-password
            v-model:value="form.apiKey"
            :placeholder="editingId ? t('ai.modelConfig.apiKeyPlaceholderEdit') : t('ai.modelConfig.apiKeyPlaceholderCreate')"
          />
          <template #extra>
            <span class="field-hint">{{ providerHints.apiKeyHint }}</span>
          </template>
        </a-form-item>

        <a-form-item label="Base URL" name="baseUrl">
          <a-input v-model:value="form.baseUrl" :placeholder="providerHints.baseUrlPlaceholder" />
          <template #extra>
            <span class="field-hint">{{ providerHints.baseUrlHint }}</span>
          </template>
        </a-form-item>

        <a-form-item :label="t('ai.modelConfig.labelDefaultModel')" name="defaultModel">
          <a-auto-complete
            v-model:value="form.defaultModel"
            :options="suggestedModels"
            :placeholder="t('ai.modelConfig.defaultModelPlaceholder')"
            :filter-option="filterModelOption"
          />
          <template #extra>
            <span class="field-hint">{{ t("ai.modelConfig.defaultModelHint") }}</span>
          </template>
        </a-form-item>

        <!-- Features & status -->
        <a-divider orientation="left">{{ t("ai.modelConfig.sectionFeatures") }}</a-divider>

        <a-row :gutter="24">
          <a-col :span="12">
            <a-form-item :label="t('ai.modelConfig.labelEmbedding')">
              <a-switch v-model:checked="form.supportsEmbedding" />
              <span class="switch-label">{{ form.supportsEmbedding ? t("ai.modelConfig.switchOn") : t("ai.modelConfig.switchOff") }}</span>
            </a-form-item>
          </a-col>
          <a-col v-if="editingId" :span="12">
            <a-form-item :label="t('ai.modelConfig.labelEnabled')">
              <a-switch v-model:checked="form.isEnabled" />
              <span class="switch-label">{{ form.isEnabled ? t("ai.modelConfig.enabledOn") : t("ai.modelConfig.enabledOff") }}</span>
            </a-form-item>
          </a-col>
        </a-row>

        <!-- Connection test -->
        <a-divider orientation="left">{{ t("ai.modelConfig.sectionTest") }}</a-divider>

        <div class="test-section">
          <a-button type="default" :loading="testing" @click="handleTestConnection">
            <template #icon><ApiOutlined /></template>
            {{ t("ai.modelConfig.testConnection") }}
          </a-button>

          <a-alert
            v-if="testResult"
            :type="testResult.success ? 'success' : 'error'"
            :message="testResult.success ? t('ai.modelConfig.connectOk') : t('ai.modelConfig.connectFail')"
            show-icon
            closable
            class="test-result"
            @close="testResult = null"
          >
            <template #description>
              <div v-if="testResult.success">
                {{ testResultDescription }}
              </div>
              <ul v-else class="test-error-list">
                <li v-for="(line, index) in testResultErrorLines" :key="`${index}-${line}`">{{ line }}</li>
              </ul>
            </template>
          </a-alert>
        </div>

        <a-divider orientation="left">{{ t("ai.modelConfig.sectionPromptTest") }}</a-divider>
        <div class="prompt-test-section">
          <a-textarea
            v-model:value="promptTestInput"
            :rows="4"
            :placeholder="t('ai.modelConfig.promptPlaceholder')"
            :disabled="promptTesting"
          />
          <a-space wrap>
            <a-switch v-model:checked="promptTestEnableReasoning" size="small" />
            <span class="switch-label">{{ t("ai.modelConfig.enableReasoning") }}</span>
            <a-switch v-model:checked="promptTestEnableTools" size="small" />
            <span class="switch-label">{{ t("ai.modelConfig.enableTools") }}</span>
          </a-space>
          <a-space>
            <a-button type="primary" :loading="promptTesting" @click="handlePromptStreamTest">
              {{ t("ai.modelConfig.runPromptTest") }}
            </a-button>
            <a-button v-if="promptTesting" danger @click="handleStopPromptStreamTest">
              {{ t("ai.modelConfig.stopPromptTest") }}
            </a-button>
          </a-space>
          <a-alert
            v-if="promptTestError"
            type="error"
            :message="promptTestError"
            show-icon
            closable
            @close="promptTestError = ''"
          />
          <div class="chat-preview">
            <div class="chat-message-row chat-message-row-user">
              <div class="chat-bubble chat-bubble-user">
                <div class="bubble-title">{{ t("ai.modelConfig.promptInput") }}</div>
                <pre class="bubble-content">{{ promptTestInput || t("ai.modelConfig.emptyOutput") }}</pre>
              </div>
            </div>
            <div class="chat-message-row chat-message-row-assistant">
              <div class="chat-bubble chat-bubble-assistant">
                <div class="stream-title stream-title-actions">
                  <span>{{ t("ai.modelConfig.outputFinal") }}</span>
                  <a-button size="small" type="link" @click="copyPromptOutput(promptTestFinalOutput)">
                    {{ t("ai.modelConfig.copyOutput") }}
                  </a-button>
                </div>
                <pre class="bubble-content">{{ promptTestFinalOutput || t("ai.modelConfig.emptyOutput") }}</pre>
              </div>
            </div>
          </div>
          <a-collapse v-model:active-key="activeThoughtPanels" size="small">
            <a-collapse-panel key="thought" :header="t('ai.modelConfig.outputThought')">
              <div class="stream-title stream-title-actions">
                <span>{{ t("ai.modelConfig.outputThought") }}</span>
                <a-button size="small" type="link" @click="copyPromptOutput(promptTestThoughtOutput)">
                  {{ t("ai.modelConfig.copyOutput") }}
                </a-button>
              </div>
              <pre class="stream-content">{{ promptTestThoughtOutput || t("ai.modelConfig.emptyOutput") }}</pre>
            </a-collapse-panel>
          </a-collapse>
          <div class="stream-card">
            <div class="stream-title stream-title-actions">
              <span>{{ t("ai.modelConfig.outputTool") }}</span>
              <a-button size="small" type="link" @click="copyPromptOutput(promptTestToolOutput)">
                {{ t("ai.modelConfig.copyOutput") }}
              </a-button>
            </div>
            <pre class="stream-content">{{ promptTestToolOutput || t("ai.modelConfig.emptyOutput") }}</pre>
          </div>
        </div>
      </a-form>
    </template>
  </CrudPageLayout>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, onUnmounted } from "vue";
import { useI18n } from "vue-i18n";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import type { FormInstance, TablePaginationConfig } from "ant-design-vue";
import { message } from "ant-design-vue";
import {
  PlusOutlined,
  EditOutlined,
  DeleteOutlined,
  ApiOutlined,
  CloudOutlined,
  ThunderboltOutlined,
  RobotOutlined,
  ToolOutlined
} from "@ant-design/icons-vue";
import CrudPageLayout from "@/components/crud/CrudPageLayout.vue";
import {
  createModelConfigPromptTestStream,
  createModelConfig,
  deleteModelConfig,
  getModelConfigById,
  getModelConfigsPaged,
  getModelConfigStats,
  type ModelConfigCreateRequest,
  type ModelConfigDto,
  type ModelConfigStatsDto,
  type ModelConfigPromptTestRequest,
  type ModelConfigTestResult,
  testModelConfigConnection,
  updateModelConfig
} from "@/services/api-model-config";

interface ProviderInfo {
  label: string;
  color: string;
  icon: ReturnType<typeof CloudOutlined>;
}

const { t } = useI18n();

const keyword = ref("");
const dataList = ref<ModelConfigDto[]>([]);
const loading = ref(false);
const testing = ref(false);
const submitting = ref(false);
const testResult = ref<ModelConfigTestResult | null>(null);
const promptTesting = ref(false);
const promptTestInput = ref("");
const promptTestEnableReasoning = ref(true);
const promptTestEnableTools = ref(false);
const promptTestFinalOutput = ref("");
const promptTestThoughtOutput = ref("");
const promptTestToolOutput = ref("");
const promptTestError = ref("");
const activeThoughtPanels = ref<string[]>([]);
let promptTestAbortController: AbortController | null = null;
const statsData = ref<ModelConfigStatsDto>({
  total: 0,
  enabled: 0,
  disabled: 0,
  embeddingCount: 0
});

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
  { title: t("ai.promptLib.colName"), dataIndex: "name", key: "name", width: 200 },
  { title: t("ai.modelConfig.colProvider"), dataIndex: "providerType", key: "providerType", width: 130 },
  { title: "Base URL", dataIndex: "baseUrl", key: "baseUrl", ellipsis: true },
  { title: t("ai.modelConfig.labelDefaultModel"), dataIndex: "defaultModel", key: "defaultModel", width: 160 },
  { title: t("ai.modelConfig.colApiKey"), dataIndex: "apiKeyMasked", key: "apiKeyMasked", width: 120 },
  { title: t("ai.knowledgeBase.colStatus"), key: "isEnabled", width: 90 },
  { title: t("ai.knowledgeBase.colCreatedAt"), dataIndex: "createdAt", key: "createdAt", width: 110 },
  { title: t("ai.colActions"), key: "actions", width: 100, fixed: "right" as const }
]);

const providerOptions = [
  { label: "OpenAI", value: "openai" },
  { label: "DeepSeek", value: "deepseek" },
  { label: "Ollama", value: "ollama" },
  { label: "Custom", value: "custom" }
];

const providerModelSuggestions: Record<string, string[]> = {
  openai: ["gpt-4o", "gpt-4o-mini", "gpt-4-turbo", "gpt-3.5-turbo", "text-embedding-3-small", "text-embedding-3-large"],
  deepseek: ["deepseek-chat", "deepseek-coder", "deepseek-reasoner"],
  ollama: ["llama3", "llama3:70b", "mistral", "codellama", "nomic-embed-text"],
  custom: []
};

const providerBaseUrls: Record<string, string> = {
  openai: "https://api.openai.com/v1",
  deepseek: "https://api.deepseek.com/v1",
  ollama: "http://localhost:11434/v1",
  custom: ""
};

function providerMeta(type: string): ProviderInfo {
  const map: Record<string, ProviderInfo> = {
    openai: { label: "OpenAI", color: "green", icon: CloudOutlined },
    deepseek: { label: "DeepSeek", color: "blue", icon: ThunderboltOutlined },
    ollama: { label: "Ollama", color: "orange", icon: RobotOutlined },
    custom: { label: "Custom", color: "default", icon: ToolOutlined }
  };
  return map[type] ?? map["custom"];
}

const providerHints = computed(() => {
  const type = form.providerType;
  const hints: Record<string, { baseUrlPlaceholder: string; baseUrlHint: string; apiKeyHint: string }> = {
    openai: {
      baseUrlPlaceholder: "https://api.openai.com/v1",
      baseUrlHint: t("ai.modelConfig.hintOpenaiBase"),
      apiKeyHint: t("ai.modelConfig.hintOpenaiKey")
    },
    deepseek: {
      baseUrlPlaceholder: "https://api.deepseek.com/v1",
      baseUrlHint: t("ai.modelConfig.hintDeepseekBase"),
      apiKeyHint: t("ai.modelConfig.hintDeepseekKey")
    },
    ollama: {
      baseUrlPlaceholder: "http://localhost:11434/v1",
      baseUrlHint: t("ai.modelConfig.hintOllamaBase"),
      apiKeyHint: t("ai.modelConfig.hintOllamaKey")
    },
    custom: {
      baseUrlPlaceholder: "https://your-api-endpoint.com/v1",
      baseUrlHint: t("ai.modelConfig.hintCustomBase"),
      apiKeyHint: t("ai.modelConfig.hintCustomKey")
    }
  };
  return hints[type] ?? hints["custom"];
});

const suggestedModels = computed(() =>
  (providerModelSuggestions[form.providerType] ?? []).map((m) => ({ value: m }))
);

function filterModelOption(input: string, option: { value: string }) {
  return option.value.toLowerCase().includes(input.toLowerCase());
}

const drawerVisible = ref(false);
const editingId = ref<number | null>(null);

const drawerTitle = computed(() =>
  editingId.value ? t("ai.modelConfig.drawerEdit") : t("ai.modelConfig.drawerCreate")
);

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
const previousProviderType = ref(form.providerType);

const baseRules = computed(() => ({
  name: [{ required: true, message: t("ai.modelConfig.ruleName") }],
  providerType: [{ required: true, message: t("ai.modelConfig.ruleProvider") }],
  baseUrl: [{ required: true, message: t("ai.modelConfig.ruleBaseUrl") }],
  defaultModel: [{ required: true, message: t("ai.modelConfig.ruleDefaultModel") }]
}));

const createRules = computed(() => ({
  ...baseRules.value,
  apiKey: [{ required: true, message: t("ai.modelConfig.ruleApiKey") }]
}));

const currentRules = computed(() => (editingId.value ? baseRules.value : createRules.value));

function formatDate(dateStr: string) {
  if (!dateStr) return "-";
  const d = new Date(dateStr);
  const y = d.getFullYear();
  const m = String(d.getMonth() + 1).padStart(2, "0");
  const day = String(d.getDate()).padStart(2, "0");
  return `${y}-${m}-${day}`;
}

const testResultDescription = computed(() => {
  if (!testResult.value) return "";
  if (testResult.value.success) {
    return t("ai.modelConfig.latency", { ms: testResult.value.latencyMs ?? 0 });
  }
  return testResult.value.errorMessage || t("ai.modelConfig.connectUnreachable");
});

const testResultErrorLines = computed(() => {
  const raw = testResult.value?.errorMessage ?? "";
  const cleaned = stripTraceId(raw);
  const normalized = cleaned
    .replace(/one or more validation errors occurred\.?/ig, "")
    .replace(/发生一个或多个验证错误。?/g, "")
    .trim();
  const tokens = normalized
    .split(/[;\n；]+/g)
    .map((item) => item.trim())
    .filter((item) => item.length > 0);
  const deduped = Array.from(new Set(tokens));
  return deduped.length > 0 ? deduped : [t("ai.modelConfig.connectUnreachable")];
});

function stripTraceId(text: string): string {
  return text
    .replace(/（\s*traceId:\s*[^）]+\s*）/gi, "")
    .replace(/\(\s*traceId:\s*[^)]+\s*\)/gi, "")
    .trim();
}

function onProviderChange(value: string) {
  const lastProviderType = previousProviderType.value;
  const suggestedUrl = providerBaseUrls[value];
  const previousProviderUrl = providerBaseUrls[lastProviderType];

  // Replace only when empty or still the previous provider default URL (do not overwrite user input).
  if (!form.baseUrl || (previousProviderUrl && form.baseUrl === previousProviderUrl)) {
    form.baseUrl = suggestedUrl ?? "";
  }

  const previousModels = providerModelSuggestions[lastProviderType] ?? [];
  const normalizedCurrentModel = form.defaultModel.trim().toLowerCase();
  const usesPreviousProviderModel = previousModels.some(
    (model) => model.toLowerCase() === normalizedCurrentModel
  );

  // Clear default model only when empty or clearly a suggestion from the previous provider.
  if (!normalizedCurrentModel || usesPreviousProviderModel) {
    form.defaultModel = "";
  }

  previousProviderType.value = value;
  testResult.value = null;
}

async function loadData() {
  loading.value = true;
  try {
    const currentKeyword = keyword.value || undefined;
    const [pagedResult, statsResult]  = await Promise.all([
      getModelConfigsPaged({
        pageIndex: pagination.current ?? 1,
        pageSize: pagination.pageSize ?? 20,
        keyword: currentKeyword
      }),
      getModelConfigStats(currentKeyword)
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

function handleReset() {
  keyword.value = "";
  pagination.current = 1;
  void loadData();
}

function resetForm() {
  Object.assign(form, {
    name: "",
    providerType: "openai",
    apiKey: "",
    baseUrl: "",
    defaultModel: "",
    supportsEmbedding: true,
    isEnabled: true
  });
  previousProviderType.value = form.providerType;
  testResult.value = null;
  promptTestInput.value = "";
  promptTestEnableReasoning.value = true;
  promptTestEnableTools.value = false;
  promptTestFinalOutput.value = "";
  promptTestThoughtOutput.value = "";
  promptTestToolOutput.value = "";
  promptTestError.value = "";
  activeThoughtPanels.value = [];
}

function openCreate() {
  editingId.value = null;
  resetForm();
  drawerVisible.value = true;
}

async function openEdit(record: ModelConfigDto) {
  try {
    const detail  = await getModelConfigById(record.id);

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
    previousProviderType.value = detail.providerType;
    testResult.value = null;
    drawerVisible.value = true;
  } catch (error: unknown) {
    message.error((error as Error).message || t("crud.loadDetailFailed"));
  }
}

function closeDrawer() {
  handleStopPromptStreamTest();
  drawerVisible.value = false;
  formRef.value?.resetFields();
  testResult.value = null;
}

async function submitForm() {
  if (submitting.value) {
    return;
  }

  try {
    await formRef.value?.validate();

    if (!isMounted.value) return;
  } catch {
    return;
  }

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
      const payload: ModelConfigCreateRequest = {
        name: form.name,
        providerType: form.providerType,
        apiKey: form.apiKey,
        baseUrl: form.baseUrl,
        defaultModel: form.defaultModel,
        supportsEmbedding: form.supportsEmbedding
      };
      await createModelConfig(payload);

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
      providerType: form.providerType,
      apiKey: form.apiKey,
      baseUrl: form.baseUrl,
      model: form.defaultModel
    });

    if (!isMounted.value) return;
  } catch (error: unknown) {
    testResult.value = {
      success: false,
      errorMessage: (error as Error).message || t("ai.modelConfig.testFailed")
    };
  } finally {
    testing.value = false;
  }
}

async function handlePromptStreamTest() {
  if (promptTesting.value) {
    return;
  }

  if (!promptTestInput.value.trim()) {
    message.warning(t("ai.modelConfig.promptRequired"));
    return;
  }

  promptTestFinalOutput.value = "";
  promptTestThoughtOutput.value = "";
  promptTestToolOutput.value = "";
  promptTestError.value = "";
  activeThoughtPanels.value = [];
  promptTesting.value = true;

  const payload: ModelConfigPromptTestRequest = {
    providerType: form.providerType,
    apiKey: form.apiKey,
    baseUrl: form.baseUrl,
    model: form.defaultModel,
    prompt: promptTestInput.value.trim(),
    enableReasoning: promptTestEnableReasoning.value,
    enableTools: promptTestEnableTools.value
  };

  const { fetchPromise, abortController } = createModelConfigPromptTestStream(payload);
  promptTestAbortController = abortController;

  try {
    const response = await fetchPromise;
    if (!response.ok) {
      throw new Error(`HTTP ${response.status}: ${response.statusText}`);
    }
    if (!response.body) {
      throw new Error(t("ai.modelConfig.testFailed"));
    }

    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let buffer = "";
    let currentEventType = "data";
    let currentDataLines: string[] = [];
    let doneReceived = false;

    const flushEvent = () => {
      if (currentDataLines.length === 0) {
        currentEventType = "data";
        return;
      }

      const eventType = currentEventType;
      const eventData = currentDataLines.join("\n");
      currentDataLines = [];
      currentEventType = "data";

      if (eventData === "[DONE]" || eventType === "done") {
        doneReceived = true;
        return;
      }

      if (eventType === "thought") {
        promptTestThoughtOutput.value += eventData;
        return;
      }

      if (eventType === "tool") {
        promptTestToolOutput.value += `${eventData}\n`;
        return;
      }

      if (eventType === "error") {
        promptTestError.value = eventData;
        return;
      }

      promptTestFinalOutput.value += eventData;
    };

    while (!doneReceived) {
      const { value, done } = await reader.read();
      if (done) {
        break;
      }

      buffer += decoder.decode(value, { stream: true });
      const lines = buffer.split(/\r?\n/);
      buffer = lines.pop() ?? "";

      for (const line of lines) {
        if (line.length === 0) {
          flushEvent();
          if (doneReceived) {
            break;
          }
          continue;
        }

        if (line.startsWith("event:")) {
          currentEventType = line.slice("event:".length).trim() || "data";
          continue;
        }

        if (line.startsWith("data:")) {
          currentDataLines.push(line.slice("data:".length).trim());
        }
      }
    }

    flushEvent();
    await reader.cancel();
  } catch (error: unknown) {
    if ((error as Error).name !== "AbortError") {
      promptTestError.value = (error as Error).message || t("ai.modelConfig.testFailed");
    }
  } finally {
    promptTesting.value = false;
    promptTestAbortController = null;
  }
}

function handleStopPromptStreamTest() {
  if (promptTestAbortController) {
    promptTestAbortController.abort();
    promptTestAbortController = null;
  }
  promptTesting.value = false;
}

async function copyPromptOutput(value: string) {
  const text = value.trim();
  if (!text) {
    message.warning(t("ai.modelConfig.emptyOutput"));
    return;
  }
  try {
    if (typeof navigator !== "undefined" && navigator.clipboard?.writeText) {
      await navigator.clipboard.writeText(text);
      message.success(t("ai.modelConfig.copySuccess"));
      return;
    }
    throw new Error("clipboard not supported");
  } catch {
    message.error(t("ai.modelConfig.copyFailed"));
  }
}

onMounted(() => {
  void loadData();
});
</script>

<style scoped>
.stats-row {
  margin-bottom: 16px;
}

.stat-card {
  background: var(--color-bg-container);
  border: 1px solid var(--color-border);
  border-radius: var(--border-radius-md);
  padding: 12px 16px;
  text-align: center;
  transition: box-shadow 0.2s;
}

.stat-card:hover {
  box-shadow: var(--shadow-sm);
}

.stat-card--success {
  border-left: 3px solid var(--color-success);
}

.stat-card--warning {
  border-left: 3px solid var(--color-warning);
}

.stat-card--info {
  border-left: 3px solid #722ed1;
}

.stat-value {
  font-size: 24px;
  font-weight: 600;
  color: var(--color-text-primary);
  line-height: 1.4;
}

.stat-label {
  font-size: 13px;
  color: var(--color-text-tertiary);
  margin-top: 2px;
}

.cell-name {
  display: flex;
  align-items: center;
  gap: 6px;
}

.name-text {
  font-weight: 500;
}

.embed-tag {
  font-size: 11px;
  line-height: 18px;
  padding: 0 4px;
}

.url-text {
  font-size: 12px;
  color: var(--color-text-tertiary);
  font-family: "SF Mono", "Fira Code", monospace;
}

.api-key-masked {
  font-family: "SF Mono", "Fira Code", monospace;
  color: var(--color-text-quaternary);
  font-size: 12px;
  letter-spacing: 1px;
}

.date-text {
  font-size: 13px;
  color: var(--color-text-tertiary);
}

.field-hint {
  font-size: 12px;
  color: var(--color-text-tertiary);
}

.switch-label {
  margin-left: 8px;
  color: var(--color-text-secondary);
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

.prompt-test-section {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.prompt-stream-row {
  margin-top: 4px;
}

.chat-preview {
  border: 1px solid var(--color-border);
  border-radius: var(--border-radius-md);
  padding: 10px;
  background: var(--color-bg-layout);
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.chat-message-row {
  display: flex;
}

.chat-message-row-user {
  justify-content: flex-end;
}

.chat-message-row-assistant {
  justify-content: flex-start;
}

.chat-bubble {
  max-width: 88%;
  border-radius: 10px;
  border: 1px solid var(--color-border);
  padding: 8px 10px;
}

.chat-bubble-user {
  background: #e6f4ff;
  border-color: #91caff;
}

.chat-bubble-assistant {
  background: var(--color-bg-container);
}

.bubble-title {
  font-size: 12px;
  color: var(--color-text-secondary);
  margin-bottom: 4px;
}

.bubble-content {
  margin: 0;
  white-space: pre-wrap;
  word-break: break-word;
  font-size: 12px;
  line-height: 1.6;
  max-height: 260px;
  overflow: auto;
}

.stream-card {
  border: 1px solid var(--color-border);
  border-radius: var(--border-radius-md);
  padding: 8px 10px;
  background: var(--color-bg-container);
}

.stream-title {
  font-size: 12px;
  font-weight: 600;
  color: var(--color-text-secondary);
  margin-bottom: 6px;
}

.stream-title-actions {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.stream-content {
  margin: 0;
  min-height: 120px;
  max-height: 260px;
  overflow: auto;
  white-space: pre-wrap;
  word-break: break-word;
  font-size: 12px;
  line-height: 1.6;
  color: var(--color-text);
}

.test-error-list {
  margin: 0;
  padding-left: 18px;
}

.test-error-list li {
  line-height: 1.6;
}
</style>
