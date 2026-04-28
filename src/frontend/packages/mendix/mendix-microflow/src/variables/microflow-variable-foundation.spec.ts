import { describe, expect, it } from "vitest";
import type { MicroflowActionActivity, MicroflowAuthoringSchema, MicroflowExpression } from "../schema";
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

function schema(objects: MicroflowActionActivity[] = [createVariable(), changeVariable()]): MicroflowAuthoringSchema {
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
    flows: [],
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
    expect(buildMicroflowExpressionContext(schema()).map(item => item.name)).toContain("approvalLevel");
  });

  it("detects duplicate variable names and parameter conflicts", () => {
    expect(getVariableNameConflicts(schema([createVariable("a1", "amount")]), "amount", "a1")).toContain("Conflicts with parameter \"amount\".");
    expect(getVariableNameConflicts(schema([createVariable("a1", "approvalLevel"), createVariable("a2", "approvalLevel")]), "approvalLevel", "a1")).toContain("Conflicts with variable \"approvalLevel\".");
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

  it("does not mutate a different microflow schema when one schema changes", () => {
    const a = schema();
    const b = { ...schema([createVariable("b1", "riskLevel")]), id: "mf-b", stableId: "mf-b", name: "MF_B", displayName: "MF_B" };
    const nextA = updateCreateVariableConfig(a, "obj-create", { variableName: "routeName" });

    expect(buildMicroflowVariableIndex(nextA).byName?.routeName).toBeTruthy();
    expect(buildMicroflowVariableIndex(b).byName?.riskLevel).toBeTruthy();
    expect(buildMicroflowVariableIndex(b).byName?.routeName).toBeUndefined();
  });
});
