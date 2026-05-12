import { describe, expect, it } from "vitest";

import { createObjectFromRegistry, createSequenceFlow, deleteObject, duplicateObject, updateObject } from "../../adapters";
import { sampleMicroflowSchema } from "../../schema/sample";
import { defaultMicroflowNodeRegistry, getMicroflowNodeRegistryKey } from "../../node-registry";
import {
  buildMicroflowVariableIndex,
  getCompatibleListVariables,
  updateAggregateFunction,
  updateAggregateListSource,
  updateAggregateResultVariable,
  updateChangeListOperation,
  updateChangeListTarget,
  updateCreateListElementType,
  updateCreateListVariableName,
  updateListOperationOutputVariable,
  updateListOperationSource,
} from "../../variables";
import {
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

function schemaWith(objects: MicroflowObject[] = [], flows: MicroflowSchema["flows"] = []): MicroflowSchema {
  return {
    ...sampleMicroflowSchema,
    id: "MF_LIST_COLLECTION_TEST",
    stableId: "MF_LIST_COLLECTION_TEST",
    parameters: [],
    objectCollection: { ...sampleMicroflowSchema.objectCollection, objects },
    flows,
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

describe("microflow list collection foundation", () => {
  it("syncs Create List variable name and elementType into variable index", () => {
    const createList = actionObject("activity:listCreate", "create-list");
    const schema = updateCreateListElementType(
      updateCreateListVariableName(schemaWith([createList]), createList.id, "approvalUsers"),
      createList.id,
      { kind: "string" },
    );
    const index = buildMicroflowVariableIndex(schema);

    expect(index.listOutputs.approvalUsers.dataType).toEqual({ kind: "list", itemType: { kind: "string" } });
    expect(index.listOutputs.approvalUsers.source.kind).toBe("createList");
    expect(index.listOutputs.approvalUsers.source.kind === "createList" ? index.listOutputs.approvalUsers.source.objectId : undefined).toBe(createList.id);
  });

  it("removes Create List variable when node is deleted", () => {
    const createList = actionObject("activity:listCreate", "create-list");
    const schema = updateCreateListVariableName(schemaWith([createList]), createList.id, "approvalUsers");
    const deleted = deleteObject(schema, createList.id);

    expect(buildMicroflowVariableIndex(deleted).listOutputs.approvalUsers).toBeUndefined();
  });

  it("duplicates Create List with unique list variable id and name", () => {
    const createList = actionObject("activity:listCreate", "create-list");
    const schema = updateCreateListVariableName(schemaWith([createList]), createList.id, "approvalUsers");
    const duplicated = duplicateObject(schema, createList.id);
    const listVariables = Object.values(buildMicroflowVariableIndex(duplicated).listOutputs);

    expect(listVariables.map(variable => variable.name).sort()).toEqual(["approvalUsers", "approvalUsers_Copy"]);
    expect(new Set(listVariables.map(variable => variable.source.kind === "createList" ? variable.source.actionId : variable.id)).size).toBe(2);
  });

  it("Change List can target only list variables from the current index", () => {
    const createList = actionObject("activity:listCreate", "create-list");
    const changeList = actionObject("activity:listChange", "change-list");
    const schema = updateChangeListOperation(
      updateChangeListTarget(
        updateCreateListVariableName(schemaWith([createList, changeList]), createList.id, "approvalUsers"),
        changeList.id,
        "approvalUsers",
      ),
      changeList.id,
      "add",
    );
    const changed = schema.objectCollection.objects.find(object => object.id === changeList.id);
    const options = getCompatibleListVariables(schema);

    expect(options.map(option => option.name)).toEqual(["approvalUsers"]);
    expect(changed?.kind === "actionActivity" && changed.action.kind === "changeList" ? changed.action.targetListVariableName : undefined).toBe("approvalUsers");
  });

  it("rewrites downstream list references when renaming Create List, Aggregate result and List Operation outputs", () => {
    const createList = actionObject("activity:listCreate", "create-list");
    const changeList = actionObject("activity:listChange", "change-list");
    const aggregateList = actionObject("activity:listAggregate", "aggregate-list");
    const changeVariable = actionObject("activity:variableChange", "change-variable");
    const listOperation = actionObject("activity:listOperation", "list-operation");
    const downstreamChangeList = actionObject("activity:listChange", "downstream-change-list");
    const flows = [
      createSequenceFlow({ originObjectId: createList.id, destinationObjectId: changeList.id }),
      createSequenceFlow({ originObjectId: aggregateList.id, destinationObjectId: changeVariable.id }),
      createSequenceFlow({ originObjectId: listOperation.id, destinationObjectId: downstreamChangeList.id }),
    ];
    let schema = updateChangeListOperation(
      updateChangeListTarget(
        updateCreateListVariableName(schemaWith([createList, changeList, aggregateList, changeVariable, listOperation, downstreamChangeList], flows), createList.id, "approvalUsers"),
        changeList.id,
        "approvalUsers",
      ),
      changeList.id,
      "addAll",
    );
    schema = updateObject(schema, changeList.id, object => object.kind === "actionActivity" && object.action.kind === "changeList"
      ? { ...object, action: { ...object.action, sourceListVariableName: "approvalUsers", sourceListVariable: "approvalUsers" } }
      : object);
    schema = updateAggregateResultVariable(
      updateAggregateFunction(
        updateAggregateListSource(schema, aggregateList.id, "approvalUsers"),
        aggregateList.id,
        "count",
      ),
      aggregateList.id,
      { name: "approvalUserCount", dataType: { kind: "integer" } },
    );
    schema = updateObject(schema, changeVariable.id, object => object.kind === "actionActivity" && object.action.kind === "changeVariable"
      ? {
          ...object,
          action: {
            ...object.action,
            targetVariableName: "approvalUserCount",
            newValueExpression: { raw: "$approvalUserCount + 1", references: { variables: ["$approvalUserCount"], entities: [], attributes: [], associations: [], enumerations: [], functions: [] }, diagnostics: [] },
          },
        }
      : object);
    schema = updateListOperationOutputVariable(
      updateListOperationSource(schema, listOperation.id, "approvalUsers"),
      listOperation.id,
      { name: "filteredApprovalUsers" },
    );
    schema = updateObject(schema, downstreamChangeList.id, object => object.kind === "actionActivity" && object.action.kind === "changeList"
      ? { ...object, action: { ...object.action, targetListVariableName: "filteredApprovalUsers", sourceListVariableName: "filteredApprovalUsers", sourceListVariable: "filteredApprovalUsers", operation: "addAll" } }
      : object);

    const renamedCreate = updateCreateListVariableName(schema, createList.id, "reviewers");
    const renamedAggregate = updateAggregateResultVariable(renamedCreate, aggregateList.id, { name: "reviewerCount", dataType: { kind: "integer" } });
    const renamedListOperation = updateListOperationOutputVariable(renamedAggregate, listOperation.id, { name: "filteredReviewers" });
    const nextChangeList = renamedListOperation.objectCollection.objects.find(object => object.id === changeList.id);
    const nextChangeVariable = renamedListOperation.objectCollection.objects.find(object => object.id === changeVariable.id);
    const nextDownstreamChangeList = renamedListOperation.objectCollection.objects.find(object => object.id === downstreamChangeList.id);

    expect(nextChangeList?.kind === "actionActivity" && nextChangeList.action.kind === "changeList" ? nextChangeList.action.targetListVariableName : undefined).toBe("reviewers");
    expect(nextChangeList?.kind === "actionActivity" && nextChangeList.action.kind === "changeList" ? nextChangeList.action.sourceListVariableName : undefined).toBe("reviewers");
    expect(nextChangeVariable?.kind === "actionActivity" && nextChangeVariable.action.kind === "changeVariable" ? nextChangeVariable.action.targetVariableName : undefined).toBe("reviewerCount");
    expect(nextChangeVariable?.kind === "actionActivity" && nextChangeVariable.action.kind === "changeVariable" ? nextChangeVariable.action.newValueExpression.raw : undefined).toBe("$reviewerCount + 1");
    expect(nextDownstreamChangeList?.kind === "actionActivity" && nextDownstreamChangeList.action.kind === "changeList" ? nextDownstreamChangeList.action.targetListVariableName : undefined).toBe("filteredReviewers");
    expect(nextDownstreamChangeList?.kind === "actionActivity" && nextDownstreamChangeList.action.kind === "changeList" ? nextDownstreamChangeList.action.sourceListVariableName : undefined).toBe("filteredReviewers");
  });

  it("Aggregate List result variable enters the variable index", () => {
    const createList = actionObject("activity:listCreate", "create-list");
    const aggregateList = actionObject("activity:listAggregate", "aggregate-list");
    const schema = updateAggregateResultVariable(
      updateAggregateFunction(
        updateAggregateListSource(
          updateCreateListVariableName(schemaWith([createList, aggregateList]), createList.id, "approvalUsers"),
          aggregateList.id,
          "approvalUsers",
        ),
        aggregateList.id,
        "count",
      ),
      aggregateList.id,
      { name: "approvalUserCount", dataType: { kind: "integer" } },
    );
    const index = buildMicroflowVariableIndex(schema);

    expect(index.localVariables.approvalUserCount.dataType).toEqual({ kind: "integer" });
    expect(index.localVariables.approvalUserCount.source.kind).toBe("aggregateList");
  });

  it("List Operation output variable enters the variable index and inherits element type", () => {
    const createList = actionObject("activity:listCreate", "create-list");
    const listOperation = actionObject("activity:listOperation", "list-operation");
    const schema = updateListOperationOutputVariable(
      updateListOperationSource(
        updateCreateListVariableName(schemaWith([createList, listOperation]), createList.id, "approvalUsers"),
        listOperation.id,
        "approvalUsers",
      ),
      listOperation.id,
      { name: "filteredApprovalUsers" },
    );
    const index = buildMicroflowVariableIndex(schema);

    expect(index.listOutputs.filteredApprovalUsers.dataType).toEqual({ kind: "list", itemType: { kind: "string" } });
    expect(index.listOutputs.filteredApprovalUsers.source.kind).toBe("listOperation");
  });

  it("keeps A/B schema list variables isolated", () => {
    const aCreate = actionObject("activity:listCreate", "a-create-list");
    const bCreate = actionObject("activity:listCreate", "b-create-list");
    const a = updateCreateListVariableName({ ...schemaWith([aCreate]), id: "MF_A", stableId: "MF_A" }, aCreate.id, "approvalUsers");
    const b = updateCreateListVariableName({ ...schemaWith([bCreate]), id: "MF_B", stableId: "MF_B" }, bCreate.id, "requestUsers");

    expect(buildMicroflowVariableIndex(a).listOutputs.approvalUsers).toBeDefined();
    expect(buildMicroflowVariableIndex(a).listOutputs.requestUsers).toBeUndefined();
    expect(buildMicroflowVariableIndex(b).listOutputs.requestUsers).toBeDefined();
    expect(buildMicroflowVariableIndex(b).listOutputs.approvalUsers).toBeUndefined();
  });

  it("does not introduce demo defaults", () => {
    const createList = actionObject("activity:listCreate", "create-list");
    const serialized = JSON.stringify(createList);

    expect(serialized).not.toContain("Sales.");
    expect(serialized).not.toContain("OrderList");
    expect(serialized).not.toContain("ProductList");
    expect(serialized).not.toContain("CustomerList");
  });
});

