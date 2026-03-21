<template>
  <div class="workflow-editor">
    <NodePalette :node-types="nodeTypes" @add-node="addNode" />

    <div class="canvas-area">
      <div class="editor-toolbar">
        <a-space>
          <a-button @click="goBack">返回列表</a-button>
          <a-button type="primary" :loading="saving" @click="saveWorkflow">保存</a-button>
          <a-button :loading="validating" @click="validateWorkflow">校验</a-button>
          <a-button @click="openVersionHistory">版本历史</a-button>
        </a-space>
      </div>

      <VueFlow
        v-model:nodes="nodes"
        v-model:edges="edges"
        :fit-view-on-init="true"
        class="workflow-canvas"
        @connect="onConnect"
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

    <!-- 版本历史抽屉 -->
    <a-drawer
      v-model:open="versionDrawerOpen"
      title="版本历史"
      width="640"
      :destroy-on-close="false"
    >
      <a-spin :spinning="versionLoading">
        <a-empty v-if="versionList.length === 0 && !versionLoading" description="暂无发布版本记录" />
        <a-list
          v-else
          :data-source="versionList"
          size="small"
          :item-layout="'vertical'"
        >
          <template #renderItem="{ item }">
            <a-list-item :key="item.version">
              <a-list-item-meta>
                <template #title>
                  <a-space>
                    <a-tag color="blue">v{{ item.version }}</a-tag>
                    <span>{{ item.workflowName }}</span>
                    <a-tag v-if="item.changeLog" color="default">{{ item.changeLog }}</a-tag>
                  </a-space>
                </template>
                <template #description>
                  发布时间：{{ formatDate(item.publishedAt) }}
                </template>
              </a-list-item-meta>
              <template #actions>
                <a-button
                  v-if="diffBaseVersion !== null && diffBaseVersion !== item.version"
                  size="small"
                  @click="loadDiff(diffBaseVersion!, item.version)"
                >
                  与 v{{ diffBaseVersion }} 对比
                </a-button>
                <a-button
                  size="small"
                  :type="diffBaseVersion === item.version ? 'primary' : 'default'"
                  @click="setDiffBase(item.version)"
                >
                  {{ diffBaseVersion === item.version ? '已选为对比基准' : '选为对比基准' }}
                </a-button>
                <a-popconfirm
                  :title="`确认回滚到 v${item.version}？此操作将创建新版本 v${currentPublishVersion + 1}。`"
                  ok-text="确认回滚"
                  cancel-text="取消"
                  @confirm="doRollback(item.version)"
                >
                  <a-button size="small" danger :loading="rollingBack">回滚</a-button>
                </a-popconfirm>
              </template>
            </a-list-item>
          </template>
        </a-list>
      </a-spin>

      <!-- Diff 结果面板 -->
      <template v-if="diffResult">
        <a-divider>版本差异：v{{ diffResult.fromVersion }} → v{{ diffResult.toVersion }}</a-divider>
        <a-row :gutter="[12, 12]">
          <a-col :span="8">
            <a-statistic title="新增节点" :value="diffResult.addedNodeIds.length" />
            <a-tag v-for="nid in diffResult.addedNodeIds" :key="nid" color="success">{{ nid }}</a-tag>
          </a-col>
          <a-col :span="8">
            <a-statistic title="移除节点" :value="diffResult.removedNodeIds.length" />
            <a-tag v-for="nid in diffResult.removedNodeIds" :key="nid" color="error">{{ nid }}</a-tag>
          </a-col>
          <a-col :span="8">
            <a-statistic title="变更节点" :value="diffResult.modifiedNodeIds.length" />
            <a-tag v-for="nid in diffResult.modifiedNodeIds" :key="nid" color="warning">{{ nid }}</a-tag>
          </a-col>
        </a-row>
        <a-row :gutter="[12, 12]" style="margin-top:12px">
          <a-col :span="12">
            <a-statistic title="新增连线" :value="diffResult.addedEdges" />
          </a-col>
          <a-col :span="12">
            <a-statistic title="移除连线" :value="diffResult.removedEdges" />
          </a-col>
        </a-row>
      </template>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";
import { message } from "ant-design-vue";
import { Controls } from "@vue-flow/controls";
import { MiniMap } from "@vue-flow/minimap";
import { VueFlow, addEdge, type Node, type Edge, type NodeMouseEvent, type Connection } from "@vue-flow/core";
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
  getAiWorkflowVersions,
  getAiWorkflowVersionDiff,
  rollbackAiWorkflow,
  runAiWorkflow,
  saveAiWorkflow,
  validateAiWorkflow,
  type AiWorkflowNodeTypeDto,
  type AiWorkflowVersionItem,
  type AiWorkflowVersionDiff
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
const currentPublishVersion = ref(0);
let progressPollTimer: number | null = null;

// 版本历史抽屉
const versionDrawerOpen = ref(false);
const versionLoading = ref(false);
const versionList = ref<AiWorkflowVersionItem[]>([]);
const diffBaseVersion = ref<number | null>(null);
const diffResult = ref<AiWorkflowVersionDiff | null>(null);
const rollingBack = ref(false);

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
    currentPublishVersion.value = detail.publishVersion;
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

function onConnect(connection: Connection) {
  edges.value = addEdge(connection, edges.value) as Edge[];
}

async function saveWorkflow(options?: { silent?: boolean }) {
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
    if (!options?.silent) {
      message.success("保存成功");
    }
  } catch (err: unknown) {
    message.error((err as Error).message || "保存失败");
    throw err;
  } finally {
    saving.value = false;
  }
}

async function validateWorkflow() {
  validating.value = true;
  try {
    await saveWorkflow({ silent: true });
    const result = await validateAiWorkflow(workflowId);
    if (result.isValid) {
      message.success("校验通过");
      return;
    }

    message.error(`校验失败: ${result.errors.join("；")}`);
  } catch (err: unknown) {
    message.error((err as Error).message || "校验失败");
  } finally {
    validating.value = false;
  }
}

async function runWorkflow(inputs: Record<string, unknown>) {
  running.value = true;
  try {
    await saveWorkflow({ silent: true });
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

// ── 版本历史相关 ──

function formatDate(value: string) {
  return new Date(value).toLocaleString();
}

async function openVersionHistory() {
  versionDrawerOpen.value = true;
  diffResult.value = null;
  diffBaseVersion.value = null;
  versionLoading.value = true;
  try {
    versionList.value = await getAiWorkflowVersions(workflowId);
  } catch (err: unknown) {
    message.error((err as Error).message || "加载版本历史失败");
  } finally {
    versionLoading.value = false;
  }
}

function setDiffBase(version: number) {
  if (diffBaseVersion.value === version) {
    diffBaseVersion.value = null;
    diffResult.value = null;
  } else {
    diffBaseVersion.value = version;
    diffResult.value = null;
  }
}

async function loadDiff(fromVer: number, toVer: number) {
  try {
    diffResult.value = await getAiWorkflowVersionDiff(workflowId, fromVer, toVer);
  } catch (err: unknown) {
    message.error((err as Error).message || "加载版本差异失败");
  }
}

async function doRollback(targetVersion: number) {
  rollingBack.value = true;
  try {
    const result = await rollbackAiWorkflow(workflowId, targetVersion);
    message.success(`已回滚到 v${targetVersion}，新版本为 v${result.newVersion}`);
    currentPublishVersion.value = result.newVersion;
    versionDrawerOpen.value = false;
    await loadWorkflow();
  } catch (err: unknown) {
    message.error((err as Error).message || "版本回滚失败");
  } finally {
    rollingBack.value = false;
  }
}

watch(executionId, (id) => {
  if (progressPollTimer) {
    window.clearInterval(progressPollTimer);
    progressPollTimer = null;
  }

  if (!id) return;
  progressPollTimer = window.setInterval(async () => {
    try {
      const progress = await getAiWorkflowExecutionProgress(id);
      executionStatus.value = progress.status;
      if (["Complete", "Terminated"].includes(progress.status)) {
        if (progressPollTimer) {
          window.clearInterval(progressPollTimer);
          progressPollTimer = null;
        }
      }
    } catch {
      if (progressPollTimer) {
        window.clearInterval(progressPollTimer);
        progressPollTimer = null;
      }
    }
  }, 2000);
});

onMounted(async () => {
  await Promise.all([loadNodeTypes(), loadWorkflow()]);
});

onBeforeUnmount(() => {
  if (progressPollTimer) {
    window.clearInterval(progressPollTimer);
    progressPollTimer = null;
  }
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
