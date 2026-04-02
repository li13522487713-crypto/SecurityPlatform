<template>
  <div class="execution-timeline-page">
    <a-page-header :title="t('logicFlow.executionTimelinePage.title')" @back="$router.back()">
      <template #sub-title>
        <a-typography-text code>{{ executionId }}</a-typography-text>
      </template>
      <template #extra>
        <router-link :to="detailLink">
          <a-button type="link">{{ t("logicFlow.executionDetailPage.backToDetail") }}</a-button>
        </router-link>
      </template>
    </a-page-header>

    <div class="gantt-wrap">
      <div v-for="lane in lanes" :key="lane.id" class="gantt-lane">
        <div class="lane-label">{{ lane.label }}</div>
        <div class="lane-track">
          <button
            v-for="bar in lane.bars"
            :key="bar.id"
            type="button"
            class="gantt-bar"
            :class="bar.status"
            :style="{ left: `${bar.leftPct}%`, width: `${bar.widthPct}%` }"
            @click="selectBar(bar)"
          >
            <span class="bar-text">{{ bar.label }}</span>
          </button>
        </div>
      </div>
    </div>

    <a-drawer v-model:open="drawerOpen" width="400" :title="t('logicFlow.executionTimelinePage.nodeDetail')" placement="right">
      <a-descriptions v-if="selectedBar" bordered size="small" :column="1">
        <a-descriptions-item :label="t('logicFlow.executionTimelinePage.barLabel')">{{ selectedBar.label }}</a-descriptions-item>
        <a-descriptions-item :label="t('logicFlow.duration')">{{ selectedBar.durationMs }} ms</a-descriptions-item>
        <a-descriptions-item :label="t('logicFlow.execStatus')">{{ selectedBar.status }}</a-descriptions-item>
      </a-descriptions>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import { useI18n } from "vue-i18n";
import { useRoute } from "vue-router";

interface GanttBar {
  id: string;
  label: string;
  leftPct: number;
  widthPct: number;
  durationMs: number;
  status: "completed" | "running" | "failed" | "pending";
}

interface GanttLane {
  id: string;
  label: string;
  bars: GanttBar[];
}

const { t } = useI18n();
const route = useRoute();

const executionId = computed(() => String(route.params.id ?? ""));
const appId = computed(() => String(route.params.appId ?? ""));

const detailLink = computed(() => ({
  name: "app-logic-flow-execution-detail",
  params: { appId: appId.value, id: executionId.value }
}));

const lanes = ref<GanttLane[]>([
  {
    id: "lane-a",
    label: "Parallel A",
    bars: [
      { id: "b1", label: "Fetch", leftPct: 0, widthPct: 25, durationMs: 80, status: "completed" },
      { id: "b2", label: "Transform", leftPct: 28, widthPct: 35, durationMs: 120, status: "running" }
    ]
  },
  {
    id: "lane-b",
    label: "Parallel B",
    bars: [{ id: "b3", label: "Notify", leftPct: 10, widthPct: 20, durationMs: 60, status: "pending" }]
  }
]);

const drawerOpen = ref(false);
const selectedBar = ref<GanttBar | null>(null);

function selectBar(bar: GanttBar): void {
  selectedBar.value = bar;
  drawerOpen.value = true;
}
</script>

<style scoped>
.execution-timeline-page {
  padding-bottom: 24px;
}

.gantt-wrap {
  margin: 0 24px 24px;
  padding: 16px;
  background: #fff;
  border-radius: 4px;
  border: 1px solid #f0f0f0;
}

.gantt-lane {
  display: flex;
  align-items: stretch;
  margin-bottom: 16px;
}

.gantt-lane:last-child {
  margin-bottom: 0;
}

.lane-label {
  width: 120px;
  flex-shrink: 0;
  padding: 8px 12px 8px 0;
  font-size: 13px;
  color: rgba(0, 0, 0, 0.65);
}

.lane-track {
  position: relative;
  flex: 1;
  min-height: 40px;
  background: repeating-linear-gradient(
    90deg,
    #fafafa,
    #fafafa 49px,
    #f0f0f0 49px,
    #f0f0f0 50px
  );
  border-radius: 4px;
}

.gantt-bar {
  position: absolute;
  top: 6px;
  height: 28px;
  margin: 0;
  padding: 0 8px;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  text-align: left;
  overflow: hidden;
  white-space: nowrap;
  text-overflow: ellipsis;
  color: #fff;
  font-size: 12px;
}

.gantt-bar.completed {
  background: #52c41a;
}

.gantt-bar.running {
  background: #1677ff;
}

.gantt-bar.failed {
  background: #ff4d4f;
}

.gantt-bar.pending {
  background: #8c8c8c;
}

.bar-text {
  pointer-events: none;
}
</style>
