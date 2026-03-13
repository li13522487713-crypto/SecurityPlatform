<template>
  <div class="workflow-editor">
    <NodePalette :node-types="nodeTypes" @add-node="addNode" />

    <div class="canvas-area">
      <div class="editor-toolbar">
        <a-space>
          <a-button @click="goBack">返回列表</a-button>
          <a-button type="primary" :loading="saving" @click="saveWorkflow">保存</a-button>
          <a-button :loading="validating" @click="validateWorkflow">校验</a-button>
        </a-space>
      </div>

      <VueFlow
        v-model:nodes="nodes"
        v-model:edges="edges"
        :fit-view-on-init="true"
        class="workflow-canvas"
        @node-click="onNodeClick"
      >
        <template #node-default="nodeProps">
          <AiNode :data="{ label: nodeProps.data?.label as string, type: nodeProps.type as string }" />
        </template>
        <MiniMap />
        <Controls />
      </VueFlow>

      <RunPanel
        :running="running"
        :execution-id="executionId || undefined"
        :status="executionStatus || undefined"
        @run="runWorkflow"
        @cancel="cancelExecution"
      />
    </div>

    <div class="config-panel">
      <h4>节点配置</h4>
      <template v-if="selectedNode">
        <p class="node-name">{{ selectedNode.data?.label || selectedNode.id }}</p>
        <LlmNodeConfig
          v-if="selectedNode.type === 'llm'"
          v-model="selectedNodeConfig"
        />
        <HttpNodeConfig
          v-else
          v-model="selectedNodeConfig"
        />
      </template>
      <a-empty v-else description="请选择一个节点" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { message } from "ant-design-vue";
import { Controls } from "@vue-flow/controls";
import { MiniMap } from "@vue-flow/minimap";
import { VueFlow, type Node, type Edge, type NodeMouseEvent } from "@vue-flow/core";
import NodePalette from "@/components/ai/workflow/NodePalette.vue";
import AiNode from "@/components/ai/workflow/AiNode.vue";
import LlmNodeConfig from "@/components/ai/workflow/LlmNodeConfig.vue";
import HttpNodeConfig from "@/components/ai/workflow/HttpNodeConfig.vue";
import RunPanel from "@/components/ai/workflow/RunPanel.vue";
import {
  cancelAiWorkflowExecution,
  getAiWorkflowById,
  getAiWorkflowExecutionProgress,
  getAiWorkflowNodeTypes,
  runAiWorkflow,
  saveAiWorkflow,
  type AiWorkflowNodeTypeDto
} from "@/services/api-ai-workflow";

import "@vue-flow/core/dist/style.css";
import "@vue-flow/core/dist/theme-default.css";
import "@vue-flow/minimap/dist/style.css";
import "@vue-flow/controls/dist/style.css";

const route = useRoute();
const router = useRouter();
const workflowId = Number(route.params["id"]);

const nodeTypes = ref<AiWorkflowNodeTypeDto[]>([]);
const nodes = ref<Node[]>([]);
const edges = ref<Edge[]>([]);
const selectedNodeId = ref<string | null>(null);
const saving = ref(false);
const validating = ref(false);
const running = ref(false);
const executionId = ref<string | null>(null);
const executionStatus = ref<string | null>(null);

const selectedNode = computed(() => nodes.value.find((x) => x.id === selectedNodeId.value) || null);

const selectedNodeConfig = computed<Record<string, unknown>>({
  get() {
    return (selectedNode.value?.data?.config as Record<string, unknown>) || {};
  },
  set(value) {
    if (!selectedNode.value) return;
    selectedNode.value.data = {
      ...(selectedNode.value.data || {}),
      config: value
    };
  }
});

function goBack() {
  void router.push("/ai/workflows");
}

async function loadNodeTypes() {
  try {
    nodeTypes.value = await getAiWorkflowNodeTypes();
  } catch (err: unknown) {
    message.error((err as Error).message || "加载节点类型失败");
  }
}

async function loadWorkflow() {
  try {
    const detail = await getAiWorkflowById(workflowId);
    if (detail.canvasJson) {
      const parsed = JSON.parse(detail.canvasJson) as { nodes?: Node[]; edges?: Edge[] };
      nodes.value = parsed.nodes || [];
      edges.value = parsed.edges || [];
    }
  } catch (err: unknown) {
    message.error((err as Error).message || "加载工作流失败");
  }
}

function addNode(type: AiWorkflowNodeTypeDto) {
  const id = `${type.key}_${Date.now()}`;
  nodes.value.push({
    id,
    type: type.key === "llm" ? "llm" : "default",
    position: { x: 200 + Math.random() * 200, y: 120 + Math.random() * 200 },
    data: { label: type.name, type: type.key, config: {} }
  });
}

function onNodeClick(evt: NodeMouseEvent) {
  selectedNodeId.value = evt.node.id;
}

async function saveWorkflow() {
  saving.value = true;
  try {
    const canvasJson = JSON.stringify({
      nodes: nodes.value,
      edges: edges.value
    });
    await saveAiWorkflow(workflowId, {
      canvasJson,
      definitionJson: "{}"
    });
    message.success("保存成功");
  } catch (err: unknown) {
    message.error((err as Error).message || "保存失败");
  } finally {
    saving.value = false;
  }
}

async function validateWorkflow() {
  validating.value = true;
  try {
    await saveWorkflow();
    message.success("已提交校验，可在后端接口查看结果");
  } finally {
    validating.value = false;
  }
}

async function runWorkflow(inputs: Record<string, unknown>) {
  running.value = true;
  try {
    await saveWorkflow();
    const result = await runAiWorkflow(workflowId, inputs);
    executionId.value = result.executionId;
    message.success("已启动执行");
  } catch (err: unknown) {
    message.error((err as Error).message || "执行失败");
  } finally {
    running.value = false;
  }
}

async function cancelExecution(id: string) {
  try {
    await cancelAiWorkflowExecution(id);
    message.success("已取消执行");
    executionStatus.value = "Terminated";
  } catch (err: unknown) {
    message.error((err as Error).message || "取消失败");
  }
}

watch(executionId, (id) => {
  if (!id) return;
  const timer = window.setInterval(async () => {
    try {
      const progress = await getAiWorkflowExecutionProgress(id);
      executionStatus.value = progress.status;
      if (["Complete", "Terminated"].includes(progress.status)) {
        window.clearInterval(timer);
      }
    } catch {
      window.clearInterval(timer);
    }
  }, 2000);
});

onMounted(async () => {
  await Promise.all([loadNodeTypes(), loadWorkflow()]);
});
</script>

<style scoped>
.workflow-editor {
  height: calc(100vh - 120px);
  display: flex;
  background: #fff;
  border-radius: 8px;
  overflow: hidden;
}

.canvas-area {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.editor-toolbar {
  padding: 10px 12px;
  border-bottom: 1px solid #f0f0f0;
}

.workflow-canvas {
  flex: 1;
  min-height: 420px;
}

.config-panel {
  width: 300px;
  border-left: 1px solid #f0f0f0;
  padding: 12px;
}

.node-name {
  margin-bottom: 8px;
  font-weight: 600;
}
</style>
