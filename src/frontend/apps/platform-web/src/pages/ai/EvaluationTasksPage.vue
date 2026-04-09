<template>
  <section class="evaluation-page">
    <a-card :title="t('aiEvaluation.title')" size="small">
      <a-table
        row-key="id"
        :columns="taskColumns"
        :data-source="taskRows"
        :loading="taskLoading"
        :pagination="pagination"
        :custom-row="buildTaskRowProps"
        @change="onTaskTableChange"
      />
    </a-card>

    <a-card :title="t('aiEvaluation.metricsCardTitle')" size="small">
      <a-alert v-if="!selectedTaskId" type="info" :message="t('aiEvaluation.emptyTip')" show-icon />
      <template v-else>
        <a-row :gutter="12" class="evaluation-page__stats">
          <a-col :span="8">
            <a-statistic :title="t('aiEvaluation.selectedTask')" :value="selectedTask?.name ?? selectedTaskId" />
          </a-col>
          <a-col :span="8">
            <a-statistic :title="t('aiEvaluation.aggregateScore')" :value="formatMetricValue(selectedTask?.score)" />
          </a-col>
          <a-col :span="8">
            <a-statistic :title="t('aiEvaluation.completedRate')" :value="completionRateText" />
          </a-col>
        </a-row>

        <div ref="radarRef" class="evaluation-page__radar" />

        <a-table
          row-key="id"
          size="small"
          :columns="resultColumns"
          :data-source="resultRows"
          :loading="resultLoading"
          :pagination="false"
        />
      </template>
    </a-card>

    <a-card :title="t('aiEvaluation.shadowReportTitle')" size="small">
      <a-row :gutter="12" class="evaluation-page__stats">
        <a-col :span="8">
          <a-statistic
            :title="t('aiEvaluation.shadowComparisonCount')"
            :value="shadowComparisons.length"
          />
        </a-col>
        <a-col :span="8">
          <a-statistic
            :title="t('aiEvaluation.shadowAverageOverlap')"
            :value="shadowAverageOverlap"
          />
        </a-col>
        <a-col :span="8">
          <a-statistic
            :title="t('aiEvaluation.shadowAverageGain')"
            :value="shadowAverageGain"
          />
        </a-col>
      </a-row>
      <a-table
        row-key="id"
        size="small"
        :columns="shadowColumns"
        :data-source="shadowComparisons"
        :loading="shadowLoading"
        :pagination="false"
      />
    </a-card>
  </section>
</template>

<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import type { TableColumnsType } from "ant-design-vue";
import { message } from "ant-design-vue";
import * as echarts from "echarts/core";
import { RadarChart } from "echarts/charts";
import { TooltipComponent, RadarComponent } from "echarts/components";
import { CanvasRenderer } from "echarts/renderers";
import type { PagedRequest } from "@atlas/shared-core";
import {
  getEvaluationTask,
  getEvaluationTaskResults,
  getEvaluationTasks,
  type EvaluationResultItem,
  type EvaluationTaskItem
} from "@/services/api-evaluations";
import {
  getRagShadowComparisons,
  type RagShadowComparisonItem
} from "@/services/api-rag-experiments";

echarts.use([RadarChart, TooltipComponent, RadarComponent, CanvasRenderer]);

const { t } = useI18n();
const radarRef = ref<HTMLElement>();
let radarChart: echarts.ECharts | null = null;

const taskRows = ref<EvaluationTaskItem[]>([]);
const taskLoading = ref(false);
const taskRequest = ref<PagedRequest>({
  pageIndex: 1,
  pageSize: 10,
  keyword: "",
  sortBy: "",
  sortDesc: true
});
const taskTotal = ref(0);

const selectedTaskId = ref<string>("");
const selectedTask = ref<EvaluationTaskItem | null>(null);
const resultRows = ref<EvaluationResultItem[]>([]);
const resultLoading = ref(false);
const shadowLoading = ref(false);
const shadowComparisons = ref<RagShadowComparisonItem[]>([]);

const taskColumns = computed<TableColumnsType<EvaluationTaskItem>>(() => [
  { title: t("aiEvaluation.taskName"), dataIndex: "name", key: "name", width: 240 },
  { title: t("aiEvaluation.status"), dataIndex: "status", key: "status", width: 120 },
  { title: t("aiEvaluation.score"), dataIndex: "score", key: "score", width: 120 },
  {
    title: t("aiEvaluation.progress"),
    key: "progress",
    width: 140,
    customRender: ({ record }) => `${record.completedCases}/${record.totalCases}`
  },
  { title: t("aiEvaluation.updatedAt"), dataIndex: "updatedAt", key: "updatedAt", width: 220 }
]);

const resultColumns = computed<TableColumnsType<EvaluationResultItem>>(() => [
  { title: "Case", dataIndex: "caseId", key: "caseId", width: 140 },
  { title: t("aiEvaluation.score"), dataIndex: "score", key: "score", width: 100 },
  { title: "Faithfulness", dataIndex: "faithfulnessScore", key: "faithfulnessScore", width: 130 },
  { title: "Context Precision", dataIndex: "contextPrecisionScore", key: "contextPrecisionScore", width: 150 },
  { title: "Context Recall", dataIndex: "contextRecallScore", key: "contextRecallScore", width: 140 },
  { title: "Answer Relevance", dataIndex: "answerRelevanceScore", key: "answerRelevanceScore", width: 150 },
  { title: "Citation Accuracy", dataIndex: "citationAccuracyScore", key: "citationAccuracyScore", width: 140 },
  { title: "Hallucination", dataIndex: "hallucinationScore", key: "hallucinationScore", width: 130 }
]);

const shadowColumns = computed<TableColumnsType<RagShadowComparisonItem>>(() => [
  { title: t("aiEvaluation.shadowExperiment"), dataIndex: "experimentName", key: "experimentName", width: 180 },
  { title: t("aiEvaluation.shadowMainVariant"), dataIndex: "mainVariant", key: "mainVariant", width: 130 },
  { title: t("aiEvaluation.shadowVariant"), dataIndex: "shadowVariant", key: "shadowVariant", width: 130 },
  {
    title: t("aiEvaluation.shadowOverlap"),
    dataIndex: "overlapScore",
    key: "overlapScore",
    width: 120,
    customRender: ({ record }) => formatMetricValue(record.overlapScore)
  },
  {
    title: t("aiEvaluation.shadowMainAvg"),
    dataIndex: "mainAvgScore",
    key: "mainAvgScore",
    width: 120,
    customRender: ({ record }) => formatMetricValue(record.mainAvgScore)
  },
  {
    title: t("aiEvaluation.shadowShadowAvg"),
    dataIndex: "shadowAvgScore",
    key: "shadowAvgScore",
    width: 120,
    customRender: ({ record }) => formatMetricValue(record.shadowAvgScore)
  },
  {
    title: t("aiEvaluation.shadowGain"),
    key: "gain",
    width: 120,
    customRender: ({ record }) => formatMetricValue(record.shadowAvgScore - record.mainAvgScore)
  },
  { title: t("aiEvaluation.updatedAt"), dataIndex: "createdAt", key: "createdAt", width: 220 }
]);

const pagination = computed(() => ({
  current: taskRequest.value.pageIndex,
  pageSize: taskRequest.value.pageSize,
  total: taskTotal.value,
  showSizeChanger: true
}));

const completionRateText = computed(() => {
  if (!selectedTask.value || selectedTask.value.totalCases <= 0) {
    return "0%";
  }

  return `${Math.round((selectedTask.value.completedCases / selectedTask.value.totalCases) * 100)}%`;
});

const shadowAverageOverlap = computed(() => {
  if (shadowComparisons.value.length === 0) {
    return "0.00";
  }

  const sum = shadowComparisons.value.reduce((acc, item) => acc + Number(item.overlapScore || 0), 0);
  return (sum / shadowComparisons.value.length).toFixed(2);
});

const shadowAverageGain = computed(() => {
  if (shadowComparisons.value.length === 0) {
    return "0.00";
  }

  const sum = shadowComparisons.value.reduce(
    (acc, item) => acc + (Number(item.shadowAvgScore || 0) - Number(item.mainAvgScore || 0)),
    0
  );
  return (sum / shadowComparisons.value.length).toFixed(2);
});

function buildTaskRowProps(record: EvaluationTaskItem) {
  return {
    onClick: () => {
      void selectTask(record.id);
    }
  };
}

function getAggregateMetric(task: EvaluationTaskItem | null, key: string): number {
  if (!task) {
    return 0;
  }

  return Number(task.aggregateMetrics?.[key] ?? 0);
}

function formatMetricValue(value: number | undefined): string {
  if (typeof value !== "number" || Number.isNaN(value)) {
    return "0.00";
  }

  return value.toFixed(4);
}

function buildRadarData(task: EvaluationTaskItem | null): number[] {
  return [
    getAggregateMetric(task, "faithfulness"),
    getAggregateMetric(task, "contextPrecision"),
    getAggregateMetric(task, "contextRecall"),
    getAggregateMetric(task, "answerRelevance"),
    getAggregateMetric(task, "citationAccuracy"),
    getAggregateMetric(task, "hallucinationRate")
  ];
}

function renderRadarChart() {
  if (!radarRef.value) {
    return;
  }

  if (!radarChart) {
    radarChart = echarts.init(radarRef.value);
  }

  radarChart.setOption({
    tooltip: { trigger: "item" },
    radar: {
      radius: "68%",
      splitNumber: 5,
      indicator: [
        { name: "Faithfulness", max: 1 },
        { name: "Context Precision", max: 1 },
        { name: "Context Recall", max: 1 },
        { name: "Answer Relevance", max: 1 },
        { name: "Citation Accuracy", max: 1 },
        { name: "Hallucination", max: 1 }
      ]
    },
    series: [
      {
        type: "radar",
        data: [
          {
            value: buildRadarData(selectedTask.value),
            name: t("aiEvaluation.aggregateRadar")
          }
        ],
        areaStyle: { opacity: 0.2 }
      }
    ]
  });
}

function handleResize() {
  radarChart?.resize();
}

async function loadTasks() {
  taskLoading.value = true;
  try {
    const data = await getEvaluationTasks(taskRequest.value);
    taskRows.value = data.items;
    taskTotal.value = data.total;

    if (!selectedTaskId.value && data.items.length > 0) {
      await selectTask(data.items[0].id);
    }
  } catch (error) {
    message.error((error as Error).message || t("aiEvaluation.loadTaskFailed"));
  } finally {
    taskLoading.value = false;
  }
}

async function selectTask(taskId: string) {
  selectedTaskId.value = taskId;
  resultLoading.value = true;
  try {
    const [task, results] = await Promise.all([
      getEvaluationTask(taskId),
      getEvaluationTaskResults(taskId)
    ]);
    selectedTask.value = task;
    resultRows.value = results;
    await nextTick();
    renderRadarChart();
  } catch (error) {
    message.error((error as Error).message || t("aiEvaluation.loadTaskFailed"));
  } finally {
    resultLoading.value = false;
  }
}

async function loadShadowComparisons() {
  shadowLoading.value = true;
  try {
    shadowComparisons.value = await getRagShadowComparisons(30);
  } catch (error) {
    message.error((error as Error).message || t("aiEvaluation.loadShadowFailed"));
  } finally {
    shadowLoading.value = false;
  }
}

function onTaskTableChange(page: { current?: number; pageSize?: number }) {
  taskRequest.value = {
    ...taskRequest.value,
    pageIndex: page.current ?? 1,
    pageSize: page.pageSize ?? 10
  };
  void loadTasks();
}

onMounted(() => {
  void loadTasks();
  void loadShadowComparisons();
  window.addEventListener("resize", handleResize);
});

onUnmounted(() => {
  window.removeEventListener("resize", handleResize);
  radarChart?.dispose();
  radarChart = null;
});

watch(
  () => selectedTask.value?.aggregateMetrics,
  () => {
    renderRadarChart();
  }
);
</script>

<style scoped>
.evaluation-page {
  display: grid;
  gap: 12px;
}

.evaluation-page__stats {
  margin-bottom: 12px;
}

.evaluation-page__radar {
  height: 320px;
  width: 100%;
  margin-bottom: 12px;
}
</style>
