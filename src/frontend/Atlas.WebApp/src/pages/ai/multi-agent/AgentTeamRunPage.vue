<template>
  <a-card title="Agent 团队运行页" :bordered="false">
    <a-space direction="vertical" style="width: 100%">
      <a-input v-model:value="teamVersionId" addon-before="TeamVersionId" />
      <a-textarea v-model:value="inputPayloadJson" :rows="6" />
      <a-space>
        <a-button type="primary" :loading="running" @click="startRun">运行</a-button>
        <a-button :disabled="!runId" @click="refreshRun">刷新</a-button>
      </a-space>
      <a-descriptions bordered :column="3" size="small" v-if="runDetail">
        <a-descriptions-item label="RunId">{{ runDetail.id }}</a-descriptions-item>
        <a-descriptions-item label="状态">{{ runDetail.currentState }}</a-descriptions-item>
        <a-descriptions-item label="开始时间">{{ runDetail.startedAt }}</a-descriptions-item>
      </a-descriptions>
      <a-table :columns="columns" :data-source="nodes" row-key="id" size="small">
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'action'">
            <a-space>
              <a-button size="small" @click="intervene(record.id, 'confirm')">确认</a-button>
              <a-button size="small" @click="intervene(record.id, 'retry')">重试</a-button>
              <a-button size="small" @click="intervene(record.id, 'skip')">跳过</a-button>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-space>
  </a-card>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import { message } from "ant-design-vue";
import { useRoute } from "vue-router";
import {
  createAgentTeamRun,
  getAgentTeamRun,
  getAgentTeamRunNodes,
  interveneRunNode,
  type AgentTeamRunDetail,
  type NodeRunItem
} from "@/services/api-agent-team";

const route = useRoute();
const teamId = computed(() => Number(route.params.id || 0));
const running = ref(false);
const runId = ref<number>(0);
const teamVersionId = ref("1");
const inputPayloadJson = ref("{\n  \"task\": \"正式运行\"\n}");
const runDetail = ref<AgentTeamRunDetail>();
const nodes = ref<NodeRunItem[]>([]);

const columns = [
  { title: "NodeRunId", dataIndex: "id", key: "id", width: 120 },
  { title: "NodeId", dataIndex: "nodeId", key: "nodeId", width: 100 },
  { title: "状态", dataIndex: "state", key: "state", width: 140 },
  { title: "重试", dataIndex: "retryCount", key: "retryCount", width: 80 },
  { title: "错误", dataIndex: "errorMessage", key: "errorMessage" },
  { title: "操作", key: "action", width: 220 }
];

async function startRun() {
  if (!teamId.value) return;
  running.value = true;
  try {
    const id = await createAgentTeamRun({
      teamId: teamId.value,
      teamVersionId: Number(teamVersionId.value),
      triggerType: "Manual",
      inputPayloadJson: inputPayloadJson.value
    });
    runId.value = id;
    await refreshRun();
    message.success("运行已启动");
  } catch (err) {
    message.error((err as Error).message || "运行失败");
  } finally {
    running.value = false;
  }
}

async function refreshRun() {
  if (!runId.value) return;
  runDetail.value = await getAgentTeamRun(runId.value);
  nodes.value = await getAgentTeamRunNodes(runId.value);
}

async function intervene(nodeRunId: number, action: "confirm" | "retry" | "skip") {
  if (!runId.value) return;
  try {
    await interveneRunNode(runId.value, nodeRunId, {
      action,
      payloadJson: "{\"operator\":\"user\"}"
    });
    await refreshRun();
    message.success("介入成功");
  } catch (err) {
    message.error((err as Error).message || "介入失败");
  }
}

watchRunRoute();
function watchRunRoute() {
  const routeRunId = Number(route.query.runId || 0);
  if (routeRunId > 0) {
    runId.value = routeRunId;
    void refreshRun();
  }
}

</script>
