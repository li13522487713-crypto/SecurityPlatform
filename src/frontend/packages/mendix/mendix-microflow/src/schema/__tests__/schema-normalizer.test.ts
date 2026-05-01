import { describe, expect, it } from "vitest";

import { createObjectFromRegistry, createSequenceFlow } from "../../adapters";
import { defaultMicroflowNodeRegistry, getMicroflowNodeRegistryKey } from "../../node-registry";
import {
  collectFlowsRecursive,
  normalizeMicroflowAuthoringSchemaForRuntime,
  sampleMicroflowSchema,
  type MicroflowObject,
  type MicroflowSchema,
} from "../index";

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
    id: "MF_NORMALIZER_TEST",
    stableId: "MF_NORMALIZER_TEST",
    parameters: [],
    objectCollection: { ...sampleMicroflowSchema.objectCollection, id: "root", objects, flows: [] },
    flows,
    editor: { ...sampleMicroflowSchema.editor, selection: {} },
  };
}

describe("microflow schema runtime normalizer", () => {
  it("repairs empty boolean Decision caseValues in branch order", () => {
    const decision = createObjectFromRegistry(registry("decision"), { x: 0, y: 0 }, "decision");
    const trueTarget = createObjectFromRegistry(registry("activity:objectChange"), { x: 200, y: -80 }, "true-target");
    const falseTarget = createObjectFromRegistry(registry("activity:logMessage"), { x: 200, y: 80 }, "false-target");
    const first = createSequenceFlow({
      id: "flow-first",
      originObjectId: decision.id,
      destinationObjectId: trueTarget.id,
      edgeKind: "sequence",
      originConnectionIndex: 1,
      caseValues: [],
    });
    const second = createSequenceFlow({
      id: "flow-second",
      originObjectId: decision.id,
      destinationObjectId: falseTarget.id,
      edgeKind: "sequence",
      originConnectionIndex: 2,
      caseValues: [],
    });

    const result = normalizeMicroflowAuthoringSchemaForRuntime(schemaWith([decision, trueTarget, falseTarget], [first, second]));
    const flows = result.schema.flows.filter((flow): flow is Extract<typeof flow, { kind: "sequence" }> => flow.kind === "sequence");

    expect(result.report.blockingIssues).toEqual([]);
    expect(flows.find(flow => flow.id === first.id)?.editor.edgeKind).toBe("decisionCondition");
    expect(flows.find(flow => flow.id === first.id)?.caseValues[0]).toMatchObject({ kind: "boolean", value: true });
    expect(flows.find(flow => flow.id === second.id)?.caseValues[0]).toMatchObject({ kind: "boolean", value: false });
  });

  it("moves same-loop body flows out of root flows into the loop collection", () => {
    const loop = createObjectFromRegistry(registry("loop"), { x: 200, y: 120 }, "loop");
    const first = createObjectFromRegistry(registry("activity:objectChange"), { x: 180, y: 180 }, "loop-first");
    const second = createObjectFromRegistry(registry("activity:logMessage"), { x: 420, y: 180 }, "loop-second");
    if (loop.kind !== "loopedActivity") {
      throw new Error("Expected loopedActivity.");
    }
    const misplacedFlow = createSequenceFlow({ id: "misplaced-loop-flow", originObjectId: first.id, destinationObjectId: second.id });
    const schema = schemaWith([{ ...loop, objectCollection: { ...loop.objectCollection, id: "loop-body", objects: [first, second], flows: [] } }], [misplacedFlow]);

    const result = normalizeMicroflowAuthoringSchemaForRuntime(schema);
    const normalizedLoop = result.schema.objectCollection.objects.find(object => object.id === loop.id);

    expect(result.report.blockingIssues).toEqual([]);
    expect(result.schema.flows.some(flow => flow.id === misplacedFlow.id)).toBe(false);
    expect(normalizedLoop?.kind === "loopedActivity" ? normalizedLoop.objectCollection.flows?.some(flow => flow.id === misplacedFlow.id) : false).toBe(true);
    expect(result.report.changes.some(change => change.type === "flowCollectionRepair" && change.flowId === misplacedFlow.id)).toBe(true);
  });

  it("reports illegal cross collection sequence flows without hiding the invalid schema", () => {
    const loop = createObjectFromRegistry(registry("loop"), { x: 200, y: 120 }, "loop");
    const rootAction = createObjectFromRegistry(registry("activity:objectRetrieve"), { x: 0, y: 120 }, "root-action");
    const loopAction = createObjectFromRegistry(registry("activity:objectChange"), { x: 220, y: 180 }, "loop-action");
    if (loop.kind !== "loopedActivity") {
      throw new Error("Expected loopedActivity.");
    }
    const invalid = createSequenceFlow({ id: "invalid-cross-flow", originObjectId: rootAction.id, destinationObjectId: loopAction.id });
    const schema = schemaWith([{ ...loop, objectCollection: { ...loop.objectCollection, id: "loop-body", objects: [loopAction], flows: [] } }, rootAction], [invalid]);

    const result = normalizeMicroflowAuthoringSchemaForRuntime(schema);

    expect(result.report.blockingIssues).toContainEqual(expect.objectContaining({ code: "MF_FLOW_INVALID_TARGET", flowId: invalid.id }));
    expect(collectFlowsRecursive(result.schema).some(flow => flow.id === invalid.id)).toBe(true);
  });
});
