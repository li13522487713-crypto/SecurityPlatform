<template>
  <a-card class="page-card">
    <template #title>
      <a-space>
        <span>工作流设计器</span>
        <a-input
          v-model:value="workflowId"
          placeholder="工作流ID（如 my-workflow）"
          style="width: 300px"
        />
      </a-space>
    </template>
    <template #extra>
      <a-space>
        <a-button @click="handleSave">保存工作流</a-button>
        <a-button type="primary" @click="handleTest">测试执行</a-button>
      </a-space>
    </template>
    <div class="designer-container">
      <div class="designer-toolbar">
        <div class="toolbar-title">步骤类型</div>
        <div class="node-types">
          <div
            v-for="stepType in stepTypes"
            :key="stepType.type"
            class="node-type-item"
            :class="{ 'node-type-disabled': stepType.supported === false }"
            :draggable="stepType.supported !== false"
            @dragstart="stepType.supported !== false && handleDragStart($event, stepType)"
          >
            <div class="node-icon" :style="{ borderColor: stepType.color, color: stepType.color }">
              {{ stepType.label }}
            </div>
            <div class="node-label">
              {{ stepType.label }}
              <a-tag v-if="stepType.supported === false" color="default" size="small">规划中</a-tag>
            </div>
          </div>
        </div>
      </div>
      <div ref="containerRef" class="designer-canvas"></div>
      <a-drawer
        v-model:open="drawerVisible"
        title="节点属性配置"
        placement="right"
        width="400"
        @close="handleDrawerClose"
      >
        <a-form :model="selectedNodeData" layout="vertical" v-if="selectedNodeData">
          <a-form-item label="节点名称">
            <a-input v-model:value="selectedNodeData.name" />
          </a-form-item>
          <a-form-item label="节点ID">
            <a-input v-model:value="selectedNodeData.id" disabled />
          </a-form-item>
          <a-form-item label="步骤类型">
            <a-input v-model:value="selectedNodeData.stepType" disabled />
          </a-form-item>
          
          <!-- 动态参数配置 -->
          <div v-for="param in selectedNodeParams" :key="param.name">
            <a-form-item :label="param.description" :required="param.required">
              <a-input
                v-if="param.type === 'string' || param.type === 'timespan'"
                v-model:value="selectedNodeData.inputs[param.name]"
                :placeholder="param.defaultValue || `请输入${param.description}`"
              />
              <a-switch
                v-else-if="param.type === 'bool'"
                v-model:checked="selectedNodeData.inputs[param.name]"
              />
              <a-input-number
                v-else-if="param.type === 'int'"
                v-model:value="selectedNodeData.inputs[param.name]"
                style="width: 100%"
              />
              <a-textarea
                v-else
                v-model:value="selectedNodeData.inputs[param.name]"
                :placeholder="`请输入${param.description}`"
                :rows="3"
              />
            </a-form-item>
          </div>

          <a-form-item>
            <a-button type="primary" @click="handleUpdateNode">确定</a-button>
          </a-form-item>
        </a-form>
      </a-drawer>
    </div>
  </a-card>

  <!-- 测试执行抽屉 -->
  <a-drawer
    v-model:open="testModalVisible"
    title="测试工作流执行"
    placement="right"
    width="560"
    destroy-on-close
    @close="testModalVisible = false"
  >
    <a-form layout="vertical">
      <a-form-item label="工作流数据（JSON格式）">
        <a-textarea v-model:value="testData" :rows="10" placeholder='{"key": "value"}' />
      </a-form-item>
      <a-form-item label="引用标识（可选）">
        <a-input v-model:value="testReference" placeholder="test-ref-001" />
      </a-form-item>
    </a-form>
    <template #footer>
      <a-space>
        <a-button @click="testModalVisible = false">取消</a-button>
        <a-button type="primary" @click="handleExecuteTest">执行测试</a-button>
      </a-space>
    </template>
  </a-drawer>
</template>

<script setup lang="ts">
import { onMounted, ref, computed, onBeforeUnmount, onUnmounted } from "vue";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { useRouter } from "vue-router";
import { Graph } from "@antv/x6";
import { getWorkflowStepTypes, registerWorkflow, startWorkflow } from "@/services/api";
import type { StepTypeMetadata } from "@/types/api";
import { message } from "ant-design-vue";
import { WORKFLOW_START_NODE_ID, WORKFLOW_END_NODE_ID } from "@/constants/workflow";
import { normalizeJsonValue } from "@/composables/useTimeNormalize";
import { useWorkflowSerializer } from "@/composables/useWorkflowSerializer";
import { useWorkflowGraph } from "@/composables/useWorkflowGraph";

const router = useRouter();
const containerRef = ref<HTMLElement>();
const graphRef = ref<Graph>();
const workflowId = ref("my-test-workflow");
const drawerVisible = ref(false);
interface DesignerSelectedNodeData {
  id: string;
  name: string;
  stepType: string;
  inputs: Record<string, unknown>;
  cellId: string;
}
const selectedNodeData = ref<DesignerSelectedNodeData | null>(null);
const stepTypes = ref<StepTypeMetadata[]>([]);
const testModalVisible = ref(false);
const testData = ref("{}");
const testReference = ref("");

const START_NODE_ID = WORKFLOW_START_NODE_ID;
const END_NODE_ID = WORKFLOW_END_NODE_ID;

const { getDefinitionJson, buildTestDataTemplate } = useWorkflowSerializer(graphRef, stepTypes, workflowId);
const { initGraph, handleDragStart, handleDrop } = useWorkflowGraph(
  containerRef,
  graphRef,
  stepTypes,
  (data) => {
    selectedNodeData.value = data;
    drawerVisible.value = true;
  }
);


const selectedNodeParams = computed(() => {
  const currentNode = selectedNodeData.value;
  if (!currentNode) return [];
  const stepType = stepTypes.value.find((st) => st.type === currentNode.stepType);
  return stepType?.parameters || [];
});



const handleUpdateNode = () => {
  const currentNode = selectedNodeData.value;
  if (!graphRef.value || !currentNode) return;

  const nodeId = currentNode.cellId;
  const node = graphRef.value.getCellById(nodeId);
  if (!node || !node.isNode()) {
    message.warning("节点不存在");
    return;
  }

  const { cellId: _cellId, ...data } = currentNode;
  
  node.setData(data);
  node.setAttrByPath("title/text", data.name);

  drawerVisible.value = false;
  message.success("节点属性已更新");
};

const handleDrawerClose = () => {
  drawerVisible.value = false;
  selectedNodeData.value = null;
};


const handleSave = async () => {
  if (!workflowId.value.trim()) {
    message.warning("请输入工作流ID");
    return;
  }

  let definitionJson = "";
  try {
    definitionJson = getDefinitionJson();
  } catch (e) {
    message.warning(e instanceof Error ? e.message : "流程结构不支持：当前后端仅支持顺序流程（NextStepId）");
    return;
  }
  const stepCount =
    graphRef.value?.getNodes().filter((node) => node.id !== START_NODE_ID && node.id !== END_NODE_ID).length ?? 0;
  if (!graphRef.value || stepCount === 0) {
    message.warning("请至少添加一个步骤节点");
    return;
  }

  try {
    await registerWorkflow({
      workflowId: workflowId.value,
      version: 1,
      definitionJson: definitionJson
    });

    if (!isMounted.value) return;
    message.success("工作流注册成功");
  } catch (err) {
    message.error(err instanceof Error ? err.message : "注册失败");
  }
};

const handleTest = () => {
  if (!workflowId.value.trim()) {
    message.warning("请先保存工作流");
    return;
  }

  // 打开弹窗时：如果为空/仅 {}，自动生成一份示例；否则把用户现有 JSON 先规范化并美化回填
  const raw = testData.value.trim();
  if (!raw || raw === "{}") {
    testData.value = JSON.stringify(buildTestDataTemplate(), null, 2);
  } else {
    try {
      const normalized = normalizeJsonValue(JSON.parse(raw));
      testData.value = JSON.stringify(normalized, null, 2);
    } catch {
      // JSON 非法就保持原样，交给用户修正
    }
  }

  testModalVisible.value = true;
};

const handleExecuteTest = async () => {
  try {
    let data: Record<string, unknown> = {};
    if (testData.value.trim()) {
      data = normalizeJsonValue(JSON.parse(testData.value)) as Record<string, unknown>;
    }

    const instanceId  = await startWorkflow({
      workflowId: workflowId.value,
      version: 1,
      data: data,
      reference: testReference.value || undefined
    });


    if (!isMounted.value) return;

    message.success(`工作流已启动，实例ID: ${instanceId}`);
    testModalVisible.value = false;
    
    // 跳转到监控页面
    router.push(`/workflow/instances?instanceId=${instanceId}`);
  } catch (err) {
    message.error(err instanceof Error ? err.message : "启动失败");
  }
};

onMounted(async () => {
  try {
    stepTypes.value = await getWorkflowStepTypes();

    if (!isMounted.value) return;
    initGraph();

    if (containerRef.value) {
      containerRef.value.addEventListener("drop", handleDrop);
      containerRef.value.addEventListener("dragover", (e) => e.preventDefault());
    }
  } catch (err) {
    message.error(err instanceof Error ? err.message : "加载失败");
  }
});

onBeforeUnmount(() => {
  if (containerRef.value) {
    containerRef.value.removeEventListener("drop", handleDrop);
  }
  graphRef.value?.dispose();
});
</script>

<style scoped>
.designer-container {
  display: flex;
  height: calc(100vh - 200px);
  border: 1px solid var(--color-border-secondary);
  border-radius: var(--border-radius-sm);
}

.designer-toolbar {
  width: 200px;
  border-right: 1px solid var(--color-border-secondary);
  padding: var(--spacing-md);
  background: var(--color-bg-subtle);
  overflow-y: auto;
}

.toolbar-title {
  font-weight: 600;
  margin-bottom: var(--spacing-md);
  color: var(--color-text-primary);
}

.node-types {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.node-type-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 12px;
  border: 1px solid var(--color-border-secondary);
  border-radius: var(--border-radius-sm);
  background: var(--color-bg-container);
  cursor: move;
  transition: all 0.3s;
}

.node-type-item:hover {
  border-color: var(--color-primary);
  box-shadow: var(--shadow-sm);
}

.node-type-disabled {
  opacity: 0.55;
  cursor: not-allowed;
}

.node-type-disabled:hover {
  border-color: var(--color-border-secondary);
  box-shadow: none;
}

.node-icon {
  width: 80px;
  height: 40px;
  display: flex;
  align-items: center;
  justify-content: center;
  border: 1px solid var(--color-border-secondary);
  border-radius: var(--border-radius-sm);
  margin-bottom: var(--spacing-sm);
  font-size: 12px;
  font-weight: 500;
}

.node-label {
  font-size: 12px;
  color: var(--color-text-tertiary);
  display: flex;
  align-items: center;
  gap: 4px;
}

.designer-canvas {
  flex: 1;
  position: relative;
  background: var(--color-bg-hover);
}
</style>
