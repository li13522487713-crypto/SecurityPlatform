import { describe, expect, it } from "vitest";

import { deleteObject, duplicateObject, createObjectFromRegistry } from "../../adapters";
import { getDefaultMockMetadataCatalog } from "../../metadata";
import { addMicroflowObjectFromDragPayload, createDragPayloadFromRegistryItem, defaultMicroflowNodeRegistry, getMicroflowNodeRegistryKey } from "../../node-registry";
import {
  getParameterNameWarning,
  renameMicroflowParameter,
  sampleMicroflowSchema,
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

  it("updates Parameter type on schema-level parameter", () => {
    const { schema, object } = addParameter(schemaWith());
    const updated = updateMicroflowParameterType(schema, object.parameterId, { kind: "decimal" });

    expect(updated.parameters[0]?.dataType).toEqual({ kind: "decimal" });
    expect(updated.parameters[0]?.type).toMatchObject({ kind: "primitive", name: "decimal" });
  });

  it("deletes Parameter node and removes schema-level parameter", () => {
    const { schema, object } = addParameter(schemaWith());
    const deleted = deleteObject(schema, object.id);

    expect(deleted.objectCollection.objects.some(item => item.id === object.id)).toBe(false);
    expect(deleted.parameters.some(parameter => parameter.id === object.parameterId)).toBe(false);
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
