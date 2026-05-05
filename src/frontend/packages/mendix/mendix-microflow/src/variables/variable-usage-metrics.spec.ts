import { describe, expect, it } from "vitest";
import type { MicroflowActionActivity, MicroflowAuthoringSchema } from "../schema";
import { EMPTY_MICROFLOW_METADATA_CATALOG } from "../metadata/metadata-catalog";
import { buildVariableIndex } from "./variable-index";
import { buildVariableUsageMetrics } from "./variable-usage-metrics";

function createActivity(id: string, variableName: string, initialRaw: string): MicroflowActionActivity {
  return {
    id,
    stableId: id,
    kind: "actionActivity",
    officialType: "Microflows$ActionActivity",
    caption: "Create Variable",
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
      initialValue: { raw: initialRaw, referencedVariables: [], references: { variables: [], entities: [], attributes: [], associations: [], enumerations: [], functions: [] }, diagnostics: [] },
      readonly: false,
    },
  };
}

function schema(): MicroflowAuthoringSchema {
  return {
    schemaVersion: "1",
    mendixProfile: "mx10",
    id: "mf-usage",
    stableId: "mf-usage",
    name: "MF_USAGE",
    displayName: "MF_USAGE",
    moduleId: "module",
    parameters: [],
    returnType: { kind: "void" },
    objectCollection: {
      id: "root",
      officialType: "Microflows$MicroflowObjectCollection",
      objects: [
        createActivity("node-a", "riskScore", "if $.riskScore > 90 then \"high\" else \"low\""),
        createActivity("node-b", "approved", "if $riskScore = \"high\" then \"Y\" else \"N\""),
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

describe("variable usage metrics", () => {
  it("counts both $var and $.var references", () => {
    const s = schema();
    const index = buildVariableIndex({ schema: s, metadata: EMPTY_MICROFLOW_METADATA_CATALOG });
    const metrics = buildVariableUsageMetrics({ schema: s, variableIndex: index });
    expect(metrics.riskScore?.referenceCount).toBeGreaterThanOrEqual(2);
  });
});

