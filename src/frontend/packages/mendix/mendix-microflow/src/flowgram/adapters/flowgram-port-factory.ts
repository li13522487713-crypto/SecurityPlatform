import { defaultMicroflowObjectNodeRegistry, objectKindFromRegistryItem } from "../../node-registry";
import { microflowActionRegistryByKind } from "../../node-registry/action-registry";
import type { MicroflowActionKind, MicroflowEditorPort, MicroflowObjectKind, MicroflowPort } from "../../schema";
import { getConnectionIndexFromPortId } from "../../schema/utils/port-utils";

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

const actionSequencePorts: MicroflowPort[] = [
  { id: "in", label: "In", direction: "input", kind: "sequenceIn", cardinality: "one", edgeTypes: ["sequence", "decisionCondition", "objectTypeCondition", "errorHandler"] },
  { id: "out", label: "Out", direction: "output", kind: "sequenceOut", cardinality: "one", edgeTypes: ["sequence"] },
];

const actionErrorPort: MicroflowPort = {
  id: "error",
  label: "Error",
  direction: "output",
  kind: "errorOut",
  cardinality: "zeroOrOne",
  edgeTypes: ["errorHandler"],
};

export function flowGramPortsForActionKind(actionKind?: MicroflowActionKind): FlowGramPortDescriptor[] {
  const registryItem = actionKind ? microflowActionRegistryByKind.get(actionKind) : undefined;
  const supportsErrorHandling = registryItem?.supportsErrorHandling ?? true;
  return registryPortsToFlowGramPorts(supportsErrorHandling
    ? [...actionSequencePorts, actionErrorPort]
    : actionSequencePorts);
}

export function flowGramPortsForObjectKind(kind: MicroflowObjectKind, actionKind?: MicroflowActionKind): FlowGramPortDescriptor[] {
  if (kind === "actionActivity") {
    return flowGramPortsForActionKind(actionKind);
  }
  const registryItem = defaultMicroflowObjectNodeRegistry.find(item => objectKindFromRegistryItem(item) === kind);
  return registryItem ? registryPortsToFlowGramPorts(registryItem.ports) : [];
}

export function connectionIndexFromPortId(portId?: string): number {
  return getConnectionIndexFromPortId(portId);
}
