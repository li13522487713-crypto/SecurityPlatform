<template>
  <div class="wf-editor-page">
    <div class="wf-toolbar">
      <a-input v-model:value="workflowName" style="width: 280px" />
      <a-space>
        <a-button @click="appendNode('Llm')">+ LLM</a-button>
        <a-button @click="appendNode('HttpRequester')">+ HTTP</a-button>
        <a-button @click="saveDraft">保存草稿</a-button>
        <a-button type="primary" @click="publishWorkflow">发布</a-button>
      </a-space>
    </div>
    <a-textarea v-model:value="canvasJsonText" :rows="24" class="wf-canvas" />
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useRoute } from "vue-router";
import { message } from "ant-design-vue";
import { workflowV2Api } from "@/services/api-workflow-v2";
type WorkflowCanvas = {
  nodes: Array<{
    key: string;
    type: string;
    title: string;
    layout: { x: number; y: number; width: number; height: number };
    configs: Record<string, unknown>;
    inputMappings: Record<string, string>;
  }>;
  connections: Array<{
    fromNode: string;
    fromPort: string;
    toNode: string;
    toPort: string;
    condition: string | null;
  }>;
};

const route = useRoute();
const workflowId = computed(() => String(route.params.id ?? ""));
const workflowName = ref("工作流");
const canvasJsonText = ref<string>(JSON.stringify({ nodes: [], connections: [] }, null, 2));

async function load() {
  const res = await workflowV2Api.getDetail(workflowId.value);
  if (res.success && res.data) {
    workflowName.value = res.data.name;
    if (res.data.canvasJson) {
      canvasJsonText.value = JSON.stringify(JSON.parse(res.data.canvasJson) as WorkflowCanvas, null, 2);
    }
  }
}

function appendNode(type: string) {
  let parsed: WorkflowCanvas;
  try {
    parsed = JSON.parse(canvasJsonText.value) as WorkflowCanvas;
  } catch {
    parsed = { nodes: [], connections: [] };
  }
  parsed.nodes.push({
    key: `${type.toLowerCase()}_${Date.now()}`,
    type,
    title: type,
    layout: { x: 180, y: 180, width: 160, height: 60 },
    configs: {},
    inputMappings: {}
  });
  canvasJsonText.value = JSON.stringify(parsed, null, 2);
}

async function saveDraft() {
  const canvasJson = canvasJsonText.value;
  const res = await workflowV2Api.saveDraft(workflowId.value, { canvasJson });
  if (res.success) {
    message.success("草稿已保存");
  }
}

async function publishWorkflow() {
  await saveDraft();
  const res = await workflowV2Api.publish(workflowId.value, { changeLog: "app-web 发布" });
  if (res.success) {
    message.success("发布成功");
  }
}

onMounted(() => {
  void load();
});
</script>

<style scoped>
.wf-editor-page {
  height: 100%;
  display: grid;
  grid-template-rows: auto 1fr;
}

.wf-toolbar {
  padding: 12px;
  display: flex;
  justify-content: space-between;
  gap: 12px;
}

.wf-canvas {
  border: 1px solid #e5e7eb;
}
</style>
