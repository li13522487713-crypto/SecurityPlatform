import { describe, expect, it } from "vitest";

import { createObjectFromRegistry, createSequenceFlow, deleteObject, duplicateObject, updateObject } from "../../adapters";
import { sampleMicroflowSchema } from "../../schema/sample";
import { defaultMicroflowNodeRegistry, getMicroflowNodeRegistryKey } from "../../node-registry";
import { renameActionOutputVariable } from "../../schema/utils";
import {
  buildMicroflowVariableIndex,
  getVariableNameConflicts,
  renameMicroflowVariable,
  updateChangeVariableExpression,
  updateChangeVariableTarget,
  updateMicroflowVariableType,
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
    id: "MF_VARIABLE_TEST",
    stableId: "MF_VARIABLE_TEST",
    parameters: [],
    objectCollection: { ...sampleMicroflowSchema.objectCollection, objects },
    flows,
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

function generateDocumentObject(id = "generate-document") {
  return {
    id,
    stableId: id,
    kind: "actionActivity",
    officialType: "Microflows$ActionActivity",
    caption: "generateDocument",
    documentation: "",
    relativeMiddlePoint: { x: 0, y: 0 },
    size: { width: 160, height: 80 },
    ports: [],
    autoGenerateCaption: false,
    backgroundColor: "default",
    action: {
      id: `${id}-action`,
      officialType: "Microflows$GenerateDocumentAction",
      kind: "generateDocument",
      caption: "generateDocument",
      errorHandlingType: "rollback",
      documentation: "",
      editor: { category: "integration", iconKey: "generateDocument", availability: "supported" },
      documentTemplateQualifiedName: "Sales.InvoiceTemplate",
      outputFileDocumentVariableName: "invoiceDoc",
    } as never,
  } as unknown as MicroflowObject;
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

  it("rewrites downstream references and simple end return bindings when renaming Create Variable", () => {
    const createVariable = createVariableObject();
    const changeVariable = changeVariableObject();
    const end = createObjectFromRegistry(registry("endEvent"), { x: 360, y: 0 }, "return-end");
    if (end.kind !== "endEvent") {
      throw new Error("Expected end event.");
    }
    const flowToChange = createSequenceFlow({ originObjectId: createVariable.id, destinationObjectId: changeVariable.id });
    const flowToEnd = createSequenceFlow({ originObjectId: changeVariable.id, destinationObjectId: end.id });
    const schema = {
      ...schemaWith([
        {
          ...createVariable,
          action: {
            ...createVariable.action,
            variableName: "approvalLevel",
            dataType: { kind: "decimal" as const },
          },
        },
        {
          ...changeVariable,
          action: {
            ...changeVariable.action,
            targetVariableName: "approvalLevel",
            newValueExpression: {
              raw: "$approvalLevel + 1",
              inferredType: { kind: "decimal" as const },
              references: { variables: ["$approvalLevel"], entities: [], attributes: [], associations: [], enumerations: [], functions: [] },
              diagnostics: [],
            },
          },
        },
        {
          ...end,
          returnValue: {
            raw: "$approvalLevel",
            inferredType: { kind: "decimal" as const },
            references: { variables: ["$approvalLevel"], entities: [], attributes: [], associations: [], enumerations: [], functions: [] },
            diagnostics: [],
          },
        },
      ], [flowToChange, flowToEnd]),
      returnType: { kind: "decimal" as const },
      returnVariableName: "approvalLevel",
    };

    const renamed = renameMicroflowVariable(schema, createVariable.id, "routeLevel");
    const nextChange = renamed.objectCollection.objects.find(object => object.id === changeVariable.id);
    const nextEnd = renamed.objectCollection.objects.find(object => object.id === end.id);

    expect(buildMicroflowVariableIndex(renamed).localVariables.routeLevel).toBeDefined();
    expect(nextChange?.kind === "actionActivity" && nextChange.action.kind === "changeVariable" ? nextChange.action.targetVariableName : undefined).toBe("routeLevel");
    expect(nextChange?.kind === "actionActivity" && nextChange.action.kind === "changeVariable" ? nextChange.action.newValueExpression.raw : undefined).toBe("$routeLevel + 1");
    expect(nextEnd?.kind === "endEvent" ? nextEnd.returnValue?.raw : undefined).toBe("$routeLevel");
    expect(renamed.returnVariableName).toBe("routeLevel");
  });

  it("keeps shadowed loop iterator references unchanged when renaming Create Variable", () => {
    const createVariable = createVariableObject();
    const loop = createObjectFromRegistry(registry("loop"), { x: 180, y: 0 }, "rename-loop");
    const nestedChange = changeVariableObject("rename-loop-change");
    if (loop.kind !== "loopedActivity" || nestedChange.kind !== "actionActivity" || nestedChange.action.kind !== "changeVariable") {
      throw new Error("Expected loop and nested change variable.");
    }
    const flowToLoop = createSequenceFlow({ originObjectId: createVariable.id, destinationObjectId: loop.id });
    const schema = schemaWith([
      {
        ...createVariable,
        action: {
          ...createVariable.action,
          variableName: "approvalLevel",
          dataType: { kind: "list" as const, itemType: { kind: "decimal" as const } },
        },
      },
      {
        ...loop,
        loopSource: {
          kind: "iterableList",
          officialType: "Microflows$IterableList",
          listVariableName: "$approvalLevel",
          iteratorVariableName: "approvalLevel",
          currentIndexVariableName: "$currentIndex",
          iteratorVariableDataType: { kind: "decimal" as const },
        },
        objectCollection: {
          ...loop.objectCollection,
          objects: [{
            ...nestedChange,
            action: {
              ...nestedChange.action,
              targetVariableName: "approvalLevel",
              newValueExpression: {
                raw: "$approvalLevel + 1",
                inferredType: { kind: "decimal" as const },
                references: { variables: ["$approvalLevel"], entities: [], attributes: [], associations: [], enumerations: [], functions: [] },
                diagnostics: [],
              },
            },
          }],
          flows: [],
        },
      },
    ], [flowToLoop]);

    const renamed = renameMicroflowVariable(schema, createVariable.action.id, "routeLevel");
    const renamedLoop = renamed.objectCollection.objects.find(object => object.id === loop.id);
    const renamedNestedChange = renamedLoop?.kind === "loopedActivity"
      ? renamedLoop.objectCollection.objects.find(object => object.id === nestedChange.id)
      : undefined;

    expect(renamedLoop?.kind === "loopedActivity" && renamedLoop.loopSource.kind === "iterableList" ? renamedLoop.loopSource.listVariableName : undefined).toBe("$routeLevel");
    expect(renamedLoop?.kind === "loopedActivity" && renamedLoop.loopSource.kind === "iterableList" ? renamedLoop.loopSource.iteratorVariableName : undefined).toBe("approvalLevel");
    expect(renamedNestedChange?.kind === "actionActivity" && renamedNestedChange.action.kind === "changeVariable" ? renamedNestedChange.action.targetVariableName : undefined).toBe("approvalLevel");
    expect(renamedNestedChange?.kind === "actionActivity" && renamedNestedChange.action.kind === "changeVariable" ? renamedNestedChange.action.newValueExpression.raw : undefined).toBe("$approvalLevel + 1");
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

  it("detects reserved system variable name conflicts for user-defined variables", () => {
    const first = createVariableObject("create-variable-reserved");
    const schema = renameMicroflowVariable(schemaWith([first]), first.action.id, "latestHttpResponse");
    const index = buildMicroflowVariableIndex(schema);

    expect(index.diagnostics?.some(issue => issue.code === "MF_VARIABLE_NAME_RESERVED")).toBe(true);
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

  it("indexes generate document outputs and rewrites downstream references when renamed", () => {
    const generateDocument = generateDocumentObject();
    const changeVariable = changeVariableObject("change-generated-document");
    const flow = createSequenceFlow({ originObjectId: generateDocument.id, destinationObjectId: changeVariable.id });
    const schema = schemaWith([
      generateDocument,
      {
        ...changeVariable,
        action: {
          ...changeVariable.action,
          targetVariableName: "invoiceDoc",
          newValueExpression: {
            raw: "$invoiceDoc",
            inferredType: { kind: "object" as const, entityQualifiedName: "System.FileDocument" },
            references: { variables: ["$invoiceDoc"], entities: [], attributes: [], associations: [], enumerations: [], functions: [] },
            diagnostics: [],
          },
        },
      },
    ], [flow]);

    const index = buildMicroflowVariableIndex(schema);
    expect(index.byName.invoiceDoc?.[0]?.dataType).toEqual({ kind: "object", entityQualifiedName: "System.FileDocument" });

    const renamed = renameActionOutputVariable(schema, generateDocument.id, "generatedInvoice");
    const nextChange = renamed.objectCollection.objects.find(object => object.id === changeVariable.id);
    expect(nextChange?.kind === "actionActivity" && nextChange.action.kind === "changeVariable" ? nextChange.action.targetVariableName : undefined).toBe("generatedInvoice");
    expect(nextChange?.kind === "actionActivity" && nextChange.action.kind === "changeVariable" ? nextChange.action.newValueExpression.raw : undefined).toBe("$generatedInvoice");
  });
});

