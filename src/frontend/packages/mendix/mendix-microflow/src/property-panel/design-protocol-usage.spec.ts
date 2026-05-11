import { describe, expect, it } from "vitest";

import type { MicroflowDesignSchema } from "../schema/types";
import { buildNodeUsageHighlights } from "../variables";
import { buildDesignPropertyPanelModel } from "./design-protocol-adapter";

function expression(raw: string) {
  return {
    raw,
    referencedVariables: [],
    references: { variables: [], entities: [], attributes: [], associations: [], enumerations: [], functions: [] },
    diagnostics: [],
  };
}

function edge(id: string, sourceNodeID: string, targetNodeID: string) {
  return {
    id,
    sourceNodeID,
    targetNodeID,
    data: {
      flowId: id,
      flowKind: "sequence",
      edgeKind: "sequence",
      caseValues: [],
      isErrorHandler: false,
      line: {
        kind: "orthogonal",
        points: [],
        routing: { mode: "auto", bendPoints: [] },
        style: { strokeType: "solid", strokeWidth: 2, arrow: "target" },
      },
    },
  };
}

function schema(selectedObjectId = "change-level"): MicroflowDesignSchema {
  return {
    schemaVersion: "flowgram.microflow.v1",
    id: "mf-design-usage",
    moduleId: "Sales",
    name: "MF_DesignUsage",
    displayName: "MF Design Usage",
    workflow: {
      nodes: [
        {
          id: "start",
          type: "startEvent",
          data: {
            objectId: "start",
            objectKind: "startEvent",
            collectionId: "root-collection",
            title: "Start",
            officialType: "Microflows$StartEvent",
            disabled: false,
            validationState: "valid",
            issueCount: 0,
          },
          meta: { position: { x: 120, y: 180 }, size: { width: 18, height: 18 } },
        },
        {
          id: "create-level",
          type: "actionActivity",
          data: {
            objectId: "create-level",
            objectKind: "actionActivity",
            officialType: "Microflows$ActionActivity",
            title: "Create Variable",
            subtitle: "createVariable",
            documentation: "",
            collectionId: "root-collection",
            autoGenerateCaption: false,
            backgroundColor: "default",
            disabled: false,
            actionKind: "createVariable",
            action: {
              id: "action-create-level",
              kind: "createVariable",
              officialType: "Microflows$CreateVariableAction",
              errorHandlingType: "rollback",
              documentation: "",
              editor: { category: "variable", iconKey: "variable", availability: "supported" },
              variableName: "approvalLevel",
              dataType: { kind: "string" },
              initialValue: expression("'L1'"),
              readonly: false,
            },
          },
          meta: { nodeDTOType: "actionActivity", collectionId: "root-collection", position: { x: 320, y: 180 }, size: { width: 110, height: 36 } },
        },
        {
          id: "change-level",
          type: "actionActivity",
          data: {
            objectId: "change-level",
            objectKind: "actionActivity",
            officialType: "Microflows$ActionActivity",
            title: "Change Variable",
            subtitle: "changeVariable",
            documentation: "",
            collectionId: "root-collection",
            autoGenerateCaption: false,
            backgroundColor: "default",
            disabled: false,
            actionKind: "changeVariable",
            action: {
              id: "action-change-level",
              kind: "changeVariable",
              officialType: "Microflows$ChangeVariableAction",
              errorHandlingType: "rollback",
              documentation: "",
              editor: { category: "variable", iconKey: "variable", availability: "supported" },
              targetVariableName: "approvalLevel",
              newValueExpression: expression("if true then 'L2' else $approvalLevel"),
            },
          },
          meta: { nodeDTOType: "actionActivity", collectionId: "root-collection", position: { x: 520, y: 180 }, size: { width: 110, height: 36 } },
        },
        {
          id: "end",
          type: "endEvent",
          data: {
            objectId: "end",
            objectKind: "endEvent",
            collectionId: "root-collection",
            title: "End",
            officialType: "Microflows$EndEvent",
            disabled: false,
            validationState: "valid",
            issueCount: 0,
          },
          meta: { position: { x: 720, y: 180 }, size: { width: 18, height: 18 } },
        },
      ],
      edges: [
        edge("flow-start-create", "start", "create-level"),
        edge("flow-create-change", "create-level", "change-level"),
        edge("flow-change-end", "change-level", "end"),
      ],
    },
    editor: {
      viewport: { x: 0, y: 0, zoom: 1 },
      zoom: 1,
      selection: { objectId: selectedObjectId, objectIds: [selectedObjectId], flowIds: [], mode: "single" },
      gridEnabled: true,
      showMiniMap: true,
    },
    parameters: [],
    returnType: { kind: "void" },
    variables: [],
    validation: { issues: [] },
    audit: { version: "1", status: "draft" },
  } as unknown as MicroflowDesignSchema;
}

describe("buildDesignPropertyPanelModel usage bridge", () => {
  it("preserves variable producer/consumer relations for usage highlighting", () => {
    const model = buildDesignPropertyPanelModel(schema("change-level"));
    const usage = buildNodeUsageHighlights(model.authoringSchema, "change-level");

    expect(usage.sourceNodeIds).toContain("create-level");
    expect(usage.usedVariableNames).toContain("approvalLevel");
  });
});
