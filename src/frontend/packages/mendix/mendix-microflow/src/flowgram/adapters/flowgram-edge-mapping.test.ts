import { describe, expect, it } from "vitest";

import { createObjectFromRegistry, toEditorGraph } from "../../adapters";
import { sampleMicroflowSchema } from "../../schema/sample";
import { defaultMicroflowNodeRegistry, getMicroflowNodeRegistryKey } from "../../node-registry";
import type { MicroflowObject, MicroflowSchema } from "../../schema";
import { mapFlowGramEdgeToMicroflowFlow } from "./flowgram-edge-mapping";

function registry(key: string) {
  const item = defaultMicroflowNodeRegistry.find(entry => getMicroflowNodeRegistryKey(entry) === key || entry.type === key);
  if (!item) {
    throw new Error(`Missing registry item ${key}`);
  }
  return item;
}

function schemaWith(objects: MicroflowObject[]): MicroflowSchema {
  return {
    ...sampleMicroflowSchema,
    id: "MF_FLOWGRAM_EDGE_MAPPING_TEST",
    stableId: "MF_FLOWGRAM_EDGE_MAPPING_TEST",
    parameters: [],
    objectCollection: { ...sampleMicroflowSchema.objectCollection, id: "root", objects, flows: [] },
    flows: [],
    editor: { ...sampleMicroflowSchema.editor, selection: {} },
  };
}

describe("flowgram edge mapping", () => {
  it("does not let empty FlowGram caseValues override boolean branch defaults", () => {
    const decision = createObjectFromRegistry(registry("decision"), { x: 0, y: 0 }, "decision");
    const target = createObjectFromRegistry(registry("activity:logMessage"), { x: 220, y: 0 }, "target");
    const schema = schemaWith([decision, target]);
    const graph = toEditorGraph(schema);
    const sourcePort = graph.nodes
      .find(node => node.objectId === decision.id)
      ?.ports.find(port => port.kind === "decisionOut" && port.label.toLowerCase() === "true");
    const targetPort = graph.nodes
      .find(node => node.objectId === target.id)
      ?.ports.find(port => port.direction === "input" && port.kind === "sequenceIn");

    if (!sourcePort || !targetPort) {
      throw new Error("Expected decision true source port and target incoming port.");
    }

    const flow = mapFlowGramEdgeToMicroflowFlow(schema, {
      sourceNodeID: decision.id,
      targetNodeID: target.id,
      sourcePortID: sourcePort.id,
      targetPortID: targetPort.id,
      data: { caseValues: [] },
    });

    expect(flow?.kind).toBe("sequence");
    expect(flow?.kind === "sequence" ? flow.editor.edgeKind : undefined).toBe("decisionCondition");
    expect(flow?.kind === "sequence" ? flow.caseValues : []).toEqual([
      expect.objectContaining({ kind: "boolean", value: true, persistedValue: "true" }),
    ]);
  });
});

