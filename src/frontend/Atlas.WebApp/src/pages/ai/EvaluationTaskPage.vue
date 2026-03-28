<template>
  <a-card :title="t('ai.evaluation.taskPageTitle')" :bordered="false">
    <div class="toolbar">
      <a-space wrap>
        <a-button type="primary" @click="openCreateModal">
          {{ t("ai.evaluation.newTask") }}
        </a-button>
        <a-button @click="goDatasetPage">
          {{ t("ai.evaluation.gotoDatasetPage") }}
        </a-button>
        <a-button @click="loadTasks">
          {{ t("common.refresh") }}
        </a-button>
      </a-space>
    </div>

    <a-table
      row-key="id"
      :columns="columns"
      :data-source="rows"
      :loading="loading"
      :pagination="false"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'status'">
          <a-tag :color="statusColor(record.status)">
            {{ statusText(record.status) }}
          </a-tag>
        </template>
        <template v-else-if="column.key === 'progress'">
          <span>{{ `${record.completedCases}/${record.totalCases}` }}</span>
        </template>
        <template v-else-if="column.key === 'score'">
          {{ Number(record.score ?? 0).toFixed(4) }}
        </template>
        <template v-else-if="column.key === 'updatedAt'">
          {{ formatDateTime(record.updatedAt) }}
        </template>
        <template v-else-if="column.key === 'errorMessage'">
          <span>{{ record.errorMessage || "-" }}</span>
        </template>
        <template v-else-if="column.key === 'action'">
          <a-space>
            <a-button type="link" @click="goReport(record.id)">
              {{ t("ai.evaluation.viewReport") }}
            </a-button>
          </a-space>
        </template>
      </template>
    </a-table>

    <div class="pager">
      <a-pagination
        v-model:current="pageIndex"
        v-model:page-size="pageSize"
        :total="total"
        show-size-changer
        :page-size-options="['10', '20', '50']"
        @change="loadTasks"
      />
    </div>
  </a-card>

  <a-modal
    v-model:open="createModalOpen"
    :title="t('ai.evaluation.newTask')"
    :confirm-loading="saving"
    @ok="handleCreateTask"
  >
    <a-form ref="formRef" layout="vertical" :model="form" :rules="rules">
      <a-form-item :label="t('ai.evaluation.taskName')" name="name">
        <a-input v-model:value="form.name" :maxlength="64" />
      </a-form-item>
      <a-form-item :label="t('ai.evaluation.taskDataset')" name="datasetId">
        <a-select
          v-model:value="form.datasetId"
          show-search
          :filter-option="false"
          :options="datasetOptions"
          :placeholder="t('ai.evaluation.taskDatasetPlaceholder')"
          :not-found-content="datasetLoading ? t('common.loading') : undefined"
          @search="searchDatasets"
        />
      </a-form-item>
      <a-form-item :label="t('ai.evaluation.taskAgent')" name="agentId">
        <a-select
          v-model:value="form.agentId"
          show-search
          :filter-option="false"
          :options="agentOptions"
          :placeholder="t('ai.evaluation.taskAgentPlaceholder')"
          :not-found-content="agentLoading ? t('common.loading') : undefined"
          @search="searchAgents"
        />
      </a-form-item>
    </a-form>
  </a-modal>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, reactive, ref } from "vue";
import type { FormInstance } from "ant-design-vue";
import { message } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import { useRoute, useRouter } from "vue-router";
import {
  createEvaluationTask,
  getEvaluationDatasetsPaged,
  getEvaluationTasksPaged,
  type EvaluationTaskDto,
  type EvaluationTaskStatus
} from "@/services/api-evaluation";
import { getAgentsPaged } from "@/services/api-agent";
import { resolveCurrentAppId } from "@/utils/app-context";
import { formatDateTime } from "@/utils/common";

const { t } = useI18n();
const route = useRoute();
const router = useRouter();

const rows = ref<EvaluationTaskDto[]>([]);
const loading = ref(false);
const pageIndex = ref(1);
const pageSize = ref(20);
const total = ref(0);

let pollTimer: number | null = null;

const createModalOpen = ref(false);
const saving = ref(false);
const formRef = ref<FormInstance>();
const form = reactive({
  name: "",
  datasetId: undefined as number | undefined,
  agentId: undefined as string | undefined
});

const rules = computed(() => ({
  name: [{ required: true, message: t("ai.evaluation.ruleTaskName") }],
  datasetId: [{ required: true, message: t("ai.evaluation.ruleTaskDataset") }],
  agentId: [{ required: true, message: t("ai.evaluation.ruleTaskAgent") }]
}));

const datasetOptions = ref<Array<{ label: string; value: number }>>([]);
const datasetLoading = ref(false);
const agentOptions = ref<Array<{ label: string; value: string }>>([]);
const agentLoading = ref(false);

const columns = computed(() => [
  { title: t("ai.evaluation.taskName"), dataIndex: "name", key: "name" },
  { title: t("ai.evaluation.taskDataset"), dataIndex: "datasetId", key: "datasetId", width: 140 },
  { title: t("ai.evaluation.taskAgent"), dataIndex: "agentId", key: "agentId", width: 120 },
  { title: t("ai.evaluation.taskStatus"), dataIndex: "status", key: "status", width: 120 },
  { title: t("ai.evaluation.taskProgress"), key: "progress", width: 120 },
  { title: t("ai.evaluation.taskScore"), key: "score", width: 110 },
  { title: t("ai.evaluation.taskUpdatedAt"), dataIndex: "updatedAt", key: "updatedAt", width: 180 },
  { title: t("ai.evaluation.taskError"), dataIndex: "errorMessage", key: "errorMessage", ellipsis: true },
  { title: t("ai.colActions"), key: "action", width: 120 }
]);

function statusText(status: EvaluationTaskStatus) {
  switch (status) {
    case 1:
      return t("ai.evaluation.statusRunning");
    case 2:
      return t("ai.evaluation.statusCompleted");
    case 3:
      return t("ai.evaluation.statusFailed");
    default:
      return t("ai.evaluation.statusPending");
  }
}

function statusColor(status: EvaluationTaskStatus) {
  switch (status) {
    case 1:
      return "processing";
    case 2:
      return "success";
    case 3:
      return "error";
    default:
      return "default";
  }
}

function clearPolling() {
  if (pollTimer !== null) {
    window.clearInterval(pollTimer);
    pollTimer = null;
  }
}

function updatePollingState() {
  const needPolling = rows.value.some((item) => item.status === 0 || item.status === 1);
  if (!needPolling) {
    clearPolling();
    return;
  }
  if (pollTimer === null) {
    pollTimer = window.setInterval(() => {
      void loadTasks();
    }, 5000);
  }
}

async function loadTasks() {
  loading.value = true;
  try {
    const result = await getEvaluationTasksPaged({
      pageIndex: pageIndex.value,
      pageSize: pageSize.value
    });
    rows.value = result.items;
    total.value = Number(result.total);
    updatePollingState();
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.evaluation.taskLoadFailed"));
  } finally {
    loading.value = false;
  }
}

function openCreateModal() {
  form.name = "";
  form.datasetId = parseQueryDatasetId();
  form.agentId = undefined;
  createModalOpen.value = true;
}

async function searchDatasets(keyword: string) {
  datasetLoading.value = true;
  try {
    const result = await getEvaluationDatasetsPaged({
      pageIndex: 1,
      pageSize: 20,
      keyword: keyword || undefined
    });
    datasetOptions.value = result.items.map((item) => ({
      label: `${item.name} (#${item.id})`,
      value: item.id
    }));
  } catch {
    datasetOptions.value = [];
  } finally {
    datasetLoading.value = false;
  }
}

async function searchAgents(keyword: string) {
  agentLoading.value = true;
  try {
    const result = await getAgentsPaged({
      pageIndex: 1,
      pageSize: 20,
      keyword: keyword || undefined
    });
    agentOptions.value = result.items.map((item) => ({
      label: `${item.name} (#${item.id})`,
      value: item.id
    }));
  } catch {
    agentOptions.value = [];
  } finally {
    agentLoading.value = false;
  }
}

async function handleCreateTask() {
  try {
    await formRef.value?.validate();
  } catch {
    return;
  }

  saving.value = true;
  try {
    const taskId = await createEvaluationTask({
      name: form.name.trim(),
      datasetId: form.datasetId as number,
      agentId: form.agentId as string
    });
    message.success(t("crud.createSuccess"));
    createModalOpen.value = false;
    await loadTasks();
    goReport(taskId);
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.evaluation.taskCreateFailed"));
  } finally {
    saving.value = false;
  }
}

function parseQueryDatasetId() {
  const raw = route.query.datasetId;
  const value = typeof raw === "string" ? Number(raw) : Number.NaN;
  return Number.isFinite(value) && value > 0 ? value : undefined;
}

function goReport(taskId: number) {
  const currentAppId = resolveCurrentAppId(route);
  if (currentAppId) {
    void router.push(`/apps/${currentAppId}/evaluations/reports/${taskId}`);
    return;
  }
  void router.push(`/ai/devops/evaluations/reports/${taskId}`);
}

function goDatasetPage() {
  const currentAppId = resolveCurrentAppId(route);
  if (currentAppId) {
    void router.push(`/apps/${currentAppId}/evaluations/datasets`);
    return;
  }
  void router.push("/ai/devops/test-sets");
}

onMounted(() => {
  void loadTasks();
  void searchDatasets("");
  void searchAgents("");
});

onUnmounted(() => {
  clearPolling();
});
</script>

<style scoped>
.toolbar {
  margin-bottom: 16px;
}

.pager {
  margin-top: 16px;
  display: flex;
  justify-content: flex-end;
}
</style>
