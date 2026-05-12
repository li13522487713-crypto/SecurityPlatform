import { describe, expect, it } from "vitest";

import { createObjectFromRegistry, createSequenceFlow, deleteObject, duplicateObject } from "../../adapters";
import { sampleMicroflowSchema } from "../../schema/sample";
import { defaultMicroflowNodeRegistry, getMicroflowNodeRegistryKey } from "../../node-registry";
import { buildMicroflowVariableIndex } from "../../variables";
import {
  assignLoopFlowKind,
  buildLoopVariableIndex,
  getBreakContinueWarnings,
  getLoopBodyFlows,
  getLoopExitFlows,
  removeLoopVariable,
  renameLoopIteratorVariable,
  updateBreakContinueTargetLoop,
  updateLoopConditionExpression,
  updateLoopIterableExpression,
  updateLoopType,
  upsertLoopVariable,
} from "../utils";
import type { MicroflowObject, MicroflowSchema } from "../index";

function registry(key: string) {
  const item = defaultMicroflowNodeRegistry.find(entry => getMicroflowNodeRegistryKey(entry) === key || entry.type === key);
  if (!item) {
    throw new Error(`Missing registry item ${key}`);
  }
  return item;
}

function loopObject(id = "loop-a") {
  const object = createObjectFromRegistry(registry("loop"), { x: 0, y: 0 }, id);
  if (object.kind !== "loopedActivity") {
    throw new Error("Expected Loop object.");
  }
  return object;
}

function breakObject(id = "break-a") {
  const object = createObjectFromRegistry(registry("breakEvent"), { x: 120, y: 0 }, id);
  if (object.kind !== "breakEvent") {
    throw new Error("Expected Break object.");
  }
  return object;
}

function continueObject(id = "continue-a") {
  const object = createObjectFromRegistry(registry("continueEvent"), { x: 160, y: 0 }, id);
  if (object.kind !== "continueEvent") {
    throw new Error("Expected Continue object.");
  }
  return object;
}

function schemaWith(objects: MicroflowObject[] = [], flows: MicroflowSchema["flows"] = []): MicroflowSchema {
  return {
    ...sampleMicroflowSchema,
    id: "MF_LOOP_TEST",
    stableId: "MF_LOOP_TEST",
    parameters: [],
    objectCollection: { ...sampleMicroflowSchema.objectCollection, id: "root", objects },
    flows,
    editor: { ...sampleMicroflowSchema.editor, selection: {} },
  };
}

describe("loop / break / continue helpers", () => {
  it("updates loop type, iterable expression, and condition expression", () => {
    const loop = loopObject();
    const withIterable = updateLoopIterableExpression(schemaWith([loop]), loop.id, "approvalUsers");
    const whileSchema = updateLoopType(withIterable, loop.id, "while");
    const conditioned = updateLoopConditionExpression(whileSchema, loop.id, "$retry");
    const iterableLoop = withIterable.objectCollection.objects[0];
    const nextLoop = conditioned.objectCollection.objects[0];

    expect(iterableLoop?.kind === "loopedActivity" ? iterableLoop.loopSource.kind : undefined).toBe("iterableList");
    expect(iterableLoop?.kind === "loopedActivity" && iterableLoop.loopSource.kind === "iterableList" ? iterableLoop.loopSource.listVariableName : undefined).toBe("approvalUsers");
    expect(nextLoop?.kind === "loopedActivity" && nextLoop.loopSource.kind === "whileCondition" ? nextLoop.loopSource.expression.raw : undefined).toBe("$retry");
  });

  it("upserts and removes loop variables from the current schema index", () => {
    const loop = loopObject();
    const schema = upsertLoopVariable(schemaWith([loop]), loop.id, { name: "currentApprover", dataType: { kind: "string" } });

    expect(buildLoopVariableIndex(schema).currentApprover.source.kind).toBe("loopIterator");
    expect(buildMicroflowVariableIndex(schema).loopVariables.currentApprover.scope.kind).toBe("loop");
    expect(buildMicroflowVariableIndex(removeLoopVariable(schema, loop.id)).loopVariables.currentApprover).toBeUndefined();
  });

  it("stores body and exit flows by loop connection index", () => {
    const loop = loopObject();
    const brk = breakObject();
    const bodyFlow = createSequenceFlow({ originObjectId: loop.id, destinationObjectId: brk.id });
    const exitFlow = createSequenceFlow({ originObjectId: loop.id, destinationObjectId: "after-node" });
    const schema = assignLoopFlowKind(assignLoopFlowKind(schemaWith([loop, brk], [bodyFlow, exitFlow]), bodyFlow.id, "body"), exitFlow.id, "exit");

    expect(getLoopBodyFlows(schema, loop.id).map(flow => flow.id)).toEqual([bodyFlow.id]);
    expect(getLoopExitFlows(schema, loop.id).map(flow => flow.id)).toEqual([exitFlow.id]);
  });

  it("deletes loop-scoped variables and related body / exit flows when deleting a loop", () => {
    const loop = loopObject();
    const brk = breakObject();
    const schema = upsertLoopVariable(schemaWith([loop, brk], [
      createSequenceFlow({ originObjectId: loop.id, destinationObjectId: brk.id, originConnectionIndex: 2 }),
      createSequenceFlow({ originObjectId: loop.id, destinationObjectId: brk.id, originConnectionIndex: 1 }),
    ]), loop.id, { name: "currentApprover" });
    const deleted = deleteObject(schema, loop.id);

    expect(buildMicroflowVariableIndex(deleted).loopVariables.currentApprover).toBeUndefined();
    expect(deleted.flows).toHaveLength(0);
  });

  it("warns for break / continue without loop and stale target loop", () => {
    const brk = breakObject();
    const cont = continueObject();
    const noLoop = schemaWith([brk, cont]);

    expect(getBreakContinueWarnings(noLoop, brk.id).some(warning => warning.includes("No Loop"))).toBe(true);
    const loop = loopObject();
    const targeted = updateBreakContinueTargetLoop(schemaWith([loop, brk]), brk.id, loop.id);
    const stale = deleteObject(targeted, loop.id);
    expect(getBreakContinueWarnings(stale, brk.id).some(warning => warning.includes("stale"))).toBe(true);
    expect(getBreakContinueWarnings(noLoop, cont.id).some(warning => warning.includes("No Loop"))).toBe(true);
  });

  it("allows Break outside loop body when targetLoopObjectId explicitly points at an existing loop", () => {
    const loop = loopObject();
    const brk = breakObject();
    const schema = updateBreakContinueTargetLoop(schemaWith([loop, brk]), brk.id, loop.id);

    expect(getBreakContinueWarnings(schema, brk.id).some(warning => warning.includes("not inside a Loop body"))).toBe(false);
  });

  it("keeps A/B schema loop variables isolated and duplicates loop variable names", () => {
    const aLoop = loopObject("loop-a");
    const bLoop = loopObject("loop-b");
    const a = upsertLoopVariable(schemaWith([aLoop]), aLoop.id, { name: "currentApprover" });
    const b = schemaWith([bLoop]);
    const duplicated = duplicateObject(a, aLoop.id);

    expect(buildMicroflowVariableIndex(a).loopVariables.currentApprover).toBeDefined();
    expect(buildMicroflowVariableIndex(b).loopVariables.currentApprover).toBeUndefined();
    expect(Object.keys(buildMicroflowVariableIndex(duplicated).loopVariables).sort()).toEqual(["currentApprover", "currentApprover_Copy"]);
    expect(JSON.stringify(duplicated)).not.toContain("Sales.");
  });

  it("rewrites only the targeted loop iterator references and preserves shadowed inner loops", () => {
    const outerLoop = loopObject("loop-outer");
    const innerLoop = loopObject("loop-inner");
    const outerEnd = createObjectFromRegistry(registry("endEvent"), { x: 120, y: 0 }, "outer-end");
    const innerEnd = createObjectFromRegistry(registry("endEvent"), { x: 160, y: 0 }, "inner-end");
    const outerChange = createObjectFromRegistry(registry("activity:variableChange"), { x: 80, y: 40 }, "outer-change");
    const innerChange = createObjectFromRegistry(registry("activity:variableChange"), { x: 120, y: 40 }, "inner-change");
    if (outerEnd.kind !== "endEvent" || innerEnd.kind !== "endEvent") {
      throw new Error("Expected end events.");
    }
    if (outerChange.kind !== "actionActivity" || outerChange.action.kind !== "changeVariable" || innerChange.kind !== "actionActivity" || innerChange.action.kind !== "changeVariable") {
      throw new Error("Expected change variable actions.");
    }
    const outerLoopObject = {
      ...outerLoop,
      loopSource: {
        ...outerLoop.loopSource,
        kind: "iterableList" as const,
        officialType: "Microflows$IterableList" as const,
        listVariableName: "$OrderList",
        iteratorVariableName: "item",
        currentIndexVariableName: "$currentIndex" as const,
      },
      objectCollection: {
        ...outerLoop.objectCollection,
        objects: [
          {
            ...outerChange,
            action: {
              ...outerChange.action,
              targetVariableName: "item",
              newValueExpression: { raw: "$item/Name", references: { variables: ["$item"], entities: [], attributes: ["Name"], associations: [], enumerations: [], functions: [] }, diagnostics: [] },
            },
          },
          { ...outerEnd, returnValue: { raw: "$item/Name", references: { variables: ["$item"], entities: [], attributes: ["Name"], associations: [], enumerations: [], functions: [] }, diagnostics: [] } },
          {
            ...innerLoop,
            loopSource: {
              ...innerLoop.loopSource,
              kind: "iterableList" as const,
              officialType: "Microflows$IterableList" as const,
              listVariableName: "$item/Children",
              iteratorVariableName: "item",
              currentIndexVariableName: "$currentIndex" as const,
            },
            objectCollection: {
              ...innerLoop.objectCollection,
              objects: [
                {
                  ...innerChange,
                  action: {
                    ...innerChange.action,
                    targetVariableName: "item",
                    newValueExpression: { raw: "$item/Code", references: { variables: ["$item"], entities: [], attributes: ["Code"], associations: [], enumerations: [], functions: [] }, diagnostics: [] },
                  },
                },
                { ...innerEnd, returnValue: { raw: "$item/Code", references: { variables: ["$item"], entities: [], attributes: ["Code"], associations: [], enumerations: [], functions: [] }, diagnostics: [] } },
              ],
              flows: [],
            },
          },
        ],
        flows: [],
      },
    };
    const updated = renameLoopIteratorVariable(schemaWith([outerLoopObject]), outerLoop.id, "orderItem");
    const updatedOuter = updated.objectCollection.objects.find((item): item is Extract<MicroflowObject, { kind: "loopedActivity" }> => item.id === outerLoop.id);
    const updatedOuterEnd = updatedOuter?.objectCollection.objects.find((item): item is Extract<MicroflowObject, { kind: "endEvent" }> => item.id === outerEnd.id);
    const updatedOuterChange = updatedOuter?.objectCollection.objects.find((item): item is Extract<MicroflowObject, { kind: "actionActivity" }> => item.id === outerChange.id);
    const updatedInnerLoop = updatedOuter?.objectCollection.objects.find((item): item is Extract<MicroflowObject, { kind: "loopedActivity" }> => item.id === innerLoop.id);
    const updatedInnerEnd = updatedInnerLoop?.objectCollection.objects.find((item): item is Extract<MicroflowObject, { kind: "endEvent" }> => item.id === innerEnd.id);
    const updatedInnerChange = updatedInnerLoop?.objectCollection.objects.find((item): item is Extract<MicroflowObject, { kind: "actionActivity" }> => item.id === innerChange.id);

    expect(updatedOuter?.loopSource.kind === "iterableList" ? updatedOuter.loopSource.iteratorVariableName : undefined).toBe("orderItem");
    expect(updatedOuterChange?.kind === "actionActivity" && updatedOuterChange.action.kind === "changeVariable" ? updatedOuterChange.action.targetVariableName : undefined).toBe("orderItem");
    expect(updatedOuterChange?.kind === "actionActivity" && updatedOuterChange.action.kind === "changeVariable" ? updatedOuterChange.action.newValueExpression.raw : undefined).toBe("$orderItem/Name");
    expect(updatedOuterEnd?.returnValue?.raw).toBe("$orderItem/Name");
    expect(updatedInnerLoop?.loopSource.kind === "iterableList" ? updatedInnerLoop.loopSource.listVariableName : undefined).toBe("$orderItem/Children");
    expect(updatedInnerChange?.kind === "actionActivity" && updatedInnerChange.action.kind === "changeVariable" ? updatedInnerChange.action.targetVariableName : undefined).toBe("item");
    expect(updatedInnerChange?.kind === "actionActivity" && updatedInnerChange.action.kind === "changeVariable" ? updatedInnerChange.action.newValueExpression.raw : undefined).toBe("$item/Code");
    expect(updatedInnerEnd?.returnValue?.raw).toBe("$item/Code");
  });
});

