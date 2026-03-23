<template>
  <a-card :bordered="false">
    <template #title>
      <a-space>
        <a-button type="link" @click="goBack">{{ t("ai.agent.backToList") }}</a-button>
        <span>{{ t("ai.agent.editorTitle") }}</span>
      </a-space>
    </template>

    <a-row :gutter="16">
      <a-col :span="8">
        <a-card size="small" :title="t('ai.agent.cardBasic')">
          <a-form layout="vertical">
            <a-form-item :label="t('ai.promptLib.colName')">
              <a-input v-model:value="form.name" />
            </a-form-item>
            <a-form-item :label="t('ai.promptLib.labelDescription')">
              <a-textarea v-model:value="form.description" :rows="2" />
            </a-form-item>
            <a-form-item :label="t('ai.agent.labelAvatar')">
              <a-input v-model:value="form.avatarUrl" />
            </a-form-item>
            <a-form-item :label="t('ai.agent.labelModelConfig')">
              <a-select v-model:value="form.modelConfigId" allow-clear :options="modelOptions" />
            </a-form-item>
            <a-form-item :label="t('ai.agent.labelModelOverride')">
              <a-input v-model:value="form.modelName" />
            </a-form-item>
            <a-form-item label="Temperature">
              <a-slider v-model:value="form.temperature" :min="0" :max="2" :step="0.1" />
            </a-form-item>
            <a-form-item label="MaxTokens">
              <a-input-number v-model:value="form.maxTokens" :min="1" :max="128000" style="width: 100%" />
            </a-form-item>
            <a-form-item :label="t('ai.agent.labelKbIds')">
              <a-input v-model:value="knowledgeBaseInput" :placeholder="t('ai.agent.kbPlaceholder')" />
            </a-form-item>

            <a-form-item :label="t('ai.agent.memoryEnable')">
              <a-switch v-model:checked="form.enableMemory" />
            </a-form-item>
            <a-form-item :label="t('ai.agent.memoryShortTermEnable')">
              <a-switch
                v-model:checked="form.enableShortTermMemory"
                :disabled="!form.enableMemory"
              />
            </a-form-item>
            <a-form-item :label="t('ai.agent.memoryLongTermEnable')">
              <a-switch
                v-model:checked="form.enableLongTermMemory"
                :disabled="!form.enableMemory"
              />
            </a-form-item>
            <a-form-item :label="t('ai.agent.memoryLongTermTopK')">
              <a-input-number
                v-model:value="form.longTermMemoryTopK"
                :min="1"
                :max="10"
                style="width: 100%"
                :disabled="!form.enableMemory || !form.enableLongTermMemory"
              />
            </a-form-item>

            <a-form-item :label="t('ai.agent.labelPluginBindings')">
              <div class="tool-binding-list">
                <div v-for="(binding, index) in pluginBindings" :key="binding.rowId" class="tool-binding-row">
                  <a-select
                    v-model:value="binding.pluginId"
                    show-search
                    :filter-option="false"
                    :placeholder="t('ai.agent.pluginSelectPlaceholder')"
                    :options="pluginOptions"
                    :loading="pluginSearchLoading"
                    @search="handlePluginSearch"
                  />
                  <a-input-number
                    v-model:value="binding.sortOrder"
                    :min="0"
                    :placeholder="t('ai.agent.pluginSortOrder')"
                    style="width: 120px"
                  />
                  <a-switch v-model:checked="binding.isEnabled" />
                  <a-button
                    danger
                    type="text"
                    @click="removePluginBinding(index)"
                  >
                    {{ t("common.delete") }}
                  </a-button>
                  <a-textarea
                    v-model:value="binding.toolConfigJson"
                    :rows="2"
                    :placeholder="t('ai.agent.pluginConfigPlaceholder')"
                  />
                </div>

                <a-button type="dashed" block @click="addPluginBinding">
                  {{ t("ai.agent.addPluginBinding") }}
                </a-button>
              </div>
            </a-form-item>
          </a-form>
        </a-card>
      </a-col>

      <a-col :span="8">
        <a-card size="small" :title="t('ai.agent.cardSystemPrompt')">
          <a-textarea v-model:value="form.systemPrompt" :rows="26" />
          <div class="counter">{{ t("ai.agent.charCount", { count: form.systemPrompt.length }) }}</div>
        </a-card>
      </a-col>

      <a-col :span="8">
        <a-card size="small" :title="t('ai.agent.cardPreview')">
          <a-alert
            :message="t('ai.agent.previewMvp')"
            :description="t('ai.agent.previewDesc')"
            type="info"
            show-icon
          />
          <div class="preview-box">
            <p><strong>{{ t("ai.agent.stateLabel") }}</strong>{{ agent?.status || "-" }}</p>
            <p><strong>{{ t("ai.agent.publishVersionLabel") }}</strong>v{{ agent?.publishVersion ?? 0 }}</p>
            <p><strong>{{ t("ai.agent.updatedAtLabel") }}</strong>{{ agent?.updatedAt || "-" }}</p>
          </div>
        </a-card>
      </a-col>
    </a-row>

    <div class="actions">
      <a-space>
        <a-button @click="goBack">{{ t("ai.agent.cancel") }}</a-button>
        <a-button :loading="publishing" @click="handlePublish">{{ t("ai.workflow.publish") }}</a-button>
        <a-button type="primary" :loading="saving" @click="handleSave">{{ t("common.save") }}</a-button>
      </a-space>
    </div>
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
import { message } from "ant-design-vue";
import {
  getAgentById,
  publishAgent,
  type AgentDetail,
  updateAgent
} from "@/services/api-agent";
import { getAiPluginsPaged, type AiPluginListItem } from "@/services/api-ai-plugin";
import { getEnabledModelConfigs, type ModelConfigDto } from "@/services/api-model-config";
import { resolveCurrentAppId } from "@/utils/app-context";

const route = useRoute();
const router = useRouter();
const agentId = Number(route.params.id);

const agent = ref<AgentDetail | null>(null);
const modelConfigs = ref<ModelConfigDto[]>([]);
const saving = ref(false);
const publishing = ref(false);
const knowledgeBaseInput = ref("");
const pluginSearchLoading = ref(false);
const pluginSource = ref<AiPluginListItem[]>([]);
const pluginBindings = ref<PluginBindingRow[]>([]);

const form = reactive({
  name: "",
  description: "",
  avatarUrl: "",
  systemPrompt: "",
  modelConfigId: undefined as number | undefined,
  modelName: "",
  temperature: 1,
  maxTokens: 2048,
  enableMemory: true,
  enableShortTermMemory: true,
  enableLongTermMemory: true,
  longTermMemoryTopK: 3
});

const modelOptions = computed(() =>
  modelConfigs.value.map((item) => ({
    label: `${item.name} (${item.providerType})`,
    value: item.id
  }))
);

const pluginOptions = computed(() =>
  pluginSource.value.map((item) => ({
    label: `${item.name}${item.category ? ` (${item.category})` : ""}`,
    value: item.id
  }))
);

type PluginBindingRow = {
  rowId: string;
  pluginId?: number;
  sortOrder: number;
  isEnabled: boolean;
  toolConfigJson: string;
};

function goBack() {
  const currentAppId = resolveCurrentAppId(route);
  if (!currentAppId) {
    void router.push("/console/apps");
    return;
  }
  void router.push(`/apps/${currentAppId}/agents`);
}

async function loadData() {
  try {
    const [detail, models]  = await Promise.all([
      getAgentById(agentId),
      getEnabledModelConfigs()
    ]);

    if (!isMounted.value) return;
    agent.value = detail;
    modelConfigs.value = models;
    Object.assign(form, {
      name: detail.name,
      description: detail.description || "",
      avatarUrl: detail.avatarUrl || "",
      systemPrompt: detail.systemPrompt || "",
      modelConfigId: detail.modelConfigId,
      modelName: detail.modelName || "",
      temperature: detail.temperature ?? 1,
      maxTokens: detail.maxTokens ?? 2048,
      enableMemory: detail.enableMemory ?? true,
      enableShortTermMemory: detail.enableShortTermMemory ?? true,
      enableLongTermMemory: detail.enableLongTermMemory ?? true,
      longTermMemoryTopK: detail.longTermMemoryTopK ?? 3
    });
    knowledgeBaseInput.value = (detail.knowledgeBaseIds || []).join(",");
    pluginBindings.value = (detail.pluginBindings || []).map((binding, index) => ({
      rowId: `${binding.pluginId}-${index}-${Date.now()}`,
      pluginId: binding.pluginId,
      sortOrder: binding.sortOrder,
      isEnabled: binding.isEnabled,
      toolConfigJson: binding.toolConfigJson || "{}"
    }));
    if (pluginBindings.value.length === 0) {
      addPluginBinding();
    }

    await loadPluginOptions();
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.agent.loadAgentFailed"));
  }
}

function parseKnowledgeBaseIds() {
  return knowledgeBaseInput.value
    .split(",")
    .map((item) => Number(item.trim()))
    .filter((item) => Number.isFinite(item) && item > 0);
}

async function loadPluginOptions(keyword?: string) {
  pluginSearchLoading.value = true;
  try {
    const result = await getAiPluginsPaged(
      { pageIndex: 1, pageSize: 20 },
      keyword && keyword.trim() ? keyword.trim() : undefined
    );

    if (!isMounted.value) return;
    pluginSource.value = result.items;
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.agent.loadPluginsFailed"));
  } finally {
    pluginSearchLoading.value = false;
  }
}

function addPluginBinding() {
  pluginBindings.value.push({
    rowId: `binding-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
    pluginId: undefined,
    sortOrder: pluginBindings.value.length,
    isEnabled: true,
    toolConfigJson: "{}"
  });
}

function removePluginBinding(index: number) {
  pluginBindings.value.splice(index, 1);
}

function handlePluginSearch(keyword: string) {
  void loadPluginOptions(keyword);
}

async function handleSave() {
  if (!form.name.trim()) {
    message.warning(t("ai.agent.warnName"));
    return;
  }

  saving.value = true;
  try {
    await updateAgent(agentId, {
      name: form.name,
      description: form.description || undefined,
      avatarUrl: form.avatarUrl || undefined,
      systemPrompt: form.systemPrompt || undefined,
      modelConfigId: form.modelConfigId,
      modelName: form.modelName || undefined,
      temperature: form.temperature,
      maxTokens: form.maxTokens,
      enableMemory: form.enableMemory,
      enableShortTermMemory: form.enableShortTermMemory,
      enableLongTermMemory: form.enableLongTermMemory,
      longTermMemoryTopK: form.longTermMemoryTopK,
      knowledgeBaseIds: parseKnowledgeBaseIds(),
      pluginBindings: pluginBindings.value
        .filter((binding) => typeof binding.pluginId === "number" && binding.pluginId > 0)
        .map((binding) => ({
          pluginId: Number(binding.pluginId),
          sortOrder: binding.sortOrder,
          isEnabled: binding.isEnabled,
          toolConfigJson: binding.toolConfigJson || "{}"
        }))
    });

    if (!isMounted.value) return;
    message.success(t("ai.agent.saveSuccess"));
    await loadData();

    if (!isMounted.value) return;
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.workflow.saveFailed"));
  } finally {
    saving.value = false;
  }
}

async function handlePublish() {
  publishing.value = true;
  try {
    await publishAgent(agentId);

    if (!isMounted.value) return;
    message.success(t("ai.agent.publishSuccess"));
    await loadData();

    if (!isMounted.value) return;
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.agent.publishFailed"));
  } finally {
    publishing.value = false;
  }
}

onMounted(() => {
  void loadData();
});
</script>

<style scoped>
.counter {
  margin-top: 8px;
  text-align: right;
  color: rgba(0, 0, 0, 0.45);
}

.preview-box {
  margin-top: 12px;
}

.actions {
  margin-top: 16px;
  text-align: right;
}

.tool-binding-list {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.tool-binding-row {
  display: grid;
  grid-template-columns: 1fr 120px auto auto;
  gap: 8px;
  align-items: center;
  padding: 10px;
  border: 1px dashed #d9d9d9;
  border-radius: 8px;
}

.tool-binding-row :deep(.ant-input-textarea) {
  grid-column: 1 / 5;
}
</style>
