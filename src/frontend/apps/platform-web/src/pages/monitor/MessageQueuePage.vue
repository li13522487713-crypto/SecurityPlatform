<template>
  <div class="mq-page">
    <a-page-header :title="t('monitor.mq.title')" :sub-title="t('monitor.mq.subtitle')" />

    <a-row :gutter="16" class="stats-row">
      <a-col v-for="item in globalStatItems" :key="item.key" :span="6">
        <a-statistic :title="statLabel(item.key)" :value="item.value" />
      </a-col>
    </a-row>

    <a-card :title="t('monitor.mq.cardQueues')" :bordered="false" class="queue-card">
      <template #extra>
        <a-button :loading="loading" @click="fetchAll">
          <ReloadOutlined />{{ t("monitor.mq.refresh") }}
        </a-button>
      </template>
      <a-table
        :columns="queueColumns"
        :data-source="queues"
        :loading="loading"
        row-key="queueName"
        :pagination="false"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'actions'">
            <a-space>
              <a-button size="small" type="link" @click="openMessages(record.queueName)">{{
                t("monitor.mq.viewMessages")
              }}</a-button>
              <a-popconfirm :title="t('monitor.mq.confirmRetry')" @confirm="retryDeadLetters(record.queueName)">
                <a-button size="small" type="link" :disabled="record.deadLettered === 0">{{
                  t("monitor.mq.retryDlq")
                }}</a-button>
              </a-popconfirm>
              <a-popconfirm :title="t('monitor.mq.confirmPurge')" @confirm="deleteDeadLetters(record.queueName)">
                <a-button size="small" type="link" danger :disabled="record.deadLettered === 0">{{
                  t("monitor.mq.purgeDlq")
                }}</a-button>
              </a-popconfirm>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <a-drawer
      v-if="selectedQueue"
      :title="t('monitor.mq.drawerTitle', { name: selectedQueue })"
      width="760"
      :open="true"
      @close="selectedQueue = null"
    >
      <a-space class="filter-bar">
        <a-select
          v-model:value="messageStatusFilter"
          allow-clear
          :placeholder="t('monitor.mq.phStatus')"
          style="width: 140px"
          @change="fetchMessages"
        >
          <a-select-option value="0">{{ t("monitor.mq.stPending") }}</a-select-option>
          <a-select-option value="1">{{ t("monitor.mq.stProcessing") }}</a-select-option>
          <a-select-option value="2">{{ t("monitor.mq.stCompleted") }}</a-select-option>
          <a-select-option value="3">{{ t("monitor.mq.stFailed") }}</a-select-option>
          <a-select-option value="4">{{ t("monitor.mq.stDeadLettered") }}</a-select-option>
        </a-select>
      </a-space>
      <a-table
        :columns="msgColumns"
        :data-source="messages"
        :loading="msgLoading"
        row-key="id"
        :pagination="{ total: msgTotal, current: msgPage, pageSize: 20, onChange: (p: number) => { msgPage = p; fetchMessages() } }"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'status'">
            <a-tag :color="statusColor(record.status)">{{ messageStatusLabel(record.status) }}</a-tag>
          </template>
        </template>
      </a-table>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import type { TableColumnsType } from "ant-design-vue";
import { message } from "ant-design-vue";
import { ReloadOutlined } from "@ant-design/icons-vue";
import { useI18n } from "vue-i18n";
import {
  getMessageQueueMessages,
  getMessageQueueQueues,
  getMessageQueueStats,
  purgeMessageQueueDeadLetters,
  retryMessageQueueDeadLetters,
  type QueueMsg,
  type QueueStat
} from "@/services/api-monitor";

const { t, locale } = useI18n();
const loading = ref(false);
const queues = ref<QueueStat[]>([]);
const globalStats = ref<QueueStat | null>(null);
const selectedQueue = ref<string | null>(null);
const messages = ref<QueueMsg[]>([]);
const msgLoading = ref(false);
const msgTotal = ref(0);
const msgPage = ref(1);
const messageStatusFilter = ref<string | undefined>();

const queueColumns = computed<TableColumnsType<QueueStat>>(() => [
  { title: t("monitor.mq.colQueue"), dataIndex: "queueName", key: "queueName" },
  { title: t("monitor.mq.colPending"), dataIndex: "pending", key: "pending" },
  { title: t("monitor.mq.colProcessing"), dataIndex: "processing", key: "processing" },
  { title: t("monitor.mq.colCompleted"), dataIndex: "completed", key: "completed" },
  { title: t("monitor.mq.colFailed"), dataIndex: "failed", key: "failed" },
  { title: t("monitor.mq.colDlq"), dataIndex: "deadLettered", key: "deadLettered" },
  { title: t("monitor.mq.colActions"), key: "actions", width: 220 }
]);

function formatEnqueuedAt(value: string) {
  return new Date(value).toLocaleString(locale.value === "en-US" ? "en-US" : "zh-CN");
}

const msgColumns = computed<TableColumnsType<QueueMsg>>(() => [
  { title: "ID", dataIndex: "id", key: "id", width: 80 },
  { title: t("monitor.mq.colMsgType"), dataIndex: "messageType", key: "messageType" },
  { title: t("monitor.mq.colStatus"), key: "status" },
  { title: t("monitor.mq.colRetries"), dataIndex: "retryCount", key: "retryCount", width: 80 },
  { title: t("monitor.mq.colError"), dataIndex: "errorMessage", key: "errorMessage", ellipsis: true },
  {
    title: t("monitor.mq.colEnqueued"),
    dataIndex: "enqueuedAt",
    key: "enqueuedAt",
    customRender: ({ value }: { value: string }) => formatEnqueuedAt(value)
  }
]);

const globalStatItems = computed(() => {
  const stats = globalStats.value;
  if (!stats) {
    return [];
  }
  return [
    { key: "pending", value: stats.pending },
    { key: "processing", value: stats.processing },
    { key: "completed", value: stats.completed },
    { key: "failed", value: stats.failed },
    { key: "deadLettered", value: stats.deadLettered }
  ];
});

function statLabel(key: string) {
  const map: Record<string, string> = {
    pending: t("monitor.mq.legendPending"),
    processing: t("monitor.mq.legendProcessing"),
    completed: t("monitor.mq.legendCompleted"),
    failed: t("monitor.mq.legendFailed"),
    deadLettered: t("monitor.mq.legendDlq")
  };
  return map[key] ?? key;
}

async function fetchAll() {
  loading.value = true;
  try {
    const [queueData, statsData] = await Promise.all([getMessageQueueQueues(), getMessageQueueStats()]);
    queues.value = queueData;
    globalStats.value = statsData;
  } catch (error: unknown) {
    queues.value = [];
    globalStats.value = null;
    const status = (error as { status?: number }).status;
    if (status !== 401) {
      message.error(t("monitor.mq.loadFailed"));
    }
  } finally {
    loading.value = false;
  }
}

async function fetchMessages() {
  if (!selectedQueue.value) return;
  msgLoading.value = true;
  try {
    const data = await getMessageQueueMessages(selectedQueue.value, {
      pageIndex: msgPage.value,
      pageSize: 20,
      status: messageStatusFilter.value
    });
    if (data) {
      messages.value = data.items;
      msgTotal.value = data.total;
    } else {
      messages.value = [];
      msgTotal.value = 0;
    }
  } finally {
    msgLoading.value = false;
  }
}

function openMessages(queueName: string) {
  selectedQueue.value = queueName;
  msgPage.value = 1;
  messageStatusFilter.value = undefined;
  void fetchMessages();
}

async function retryDeadLetters(queueName: string) {
  await retryMessageQueueDeadLetters(queueName);
  message.success(t("monitor.mq.retryTriggered"));
  void fetchAll();
}

async function deleteDeadLetters(queueName: string) {
  await purgeMessageQueueDeadLetters(queueName);
  message.success(t("monitor.mq.dlqCleared"));
  void fetchAll();
}

function statusColor(status: string) {
  const map: Record<string, string> = {
    Pending: "blue",
    Processing: "orange",
    Completed: "green",
    Failed: "red",
    DeadLettered: "purple"
  };
  return map[status] ?? "default";
}

function messageStatusLabel(status: string) {
  const keyByStatus: Record<string, string> = {
    Pending: "monitor.mq.stPending",
    Processing: "monitor.mq.stProcessing",
    Completed: "monitor.mq.stCompleted",
    Failed: "monitor.mq.stFailed",
    DeadLettered: "monitor.mq.stDeadLettered"
  };
  const key = keyByStatus[status];
  return key ? t(key) : status;
}

onMounted(() => {
  void fetchAll();
});
</script>

<style scoped>
.mq-page {
  padding: 0 24px 24px;
}

.stats-row {
  margin-bottom: 16px;
  background: #fff;
  padding: 16px;
  border-radius: 8px;
}

.queue-card {
  margin-bottom: 16px;
}

.filter-bar {
  margin-bottom: 12px;
}
</style>
