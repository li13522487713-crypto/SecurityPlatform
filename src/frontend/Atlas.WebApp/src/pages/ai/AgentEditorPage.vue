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

const form = reactive({
  name: "",
  description: "",
  avatarUrl: "",
  systemPrompt: "",
  modelConfigId: undefined as number | undefined,
  modelName: "",
  temperature: 1,
  maxTokens: 2048
});

const modelOptions = computed(() =>
  modelConfigs.value.map((item) => ({
    label: `${item.name} (${item.providerType})`,
    value: item.id
  }))
);

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
      maxTokens: detail.maxTokens ?? 2048
    });
    knowledgeBaseInput.value = (detail.knowledgeBaseIds || []).join(",");
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
      knowledgeBaseIds: parseKnowledgeBaseIds()
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
</style>
