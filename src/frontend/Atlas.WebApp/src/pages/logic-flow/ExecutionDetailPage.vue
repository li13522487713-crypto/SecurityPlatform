<template>
  <div class="execution-detail-page">
    <a-page-header @back="$router.back()">
      <template #title>
        <a-space wrap>
          <span>{{ t("logicFlow.executionDetailPage.title") }}</span>
          <a-typography-text code>{{ executionId }}</a-typography-text>
          <a-tag :color="statusColor(execStatus)">{{ statusLabel(execStatus) }}</a-tag>
        </a-space>
      </template>
      <template #sub-title>
        <a-space wrap>
          <span>{{ t("logicFlow.duration") }}: {{ durationLabel }}</span>
          <span>{{ t("logicFlow.startedAt") }}: {{ startedAt }}</span>
          <span>{{ t("logicFlow.completedAt") }}: {{ completedAt }}</span>
        </a-space>
      </template>
      <template #extra>
        <a-space wrap>
          <a-button @click="emitAction('cancel')">{{ t("logicFlow.cancel") }}</a-button>
          <a-button @click="emitAction('retry')">{{ t("logicFlow.retry") }}</a-button>
          <a-button @click="emitAction('pause')">{{ t("logicFlow.pause") }}</a-button>
          <a-button type="primary" @click="emitAction('resume')">{{ t("logicFlow.resume") }}</a-button>
        </a-space>
      </template>
    </a-page-header>

    <a-row :gutter="16" class="body-row">
      <a-col :xs="24" :xl="14">
        <a-card size="small" :title="t('logicFlow.executionDetailPage.canvasProgress')" :bordered="false">
          <div class="exec-canvas-placeholder">
            <a-space wrap>
              <a-tag v-for="n in nodeStates" :key="n.id" :color="n.color">{{ n.label }}</a-tag>
            </a-space>
          </div>
        </a-card>
        <a-card size="small" :title="t('logicFlow.executionDetailPage.nodeTimeline')" :bordered="false" class="mt-card">
          <a-timeline>
            <a-timeline-item v-for="ev in timelineEvents" :key="ev.id" :color="ev.color">
              <div class="tl-title">{{ ev.title }}</div>
              <div class="tl-meta">{{ ev.meta }}</div>
            </a-timeline-item>
          </a-timeline>
        </a-card>
      </a-col>
      <a-col :xs="24" :xl="10">
        <a-card size="small" :title="t('logicFlow.executionDetailPage.inputData')" :bordered="false">
          <pre class="json-block">{{ inputJson }}</pre>
        </a-card>
        <a-card size="small" :title="t('logicFlow.executionDetailPage.outputData')" :bordered="false" class="mt-card">
          <pre class="json-block">{{ outputJson }}</pre>
        </a-card>
        <a-card size="small" :title="t('logicFlow.executionDetailPage.errorPanel')" :bordered="false" class="mt-card">
          <a-empty v-if="!errorText" :description="t('logicFlow.executionDetailPage.noError')" />
          <pre v-else class="json-block err">{{ errorText }}</pre>
        </a-card>
      </a-col>
    </a-row>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import { useI18n } from "vue-i18n";
import { useRoute } from "vue-router";

type ExecStatusKey = "pending" | "running" | "completed" | "failed" | "cancelled" | "paused";

const { t } = useI18n();
const route = useRoute();

const executionId = computed(() => String(route.params.id ?? ""));

const execStatus = ref<ExecStatusKey>("running");
const startedAt = ref("2026-04-02 10:00:00");
const completedAt = ref("—");
const durationLabel = ref("12s");
const inputJson = ref('{\n  "orderId": "1001"\n}');
const outputJson = ref('{\n  "ok": true\n}');
const errorText = ref("");

const nodeStates = ref<{ id: string; label: string; color: string }[]>([
  { id: "1", label: "Start · ok", color: "green" },
  { id: "2", label: "Query · running", color: "blue" },
  { id: "3", label: "End · pending", color: "default" }
]);

const timelineEvents = ref<{ id: string; title: string; meta: string; color: string }[]>([
  { id: "e1", title: "Start", meta: "0ms", color: "green" },
  { id: "e2", title: "Query", meta: "+120ms", color: "blue" }
]);

function statusColor(s: ExecStatusKey): string {
  const map: Record<ExecStatusKey, string> = {
    pending: "default",
    running: "processing",
    completed: "success",
    failed: "error",
    cancelled: "warning",
    paused: "orange"
  };
  return map[s];
}

function statusLabel(s: ExecStatusKey): string {
  const map: Record<ExecStatusKey, string> = {
    pending: t("logicFlow.executionStatus.pending"),
    running: t("logicFlow.executionStatus.running"),
    completed: t("logicFlow.executionStatus.completed"),
    failed: t("logicFlow.executionStatus.failed"),
    cancelled: t("logicFlow.executionStatus.cancelled"),
    paused: t("logicFlow.executionStatus.paused")
  };
  return map[s];
}

function emitAction(_action: "cancel" | "retry" | "pause" | "resume"): void {
  // 占位：接入 FlowExecutions API
}
</script>

<style scoped>
.execution-detail-page {
  padding-bottom: 24px;
}

.body-row {
  padding: 0 24px;
}

.exec-canvas-placeholder {
  min-height: 160px;
  padding: 16px;
  background: #fafafa;
  border: 1px dashed #d9d9d9;
  border-radius: 4px;
}

.mt-card {
  margin-top: 16px;
}

.json-block {
  margin: 0;
  font-size: 12px;
  line-height: 1.5;
  white-space: pre-wrap;
  word-break: break-word;
}

.json-block.err {
  color: #cf1322;
}

.tl-title {
  font-weight: 500;
}

.tl-meta {
  font-size: 12px;
  color: rgba(0, 0, 0, 0.45);
}
</style>
