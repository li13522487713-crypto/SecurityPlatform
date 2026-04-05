<template>
  <div class="instance-detail-page">
    <a-page-header :title="t('approvalDetail.title', { name: instance?.flowName || '-' })" @back="router.back()">
      <template #tags>
        <a-tag :color="statusColor(instance?.status)">{{ statusText(instance?.status) }}</a-tag>
      </template>
      <template #extra>
        <a-button v-if="canCancel" danger @click="handleCancel">{{ t("approvalDetail.cancel") }}</a-button>
        <a-button v-if="canSuspend" @click="handleSuspend">{{ t("approvalDetail.suspend") }}</a-button>
        <a-button v-if="canActivate" type="primary" @click="handleActivate">{{ t("approvalDetail.activate") }}</a-button>
      </template>
    </a-page-header>

    <a-spin :spinning="loading">
      <a-row :gutter="16">
        <a-col :span="16">
          <a-card :title="t('approvalDetail.formData')" size="small">
            <pre class="json-preview">{{ formDataText }}</pre>
          </a-card>
        </a-col>
        <a-col :span="8">
          <a-card :title="t('approvalDetail.history')" size="small">
            <a-timeline>
              <a-timeline-item v-for="event in historyEvents" :key="event.id" :color="eventColor(event.eventType)">
                <div class="event-line">
                  <span>{{ eventText(event.eventType) }}</span>
                  <span>{{ formatTime(event.occurredAt) }}</span>
                </div>
                <div v-if="event.payloadJson" class="event-payload">{{ event.payloadJson }}</div>
              </a-timeline-item>
            </a-timeline>
          </a-card>
        </a-col>
      </a-row>
    </a-spin>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { message } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import {
  activateInstance,
  cancelApprovalInstance,
  getApprovalInstanceById,
  getApprovalInstanceHistory,
  suspendInstance,
  type ApprovalHistoryEvent,
  type ApprovalInstanceDetail,
} from "@/services/api-approval";

const { t, locale } = useI18n();
const route = useRoute();
const router = useRouter();
const instanceId = computed(() => (typeof route.params.instanceId === "string" ? route.params.instanceId : ""));

const loading = ref(false);
const instance = ref<ApprovalInstanceDetail | null>(null);
const historyEvents = ref<ApprovalHistoryEvent[]>([]);

const canCancel = computed(() => instance.value?.status === 0);
const canSuspend = computed(() => instance.value?.status === 0);
const canActivate = computed(() => instance.value?.status === 4);

const formDataText = computed(() => {
  if (!instance.value?.dataJson) {
    return t("approvalDetail.emptyFormData");
  }
  try {
    const parsed = JSON.parse(instance.value.dataJson);
    return JSON.stringify(parsed, null, 2);
  } catch {
    return instance.value.dataJson;
  }
});

const statusText = (status?: number) => {
  const map: Record<number, string> = {
    0: t("approvalWorkspace.statusRunning"),
    1: t("approvalWorkspace.statusCompleted"),
    2: t("approvalWorkspace.statusRejected"),
    3: t("approvalWorkspace.statusCancelled"),
    4: t("approvalDetail.statusSuspended"),
  };
  return status === undefined ? "-" : (map[status] || String(status));
};

const statusColor = (status?: number) => {
  const map: Record<number, string> = {
    0: "processing",
    1: "success",
    2: "error",
    3: "default",
    4: "orange",
  };
  return status === undefined ? "default" : (map[status] || "default");
};

const eventText = (eventType: number) => {
  const map: Record<number, string> = {
    1: t("approvalDetail.eventStarted"),
    2: t("approvalDetail.eventTaskCreated"),
    3: t("approvalDetail.eventApproved"),
    4: t("approvalDetail.eventRejected"),
    5: t("approvalDetail.eventAdvanced"),
    6: t("approvalDetail.eventCompleted"),
    7: t("approvalDetail.eventCancelled"),
  };
  return map[eventType] || t("approvalDetail.eventFallback");
};

const eventColor = (eventType: number) => {
  const map: Record<number, string> = {
    3: "green",
    6: "green",
    4: "red",
    7: "orange",
  };
  return map[eventType] || "blue";
};

const formatTime = (value: string) => {
  const language = locale.value === "en-US" ? "en-US" : "zh-CN";
  return new Date(value).toLocaleString(language, { hour12: false });
};

const loadData = async () => {
  if (!instanceId.value) {
    return;
  }
  loading.value = true;
  try {
    const [detail, history] = await Promise.all([
      getApprovalInstanceById(instanceId.value),
      getApprovalInstanceHistory(instanceId.value, { pageIndex: 1, pageSize: 100 }),
    ]);
    instance.value = detail;
    historyEvents.value = history.items;
  } catch (error) {
    message.error((error as Error).message || t("approvalDetail.loadFailed"));
  } finally {
    loading.value = false;
  }
};

const handleCancel = async () => {
  if (!instanceId.value) return;
  try {
    await cancelApprovalInstance(instanceId.value);
    message.success(t("approvalDetail.cancelSuccess"));
    await loadData();
  } catch (error) {
    message.error((error as Error).message || t("approvalDetail.cancelFailed"));
  }
};

const handleSuspend = async () => {
  if (!instanceId.value) return;
  try {
    await suspendInstance(instanceId.value);
    message.success(t("approvalDetail.suspendSuccess"));
    await loadData();
  } catch (error) {
    message.error((error as Error).message || t("approvalDetail.suspendFailed"));
  }
};

const handleActivate = async () => {
  if (!instanceId.value) return;
  try {
    await activateInstance(instanceId.value);
    message.success(t("approvalDetail.activateSuccess"));
    await loadData();
  } catch (error) {
    message.error((error as Error).message || t("approvalDetail.activateFailed"));
  }
};

onMounted(() => {
  void loadData();
});
</script>

<style scoped>
.instance-detail-page {
  padding: 16px;
}

.json-preview {
  margin: 0;
  white-space: pre-wrap;
  word-break: break-word;
  max-height: 60vh;
  overflow: auto;
}

.event-line {
  display: flex;
  justify-content: space-between;
  gap: 8px;
}

.event-payload {
  margin-top: 4px;
  color: #666;
  font-size: 12px;
}
</style>
