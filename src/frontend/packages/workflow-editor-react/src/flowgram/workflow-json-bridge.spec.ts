import { describe, expect, it } from "vitest";
import { toFlowgramWorkflowJSON, toEditorCanvasSchema } from "./workflow-json-bridge";
import type { CanvasSchema, NodeSchema } from "../types/workflow-v2";
import type { NodeTypeMetadata } from "../types";
import type { WorkflowJSON } from "@flowgram.ai/free-layout-editor";

describe("workflow-json-bridge", () => {
  // ─── Test fixtures ─────────────────────────────────────────────────────────

  const nodeTypesMeta: NodeTypeMetadata[] = [
    {
      key: "Entry",
      name: "开始",
      category: "flow",
      description: "",
      ports: [{ key: "output", name: "output", direction: "Output", dataType: "any", isRequired: true, maxConnections: 10 }]
    },
    {
      key: "Exit",
      name: "结束",
      category: "flow",
      description: "",
      ports: [{ key: "input", name: "input", direction: "Input", dataType: "any", isRequired: false, maxConnections: 10 }]
    },
    {
      key: "Llm",
      name: "大模型",
      category: "ai",
      description: "",
      ports: [
        { key: "input", name: "input", direction: "Input", dataType: "any", isRequired: false, maxConnections: 1 },
        { key: "output", name: "output", direction: "Output", dataType: "any", isRequired: false, maxConnections: 5 }
      ]
    }
  ];

  const nodeTypesMap = new Map(nodeTypesMeta.map((m) => [m.key, m]));

  function makeNode(key: string, type: string): NodeSchema {
    return {
      key,
      type,
      title: type,
      layout: { x: 100, y: 100, width: 360, height: 160 },
      configs: {},
      inputMappings: {}
    };
  }

  const baseCanvas: CanvasSchema = {
    nodes: [
      makeNode("entry_1", "Entry"),
      makeNode("llm_1", "Llm"),
      makeNode("exit_1", "Exit")
    ],
    connections: [
      { fromNode: "entry_1", fromPort: "output", toNode: "llm_1", toPort: "input", condition: null },
      { fromNode: "llm_1", fromPort: "output", toNode: "exit_1", toPort: "input", condition: null }
    ],
    schemaVersion: 2
  };

  // ─── toFlowgramWorkflowJSON ────────────────────────────────────────────────

  it("toFlowgramWorkflowJSON: 节点数量应与 canvas 一致", () => {
    const json = toFlowgramWorkflowJSON(baseCanvas, nodeTypesMap);
    expect(json.nodes).toHaveLength(3);
    expect(json.edges).toHaveLength(2);
  });

  it("toFlowgramWorkflowJSON: 节点 id 应等于 canvas node.key", () => {
    const json = toFlowgramWorkflowJSON(baseCanvas, nodeTypesMap);
    const nodeIds = json.nodes.map((n) => n.id);
    expect(nodeIds).toContain("entry_1");
    expect(nodeIds).toContain("llm_1");
    expect(nodeIds).toContain("exit_1");
  });

  it("toFlowgramWorkflowJSON: 边应保留 source/target 节点 ID 和端口", () => {
    const json = toFlowgramWorkflowJSON(baseCanvas, nodeTypesMap);
    const edge = json.edges.find((e) => e.sourceNodeID === "entry_1");
    expect(edge).toBeDefined();
    expect(edge?.targetNodeID).toBe("llm_1");
    expect(edge?.sourcePortID).toBe("output");
    expect(edge?.targetPortID).toBe("input");
  });

  it("toFlowgramWorkflowJSON: 节点 meta.position 应反映 layout 坐标", () => {
    const json = toFlowgramWorkflowJSON(baseCanvas, nodeTypesMap);
    const node = json.nodes.find((n) => n.id === "entry_1");
    expect(node?.meta?.position?.x).toBe(100);
    expect(node?.meta?.position?.y).toBe(100);
  });

  it("toFlowgramWorkflowJSON: 节点端口从 nodeTypes meta 解析", () => {
    const json = toFlowgramWorkflowJSON(baseCanvas, nodeTypesMap);
    const entryNode = json.nodes.find((n) => n.id === "entry_1")!;
    const ports = entryNode.meta?.defaultPorts ?? [];
    expect(ports.some((p) => p.portID === "output" && p.type === "output")).toBe(true);
  });

  it("toFlowgramWorkflowJSON: 带条件的边应把 condition 存入 edge.data", () => {
    const canvasWithCondition: CanvasSchema = {
      ...baseCanvas,
      connections: [
        { fromNode: "entry_1", fromPort: "true", toNode: "llm_1", toPort: "input", condition: "flag == true" }
      ]
    };
    const json = toFlowgramWorkflowJSON(canvasWithCondition, nodeTypesMap);
    const edge = json.edges[0];
    const data = edge.data as { condition?: string } | undefined;
    expect(data?.condition).toBe("flag == true");
  });

  // ─── toEditorCanvasSchema ─────────────────────────────────────────────────

  it("toEditorCanvasSchema: 节点数量应还原一致", () => {
    const json = toFlowgramWorkflowJSON(baseCanvas, nodeTypesMap);
    const restored = toEditorCanvasSchema(json, baseCanvas);
    expect(restored.nodes).toHaveLength(3);
    expect(restored.connections).toHaveLength(2);
  });

  it("toEditorCanvasSchema: 节点 key 应正确还原", () => {
    const json = toFlowgramWorkflowJSON(baseCanvas, nodeTypesMap);
    const restored = toEditorCanvasSchema(json, baseCanvas);
    const keys = restored.nodes.map((n) => n.key);
    expect(keys).toContain("entry_1");
    expect(keys).toContain("llm_1");
    expect(keys).toContain("exit_1");
  });

  it("toEditorCanvasSchema: 边的 fromNode/toNode 应正确还原", () => {
    const json = toFlowgramWorkflowJSON(baseCanvas, nodeTypesMap);
    const restored = toEditorCanvasSchema(json, baseCanvas);
    const edge = restored.connections.find((c) => c.fromNode === "entry_1");
    expect(edge).toBeDefined();
    expect(edge?.toNode).toBe("llm_1");
    expect(edge?.fromPort).toBe("output");
  });

  it("toEditorCanvasSchema: schemaVersion 应从前一个 canvas 继承", () => {
    const json = toFlowgramWorkflowJSON(baseCanvas, nodeTypesMap);
    const restored = toEditorCanvasSchema(json, baseCanvas);
    expect(restored.schemaVersion).toBe(2);
  });

  // ─── Roundtrip ────────────────────────────────────────────────────────────

  it("往返（canvas → flowgram → canvas）节点 key/type 不丢失", () => {
    const json = toFlowgramWorkflowJSON(baseCanvas, nodeTypesMap);
    const restored = toEditorCanvasSchema(json, baseCanvas);

    for (const original of baseCanvas.nodes) {
      const found = restored.nodes.find((n) => n.key === original.key);
      expect(found).toBeDefined();
      expect(found?.type).toBe(original.type);
    }
  });

  it("往返：连接条件不丢失", () => {
    const canvasWithCondition: CanvasSchema = {
      ...baseCanvas,
      connections: [
        { fromNode: "entry_1", fromPort: "true", toNode: "llm_1", toPort: "input", condition: "x > 5" }
      ]
    };
    const json = toFlowgramWorkflowJSON(canvasWithCondition, nodeTypesMap);
    const restored = toEditorCanvasSchema(json, canvasWithCondition);
    expect(restored.connections[0].condition).toBe("x > 5");
  });

  it("往返：没有条件的边 condition 应为 null", () => {
    const json = toFlowgramWorkflowJSON(baseCanvas, nodeTypesMap);
    const restored = toEditorCanvasSchema(json, baseCanvas);
    const edge = restored.connections.find((c) => c.fromNode === "entry_1")!;
    expect(edge.condition).toBeNull();
  });

  it("往返：空画布不报错", () => {
    const emptyCanvas: CanvasSchema = { nodes: [], connections: [], schemaVersion: 2 };
    const json = toFlowgramWorkflowJSON(emptyCanvas, nodeTypesMap);
    const restored = toEditorCanvasSchema(json, emptyCanvas);
    expect(restored.nodes).toHaveLength(0);
    expect(restored.connections).toHaveLength(0);
  });
});
