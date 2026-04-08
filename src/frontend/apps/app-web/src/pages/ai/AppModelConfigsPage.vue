<template>
  <div class="mc-page">
    <!-- Left Panel: Model List -->
    <div class="mc-list-panel">
      <div class="mc-list-header">
        <h2 class="mc-list-title">{{ t("modelConfig.listTitle") }}</h2>
        <button class="mc-add-btn" @click="handleCreate">
          <PlusOutlined />
        </button>
      </div>
      <div class="mc-list-search">
        <a-input
          v-model:value="keyword"
          :placeholder="t('modelConfig.searchListPlaceholder')"
          allow-clear
          @change="handleSearch"
        >
          <template #prefix><SearchOutlined style="color: rgba(16,24,40,0.4)" /></template>
        </a-input>
      </div>
      <div class="mc-list-items">
        <a-spin v-if="loading" style="display: flex; justify-content: center; padding: 24px" />
        <div
          v-for="item in dataList"
          :key="item.id"
          :class="['mc-card', { 'mc-card--active': selectedId === item.id }]"
          @click="handleSelect(item)"
        >
          <div :class="['mc-card-icon', { 'mc-card-icon--active': selectedId === item.id }]">
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
              <polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2" />
            </svg>
          </div>
          <div class="mc-card-body">
            <div class="mc-card-top">
              <span class="mc-card-name">{{ item.name }}</span>
              <span v-if="isDefault(item)" class="mc-card-default">{{ t("modelConfig.defaultBadge") }}</span>
            </div>
            <div class="mc-card-provider">{{ providerLabel(item.providerType) }}</div>
            <div class="mc-card-caps">
              <span :class="['mc-status-dot', item.isEnabled ? 'mc-status-dot--on' : 'mc-status-dot--off']" />
              <span class="mc-caps-text">{{ providerCapabilities(item.providerType) }}</span>
            </div>
          </div>
        </div>
        <div v-if="!loading && dataList.length === 0" class="mc-list-empty">
          {{ t("modelConfig.emptyDetail") }}
        </div>
      </div>
    </div>

    <!-- Right Panel: Detail / Edit -->
    <div class="mc-detail-panel">
      <template v-if="selectedId || isCreating">
        <!-- Detail Header -->
        <div class="mc-detail-header">
          <div class="mc-detail-header-left">
            <div class="mc-detail-icon">
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                <polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2" />
              </svg>
            </div>
            <div class="mc-detail-info">
              <div class="mc-detail-name-row">
                <span class="mc-detail-name">{{ form.name || t("modelConfig.newModel") }}</span>
                <span v-if="selectedId" :class="['mc-status-badge', form.isEnabled ? 'mc-status-badge--active' : 'mc-status-badge--inactive']">
                  {{ form.isEnabled ? t("modelConfig.active") : t("modelConfig.inactive") }}
                </span>
              </div>
              <div class="mc-detail-sub">
                {{ t("modelConfig.providerLabel") }}: <strong>{{ providerLabel(form.providerType) }}</strong>
                <template v-if="form.providerType"> | {{ providerCapabilities(form.providerType) }}</template>
              </div>
            </div>
          </div>
          <div class="mc-detail-header-right">
            <a-popconfirm
              v-if="selectedId"
              :title="t('modelConfig.deleteConfirm')"
              :description="t('modelConfig.deleteRiskTip')"
              :ok-text="t('modelConfig.ok')"
              placement="bottomRight"
              @confirm="handleDelete"
            >
              <a-button danger size="small" type="text">
                <template #icon><DeleteOutlined /></template>
              </a-button>
            </a-popconfirm>
            <a-button type="primary" :loading="submitting" class="mc-save-btn" @click="handleSave">
              <template #icon><SaveOutlined /></template>
              {{ t("modelConfig.saveConfig") }}
            </a-button>
          </div>
        </div>

        <!-- Tab Navigation -->
        <div class="mc-tabs-bar">
          <button
            v-for="tab in tabs"
            :key="tab.key"
            :class="['mc-tab', { 'mc-tab--active': activeTab === tab.key }]"
            @click="activeTab = tab.key"
          >
            <component :is="tab.icon" style="font-size: 16px" />
            {{ tab.label }}
          </button>
        </div>

        <!-- Tab Content (scrollable) -->
        <div class="mc-tab-content">
          <!-- Basic Settings Tab -->
          <template v-if="activeTab === 'basic'">
            <a-form ref="formRef" :model="form" layout="vertical" :rules="currentRules">
              <!-- API Auth Card -->
              <div class="mc-section-card">
                <h3 class="mc-section-title">{{ t("modelConfig.apiAuthTitle") }}</h3>

                <template v-if="isCreating">
                  <a-form-item :label="t('modelConfig.labelName')" name="name">
                    <a-input v-model:value="form.name" :placeholder="t('modelConfig.namePlaceholder')" />
                  </a-form-item>
                  <a-form-item :label="t('modelConfig.labelProvider')" name="providerType">
                    <a-select v-model:value="form.providerType" :options="providerOptions" @change="onProviderChange" />
                  </a-form-item>
                </template>

                <a-form-item :label="t('modelConfig.labelBaseUrl')" name="baseUrl">
                  <a-input v-model:value="form.baseUrl" class="mc-input" />
                </a-form-item>

                <a-form-item :label="t('modelConfig.labelApiKey')" name="apiKey">
                  <a-input-password v-model:value="form.apiKey" class="mc-input" :placeholder="selectedId ? t('modelConfig.apiKeyPlaceholderEdit') : t('modelConfig.apiKeyPlaceholderCreate')" />
                </a-form-item>
                <div class="mc-hint">{{ t("modelConfig.apiKeyHintSecurity") }}</div>

                <button class="mc-test-btn" :disabled="testing" @click="handleTestConnection">
                  <ThunderboltOutlined />
                  {{ testing ? "..." : t("modelConfig.testConnectivity") }}
                </button>
                <a-alert
                  v-if="testResult"
                  :type="testResult.success ? 'success' : 'error'"
                  :message="testResult.success ? t('modelConfig.connectOk') : t('modelConfig.connectFail')"
                  show-icon
                  closable
                  style="margin-top: 12px"
                  @close="testResult = null"
                >
                  <template #description>
                    <span v-if="testResult.success">{{ t("modelConfig.latency", { ms: testResult.latencyMs ?? 0 }) }}</span>
                    <span v-else>{{ testResult.errorMessage || t("modelConfig.connectUnreachable") }}</span>
                  </template>
                </a-alert>
              </div>

              <!-- Default Model Card -->
              <div v-if="isCreating" class="mc-section-card">
                <h3 class="mc-section-title">{{ t("modelConfig.labelDefaultModel") }}</h3>
                <a-form-item name="defaultModel">
                  <a-auto-complete
                    v-model:value="form.defaultModel"
                    :options="suggestedModels"
                    :placeholder="t('modelConfig.defaultModelPlaceholder')"
                    :filter-option="filterModelOption"
                    class="mc-input"
                  />
                </a-form-item>
              </div>

              <!-- System Prompt Card -->
              <div class="mc-section-card">
                <h3 class="mc-section-title">{{ t("modelConfig.systemPromptTitle") }}</h3>
                <a-textarea
                  v-model:value="form.systemPrompt"
                  :rows="5"
                  :placeholder="t('modelConfig.systemPromptPlaceholder')"
                  class="mc-textarea"
                />
              </div>
            </a-form>
          </template>

          <!-- Advanced Tab -->
          <template v-if="activeTab === 'advanced'">
            <a-form layout="vertical">
              <div class="mc-section-card">
                <h3 class="mc-section-title">{{ t("modelConfig.sectionFeatures") }}</h3>
                <a-form-item v-if="isCreating" :label="t('modelConfig.labelName')" style="margin-bottom: 16px">
                  <a-input v-model:value="form.name" :placeholder="t('modelConfig.namePlaceholder')" />
                </a-form-item>
                <a-row :gutter="24">
                  <a-col :span="12">
                    <a-form-item :label="t('modelConfig.labelEmbedding')">
                      <a-switch v-model:checked="form.supportsEmbedding" />
                      <span class="mc-switch-label">{{ form.supportsEmbedding ? t("modelConfig.switchOn") : t("modelConfig.switchOff") }}</span>
                    </a-form-item>
                  </a-col>
                  <a-col v-if="selectedId" :span="12">
                    <a-form-item :label="t('modelConfig.labelEnabled')">
                      <a-switch v-model:checked="form.isEnabled" />
                      <span class="mc-switch-label">{{ form.isEnabled ? t("modelConfig.enabledOn") : t("modelConfig.enabledOff") }}</span>
                    </a-form-item>
                  </a-col>
                  <a-col :span="12">
                    <a-form-item :label="t('modelConfig.labelStreamingTypewriter')">
                      <a-switch v-model:checked="form.enableStreamingTypewriter" />
                      <span class="mc-switch-label">{{ form.enableStreamingTypewriter ? t("modelConfig.switchOn") : t("modelConfig.switchOff") }}</span>
                    </a-form-item>
                  </a-col>
                </a-row>
                <a-form-item v-if="!isCreating" :label="t('modelConfig.labelDefaultModel')">
                  <a-auto-complete
                    v-model:value="form.defaultModel"
                    :options="suggestedModels"
                    :placeholder="t('modelConfig.defaultModelPlaceholder')"
                    :filter-option="filterModelOption"
                    class="mc-input"
                  />
                </a-form-item>
              </div>
            </a-form>
          </template>

          <!-- Test Tab -->
          <template v-if="activeTab === 'test'">
            <div class="mc-section-card">
              <h3 class="mc-section-title">{{ t("modelConfig.sectionTest") }}</h3>
              <p class="mc-hint">{{ t("modelConfig.testConnectivity") }}</p>
              <a-button :loading="testing" type="primary" @click="handleTestConnection">
                <template #icon><ApiOutlined /></template>
                {{ t("modelConfig.testConnection") }}
              </a-button>
              <a-alert
                v-if="testResult"
                :type="testResult.success ? 'success' : 'error'"
                :message="testResult.success ? t('modelConfig.connectOk') : t('modelConfig.connectFail')"
                show-icon
                closable
                style="margin-top: 12px"
                @close="testResult = null"
              >
                <template #description>
                  <span v-if="testResult.success">{{ t("modelConfig.latency", { ms: testResult.latencyMs ?? 0 }) }}</span>
                  <span v-else>{{ testResult.errorMessage || t("modelConfig.connectUnreachable") }}</span>
                </template>
              </a-alert>
            </div>
          </template>
        </div>
      </template>

      <!-- Empty State -->
      <template v-else>
        <div class="mc-empty-state">
          <div class="mc-empty-icon">
            <svg width="40" height="40" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">
              <polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2" />
            </svg>
          </div>
          <p class="mc-empty-text">{{ t("modelConfig.emptyDetail") }}</p>
        </div>
      </template>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, reactive, ref } from "vue";
import { useI18n } from "vue-i18n";
import type { FormInstance } from "ant-design-vue";
import { message } from "ant-design-vue";
import {
  PlusOutlined,
  SearchOutlined,
  DeleteOutlined,
  SaveOutlined,
  SettingOutlined,
  SlidersOutlined,
  PlayCircleOutlined,
  ThunderboltOutlined,
  ApiOutlined
} from "@ant-design/icons-vue";
import {
  createModelConfig,
  deleteModelConfig,
  getModelConfigUiPreferences,
  getModelConfigById,
  getModelConfigsPaged,
  setModelConfigUiPreferences,
  testModelConfigConnection,
  updateModelConfig,
  type ModelConfigDto,
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
const selectedId = ref<number | null>(null);
const isCreating = ref(false);
const activeTab = ref<"basic" | "advanced" | "test">("basic");
const formRef = ref<FormInstance>();

const form = reactive({
  name: "",
  providerType: "openai",
  apiKey: "",
  baseUrl: "",
  defaultModel: "",
  supportsEmbedding: true,
  enableStreamingTypewriter: false,
  isEnabled: true,
  systemPrompt: ""
});

const tabs = computed(() => [
  { key: "basic" as const, label: t("modelConfig.tabBasic"), icon: SettingOutlined },
  { key: "advanced" as const, label: t("modelConfig.tabAdvanced"), icon: SlidersOutlined },
  { key: "test" as const, label: t("modelConfig.tabTest"), icon: PlayCircleOutlined }
]);

const providerOptions = [
  { label: "OpenAI", value: "openai" },
  { label: "DeepSeek", value: "deepseek" },
  { label: "Anthropic", value: "anthropic" },
  { label: "Ollama", value: "ollama" },
  { label: "Custom", value: "custom" }
];

const providerModelSuggestions: Record<string, string[]> = {
  openai: ["gpt-4o", "gpt-4o-mini", "gpt-4-turbo", "gpt-3.5-turbo", "text-embedding-3-small"],
  deepseek: ["deepseek-chat", "deepseek-coder", "deepseek-reasoner"],
  anthropic: ["claude-3.5-sonnet", "claude-3-opus", "claude-3-haiku"],
  ollama: ["llama3", "mistral", "codellama", "nomic-embed-text", "qwen2.5:72b"],
  custom: []
};

const providerBaseUrls: Record<string, string> = {
  openai: "https://api.openai.com/v1",
  deepseek: "https://api.deepseek.com/v1",
  anthropic: "https://api.anthropic.com/v1",
  ollama: "http://localhost:11434/v1",
  custom: ""
};

const providerCaps: Record<string, string> = {
  openai: "Chat, Vision, Tool",
  deepseek: "Chat, Reasoning",
  anthropic: "Chat, Coding, Vision",
  ollama: "Chat, Open Source",
  custom: "Chat"
};

function providerLabel(type: string): string {
  const map: Record<string, string> = {
    openai: "OpenAI", deepseek: "DeepSeek", anthropic: "Anthropic",
    ollama: "Local/vLLM", custom: "Custom"
  };
  return map[type] ?? type;
}

function providerCapabilities(type: string): string {
  return providerCaps[type] ?? "Chat";
}

function isDefault(item: ModelConfigDto): boolean {
  return dataList.value.indexOf(item) === 0 && item.isEnabled;
}

const suggestedModels = computed(() =>
  (providerModelSuggestions[form.providerType] ?? []).map((m) => ({ value: m }))
);

function filterModelOption(input: string, option: { value: string }) {
  return option.value.toLowerCase().includes(input.toLowerCase());
}

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

const currentRules = computed(() => (isCreating.value ? createRules.value : baseRules.value));

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
    const result = await getModelConfigsPaged({
      pageIndex: 1,
      pageSize: 100,
      keyword: keyword.value || undefined
    });
    if (!isMounted.value) return;
    dataList.value = result.items;
  } catch (error: unknown) {
    message.error((error as Error).message || t("crud.queryFailed"));
  } finally {
    loading.value = false;
  }
}

let searchTimer: ReturnType<typeof setTimeout> | null = null;
function handleSearch() {
  if (searchTimer) clearTimeout(searchTimer);
  searchTimer = setTimeout(() => { void loadData(); }, 300);
}

function resetForm() {
  Object.assign(form, {
    name: "", providerType: "openai", apiKey: "", baseUrl: "",
    defaultModel: "", supportsEmbedding: true, enableStreamingTypewriter: false, isEnabled: true, systemPrompt: ""
  });
  testResult.value = null;
}

function handleCreate() {
  selectedId.value = null;
  isCreating.value = true;
  activeTab.value = "basic";
  resetForm();
}

async function handleSelect(item: ModelConfigDto) {
  if (selectedId.value === item.id && !isCreating.value) return;
  isCreating.value = false;
  selectedId.value = item.id;
  activeTab.value = "basic";
  testResult.value = null;

  try {
    const detail = await getModelConfigById(item.id);
    if (!isMounted.value) return;
    Object.assign(form, {
      name: detail.name,
      providerType: detail.providerType,
      apiKey: "",
      baseUrl: detail.baseUrl,
      defaultModel: detail.defaultModel,
      supportsEmbedding: detail.supportsEmbedding,
      enableStreamingTypewriter: getModelConfigUiPreferences(item.id).enableStreamingTypewriter ?? false,
      isEnabled: detail.isEnabled,
      systemPrompt: ""
    });
  } catch (error: unknown) {
    message.error((error as Error).message || t("crud.queryFailed"));
  }
}

async function handleSave() {
  if (submitting.value) return;

  if (activeTab.value === "basic" && formRef.value) {
    try {
      await formRef.value.validate();
      if (!isMounted.value) return;
    } catch { return; }
  }

  submitting.value = true;
  try {
    let savedId = selectedId.value;
    if (isCreating.value) {
      const createdId = await createModelConfig({
        name: form.name,
        providerType: form.providerType,
        apiKey: form.apiKey,
        baseUrl: form.baseUrl,
        defaultModel: form.defaultModel,
        supportsEmbedding: form.supportsEmbedding
      });
      savedId = Number(createdId);
      if (Number.isNaN(savedId)) {
        savedId = null;
      }
      if (!isMounted.value) return;
      message.success(t("crud.createSuccess"));
      isCreating.value = false;
    } else if (selectedId.value) {
      await updateModelConfig(selectedId.value, {
        name: form.name,
        apiKey: form.apiKey,
        baseUrl: form.baseUrl,
        defaultModel: form.defaultModel,
        isEnabled: form.isEnabled,
        supportsEmbedding: form.supportsEmbedding
      });
      if (!isMounted.value) return;
      message.success(t("crud.updateSuccess"));
    }

    if (savedId !== null) {
      setModelConfigUiPreferences(savedId, {
        enableStreamingTypewriter: form.enableStreamingTypewriter
      });
    }

    await loadData();
    if (!isMounted.value) return;

    if (!isCreating.value && dataList.value.length > 0) {
      const match = dataList.value.find((d) => d.id === savedId);
      if (match) {
        selectedId.value = match.id;
        await handleSelect(match);
      } else {
        await handleSelect(dataList.value[0]);
      }
    }
  } catch (error: unknown) {
    message.error((error as Error).message || t("crud.submitFailed"));
  } finally {
    submitting.value = false;
  }
}

async function handleDelete() {
  if (!selectedId.value) return;
  try {
    await deleteModelConfig(selectedId.value);
    if (!isMounted.value) return;
    message.success(t("crud.deleteSuccess"));
    selectedId.value = null;
    isCreating.value = false;
    resetForm();
    await loadData();
    if (!isMounted.value) return;
    if (dataList.value.length > 0) {
      await handleSelect(dataList.value[0]);
    }
  } catch (error: unknown) {
    message.error((error as Error).message || t("crud.deleteFailed"));
  }
}

async function handleTestConnection() {
  testing.value = true;
  testResult.value = null;
  try {
    testResult.value = await testModelConfigConnection({
      modelConfigId: selectedId.value ?? undefined,
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

onMounted(async () => {
  await loadData();
  if (!isMounted.value) return;
  if (dataList.value.length > 0) {
    await handleSelect(dataList.value[0]);
  }
});
</script>

<style scoped>
.mc-page {
  display: flex;
  gap: 24px;
  height: calc(100vh - 96px);
  padding: 32px;
  background: #f4f7f9;
}

/* ── Left Panel ── */
.mc-list-panel {
  width: 320px;
  min-width: 320px;
  background: #fff;
  border: 0.8px solid rgba(229, 231, 235, 0.8);
  border-radius: 16px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1), 0 1px 2px -1px rgba(0, 0, 0, 0.1);
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.mc-list-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px;
  background: rgba(249, 250, 251, 0.3);
  border-bottom: 0.8px solid rgba(243, 244, 246, 0.8);
}

.mc-list-title {
  font-size: 20px;
  font-weight: 700;
  color: #101828;
  margin: 0;
}

.mc-add-btn {
  width: 28px;
  height: 28px;
  border-radius: 8px;
  border: none;
  background: transparent;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #6a7282;
  transition: all 0.15s;
}

.mc-add-btn:hover {
  background: #f3f4f6;
  color: #4f39f6;
}

.mc-list-search {
  padding: 12px;
  border-bottom: 0.8px solid rgba(243, 244, 246, 0.8);
}

.mc-list-search :deep(.ant-input-affix-wrapper) {
  border-radius: 10px;
  background: #f9fafb;
  border-color: transparent;
}

.mc-list-items {
  flex: 1;
  overflow-y: auto;
  padding: 8px;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.mc-card {
  display: flex;
  gap: 12px;
  padding: 12.8px;
  border-radius: 14px;
  border: 0.8px solid transparent;
  cursor: pointer;
  transition: all 0.15s;
}

.mc-card:hover {
  background: #f9fafb;
}

.mc-card--active {
  background: rgba(238, 242, 255, 0.5);
  border-color: #c6d2ff;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1), 0 1px 2px rgba(0, 0, 0, 0.1);
}

.mc-card-icon {
  width: 40px;
  height: 40px;
  border-radius: 10px;
  background: #f3f4f6;
  border: 0.8px solid #e5e7eb;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  color: #6a7282;
}

.mc-card-icon--active {
  background: #4f39f6;
  border-color: #432dd7;
  color: #fff;
}

.mc-card-body {
  flex: 1;
  min-width: 0;
}

.mc-card-top {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.mc-card-name {
  font-size: 14px;
  font-weight: 600;
  color: #101828;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.mc-card--active .mc-card-name {
  color: #312c85;
}

.mc-card-default {
  background: #e0e7ff;
  color: #432dd7;
  font-size: 10px;
  font-weight: 700;
  padding: 2px 6px;
  border-radius: 4px;
  flex-shrink: 0;
}

.mc-card-provider {
  font-size: 12px;
  font-weight: 500;
  color: #6a7282;
  margin-top: 2px;
}

.mc-card-caps {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-top: 4px;
}

.mc-status-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  flex-shrink: 0;
}

.mc-status-dot--on { background: #00bc7d; }
.mc-status-dot--off { background: #99a1af; }

.mc-caps-text {
  font-size: 11px;
  font-weight: 500;
  color: #99a1af;
}

.mc-list-empty {
  padding: 32px 16px;
  text-align: center;
  color: #9ca3af;
  font-size: 13px;
}

/* ── Right Panel ── */
.mc-detail-panel {
  flex: 1;
  min-width: 0;
  background: #fff;
  border: 0.8px solid rgba(229, 231, 235, 0.8);
  border-radius: 16px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1), 0 1px 2px -1px rgba(0, 0, 0, 0.1);
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.mc-detail-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 20px 24px;
  border-bottom: 0.8px solid #f3f4f6;
  flex-shrink: 0;
}

.mc-detail-header-left {
  display: flex;
  gap: 16px;
  align-items: center;
}

.mc-detail-icon {
  width: 48px;
  height: 48px;
  border-radius: 14px;
  background: #4f39f6;
  border: 0.8px solid rgba(67, 45, 215, 0.2);
  box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1), 0 2px 4px rgba(0, 0, 0, 0.1);
  display: flex;
  align-items: center;
  justify-content: center;
  color: #fff;
  flex-shrink: 0;
}

.mc-detail-info {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.mc-detail-name-row {
  display: flex;
  align-items: center;
  gap: 12px;
}

.mc-detail-name {
  font-size: 20px;
  font-weight: 700;
  color: #101828;
  letter-spacing: -0.5px;
}

.mc-status-badge {
  font-size: 10px;
  font-weight: 700;
  padding: 2.8px 8.8px;
  border-radius: 8px;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.mc-status-badge--active {
  background: #ecfdf5;
  border: 0.8px solid rgba(164, 244, 207, 0.6);
  color: #007a55;
}

.mc-status-badge--inactive {
  background: #f3f4f6;
  border: 0.8px solid #e5e7eb;
  color: #6a7282;
}

.mc-detail-sub {
  font-size: 14px;
  color: #6a7282;
}

.mc-detail-sub strong {
  color: #364153;
  font-weight: 500;
}

.mc-detail-header-right {
  display: flex;
  align-items: center;
  gap: 8px;
}

.mc-save-btn {
  background: #4f39f6;
  border-color: transparent;
  border-radius: 10px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1), 0 1px 2px rgba(0, 0, 0, 0.1);
}

.mc-save-btn:hover {
  background: #432dd7 !important;
}

/* ── Tabs ── */
.mc-tabs-bar {
  display: flex;
  gap: 4px;
  padding: 0 24px 16px;
  flex-shrink: 0;
}

.mc-tabs-bar {
  padding: 12px 24px;
  background: transparent;
}

.mc-tab {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 20px;
  border-radius: 8px;
  border: none;
  background: transparent;
  font-size: 14px;
  font-weight: 500;
  color: #6a7282;
  cursor: pointer;
  transition: all 0.15s;
  height: 32px;
}

.mc-tab:hover {
  background: rgba(243, 244, 246, 0.5);
}

.mc-tab--active {
  background: #fff;
  color: #101828;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1), 0 1px 2px rgba(0, 0, 0, 0.1);
}

/* Tab bar background strip */
.mc-tabs-bar {
  background: rgba(243, 244, 246, 0.8);
  border-radius: 10px;
  margin: 0 24px 0;
  padding: 4px;
}

/* ── Tab Content ── */
.mc-tab-content {
  flex: 1;
  overflow-y: auto;
  padding: 24px;
  background: rgba(249, 250, 251, 0.3);
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.mc-section-card {
  background: #fff;
  border: 0.8px solid rgba(229, 231, 235, 0.6);
  border-radius: 14px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1), 0 1px 2px rgba(0, 0, 0, 0.1);
  padding: 20px;
}

.mc-section-title {
  font-size: 18px;
  font-weight: 600;
  color: #101828;
  margin: 0 0 16px;
  padding-bottom: 12px;
  border-bottom: 0.8px solid #f3f4f6;
}

.mc-input {
  border-radius: 10px;
}

.mc-input :deep(.ant-input) {
  border-radius: 10px;
}

.mc-textarea {
  border-radius: 10px;
  background: rgba(249, 250, 251, 0.5);
}

.mc-hint {
  font-size: 12px;
  color: #6a7282;
  margin-bottom: 16px;
}

.mc-test-btn {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 8px 16px;
  border-radius: 10px;
  background: #ecfdf5;
  border: 0.8px solid #a4f4cf;
  color: #007a55;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1), 0 1px 2px rgba(0, 0, 0, 0.1);
  transition: all 0.15s;
}

.mc-test-btn:hover {
  background: #d1fae5;
}

.mc-test-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.mc-switch-label {
  margin-left: 8px;
  color: #6a7282;
  font-size: 13px;
}

/* ── Empty State ── */
.mc-empty-state {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 16px;
}

.mc-empty-icon {
  width: 72px;
  height: 72px;
  border-radius: 20px;
  background: #f3f4f6;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #9ca3af;
}

.mc-empty-text {
  font-size: 14px;
  color: #9ca3af;
  margin: 0;
}
</style>
