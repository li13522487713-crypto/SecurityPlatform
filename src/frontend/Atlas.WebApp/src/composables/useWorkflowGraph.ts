/**
 * 工作流图形初始化 Composable
 * 封装 AntV X6 图的创建、节点注册、事件绑定、拖拽处理
 */
import type { Ref } from "vue";
import { Graph } from "@antv/x6";
import type { StepTypeMetadata } from "@/types/api";
import { WORKFLOW_START_NODE_ID, WORKFLOW_END_NODE_ID } from "@/constants/workflow";

interface WorkflowNodeData {
  id: string;
  name: string;
  stepType: string;
  inputs: Record<string, unknown>;
}

export function useWorkflowGraph(
  containerRef: Ref<HTMLElement | undefined>,
  graphRef: Ref<Graph | undefined>,
  stepTypes: Ref<StepTypeMetadata[]>,
  onNodeSelect: (data: (WorkflowNodeData & { cellId: string }) | null) => void
) {
  function initGraph() {
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
        createEdge() {
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
          if (targetCell?.id === WORKFLOW_START_NODE_ID) return false;
          if (sourceCell?.id === WORKFLOW_END_NODE_ID) return false;
          if (sourceCell?.id) {
             
            const outs = graph.getOutgoingEdges(sourceCell as any) ?? [];
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

    Graph.registerNode("custom-node", {
      inherit: "rect",
      width: 220,
      height: 72,
      attrs: {
        body: {
          strokeWidth: 1,
          stroke: "#d9d9d9",
          fill: "#ffffff",
          rx: 4,
          ry: 4,
          filter: {
            name: "dropShadow",
            args: { dx: 0, dy: 2, blur: 5, color: "rgba(0, 0, 0, 0.1)" }
          }
        },
        header: {
          refWidth: "100%",
          height: 24,
          fill: "#576a95",
          stroke: "none",
          rx: 4,
          ry: 4
        },
        title: {
          text: "节点标题",
          refX: 10,
          refY: 12,
          fill: "#ffffff",
          fontSize: 12,
          textAnchor: "start",
          textVerticalAnchor: "middle",
          pointerEvents: "none"
        },
        text: {
          text: "请选择",
          refX: 0.5,
          refY: 48,
          fontSize: 14,
          fill: "#262626",
          textAnchor: "middle",
          textVerticalAnchor: "middle"
        }
      },
      markup: [
        { tagName: "rect", selector: "body" },
        { tagName: "rect", selector: "header" },
        { tagName: "text", selector: "title" },
        { tagName: "text", selector: "text" }
      ],
      ports: {
        groups: {
          top: { position: "top", attrs: { circle: { r: 4, magnet: true, stroke: "#5F95FF", strokeWidth: 1, fill: "#fff", visibility: "hidden" } } },
          right: { position: "right", attrs: { circle: { r: 4, magnet: true, stroke: "#5F95FF", strokeWidth: 1, fill: "#fff", visibility: "hidden" } } },
          bottom: { position: "bottom", attrs: { circle: { r: 4, magnet: true, stroke: "#5F95FF", strokeWidth: 1, fill: "#fff", visibility: "hidden" } } },
          left: { position: "left", attrs: { circle: { r: 4, magnet: true, stroke: "#5F95FF", strokeWidth: 1, fill: "#fff", visibility: "hidden" } } }
        },
        items: [
          { group: "top", id: "port-top" },
          { group: "right", id: "port-right" },
          { group: "bottom", id: "port-bottom" },
          { group: "left", id: "port-left" }
        ]
      }
    });

     
    graph.on("node:dblclick", ({ node }: any) => {
      if (node?.id === WORKFLOW_START_NODE_ID || node?.id === WORKFLOW_END_NODE_ID) return;
      const data = node.getData() as WorkflowNodeData;
      onNodeSelect({ ...data, cellId: node.id as string });
    });

     
    graph.on("node:mouseenter", ({ node }: any) => {
      const ports = containerRef.value?.querySelectorAll(`[data-cell-id="${node.id}"] .x6-port-body`) as NodeListOf<HTMLElement>;
      ports?.forEach((port) => port.setAttribute("visibility", "visible"));
      node.setAttrs({ body: { stroke: "#3296fa", filter: { name: "dropShadow", args: { dx: 0, dy: 0, blur: 6, color: "rgba(50, 150, 250, 0.3)" } } } });
    });

     
    graph.on("node:mouseleave", ({ node }: any) => {
      const ports = containerRef.value?.querySelectorAll(`[data-cell-id="${node.id}"] .x6-port-body`) as NodeListOf<HTMLElement>;
      ports?.forEach((port) => port.setAttribute("visibility", "hidden"));
      node.setAttrs({ body: { stroke: "#d9d9d9", filter: { name: "dropShadow", args: { dx: 0, dy: 2, blur: 5, color: "rgba(0, 0, 0, 0.1)" } } } });
    });

    graphRef.value = graph;

    graph.addNode({
      id: WORKFLOW_START_NODE_ID,
      shape: "custom-node",
      x: 80,
      y: 120,
      data: { id: WORKFLOW_START_NODE_ID, name: "开始", stepType: "Start", inputs: {} },
      attrs: { body: { fill: "#f6ffed", stroke: "#52c41a" }, header: { fill: "#52c41a" }, title: { text: "开始" }, text: { text: "流程开始" } }
    });

    graph.addNode({
      id: WORKFLOW_END_NODE_ID,
      shape: "custom-node",
      x: 520,
      y: 120,
      data: { id: WORKFLOW_END_NODE_ID, name: "结束", stepType: "End", inputs: {} },
      attrs: { body: { fill: "#fff1f0", stroke: "#ff4d4f" }, header: { fill: "#ff4d4f" }, title: { text: "结束" }, text: { text: "流程结束" } }
    });
  }

  function handleDragStart(e: DragEvent, stepType: StepTypeMetadata) {
    if (!graphRef.value) return;
    e.dataTransfer!.effectAllowed = "move";
    e.dataTransfer!.setData("stepType", JSON.stringify(stepType));
  }

  function handleDrop(e: DragEvent) {
    if (!graphRef.value) return;
    e.preventDefault();
    const stepTypeStr = e.dataTransfer?.getData("stepType");
    if (!stepTypeStr) return;

    const stepType: StepTypeMetadata = JSON.parse(stepTypeStr);
    const point = graphRef.value.clientToLocal(e.clientX, e.clientY);
    const nodeId = `step_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;

    const inputs: Record<string, unknown> = {};
    stepType.parameters.forEach((param) => {
      if (param.defaultValue) {
        inputs[param.name] =
          param.defaultValue === "true" ? true : param.defaultValue === "false" ? false : param.defaultValue;
      }
    });

    graphRef.value.addNode({
      id: nodeId,
      shape: "custom-node",
      x: point.x - 60,
      y: point.y - 30,
      data: { id: nodeId, name: stepType.label, stepType: stepType.type, inputs },
      attrs: {
        header: { fill: stepType.color },
        title: { text: stepType.label },
        text: { text: `请选择${stepType.label}` }
      }
    });
  }

  return { initGraph, handleDragStart, handleDrop };
}
