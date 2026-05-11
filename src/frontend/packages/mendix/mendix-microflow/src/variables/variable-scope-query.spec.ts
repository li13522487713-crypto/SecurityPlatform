import { describe, expect, it } from "vitest";

import { createObjectFromRegistry } from "../adapters";
import { defaultMicroflowNodeRegistry, getMicroflowNodeRegistryKey } from "../node-registry";
import type { MicroflowActionActivity, MicroflowAuthoringSchema } from "../schema";
import type { MicroflowObject } from "../schema/types";
import { EMPTY_MICROFLOW_METADATA_CATALOG } from "../metadata/metadata-catalog";
import { getVariablesForExpressionFromIndex, resolveVariableReferenceFromIndex } from "./variable-scope-query";
import { buildVariableIndex } from "./variable-index";

function registry(key: string) {
  const item = defaultMicroflowNodeRegistry.find(entry => getMicroflowNodeRegistryKey(entry) === key || entry.type === key);
  if (!item) {
    throw new Error(`Missing registry item ${key}`);
  }
  return item;
}

function objectFrom(key: string, id: string, x = 0, y = 0): MicroflowObject {
  return createObjectFromRegistry(registry(key), { x, y }, id);
}

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
  const flow = {
    id: "flow-a-b",
    kind: "sequence",
    officialType: "Microflows$SequenceFlow",
    originObjectId: "node-a",
    destinationObjectId: "node-b",
    caseValues: [],
    isErrorHandler: false,
    relativeMiddlePoint: { x: 0, y: 0 },
    size: { width: 0, height: 0 },
    backgroundColor: "default",
    disabled: false,
    editor: { edgeKind: "sequence" },
  } as any;
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
      flows: withLink ? [flow] : [],
    },
    flows: withLink ? [flow] : [],
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
    const linkedNames = getVariablesForExpressionFromIndex(linked, buildVariableIndex({ schema: linked, metadata: EMPTY_MICROFLOW_METADATA_CATALOG }), { objectId: "node-b" }).map(item => item.name);
    const unlinkedNames = getVariablesForExpressionFromIndex(unlinked, buildVariableIndex({ schema: unlinked, metadata: EMPTY_MICROFLOW_METADATA_CATALOG }), { objectId: "node-b" }).map(item => item.name);

    expect(linkedNames).toContain("aOut");
    expect(unlinkedNames).not.toContain("aOut");
  });

  it("resolves $.variable alias the same as $variable", () => {
    const linked = schema(true);
    const index = buildVariableIndex({ schema: linked, metadata: EMPTY_MICROFLOW_METADATA_CATALOG });
    const withDollar = resolveVariableReferenceFromIndex(linked, index, { objectId: "node-b" }, "$aOut");
    const withJsonRootDollar = resolveVariableReferenceFromIndex(linked, index, { objectId: "node-b" }, "$.aOut");

    expect(withDollar?.name).toBe("aOut");
    expect(withJsonRootDollar?.name).toBe("aOut");
  });

  it("adds loop iterator variables and $currentIndex inside loop body scope", () => {
    const inner = actionActivity("loop-inner", "innerOut");
    const loop = objectFrom("loop", "loop-node") as Extract<MicroflowObject, { kind: "loopedActivity" }>;
    loop.objectCollection = {
      ...loop.objectCollection,
      id: "loop-body",
      objects: [inner],
      flows: [],
    };
    loop.loopSource = {
      kind: "iterableList",
      officialType: "Microflows$IterableList",
      listVariableName: "orders",
      iteratorVariableName: "orderItem",
      currentIndexVariableName: "$currentIndex",
      iteratorVariableDataType: { kind: "string" },
    };
    const createList = actionActivity("node-orders", "orders");
    createList.action = {
      id: "action-orders",
      kind: "createList",
      officialType: "Microflows$CreateListAction",
      caption: "Create List",
      errorHandlingType: "rollback",
      documentation: "",
      editor: { category: "list", iconKey: "list", availability: "supported" },
      outputListVariableName: "orders",
      elementType: { kind: "string" },
      itemType: { kind: "string" },
    } as any;
    const loopSchema = {
      ...schema(true),
      objectCollection: {
        ...schema(true).objectCollection,
        objects: [createList, loop],
      },
      flows: [],
    };
    const index = buildVariableIndex({ schema: loopSchema, metadata: EMPTY_MICROFLOW_METADATA_CATALOG });
    const names = getVariablesForExpressionFromIndex(loopSchema, index, { objectId: "loop-inner" }).map(item => item.name);

    expect(names).toContain("orderItem");
    expect(names).toContain("$currentIndex");
  });
});
