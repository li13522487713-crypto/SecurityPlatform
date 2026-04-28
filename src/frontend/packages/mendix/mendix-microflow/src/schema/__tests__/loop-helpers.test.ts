import { describe, expect, it } from "vitest";

import { createObjectFromRegistry, createSequenceFlow, deleteObject, duplicateObject } from "../../adapters";
import { defaultMicroflowNodeRegistry, getMicroflowNodeRegistryKey } from "../../node-registry";
import { buildMicroflowVariableIndex } from "../../variables";
import {
  assignLoopFlowKind,
  buildLoopVariableIndex,
  getBreakContinueWarnings,
  getLoopBodyFlows,
  getLoopExitFlows,
  removeLoopVariable,
  updateBreakContinueTargetLoop,
  updateLoopConditionExpression,
  updateLoopIterableExpression,
  updateLoopType,
  upsertLoopVariable,
} from "../utils";
import { sampleMicroflowSchema, type MicroflowObject, type MicroflowSchema } from "../index";

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
});
