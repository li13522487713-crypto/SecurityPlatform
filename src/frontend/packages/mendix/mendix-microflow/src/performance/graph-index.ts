import type { MicroflowAuthoringSchema, MicroflowFlow, MicroflowObject } from "../schema";
import { collectFlowsRecursive, collectObjectsRecursive } from "../schema/utils/object-utils";

export interface MicroflowGraphIndex {
  objectsById: Map<string, MicroflowObject>;
  flowsById: Map<string, MicroflowFlow>;
  outgoingFlowIdsByObjectId: Map<string, string[]>;
  incomingFlowIdsByObjectId: Map<string, string[]>;
}

export function createMicroflowGraphIndex(schema: Pick<MicroflowAuthoringSchema, "objectCollection" | "flows">): MicroflowGraphIndex {
  const objectsById = new Map<string, MicroflowObject>();
  const flowsById = new Map<string, MicroflowFlow>();
  const outgoingFlowIdsByObjectId = new Map<string, string[]>();
  const incomingFlowIdsByObjectId = new Map<string, string[]>();

  for (const object of collectObjectsRecursive(schema.objectCollection)) {
    objectsById.set(object.id, object);
  }

  for (const flow of collectFlowsRecursive(schema)) {
    flowsById.set(flow.id, flow);
    const outgoing = outgoingFlowIdsByObjectId.get(flow.originObjectId) ?? [];
    outgoing.push(flow.id);
    outgoingFlowIdsByObjectId.set(flow.originObjectId, outgoing);

    const incoming = incomingFlowIdsByObjectId.get(flow.destinationObjectId) ?? [];
    incoming.push(flow.id);
    incomingFlowIdsByObjectId.set(flow.destinationObjectId, incoming);
  }

  return {
    objectsById,
    flowsById,
    outgoingFlowIdsByObjectId,
    incomingFlowIdsByObjectId,
  };
}
