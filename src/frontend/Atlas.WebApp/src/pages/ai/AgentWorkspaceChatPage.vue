<template>
  <AgentChatPage v-if="!isTeamChat" />

  <div v-else class="team-chat-page">
    <a-card :bordered="false" class="team-chat-hero">
      <div class="team-chat-hero__header">
        <div>
          <a-button type="link" class="team-chat-back" @click="goBack">
            {{ t("ai.agent.backToWorkspace") }}
          </a-button>
          <div class="team-chat-title-row">
            <h1 class="team-chat-title">{{ detail?.name || t("ai.agent.teamChatTitle") }}</h1>
            <a-tag :color="teamModeColor(teamMetadata.mode)">{{ teamModeText(teamMetadata.mode) }}</a-tag>
          </div>
          <p class="team-chat-subtitle">{{ detail?.description || t("ai.agent.teamChatSubtitle") }}</p>
        </div>
        <a-space>
          <a-button @click="goEditor">{{ t("common.edit") }}</a-button>
          <a-button @click="goDataManagement">{{ t("ai.agent.schemaConfirmCreate") }}</a-button>
        </a-space>
      </div>

      <a-form layout="vertical">
        <a-form-item :label="t('ai.agent.schemaPromptLabel')">
          <a-textarea v-model:value="prompt" :rows="4" :placeholder="t('ai.agent.schemaPromptPlaceholder')" />
        </a-form-item>
        <div class="team-chat-actions">
          <a-space>
            <a-button type="primary" :loading="running" @click="handleRun">
              {{ t("ai.agent.actionRun") }}
            </a-button>
            <a-button :loading="streaming" @click="handleStream">
              {{ t("ai.multiAgent.streamRun") }}
            </a-button>
            <a-button danger :disabled="!streaming" @click="handleStopStream">
              {{ t("ai.multiAgent.stopStream") }}
            </a-button>
          </a-space>
          <a-checkbox v-model:checked="enableRag">{{ t("ai.chat.enableRag") }}</a-checkbox>
        </div>
      </a-form>
    </a-card>

    <div class="team-chat-grid">
      <a-card :bordered="false" class="team-chat-panel">
        <div class="panel-title">{{ t("ai.agent.groupChatTimelineTitle") }}</div>
        <div class="timeline-list">
          <div class="timeline-item timeline-item--user">
            <div class="timeline-item__role">{{ t("ai.agent.timelineUser") }}</div>
            <div class="timeline-item__content">{{ prompt || t("ai.agent.schemaPromptPlaceholder") }}</div>
          </div>
          <div
            v-for="item in timelineItems"
            :key="item.id"
            class="timeline-item"
          >
            <div class="timeline-item__role">{{ item.role }}</div>
            <div class="timeline-item__content">{{ item.content }}</div>
          </div>
        </div>
      </a-card>

      <a-card :bordered="false" class="team-chat-panel">
        <div class="panel-title">{{ t("ai.agent.executionPanelTitle") }}</div>
        <a-descriptions v-if="lastExecution" size="small" :column="1" bordered>
          <a-descriptions-item :label="t('ai.multiAgent.executionId')">
            {{ lastExecution.executionId }}
          </a-descriptions-item>
          <a-descriptions-item :label="t('ai.multiAgent.executionStatus')">
            {{ statusText(lastExecution.status) }}
          </a-descriptions-item>
        </a-descriptions>
        <div class="stream-log">
          <div class="stream-log__label">{{ t("ai.multiAgent.streamLog") }}</div>
          <pre>{{ streamLogs.join("\n") }}</pre>
        </div>
      </a-card>
    </div>

    <a-card :bordered="false" class="team-chat-panel">
      <div class="panel-title">{{ t("ai.agent.schemaPanelTitle") }}</div>
      <div v-if="schemaDraft" class="schema-draft">
        <div class="schema-draft__title">{{ schemaDraft.title }}</div>
        <div class="schema-draft__summary">{{ schemaDraft.summary }}</div>
        <div class="schema-draft__section">
          <div class="schema-draft__label">{{ t("ai.agent.schemaEntityCount") }}</div>
          <a-tag v-for="entity in schemaDraft.entities" :key="entity.name">{{ entity.title }}</a-tag>
        </div>
        <div class="schema-draft__section">
          <div class="schema-draft__label">{{ t("ai.agent.schemaQuestions") }}</div>
          <ul class="schema-draft__list">
            <li v-for="question in schemaDraft.openQuestions" :key="question">{{ question }}</li>
          </ul>
        </div>
      </div>
      <a-empty v-else :description="t('ai.agent.schemaEmpty')" />
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import { useRoute, useRouter } from "vue-router";
import { message } from "ant-design-vue";
import AgentChatPage from "@/pages/ai/AgentChatPage.vue";
import {
  getMultiAgentOrchestrationById,
  runMultiAgentOrchestration,
  streamMultiAgentOrchestration,
  type MultiAgentExecutionResult,
  type MultiAgentExecutionStatus
} from "@/services/api-multi-agent";
import {
  buildSchemaDraftFromPrompt,
  getTeamAgentSourceId,
  isTeamAgentId,
  loadTeamAgentMetadata,
  saveTeamAgentMetadata,
  type SchemaDraft,
  type TeamAgentMetadata,
  type WorkspaceTeamMode
} from "@/services/agent-workspace";

const { t } = useI18n();
const route = useRoute();
const router = useRouter();

const teamId = computed(() => String(route.params.agentId ?? ""));
const isTeamChat = computed(() => isTeamAgentId(teamId.value));
const sourceId = computed(() => (isTeamChat.value ? getTeamAgentSourceId(teamId.value) : 0));

const detail = ref<Awaited<ReturnType<typeof getMultiAgentOrchestrationById>> | null>(null);
const teamMetadata = ref<TeamAgentMetadata>(loadTeamAgentMetadata(0, "", ""));
const prompt = ref(typeof route.query.entrySkill === "string" && route.query.entrySkill === "schema_builder"
  ? t("ai.agent.schemaDefaultPrompt")
  : "");
const enableRag = ref(false);
const running = ref(false);
const streaming = ref(false);
const streamAbort = ref<AbortController | null>(null);
const streamLogs = ref<string[]>([]);
const lastExecution = ref<MultiAgentExecutionResult | null>(null);
const schemaDraft = ref<SchemaDraft | null>(null);

const timelineItems = computed(() => {
  if (lastExecution.value?.steps?.length) {
    return lastExecution.value.steps.map((item, index) => ({
      id: `${item.agentId}-${index}`,
      role: item.alias || item.agentName,
      content: item.outputMessage || item.inputMessage
    }));
  }

  return streamLogs.value.map((item, index) => ({
    id: `stream-${index}`,
    role: t("ai.agent.coordinatorLabel"),
    content: item
  }));
});

function teamModeText(mode: WorkspaceTeamMode) {
  if (mode === "workflow") {
    return t("ai.agent.teamModeWorkflow");
  }

  if (mode === "handoff") {
    return t("ai.agent.teamModeHandoff");
  }

  return t("ai.agent.teamModeGroupChat");
}

function teamModeColor(mode: WorkspaceTeamMode) {
  if (mode === "workflow") {
    return "purple";
  }

  if (mode === "handoff") {
    return "orange";
  }

  return "cyan";
}

function statusText(status: MultiAgentExecutionStatus) {
  if (status === 2) {
    return t("ai.multiAgent.statusCompleted");
  }

  if (status === 3) {
    return t("ai.multiAgent.statusFailed");
  }

  if (status === 1) {
    return t("ai.multiAgent.statusRunning");
  }

  return t("ai.multiAgent.statusPending");
}

function goBack() {
  void router.push(`/apps/${route.params.appId}/agents`);
}

function goEditor() {
  void router.push(`/apps/${route.params.appId}/agents/${teamId.value}/edit`);
}

function goDataManagement() {
  void router.push(`/apps/${route.params.appId}/data`);
}

function persistSchemaDraft() {
  if (!detail.value) {
    return;
  }

  teamMetadata.value.schemaDraft = buildSchemaDraftFromPrompt(prompt.value.trim());
  schemaDraft.value = teamMetadata.value.schemaDraft;
  saveTeamAgentMetadata(sourceId.value, teamMetadata.value);
}

async function handleRun() {
  if (!prompt.value.trim()) {
    message.warning(t("ai.agent.schemaPromptRequired"));
    return;
  }

  running.value = true;
  try {
    lastExecution.value = await runMultiAgentOrchestration(sourceId.value, {
      message: prompt.value.trim(),
      enableRag: enableRag.value
    });
    if (teamMetadata.value.capabilityFlags.schemaBuilder || route.query.entrySkill === "schema_builder") {
      persistSchemaDraft();
    }
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.multiAgent.runFailed"));
  } finally {
    running.value = false;
  }
}

async function handleStream() {
  if (!prompt.value.trim()) {
    message.warning(t("ai.agent.schemaPromptRequired"));
    return;
  }

  streamLogs.value = [];
  const controller = new AbortController();
  streamAbort.value = controller;
  streaming.value = true;
  try {
    await streamMultiAgentOrchestration(
      sourceId.value,
      { message: prompt.value.trim(), enableRag: enableRag.value },
      (event) => {
        streamLogs.value.push(`[${event.eventType}] ${event.data}`);
      },
      controller.signal
    );
    if (teamMetadata.value.capabilityFlags.schemaBuilder || route.query.entrySkill === "schema_builder") {
      persistSchemaDraft();
    }
  } catch (error: unknown) {
    if (!controller.signal.aborted) {
      message.error((error as Error).message || t("ai.multiAgent.streamFailed"));
    }
  } finally {
    streaming.value = false;
    streamAbort.value = null;
  }
}

function handleStopStream() {
  streamAbort.value?.abort();
}

onMounted(async () => {
  if (!isTeamChat.value) {
    return;
  }

  try {
    detail.value = await getMultiAgentOrchestrationById(sourceId.value);
    teamMetadata.value = loadTeamAgentMetadata(sourceId.value, detail.value.name, detail.value.description);
    schemaDraft.value = teamMetadata.value.schemaDraft || null;
    if (!prompt.value) {
      prompt.value = detail.value.description || "";
    }
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.multiAgent.loadDetailFailed"));
  }
});
</script>

<style scoped>
.team-chat-page {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.team-chat-hero,
.team-chat-panel {
  border-radius: 20px;
}

.team-chat-hero__header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 16px;
  margin-bottom: 18px;
}

.team-chat-back {
  padding-left: 0;
}

.team-chat-title-row {
  display: flex;
  align-items: center;
  gap: 10px;
}

.team-chat-title {
  margin: 0;
  font-size: 28px;
  color: #0f172a;
}

.team-chat-subtitle {
  margin: 8px 0 0;
  color: rgba(0, 0, 0, 0.45);
}

.team-chat-actions {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.team-chat-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 20px;
}

.panel-title {
  margin-bottom: 16px;
  font-size: 16px;
  font-weight: 700;
  color: #0f172a;
}

.timeline-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.timeline-item {
  padding: 14px;
  border-radius: 16px;
  background: #f8fafc;
}

.timeline-item--user {
  background: #eff6ff;
}

.timeline-item__role {
  font-size: 12px;
  font-weight: 700;
  color: #1d4ed8;
  text-transform: uppercase;
}

.timeline-item__content {
  margin-top: 8px;
  color: rgba(0, 0, 0, 0.72);
  line-height: 1.6;
  white-space: pre-wrap;
}

.stream-log {
  margin-top: 16px;
  padding: 14px;
  border-radius: 16px;
  background: #f8fafc;
}

.stream-log__label {
  margin-bottom: 8px;
  color: rgba(0, 0, 0, 0.45);
  font-size: 12px;
}

.stream-log pre {
  margin: 0;
  max-height: 300px;
  overflow: auto;
  white-space: pre-wrap;
}

.schema-draft__title {
  font-size: 16px;
  font-weight: 700;
  color: #0f172a;
}

.schema-draft__summary {
  margin-top: 8px;
  color: rgba(0, 0, 0, 0.56);
}

.schema-draft__section {
  margin-top: 14px;
}

.schema-draft__label {
  margin-bottom: 8px;
  font-size: 12px;
  color: rgba(0, 0, 0, 0.45);
  text-transform: uppercase;
}

.schema-draft__list {
  margin: 0;
  padding-left: 18px;
}

@media (max-width: 900px) {
  .team-chat-grid {
    grid-template-columns: 1fr;
  }

  .team-chat-hero__header,
  .team-chat-actions {
    flex-direction: column;
    align-items: stretch;
  }
}
</style>
