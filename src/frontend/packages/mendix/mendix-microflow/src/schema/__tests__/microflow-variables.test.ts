import { describe, expect, it } from "vitest";

import { createObjectFromRegistry, deleteObject, duplicateObject, updateObject } from "../../adapters";
import { defaultMicroflowNodeRegistry, getMicroflowNodeRegistryKey } from "../../node-registry";
import {
  buildMicroflowVariableIndex,
  getVariableNameConflicts,
  renameMicroflowVariable,
  updateChangeVariableExpression,
  updateChangeVariableTarget,
  updateMicroflowVariableType,
} from "../../variables";
import {
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

function schemaWith(objects: MicroflowObject[] = []): MicroflowSchema {
  return {
    ...sampleMicroflowSchema,
    id: "MF_VARIABLE_TEST",
    stableId: "MF_VARIABLE_TEST",
    parameters: [],
    objectCollection: { ...sampleMicroflowSchema.objectCollection, objects },
    flows: [],
    editor: { ...sampleMicroflowSchema.editor, selection: {} },
  };
}

function createVariableObject(id = "create-variable") {
  const object = createObjectFromRegistry(registry("activity:variableCreate"), { x: 0, y: 0 }, id);
  if (object.kind !== "actionActivity" || object.action.kind !== "createVariable") {
    throw new Error("Expected Create Variable action.");
  }
  return object;
}

function changeVariableObject(id = "change-variable") {
  const object = createObjectFromRegistry(registry("activity:variableChange"), { x: 200, y: 0 }, id);
  if (object.kind !== "actionActivity" || object.action.kind !== "changeVariable") {
    throw new Error("Expected Change Variable action.");
  }
  return object;
}

describe("microflow variable foundation", () => {
  it("builds variable index from parameters and Create Variable actions", () => {
    const createVariable = createVariableObject();
    const schema = {
      ...schemaWith([createVariable]),
      parameters: [{ id: "param-amount", name: "amount", dataType: { kind: "decimal" as const }, required: true }],
    };
    const index = buildMicroflowVariableIndex(schema);

    expect(index.parameters.amount.name).toBe("amount");
    expect(index.localVariables.newVariable.name).toBe("newVariable");
    expect(index.localVariables.newVariable.source.kind).toBe("createVariable");
  });

  it("renames and retargets Create Variable through schema helpers", () => {
    const createVariable = createVariableObject();
    const changeVariable = changeVariableObject();
    const schema = schemaWith([createVariable, changeVariable]);
    const renamed = renameMicroflowVariable(schema, createVariable.action.id, "approvalLevel");
    const typed = updateMicroflowVariableType(renamed, createVariable.action.id, { kind: "string" });
    const targeted = updateChangeVariableTarget(typed, changeVariable.id, "approvalLevel");
    const changed = updateChangeVariableExpression(targeted, changeVariable.id, { raw: "\"L2\"", inferredType: { kind: "string" } });
    const nextChange = changed.objectCollection.objects.find(object => object.id === changeVariable.id);

    expect(buildMicroflowVariableIndex(changed).localVariables.approvalLevel.dataType).toEqual({ kind: "string" });
    expect(nextChange?.kind === "actionActivity" && nextChange.action.kind === "changeVariable" ? nextChange.action.targetVariableName : undefined).toBe("approvalLevel");
    expect(nextChange?.kind === "actionActivity" && nextChange.action.kind === "changeVariable" ? nextChange.action.newValueExpression.raw : undefined).toBe("\"L2\"");
  });

  it("deletes Create Variable by removing its node from the derived variable index", () => {
    const createVariable = createVariableObject();
    const schema = schemaWith([createVariable]);
    const deleted = deleteObject(schema, createVariable.id);

    expect(buildMicroflowVariableIndex(deleted).localVariables.newVariable).toBeUndefined();
  });

  it("duplicates Create Variable with a new action id and variable name", () => {
    const createVariable = updateObject(schemaWith([createVariableObject()]), "create-variable", object => {
      if (object.kind !== "actionActivity" || object.action.kind !== "createVariable") {
        return object;
      }
      return { ...object, action: { ...object.action, variableName: "approvalLevel" } };
    }).objectCollection.objects[0];
    if (!createVariable || createVariable.kind !== "actionActivity" || createVariable.action.kind !== "createVariable") {
      throw new Error("Expected Create Variable action.");
    }
    const duplicated = duplicateObject(schemaWith([createVariable]), createVariable.id);
    const variables = Object.values(buildMicroflowVariableIndex(duplicated).localVariables);

    expect(variables.map(variable => variable.name).sort()).toEqual(["approvalLevel", "approvalLevel_Copy"]);
    expect(new Set(variables.map(variable => variable.source.kind === "createVariable" ? variable.source.actionId : variable.id)).size).toBe(2);
  });

  it("detects variable duplicate and parameter-name conflicts", () => {
    const first = createVariableObject("create-variable-a");
    const second = createVariableObject("create-variable-b");
    const schema = {
      ...schemaWith([first, second]),
      parameters: [{ id: "param-approval", name: "approvalLevel", dataType: { kind: "string" as const }, required: true }],
    };
    const renamed = renameMicroflowVariable(renameMicroflowVariable(schema, first.action.id, "approvalLevel"), second.action.id, "ApprovalLevel");
    const index = buildMicroflowVariableIndex(renamed);

    expect(getVariableNameConflicts(renamed, "approvalLevel", first.action.id).length).toBeGreaterThan(0);
    expect(index.diagnostics?.some(issue => issue.code === "MF_VARIABLE_PARAMETER_CONFLICT")).toBe(true);
    expect(index.diagnostics?.some(issue => issue.code === "MF_VARIABLE_DUPLICATED")).toBe(true);
  });

  it("keeps A/B variable indexes isolated", () => {
    const aCreate = createVariableObject("a-create");
    const bCreate = createVariableObject("b-create");
    const a = renameMicroflowVariable({ ...schemaWith([aCreate]), id: "MF_A", stableId: "MF_A" }, aCreate.action.id, "approvalLevel");
    const b = { ...schemaWith([bCreate]), id: "MF_B", stableId: "MF_B" };

    expect(buildMicroflowVariableIndex(a).localVariables.approvalLevel).toBeDefined();
    expect(buildMicroflowVariableIndex(b).localVariables.approvalLevel).toBeUndefined();
    expect(buildMicroflowVariableIndex(b).localVariables.newVariable).toBeDefined();
  });
});
