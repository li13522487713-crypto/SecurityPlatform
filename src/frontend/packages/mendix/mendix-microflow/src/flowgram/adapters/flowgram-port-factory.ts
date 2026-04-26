import { defaultMicroflowObjectNodeRegistry, objectKindFromRegistryItem } from "../../node-registry";
import type { MicroflowEditorPort, MicroflowObjectKind, MicroflowPort } from "../../schema";

export interface FlowGramPortDescriptor {
  type: "input" | "output";
  portID: string;
  disabled?: boolean;
}

export function microflowPortToFlowGramPort(port: MicroflowEditorPort): FlowGramPortDescriptor {
  return {
    type: port.direction,
    portID: port.id,
    disabled: port.cardinality === "none",
  };
}

export function microflowPortsToFlowGramPorts(ports: MicroflowEditorPort[]): FlowGramPortDescriptor[] {
  return ports.map(microflowPortToFlowGramPort);
}

export function registryPortToFlowGramPort(port: MicroflowPort): FlowGramPortDescriptor {
  return {
    type: port.direction,
    portID: port.id,
    disabled: port.cardinality === "none",
  };
}

export function registryPortsToFlowGramPorts(ports: MicroflowPort[]): FlowGramPortDescriptor[] {
  return ports.map(registryPortToFlowGramPort);
}

export function flowGramPortsForObjectKind(kind: MicroflowObjectKind): FlowGramPortDescriptor[] {
  const registryItem = defaultMicroflowObjectNodeRegistry.find(item => objectKindFromRegistryItem(item) === kind);
  return registryItem ? registryPortsToFlowGramPorts(registryItem.ports) : [];
}

export function connectionIndexFromPortId(portId?: string): number {
  if (!portId) {
    return 0;
  }
  if (portId.endsWith(":error")) {
    return 1;
  }
  const match = portId.match(/:(?:case|type)-(\d+)$/);
  if (match?.[1]) {
    return Number(match[1]);
  }
  return 0;
}
