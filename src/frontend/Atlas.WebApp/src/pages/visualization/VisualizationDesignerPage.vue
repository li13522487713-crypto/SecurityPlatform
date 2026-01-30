<template>
  <a-card class="page-card" :bordered="false">
    <template #title>流程可视化设计器（骨架）</template>
    <template #extra>
      <a-space>
        <a-button @click="handleSave">保存草稿</a-button>
        <a-button @click="handleValidate">校验</a-button>
        <a-button type="primary" @click="handlePublish">发布</a-button>
      </a-space>
    </template>

    <div class="designer-shell">
      <div class="panel panel-left">
        <div class="panel-title">节点库</div>
        <a-list size="small" bordered :data-source="nodeLibrary">
          <template #renderItem="{ item }">
            <a-list-item class="node-item">
              <a-badge :color="item.color" />
              <span
                class="node-label"
                draggable="true"
                @dragstart="(e) => handleDragStart(e, item)"
              >
                {{ item.label }}
              </span>
            </a-list-item>
          </template>
        </a-list>
      </div>

      <div class="panel panel-center">
        <div class="panel-title">画布</div>
        <div ref="canvasRef" class="canvas"></div>
      </div>

      <div class="panel panel-right">
        <div class="panel-title">属性配置</div>
        <a-form layout="vertical">
          <a-form-item label="流程名称">
            <a-input v-model:value="processName" placeholder="请输入流程名称" />
          </a-form-item>
          <a-form-item label="版本">
            <a-input-number v-model:value="version" :min="1" :max="999" style="width: 100%" />
          </a-form-item>
          <a-form-item label="备注">
            <a-textarea v-model:value="note" :rows="3" />
          </a-form-item>
          <a-divider />
          <a-form-item label="选中节点名称">
            <a-input v-model:value="selectedNodeName" :disabled="!selectedNode" placeholder="选择画布节点" />
          </a-form-item>
          <a-form-item label="责任人">
            <a-input v-model:value="selectedNodeAssignee" :disabled="!selectedNode" />
          </a-form-item>
          <a-form-item label="超时(分钟)">
            <a-input-number v-model:value="selectedNodeTimeout" :disabled="!selectedNode" style="width: 100%" />
          </a-form-item>
          <a-button type="primary" block :disabled="!selectedNode" @click="applyNodeEdit">更新节点</a-button>
        </a-form>
      </div>
    </div>
  </a-card>
</template>

<script setup lang="ts">
import { onBeforeUnmount, onMounted, reactive, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { message } from "ant-design-vue";
import { Graph, Node } from "@antv/x6";
import { Dnd } from "@antv/x6-plugin-dnd";
import {
  getVisualizationProcessDetail,
  publishVisualizationProcess,
  saveVisualizationProcess,
  validateVisualizationProcess
} from "@/services/api";

interface FlowNodeData {
  type?: string;
  label?: string;
  name?: string;
  assignee?: string;
  timeoutMinutes?: number;
}

interface FlowNodeDefinition {
  id: string;
  type: string;
  label?: string;
  x: number;
  y: number;
  data?: FlowNodeData;
}

interface FlowEdgeDefinition {
  source: string;
  target: string;
  data?: Record<string, string>;
  label?: string;
}

interface FlowDefinition {
  nodes: FlowNodeDefinition[];
  edges: FlowEdgeDefinition[];
}

interface NodeLibraryItem {
  label: string;
  color: string;
  type: string;
}

const route = useRoute();
const router = useRouter();
const processId = ref<string>();
const processName = ref("示例流程");
const version = ref(1);
const note = ref("");
const canvasDefinition = reactive<FlowDefinition>({ nodes: [], edges: [] });
const canvasRef = ref<HTMLDivElement>();
const graphRef = ref<Graph>();
const dndRef = ref<Dnd>();

const nodeLibrary: NodeLibraryItem[] = [
  { label: "开始", color: "#1890ff", type: "start" },
  { label: "审批", color: "#52c41a", type: "approve" },
  { label: "条件", color: "#fa8c16", type: "condition" },
  { label: "抄送", color: "#722ed1", type: "cc" },
  { label: "结束", color: "#595959", type: "end" }
];

const selectedNode = ref<Node>();
const selectedNodeName = ref("");
const selectedNodeAssignee = ref("");
const selectedNodeTimeout = ref<number | null>(null);

const buildDefinition = (): FlowDefinition => {
  const graph = graphRef.value;
  if (!graph) {
    return canvasDefinition;
  }

  const nodes = graph.getNodes().map((node) => {
    const data = node.getData() as FlowNodeData | undefined;
    const position = node.getPosition();
    const label = (node.getAttrs().label?.text as string | undefined) ?? data?.label ?? node.id;
    return {
      id: node.id,
      type: data?.type ?? "node",
      label,
      x: position.x,
      y: position.y,
      data: { ...data, label }
    };
  });

  const edges = graph.getEdges().map((edge) => {
    const source = edge.getSourceCellId() ?? "";
    const target = edge.getTargetCellId() ?? "";
    return {
      source,
      target,
      data: edge.getData() as Record<string, string> | undefined,
      label: edge.getLabels()?.[0]?.attrs?.label?.text as string | undefined
    };
  });

  return { nodes, edges };
};

const loadDefinition = (definitionJson: string) => {
  if (!graphRef.value) return;
  graphRef.value.clearCells();
  try {
    const parsed = JSON.parse(definitionJson) as FlowDefinition;
    const nodes = parsed.nodes ?? [];
    const edges = parsed.edges ?? [];
    nodes.forEach((node) => {
      graphRef.value?.addNode({
        id: node.id,
        width: 120,
        height: 40,
        x: node.x,
        y: node.y,
        attrs: {
          body: { stroke: "#1890ff", fill: "#fff" },
          label: { text: node.label ?? node.id, fill: "#262626" }
        },
        data: { ...(node.data ?? {}), type: node.type }
      });
    });
    edges.forEach((edge) => {
      if (!edge.source || !edge.target) return;
      graphRef.value?.addEdge({
        source: edge.source,
        target: edge.target,
        data: edge.data ?? {},
        labels: edge.label ? [{ attrs: { label: { text: edge.label } } }] : undefined
      });
    });
  } catch (err) {
    message.error("流程定义解析失败");
  }
};

const handleValidate = async () => {
  const definitionJson = JSON.stringify(buildDefinition());
  const result = await validateVisualizationProcess({ definitionJson });
  if (result.passed) {
    message.success("校验通过");
  } else {
    message.error(`校验失败：${result.errors.join("；")}`);
  }
};

const handlePublish = async () => {
  const definitionJson = JSON.stringify(buildDefinition());
  const validateResult = await validateVisualizationProcess({ definitionJson });
  if (!validateResult.passed) {
    message.error("请先修复校验错误再发布");
    return;
  }

  if (!processId.value) {
    const saved = await saveVisualizationProcess({
      name: processName.value,
      definitionJson
    });
    processId.value = saved.processId;
    version.value = saved.version;
  }

  const result = await publishVisualizationProcess({
    processId: processId.value,
    version: version.value,
    note: note.value
  });
  message.success(`已发布：${result.processId} v${result.version}`);
  router.replace(`/visualization/designer/${result.processId}`);
};

const handleSave = async () => {
  const definitionJson = JSON.stringify(buildDefinition());
  const saved = await saveVisualizationProcess({
    processId: processId.value,
    name: processName.value,
    definitionJson
  });
  processId.value = saved.processId;
  version.value = saved.version;
  message.success("已保存草稿");
  router.replace(`/visualization/designer/${saved.processId}`);
};

const initGraph = () => {
  if (!canvasRef.value) return;
  const graph = new Graph({
    container: canvasRef.value,
    grid: true,
    panning: true,
    mousewheel: true,
    connecting: {
      snap: true,
      allowBlank: false
    }
  });
  graphRef.value = graph;
  dndRef.value = new Dnd({ target: graph, scaled: false });

  graph.on("node:click", ({ node }) => {
    selectedNode.value = node;
    selectedNodeName.value = (node.getData()?.name as string) || (node.getAttrs().label?.text as string) || "";
    selectedNodeAssignee.value = (node.getData()?.assignee as string) || "";
    const timeout = node.getData()?.timeoutMinutes;
    selectedNodeTimeout.value = timeout ?? null;
  });
};

const handleDragStart = (e: DragEvent, item: NodeLibraryItem) => {
  if (!graphRef.value || !dndRef.value) return;
  const node = graphRef.value.createNode({
    width: 120,
    height: 40,
    attrs: {
      body: { stroke: item.color, fill: "#fff" },
      label: { text: item.label, fill: "#262626" }
    },
    data: { type: item.type, label: item.label }
  });
  dndRef.value.start(node, e);
};

const applyNodeEdit = () => {
  if (!selectedNode.value) return;
  selectedNode.value.setData({
    ...selectedNode.value.getData(),
    name: selectedNodeName.value,
    label: selectedNodeName.value || (selectedNode.value.getData()?.label as string | undefined),
    assignee: selectedNodeAssignee.value,
    timeoutMinutes: selectedNodeTimeout.value ?? undefined
  });
  selectedNode.value.setAttrs({
    label: { text: selectedNodeName.value || selectedNode.value.getAttrs().label?.text }
  });
  message.success("节点已更新");
};

onMounted(() => {
  initGraph();
  const idParam = route.params.id;
  if (typeof idParam === "string" && idParam.length > 0) {
    processId.value = idParam;
    getVisualizationProcessDetail(idParam)
      .then((detail) => {
        processName.value = detail.name;
        version.value = detail.version;
        loadDefinition(detail.definitionJson);
      })
      .catch((err) => {
        message.error((err as Error).message);
      });
  }
});

onBeforeUnmount(() => {
  graphRef.value?.dispose();
});
</script>

<style scoped>
.designer-shell {
  display: grid;
  grid-template-columns: 240px 1fr 300px;
  gap: 12px;
  min-height: 520px;
}

.panel {
  background: #fff;
  border: 1px solid #f0f0f0;
  border-radius: 6px;
  padding: 12px;
  display: flex;
  flex-direction: column;
}

.panel-title {
  font-weight: 600;
  margin-bottom: 8px;
}

.canvas-placeholder {
  color: #8c8c8c;
  background: #fafafa;
}

.canvas {
  flex: 1;
  border: 1px dashed #d9d9d9;
  border-radius: 4px;
  min-height: 520px;
}

.node-item {
  display: flex;
  gap: 8px;
  align-items: center;
}

.node-label {
  font-size: 13px;
}
</style>
