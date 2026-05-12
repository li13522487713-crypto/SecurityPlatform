import { describe, expect, it } from "vitest";

import type { MicroflowAuthoringSchema, MicroflowVariableIndex, MicroflowVariableSymbol } from "../schema";
import { getVariablesBeforeObject } from "./variable-scope-engine";

function schema(): MicroflowAuthoringSchema {
  return {
    schemaVersion: "1",
    mendixProfile: "mx10",
    id: "mf-variable-options",
    stableId: "mf-variable-options",
    name: "MF_VARIABLE_OPTIONS",
    displayName: "MF_VARIABLE_OPTIONS",
    moduleId: "module-a",
    parameters: [],
    returnType: { kind: "void" },
    objectCollection: {
      id: "root",
      officialType: "Microflows$MicroflowObjectCollection",
      objects: [
        {
          id: "node-1",
          stableId: "node-1",
          kind: "actionActivity",
          officialType: "Microflows$ActionActivity",
          caption: "Node 1",
          autoGenerateCaption: false,
          backgroundColor: "default",
          disabled: false,
          relativeMiddlePoint: { x: 0, y: 0 },
          size: { width: 178, height: 76 },
          editor: {},
          action: {
            id: "action-1",
            kind: "createVariable",
            officialType: "Microflows$CreateVariableAction",
            caption: "Create",
            errorHandlingType: "rollback",
            documentation: "",
            editor: { category: "variable", iconKey: "variable", availability: "supported" },
            variableName: "out1",
            dataType: { kind: "string" },
            initialValue: { raw: "\"ok\"", referencedVariables: [], references: { variables: [], entities: [], attributes: [], associations: [], enumerations: [], functions: [] }, diagnostics: [] },
            readonly: false,
          },
        } as any,
      ],
      flows: [],
    },
    flows: [],
    security: { allowedModuleRoleIds: [], allowedRoleNames: [], applyEntityAccess: true },
    concurrency: { allowConcurrentExecution: true },
    exposure: { exportLevel: "module", markAsUsed: false },
    validation: { issues: [] },
    editor: { viewport: { x: 0, y: 0, zoom: 1 }, zoom: 1, selection: {} },
    audit: { version: "1", status: "draft" },
  };
}

function variable(
  name: string,
  overrides: Partial<MicroflowVariableSymbol> = {},
): MicroflowVariableSymbol {
  return {
    id: `var:${name}`,
    name,
    displayName: name,
    kind: "localVariable",
    dataType: { kind: "string" },
    source: { kind: "localVariable", objectId: "node-1", actionId: "action-1" },
    scope: { kind: "downstream", collectionId: "root" },
    visibility: "definite",
    readonly: false,
    ...overrides,
  };
}

function variableIndex(symbols: MicroflowVariableSymbol[]): MicroflowVariableIndex {
  const byName: Record<string, MicroflowVariableSymbol[]> = {};
  for (const symbol of symbols) {
    byName[symbol.name] = [...(byName[symbol.name] ?? []), symbol];
  }
  return {
    all: symbols,
    byName,
    parameters: {},
    localVariables: {},
    objectOutputs: {},
    listOutputs: {},
    loopVariables: {},
    errorVariables: {},
    systemVariables: {},
  };
}

describe("variable scope engine options", () => {
  it("filters maybe variables when includeMaybe is false", () => {
    const index = variableIndex([
      variable("definiteVar"),
      variable("maybeVar", { visibility: "maybe", maybeReason: "" }),
    ]);

    const allVariables = getVariablesBeforeObject(schema(), index, "node-1", { includeMaybe: true });
    const definiteOnly = getVariablesBeforeObject(schema(), index, "node-1", { includeMaybe: false });

    expect(allVariables.some(item => item.name === "maybeVar")).toBe(true);
    expect(allVariables.find(item => item.name === "maybeVar")?.maybeReason).toBe("Variable is not definitely assigned on every normal path to this object.");
    expect(definiteOnly.some(item => item.name === "maybeVar")).toBe(false);
  });

  it("filters system and error-context variables", () => {
    const index = variableIndex([
      variable("definiteVar"),
      variable("$currentUser", {
        kind: "system",
        readonly: true,
        source: { kind: "system", name: "$currentUser" },
        scope: { kind: "global", collectionId: "root" },
      }),
      variable("$latestError", {
        kind: "errorContext",
        source: { kind: "errorContext", flowId: "flow-err", errorVariable: "$latestError" },
        scope: { kind: "errorHandler", collectionId: "root", errorHandlerFlowId: "flow-err" },
      }),
      variable("$latestHttpResponse", {
        kind: "restResponse",
        source: { kind: "restResponse", objectId: "node-1", responseKind: "json" },
        scope: { kind: "errorHandler", collectionId: "root", errorHandlerFlowId: "flow-err" },
      }),
      variable("$latestSoapFault", {
        kind: "soapFault",
        source: { kind: "errorContext", flowId: "flow-err", errorVariable: "$latestSoapFault" },
        scope: { kind: "errorHandler", collectionId: "root", errorHandlerFlowId: "flow-err" },
      }),
    ]);

    const filtered = getVariablesBeforeObject(schema(), index, "node-1", {
      includeSystem: false,
      includeErrorContext: false,
    });

    const names = filtered.map(item => item.name);
    expect(names).toContain("definiteVar");
    expect(names).not.toContain("$currentUser");
    expect(names).not.toContain("$latestError");
    expect(names).not.toContain("$latestHttpResponse");
    expect(names).not.toContain("$latestSoapFault");
  });

  it("supports type filters with writableOnly and readonlyOnly", () => {
    const index = variableIndex([
      variable("stringVar"),
      variable("mutableList", { dataType: { kind: "list", itemType: { kind: "string" } } }),
      variable("readonlyList", { dataType: { kind: "list", itemType: { kind: "string" } }, readonly: true }),
    ]);

    const writableLists = getVariablesBeforeObject(schema(), index, "node-1", {
      allowedTypeKinds: ["list"],
      writableOnly: true,
    });
    const readonlyLists = getVariablesBeforeObject(schema(), index, "node-1", {
      allowedTypeKinds: ["list"],
      readonlyOnly: true,
    });

    expect(writableLists.map(item => item.name)).toEqual(["mutableList"]);
    expect(readonlyLists.map(item => item.name)).toEqual(["readonlyList"]);
  });

  it("respects collectionId while keeping global scope variables", () => {
    const index = variableIndex([
      variable("rootVar", { scope: { kind: "downstream", collectionId: "root" } }),
      variable("loopBodyVar", { scope: { kind: "loop", collectionId: "loop-body", loopObjectId: "loop-1" } }),
      variable("$currentSession", {
        kind: "system",
        readonly: true,
        source: { kind: "system", name: "$currentSession" },
        scope: { kind: "global", collectionId: "root" },
      }),
    ]);

    const inRootCollection = getVariablesBeforeObject(schema(), index, "node-1", { collectionId: "root" });
    const names = inRootCollection.map(item => item.name);

    expect(names).toContain("rootVar");
    expect(names).toContain("$currentSession");
    expect(names).not.toContain("loopBodyVar");
  });

  it("includes unavailable variables when includeUnavailable is true", () => {
    const index = variableIndex([
      variable("activeVar"),
      variable("staleVar", {
        visibility: "unavailable",
        scope: { kind: "downstream", collectionId: "root", startObjectId: "node-missing" },
      }),
    ]);

    const defaultVisible = getVariablesBeforeObject(schema(), index, "node-1");
    const includeUnavailable = getVariablesBeforeObject(schema(), index, "node-1", { includeUnavailable: true });

    expect(defaultVisible.some(item => item.name === "staleVar")).toBe(false);
    expect(includeUnavailable.some(item => item.name === "staleVar")).toBe(true);
    expect(includeUnavailable.find(item => item.name === "staleVar")?.visibility).toBe("unavailable");
  });
});
