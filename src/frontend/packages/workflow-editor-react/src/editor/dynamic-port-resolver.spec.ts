import { describe, expect, it } from "vitest";
import type { NodePortsRuntime } from "./connection-rules";
import { resolveNodePorts } from "./dynamic-port-resolver";

const BASE_PORTS: NodePortsRuntime = {
  inputs: [{ key: "input", name: "input", direction: "input", dataType: "any", isRequired: false, maxConnections: 1 }],
  outputs: [{ key: "true", name: "true", direction: "output", dataType: "any", isRequired: false, maxConnections: 99 }]
};

describe("resolveNodePorts", () => {
  it("keeps non-selector ports unchanged", () => {
    const result = resolveNodePorts(
      {
        type: "TextProcessor",
        configs: {}
      },
      BASE_PORTS
    );

    expect(result.outputs.map((port) => port.key)).toEqual(["true"]);
  });

  it("generates selector true/false ports when no conditions", () => {
    const result = resolveNodePorts(
      {
        type: "Selector",
        configs: {}
      },
      BASE_PORTS
    );

    expect(result.outputs.map((port) => port.key)).toEqual(["true", "false"]);
  });

  it("generates dynamic ports from conditions", () => {
    const result = resolveNodePorts(
      {
        type: "Selector",
        configs: {
          conditions: [{ left: "{{a}}" }, { left: "{{b}}" }, { left: "{{c}}" }]
        }
      },
      BASE_PORTS
    );

    expect(result.outputs.map((port) => port.key)).toEqual(["true", "true_1", "true_2", "false"]);
  });
});
