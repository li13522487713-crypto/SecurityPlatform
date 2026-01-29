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
            :draggable="true"
            @dragstart="handleDragStart($event, stepType)"
          >
            <div class="node-icon" :style="{ borderColor: stepType.color, color: stepType.color }">
              {{ stepType.label }}
            </div>
            <div class="node-label">{{ stepType.label }}</div>
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

  <!-- 测试执行对话框 -->
  <a-modal
    v-model:open="testModalVisible"
    title="测试工作流执行"
    @ok="handleExecuteTest"
    @cancel="testModalVisible = false"
    width="600px"
  >
    <a-form layout="vertical">
      <a-form-item label="工作流数据（JSON格式）">
        <a-textarea v-model:value="testData" :rows="10" placeholder='{"key": "value"}' />
      </a-form-item>
      <a-form-item label="引用标识（可选）">
        <a-input v-model:value="testReference" placeholder="test-ref-001" />
      </a-form-item>
    </a-form>
  </a-modal>
</template>

<script setup lang="ts">
import { onMounted, ref, computed, onBeforeUnmount } from "vue";
import { useRouter } from "vue-router";
import { Graph } from "@antv/x6";
import { getWorkflowStepTypes, registerWorkflow, startWorkflow } from "@/services/api";
import type { StepTypeMetadata } from "@/types/api";
import { message } from "ant-design-vue";

const router = useRouter();
const containerRef = ref<HTMLElement>();
const graphRef = ref<Graph>();
const workflowId = ref("my-test-workflow");
const drawerVisible = ref(false);
const selectedNodeData = ref<any>(null);
const stepTypes = ref<StepTypeMetadata[]>([]);
const testModalVisible = ref(false);
const testData = ref("{}");
const testReference = ref("");

const selectedNodeParams = computed(() => {
  if (!selectedNodeData.value) return [];
  const stepType = stepTypes.value.find((st) => st.type === selectedNodeData.value.stepType);
  return stepType?.parameters || [];
});

const initGraph = () => {
  if (!containerRef.value) return;

  const graph: Graph = new Graph({
    container: containerRef.value,
    grid: true,
    panning: true,
    mousewheel: {
      enabled: true,
      zoomAtMousePosition: true,
      modifiers: "ctrl",
      minScale: 0.5,
      maxScale: 4
    },
    connecting: {
      router: "manhattan",
      connector: {
        name: "rounded",
        args: { radius: 8 }
      },
      anchor: "center",
      connectionPoint: "anchor",
      allowBlank: false,
      snap: { radius: 20 },
      createEdge(): any {
        return graph.createEdge({
          attrs: {
            line: {
              stroke: "#8f8f8f",
              strokeWidth: 1,
              targetMarker: { name: "classic", size: 7 }
            }
          },
          zIndex: 0
        });
      },
      validateConnection({ targetMagnet }) {
        return !!targetMagnet;
      }
    },
    highlighting: {
      magnetAdsorbed: {
        name: "stroke",
        args: {
          attrs: { fill: "#fff", stroke: "#31d0c6", strokeWidth: 4 }
        }
      }
    }
  });

  // 注册自定义节点
  Graph.registerNode("custom-node", {
    inherit: "rect",
    width: 120,
    height: 60,
    attrs: {
      body: { strokeWidth: 1, stroke: "#5F95FF", fill: "#EFF4FF" },
      text: { fontSize: 12, fill: "#262626" }
    },
    ports: {
      groups: {
        top: { position: "top", attrs: { circle: { r: 4, magnet: true, stroke: "#5F95FF", strokeWidth: 1, fill: "#fff", style: { visibility: "hidden" } } } },
        right: { position: "right", attrs: { circle: { r: 4, magnet: true, stroke: "#5F95FF", strokeWidth: 1, fill: "#fff", style: { visibility: "hidden" } } } },
        bottom: { position: "bottom", attrs: { circle: { r: 4, magnet: true, stroke: "#5F95FF", strokeWidth: 1, fill: "#fff", style: { visibility: "hidden" } } } },
        left: { position: "left", attrs: { circle: { r: 4, magnet: true, stroke: "#5F95FF", strokeWidth: 1, fill: "#fff", style: { visibility: "hidden" } } } }
      },
      items: [
        { group: "top", id: "port-top" },
        { group: "right", id: "port-right" },
        { group: "bottom", id: "port-bottom" },
        { group: "left", id: "port-left" }
      ]
    }
  });

  // 双击节点编辑
  graph.on("node:dblclick", ({ node }: any) => {
    const data = node.getData();
    selectedNodeData.value = { ...data, cellId: node.id };
    drawerVisible.value = true;
  });

  // 鼠标悬停显示连接点
  graph.on("node:mouseenter", () => {
    const ports = containerRef.value?.querySelectorAll(".x6-port-body") as NodeListOf<HTMLElement>;
    ports?.forEach((port) => {
      port.style.visibility = "visible";
    });
  });

  graph.on("node:mouseleave", () => {
    const ports = containerRef.value?.querySelectorAll(".x6-port-body") as NodeListOf<HTMLElement>;
    ports?.forEach((port) => {
      port.style.visibility = "hidden";
    });
  });

  graphRef.value = graph;
};

const handleDragStart = (e: DragEvent, stepType: StepTypeMetadata) => {
  if (!graphRef.value) return;
  e.dataTransfer!.effectAllowed = "move";
  e.dataTransfer!.setData("stepType", JSON.stringify(stepType));
};

const handleDrop = (e: DragEvent) => {
  if (!graphRef.value) return;
  e.preventDefault();
  const stepTypeStr = e.dataTransfer?.getData("stepType");
  if (!stepTypeStr) return;

  const stepType: StepTypeMetadata = JSON.parse(stepTypeStr);
  const point = graphRef.value.clientToLocal(e.clientX, e.clientY);
  const nodeId = `step_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;

  const inputs: Record<string, any> = {};
  stepType.parameters.forEach((param) => {
    if (param.defaultValue) {
      inputs[param.name] = param.defaultValue === "true" ? true : param.defaultValue === "false" ? false : param.defaultValue;
    }
  });

  graphRef.value.addNode({
    id: nodeId,
    shape: "custom-node",
    x: point.x - 60,
    y: point.y - 30,
    label: stepType.label,
    data: {
      id: nodeId,
      name: stepType.label,
      stepType: stepType.type,
      inputs: inputs
    },
    attrs: {
      body: {
        fill: `${stepType.color}20`,
        stroke: stepType.color
      }
    }
  });
};

const handleUpdateNode = () => {
  if (!graphRef.value || !selectedNodeData.value) return;

  const nodeId = selectedNodeData.value.cellId;
  const node = graphRef.value.getCellById(nodeId);
  if (!node || !node.isNode()) {
    message.warning("节点不存在");
    return;
  }

  const data = { ...selectedNodeData.value };
  delete data.cellId;
  
  node.setData(data);
  node.setAttrByPath("text/text", data.name);

  drawerVisible.value = false;
  message.success("节点属性已更新");
};

const handleDrawerClose = () => {
  drawerVisible.value = false;
  selectedNodeData.value = null;
};

const getDefinitionJson = (): string => {
  if (!graphRef.value) return "";

  const nodes = graphRef.value.getNodes().map((node) => {
    const data = node.getData();
    return {
      Id: data.id,
      Name: data.name,
      StepType: data.stepType,
      Inputs: data.inputs || {},
      NextStepId: null as string | null // 将通过边来确定
    };
  });

  const edges = graphRef.value.getEdges();
  
  // 根据边设置 NextStepId
  edges.forEach((edge) => {
    const sourceId = edge.getSourceCellId();
    const targetId = edge.getTargetCellId();
    const sourceNode = nodes.find((n) => n.Id === sourceId);
    if (sourceNode) {
      sourceNode.NextStepId = targetId;
    }
  });

  const definition = {
    Id: workflowId.value,
    Version: 1,
    Steps: nodes
  };

  return JSON.stringify(definition);
};

const handleSave = async () => {
  if (!workflowId.value.trim()) {
    message.warning("请输入工作流ID");
    return;
  }

  const definitionJson = getDefinitionJson();
  if (!graphRef.value || graphRef.value.getNodes().length === 0) {
    message.warning("请至少添加一个步骤节点");
    return;
  }

  try {
    await registerWorkflow({
      workflowId: workflowId.value,
      version: 1,
      definitionJson: definitionJson
    });
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
  testModalVisible.value = true;
};

const handleExecuteTest = async () => {
  try {
    let data = {};
    if (testData.value.trim()) {
      data = JSON.parse(testData.value);
    }

    const instanceId = await startWorkflow({
      workflowId: workflowId.value,
      version: 1,
      data: data,
      reference: testReference.value || undefined
    });

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
  border: 1px solid #d9d9d9;
  border-radius: 4px;
}

.designer-toolbar {
  width: 200px;
  border-right: 1px solid #d9d9d9;
  padding: 16px;
  background: #fafafa;
  overflow-y: auto;
}

.toolbar-title {
  font-weight: 600;
  margin-bottom: 16px;
  color: #262626;
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
  border: 1px solid #d9d9d9;
  border-radius: 4px;
  background: white;
  cursor: move;
  transition: all 0.3s;
}

.node-type-item:hover {
  border-color: #1890ff;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.15);
}

.node-icon {
  width: 80px;
  height: 40px;
  display: flex;
  align-items: center;
  justify-content: center;
  border: 1px solid #d9d9d9;
  border-radius: 4px;
  margin-bottom: 8px;
  font-size: 12px;
  font-weight: 500;
}

.node-label {
  font-size: 12px;
  color: #666;
}

.designer-canvas {
  flex: 1;
  position: relative;
  background: #f5f5f5;
}
</style>
