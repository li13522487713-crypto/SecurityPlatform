<template>
  <a-card class="page-card">
    <template #title>
      <a-input
        v-model:value="flowName"
        placeholder="流程名称"
        style="width: 300px"
        :maxlength="100"
      />
    </template>
    <template #extra>
      <a-space>
        <a-button @click="handleSave">保存</a-button>
        <a-button type="primary" @click="handlePublish">发布</a-button>
      </a-space>
    </template>
    <div class="designer-container">
      <div class="designer-toolbar">
        <div class="node-types">
          <div
            v-for="nodeType in nodeTypes"
            :key="nodeType.type"
            class="node-type-item"
            :draggable="true"
            @dragstart="handleDragStart($event, nodeType)"
          >
            <div class="node-icon" :class="`node-${nodeType.type}`">
              {{ nodeType.label }}
            </div>
            <div class="node-label">{{ nodeType.label }}</div>
          </div>
        </div>
      </div>
      <div ref="containerRef" class="designer-canvas"></div>
      <a-drawer
        v-model:open="drawerVisible"
        title="节点属性"
        placement="right"
        width="400"
        @close="handleDrawerClose"
      >
        <a-form :model="selectedNodeData" layout="vertical" v-if="selectedNodeData">
          <a-form-item label="节点名称">
            <a-input v-model:value="selectedNodeData.label" />
          </a-form-item>
          <a-form-item v-if="selectedNodeData.type === 'approve'" label="审批人类型">
            <a-select v-model:value="selectedNodeData.assigneeType">
              <a-select-option :value="0">指定用户</a-select-option>
              <a-select-option :value="1">按角色</a-select-option>
              <a-select-option :value="2">部门负责人</a-select-option>
            </a-select>
          </a-form-item>
          <a-form-item v-if="selectedNodeData.type === 'approve' && selectedNodeData.assigneeType !== undefined" label="审批人值">
            <a-input v-model:value="selectedNodeData.assigneeValue" placeholder="用户ID/角色代码/部门ID" />
          </a-form-item>
          <a-form-item v-if="selectedNodeData.type === 'approve'" label="审批模式">
            <a-select v-model:value="selectedNodeData.approvalMode">
              <a-select-option value="all">会签（全部通过）</a-select-option>
              <a-select-option value="any">或签（任一通过）</a-select-option>
            </a-select>
          </a-form-item>
          <a-form-item v-if="selectedNodeData.type === 'condition'" label="条件规则">
            <a-form :model="conditionRule" layout="vertical">
              <a-form-item label="字段">
                <a-input v-model:value="conditionRule.field" placeholder="字段名" />
              </a-form-item>
              <a-form-item label="运算符">
                <a-select v-model:value="conditionRule.operator">
                  <a-select-option value="equals">等于</a-select-option>
                  <a-select-option value="notEquals">不等于</a-select-option>
                  <a-select-option value="greaterThan">大于</a-select-option>
                  <a-select-option value="lessThan">小于</a-select-option>
                  <a-select-option value="greaterThanOrEqual">大于等于</a-select-option>
                  <a-select-option value="lessThanOrEqual">小于等于</a-select-option>
                  <a-select-option value="in">包含于</a-select-option>
                  <a-select-option value="contains">包含</a-select-option>
                  <a-select-option value="startsWith">以...开始</a-select-option>
                  <a-select-option value="endsWith">以...结束</a-select-option>
                </a-select>
              </a-form-item>
              <a-form-item label="值">
                <a-input v-model:value="conditionRule.value" placeholder="比较值" />
              </a-form-item>
            </a-form>
          </a-form-item>
          <a-form-item>
            <a-button type="primary" @click="handleUpdateNode">确定</a-button>
          </a-form-item>
        </a-form>
      </a-drawer>
    </div>
  </a-card>
</template>

<script setup lang="ts">
import { onMounted, ref, onBeforeUnmount } from "vue";
import { useRoute, useRouter } from "vue-router";
import { Graph } from "@antv/x6";
import { getApprovalFlowById, createApprovalFlow, updateApprovalFlow, publishApprovalFlow } from "@/services/api";
import { message } from "ant-design-vue";

const route = useRoute();
const router = useRouter();
const containerRef = ref<HTMLElement>();
const graphRef = ref<Graph>();
const flowName = ref("");
const flowId = ref<string | null>(null);
const drawerVisible = ref(false);
const selectedNodeData = ref<any>(null);
const conditionRule = ref({ field: "", operator: "equals", value: "" });

const nodeTypes = [
  { type: "start", label: "开始", color: "#52c41a" },
  { type: "approve", label: "审批", color: "#1890ff" },
  { type: "condition", label: "条件", color: "#faad14" },
  { type: "end", label: "结束", color: "#ff4d4f" }
];

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
        args: {
          radius: 8
        }
      },
      anchor: "center",
      connectionPoint: "anchor",
      allowBlank: false,
      snap: {
        radius: 20
      },
      createEdge(): any {
        return graph.createEdge({
          attrs: {
            line: {
              stroke: "#8f8f8f",
              strokeWidth: 1,
              targetMarker: {
                name: "classic",
                size: 7
              }
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
          attrs: {
            fill: "#fff",
            stroke: "#31d0c6",
            strokeWidth: 4
          }
        }
      }
    },
  });

  // 注册节点
  Graph.registerNode("custom-node", {
    inherit: "rect",
    width: 120,
    height: 60,
    attrs: {
      body: {
        strokeWidth: 1,
        stroke: "#5F95FF",
        fill: "#EFF4FF"
      },
      text: {
        fontSize: 12,
        fill: "#262626"
      }
    },
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
              style: {
                visibility: "hidden"
              }
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
              style: {
                visibility: "hidden"
              }
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
              style: {
                visibility: "hidden"
              }
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
              style: {
                visibility: "hidden"
              }
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

  // 节点双击事件
  graph.on("node:dblclick", ({ node }: any) => {
    const data = node.getData();
    selectedNodeData.value = { ...data, id: node.id };
    if (data.type === "condition" && data.conditionRule) {
      conditionRule.value = { ...data.conditionRule };
    } else {
      conditionRule.value = { field: "", operator: "equals", value: "" };
    }
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

const handleDragStart = (e: DragEvent, nodeType: any) => {
  if (!graphRef.value) return;
  e.dataTransfer!.effectAllowed = "move";
  e.dataTransfer!.setData("nodeType", JSON.stringify(nodeType));
};

const handleDrop = (e: DragEvent) => {
  if (!graphRef.value) return;
  e.preventDefault();
  const nodeTypeStr = e.dataTransfer?.getData("nodeType");
  if (!nodeTypeStr) return;

  const nodeType = JSON.parse(nodeTypeStr);
  const point = graphRef.value.clientToLocal(e.clientX, e.clientY);
  const nodeId = `node_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;

  graphRef.value.addNode({
    id: nodeId,
    shape: "custom-node",
    x: point.x - 60,
    y: point.y - 30,
    label: nodeType.label,
    data: {
      type: nodeType.type,
      label: nodeType.label,
      assigneeType: nodeType.type === "approve" ? 0 : undefined,
      assigneeValue: "",
      approvalMode: nodeType.type === "approve" ? "all" : undefined,
      conditionRule: nodeType.type === "condition" ? { field: "", operator: "equals", value: "" } : undefined
    },
    attrs: {
      body: {
        fill: nodeType.color ? `${nodeType.color}20` : "#EFF4FF",
        stroke: nodeType.color || "#5F95FF"
      }
    }
  });
};

const handleUpdateNode = () => {
  if (!graphRef.value || !selectedNodeData.value) return;

  const nodeId = selectedNodeData.value.id;
  if (!nodeId) {
    message.warning("节点ID不存在");
    return;
  }

  const node = graphRef.value.getCellById(nodeId);
  if (!node || !node.isNode()) {
    message.warning("节点不存在");
    return;
  }

  const data = { ...selectedNodeData.value };
  delete (data as any).id; // 移除 id，因为它是节点的属性，不是 data 的一部分
  if (data.type === "condition") {
    data.conditionRule = { ...conditionRule.value };
  }

  node.setData(data);
  node.setAttrByPath("text/text", data.label);

  drawerVisible.value = false;
  message.success("节点属性已更新");
};

const handleDrawerClose = () => {
  drawerVisible.value = false;
  selectedNodeData.value = null;
};

const loadFlow = async () => {
  const id = route.params.id as string;
  if (!id || id === "undefined") {
    flowName.value = "";
    flowId.value = null;
    return;
  }

  try {
    const flow = await getApprovalFlowById(id);
    flowName.value = flow.name;
    flowId.value = flow.id;

    if (flow.definitionJson) {
      const definition = JSON.parse(flow.definitionJson);
      loadDefinition(definition);
    }
  } catch (err) {
    message.error(err instanceof Error ? err.message : "加载失败");
  }
};

const loadDefinition = (definition: any) => {
  if (!graphRef.value) return;

  graphRef.value.clearCells();

  // 加载节点
  definition.nodes?.forEach((node: any) => {
    const nodeType = nodeTypes.find((nt) => nt.type === node.type);
    graphRef.value!.addNode({
      id: node.id,
      shape: "custom-node",
      x: node.x || 100,
      y: node.y || 100,
      label: node.label || nodeType?.label,
      data: {
        type: node.type,
        label: node.label || nodeType?.label,
        assigneeType: node.assigneeType,
        assigneeValue: node.assigneeValue,
        approvalMode: node.approvalMode,
        conditionRule: node.conditionRule
      },
      attrs: {
        body: {
          fill: nodeType?.color ? `${nodeType.color}20` : "#EFF4FF",
          stroke: nodeType?.color || "#5F95FF"
        }
      }
    });
  });

  // 加载边
  definition.edges?.forEach((edge: any) => {
    graphRef.value!.addEdge({
      source: edge.source,
      target: edge.target,
      attrs: {
        line: {
          stroke: "#8f8f8f",
          strokeWidth: 1,
          targetMarker: {
            name: "classic",
            size: 7
          }
        }
      },
      data: {
        conditionRule: edge.conditionRule
      }
    });
  });
};

const getDefinitionJson = (): string => {
  if (!graphRef.value) return JSON.stringify({ nodes: [], edges: [] });

  const nodes = graphRef.value.getNodes().map((node) => {
    const position = node.position();
    const data = node.getData();
    return {
      id: node.id,
      type: data.type,
      label: data.label || (node.getAttrByPath("text/text") as string) || "",
      x: position.x,
      y: position.y,
      assigneeType: data.assigneeType,
      assigneeValue: data.assigneeValue,
      approvalMode: data.approvalMode,
      conditionRule: data.conditionRule
    };
  });

  const edges = graphRef.value.getEdges().map((edge) => {
    const data = edge.getData();
    return {
      source: edge.getSourceCellId(),
      target: edge.getTargetCellId(),
      conditionRule: data.conditionRule
    };
  });

  return JSON.stringify({ nodes, edges });
};

const handleSave = async () => {
  if (!flowName.value.trim()) {
    message.warning("请输入流程名称");
    return;
  }

  const definitionJson = getDefinitionJson();
  if (!definitionJson || definitionJson === '{"nodes":[],"edges":[]}') {
    message.warning("请至少添加一个开始节点和一个结束节点");
    return;
  }

  try {
    if (flowId.value) {
      await updateApprovalFlow(flowId.value, {
        name: flowName.value,
        definitionJson
      });
      message.success("保存成功");
    } else {
      const result = await createApprovalFlow({
        name: flowName.value,
        definitionJson
      });
      flowId.value = result.id;
      router.replace(`/approval/designer/${result.id}`);
      message.success("创建成功");
    }
  } catch (err) {
    message.error(err instanceof Error ? err.message : "保存失败");
  }
};

const handlePublish = async () => {
  if (!flowId.value) {
    message.warning("请先保存流程");
    return;
  }

  try {
    await publishApprovalFlow(flowId.value);
    message.success("发布成功");
    router.push("/approval/flows");
  } catch (err) {
    message.error(err instanceof Error ? err.message : "发布失败");
  }
};

onMounted(() => {
  initGraph();
  loadFlow();

  if (containerRef.value) {
    containerRef.value.addEventListener("drop", handleDrop);
    containerRef.value.addEventListener("dragover", (e) => e.preventDefault());
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
}

.designer-toolbar {
  width: 200px;
  border-right: 1px solid #d9d9d9;
  padding: 16px;
  background: #fafafa;
  overflow-y: auto;
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
}

.node-start {
  background: #52c41a20;
  border-color: #52c41a;
  color: #52c41a;
}

.node-approve {
  background: #1890ff20;
  border-color: #1890ff;
  color: #1890ff;
}

.node-condition {
  background: #faad1420;
  border-color: #faad14;
  color: #faad14;
}

.node-end {
  background: #ff4d4f20;
  border-color: #ff4d4f;
  color: #ff4d4f;
}

.node-label {
  font-size: 12px;
  color: #666;
}

.designer-canvas {
  flex: 1;
  position: relative;
}
</style>
