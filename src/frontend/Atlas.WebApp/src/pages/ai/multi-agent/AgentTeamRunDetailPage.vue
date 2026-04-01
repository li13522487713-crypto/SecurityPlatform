<template>
  <a-card title="执行详情" :bordered="false">
    <a-space direction="vertical" style="width: 100%">
      <a-descriptions bordered size="small" :column="2" v-if="run">
        <a-descriptions-item label="RunId">{{ run.id }}</a-descriptions-item>
        <a-descriptions-item label="状态">{{ run.currentState }}</a-descriptions-item>
        <a-descriptions-item label="TeamId">{{ run.teamId }}</a-descriptions-item>
        <a-descriptions-item label="TeamVersionId">{{ run.teamVersionId }}</a-descriptions-item>
        <a-descriptions-item label="开始时间">{{ run.startedAt }}</a-descriptions-item>
        <a-descriptions-item label="结束时间">{{ run.endedAt || "-" }}</a-descriptions-item>
      </a-descriptions>
      <a-card size="small" title="结果">
        <pre>{{ run?.outputResultJson }}</pre>
      </a-card>
      <a-table :columns="columns" :data-source="nodes" row-key="id" />
    </a-space>
  </a-card>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useRoute } from "vue-router";
import { message } from "ant-design-vue";
import { getAgentTeamRun, getAgentTeamRunNodes, type AgentTeamRunDetail, type NodeRunItem } from "@/services/api-agent-team";

const route = useRoute();
const runId = computed(() => Number(route.query.runId || 0));
const run = ref<AgentTeamRunDetail>();
const nodes = ref<NodeRunItem[]>([]);

const columns = [
  { title: "NodeRunId", dataIndex: "id", key: "id", width: 120 },
  { title: "NodeId", dataIndex: "nodeId", key: "nodeId", width: 120 },
  { title: "状态", dataIndex: "state", key: "state", width: 140 },
  { title: "错误", dataIndex: "errorMessage", key: "errorMessage" },
  { title: "开始", dataIndex: "startedAt", key: "startedAt", width: 180 },
  { title: "结束", dataIndex: "endedAt", key: "endedAt", width: 180 }
];

onMounted(async () => {
  if (!runId.value) {
    message.warning("缺少 runId");
    return;
  }

  try {
    run.value = await getAgentTeamRun(runId.value);
    nodes.value = await getAgentTeamRunNodes(runId.value);
  } catch (err) {
    message.error((err as Error).message || "加载执行详情失败");
  }
});
</script>
