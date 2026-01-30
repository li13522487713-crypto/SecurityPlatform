<template>
  <a-card title="运行态中心（骨架）" class="page-card" :loading="loading">
    <template #extra>
      <a-space>
        <a-button @click="loadData">刷新</a-button>
      </a-space>
    </template>

    <a-table
      :data-source="instances"
      :columns="columns"
      :pagination="false"
      row-key="id"
      size="middle"
    />
  </a-card>
</template>

<script setup lang="ts">
import { onMounted, ref } from "vue";
import { getVisualizationInstances } from "@/services/api";
import type { VisualizationInstanceSummary } from "@/types/api";
import { message } from "ant-design-vue";

const instances = ref<VisualizationInstanceSummary[]>([]);
const loading = ref(false);

const columns = [
  { title: "实例ID", dataIndex: "id", key: "id" },
  { title: "流程", dataIndex: "flowName", key: "flowName" },
  { title: "状态", dataIndex: "status", key: "status" },
  { title: "当前节点", dataIndex: "currentNode", key: "currentNode" },
  { title: "启动时间", dataIndex: "startedAt", key: "startedAt" },
  { title: "耗时(分钟)", dataIndex: "durationMinutes", key: "durationMinutes" }
];

const loadData = async () => {
  try {
    loading.value = true;
    const result = await getVisualizationInstances({ pageIndex: 1, pageSize: 10 });
    instances.value = result.items;
  } catch (err) {
    message.error((err as Error).message);
  } finally {
    loading.value = false;
  }
};

onMounted(loadData);
</script>
