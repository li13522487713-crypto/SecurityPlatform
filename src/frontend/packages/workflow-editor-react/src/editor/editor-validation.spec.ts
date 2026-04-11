import { describe, expect, it } from "vitest";
import { validateCanvas } from "./editor-validation";

describe("editor-validation", () => {
  const nodeTypes = [
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
        { key: "n2", type: "Llm", configs: { model: "gpt-5.4-medium" }, inputMappings: { input: "n1.output" } }
      ],
      [{ id: "c1", fromNode: "n1", fromPort: "output", toNode: "n2", toPort: "input", condition: null }],
      nodeTypes
    );
    expect(result.ok).toBe(true);
  });
});