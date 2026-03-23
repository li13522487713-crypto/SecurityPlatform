<template>
  <a-card :title="t('ai.multiAgent.runPanelTitle')" size="small">
    <a-form layout="vertical">
      <a-form-item :label="t('ai.multiAgent.runMessage')">
        <a-textarea
          v-model:value="runMessage"
          :rows="4"
          :placeholder="t('ai.multiAgent.runMessagePlaceholder')"
        />
      </a-form-item>
      <a-form-item :label="t('ai.multiAgent.enableRag')">
        <a-switch v-model:checked="enableRag" />
      </a-form-item>
      <a-space>
        <a-button type="primary" :loading="running" @click="handleRun">
          {{ t("ai.multiAgent.runNow") }}
        </a-button>
        <a-button :loading="streaming" @click="handleStreamStart">
          {{ t("ai.multiAgent.streamRun") }}
        </a-button>
        <a-button danger :disabled="!streaming" @click="handleStreamStop">
          {{ t("ai.multiAgent.stopStream") }}
        </a-button>
      </a-space>
    </a-form>

    <a-divider />

    <a-descriptions v-if="lastExecution" size="small" :column="2" bordered>
      <a-descriptions-item :label="t('ai.multiAgent.executionId')">
        {{ lastExecution.executionId }}
      </a-descriptions-item>
      <a-descriptions-item :label="t('ai.multiAgent.executionStatus')">
        <a-tag :color="statusColor(lastExecution.status)">{{ statusText(lastExecution.status) }}</a-tag>
      </a-descriptions-item>
      <a-descriptions-item :label="t('ai.multiAgent.executionStartedAt')">
        {{ lastExecution.startedAt }}
      </a-descriptions-item>
      <a-descriptions-item :label="t('ai.multiAgent.executionCompletedAt')">
        {{ lastExecution.completedAt || "-" }}
      </a-descriptions-item>
    </a-descriptions>

    <a-alert
      v-if="lastExecution?.errorMessage"
      type="error"
      show-icon
      style="margin-top: 12px"
      :message="lastExecution.errorMessage"
    />

    <a-table
      style="margin-top: 12px"
      row-key="startedAt"
      :columns="stepColumns"
      :data-source="lastExecution?.steps || []"
      :pagination="false"
      size="small"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'status'">
          <a-tag :color="statusColor(record.status)">{{ statusText(record.status) }}</a-tag>
        </template>
      </template>
    </a-table>

    <a-divider />

    <div class="stream-log">
      <div class="stream-log__header">{{ t("ai.multiAgent.streamLog") }}</div>
      <pre>{{ streamLogText }}</pre>
    </div>
  </a-card>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import { message } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import {
  runMultiAgentOrchestration,
  streamMultiAgentOrchestration,
  type MultiAgentExecutionResult,
  type MultiAgentExecutionStatus
} from "@/services/api-multi-agent";

const props = defineProps<{
  orchestrationId: number;
}>();

const emit = defineEmits<{
  (event: "executed", payload: MultiAgentExecutionResult): void;
}>();

const { t } = useI18n();
const runMessage = ref("");
const enableRag = ref(false);
const running = ref(false);
const streaming = ref(false);
const streamAbortController = ref<AbortController | null>(null);
const streamLogs = ref<string[]>([]);
const lastExecution = ref<MultiAgentExecutionResult | null>(null);

const stepColumns = computed(() => [
  { title: t("ai.multiAgent.stepAgent"), dataIndex: "agentName", key: "agentName", width: 180 },
  { title: t("ai.multiAgent.stepAlias"), dataIndex: "alias", key: "alias", width: 140 },
  { title: t("ai.multiAgent.stepStatus"), dataIndex: "status", key: "status", width: 120 },
  { title: t("ai.multiAgent.stepInput"), dataIndex: "inputMessage", key: "inputMessage" },
  { title: t("ai.multiAgent.stepOutput"), dataIndex: "outputMessage", key: "outputMessage" }
]);

const streamLogText = computed(() => streamLogs.value.join("\n"));

function statusText(status: MultiAgentExecutionStatus) {
  switch (status) {
    case 0: return t("ai.multiAgent.statusPending");
    case 1: return t("ai.multiAgent.statusRunning");
    case 2: return t("ai.multiAgent.statusCompleted");
    case 3: return t("ai.multiAgent.statusFailed");
    case 4: return t("ai.multiAgent.statusCancelled");
    case 5: return t("ai.multiAgent.statusInterrupted");
    default: return String(status);
  }
}

function statusColor(status: MultiAgentExecutionStatus) {
  switch (status) {
    case 2: return "green";
    case 3: return "red";
    case 1: return "blue";
    case 4: return "orange";
    case 5: return "purple";
    default: return "default";
  }
}

async function handleRun() {
  if (!runMessage.value.trim()) {
    message.warning(t("ai.multiAgent.runMessageRequired"));
    return;
  }

  running.value = true;
  try {
    const result = await runMultiAgentOrchestration(props.orchestrationId, {
      message: runMessage.value.trim(),
      enableRag: enableRag.value
    });
    lastExecution.value = result;
    emit("executed", result);
    message.success(t("ai.multiAgent.runSuccess"));
  } catch (err: unknown) {
    message.error((err as Error).message || t("ai.multiAgent.runFailed"));
  } finally {
    running.value = false;
  }
}

async function handleStreamStart() {
  if (!runMessage.value.trim()) {
    message.warning(t("ai.multiAgent.runMessageRequired"));
    return;
  }
  if (streaming.value) {
    return;
  }

  streamLogs.value = [];
  const controller = new AbortController();
  streamAbortController.value = controller;
  streaming.value = true;
  try {
    await streamMultiAgentOrchestration(
      props.orchestrationId,
      { message: runMessage.value.trim(), enableRag: enableRag.value },
      (evt) => {
        streamLogs.value.push(`[${evt.eventType}] ${evt.data}`);
      },
      controller.signal
    );
  } catch (err: unknown) {
    if (controller.signal.aborted) {
      streamLogs.value.push(t("ai.multiAgent.streamStopped"));
      return;
    }
    message.error((err as Error).message || t("ai.multiAgent.streamFailed"));
  } finally {
    streaming.value = false;
    streamAbortController.value = null;
  }
}

function handleStreamStop() {
  streamAbortController.value?.abort();
}
</script>

<style scoped>
.stream-log {
  border: 1px solid #f0f0f0;
  border-radius: 6px;
  padding: 8px;
  background: #fafafa;
}

.stream-log__header {
  font-weight: 500;
  margin-bottom: 6px;
}

.stream-log pre {
  margin: 0;
  max-height: 220px;
  overflow: auto;
  white-space: pre-wrap;
  word-break: break-word;
  font-size: 12px;
}
</style>
