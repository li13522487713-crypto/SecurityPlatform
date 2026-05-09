import { describe, expect, it } from "vitest";

import { createObjectFromRegistry, createSequenceFlow } from "../adapters";
import { sampleMicroflowSchema } from "../__fixtures__/sample-microflow";
import { defaultMicroflowNodeRegistry, getMicroflowNodeRegistryKey } from "../node-registry";
import type { MicroflowObject, MicroflowSchema } from "../schema";
import { collectFlowsRecursive } from "../schema/utils/object-utils";
import { createMissingBooleanBranch } from "./problem-quick-fixes";

function registry(key: string) {
  const item = defaultMicroflowNodeRegistry.find(entry => getMicroflowNodeRegistryKey(entry) === key || entry.type === key);
  if (!item) {
    throw new Error(`Missing registry item ${key}`);
  }
  return item;
}

function objectFrom(key: string, id: string, x = 0, y = 0): MicroflowObject {
  return createObjectFromRegistry(registry(key), { x, y }, id);
}

function schemaWith(objects: MicroflowObject[], flows: MicroflowSchema["flows"] = []): MicroflowSchema {
  return {
    ...sampleMicroflowSchema,
    id: "quick-fix-schema",
    objectCollection: { ...sampleMicroflowSchema.objectCollection, id: "root", objects },
    flows,
    editor: { ...sampleMicroflowSchema.editor, selection: {} },
  };
}

describe("problem quick fixes", () => {
  it("creates a missing root Boolean Decision branch without crossing collections", () => {
    const decision = objectFrom("decision", "decision", 240, 120);
    const end = objectFrom("endEvent", "end", 480, 120);
    const trueFlow = createSequenceFlow({
      id: "flow-true",
      originObjectId: decision.id,
      destinationObjectId: end.id,
      edgeKind: "decisionCondition",
      caseValues: [{ kind: "boolean", officialType: "Microflows$EnumerationCase", value: true, persistedValue: "true" }],
    });
    const next = createMissingBooleanBranch(schemaWith([decision, end], [trueFlow]), {
      id: "missing-false",
      code: "MF_DECISION_BOOLEAN_FALSE_MISSING",
      message: "missing false",
      severity: "warning",
      objectId: decision.id,
    });

    expect(next?.objectCollection.objects.some(object => object.kind === "endEvent" && object.id !== end.id)).toBe(true);
    const falseFlow = next?.flows.find(flow => flow.kind === "sequence" && flow.caseValues.some(caseValue => caseValue.kind === "boolean" && caseValue.value === false));
    expect(falseFlow).toBeDefined();
    expect(next?.objectCollection.flows ?? []).toEqual([]);
  });

  it("creates missing loop Boolean Decision branch inside loop collection", () => {
    const loop = objectFrom("loop", "loop");
    if (loop.kind !== "loopedActivity") {
      throw new Error("Expected loopedActivity.");
    }
    const decision = objectFrom("decision", "loop-decision", 80, 80);
    const continueEvent = objectFrom("continueEvent", "continue", 320, 80);
    const trueFlow = createSequenceFlow({
      id: "loop-flow-true",
      originObjectId: decision.id,
      destinationObjectId: continueEvent.id,
      edgeKind: "decisionCondition",
      caseValues: [{ kind: "boolean", officialType: "Microflows$EnumerationCase", value: true, persistedValue: "true" }],
    });
    const schema = schemaWith([{
      ...loop,
      objectCollection: {
        ...loop.objectCollection,
        id: "loop-body",
        objects: [decision, continueEvent],
        flows: [trueFlow],
      },
    }]);

    const next = createMissingBooleanBranch(schema, {
      id: "missing-loop-false",
      code: "MF_DECISION_BOOLEAN_FALSE_MISSING",
      message: "missing false",
      severity: "warning",
      objectId: decision.id,
    });
    const nextLoop = next?.objectCollection.objects.find(object => object.id === loop.id);
    const loopFlows = nextLoop?.kind === "loopedActivity" ? nextLoop.objectCollection.flows : [];
    const allFlows = next ? collectFlowsRecursive(next) : [];

    expect(next?.flows).toEqual([]);
    expect(nextLoop?.kind === "loopedActivity" ? nextLoop.objectCollection.objects.some(object => object.kind === "continueEvent" && object.id !== continueEvent.id) : false).toBe(true);
    expect(loopFlows?.some(flow => flow.kind === "sequence" && flow.caseValues.some(caseValue => caseValue.kind === "boolean" && caseValue.value === false))).toBe(true);
    expect(allFlows.every(flow => flow.originObjectId !== decision.id || loopFlows?.some(loopFlow => loopFlow.id === flow.id))).toBe(true);
  });
});
