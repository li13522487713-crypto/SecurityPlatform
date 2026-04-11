import type { NodePortsRuntime, PortRuntime } from "./connection-rules";
import { deriveSelectorOutputPortKeys } from "./selector-branches";

interface CanvasNodeLike {
  type: string;
  configs?: Record<string, unknown>;
}

function asObjectRecord(value: unknown): Record<string, unknown> {
  return value && typeof value === "object" && !Array.isArray(value) ? (value as Record<string, unknown>) : {};
}

function createOutputPort(template: PortRuntime, key: string): PortRuntime {
  return {
    ...template,
    key,
    name: key
  };
}

function resolveSelectorPorts(node: CanvasNodeLike, base: NodePortsRuntime): NodePortsRuntime {
  const configs = asObjectRecord(node.configs);
  const templateOutput = base.outputs[0] ?? {
    key: "true",
    name: "true",
    direction: "output" as const,
    dataType: "any",
    isRequired: false,
    maxConnections: 99
  };

  const portKeys = deriveSelectorOutputPortKeys(configs.conditions);
  const outputs = portKeys.map((key) => createOutputPort(templateOutput, key));

  return {
    inputs: base.inputs,
    outputs
  };
}

export function resolveNodePorts(node: CanvasNodeLike, base: NodePortsRuntime): NodePortsRuntime {
  if (node.type === "Selector") {
    return resolveSelectorPorts(node, base);
  }

  return base;
}
