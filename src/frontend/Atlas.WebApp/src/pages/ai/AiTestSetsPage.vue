<template>
  <a-space direction="vertical" style="width: 100%" :size="16">
    <a-card title="AI 测试集" :bordered="false">
      <a-table :columns="columns" :data-source="testSets" row-key="id" :pagination="false">
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'status'">
            <a-tag :color="record.passRate >= 90 ? 'green' : 'orange'">{{ record.passRate }}%</a-tag>
          </template>
          <template v-if="column.key === 'action'">
            <a-button type="link" @click="openTrace(record.id)">查看 Trace</a-button>
          </template>
        </template>
      </a-table>
    </a-card>

    <TraceViewer :traces="selectedTraces" />
    <PreviewPanel title="预览结果摘要" :content="previewText" />
  </a-space>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import TraceViewer, { type TraceItem } from "@/components/ai/TraceViewer.vue";
import PreviewPanel from "@/components/ai/PreviewPanel.vue";

interface TestSetItem {
  id: number;
  name: string;
  scene: string;
  totalCases: number;
  passRate: number;
}

const testSets = ref<TestSetItem[]>([
  { id: 1, name: "客服问答回归集", scene: "客服 Agent", totalCases: 80, passRate: 96 },
  { id: 2, name: "RAG 检索评测集", scene: "知识库检索", totalCases: 50, passRate: 88 }
]);

const traceMap: Record<number, TraceItem[]> = {
  1: [
    { step: "输入预处理", durationMs: 12, output: "标准化用户问句", success: true },
    { step: "知识召回", durationMs: 45, output: "召回 6 条候选", success: true },
    { step: "答案生成", durationMs: 120, output: "生成最终回答", success: true }
  ],
  2: [
    { step: "向量检索", durationMs: 38, output: "召回 8 条文档", success: true },
    { step: "重排序", durationMs: 24, output: "Top3 结果重排", success: true },
    { step: "答案校验", durationMs: 15, output: "命中率偏低，待优化", success: false }
  ]
};

const selectedId = ref<number>(1);

const columns = [
  { title: "名称", dataIndex: "name", key: "name" },
  { title: "场景", dataIndex: "scene", key: "scene", width: 180 },
  { title: "用例数", dataIndex: "totalCases", key: "totalCases", width: 120 },
  { title: "通过率", key: "status", width: 120 },
  { title: "操作", key: "action", width: 120 }
];

const selectedTraces = computed(() => traceMap[selectedId.value] ?? []);

const previewText = computed(() => {
  const item = testSets.value.find((x) => x.id === selectedId.value);
  if (!item) {
    return "暂无数据";
  }

  return `测试集：${item.name}\n场景：${item.scene}\n用例数：${item.totalCases}\n通过率：${item.passRate}%`;
});

function openTrace(id: number) {
  selectedId.value = id;
}
</script>
