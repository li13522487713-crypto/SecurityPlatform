import { describe, expect, it } from "vitest";

import { deleteObject, duplicateObject, createObjectFromRegistry } from "../../adapters";
import { sampleMicroflowSchema } from "../../schema/sample";
import { getDefaultMockMetadataCatalog } from "../../metadata";
import { addMicroflowObjectFromDragPayload, createDragPayloadFromRegistryItem, defaultMicroflowNodeRegistry, getMicroflowNodeRegistryKey } from "../../node-registry";
import {
  getParameterNameWarning,
  renameMicroflowParameter,
  setParameterAsMicroflowReturnValue,
  updateEndEventReturnValue,
  updateMicroflowParameterType,
  updateMicroflowReturnType,
  validateMicroflowSchema,
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
    id: "MF_TEST",
    stableId: "MF_TEST",
    parameters: [],
    objectCollection: { ...sampleMicroflowSchema.objectCollection, objects },
    flows: [],
    editor: { ...sampleMicroflowSchema.editor, selection: {} },
  };
}

function addParameter(schema: MicroflowSchema) {
  const result = addMicroflowObjectFromDragPayload({
    schema,
    payload: createDragPayloadFromRegistryItem(registry("parameter")),
    position: { x: 0, y: 0 },
  });
  const object = result.objectId ? result.schema.objectCollection.objects.find(item => item.id === result.objectId) : undefined;
  if (!object || object.kind !== "parameterObject") {
    throw new Error("Expected parameter object.");
  }
  return { schema: result.schema, object };
}

describe("microflow parameter and return schema helpers", () => {
  it("adds Parameter node into schema.parameters", () => {
    const { schema, object } = addParameter(schemaWith());

    expect(schema.parameters).toHaveLength(1);
    expect(schema.parameters[0]).toMatchObject({ id: object.parameterId, name: "parameter", dataType: { kind: "string" } });
  });

  it("renames Parameter node and schema-level parameter together", () => {
    const { schema, object } = addParameter(schemaWith());
    const renamed = renameMicroflowParameter(schema, object.parameterId, "amount");
    const renamedObject = renamed.objectCollection.objects.find(item => item.id === object.id);

    expect(renamed.parameters[0]?.name).toBe("amount");
    expect(renamedObject?.kind === "parameterObject" ? renamedObject.parameterName : undefined).toBe("amount");
    expect(renamedObject?.caption).toBe("amount");
  });

  it("blocks parameter names that collide with transport error context variables", () => {
    const { schema, object } = addParameter(schemaWith());

    expect(getParameterNameWarning(schema, object.parameterId, "latestHttpResponse")).toBe("Parameter name conflicts with a reserved system variable.");
    expect(getParameterNameWarning(schema, object.parameterId, "latestSoapFault")).toBe("Parameter name conflicts with a reserved system variable.");
  });

  it("updates Parameter type on schema-level parameter", () => {
    const { schema, object } = addParameter(schemaWith());
    const updated = updateMicroflowParameterType(schema, object.parameterId, { kind: "decimal" });

    expect(updated.parameters[0]?.dataType).toEqual({ kind: "decimal" });
    expect(updated.parameters[0]?.type).toMatchObject({ kind: "primitive", name: "decimal" });
  });

  it("updates microflow returnType and end inferredType when a bound return parameter changes type", () => {
    const end = createObjectFromRegistry(registry("endEvent"), { x: 0, y: 0 }, "return-end-type");
    const added = addParameter(schemaWith([end]));
    const renamed = renameMicroflowParameter(added.schema, added.object.parameterId, "amount");
    const linked = setParameterAsMicroflowReturnValue(renamed, added.object.parameterId);
    const updated = updateMicroflowParameterType(linked, added.object.parameterId, { kind: "decimal" });
    const updatedEnd = updated.objectCollection.objects.find(item => item.id === end.id);

    expect(updated.parameters[0]?.dataType).toEqual({ kind: "decimal" });
    expect(updated.returnType).toEqual({ kind: "decimal" });
    expect(updated.returnVariableName).toBe("amount");
    expect(updatedEnd?.kind === "endEvent" ? updatedEnd.returnValue?.raw : undefined).toBe("$amount");
    expect(updatedEnd?.kind === "endEvent" ? updatedEnd.returnValue?.inferredType : undefined).toEqual({ kind: "decimal" });
  });

  it("deletes Parameter node and removes schema-level parameter", () => {
    const { schema, object } = addParameter(schemaWith());
    const deleted = deleteObject(schema, object.id);

    expect(deleted.objectCollection.objects.some(item => item.id === object.id)).toBe(false);
    expect(deleted.parameters.some(parameter => parameter.id === object.parameterId)).toBe(false);
  });

  it("clears bound return chain when deleting a parameter node that was set as microflow return value", () => {
    const end = createObjectFromRegistry(registry("endEvent"), { x: 240, y: 0 }, "return-end-delete");
    const added = addParameter(schemaWith([end]));
    const renamed = renameMicroflowParameter(added.schema, added.object.parameterId, "amount");
    const linked = setParameterAsMicroflowReturnValue(renamed, added.object.parameterId);
    const deleted = deleteObject(linked, added.object.id);
    const updatedEnd = deleted.objectCollection.objects.find(item => item.id === end.id);

    expect(deleted.parameters.some(parameter => parameter.id === added.object.parameterId)).toBe(false);
    expect(deleted.returnType).toEqual({ kind: "void" });
    expect(deleted.returnVariableName).toBeUndefined();
    expect(updatedEnd?.kind === "endEvent" ? updatedEnd.returnValue : undefined).toBeUndefined();
  });

  it("keeps complex end return expressions when deleting the referenced parameter", () => {
    const end = createObjectFromRegistry(registry("endEvent"), { x: 240, y: 0 }, "return-end-delete-complex");
    const added = addParameter(schemaWith([end]));
    const renamed = renameMicroflowParameter(added.schema, added.object.parameterId, "amount");
    const typed = updateMicroflowReturnType(renamed, { kind: "string" });
    const withExpression = updateEndEventReturnValue(typed, end.id, {
      raw: "if $amount > 100 then 'vip' else 'normal'",
      inferredType: { kind: "string" },
      references: { variables: ["$amount"], entities: [], attributes: [], associations: [], enumerations: [], functions: [] },
      diagnostics: [],
    });
    const deleted = deleteObject(withExpression, added.object.id);
    const updatedEnd = deleted.objectCollection.objects.find(item => item.id === end.id);

    expect(deleted.parameters.some(parameter => parameter.id === added.object.parameterId)).toBe(false);
    expect(deleted.returnType).toEqual({ kind: "string" });
    expect(deleted.returnVariableName).toBeUndefined();
    expect(updatedEnd?.kind === "endEvent" ? updatedEnd.returnValue?.raw : undefined).toBe("if $amount > 100 then 'vip' else 'normal'");
  });

  it("duplicates Parameter node with a new parameter id and non-conflicting name", () => {
    const { schema, object } = addParameter(schemaWith());
    const renamed = renameMicroflowParameter(schema, object.parameterId, "amount");
    const duplicated = duplicateObject(renamed, object.id);
    const copies = duplicated.objectCollection.objects.filter(item => item.kind === "parameterObject");
    const copied = copies.find(item => item.id !== object.id);

    expect(copied?.kind).toBe("parameterObject");
    expect(copied?.kind === "parameterObject" ? copied.parameterId : undefined).not.toBe(object.parameterId);
    expect(copied?.caption).toBe("amount_Copy");
    expect(duplicated.parameters.map(parameter => parameter.name)).toEqual(["amount", "amount_Copy"]);
  });

  it("updates End returnType and returnValueExpression in schema", () => {
    const end = createObjectFromRegistry(registry("endEvent"), { x: 0, y: 0 }, "end-test");
    const schema = schemaWith([end]);
    const typed = updateMicroflowReturnType(schema, { kind: "boolean" });
    const returned = updateEndEventReturnValue(typed, end.id, { raw: "amount > 100", inferredType: { kind: "boolean" } });
    const returnedEnd = returned.objectCollection.objects.find(item => item.id === end.id);

    expect(returned.returnType).toEqual({ kind: "boolean" });
    expect(returnedEnd?.kind === "endEvent" ? returnedEnd.returnValue?.raw : undefined).toBe("amount > 100");
  });

  it("keeps A/B parameter and return edits isolated", () => {
    const a = addParameter({ ...schemaWith(), id: "MF_A", stableId: "MF_A" });
    const b = addParameter({ ...schemaWith(), id: "MF_B", stableId: "MF_B" });
    const updatedA = updateMicroflowReturnType(renameMicroflowParameter(a.schema, a.object.parameterId, "totalAmount"), { kind: "boolean" });

    expect(updatedA.parameters[0]?.name).toBe("totalAmount");
    expect(updatedA.returnType).toEqual({ kind: "boolean" });
    expect(b.schema.parameters[0]?.name).toBe("parameter");
    expect(b.schema.returnType).toEqual(sampleMicroflowSchema.returnType);
  });

  it("sets parameter as microflow return value across end events", () => {
    const firstEnd = createObjectFromRegistry(registry("endEvent"), { x: 0, y: 0 }, "return-end-a");
    const secondEnd = createObjectFromRegistry(registry("endEvent"), { x: 240, y: 0 }, "return-end-b");
    const added = addParameter(schemaWith([firstEnd, secondEnd]));
    const typed = updateMicroflowParameterType(added.schema, added.object.parameterId, { kind: "decimal" });
    const renamed = renameMicroflowParameter(typed, added.object.parameterId, "amount");

    const updated = setParameterAsMicroflowReturnValue(renamed, added.object.parameterId);
    const endValues = updated.objectCollection.objects
      .filter((object): object is typeof firstEnd => object.kind === "endEvent")
      .map(object => object.returnValue?.raw);

    expect(updated.returnType).toEqual({ kind: "decimal" });
    expect(updated.returnVariableName).toBe("amount");
    expect(endValues).toEqual(["$amount", "$amount"]);
  });

  it("renames parameter references inside end return expressions after using parameter as return value", () => {
    const end = createObjectFromRegistry(registry("endEvent"), { x: 0, y: 0 }, "return-end-rename");
    const added = addParameter(schemaWith([end]));
    const firstRename = renameMicroflowParameter(added.schema, added.object.parameterId, "amount");
    const linked = setParameterAsMicroflowReturnValue(firstRename, added.object.parameterId);
    const renamed = renameMicroflowParameter(linked, added.object.parameterId, "totalAmount");
    const updatedEnd = renamed.objectCollection.objects.find(item => item.id === end.id);

    expect(renamed.parameters[0]?.name).toBe("totalAmount");
    expect(renamed.returnVariableName).toBe("totalAmount");
    expect(updatedEnd?.kind === "endEvent" ? updatedEnd.returnValue?.raw : undefined).toBe("$totalAmount");
  });

  it("rewrites direct variable reference fields that target a renamed parameter", () => {
    const change = createObjectFromRegistry(registry("activity:variableChange"), { x: 120, y: 0 }, "param-change");
    if (change.kind !== "actionActivity" || change.action.kind !== "changeVariable") {
      throw new Error("Expected change variable action.");
    }
    const added = addParameter(schemaWith([{
      ...change,
      action: {
        ...change.action,
        targetVariableName: "amount",
        newValueExpression: { raw: "$amount + 1", references: { variables: ["$amount"], entities: [], attributes: [], associations: [], enumerations: [], functions: [] }, diagnostics: [] },
      },
    }]));
    const renamed = renameMicroflowParameter(added.schema, added.object.parameterId, "amount");
    const updated = renameMicroflowParameter(renamed, added.object.parameterId, "totalAmount");
    const updatedChange = updated.objectCollection.objects.find(item => item.id === change.id);

    expect(updatedChange?.kind === "actionActivity" && updatedChange.action.kind === "changeVariable" ? updatedChange.action.targetVariableName : undefined).toBe("totalAmount");
    expect(updatedChange?.kind === "actionActivity" && updatedChange.action.kind === "changeVariable" ? updatedChange.action.newValueExpression.raw : undefined).toBe("$totalAmount + 1");
  });

  it("keeps shadowed loop iterator references unchanged when renaming a parameter", () => {
    const loop = createObjectFromRegistry(registry("loop"), { x: 120, y: 0 }, "param-shadow-loop");
    const change = createObjectFromRegistry(registry("activity:variableChange"), { x: 200, y: 0 }, "param-shadow-change");
    if (loop.kind !== "loopedActivity" || change.kind !== "actionActivity" || change.action.kind !== "changeVariable") {
      throw new Error("Expected loop and change variable action.");
    }
    const added = addParameter(schemaWith([{
      ...loop,
      loopSource: {
        kind: "iterableList",
        officialType: "Microflows$IterableList",
        listVariableName: "$amount",
        iteratorVariableName: "amount",
        currentIndexVariableName: "$currentIndex",
      },
      objectCollection: {
        ...loop.objectCollection,
        objects: [
          {
            ...change,
            action: {
              ...change.action,
              targetVariableName: "amount",
              newValueExpression: { raw: "$amount + 1", references: { variables: ["$amount"], entities: [], attributes: [], associations: [], enumerations: [], functions: [] }, diagnostics: [] },
            },
          },
        ],
        flows: [],
      },
    }]));
    const renamed = renameMicroflowParameter(added.schema, added.object.parameterId, "amount");
    const updated = renameMicroflowParameter(renamed, added.object.parameterId, "totalAmount");
    const updatedLoop = updated.objectCollection.objects.find(item => item.id === loop.id);
    const updatedChange = updatedLoop?.kind === "loopedActivity"
      ? updatedLoop.objectCollection.objects.find(item => item.id === change.id)
      : undefined;

    expect(updatedLoop?.kind === "loopedActivity" && updatedLoop.loopSource.kind === "iterableList" ? updatedLoop.loopSource.listVariableName : undefined).toBe("$totalAmount");
    expect(updatedLoop?.kind === "loopedActivity" && updatedLoop.loopSource.kind === "iterableList" ? updatedLoop.loopSource.iteratorVariableName : undefined).toBe("amount");
    expect(updatedChange?.kind === "actionActivity" && updatedChange.action.kind === "changeVariable" ? updatedChange.action.targetVariableName : undefined).toBe("amount");
    expect(updatedChange?.kind === "actionActivity" && updatedChange.action.kind === "changeVariable" ? updatedChange.action.newValueExpression.raw : undefined).toBe("$amount + 1");
  });

  it("clears compat returnVariableName when end return expression is not a simple variable", () => {
    const end = createObjectFromRegistry(registry("endEvent"), { x: 0, y: 0 }, "return-end-expression");
    const schema = schemaWith([end]);
    const typed = updateMicroflowReturnType(schema, { kind: "string" });
    const simple = updateEndEventReturnValue(typed, end.id, { raw: "$amount", inferredType: { kind: "decimal" } });
    const complex = updateEndEventReturnValue(simple, end.id, { raw: "if $amount > 100 then 'vip' else 'normal'", inferredType: { kind: "string" } });

    expect(simple.returnVariableName).toBe("amount");
    expect(complex.returnVariableName).toBeUndefined();
  });

  it("clears compat returnVariableName when returnType becomes void", () => {
    const end = createObjectFromRegistry(registry("endEvent"), { x: 0, y: 0 }, "return-end-void");
    const schema = schemaWith([end]);
    const typed = updateMicroflowReturnType(schema, { kind: "decimal" });
    const returned = updateEndEventReturnValue(typed, end.id, { raw: "$amount", inferredType: { kind: "decimal" } });
    const voided = updateMicroflowReturnType(returned, { kind: "void" });

    expect(returned.returnVariableName).toBe("amount");
    expect(voided.returnVariableName).toBeUndefined();
    expect(voided.objectCollection.objects.find(item => item.id === end.id)?.kind === "endEvent"
      ? voided.objectCollection.objects.find(item => item.id === end.id)?.returnValue
      : undefined).toBeUndefined();
  });

  it("reports empty and duplicate parameter names", () => {
    const first = addParameter(schemaWith());
    const second = addParameter(first.schema);
    const duplicated = renameMicroflowParameter(renameMicroflowParameter(second.schema, first.object.parameterId, "amount"), second.object.parameterId, "Amount");
    const empty = renameMicroflowParameter(duplicated, first.object.parameterId, " ");
    const validation = validateMicroflowSchema({ schema: empty, metadata: getDefaultMockMetadataCatalog() });

    expect(getParameterNameWarning(duplicated, second.object.parameterId, "Amount")).toContain("unique");
    expect(getParameterNameWarning(empty, first.object.parameterId, " ")).toContain("required");
    expect(validation.issues.some(issue => issue.code === "MF_PARAMETER_NAME_MISSING")).toBe(true);
    expect(validateMicroflowSchema({ schema: duplicated, metadata: getDefaultMockMetadataCatalog() }).issues.some(issue => issue.code === "MF_PARAMETER_DUPLICATED")).toBe(true);
  });
});

