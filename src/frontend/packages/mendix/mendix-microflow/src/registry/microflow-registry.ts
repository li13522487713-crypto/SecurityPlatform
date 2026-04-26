import {
  defaultMicroflowActionRegistry,
  defaultMicroflowEdgeRegistry,
  defaultMicroflowNodeRegistry,
  getMicroflowNodeRegistryKey,
  microflowActionRegistryByActivityType,
  microflowActionRegistryByKind,
  microflowNodeRegistryByKey,
  type MicroflowActionRegistryEntry,
  type MicroflowEdgeRegistryItem,
  type MicroflowFlowRegistryKind,
  type MicroflowNodeRegistryEntry
} from "../node-registry";
import type {
  MicroflowActionKind,
  MicroflowActivityType
} from "../schema";

export interface MicroflowRegistryBundle {
  nodes: readonly MicroflowNodeRegistryEntry[];
  actions: readonly MicroflowActionRegistryEntry[];
  edges: readonly MicroflowEdgeRegistryItem[];
}

export const defaultMicroflowRegistryBundle: MicroflowRegistryBundle = {
  nodes: defaultMicroflowNodeRegistry,
  actions: defaultMicroflowActionRegistry,
  edges: defaultMicroflowEdgeRegistry
};

export function findNodeRegistryEntry(registryKey: string): MicroflowNodeRegistryEntry | undefined {
  return microflowNodeRegistryByKey.get(registryKey);
}

export function findNodeRegistryEntryByType(type: MicroflowNodeRegistryEntry["type"], activityType?: MicroflowActivityType): MicroflowNodeRegistryEntry | undefined {
  const key = activityType ? `${type}:${activityType}` : type;
  return findNodeRegistryEntry(key);
}

export function findActionRegistryEntry(kind: MicroflowActionKind): MicroflowActionRegistryEntry | undefined {
  return microflowActionRegistryByKind.get(kind);
}

export function findActionRegistryEntryByActivityType(activityType: MicroflowActivityType): MicroflowActionRegistryEntry | undefined {
  return microflowActionRegistryByActivityType.get(activityType);
}

export function findEdgeRegistryEntry(kind: MicroflowFlowRegistryKind): MicroflowEdgeRegistryItem | undefined {
  return defaultMicroflowRegistryBundle.edges.find(item => item.kind === kind);
}

export function listNodeRegistryKeys(bundle: MicroflowRegistryBundle = defaultMicroflowRegistryBundle): string[] {
  return bundle.nodes.map(entry => getMicroflowNodeRegistryKey(entry));
}
