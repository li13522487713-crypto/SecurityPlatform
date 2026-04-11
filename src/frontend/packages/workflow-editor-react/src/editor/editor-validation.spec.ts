import { describe, expect, it } from "vitest";
import { validateCanvas } from "./editor-validation";
import type { NodeTypeMetadata } from "../types";

describe("editor-validation", () => {
  const nodeTypes: NodeTypeMetadata[] = [
    {
      key: "Entry",
      name: "开始",
      category: "flow",
      description: "",
      configSchemaJson: JSON.stringify({ type: "object", required: ["entryVariable"], properties: { entryVariable: { type: "string", minLength: 1 } } }),
      ports: [
        { key: "output", name: "output", direction: "Output", dataType: "string", isRequired: true, maxConnections: 3 }
      ]
    },
    {
      key: "Exit",
      name: "结束",
      category: "flow",
      description: "",
      ports: [
        { key: "input", name: "input", direction: "Input", dataType: "any", isRequired: false, maxConnections: 10 }
      ]
    },
    {
      key: "Llm",
      name: "大模型",
      category: "ai",
      description: "",
      configSchemaJson: JSON.stringify({ type: "object", required: ["model"], properties: { model: { type: "string", minLength: 1 } } }),
      ports: [
        { key: "input", name: "input", direction: "Input", dataType: "string", isRequired: true, maxConnections: 1 },
        { key: "output", name: "output", direction: "Output", dataType: "string", isRequired: true, maxConnections: 2 }
      ]
    }
  ];

  it("发现字段错误、inputMappings 错误和连线端口错误", () => {
    const result = validateCanvas(
      [
        { key: "n1", type: "Entry", configs: {}, inputMappings: {} },
        { key: "n2", type: "Llm", configs: {}, inputMappings: { badPort: "n1.output" } }
      ],
      [
        { id: "c1", fromNode: "n1", fromPort: "badOut", toNode: "n2", toPort: "input", condition: null }
      ],
      nodeTypes
    );
    expect(result.ok).toBe(false);
    expect(result.nodeResults.some((item) => item.nodeKey === "n1" && item.issues.length > 0)).toBe(true);
    expect(result.nodeResults.some((item) => item.nodeKey === "n2" && item.issues.some((x) => x.includes("inputMappings")))).toBe(true);
    expect(result.canvasIssues.length).toBeGreaterThan(0);
  });

  it("合法画布通过", () => {
    const result = validateCanvas(
      [
        { key: "n1", type: "Entry", configs: { entryVariable: "USER_INPUT" }, inputMappings: {} },
        { key: "n2", type: "Llm", configs: { model: "gpt-5.4-medium" }, inputMappings: { input: "n1.output" } },
        { key: "n3", type: "Exit", configs: {}, inputMappings: {} }
      ],
      [
        { id: "c1", fromNode: "n1", fromPort: "output", toNode: "n2", toPort: "input", condition: null },
        { id: "c2", fromNode: "n2", fromPort: "output", toNode: "n3", toPort: "input", condition: null }
      ],
      nodeTypes
    );
    expect(result.ok).toBe(true);
  });

  it("引用下游节点变量应报错", () => {
    const result = validateCanvas(
      [
        { key: "n1", type: "Entry", configs: { entryVariable: "USER_INPUT" }, inputMappings: {} },
        { key: "n2", type: "Llm", configs: { model: "gpt-5.4-medium", prompt: "{{n3.output}}" }, inputMappings: {} },
        { key: "n3", type: "Llm", configs: { model: "gpt-5.4-medium" }, inputMappings: {} },
        { key: "exit_1", type: "Exit", configs: {}, inputMappings: {} }
      ],
      [
        { id: "c1", fromNode: "n1", fromPort: "output", toNode: "n2", toPort: "input", condition: null },
        { id: "c2", fromNode: "n2", fromPort: "output", toNode: "n3", toPort: "input", condition: null },
        { id: "c3", fromNode: "n3", fromPort: "output", toNode: "exit_1", toPort: "input", condition: null }
      ],
      nodeTypes
    );

    expect(result.ok).toBe(false);
    expect(result.nodeResults.some((item) => item.nodeKey === "n2" && item.issues.some((x) => x.includes("不是当前节点的上游")))).toBe(true);
  });

  it("引用不存在的全局变量应报错", () => {
    const result = validateCanvas(
      [{ key: "n1", type: "Entry", configs: { entryVariable: "{{global.missingKey}}" }, inputMappings: {} }],
      [],
      nodeTypes,
      { existingKey: "ok" }
    );

    expect(result.ok).toBe(false);
    expect(result.nodeResults.some((item) => item.issues.some((x) => x.includes("global")))).toBe(true);
  });

  // ─── VL-01: 图结构完整性校验 ──────────────────────────────────────────────

  it("VL-01: 缺少 Entry 节点应报 canvasIssues 错误", () => {
    const result = validateCanvas(
      [{ key: "llm_1", type: "Llm", configs: { model: "gpt-4" }, inputMappings: {} }],
      [],
      nodeTypes
    );
    expect(result.ok).toBe(false);
    expect(result.canvasIssues.some((msg) => msg.includes("开始节点"))).toBe(true);
  });

  it("VL-01: 缺少 Exit 节点应报 canvasIssues 错误", () => {
    const exitNodeTypes: NodeTypeMetadata[] = [
      ...nodeTypes,
      { key: "Exit", name: "结束", category: "flow", description: "", ports: [] }
    ];
    const result = validateCanvas(
      [{ key: "n1", type: "Entry", configs: { entryVariable: "x" }, inputMappings: {} }],
      [],
      exitNodeTypes
    );
    expect(result.ok).toBe(false);
    expect(result.canvasIssues.some((msg) => msg.includes("结束节点"))).toBe(true);
  });

  it("VL-01: 多个 Entry 节点应报错", () => {
    const result = validateCanvas(
      [
        { key: "e1", type: "Entry", configs: { entryVariable: "x" }, inputMappings: {} },
        { key: "e2", type: "Entry", configs: { entryVariable: "y" }, inputMappings: {} }
      ],
      [],
      nodeTypes
    );
    expect(result.ok).toBe(false);
    expect(result.canvasIssues.some((msg) => msg.includes("多个开始节点"))).toBe(true);
  });

  it("VL-01: 孤立节点（无入无出边）应报错", () => {
    const allTypes: NodeTypeMetadata[] = [
      ...nodeTypes,
      { key: "Exit", name: "结束", category: "flow", description: "", ports: [] }
    ];
    const result = validateCanvas(
      [
        { key: "entry_1", type: "Entry", configs: { entryVariable: "x" }, inputMappings: {} },
        { key: "llm_orphan", type: "Llm", configs: { model: "gpt-4" }, inputMappings: {} },
        { key: "exit_1", type: "Exit", configs: {}, inputMappings: {} }
      ],
      [{ id: "c1", fromNode: "entry_1", fromPort: "output", toNode: "exit_1", toPort: "input", condition: null }],
      allTypes
    );
    expect(result.ok).toBe(false);
    expect(result.canvasIssues.some((msg) => msg.includes("孤立节点"))).toBe(true);
  });

  it("VL-01: 从 Entry 不可达的节点应报错", () => {
    const allTypes: NodeTypeMetadata[] = [
      ...nodeTypes,
      { key: "Exit", name: "结束", category: "flow", description: "", ports: [] }
    ];
    const result = validateCanvas(
      [
        { key: "entry_1", type: "Entry", configs: { entryVariable: "x" }, inputMappings: {} },
        { key: "llm_1", type: "Llm", configs: { model: "gpt-4" }, inputMappings: {} },
        { key: "llm_unreachable", type: "Llm", configs: { model: "gpt-4" }, inputMappings: {} },
        { key: "exit_1", type: "Exit", configs: {}, inputMappings: {} }
      ],
      [
        { id: "c1", fromNode: "entry_1", fromPort: "output", toNode: "llm_1", toPort: "input", condition: null },
        { id: "c2", fromNode: "llm_1", fromPort: "output", toNode: "exit_1", toPort: "input", condition: null },
        // llm_unreachable 有入边（来自不在图中的节点）但从 entry_1 不可达
        { id: "c3", fromNode: "llm_unreachable", fromPort: "output", toNode: "exit_1", toPort: "input", condition: null }
      ],
      allTypes
    );
    expect(result.ok).toBe(false);
    expect(result.canvasIssues.some((msg) => msg.includes("不可达"))).toBe(true);
  });

  // ─── VL-02: 重复 key 检测 ──────────────────────────────────────────────────

  it("VL-02: 重复节点 key 应报错", () => {
    const result = validateCanvas(
      [
        { key: "n1", type: "Entry", configs: { entryVariable: "x" }, inputMappings: {} },
        { key: "n1", type: "Llm", configs: { model: "gpt-4" }, inputMappings: {} }
      ],
      [],
      nodeTypes
    );
    expect(result.ok).toBe(false);
    expect(result.canvasIssues.some((msg) => msg.includes("重复节点标识"))).toBe(true);
  });
});