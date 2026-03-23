<template>
  <a-space direction="vertical" style="width: 100%" :size="16">
    <a-card :title="t('ai.evaluation.reportPageTitle')" :bordered="false" :loading="loading">
      <template #extra>
        <a-space>
          <a-button @click="goTaskPage">{{ t("ai.evaluation.gotoTaskPage") }}</a-button>
          <a-button @click="loadReport">{{ t("common.refresh") }}</a-button>
        </a-space>
      </template>

      <a-descriptions v-if="taskDetail" :column="3" bordered size="small">
        <a-descriptions-item :label="t('ai.evaluation.taskName')">
          {{ taskDetail.name }}
        </a-descriptions-item>
        <a-descriptions-item :label="t('ai.evaluation.taskStatus')">
          <a-tag :color="taskStatusColor(taskDetail.status)">
            {{ taskStatusText(taskDetail.status) }}
          </a-tag>
        </a-descriptions-item>
        <a-descriptions-item :label="t('ai.evaluation.taskScore')">
          {{ Number(taskDetail.score ?? 0).toFixed(4) }}
        </a-descriptions-item>
        <a-descriptions-item :label="t('ai.evaluation.taskProgress')">
          {{ `${taskDetail.completedCases}/${taskDetail.totalCases}` }}
        </a-descriptions-item>
        <a-descriptions-item :label="t('ai.evaluation.taskUpdatedAt')">
          {{ formatDateTime(taskDetail.updatedAt) }}
        </a-descriptions-item>
        <a-descriptions-item :label="t('ai.evaluation.taskError')">
          {{ taskDetail.errorMessage || "-" }}
        </a-descriptions-item>
      </a-descriptions>

      <a-alert
        v-if="taskDetail?.errorMessage"
        type="error"
        show-icon
        style="margin-top: 12px"
        :message="taskDetail.errorMessage"
      />
    </a-card>

    <a-card :title="t('ai.evaluation.reportChartTitle')" :bordered="false">
      <div ref="chartRef" class="chart-container"></div>
    </a-card>

    <a-card :title="t('ai.evaluation.compareTitle')" :bordered="false">
      <a-space wrap>
        <a-select
          v-model:value="rightTaskId"
          style="width: 320px"
          :placeholder="t('ai.evaluation.compareTaskPlaceholder')"
          :options="taskOptions"
        />
        <a-button type="primary" :loading="compareLoading" @click="handleCompare">
          {{ t("ai.evaluation.compareNow") }}
        </a-button>
      </a-space>

      <a-alert
        v-if="comparison"
        style="margin-top: 12px"
        type="info"
        show-icon
        :message="comparisonSummary"
      />
    </a-card>

    <a-card :title="t('ai.evaluation.resultTableTitle')" :bordered="false">
      <a-table
        row-key="id"
        :columns="resultColumns"
        :data-source="resultRows"
        :loading="loading"
        :pagination="{ pageSize: 10 }"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'status'">
            <a-tag :color="caseStatusColor(record.status)">
              {{ caseStatusText(record.status) }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'score'">
            {{ Number(record.score ?? 0).toFixed(4) }}
          </template>
          <template v-else-if="column.key === 'createdAt'">
            {{ formatDateTime(record.createdAt) }}
          </template>
        </template>
      </a-table>
    </a-card>
  </a-space>
</template>

<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from "vue";
import { message } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import { useRoute, useRouter } from "vue-router";
import * as echarts from "echarts";
import {
  compareEvaluationTasks,
  getEvaluationTask,
  getEvaluationTaskResults,
  getEvaluationTasksPaged,
  type EvaluationCaseStatus,
  type EvaluationComparisonResult,
  type EvaluationResultDto,
  type EvaluationTaskDto,
  type EvaluationTaskStatus
} from "@/services/api-evaluation";
import { resolveCurrentAppId } from "@/utils/app-context";
import { formatDateTime } from "@/utils/common";

const { t } = useI18n();
const route = useRoute();
const router = useRouter();

const loading = ref(false);
const taskDetail = ref<EvaluationTaskDto | null>(null);
const resultRows = ref<EvaluationResultDto[]>([]);

const chartRef = ref<HTMLElement | null>(null);
let chart: echarts.ECharts | null = null;

const rightTaskId = ref<number | undefined>(undefined);
const taskOptions = ref<Array<{ label: string; value: number }>>([]);
const comparison = ref<EvaluationComparisonResult | null>(null);
const compareLoading = ref(false);

const taskId = computed(() => {
  const value = Number(route.params.taskId || 0);
  return Number.isFinite(value) ? value : 0;
});

const resultColumns = computed(() => [
  { title: t("ai.evaluation.resultCaseId"), dataIndex: "caseId", key: "caseId", width: 120 },
  { title: t("ai.evaluation.resultStatus"), dataIndex: "status", key: "status", width: 120 },
  { title: t("ai.evaluation.resultScore"), dataIndex: "score", key: "score", width: 120 },
  { title: t("ai.evaluation.resultJudgeReason"), dataIndex: "judgeReason", key: "judgeReason", ellipsis: true },
  { title: t("ai.evaluation.resultActualOutput"), dataIndex: "actualOutput", key: "actualOutput", ellipsis: true },
  { title: t("ai.evaluation.resultCreatedAt"), dataIndex: "createdAt", key: "createdAt", width: 180 }
]);

const comparisonSummary = computed(() => {
  if (!comparison.value) {
    return "";
  }

  return t("ai.evaluation.compareSummary", {
    leftTaskId: comparison.value.leftTaskId,
    leftScore: Number(comparison.value.leftScore ?? 0).toFixed(4),
    rightTaskId: comparison.value.rightTaskId,
    rightScore: Number(comparison.value.rightScore ?? 0).toFixed(4),
    delta: Number(comparison.value.delta ?? 0).toFixed(4),
    winner: winnerText(comparison.value.winner)
  });
});

function taskStatusText(status: EvaluationTaskStatus) {
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

function taskStatusColor(status: EvaluationTaskStatus) {
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

function caseStatusText(status: EvaluationCaseStatus) {
  switch (status) {
    case 1:
      return t("ai.evaluation.caseStatusPassed");
    case 2:
      return t("ai.evaluation.caseStatusFailed");
    case 3:
      return t("ai.evaluation.caseStatusError");
    default:
      return t("ai.evaluation.caseStatusPending");
  }
}

function caseStatusColor(status: EvaluationCaseStatus) {
  switch (status) {
    case 1:
      return "success";
    case 2:
      return "warning";
    case 3:
      return "error";
    default:
      return "default";
  }
}

function winnerText(winner: "left" | "right" | "draw") {
  if (winner === "left") {
    return t("ai.evaluation.compareWinnerLeft");
  }
  if (winner === "right") {
    return t("ai.evaluation.compareWinnerRight");
  }
  return t("ai.evaluation.compareWinnerDraw");
}

async function loadTaskOptions() {
  try {
    const result = await getEvaluationTasksPaged({
      pageIndex: 1,
      pageSize: 100
    });
    taskOptions.value = result.items
      .filter((item) => item.id !== taskId.value)
      .map((item) => ({
        label: `${item.name} (#${item.id})`,
        value: item.id
      }));
  } catch {
    taskOptions.value = [];
  }
}

async function loadReport() {
  if (!taskId.value) {
    message.warning(t("ai.evaluation.invalidTaskId"));
    return;
  }

  loading.value = true;
  try {
    const [task, results] = await Promise.all([
      getEvaluationTask(taskId.value),
      getEvaluationTaskResults(taskId.value)
    ]);
    taskDetail.value = task;
    resultRows.value = results;
    comparison.value = null;
    await nextTick();
    renderChart();
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.evaluation.reportLoadFailed"));
  } finally {
    loading.value = false;
  }
}

function renderChart() {
  if (!chartRef.value) {
    return;
  }

  if (!chart) {
    chart = echarts.init(chartRef.value);
  }

  const scoreSeries = resultRows.value.slice(0, 20);
  const statusCountMap = resultRows.value.reduce<Record<string, number>>((acc, item) => {
    const key = caseStatusText(item.status);
    acc[key] = (acc[key] ?? 0) + 1;
    return acc;
  }, {});
  const pieData = Object.entries(statusCountMap).map(([name, value]) => ({ name, value }));

  chart.setOption({
    tooltip: { trigger: "axis" },
    legend: { top: 0 },
    grid: { left: "3%", right: "45%", bottom: "3%", containLabel: true },
    xAxis: {
      type: "category",
      data: scoreSeries.map((item) => `#${item.caseId}`),
      axisLabel: { interval: 0, rotate: 35 }
    },
    yAxis: {
      type: "value",
      min: 0,
      max: 1
    },
    series: [
      {
        name: t("ai.evaluation.resultScore"),
        type: "bar",
        barWidth: 24,
        data: scoreSeries.map((item) => Number(item.score ?? 0))
      },
      {
        name: t("ai.evaluation.caseStatusDistribution"),
        type: "pie",
        radius: ["35%", "58%"],
        center: ["82%", "50%"],
        label: { formatter: "{b}: {c}" },
        data: pieData
      }
    ]
  });
}

async function handleCompare() {
  if (!taskId.value || !rightTaskId.value) {
    message.warning(t("ai.evaluation.compareTaskRequired"));
    return;
  }

  compareLoading.value = true;
  try {
    comparison.value = await compareEvaluationTasks(taskId.value, rightTaskId.value);
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.evaluation.compareFailed"));
  } finally {
    compareLoading.value = false;
  }
}

function goTaskPage() {
  const currentAppId = resolveCurrentAppId(route);
  if (currentAppId) {
    void router.push(`/apps/${currentAppId}/evaluations/tasks`);
    return;
  }
  void router.push("/ai/devops/evaluations/tasks");
}

function handleWindowResize() {
  chart?.resize();
}

watch(() => taskId.value, () => {
  void loadTaskOptions();
  void loadReport();
});

onMounted(() => {
  void loadTaskOptions();
  void loadReport();
  window.addEventListener("resize", handleWindowResize);
});

onUnmounted(() => {
  window.removeEventListener("resize", handleWindowResize);
  chart?.dispose();
  chart = null;
});
</script>

<style scoped>
.chart-container {
  width: 100%;
  height: 360px;
}
</style>
