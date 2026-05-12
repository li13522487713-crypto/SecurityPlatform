import { describe, expect, it } from "vitest";
import type { MicroflowActionActivity, MicroflowAuthoringSchema, MicroflowExpression } from "../schema";
import { createSequenceFlow } from "../adapters";
import {
  buildMicroflowExpressionContext,
  buildMicroflowVariableIndex,
  duplicateCreateVariableObject,
  findVariableTextReferences,
  getStaleVariableReferences,
  getVariableNameConflicts,
  removeMicroflowVariableDefinition,
  updateChangeVariableConfig,
  updateCreateVariableConfig,
} from "./microflow-variable-foundation";

function expression(raw: string): MicroflowExpression {
  return {
    raw,
    referencedVariables: [],
    references: { variables: [], entities: [], attributes: [], associations: [], enumerations: [], functions: [] },
    diagnostics: [],
  };
}

function activity(id: string, action: MicroflowActionActivity["action"]): MicroflowActionActivity {
  return {
    id,
    stableId: id,
    kind: "actionActivity",
    officialType: "Microflows$ActionActivity",
    caption: action.caption ?? action.kind,
    autoGenerateCaption: false,
    backgroundColor: "default",
    disabled: false,
    relativeMiddlePoint: { x: 0, y: 0 },
    size: { width: 178, height: 76 },
    editor: {},
    action,
  };
}

function baseAction(kind: string, id: string) {
  return {
    id,
    kind,
    caption: kind,
    errorHandlingType: "rollback" as const,
    documentation: "",
    editor: { category: "variable" as const, iconKey: "variable", availability: "supported" as const },
  };
}

function createVariable(id = "action-create", variableName = "approvalLevel") {
  return activity("obj-create", {
    ...baseAction("createVariable", id),
    officialType: "Microflows$CreateVariableAction",
    kind: "createVariable",
    variableName,
    dataType: { kind: "string" },
    initialValue: expression("\"L1\""),
    readonly: false,
  });
}

function changeVariable(id = "action-change", targetVariableName = "approvalLevel") {
  return activity("obj-change", {
    ...baseAction("changeVariable", id),
    officialType: "Microflows$ChangeVariableAction",
    kind: "changeVariable",
    targetVariableName,
    newValueExpression: expression("\"L2\""),
  });
}

function schema(objects: MicroflowActionActivity[] = [createVariable(), changeVariable()], flows = [createSequenceFlow({ originObjectId: "obj-create", destinationObjectId: "obj-change" })]): MicroflowAuthoringSchema {
  return {
    schemaVersion: "1",
    mendixProfile: "mx10",
    id: "mf-a",
    stableId: "mf-a",
    name: "MF_A",
    displayName: "MF_A",
    moduleId: "module-a",
    parameters: [{ id: "param-amount", name: "amount", dataType: { kind: "decimal" }, required: true }],
    returnType: { kind: "void" },
    objectCollection: { id: "root", officialType: "Microflows$MicroflowObjectCollection", objects, flows: [] },
    flows,
    security: { allowedModuleRoleIds: [], allowedRoleNames: [], applyEntityAccess: true },
    concurrency: { allowConcurrentExecution: true },
    exposure: { exportLevel: "module", markAsUsed: false },
    validation: { issues: [] },
    editor: {
      viewport: { x: 0, y: 0, zoom: 1 },
      zoom: 1,
      selection: {},
    },
    audit: { version: "1", status: "draft" },
  };
}

describe("microflow variable foundation", () => {
  it("builds parameter and Create Variable symbols from the current schema", () => {
    const index = buildMicroflowVariableIndex(schema());
    expect(index.parameters.amount.name).toBe("amount");
    expect(index.byName?.approvalLevel?.[0]?.source.kind).toBe("createVariable");
    expect(index.byName?.["$currentUser"]?.[0]?.source.kind).toBe("system");
    expect(index.byName?.["$currentSession"]?.[0]?.source.kind).toBe("system");
    expect(buildMicroflowExpressionContext(schema()).map(item => item.name)).toContain("approvalLevel");
  });

  it("detects duplicate variable names and parameter conflicts", () => {
    expect(getVariableNameConflicts(schema([createVariable("a1", "amount")]), "amount", "a1")).toContain("Conflicts with parameter \"amount\".");
    expect(getVariableNameConflicts(schema([createVariable("a1", "approvalLevel"), createVariable("a2", "approvalLevel")]), "approvalLevel", "a1")).toContain("Conflicts with variable \"approvalLevel\".");
    expect(getVariableNameConflicts(schema([createVariable("a1", "approvalLevel")]), "latestHttpResponse", "a1")).toContain("Conflicts with reserved system variable \"$latestHttpResponse\".");
    expect(getVariableNameConflicts(schema([createVariable("a1", "approvalLevel")]), "latestSoapFault", "a1")).toContain("Conflicts with reserved system variable \"$latestSoapFault\".");
  });

  it("updates Create Variable and Change Variable configs immutably", () => {
    const original = schema();
    const renamed = updateCreateVariableConfig(original, "obj-create", {
      variableName: "approvalLevel2",
      initialValue: expression("\"L1\""),
      documentation: "approval routing level",
    });
    const changed = updateChangeVariableConfig(renamed, "obj-change", {
      targetVariableName: "approvalLevel2",
      newValueExpression: expression("\"L2\""),
    });

    expect(original.objectCollection.objects[0]).not.toBe(changed.objectCollection.objects[0]);
    expect(buildMicroflowVariableIndex(changed).byName?.approvalLevel2?.[0]?.name).toBe("approvalLevel2");
    expect(getStaleVariableReferences(changed)).toHaveLength(0);
  });

  it("rewrites downstream references when updateCreateVariableConfig renames a variable", () => {
    const original = schema([
      createVariable("action-create", "approvalLevel"),
      activity("obj-end", {
        ...baseAction("changeVariable", "action-end"),
        officialType: "Microflows$ChangeVariableAction",
        kind: "changeVariable",
        targetVariableName: "approvalLevel",
        newValueExpression: expression("$approvalLevel + 1"),
      }),
    ], [createSequenceFlow({ originObjectId: "obj-create", destinationObjectId: "obj-end" })]);
    const renamed = updateCreateVariableConfig(original, "obj-create", { variableName: "routeName" });
    const changedAction = renamed.objectCollection.objects.find(object => object.id === "obj-end");

    expect(buildMicroflowVariableIndex(renamed).byName?.routeName?.[0]?.name).toBe("routeName");
    expect(changedAction?.kind === "actionActivity" && changedAction.action.kind === "changeVariable" ? changedAction.action.targetVariableName : undefined).toBe("routeName");
    expect(changedAction?.kind === "actionActivity" && changedAction.action.kind === "changeVariable" ? changedAction.action.newValueExpression.raw : undefined).toBe("$routeName + 1");
  });

  it("keeps deleted-variable references visible as stale instead of auto-rewriting expressions", () => {
    const original = schema();
    const removed = removeMicroflowVariableDefinition(original, "obj-create");

    expect(buildMicroflowVariableIndex(removed).byName?.approvalLevel).toBeUndefined();
    expect(getStaleVariableReferences(removed)).toEqual([
      { objectId: "obj-change", actionId: "action-change", fieldPath: "action.targetVariableName", variableName: "approvalLevel" },
    ]);
  });

  it("finds text references and duplicates Create Variable with a fresh name/id", () => {
    const original = schema();
    expect(findVariableTextReferences(original, "approvalLevel").some(reference => reference.fieldPath === "action.newValueExpression")).toBe(false);

    const duplicated = duplicateCreateVariableObject(original, "obj-create");
    const names = (buildMicroflowVariableIndex(duplicated).all ?? [])
      .filter(symbol => symbol.source.kind === "createVariable")
      .map(symbol => symbol.name);

    expect(names).toContain("approvalLevel");
    expect(names.some(name => name !== "approvalLevel" && name.startsWith("approvalLevel"))).toBe(true);
  });

  it("ignores partially configured action expressions when finding variable references", () => {
    const partiallyConfiguredChange = activity("obj-partial-change", {
      ...baseAction("changeVariable", "action-partial-change"),
      officialType: "Microflows$ChangeVariableAction",
      kind: "changeVariable",
      targetVariableName: "approvalLevel",
      newValueExpression: undefined as unknown as MicroflowExpression,
    });
    const partiallyConfiguredCreate = activity("obj-partial-create", {
      ...baseAction("createVariable", "action-partial-create"),
      officialType: "Microflows$CreateVariableAction",
      kind: "createVariable",
      variableName: "routeName",
      dataType: { kind: "string" },
      initialValue: { raw: undefined } as unknown as MicroflowExpression,
      readonly: false,
    });

    expect(() => findVariableTextReferences(schema([createVariable(), partiallyConfiguredChange, partiallyConfiguredCreate]), "approvalLevel")).not.toThrow();
  });

  it("does not mutate a different microflow schema when one schema changes", () => {
    const a = schema();
    const b = { ...schema([createVariable("b1", "riskLevel")]), id: "mf-b", stableId: "mf-b", name: "MF_B", displayName: "MF_B" };
    const nextA = updateCreateVariableConfig(a, "obj-create", { variableName: "routeName" });

    expect(buildMicroflowVariableIndex(nextA).byName?.routeName).toBeTruthy();
    expect(buildMicroflowVariableIndex(b).byName?.riskLevel).toBeTruthy();
    expect(buildMicroflowVariableIndex(b).byName?.routeName).toBeUndefined();
  });

  it("builds variable index even when root object collection is missing", () => {
    const corrupted = { ...schema(), objectCollection: undefined } as unknown as MicroflowAuthoringSchema;

    expect(() => buildMicroflowVariableIndex(corrupted)).not.toThrow();
    expect((buildMicroflowVariableIndex(corrupted).all ?? []).length).toBeGreaterThan(0);
  });

  it("indexes latestSoapFault as System.SoapFault for webServiceCall error handlers", () => {
    const soapCall = activity("obj-soap", {
      ...baseAction("webServiceCall", "action-soap"),
      officialType: "Microflows$WebServiceCallAction",
      kind: "webServiceCall",
      editor: { category: "integration", iconKey: "webServiceCall", availability: "supported" as const },
      endpoint: "https://soap.test/service",
      operation: "SubmitOrder",
      outputVariableName: "soapResult",
    } as never);
    const errorHandler = changeVariable("action-handle-soap", "approvalLevel");
    const errorFlow = {
      ...createSequenceFlow({ originObjectId: soapCall.id, destinationObjectId: errorHandler.id }),
      id: "f-soap-error",
      isErrorHandler: true,
      editor: { edgeKind: "errorHandler" as const },
    };
    const index = buildMicroflowVariableIndex(schema([soapCall, errorHandler], [errorFlow]));

    expect(index.errorVariables.$latestSoapFault.dataType).toEqual({ kind: "object", entityQualifiedName: "System.SoapFault" });
    expect(index.errorVariables.$latestSoapFault.kind).toBe("soapFault");
  });

  it("indexes latestHttpResponse as System.HttpResponse for restCall error handlers", () => {
    const restCall = activity("obj-rest", {
      ...baseAction("restCall", "action-rest"),
      officialType: "Microflows$RestCallAction",
      kind: "restCall",
      editor: { category: "integration", iconKey: "rest", availability: "supported" as const },
      request: {
        method: "GET",
        urlExpression: { raw: "'https://api.test/orders'" },
        headers: [],
        queryParameters: [],
        body: { kind: "none" },
      },
      response: { handling: { kind: "ignore" } },
      timeoutSeconds: 30,
    } as never);
    const errorHandler = changeVariable("action-handle-rest", "approvalLevel");
    const errorFlow = {
      ...createSequenceFlow({ originObjectId: restCall.id, destinationObjectId: errorHandler.id }),
      id: "f-rest-error",
      isErrorHandler: true,
      editor: { edgeKind: "errorHandler" as const },
    };
    const index = buildMicroflowVariableIndex(schema([restCall, errorHandler], [errorFlow]));

    expect(index.errorVariables.$latestHttpResponse.dataType).toEqual({ kind: "object", entityQualifiedName: "System.HttpResponse" });
    expect(index.errorVariables.$latestHttpResponse.kind).toBe("restResponse");
  });
});
