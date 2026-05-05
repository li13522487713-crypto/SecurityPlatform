import { describe, expect, it } from "vitest";

import type { MicroflowActionActivity, MicroflowAuthoringSchema } from "../schema";
import { getVariablesForExpressionFromIndex, resolveVariableReferenceFromIndex } from "./variable-scope-query";
import { buildVariableIndex } from "./variable-index";

function actionActivity(id: string, variableName: string): MicroflowActionActivity {
  return {
    id,
    stableId: id,
    kind: "actionActivity",
    officialType: "Microflows$ActionActivity",
    caption: "Create",
    autoGenerateCaption: false,
    backgroundColor: "default",
    disabled: false,
    relativeMiddlePoint: { x: 0, y: 0 },
    size: { width: 178, height: 76 },
    editor: {},
    action: {
      id: `action-${id}`,
      kind: "createVariable",
      officialType: "Microflows$CreateVariableAction",
      caption: "createVariable",
      errorHandlingType: "rollback",
      documentation: "",
      editor: { category: "variable", iconKey: "variable", availability: "supported" },
      variableName,
      dataType: { kind: "string" },
      initialValue: { raw: "\"ok\"", referencedVariables: [], references: { variables: [], entities: [], attributes: [], associations: [], enumerations: [], functions: [] }, diagnostics: [] },
      readonly: false,
    },
  };
}

function schema(withLink: boolean): MicroflowAuthoringSchema {
  const createA = actionActivity("node-a", "aOut");
  const useB = actionActivity("node-b", "bOut");
  return {
    schemaVersion: "1",
    mendixProfile: "mx10",
    id: "mf-scope",
    stableId: "mf-scope",
    name: "MF_SCOPE",
    displayName: "MF_SCOPE",
    moduleId: "module-a",
    parameters: [],
    returnType: { kind: "void" },
    objectCollection: {
      id: "root",
      officialType: "Microflows$MicroflowObjectCollection",
      objects: [createA, useB],
      flows: withLink ? [{
        id: "flow-a-b",
        kind: "sequence",
        officialType: "Microflows$SequenceFlow",
        originObjectId: "node-a",
        destinationObjectId: "node-b",
        caseValue: undefined,
        isErrorHandler: false,
        errorType: "all",
        documentation: "",
        relativeMiddlePoint: { x: 0, y: 0 },
        size: { width: 0, height: 0 },
        backgroundColor: "default",
        disabled: false,
        editor: { edgeKind: "sequence" },
      }] : [],
    },
    flows: withLink ? [{
      id: "flow-a-b",
      kind: "sequence",
      officialType: "Microflows$SequenceFlow",
      originObjectId: "node-a",
      destinationObjectId: "node-b",
      caseValue: undefined,
      isErrorHandler: false,
      errorType: "all",
      documentation: "",
      relativeMiddlePoint: { x: 0, y: 0 },
      size: { width: 0, height: 0 },
      backgroundColor: "default",
      disabled: false,
      editor: { edgeKind: "sequence" },
    }] : [],
    security: { allowedModuleRoleIds: [], allowedRoleNames: [], applyEntityAccess: true },
    concurrency: { allowConcurrentExecution: true },
    exposure: { exportLevel: "module", markAsUsed: false },
    validation: { issues: [] },
    editor: { viewport: { x: 0, y: 0, zoom: 1 }, zoom: 1, selection: {} },
    audit: { version: "1", status: "draft" },
  };
}

describe("variable scope by graph links", () => {
  it("only exposes upstream outputs when a link path exists", () => {
    const linked = schema(true);
    const unlinked = schema(false);
    const linkedNames = getVariablesForExpressionFromIndex(linked, buildVariableIndex(linked), { objectId: "node-b" }).map(item => item.name);
    const unlinkedNames = getVariablesForExpressionFromIndex(unlinked, buildVariableIndex(unlinked), { objectId: "node-b" }).map(item => item.name);

    expect(linkedNames).toContain("aOut");
    expect(unlinkedNames).not.toContain("aOut");
  });

  it("resolves $.variable alias the same as $variable", () => {
    const linked = schema(true);
    const index = buildVariableIndex(linked);
    const withDollar = resolveVariableReferenceFromIndex(linked, index, { objectId: "node-b" }, "$aOut");
    const withJsonRootDollar = resolveVariableReferenceFromIndex(linked, index, { objectId: "node-b" }, "$.aOut");

    expect(withDollar?.name).toBe("aOut");
    expect(withJsonRootDollar?.name).toBe("aOut");
  });
});

