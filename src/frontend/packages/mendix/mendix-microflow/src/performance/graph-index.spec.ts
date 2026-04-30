import { describe, expect, it } from "vitest";

import { createObjectFromRegistry, createSequenceFlow } from "../adapters";
import { defaultMicroflowNodeRegistry, getMicroflowNodeRegistryKey } from "../node-registry";
import { sampleMicroflowSchema, type MicroflowObject, type MicroflowSchema } from "../schema";
import { createMicroflowGraphIndex } from "./graph-index";

function registry(key: string) {
  const item = defaultMicroflowNodeRegistry.find(entry => getMicroflowNodeRegistryKey(entry) === key || entry.type === key);
  if (!item) {
    throw new Error(`Missing registry item ${key}`);
  }
  return item;
}

function schemaWith(objects: MicroflowObject[], flows: MicroflowSchema["flows"] = []): MicroflowSchema {
  return {
    ...sampleMicroflowSchema,
    objectCollection: { ...sampleMicroflowSchema.objectCollection, objects },
    flows,
    editor: { ...sampleMicroflowSchema.editor, selection: {} },
  };
}

describe("createMicroflowGraphIndex", () => {
  it("indexes objects, flows, and adjacency lists", () => {
    const a = createObjectFromRegistry(registry("activity:logMessage"), { x: 120, y: 120 });
    const b = createObjectFromRegistry(registry("activity:variableCreate"), { x: 260, y: 120 });
    const flow = createSequenceFlow({ originObjectId: a.id, destinationObjectId: b.id });

    const index = createMicroflowGraphIndex(schemaWith([a, b], [flow]));
    expect(index.objectsById.get(a.id)).toBeTruthy();
    expect(index.flowsById.get(flow.id)).toBeTruthy();
    expect(index.outgoingFlowIdsByObjectId.get(a.id)).toEqual([flow.id]);
    expect(index.incomingFlowIdsByObjectId.get(b.id)).toEqual([flow.id]);
  });
});
