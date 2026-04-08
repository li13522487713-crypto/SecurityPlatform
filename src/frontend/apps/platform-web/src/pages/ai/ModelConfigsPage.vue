<template>
  <div class="model-configs-page">
    <!-- Left Panel: Provider & Model Tree -->
    <div class="left-panel">
      <div class="left-header">
        <h2 class="left-title">{{ t("ai.modelConfig.providerPanelTitle") }}</h2>
        <a-button type="text" size="small" class="add-btn" @click="openCreateDrawer">
          <template #icon><PlusOutlined /></template>
        </a-button>
      </div>
      <div class="search-box">
        <a-input
          v-model:value="searchKeyword"
          :placeholder="t('ai.modelConfig.providerSearchPlaceholder')"
          allow-clear
          size="small"
        >
          <template #prefix><SearchOutlined style="color: rgba(16,24,40,0.5)" /></template>
        </a-input>
      </div>
      <div class="tree-list">
        <div v-for="group in filteredProviderGroups" :key="group.providerType" class="provider-group">
          <div
            class="provider-row"
            :class="{ 'provider-row--selected': selectedType === 'provider' && selectedProviderType === group.providerType }"
            @click="selectProvider(group.providerType)"
          >
            <a-button type="text" size="small" class="expand-btn" @click.stop="toggleExpand(group.providerType)">
              <template #icon><RightOutlined :class="{ 'expand-icon--open': expandedProviders.has(group.providerType) }" /></template>
            </a-button>
            <div class="provider-icon-wrapper">
              <component :is="providerMeta(group.providerType).icon" />
            </div>
            <span class="provider-name">{{ group.label }}</span>
            <span class="provider-status-dot" :class="group.hasEnabledModels ? 'dot--online' : 'dot--offline'" />
          </div>
          <div v-if="expandedProviders.has(group.providerType)" class="model-list">
            <div
              v-for="model in group.models"
              :key="model.id"
              class="model-row"
              :class="{ 'model-row--selected': selectedType === 'model' && selectedModelId === model.id }"
              @click="selectModel(model)"
            >
              <span class="model-dot" :class="model.isEnabled ? 'dot--online' : 'dot--offline'" />
              <span class="model-name">{{ model.name }}</span>
              <a-tag v-if="isDefaultModel(model, group)" color="purple" class="default-tag">{{ t("ai.modelConfig.badgeDefault") }}</a-tag>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Right Panel -->
    <div class="right-panel">
      <!-- Provider View -->
      <template v-if="selectedType === 'provider' && currentProviderGroup">
        <div class="detail-header">
          <div class="detail-header-left">
            <div class="detail-icon provider-detail-icon">
              <component :is="providerMeta(currentProviderGroup.providerType).icon" />
            </div>
            <div class="detail-header-info">
              <div class="detail-header-title-row">
                <h2 class="detail-title">{{ currentProviderGroup.label }} {{ t("ai.modelConfig.providerConfigTitle", { name: "" }).replace(" ", "") }}</h2>
                <a-tag color="green" class="active-badge">{{ t("ai.modelConfig.badgeActive") }}</a-tag>
              </div>
              <p class="detail-desc">{{ t("ai.modelConfig.providerConfigDesc") }}</p>
            </div>
          </div>
          <a-button type="primary" :loading="providerSaving" @click="saveProviderConfig">
            <template #icon><SaveOutlined /></template>
            {{ t("ai.modelConfig.saveConfig") }}
          </a-button>
        </div>

        <div class="detail-body">
          <div class="config-section">
            <h3 class="section-title">{{ t("ai.modelConfig.providerApiAuth") }}</h3>
            <a-form layout="vertical">
              <a-form-item :label="t('ai.modelConfig.providerApiBaseUrl')">
                <a-input v-model:value="providerForm.baseUrl" :placeholder="providerBaseUrls[currentProviderGroup.providerType]" />
              </a-form-item>
              <a-form-item :label="t('ai.modelConfig.providerApiKey')">
                <a-input-password v-model:value="providerForm.apiKey" :placeholder="t('ai.modelConfig.apiKeyPlaceholderEdit')" />
              </a-form-item>
            </a-form>
            <a-button type="default" :loading="providerTesting" class="test-conn-btn" @click="handleProviderTestConnection">
              <template #icon><ThunderboltOutlined /></template>
              {{ t("ai.modelConfig.providerTestConnection") }}
            </a-button>
            <a-alert
              v-if="providerTestResult"
              :type="providerTestResult.success ? 'success' : 'error'"
              :message="providerTestResult.success ? t('ai.modelConfig.connectOk') : t('ai.modelConfig.connectFail')"
              show-icon
              closable
              style="margin-top: 12px"
              @close="providerTestResult = null"
            />
          </div>

          <div class="config-section">
            <div class="section-header-row">
              <h3 class="section-title">{{ t("ai.modelConfig.providerSupportedModels") }} ({{ currentProviderGroup.models.length }})</h3>
              <a-button type="link" size="small" @click="openCreateDrawer">
                <template #icon><PlusOutlined /></template>
                {{ t("ai.modelConfig.providerAddModel") }}
              </a-button>
            </div>
            <div class="model-cards">
              <div
                v-for="model in currentProviderGroup.models"
                :key="model.id"
                class="model-card"
                @click="selectModel(model)"
              >
                <div class="model-card-info">
                  <span class="model-card-name">{{ model.name }}</span>
                  <a-tag v-if="isDefaultModel(model, currentProviderGroup)" color="purple" size="small">{{ t("ai.modelConfig.badgeDefault") }}</a-tag>
                </div>
                <div class="model-card-caps">
                  <span class="cap-label">{{ t("ai.modelConfig.capabilityChat") }}</span>
                  <span v-if="model.enableReasoning" class="cap-label">, {{ t("ai.modelConfig.capabilityReasoning") }}</span>
                  <span v-if="model.enableVision" class="cap-label">, {{ t("ai.modelConfig.capabilityVision") }}</span>
                  <span v-if="model.supportsEmbedding" class="cap-label">, {{ t("ai.modelConfig.capabilityEmbedding") }}</span>
                </div>
                <RightOutlined class="model-card-arrow" />
              </div>
            </div>
          </div>
        </div>
      </template>

      <!-- Model Detail View -->
      <template v-else-if="selectedType === 'model' && currentModel">
        <div class="detail-header">
          <div class="detail-header-left">
            <div class="detail-icon model-detail-icon">
              <component :is="providerMeta(currentModel.providerType).icon" />
            </div>
            <div class="detail-header-info">
              <div class="detail-header-title-row">
                <h2 class="detail-title">{{ currentModel.name }}</h2>
                <a-tag color="green" class="active-badge">{{ t("ai.modelConfig.badgeActive") }}</a-tag>
              </div>
              <p class="detail-desc">
                {{ t("ai.modelConfig.modelDetailProvider", { provider: providerMeta(currentModel.providerType).label }) }}
                <span class="cap-separator">|</span>
                <span class="cap-label">{{ t("ai.modelConfig.capabilityChat") }}</span>
                <span v-if="currentModel.enableReasoning" class="cap-label">, {{ t("ai.modelConfig.capabilityReasoning") }}</span>
              </p>
            </div>
          </div>
          <div class="detail-header-actions">
            <a-popconfirm :title="t('ai.modelConfig.deleteModelConfirm')" @confirm="handleDeleteModel(currentModel.id)">
              <a-button type="text" danger size="small">
                <template #icon><DeleteOutlined /></template>
              </a-button>
            </a-popconfirm>
            <a-button type="primary" :loading="modelSaving" @click="saveModelConfig">
              <template #icon><SaveOutlined /></template>
              {{ t("ai.modelConfig.saveConfig") }}
            </a-button>
          </div>
        </div>

        <a-tabs v-model:activeKey="activeTab" class="model-tabs">
          <!-- Tab 1: Basic Info -->
          <a-tab-pane key="basic" :tab="tabLabel('basic')">
            <div class="tab-content">
              <h3 class="section-title">{{ t("ai.modelConfig.modelBasicSettings") }}</h3>
              <a-form layout="vertical">
                <a-row :gutter="16">
                  <a-col :span="12">
                    <a-form-item :label="t('ai.modelConfig.labelDisplayName')">
                      <a-input v-model:value="modelForm.name" />
                    </a-form-item>
                  </a-col>
                  <a-col :span="12">
                    <a-form-item :label="t('ai.modelConfig.labelModelId')">
                      <a-input v-model:value="modelForm.modelId" />
                    </a-form-item>
                  </a-col>
                </a-row>
                <a-form-item :label="t('ai.modelConfig.labelSystemPrompt')">
                  <a-textarea
                    v-model:value="modelForm.systemPrompt"
                    :rows="6"
                    :placeholder="t('ai.modelConfig.systemPromptPlaceholder')"
                  />
                </a-form-item>
              </a-form>
            </div>
          </a-tab-pane>

          <!-- Tab 2: Advanced -->
          <a-tab-pane key="advanced" :tab="tabLabel('advanced')">
            <div class="tab-content tab-content-scroll">
              <h3 class="section-title">{{ t("ai.modelConfig.sectionFeaturesStatus") }}</h3>
              <a-row :gutter="16" style="margin-bottom: 24px">
                <a-col :span="8">
                  <div class="status-item">
                    <span class="status-label">{{ t("ai.modelConfig.labelEmbedding") }}</span>
                    <a-switch v-model:checked="modelForm.supportsEmbedding" />
                    <span class="status-value">{{ modelForm.supportsEmbedding ? t("ai.modelConfig.switchOn") : t("ai.modelConfig.switchOff") }}</span>
                  </div>
                </a-col>
                <a-col :span="8">
                  <div class="status-item">
                    <span class="status-label">{{ t("ai.modelConfig.labelEnabled") }}</span>
                    <a-switch v-model:checked="modelForm.isEnabled" />
                    <span class="status-value">{{ modelForm.isEnabled ? t("ai.modelConfig.enabledOn") : t("ai.modelConfig.enabledOff") }}</span>
                  </div>
                </a-col>
                <a-col :span="8">
                  <div class="status-item">
                    <span class="status-label">{{ t("ai.modelConfig.labelDefaultModel") }}</span>
                    <a-auto-complete
                      v-model:value="modelForm.defaultModel"
                      :options="suggestedModels"
                      size="small"
                      style="width: 100%"
                    />
                  </div>
                </a-col>
              </a-row>

              <h3 class="section-title section-title-icon">
                {{ t("ai.modelConfig.sectionFeatureToggles") }}
              </h3>
              <div class="feature-toggles-grid">
                <div class="feature-card" :class="{ 'feature-card--active': modelForm.enableReasoning }">
                  <div class="feature-card-header">
                    <div class="feature-card-icon" :class="modelForm.enableReasoning ? 'fci--active' : ''">
                      <BulbOutlined />
                    </div>
                    <div class="feature-card-title">{{ t("ai.modelConfig.toggleCoT") }}<br>{{ t("ai.modelConfig.toggleCoTSuffix") }}</div>
                    <a-switch v-model:checked="modelForm.enableReasoning" />
                  </div>
                  <p class="feature-card-desc">{{ t("ai.modelConfig.toggleCoTDesc") }}</p>
                </div>

                <div class="feature-card" :class="{ 'feature-card--active': modelForm.enableTools }">
                  <div class="feature-card-header">
                    <div class="feature-card-icon" :class="modelForm.enableTools ? 'fci--active' : ''">
                      <ToolOutlined />
                    </div>
                    <div class="feature-card-title">{{ t("ai.modelConfig.toggleTools") }}<br>{{ t("ai.modelConfig.toggleToolsSuffix") }}</div>
                    <a-switch v-model:checked="modelForm.enableTools" />
                  </div>
                  <p class="feature-card-desc">{{ t("ai.modelConfig.toggleToolsDesc") }}</p>
                </div>

                <div class="feature-card" :class="{ 'feature-card--active': modelForm.enableVision }">
                  <div class="feature-card-header">
                    <div class="feature-card-icon" :class="modelForm.enableVision ? 'fci--active' : ''">
                      <EyeOutlined />
                    </div>
                    <div class="feature-card-title">{{ t("ai.modelConfig.toggleVision") }}<br>{{ t("ai.modelConfig.toggleVisionSuffix") }}</div>
                    <a-switch v-model:checked="modelForm.enableVision" />
                  </div>
                  <p class="feature-card-desc">{{ t("ai.modelConfig.toggleVisionDesc") }}</p>
                </div>

                <div class="feature-card" :class="{ 'feature-card--active': modelForm.enableJsonMode }">
                  <div class="feature-card-header">
                    <div class="feature-card-icon" :class="modelForm.enableJsonMode ? 'fci--active' : ''">
                      <CodeOutlined />
                    </div>
                    <div class="feature-card-title">{{ t("ai.modelConfig.toggleJsonMode") }}<br>{{ t("ai.modelConfig.toggleJsonModeSuffix") }}</div>
                    <a-switch v-model:checked="modelForm.enableJsonMode" />
                  </div>
                  <p class="feature-card-desc">{{ t("ai.modelConfig.toggleJsonModeDesc") }}</p>
                </div>

                <div class="feature-card" :class="{ 'feature-card--active': modelForm.enableStreaming }">
                  <div class="feature-card-header">
                    <div class="feature-card-icon" :class="modelForm.enableStreaming ? 'fci--active' : ''">
                      <ThunderboltOutlined />
                    </div>
                    <div class="feature-card-title">{{ t("ai.modelConfig.toggleStreaming") }}<br>{{ t("ai.modelConfig.toggleStreamingSuffix") }}</div>
                    <a-switch v-model:checked="modelForm.enableStreaming" />
                  </div>
                  <p class="feature-card-desc">{{ t("ai.modelConfig.toggleStreamingDesc") }}</p>
                </div>
              </div>

              <h3 class="section-title section-title-icon" style="margin-top: 32px">
                {{ t("ai.modelConfig.sectionParameters") }}
              </h3>
              <div class="params-grid">
                <div class="param-item">
                  <span class="param-label">{{ t("ai.modelConfig.paramTemperature") }}</span>
                  <a-slider v-model:value="temperatureValue" :min="0" :max="200" :step="1" style="flex: 1" />
                  <a-input-number v-model:value="temperatureValue" :min="0" :max="200" size="small" style="width: 70px" />
                </div>
                <div class="param-item">
                  <span class="param-label">{{ t("ai.modelConfig.paramMaxTokens") }}</span>
                  <a-input-number v-model:value="modelForm.maxTokens" :min="1" :max="1000000" size="small" style="width: 120px" />
                </div>
                <div class="param-item">
                  <span class="param-label">{{ t("ai.modelConfig.paramTopP") }}</span>
                  <a-slider v-model:value="topPValue" :min="0" :max="100" :step="1" style="flex: 1" />
                  <a-input-number v-model:value="topPValue" :min="0" :max="100" size="small" style="width: 70px" />
                </div>
                <div class="param-item">
                  <span class="param-label">{{ t("ai.modelConfig.paramFrequencyPenalty") }}</span>
                  <a-slider v-model:value="frequencyPenaltyValue" :min="-200" :max="200" :step="1" style="flex: 1" />
                  <a-input-number v-model:value="frequencyPenaltyValue" :min="-200" :max="200" size="small" style="width: 70px" />
                </div>
                <div class="param-item">
                  <span class="param-label">{{ t("ai.modelConfig.paramPresencePenalty") }}</span>
                  <a-slider v-model:value="presencePenaltyValue" :min="-200" :max="200" :step="1" style="flex: 1" />
                  <a-input-number v-model:value="presencePenaltyValue" :min="-200" :max="200" size="small" style="width: 70px" />
                </div>
              </div>
            </div>
          </a-tab-pane>

          <!-- Tab 3: Model Test -->
          <a-tab-pane key="test" :tab="tabLabel('test')">
            <div class="tab-content tab-test">
              <div class="debug-console">
                <div class="debug-console-header">
                  <span class="debug-console-title">
                    <CaretRightOutlined style="margin-right: 6px" />
                    {{ t("ai.modelConfig.debugConsoleTitle", { name: currentModel.name }) }}
                  </span>
                  <div class="debug-console-stats">
                    <span class="stat-item stat-latency">{{ t("ai.modelConfig.debugConsoleLatency") }} <span class="stat-val">{{ testLatency ?? "-" }}</span></span>
                    <span class="stat-item stat-tokens">{{ t("ai.modelConfig.debugConsoleTokens") }} <span class="stat-val">{{ testTokens ?? "0" }}</span></span>
                  </div>
                </div>
                <div class="debug-console-body" ref="debugBodyRef" @scroll="handleDebugScroll">
                  <div v-if="testMessages.length === 0" class="debug-empty">
                    <div class="debug-empty-icon">
                      <FileTextOutlined style="font-size: 32px; color: rgba(255,255,255,0.3)" />
                    </div>
                    <p class="debug-empty-text">{{ t("ai.modelConfig.debugConsoleEmpty") }}</p>
                  </div>
                  <div v-for="(msg, idx) in testMessages" :key="idx" class="debug-message" :class="`debug-message--${msg.role}`">
                    <div class="debug-msg-content">
                      <pre v-if="msg.role === 'user'" class="debug-msg-pre">{{ msg.content }}</pre>
                      <div v-else-if="msg.isError" class="debug-msg-error">{{ msg.content }}</div>
                      <DualStreamMessage
                        v-else
                        class="debug-msg-stream"
                        :content="msg.content"
                        :reasoning-text="msg.reasoningText"
                        :react-steps="msg.reactSteps ?? []"
                        :is-streaming="msg.isStreaming"
                        :show-typing-cursor="testStreaming && idx === testMessages.length - 1"
                        :reasoning-title="t('ai.chat.thinkPanelTitle')"
                        :step-labels="chatStepLabels"
                      />
                    </div>
                  </div>
                </div>
              </div>
              <div class="debug-input-row">
                <a-input
                  v-model:value="testPromptInput"
                  :placeholder="t('ai.modelConfig.debugConsolePlaceholder')"
                  size="large"
                  @press-enter="handleSendTest"
                />
                <a-button
                  type="primary"
                  size="large"
                  class="send-btn"
                  :loading="testStreaming"
                  @click="handleSendTest"
                >
                  <template #icon><SendOutlined /></template>
                  {{ t("ai.modelConfig.debugConsoleSend") }}
                </a-button>
              </div>
            </div>
          </a-tab-pane>
        </a-tabs>
      </template>

      <!-- Empty state -->
      <template v-else>
        <div class="empty-state">
          <a-empty :description="t('ai.modelConfig.providerSearchPlaceholder')" />
        </div>
      </template>
    </div>

    <!-- Create Model Drawer -->
    <a-drawer
      v-model:open="createDrawerVisible"
      :title="t('ai.modelConfig.drawerCreate')"
      :width="480"
      @close="closeCreateDrawer"
    >
      <a-form ref="createFormRef" :model="createForm" layout="vertical" :rules="createRules">
        <a-form-item :label="t('ai.modelConfig.labelName')" name="name">
          <a-input v-model:value="createForm.name" :placeholder="t('ai.modelConfig.namePlaceholder')" />
        </a-form-item>
        <a-form-item :label="t('ai.modelConfig.labelProvider')" name="providerType">
          <a-select v-model:value="createForm.providerType" :options="providerOptions" @change="onCreateProviderChange" />
        </a-form-item>
        <a-form-item :label="t('ai.modelConfig.labelApiKey')" name="apiKey">
          <a-input-password v-model:value="createForm.apiKey" :placeholder="t('ai.modelConfig.apiKeyPlaceholderCreate')" />
        </a-form-item>
        <a-form-item :label="t('ai.modelConfig.labelBaseUrl')" name="baseUrl">
          <a-input v-model:value="createForm.baseUrl" />
        </a-form-item>
        <a-form-item :label="t('ai.modelConfig.labelDefaultModel')" name="defaultModel">
          <a-auto-complete
            v-model:value="createForm.defaultModel"
            :options="createSuggestedModels"
            :placeholder="t('ai.modelConfig.defaultModelPlaceholder')"
          />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-space>
          <a-button @click="closeCreateDrawer">{{ t("common.cancel") }}</a-button>
          <a-button type="primary" :loading="createSubmitting" @click="handleCreateSubmit">{{ t("ai.modelConfig.ok") }}</a-button>
        </a-space>
      </template>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, reactive, ref, h } from "vue";
import { useI18n } from "vue-i18n";
import type { FormInstance } from "ant-design-vue";
import { message } from "ant-design-vue";
import {
  DualStreamMessage,
  useDualStreamRenderer,
  type StreamChatMessage
} from "@atlas/ai-core";
import {
  PlusOutlined,
  SearchOutlined,
  RightOutlined,
  SaveOutlined,
  DeleteOutlined,
  ThunderboltOutlined,
  CloudOutlined,
  RobotOutlined,
  ToolOutlined,
  BulbOutlined,
  EyeOutlined,
  CodeOutlined,
  CaretRightOutlined,
  FileTextOutlined,
  SendOutlined,
  SettingOutlined,
  ExperimentOutlined,
  PlayCircleOutlined
} from "@ant-design/icons-vue";
import {
  createModelConfig,
  createModelConfigPromptTestStream,
  deleteModelConfig,
  getModelConfigsPaged,
  testModelConfigConnection,
  updateModelConfig,
  type ModelConfigCreateRequest,
  type ModelConfigDto,
  type ModelConfigPromptTestRequest,
  type ModelConfigTestResult as TestResult
} from "@/services/api-ai";

interface ProviderInfo {
  label: string;
  color: string;
  icon: typeof CloudOutlined;
}

interface ProviderGroup {
  providerType: string;
  label: string;
  hasEnabledModels: boolean;
  models: ModelConfigDto[];
}

interface TestMessage extends StreamChatMessage {
  isError?: boolean;
}

const { t } = useI18n();

const chatStepLabels = computed(() => ({
  thought: t("ai.chat.reactThought"),
  action: t("ai.chat.reactAction"),
  observation: t("ai.chat.reactObservation"),
  final: t("ai.chat.reactFinal")
}));

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

// ── Data ──
const allModels = ref<ModelConfigDto[]>([]);
const loading = ref(false);
const searchKeyword = ref("");
const expandedProviders = ref(new Set<string>());
const selectedType = ref<"provider" | "model" | null>(null);
const selectedProviderType = ref("");
const selectedModelId = ref<number | null>(null);
const activeTab = ref("basic");

// ── Provider metadata ──
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
  return map[type] ?? map.custom;
}

// ── Provider Groups ──
const providerGroups = computed<ProviderGroup[]>(() => {
  const map = new Map<string, ModelConfigDto[]>();
  for (const model of allModels.value) {
    const key = model.providerType;
    if (!map.has(key)) map.set(key, []);
    map.get(key)!.push(model);
  }

  const order = ["deepseek", "openai", "ollama", "custom"];
  const allTypes = new Set([...order, ...map.keys()]);

  const result: ProviderGroup[] = [];
  for (const type of allTypes) {
    if (!map.has(type)) continue;
    const models = map.get(type)!;
    result.push({
      providerType: type,
      label: providerMeta(type).label,
      hasEnabledModels: models.some((m) => m.isEnabled),
      models
    });
  }
  return result;
});

const filteredProviderGroups = computed(() => {
  const kw = searchKeyword.value.toLowerCase().trim();
  if (!kw) return providerGroups.value;
  return providerGroups.value
    .map((g) => ({
      ...g,
      models: g.models.filter(
        (m) => m.name.toLowerCase().includes(kw) || g.label.toLowerCase().includes(kw)
      )
    }))
    .filter((g) => g.models.length > 0 || g.label.toLowerCase().includes(kw));
});

const currentProviderGroup = computed(() =>
  providerGroups.value.find((g) => g.providerType === selectedProviderType.value)
);

const currentModel = computed(() =>
  allModels.value.find((m) => m.id === selectedModelId.value)
);

function isDefaultModel(model: ModelConfigDto, group: ProviderGroup): boolean {
  return group.models.indexOf(model) === 0;
}

const suggestedModels = computed(() => {
  if (!currentModel.value) return [];
  return (providerModelSuggestions[currentModel.value.providerType] ?? []).map((m) => ({ value: m }));
});

// ── Tree interaction ──
function toggleExpand(providerType: string) {
  const set = new Set(expandedProviders.value);
  if (set.has(providerType)) set.delete(providerType);
  else set.add(providerType);
  expandedProviders.value = set;
}

function selectProvider(providerType: string) {
  selectedType.value = "provider";
  selectedProviderType.value = providerType;
  selectedModelId.value = null;
  const set = new Set(expandedProviders.value);
  set.add(providerType);
  expandedProviders.value = set;

  const group = providerGroups.value.find((g) => g.providerType === providerType);
  if (group && group.models.length > 0) {
    providerForm.baseUrl = group.models[0].baseUrl;
    providerForm.apiKey = "";
  }
}

function selectModel(model: ModelConfigDto) {
  selectedType.value = "model";
  selectedModelId.value = model.id;
  selectedProviderType.value = model.providerType;
  activeTab.value = "basic";
  loadModelForm(model);
}

// ── Tab label helper ──
function tabLabel(key: string) {
  const iconMap: Record<string, typeof SettingOutlined> = {
    basic: SettingOutlined,
    advanced: ExperimentOutlined,
    test: PlayCircleOutlined
  };
  const labelMap: Record<string, string> = {
    basic: t("ai.modelConfig.tabBasicInfo"),
    advanced: t("ai.modelConfig.tabAdvanced"),
    test: t("ai.modelConfig.tabModelTest")
  };
  return h("span", {}, [h(iconMap[key], { style: "margin-right: 6px" }), labelMap[key]]);
}

// ── Provider form ──
const providerForm = reactive({ baseUrl: "", apiKey: "" });
const providerSaving = ref(false);
const providerTesting = ref(false);
const providerTestResult = ref<TestResult | null>(null);

async function handleProviderTestConnection() {
  const group = currentProviderGroup.value;
  if (!group || group.models.length === 0) return;
  const firstModel = group.models[0];
  providerTesting.value = true;
  providerTestResult.value = null;
  try {
    providerTestResult.value = await testModelConfigConnection({
      modelConfigId: firstModel.id,
      providerType: firstModel.providerType,
      apiKey: providerForm.apiKey,
      baseUrl: providerForm.baseUrl || firstModel.baseUrl,
      model: firstModel.defaultModel
    });
  } catch (error: unknown) {
    providerTestResult.value = { success: false, errorMessage: (error as Error).message };
  } finally {
    providerTesting.value = false;
  }
}

async function saveProviderConfig() {
  const group = currentProviderGroup.value;
  if (!group) return;
  providerSaving.value = true;
  try {
    for (const model of group.models) {
      await updateModelConfig(model.id, {
        name: model.name,
        apiKey: providerForm.apiKey,
        baseUrl: providerForm.baseUrl || model.baseUrl,
        defaultModel: model.defaultModel,
        isEnabled: model.isEnabled,
        supportsEmbedding: model.supportsEmbedding
      });
    }
    message.success(t("crud.updateSuccess"));
    await loadData();
  } catch (error: unknown) {
    message.error((error as Error).message || t("crud.submitFailed"));
  } finally {
    providerSaving.value = false;
  }
}

// ── Model form ──
const modelForm = reactive({
  name: "",
  modelId: "",
  systemPrompt: "",
  defaultModel: "",
  isEnabled: true,
  supportsEmbedding: true,
  enableStreaming: true,
  enableReasoning: false,
  enableTools: false,
  enableVision: false,
  enableJsonMode: false,
  maxTokens: undefined as number | undefined
});

const temperatureValue = ref(20);
const topPValue = ref(100);
const frequencyPenaltyValue = ref(0);
const presencePenaltyValue = ref(0);

function loadModelForm(model: ModelConfigDto) {
  modelForm.name = model.name;
  modelForm.modelId = model.modelId || model.defaultModel;
  modelForm.systemPrompt = model.systemPrompt || "";
  modelForm.defaultModel = model.defaultModel;
  modelForm.isEnabled = model.isEnabled;
  modelForm.supportsEmbedding = model.supportsEmbedding;
  modelForm.enableStreaming = model.enableStreaming;
  modelForm.enableReasoning = model.enableReasoning;
  modelForm.enableTools = model.enableTools;
  modelForm.enableVision = model.enableVision;
  modelForm.enableJsonMode = model.enableJsonMode;
  modelForm.maxTokens = model.maxTokens ?? undefined;
  temperatureValue.value = Math.round((model.temperature ?? 0.2) * 100);
  topPValue.value = Math.round((model.topP ?? 1) * 100);
  frequencyPenaltyValue.value = Math.round((model.frequencyPenalty ?? 0) * 100);
  presencePenaltyValue.value = Math.round((model.presencePenalty ?? 0) * 100);
}

const modelSaving = ref(false);

async function saveModelConfig() {
  if (!currentModel.value) return;
  modelSaving.value = true;
  try {
    await updateModelConfig(currentModel.value.id, {
      name: modelForm.name,
      apiKey: "",
      baseUrl: currentModel.value.baseUrl,
      defaultModel: modelForm.defaultModel,
      isEnabled: modelForm.isEnabled,
      supportsEmbedding: modelForm.supportsEmbedding,
      modelId: modelForm.modelId,
      systemPrompt: modelForm.systemPrompt || undefined,
      enableStreaming: modelForm.enableStreaming,
      enableReasoning: modelForm.enableReasoning,
      enableTools: modelForm.enableTools,
      enableVision: modelForm.enableVision,
      enableJsonMode: modelForm.enableJsonMode,
      temperature: temperatureValue.value / 100,
      maxTokens: modelForm.maxTokens ?? undefined,
      topP: topPValue.value / 100,
      frequencyPenalty: frequencyPenaltyValue.value / 100,
      presencePenalty: presencePenaltyValue.value / 100
    });
    message.success(t("crud.updateSuccess"));
    await loadData();
    const updated = allModels.value.find((m) => m.id === currentModel.value!.id);
    if (updated) loadModelForm(updated);
  } catch (error: unknown) {
    message.error((error as Error).message || t("crud.submitFailed"));
  } finally {
    modelSaving.value = false;
  }
}

async function handleDeleteModel(id: number) {
  try {
    await deleteModelConfig(id);
    message.success(t("crud.deleteSuccess"));
    selectedType.value = null;
    selectedModelId.value = null;
    await loadData();
  } catch (error: unknown) {
    message.error((error as Error).message || t("crud.deleteFailed"));
  }
}

// ── Create Drawer ──
const createDrawerVisible = ref(false);
const createFormRef = ref<FormInstance>();
const createSubmitting = ref(false);
const createForm = reactive({
  name: "",
  providerType: "openai",
  apiKey: "",
  baseUrl: "https://api.openai.com/v1",
  defaultModel: ""
});

const createRules = computed(() => ({
  name: [{ required: true, message: t("ai.modelConfig.ruleName") }],
  providerType: [{ required: true, message: t("ai.modelConfig.ruleProvider") }],
  apiKey: [{ required: true, message: t("ai.modelConfig.ruleApiKey") }],
  baseUrl: [{ required: true, message: t("ai.modelConfig.ruleBaseUrl") }],
  defaultModel: [{ required: true, message: t("ai.modelConfig.ruleDefaultModel") }]
}));

const createSuggestedModels = computed(() =>
  (providerModelSuggestions[createForm.providerType] ?? []).map((m) => ({ value: m }))
);

function onCreateProviderChange(value: string) {
  createForm.baseUrl = providerBaseUrls[value] ?? "";
  createForm.defaultModel = "";
}

function openCreateDrawer() {
  Object.assign(createForm, {
    name: "",
    providerType: selectedProviderType.value || "openai",
    apiKey: "",
    baseUrl: providerBaseUrls[selectedProviderType.value || "openai"] ?? "",
    defaultModel: ""
  });
  createDrawerVisible.value = true;
}

function closeCreateDrawer() {
  createDrawerVisible.value = false;
}

async function handleCreateSubmit() {
  try {
    await createFormRef.value?.validate();
  } catch {
    return;
  }
  createSubmitting.value = true;
  try {
    const payload: ModelConfigCreateRequest = {
      name: createForm.name,
      providerType: createForm.providerType,
      apiKey: createForm.apiKey,
      baseUrl: createForm.baseUrl,
      defaultModel: createForm.defaultModel,
      supportsEmbedding: true,
      modelId: createForm.defaultModel,
      enableStreaming: true
    };
    await createModelConfig(payload);
    message.success(t("crud.createSuccess"));
    createDrawerVisible.value = false;
    await loadData();
  } catch (error: unknown) {
    message.error((error as Error).message || t("crud.submitFailed"));
  } finally {
    createSubmitting.value = false;
  }
}

// ── Model Test (Debug Console) ──
const testPromptInput = ref("");
const testStreaming = ref(false);
const testMessages = ref<TestMessage[]>([]);
const testLatency = ref<number | null>(null);
const testTokens = ref<number | null>(null);
const debugBodyRef = ref<HTMLElement>();
const debugShouldAutoScroll = ref(true);
let testAbortController: AbortController | null = null;

async function handleSendTest() {
  if (!currentModel.value || testStreaming.value) return;
  const prompt = testPromptInput.value.trim();
  if (!prompt) {
    message.warning(t("ai.modelConfig.promptRequired"));
    return;
  }

  testMessages.value.push({
    id: `user-${Date.now()}`,
    role: "user",
    content: prompt,
    createdAt: new Date().toISOString(),
    reasoningText: "",
    reactSteps: [],
    isStreaming: false,
    isReasoningStreaming: false,
    isAnswerStreaming: false,
    streamPhase: "completed"
  });
  const assistantMessage: TestMessage = {
    id: `assistant-${Date.now()}`,
    role: "assistant",
    content: "",
    createdAt: new Date().toISOString(),
    reasoningText: "",
    reactSteps: [],
    isStreaming: true,
    isReasoningStreaming: true,
    isAnswerStreaming: false,
    streamPhase: "reasoning",
    isError: false
  };
  testMessages.value.push(assistantMessage);
  testPromptInput.value = "";
  testStreaming.value = true;
  testLatency.value = null;
  testTokens.value = null;
  await scrollDebug(true);

  const startTime = Date.now();
  const model = currentModel.value;

  const payload: ModelConfigPromptTestRequest = {
    modelConfigId: model.id,
    providerType: model.providerType,
    apiKey: "",
    baseUrl: model.baseUrl,
    model: modelForm.modelId || model.defaultModel,
    prompt,
    enableReasoning: modelForm.enableReasoning,
    enableTools: modelForm.enableTools,
    enableStreaming: modelForm.enableStreaming
  };

  const { fetchPromise, abortController } = createModelConfigPromptTestStream(payload);
  testAbortController = abortController;

  const streamRenderer = useDualStreamRenderer({
    onFlush: (state) => {
      assistantMessage.content = state.answerText;
      assistantMessage.reasoningText = state.reasoningText;
      assistantMessage.reactSteps = [...state.reactSteps];
      assistantMessage.isStreaming = state.isStreaming;
      assistantMessage.isReasoningStreaming = state.isReasoningStreaming;
      assistantMessage.isAnswerStreaming = state.isAnswerStreaming;
      assistantMessage.streamPhase = state.streamPhase;
      void scrollDebug(false);
    }
  });

  try {
    const response = await fetchPromise;
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    if (!response.body) throw new Error("Empty body");

    await streamRenderer.consumeStream(response.body);

    if (streamRenderer.eventError.value) {
      throw new Error(streamRenderer.eventError.value);
    }

    testLatency.value = Date.now() - startTime;
    testTokens.value = Math.ceil((assistantMessage.content.length + (assistantMessage.reasoningText?.length ?? 0)) / 4);
  } catch (error: unknown) {
    await streamRenderer.stop();
    if ((error as Error).name !== "AbortError") {
      assistantMessage.isError = true;
      assistantMessage.content = (error as Error).message;
      assistantMessage.reasoningText = "";
      assistantMessage.reactSteps = [];
    }
  } finally {
    assistantMessage.isStreaming = false;
    assistantMessage.isReasoningStreaming = false;
    assistantMessage.isAnswerStreaming = false;
    assistantMessage.streamPhase = "completed";
    testStreaming.value = false;
    testAbortController = null;
  }
}

function handleDebugScroll() {
  if (!debugBodyRef.value) {
    return;
  }
  debugShouldAutoScroll.value = isNearDebugBottom(debugBodyRef.value);
}

function isNearDebugBottom(container: HTMLElement) {
  return container.scrollHeight - container.scrollTop - container.clientHeight < 120;
}

function scrollDebug(force = false) {
  void nextTick(() => {
    if (debugBodyRef.value) {
      if (!force && !debugShouldAutoScroll.value) {
        return;
      }
      debugBodyRef.value.scrollTop = debugBodyRef.value.scrollHeight;
      debugShouldAutoScroll.value = true;
    }
  });
}

// ── Data loading ──
async function loadData() {
  loading.value = true;
  try {
    const result = await getModelConfigsPaged({ pageIndex: 1, pageSize: 200 });
    if (!isMounted.value) return;
    allModels.value = result.items;

    if (providerGroups.value.length > 0 && !selectedType.value) {
      const first = providerGroups.value[0];
      selectProvider(first.providerType);
    }
  } catch (error: unknown) {
    message.error((error as Error).message || t("crud.queryFailed"));
  } finally {
    loading.value = false;
  }
}

onMounted(() => { void loadData(); });

onUnmounted(() => {
  if (testAbortController) {
    testAbortController.abort();
  }
});
</script>

<style scoped>
.model-configs-page {
  display: flex;
  gap: 24px;
  height: 100%;
  min-height: 0;
  padding: 24px;
  box-sizing: border-box;
}

/* ── Left Panel ── */
.left-panel {
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

.left-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px 16px 0;
}

.left-title {
  font-size: 20px;
  font-weight: 700;
  color: #101828;
  margin: 0;
  line-height: 30px;
}

.add-btn {
  width: 28px;
  height: 28px;
  border-radius: 8px;
}

.search-box {
  padding: 12px 12px 0;
}

.search-box :deep(.ant-input-affix-wrapper) {
  border-radius: 10px;
  background: #f9fafb;
  border-color: transparent;
}

.tree-list {
  flex: 1;
  overflow-y: auto;
  padding: 12px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.provider-group {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.provider-row {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px;
  border-radius: 14px;
  cursor: pointer;
  transition: background 0.15s;
}

.provider-row:hover {
  background: #f9fafb;
}

.provider-row--selected {
  background: #f0f0ff;
}

.expand-btn {
  width: 20px;
  height: 20px;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 0;
}

.expand-icon--open {
  transform: rotate(90deg);
  transition: transform 0.2s;
}

.provider-icon-wrapper {
  width: 28px;
  height: 28px;
  border-radius: 10px;
  border: 0.8px solid #e5e7eb;
  background: #fff;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 14px;
}

.provider-name {
  font-size: 14px;
  font-weight: 700;
  color: #364153;
  flex: 1;
}

.provider-status-dot,
.model-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
}

.dot--online {
  background: #00bc7d;
}

.dot--offline {
  background: #d1d5dc;
}

.model-list {
  padding-left: 36px;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.model-row {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px;
  border-radius: 10px;
  cursor: pointer;
  transition: background 0.15s;
}

.model-row:hover {
  background: #f9fafb;
}

.model-row--selected {
  background: #fff;
  border: 0.8px solid #c6d2ff;
  box-shadow: 0 0 0 0 rgba(97, 95, 255, 0.2), 0 1px 3px rgba(0, 0, 0, 0.1);
}

.model-row--selected .model-name {
  color: #432dd7;
  font-weight: 700;
}

.model-name {
  font-size: 13px;
  font-weight: 500;
  color: #4a5565;
  flex: 1;
}

.default-tag {
  font-size: 10px;
  line-height: 15px;
  padding: 0 6px;
  border-radius: 4px;
  background: #e0e7ff;
  color: #432dd7;
  border: none;
  font-weight: 700;
}

/* ── Right Panel ── */
.right-panel {
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

.detail-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  padding: 20px 24px;
  border-bottom: 0.8px solid rgba(243, 244, 246, 0.8);
  background: rgba(249, 250, 251, 0.3);
}

.detail-header-left {
  display: flex;
  gap: 12px;
  align-items: flex-start;
}

.detail-icon {
  width: 44px;
  height: 44px;
  border-radius: 12px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 20px;
  flex-shrink: 0;
}

.provider-detail-icon {
  background: #e0e7ff;
  color: #4f39f6;
}

.model-detail-icon {
  background: #e0e7ff;
  color: #4f39f6;
}

.detail-header-info {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.detail-header-title-row {
  display: flex;
  align-items: center;
  gap: 8px;
}

.detail-title {
  font-size: 18px;
  font-weight: 700;
  color: #101828;
  margin: 0;
}

.active-badge {
  font-size: 10px;
  font-weight: 700;
  border-radius: 4px;
}

.detail-desc {
  font-size: 13px;
  color: #6a7282;
  margin: 0;
}

.detail-header-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

.cap-separator {
  margin: 0 6px;
  color: #d1d5dc;
}

.cap-label {
  color: #6a7282;
}

/* ── Detail body ── */
.detail-body {
  flex: 1;
  overflow-y: auto;
  padding: 24px;
  display: flex;
  flex-direction: column;
  gap: 32px;
}

.config-section {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.section-title {
  font-size: 14px;
  font-weight: 700;
  color: #101828;
  margin: 0;
}

.section-title-icon {
  text-transform: uppercase;
  letter-spacing: 0.7px;
}

.section-header-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.test-conn-btn {
  align-self: flex-start;
}

/* ── Model cards ── */
.model-cards {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
}

.model-card {
  flex: 0 0 calc(50% - 6px);
  padding: 12px 16px;
  border: 0.8px solid #e5e7eb;
  border-radius: 12px;
  cursor: pointer;
  transition: all 0.15s;
  display: flex;
  align-items: center;
  gap: 8px;
}

.model-card:hover {
  border-color: #c6d2ff;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.05);
}

.model-card-info {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.model-card-name {
  font-size: 14px;
  font-weight: 600;
  color: #101828;
}

.model-card-caps {
  font-size: 12px;
  color: #6a7282;
}

.model-card-arrow {
  font-size: 12px;
  color: #d1d5dc;
}

/* ── Tabs ── */
.model-tabs {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.model-tabs :deep(.ant-tabs-nav) {
  margin: 0;
  padding: 0 24px;
  border-bottom: 0.8px solid rgba(243, 244, 246, 0.8);
}

.model-tabs :deep(.ant-tabs-content-holder) {
  flex: 1;
  overflow: hidden;
}

.model-tabs :deep(.ant-tabs-content) {
  height: 100%;
}

.model-tabs :deep(.ant-tabs-tabpane) {
  height: 100%;
  overflow: hidden;
}

.tab-content {
  padding: 24px;
  overflow-y: auto;
  height: 100%;
}

.tab-content-scroll {
  overflow-y: auto;
}

/* ── Features & Status ── */
.status-item {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.status-label {
  font-size: 13px;
  font-weight: 600;
  color: #364153;
}

.status-value {
  font-size: 12px;
  color: #6a7282;
}

/* ── Feature Toggles ── */
.feature-toggles-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
}

.feature-card {
  border: 0.8px solid rgba(229, 231, 235, 0.8);
  border-radius: 14px;
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 8px;
  transition: all 0.15s;
}

.feature-card--active {
  background: rgba(238, 242, 255, 0.3);
  border-color: #c6d2ff;
}

.feature-card-header {
  display: flex;
  align-items: center;
  gap: 8px;
}

.feature-card-icon {
  width: 28px;
  height: 28px;
  border-radius: 8px;
  background: #f3f4f6;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 14px;
  color: #6a7282;
}

.fci--active {
  background: #e0e7ff;
  color: #4f39f6;
}

.feature-card-title {
  flex: 1;
  font-size: 14px;
  font-weight: 600;
  color: #101828;
  line-height: 1.3;
}

.feature-card-desc {
  font-size: 12px;
  line-height: 19.5px;
  color: #6a7282;
  margin: 0;
}

/* ── Parameters ── */
.params-grid {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.param-item {
  display: flex;
  align-items: center;
  gap: 12px;
}

.param-label {
  min-width: 140px;
  font-size: 13px;
  font-weight: 600;
  color: #364153;
}

/* ── Debug Console ── */
.tab-test {
  display: flex;
  flex-direction: column;
  gap: 0;
  padding: 0;
}

.debug-console {
  flex: 1;
  background: #1a1a2e;
  border-radius: 12px;
  margin: 16px 16px 0;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  min-height: 0;
}

.debug-console-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 10px 16px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
}

.debug-console-title {
  font-size: 12px;
  font-weight: 600;
  color: rgba(255, 255, 255, 0.7);
  font-family: "SF Mono", "Fira Code", monospace;
}

.debug-console-stats {
  display: flex;
  gap: 16px;
}

.stat-item {
  font-size: 11px;
  font-family: "SF Mono", "Fira Code", monospace;
}

.stat-latency {
  color: #00bc7d;
}

.stat-tokens {
  color: #818cf8;
}

.stat-val {
  font-weight: 700;
}

.debug-console-body {
  flex: 1;
  overflow-y: auto;
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 12px;
  min-height: 200px;
}

.debug-empty {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  flex: 1;
  gap: 12px;
}

.debug-empty-text {
  font-size: 13px;
  color: rgba(255, 255, 255, 0.3);
}

.debug-message {
  display: flex;
}

.debug-message--user {
  justify-content: flex-end;
}

.debug-message--user .debug-msg-content {
  background: #3730a3;
  border-radius: 12px 12px 4px 12px;
}

.debug-message--assistant .debug-msg-content {
  background: rgba(255, 255, 255, 0.05);
  border-radius: 12px 12px 12px 4px;
}

.debug-msg-content {
  max-width: 85%;
  padding: 10px 14px;
  color: rgba(255, 255, 255, 0.9);
  font-size: 13px;
  line-height: 1.6;
}

.debug-msg-pre {
  margin: 0;
  white-space: pre-wrap;
  word-break: break-word;
  font-size: 13px;
  color: rgba(255, 255, 255, 0.9);
  font-family: inherit;
}

.debug-msg-stream {
  font-size: 13px;
  line-height: 1.6;
}

.debug-msg-stream :deep(.dual-stream-message__reasoning) {
  background: rgba(110, 231, 183, 0.08);
  border-color: rgba(110, 231, 183, 0.18);
}

.debug-msg-stream :deep(.dual-stream-message__toggle) {
  color: #6ee7b7;
}

.debug-msg-stream :deep(.dual-stream-message__reasoning-text .markdown-body) {
  color: rgba(226, 232, 240, 0.78);
}

.debug-msg-stream :deep(.dual-stream-message__step-label) {
  color: #6ee7b7;
}

.debug-msg-stream :deep(.dual-stream-message__cursor) {
  color: #818cf8;
}

.debug-msg-stream :deep(.dual-stream-message__step) {
  border-top-color: rgba(110, 231, 183, 0.12);
}

.debug-msg-stream :deep(.markdown-body) {
  color: #f8fafc;
}

.debug-msg-stream :deep(.markdown-body pre) {
  background: rgba(15, 23, 42, 0.92);
  border: 1px solid rgba(148, 163, 184, 0.16);
}

.debug-msg-stream :deep(.markdown-body a) {
  color: #93c5fd;
}

.debug-msg-stream :deep(.markdown-body code) {
  background: rgba(148, 163, 184, 0.14);
  color: #f8fafc;
}

.debug-msg-stream :deep(.markdown-body blockquote) {
  border-left-color: rgba(148, 163, 184, 0.3);
  color: rgba(226, 232, 240, 0.72);
}

.debug-msg-stream :deep(.markdown-body th),
.debug-msg-stream :deep(.markdown-body td) {
  border-color: rgba(148, 163, 184, 0.16);
}

.debug-msg-stream :deep(.markdown-body th) {
  background: rgba(15, 23, 42, 0.65);
}

.debug-msg-stream :deep(.markdown-body hr) {
  border-top-color: rgba(148, 163, 184, 0.18);
}

.debug-msg-error {
  color: #f87171;
  white-space: pre-wrap;
  word-break: break-word;
  line-height: 1.7;
}

.debug-input-row {
  display: flex;
  gap: 12px;
  padding: 16px;
  align-items: stretch;
}

.debug-input-row :deep(.ant-input) {
  border-radius: 12px;
}

.send-btn {
  border-radius: 12px;
  min-width: 80px;
  background: #4f39f6;
  border-color: #4f39f6;
}

.send-btn:hover {
  background: #3730a3;
  border-color: #3730a3;
}

/* ── Empty state ── */
.empty-state {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
}
</style>
