<template>
  <a-space direction="vertical" style="width: 100%" :size="16">
    <a-card :title="t('ai.testSets.pageTitle')" :bordered="false">
      <a-table :columns="columns" :data-source="testSets" row-key="id" :pagination="false">
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'status'">
            <a-tag :color="record.passRate >= 90 ? 'green' : 'orange'">{{ record.passRate }}%</a-tag>
          </template>
          <template v-if="column.key === 'action'">
            <a-button type="link" @click="openTrace(record.id)">{{ t("ai.testSets.viewTrace") }}</a-button>
          </template>
        </template>
      </a-table>
    </a-card>

    <TraceViewer :traces="selectedTraces" />
    <PreviewPanel :title="t('ai.testSets.previewTitle')" :content="previewText" />
  </a-space>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import { useI18n } from "vue-i18n";
import TraceViewer, { type TraceItem } from "@/components/ai/TraceViewer.vue";
import PreviewPanel from "@/components/ai/PreviewPanel.vue";

const { t } = useI18n();

interface TestSetItem {
  id: number;
  name: string;
  scene: string;
  totalCases: number;
  passRate: number;
}

const testSets = computed<TestSetItem[]>(() => [
  { id: 1, name: t("ai.testSets.demo1Name"), scene: t("ai.testSets.demo1Scene"), totalCases: 80, passRate: 96 },
  { id: 2, name: t("ai.testSets.demo2Name"), scene: t("ai.testSets.demo2Scene"), totalCases: 50, passRate: 88 }
]);

const traceMap = computed<Record<number, TraceItem[]>>(() => ({
  1: [
    { step: t("ai.testSets.t1s0"), durationMs: 12, output: t("ai.testSets.t1o0"), success: true },
    { step: t("ai.testSets.t1s1"), durationMs: 45, output: t("ai.testSets.t1o1"), success: true },
    { step: t("ai.testSets.t1s2"), durationMs: 120, output: t("ai.testSets.t1o2"), success: true }
  ],
  2: [
    { step: t("ai.testSets.t2s0"), durationMs: 38, output: t("ai.testSets.t2o0"), success: true },
    { step: t("ai.testSets.t2s1"), durationMs: 24, output: t("ai.testSets.t2o1"), success: true },
    { step: t("ai.testSets.t2s2"), durationMs: 15, output: t("ai.testSets.t2o2"), success: false }
  ]
}));

const selectedId = ref<number>(1);

const columns = computed(() => [
  { title: t("ai.promptLib.colName"), dataIndex: "name", key: "name" },
  { title: t("ai.testSets.colScene"), dataIndex: "scene", key: "scene", width: 180 },
  { title: t("ai.testSets.colCases"), dataIndex: "totalCases", key: "totalCases", width: 120 },
  { title: t("ai.testSets.colPassRate"), key: "status", width: 120 },
  { title: t("ai.colActions"), key: "action", width: 120 }
]);

const selectedTraces = computed(() => traceMap.value[selectedId.value] ?? []);

const previewText = computed(() => {
  const item = testSets.value.find((x) => x.id === selectedId.value);
  if (!item) {
    return t("ai.testSets.noData");
  }

  return t("ai.testSets.previewFmt", {
    name: item.name,
    scene: item.scene,
    cases: item.totalCases,
    rate: item.passRate
  });
});

function openTrace(id: number) {
  selectedId.value = id;
}
</script>
