import { describe, expect, it } from "vitest";

import { createObjectFromRegistry, createSequenceFlow, deleteFlow, deleteObject } from "../../adapters";
import { defaultMicroflowNodeRegistry, getMicroflowNodeRegistryKey } from "../../node-registry";
import {
  assignDecisionBooleanCase,
  createBooleanCaseValue,
  getDecisionBranchConflicts,
  getMergeFlowSummary,
  releaseDecisionBranchCase,
  sampleMicroflowSchema,
  updateDecisionExpression,
  updateFlowLabel,
  updateMergeBehavior,
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
    id: "MF_DECISION_TEST",
    stableId: "MF_DECISION_TEST",
    parameters: [],
    objectCollection: { ...sampleMicroflowSchema.objectCollection, objects },
    flows,
    editor: { ...sampleMicroflowSchema.editor, selection: {} },
  };
}

function decisionObject(id = "decision") {
  const object = createObjectFromRegistry(registry("decision"), { x: 0, y: 0 }, id);
  if (object.kind !== "exclusiveSplit") {
    throw new Error("Expected ExclusiveSplit.");
  }
  return object;
}

function mergeObject(id = "merge") {
  const object = createObjectFromRegistry(registry("merge"), { x: 300, y: 0 }, id);
  if (object.kind !== "exclusiveMerge") {
    throw new Error("Expected ExclusiveMerge.");
  }
  return object;
}

describe("decision and merge branch helpers", () => {
  it("updates Decision expression in schema", () => {
    const decision = decisionObject();
    const updated = updateDecisionExpression(schemaWith([decision]), decision.id, { raw: "totalAmount > 100", inferredType: { kind: "boolean" } });
    const nextDecision = updated.objectCollection.objects[0];

    expect(nextDecision?.kind === "exclusiveSplit" && nextDecision.splitCondition.kind === "expression" ? nextDecision.splitCondition.expression.raw : undefined).toBe("totalAmount > 100");
  });

  it("assigns true and false branch cases and labels", () => {
    const decision = decisionObject();
    const merge = mergeObject();
    const trueFlow = createSequenceFlow({ originObjectId: decision.id, destinationObjectId: merge.id, caseValues: [createBooleanCaseValue(true)], edgeKind: "decisionCondition" });
    const falseFlow = createSequenceFlow({ originObjectId: decision.id, destinationObjectId: merge.id, caseValues: [createBooleanCaseValue(false)], edgeKind: "decisionCondition", originConnectionIndex: 1 });
    const schema = schemaWith([decision, merge], [trueFlow, falseFlow]);
    const updatedTrue = assignDecisionBooleanCase(schema, trueFlow.id, true);
    const updatedFalse = assignDecisionBooleanCase(updatedTrue, falseFlow.id, false);

    expect(updatedFalse.flows[0]?.kind === "sequence" ? updatedFalse.flows[0].caseValues[0] : undefined).toMatchObject({ kind: "boolean", value: true });
    expect(updatedFalse.flows[1]?.kind === "sequence" ? updatedFalse.flows[1].caseValues[0] : undefined).toMatchObject({ kind: "boolean", value: false });
    expect(updatedFalse.flows[0]?.editor.label).toBe("是");
    expect(updatedFalse.flows[1]?.editor.label).toBe("否");
  });

  it("detects duplicate boolean branch cases", () => {
    const decision = decisionObject();
    const merge = mergeObject();
    const first = createSequenceFlow({ originObjectId: decision.id, destinationObjectId: merge.id, caseValues: [createBooleanCaseValue(true)], edgeKind: "decisionCondition" });
    const second = createSequenceFlow({ originObjectId: decision.id, destinationObjectId: merge.id, caseValues: [createBooleanCaseValue(true)], edgeKind: "decisionCondition", originConnectionIndex: 1 });
    const schema = schemaWith([decision, merge], [first, second]);

    expect(getDecisionBranchConflicts(schema, decision.id)).toEqual([{ key: "boolean:true", flowIds: [first.id, second.id] }]);
  });

  it("releases a branch case and deleting a flow removes branch metadata", () => {
    const decision = decisionObject();
    const merge = mergeObject();
    const flow = createSequenceFlow({ originObjectId: decision.id, destinationObjectId: merge.id, caseValues: [createBooleanCaseValue(true)], edgeKind: "decisionCondition" });
    const schema = schemaWith([decision, merge], [flow]);
    const released = releaseDecisionBranchCase(schema, flow.id);
    const deleted = deleteFlow(schema, flow.id);

    expect(released.flows[0]?.kind === "sequence" ? released.flows[0].caseValues[0] : undefined).toMatchObject({ kind: "noCase" });
    expect(deleted.flows).toHaveLength(0);
    expect(getDecisionBranchConflicts(deleted, decision.id)).toEqual([]);
  });

  it("updates flow label and merge behavior", () => {
    const decision = decisionObject();
    const merge = mergeObject();
    const flow = createSequenceFlow({ originObjectId: decision.id, destinationObjectId: merge.id, caseValues: [createBooleanCaseValue(true)], edgeKind: "decisionCondition" });
    const labelled = updateFlowLabel(schemaWith([decision, merge], [flow]), flow.id, "High Amount");
    const merged = updateMergeBehavior(labelled, merge.id, "firstArrived");

    expect(merged.flows[0]?.editor.label).toBe("High Amount");
    expect(merged.objectCollection.objects.find(object => object.id === merge.id)?.kind === "exclusiveMerge"
      ? (merged.objectCollection.objects.find(object => object.id === merge.id) as typeof merge).mergeBehavior.strategy
      : undefined).toBe("firstArrived");
  });

  it("summarizes Merge flows and deleting Merge removes related flows", () => {
    const decision = decisionObject();
    const merge = mergeObject();
    const end = createObjectFromRegistry(registry("endEvent"), { x: 520, y: 0 }, "end");
    const first = createSequenceFlow({ originObjectId: decision.id, destinationObjectId: merge.id, caseValues: [createBooleanCaseValue(true)], edgeKind: "decisionCondition" });
    const second = createSequenceFlow({ originObjectId: decision.id, destinationObjectId: merge.id, caseValues: [createBooleanCaseValue(false)], edgeKind: "decisionCondition", originConnectionIndex: 1 });
    const out = createSequenceFlow({ originObjectId: merge.id, destinationObjectId: end.id });
    const schema = schemaWith([decision, merge, end], [first, second, out]);
    const summary = getMergeFlowSummary(schema, merge.id);
    const deleted = deleteObject(schema, merge.id);

    expect(summary.incoming).toHaveLength(2);
    expect(summary.outgoing).toHaveLength(1);
    expect(deleted.flows).toHaveLength(0);
  });

  it("keeps A/B Decision branch configuration isolated", () => {
    const aDecision = decisionObject("a-decision");
    const aMerge = mergeObject("a-merge");
    const bDecision = decisionObject("b-decision");
    const bMerge = mergeObject("b-merge");
    const aFlow = createSequenceFlow({ originObjectId: aDecision.id, destinationObjectId: aMerge.id, edgeKind: "decisionCondition" });
    const bFlow = createSequenceFlow({ originObjectId: bDecision.id, destinationObjectId: bMerge.id, edgeKind: "decisionCondition" });
    const schemaA = assignDecisionBooleanCase(schemaWith([aDecision, aMerge], [aFlow]), aFlow.id, true);
    const schemaB = schemaWith([bDecision, bMerge], [bFlow]);

    expect(schemaA.flows[0]?.kind === "sequence" ? schemaA.flows[0].caseValues[0] : undefined).toMatchObject({ kind: "boolean", value: true });
    expect(schemaB.flows[0]?.kind === "sequence" ? schemaB.flows[0].caseValues : undefined).toEqual([]);
  });
});
