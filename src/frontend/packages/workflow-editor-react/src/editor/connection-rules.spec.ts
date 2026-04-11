import { describe, expect, it } from "vitest";
import { buildNodePortsRuntime, validateConnectionCandidate, type ConnectionRuntime } from "./connection-rules";

describe("connection-rules", () => {
  const sourcePorts = buildNodePortsRuntime({
    key: "Llm",
    name: "LLM",
    category: "ai",
    description: "",
    ports: [{ key: "result", name: "result", direction: "Output", dataType: "string", isRequired: true, maxConnections: 1 }]
  }).outputs;

  const targetPorts = buildNodePortsRuntime({
    key: "Exit",
    name: "Exit",
    category: "flow",
    description: "",
    ports: [{ key: "input", name: "input", direction: "Input", dataType: "string", isRequired: true, maxConnections: 1 }]
  }).inputs;

  it("允许 output -> input 的合法连线", () => {
    const result = validateConnectionCandidate(
      { fromNode: "n1", fromPort: "result", toNode: "n2", toPort: "input" },
      [],
      sourcePorts,
      targetPorts
    );
    expect(result.ok).toBe(true);
    expect(result.code).toBe("OK");
  });

  it("拦截重复连线", () => {
    const existing: ConnectionRuntime[] = [
      { id: "c1", fromNode: "n1", fromPort: "result", toNode: "n2", toPort: "input", condition: null }
    ];
    const result = validateConnectionCandidate(
      { fromNode: "n1", fromPort: "result", toNode: "n2", toPort: "input" },
      existing,
      sourcePorts,
      targetPorts
    );
    expect(result.ok).toBe(false);
    expect(result.code).toBe("DUPLICATE");
  });

  it("拦截端口连接上限", () => {
    const existing: ConnectionRuntime[] = [
      { id: "c1", fromNode: "n1", fromPort: "result", toNode: "n2", toPort: "input", condition: null }
    ];
    const result = validateConnectionCandidate(
      { fromNode: "n1", fromPort: "result", toNode: "n3", toPort: "input" },
      existing,
      sourcePorts,
      targetPorts
    );
    expect(result.ok).toBe(false);
    expect(result.code).toBe("MAX_CONNECTIONS");
  });

  it("拦截类型不兼容", () => {
    const numberTarget = buildNodePortsRuntime({
      key: "Exit",
      name: "Exit",
      category: "flow",
      description: "",
      ports: [{ key: "inNum", name: "inNum", direction: "Input", dataType: "number", isRequired: true, maxConnections: 2 }]
    }).inputs;
    const result = validateConnectionCandidate(
      { fromNode: "n1", fromPort: "result", toNode: "n2", toPort: "inNum" },
      [],
      sourcePorts,
      numberTarget
    );
    expect(result.ok).toBe(false);
    expect(result.code).toBe("TYPE_MISMATCH");
  });
});