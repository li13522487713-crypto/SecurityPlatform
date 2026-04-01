<template>
  <a-card title="Agent 团队调试" :bordered="false">
    <a-space direction="vertical" style="width: 100%">
      <a-textarea v-model:value="inputPayloadJson" :rows="6" />
      <a-space>
        <a-switch v-model:checked="fullChain" />
        <span>全链路调试</span>
        <a-button type="primary" :loading="running" @click="runDebug">开始调试</a-button>
      </a-space>
      <a-alert v-if="resultMessage" type="info" :message="resultMessage" show-icon />
      <a-card size="small" title="调试输出">
        <pre>{{ outputJson }}</pre>
      </a-card>
      <a-table :data-source="nodeRuns" :columns="columns" row-key="id" size="small" />
    </a-space>
  </a-card>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import { message } from "ant-design-vue";
import { useRoute } from "vue-router";
import { debugAgentTeam, type NodeRunItem } from "@/services/api-agent-team";

const route = useRoute();
const teamId = computed(() => Number(route.params.id || 0));
const running = ref(false);
const fullChain = ref(true);
const inputPayloadJson = ref("{\n  \"task\": \"debug-agent-team\"\n}");
const resultMessage = ref("");
const outputJson = ref("");
const nodeRuns = ref<NodeRunItem[]>([]);

const columns = [
  { title: "节点运行ID", dataIndex: "id", key: "id", width: 120 },
  { title: "节点ID", dataIndex: "nodeId", key: "nodeId", width: 100 },
  { title: "状态", dataIndex: "state", key: "state", width: 140 },
  { title: "重试次数", dataIndex: "retryCount", key: "retryCount", width: 100 },
  { title: "错误", dataIndex: "errorMessage", key: "errorMessage" }
];

async function runDebug() {
  if (!teamId.value) return;
  running.value = true;
  try {
    const result = await debugAgentTeam(teamId.value, {
      inputPayloadJson: inputPayloadJson.value,
      fullChain: fullChain.value
    });
    resultMessage.value = result.message;
    outputJson.value = result.outputJson;
    nodeRuns.value = result.nodeRuns;
    message.success("调试完成");
  } catch (err) {
    message.error((err as Error).message || "调试失败");
  } finally {
    running.value = false;
  }
}
</script>
