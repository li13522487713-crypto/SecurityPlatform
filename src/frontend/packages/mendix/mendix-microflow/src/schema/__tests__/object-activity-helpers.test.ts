import { describe, expect, it } from "vitest";

import { createObjectFromRegistry, deleteObject, duplicateObject } from "../../adapters";
import { defaultMicroflowNodeRegistry, getMicroflowNodeRegistryKey } from "../../node-registry";
import {
  buildMicroflowVariableIndex,
  buildObjectActionWarnings,
  getListObjectVariables,
  getObjectVariables,
  updateCommitObjectTarget,
  updateDeleteObjectTarget,
  updateObjectActionEntity,
  updateObjectOutputVariable,
  updateRetrieveObjectRange,
  updateRetrieveObjectSource,
  upsertObjectMemberChange,
} from "../../variables";
import { EMPTY_MICROFLOW_METADATA_CATALOG } from "../../metadata";
import {
  sampleMicroflowSchema,
  type MicroflowExpression,
  type MicroflowObject,
  type MicroflowSchema,
} from "../index";

function expression(raw: string): MicroflowExpression {
  return { raw, text: raw, language: "mendix" };
}

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
    id: "MF_OBJECT_ACTIVITY_TEST",
    stableId: "MF_OBJECT_ACTIVITY_TEST",
    parameters: [],
    objectCollection: { ...sampleMicroflowSchema.objectCollection, objects },
    flows: [],
    editor: { ...sampleMicroflowSchema.editor, selection: {} },
  };
}

function actionObject(key: string, id: string) {
  const object = createObjectFromRegistry(registry(key), { x: 0, y: 0 }, id);
  if (object.kind !== "actionActivity") {
    throw new Error(`Expected action object for ${key}.`);
  }
  return object;
}

describe("microflow object activity foundation", () => {
  it("writes Create Object entity and output object variable into the current variable index", () => {
    const createObject = actionObject("activity:objectCreate", "create-object");
    const schema = updateObjectOutputVariable(
      updateObjectActionEntity(schemaWith([createObject]), createObject.id, { qualifiedName: "Procurement.PurchaseRequest" }),
      createObject.id,
      { name: "purchaseRequest" },
    );
    const index = buildMicroflowVariableIndex(schema);

    expect(index.objectOutputs.purchaseRequest.dataType).toEqual({ kind: "object", entityQualifiedName: "Procurement.PurchaseRequest" });
    expect(getObjectVariables(schema).map(variable => variable.name)).toEqual(["purchaseRequest"]);
  });

  it("writes Change Object memberChanges into schema", () => {
    const createObject = actionObject("activity:objectCreate", "create-object");
    const changeObject = actionObject("activity:objectChange", "change-object");
    let schema = updateObjectOutputVariable(
      updateObjectActionEntity(schemaWith([createObject, changeObject]), createObject.id, { qualifiedName: "Procurement.PurchaseRequest" }),
      createObject.id,
      { name: "purchaseRequest" },
    );
    schema = upsertObjectMemberChange(schema, changeObject.id, {
      id: "change-title",
      memberKind: "attribute",
      memberQualifiedName: "Procurement.PurchaseRequest.title",
      assignmentKind: "set",
      valueExpression: expression("'Laptop'"),
    });
    const changed = schema.objectCollection.objects.find(object => object.id === changeObject.id);

    expect(changed?.kind === "actionActivity" && changed.action.kind === "changeMembers" ? changed.action.memberChanges[0]?.memberQualifiedName : undefined).toBe("Procurement.PurchaseRequest.title");
  });

  it("infers Retrieve Object output type from range", () => {
    const retrieve = actionObject("activity:objectRetrieve", "retrieve-object");
    const source = {
      kind: "database" as const,
      officialType: "Microflows$DatabaseRetrieveSource" as const,
      entityQualifiedName: "Procurement.PurchaseRequest",
      xPathConstraint: null,
      sortItemList: { items: [] },
      range: { kind: "all" as const, officialType: "Microflows$ConstantRange" as const, value: "all" as const },
    };
    const schema = updateObjectOutputVariable(
      updateRetrieveObjectSource(schemaWith([retrieve]), retrieve.id, source),
      retrieve.id,
      { name: "purchaseRequests" },
    );

    expect(buildMicroflowVariableIndex(schema).listOutputs.purchaseRequests.dataType).toEqual({
      kind: "list",
      itemType: { kind: "object", entityQualifiedName: "Procurement.PurchaseRequest" },
    });

    const first = updateRetrieveObjectRange(schema, retrieve.id, { kind: "first", officialType: "Microflows$ConstantRange", value: "first" });
    expect(buildMicroflowVariableIndex(first).objectOutputs.purchaseRequests.dataType).toEqual({ kind: "object", entityQualifiedName: "Procurement.PurchaseRequest" });
  });

  it("Commit and Delete selectors can be backed by only Object or List<Object> variables", () => {
    const createObject = actionObject("activity:objectCreate", "create-object");
    const retrieve = actionObject("activity:objectRetrieve", "retrieve-object");
    const commit = actionObject("activity:objectCommit", "commit-object");
    const del = actionObject("activity:objectDelete", "delete-object");
    const retrieveSource = {
      kind: "database" as const,
      officialType: "Microflows$DatabaseRetrieveSource" as const,
      entityQualifiedName: "Procurement.PurchaseRequest",
      xPathConstraint: null,
      sortItemList: { items: [] },
      range: { kind: "all" as const, officialType: "Microflows$ConstantRange" as const, value: "all" as const },
    };
    let schema = updateObjectOutputVariable(
      updateObjectActionEntity(schemaWith([createObject, retrieve, commit, del]), createObject.id, { qualifiedName: "Procurement.PurchaseRequest" }),
      createObject.id,
      { name: "purchaseRequest" },
    );
    schema = updateObjectOutputVariable(updateRetrieveObjectSource(schema, retrieve.id, retrieveSource), retrieve.id, { name: "purchaseRequests" });
    schema = updateCommitObjectTarget(schema, commit.id, { name: "purchaseRequest" });
    schema = updateDeleteObjectTarget(schema, del.id, { name: "purchaseRequests" });

    expect(getObjectVariables(schema).map(variable => variable.name)).toEqual(["purchaseRequest"]);
    expect(getListObjectVariables(schema).map(variable => variable.name)).toEqual(["purchaseRequests"]);
  });

  it("keeps stale entity configuration and reports a warning", () => {
    const createObject = actionObject("activity:objectCreate", "create-object");
    const schema = updateObjectOutputVariable(
      updateObjectActionEntity(schemaWith([createObject]), createObject.id, { qualifiedName: "Procurement.DeletedEntity" }),
      createObject.id,
      { name: "staleObject" },
    );

    expect(JSON.stringify(schema)).toContain("Procurement.DeletedEntity");
    expect(buildObjectActionWarnings(schema, createObject.id, EMPTY_MICROFLOW_METADATA_CATALOG).some(warning => warning.includes("stale entity"))).toBe(true);
  });

  it("keeps A/B object variable indexes isolated", () => {
    const aCreate = actionObject("activity:objectCreate", "a-create-object");
    const bCreate = actionObject("activity:objectCreate", "b-create-object");
    const a = updateObjectOutputVariable(
      updateObjectActionEntity({ ...schemaWith([aCreate]), id: "MF_A", stableId: "MF_A" }, aCreate.id, { qualifiedName: "Procurement.PurchaseRequest" }),
      aCreate.id,
      { name: "purchaseRequest" },
    );
    const b = updateObjectOutputVariable(
      updateObjectActionEntity({ ...schemaWith([bCreate]), id: "MF_B", stableId: "MF_B" }, bCreate.id, { qualifiedName: "Procurement.Department" }),
      bCreate.id,
      { name: "department" },
    );

    expect(buildMicroflowVariableIndex(a).objectOutputs.purchaseRequest).toBeDefined();
    expect(buildMicroflowVariableIndex(a).objectOutputs.department).toBeUndefined();
    expect(buildMicroflowVariableIndex(b).objectOutputs.department).toBeDefined();
    expect(buildMicroflowVariableIndex(b).objectOutputs.purchaseRequest).toBeUndefined();
  });

  it("removes and duplicates Create/Retrieve Object output variables with isolated names", () => {
    const createObject = actionObject("activity:objectCreate", "create-object");
    const retrieve = actionObject("activity:objectRetrieve", "retrieve-object");
    const retrieveSource = {
      kind: "database" as const,
      officialType: "Microflows$DatabaseRetrieveSource" as const,
      entityQualifiedName: "Procurement.PurchaseRequest",
      xPathConstraint: null,
      sortItemList: { items: [] },
      range: { kind: "first" as const, officialType: "Microflows$ConstantRange" as const, value: "first" as const },
    };
    const schema = updateObjectOutputVariable(
      updateRetrieveObjectSource(
        updateObjectOutputVariable(
          updateObjectActionEntity(schemaWith([createObject, retrieve]), createObject.id, { qualifiedName: "Procurement.PurchaseRequest" }),
          createObject.id,
          { name: "purchaseRequest" },
        ),
        retrieve.id,
        retrieveSource,
      ),
      retrieve.id,
      { name: "purchaseRequestFromRetrieve" },
    );
    const deleted = deleteObject(schema, createObject.id);
    const duplicated = duplicateObject(schema, retrieve.id);

    expect(buildMicroflowVariableIndex(deleted).objectOutputs.purchaseRequest).toBeUndefined();
    expect(Object.keys(buildMicroflowVariableIndex(duplicated).objectOutputs).sort()).toEqual(["purchaseRequest", "purchaseRequestFromRetrieve", "purchaseRequestFromRetrieve_Copy"]);
  });

  it("does not introduce Sales demo defaults in new object activity nodes", () => {
    const serialized = JSON.stringify([
      actionObject("activity:objectCreate", "create-object"),
      actionObject("activity:objectRetrieve", "retrieve-object"),
      actionObject("activity:objectChange", "change-object"),
      actionObject("activity:objectCommit", "commit-object"),
      actionObject("activity:objectDelete", "delete-object"),
    ]);

    expect(serialized).not.toContain("Sales.");
    expect(serialized).not.toContain("Order");
    expect(serialized).not.toContain("Customer");
  });
});
