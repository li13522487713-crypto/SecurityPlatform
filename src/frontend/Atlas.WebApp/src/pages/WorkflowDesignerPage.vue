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

const START_NODE_ID = "__start__";
const END_NODE_ID = "__end__";

function pad2(n: number) {
  return n.toString().padStart(2, "0");
}

function formatTimeSpan(totalSeconds: number) {
  const sign = totalSeconds < 0 ? "-" : "";
  const abs = Math.abs(Math.floor(totalSeconds));
  const days = Math.floor(abs / 86400);
  const hours = Math.floor((abs % 86400) / 3600);
  const minutes = Math.floor((abs % 3600) / 60);
  const seconds = abs % 60;

  // .NET TimeSpan 支持 "d.hh:mm:ss"；当天数为 0 时用 "HH:mm:ss"
  if (days > 0) {
    return `${sign}${days}.${pad2(hours)}:${pad2(minutes)}:${pad2(seconds)}`;
  }
  return `${sign}${pad2(hours)}:${pad2(minutes)}:${pad2(seconds)}`;
}

function normalizeTimeSpanInput(value: unknown): unknown {
  if (typeof value !== "string") return value;
  const raw = value.trim();
  if (!raw) return value;

  // 已是 HH:mm:ss / H:mm:ss
  if (/^\d{1,2}:\d{2}:\d{2}$/.test(raw)) return raw;
  // 允许 HH:mm（补秒）
  if (/^\d{1,2}:\d{2}$/.test(raw)) return `${raw}:00`;

  // 允许 5s / 10m / 2h / 1d / 1500ms
  const m = raw.match(/^(\d+(?:\.\d+)?)\s*(ms|s|m|h|d)$/i);
  if (!m) return value;
  const num = Number(m[1]);
  if (Number.isNaN(num)) return value;

  const unit = m[2].toLowerCase();
  const seconds =
    unit === "ms"
      ? Math.round(num / 1000)
      : unit === "s"
        ? Math.round(num)
        : unit === "m"
          ? Math.round(num * 60)
          : unit === "h"
            ? Math.round(num * 3600)
            : Math.round(num * 86400);

  return formatTimeSpan(seconds);
}

function normalizeDateTimeInput(value: unknown): unknown {
  if (typeof value !== "string") return value;
  const raw = value.trim();
  if (!raw) return value;

  // ISO（带 T 或带时区）直接放行
  if (/^\d{4}-\d{2}-\d{2}T/.test(raw) || /[zZ]|[+\-]\d{2}:\d{2}$/.test(raw)) {
    return raw;
  }

  // 支持 "YYYY-MM-DD HH:mm:ss" / "YYYY-MM-DD HH:mm" / "YYYY-MM-DD"
  const m =
    raw.match(/^(\d{4})-(\d{2})-(\d{2})\s+(\d{2}):(\d{2}):(\d{2})$/) ||
    raw.match(/^(\d{4})-(\d{2})-(\d{2})\s+(\d{2}):(\d{2})$/) ||
    raw.match(/^(\d{4})-(\d{2})-(\d{2})$/);
  if (!m) return value;

  const y = m[1];
  const mo = m[2];
  const d = m[3];
  const hh = m[4] ?? "00";
  const mm = m[5] ?? "00";
  const ss = m[6] ?? "00";

  // 输出为 "YYYY-MM-DDTHH:mm:ss"（后端 System.Text.Json 可解析）
  return `${y}-${mo}-${d}T${hh}:${mm}:${ss}`;
}

function normalizeJsonValue(value: unknown): unknown {
  if (Array.isArray(value)) {
    return value.map((it) => normalizeJsonValue(it));
  }
  if (value && typeof value === "object") {
    const obj = value as Record<string, unknown>;
    const next: Record<string, unknown> = {};
    for (const [k, v] of Object.entries(obj)) {
      // 对任意字符串做一次 datetime 归一（不会影响普通字符串）
      next[k] = normalizeJsonValue(normalizeDateTimeInput(v));
    }
    return next;
  }
  // 仅 datetime 在这里做全局归一；timespan 只在步骤参数里按类型归一
  return normalizeDateTimeInput(value);
}

function getLinearPathOrThrow(graph: Graph): string[] {
  const stepNodes = graph.getNodes().filter((n) => n.id !== START_NODE_ID && n.id !== END_NODE_ID);
  const stepNodeIds = new Set(stepNodes.map((n) => n.id));

  const edges = graph.getEdges();
  const outgoing = new Map<string, string[]>();
  const incoming = new Map<string, number>();

  for (const edge of edges) {
    const sourceId = edge.getSourceCellId();
    const targetId = edge.getTargetCellId();
    if (!sourceId || !targetId) continue;
    if (!outgoing.has(sourceId)) outgoing.set(sourceId, []);
    outgoing.get(sourceId)!.push(targetId);
    incoming.set(targetId, (incoming.get(targetId) ?? 0) + 1);
  }

  const startOut = outgoing.get(START_NODE_ID) ?? [];
  if (startOut.length === 0) {
    throw new Error("请从“开始”节点连线到第一个步骤节点");
  }
  if (startOut.length > 1) {
    throw new Error("当前后端仅支持顺序流程（NextStepId），开始节点只能连出一条线");
  }

  // 强制顺序链路：每个步骤节点最多 1 入 1 出，最终到结束
  for (const id of stepNodeIds) {
    const inCount = incoming.get(id) ?? 0;
    if (inCount > 1) {
      throw new Error("当前后端仅支持顺序流程（NextStepId），每个步骤节点最多只能有一条入线");
    }
    const outCount = (outgoing.get(id) ?? []).length;
    if (outCount > 1) {
      throw new Error("当前后端仅支持顺序流程（NextStepId），每个步骤节点最多只能连出一条线");
    }
  }

  const endIn = incoming.get(END_NODE_ID) ?? 0;
  if (endIn === 0) {
    throw new Error("请将最后一个步骤节点连线到“结束”节点");
  }
  if (endIn > 1) {
    throw new Error("当前后端仅支持顺序流程（NextStepId），“结束”节点只能有一条入线");
  }

  const path: string[] = [];
  const visited = new Set<string>();
  let current = startOut[0];
  while (true) {
    if (current === END_NODE_ID) {
      break;
    }
    if (!stepNodeIds.has(current)) {
      throw new Error("检测到无效连线：请确保“开始”只能连接到步骤节点，且步骤节点最终连接到“结束”");
    }
    if (visited.has(current)) {
      throw new Error("检测到循环连线：当前后端仅支持无环的顺序流程");
    }
    visited.add(current);
    path.push(current);
    const outs = outgoing.get(current) ?? [];
    if (outs.length === 0) {
      throw new Error("流程未完整闭合：请将每个步骤节点连线到下一个节点或“结束”");
    }
    current = outs[0];
  }

  if (path.length !== stepNodes.length) {
    throw new Error("存在未接入主链路的步骤节点：当前后端仅支持单一顺序链路（开始→…→结束）");
  }

  return path;
}

function formatLocalIsoSeconds(d: Date) {
  const yyyy = d.getFullYear();
  const mm = pad2(d.getMonth() + 1);
  const dd = pad2(d.getDate());
  const hh = pad2(d.getHours());
  const mi = pad2(d.getMinutes());
  const ss = pad2(d.getSeconds());
  return `${yyyy}-${mm}-${dd}T${hh}:${mi}:${ss}`;
}

function buildTestDataTemplate(): Record<string, any> {
  // 说明：后端 StartWorkflowRequest.Data 是可选 object。这里给一份“可编辑示例”，
  // 重点是让用户看到 datetime/timespan 的推荐格式，并提供常用字段占位。
  const template: Record<string, any> = {
    // 示例字段（可删改）
    now: formatLocalIsoSeconds(new Date())
  };

  const nodes =
    graphRef.value?.getNodes().filter((n) => n.id !== START_NODE_ID && n.id !== END_NODE_ID) ?? [];
  const usedParamTypes = new Map<string, string>();

  for (const node of nodes) {
    const data = node.getData() as any;
    const meta = stepTypes.value.find((st) => st.type === data.stepType);
    for (const p of meta?.parameters ?? []) {
      // 只收集一次同名参数（避免重复）
      if (!usedParamTypes.has(p.name)) {
        usedParamTypes.set(p.name, p.type);
      }
    }
  }

  for (const [name, type] of usedParamTypes.entries()) {
    if (name in template) continue;
    if (type === "timespan") {
      template[name] = "00:00:05";
    } else if (type === "datetime") {
      template[name] = formatLocalIsoSeconds(new Date());
    } else if (type === "bool") {
      template[name] = false;
    } else if (type === "int") {
      template[name] = 0;
    } else if (type === "array") {
      template[name] = [];
    } else {
      template[name] = "";
    }
  }

  return template;
}

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
      validateConnection({ sourceCell, targetCell, targetMagnet }) {
        if (!targetMagnet) return false;
        // 不允许连入开始节点 / 不允许从结束节点连出
        if (targetCell?.id === START_NODE_ID) return false;
        if (sourceCell?.id === END_NODE_ID) return false;

        // 当前后端仅支持顺序流程：每个节点最多 1 入 1 出（避免导出 JSON 不可用）
        if (sourceCell?.id) {
          const outs = graph.getOutgoingEdges(sourceCell as any) ?? [];
          // validateConnection 在连接完成前触发，此处 outs 已包含已有边
          if (outs.length >= 1) return false;
        }
        if (targetCell?.id) {
          const ins = graph.getIncomingEdges(targetCell as any) ?? [];
          if (ins.length >= 1) return false;
        }
        return true;
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
    width: 220,
    height: 72,
    attrs: {
      body: { 
        strokeWidth: 1, 
        stroke: "#d9d9d9", // 默认边框颜色
        fill: "#ffffff",   // 默认背景颜色
        rx: 4,             // 圆角
        ry: 4,
        filter: {
          name: 'dropShadow',
          args: {
            dx: 0,
            dy: 2,
            blur: 5,
            color: 'rgba(0, 0, 0, 0.1)'
          }
        }
      },
      // 标题栏背景
      header: {
        refWidth: '100%',
        height: 24,
        fill: '#576a95', // 默认标题颜色
        stroke: 'none',
        rx: 4, // 顶部圆角
        ry: 4,
        // 通过 clip-path 裁剪底部圆角，或者简单处理
      },
      // 标题文字
      title: {
        text: '节点标题',
        refX: 10,
        refY: 12,
        fill: '#ffffff',
        fontSize: 12,
        textAnchor: 'start',
        textVerticalAnchor: 'middle',
        pointerEvents: 'none'
      },
      // 内容文字
      text: { 
        text: '请选择',
        refX: 0.5, 
        refY: 48, // header height (24) + padding
        fontSize: 14, 
        fill: "#262626",
        textAnchor: 'middle',
        textVerticalAnchor: 'middle'
      }
    },
    markup: [
      {
        tagName: 'rect',
        selector: 'body',
      },
      {
        tagName: 'rect',
        selector: 'header',
      },
      {
        tagName: 'text',
        selector: 'title',
      },
      {
        tagName: 'text',
        selector: 'text',
      },
    ],
    ports: {
      groups: {
        top: { 
          position: "top", 
          attrs: { 
            circle: { 
              r: 4, 
              magnet: true, 
              stroke: "#5F95FF", 
              strokeWidth: 1, 
              fill: "#fff", 
              visibility: "hidden" // 默认隐藏
            } 
          } 
        },
        right: { 
          position: "right", 
          attrs: { 
            circle: { 
              r: 4, 
              magnet: true, 
              stroke: "#5F95FF", 
              strokeWidth: 1, 
              fill: "#fff", 
              visibility: "hidden" 
            } 
          } 
        },
        bottom: { 
          position: "bottom", 
          attrs: { 
            circle: { 
              r: 4, 
              magnet: true, 
              stroke: "#5F95FF", 
              strokeWidth: 1, 
              fill: "#fff", 
              visibility: "hidden" 
            } 
          } 
        },
        left: { 
          position: "left", 
          attrs: { 
            circle: { 
              r: 4, 
              magnet: true, 
              stroke: "#5F95FF", 
              strokeWidth: 1, 
              fill: "#fff", 
              visibility: "hidden" 
            } 
          } 
        }
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
    if (node?.id === START_NODE_ID || node?.id === END_NODE_ID) {
      return;
    }
    const data = node.getData();
    selectedNodeData.value = { ...data, cellId: node.id };
    drawerVisible.value = true;
  });

  // 鼠标悬停显示连接点
  graph.on("node:mouseenter", ({ node }: any) => {
    const ports = containerRef.value?.querySelectorAll(`[data-cell-id="${node.id}"] .x6-port-body`) as NodeListOf<HTMLElement>;
    ports?.forEach((port) => {
      port.setAttribute("visibility", "visible");
    });
    // 悬停样式
    node.setAttrs({
      body: { stroke: "#3296fa", filter: { name: 'dropShadow', args: { dx: 0, dy: 0, blur: 6, color: 'rgba(50, 150, 250, 0.3)' } } }
    });
  });

  graph.on("node:mouseleave", ({ node }: any) => {
    const ports = containerRef.value?.querySelectorAll(`[data-cell-id="${node.id}"] .x6-port-body`) as NodeListOf<HTMLElement>;
    ports?.forEach((port) => {
      port.setAttribute("visibility", "hidden");
    });
    // 恢复样式
    node.setAttrs({
      body: { stroke: "#d9d9d9", filter: { name: 'dropShadow', args: { dx: 0, dy: 2, blur: 5, color: 'rgba(0, 0, 0, 0.1)' } } }
    });
  });

  graphRef.value = graph;

  // 连接完成后兜底校验（给出提示）
  graph.on("edge:connected", ({ edge }: any) => {
    const sourceId = edge.getSourceCellId();
    const targetId = edge.getTargetCellId();
    const sourceCell = sourceId ? graph.getCellById(sourceId) : null;
    const targetCell = targetId ? graph.getCellById(targetId) : null;
    if (!sourceCell || !targetCell) return;

    // 移除冗余校验，validateConnection 已经处理了核心规则
    // 保留 edge:connected 用于其他可能的后处理，目前可以为空
  });

  // 默认开始/结束节点（仅用于画布展示，不会写入后端 DSL）
  graph.addNode({
    id: START_NODE_ID,
    shape: "custom-node",
    x: 80,
    y: 120,
    data: {
      id: START_NODE_ID,
      name: "开始",
      stepType: "Start",
      inputs: {}
    },
    attrs: {
      body: { fill: "#f6ffed", stroke: "#52c41a" },
      header: { fill: "#52c41a" },
      title: { text: "开始" },
      text: { text: "流程开始" }
    }
  });

  graph.addNode({
    id: END_NODE_ID,
    shape: "custom-node",
    x: 520,
    y: 120,
    data: {
      id: END_NODE_ID,
      name: "结束",
      stepType: "End",
      inputs: {}
    },
    attrs: {
      body: { fill: "#fff1f0", stroke: "#ff4d4f" },
      header: { fill: "#ff4d4f" },
      title: { text: "结束" },
      text: { text: "流程结束" }
    }
  });
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
    data: {
      id: nodeId,
      name: stepType.label,
      stepType: stepType.type,
      inputs: inputs
    },
    attrs: {
      header: { fill: stepType.color },
      title: { text: stepType.label },
      text: { text: `请选择${stepType.label}` }
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
  node.setAttrByPath("title/text", data.name);

  drawerVisible.value = false;
  message.success("节点属性已更新");
};

const handleDrawerClose = () => {
  drawerVisible.value = false;
  selectedNodeData.value = null;
};

const getDefinitionJson = (): string => {
  if (!graphRef.value) return "";

  const graph = graphRef.value;
  const path = getLinearPathOrThrow(graph);

  const outgoing = new Map<string, string>();
  for (const edge of graph.getEdges()) {
    const sourceId = edge.getSourceCellId();
    const targetId = edge.getTargetCellId();
    if (!sourceId || !targetId) continue;
    outgoing.set(sourceId, targetId);
  }

  const nodes = path.map((nodeId) => {
    const node = graph.getCellById(nodeId);
    if (!node || !node.isNode()) {
      throw new Error("步骤节点不存在，请刷新后重试");
    }
    const data: any = node.getData();
    const stepMeta = stepTypes.value.find((st) => st.type === data.stepType);
    const inputs: Record<string, any> = { ...(data.inputs || {}) };
    // 将步骤输入按参数类型归一成后端可解析格式
    stepMeta?.parameters?.forEach((p: any) => {
      const raw = inputs[p.name];
      if (p.type === "timespan") {
        inputs[p.name] = normalizeTimeSpanInput(raw);
      } else if (p.type === "datetime") {
        inputs[p.name] = normalizeDateTimeInput(raw);
      }
    });

    const next = outgoing.get(nodeId);
    const nextStepId = next && next !== END_NODE_ID ? next : null;

    return {
      Id: data.id,
      Name: data.name,
      StepType: data.stepType,
      Inputs: inputs,
      NextStepId: nextStepId
    };
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
    let data = {};
    if (testData.value.trim()) {
      data = normalizeJsonValue(JSON.parse(testData.value)) as any;
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
}

.designer-canvas {
  flex: 1;
  position: relative;
  background: var(--color-bg-hover);
}
</style>
