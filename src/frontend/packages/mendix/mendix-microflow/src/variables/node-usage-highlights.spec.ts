import { describe, expect, it } from "vitest";

import type { MicroflowAuthoringSchema, MicroflowExpression } from "../schema/types";
import { buildNodeUsageHighlights, buildVariableUsageHighlights } from "./node-usage-highlights";

function expression(raw: string): MicroflowExpression {
  return {
    raw,
    referencedVariables: [],
    references: { variables: [], entities: [], attributes: [], associations: [], enumerations: [], functions: [] },
    diagnostics: [],
  };
}

function schema(): MicroflowAuthoringSchema {
  return {
    schemaVersion: "1",
    mendixProfile: "mx10",
    id: "mf-usage",
    stableId: "mf-usage",
    name: "MF_Usage",
    displayName: "MF Usage",
    moduleId: "module-a",
    parameters: [
      { id: "param-amount", name: "amount", dataType: { kind: "decimal" }, required: true },
    ],
    returnType: { kind: "void" },
    objectCollection: {
      id: "root",
      officialType: "Microflows$MicroflowObjectCollection",
      objects: [
        {
          id: "start",
          stableId: "start",
          kind: "startEvent",
          officialType: "Microflows$StartEvent",
          caption: "Start",
          documentation: "",
          relativeMiddlePoint: { x: -120, y: 0 },
          size: { width: 18, height: 18 },
          editor: {},
        } as any,
        {
          id: "param-object",
          stableId: "param-object",
          kind: "parameterObject",
          officialType: "Microflows$MicroflowParameterObject",
          parameterId: "param-amount",
          parameterName: "amount",
          relativeMiddlePoint: { x: 0, y: 0 },
          size: { width: 120, height: 56 },
          editor: {},
          documentation: "",
        },
        {
          id: "create-level",
          stableId: "create-level",
          kind: "actionActivity",
          officialType: "Microflows$ActionActivity",
          caption: "Create Variable",
          autoGenerateCaption: false,
          backgroundColor: "default",
          disabled: false,
          relativeMiddlePoint: { x: 120, y: 0 },
          size: { width: 178, height: 76 },
          editor: {},
          action: {
            id: "action-create-level",
            kind: "createVariable",
            officialType: "Microflows$CreateVariableAction",
            caption: "Create Variable",
            errorHandlingType: "rollback",
            documentation: "",
            editor: { category: "variable", iconKey: "variable", availability: "supported" },
            variableName: "approvalLevel",
            dataType: { kind: "string" },
            initialValue: expression("'L1'"),
            readonly: false,
          },
        },
        {
          id: "change-level",
          stableId: "change-level",
          kind: "actionActivity",
          officialType: "Microflows$ActionActivity",
          caption: "Change Variable",
          autoGenerateCaption: false,
          backgroundColor: "default",
          disabled: false,
          relativeMiddlePoint: { x: 240, y: 0 },
          size: { width: 178, height: 76 },
          editor: {},
          action: {
            id: "action-change-level",
            kind: "changeVariable",
            officialType: "Microflows$ChangeVariableAction",
            caption: "Change Variable",
            errorHandlingType: "rollback",
            documentation: "",
            editor: { category: "variable", iconKey: "variable", availability: "supported" },
            targetVariableName: "approvalLevel",
            newValueExpression: expression("if $amount > 100 then 'L2' else $approvalLevel"),
          },
        },
        {
          id: "loop-1",
          stableId: "loop-1",
          kind: "loopedActivity",
          officialType: "Microflows$LoopedActivity",
          documentation: "",
          errorHandlingType: "rollback",
          relativeMiddlePoint: { x: 360, y: 0 },
          size: { width: 320, height: 190 },
          editor: {},
          loopSource: {
            kind: "iterableList",
            officialType: "Microflows$IterableList",
            listVariableName: "approvalLevel",
            iteratorVariableName: "item",
            iteratorVariableDataType: { kind: "string" },
            currentIndexVariableName: "$currentIndex",
          },
          objectCollection: {
            id: "loop-body",
            officialType: "Microflows$MicroflowObjectCollection",
            objects: [
              {
                id: "inner-change",
                stableId: "inner-change",
                kind: "actionActivity",
                officialType: "Microflows$ActionActivity",
                caption: "Inner Change",
                autoGenerateCaption: false,
                backgroundColor: "default",
                disabled: false,
                relativeMiddlePoint: { x: 0, y: 0 },
                size: { width: 178, height: 76 },
                editor: {},
                action: {
                  id: "action-inner-change",
                  kind: "changeVariable",
                  officialType: "Microflows$ChangeVariableAction",
                  caption: "Change Variable",
                  errorHandlingType: "rollback",
                  documentation: "",
                  editor: { category: "variable", iconKey: "variable", availability: "supported" },
                  targetVariableName: "approvalLevel",
                  newValueExpression: expression("$item + $currentIndex"),
                },
              },
            ],
            flows: [],
          },
        },
      ],
      flows: [
        {
          id: "flow-start-create",
          stableId: "flow-start-create",
          kind: "sequence",
          officialType: "Microflows$SequenceFlow",
          originObjectId: "start",
          destinationObjectId: "create-level",
          originConnectionIndex: 0,
          destinationConnectionIndex: 0,
          caseValues: [],
          isErrorHandler: false,
          line: { kind: "orthogonal", points: [], routing: { mode: "auto", bendPoints: [] }, style: { strokeType: "solid", strokeWidth: 2, arrow: "target" } },
          editor: { edgeKind: "sequence" },
        } as any,
        {
          id: "flow-create-change",
          stableId: "flow-create-change",
          kind: "sequence",
          officialType: "Microflows$SequenceFlow",
          originObjectId: "create-level",
          destinationObjectId: "change-level",
          originConnectionIndex: 0,
          destinationConnectionIndex: 0,
          caseValues: [],
          isErrorHandler: false,
          line: { kind: "orthogonal", points: [], routing: { mode: "auto", bendPoints: [] }, style: { strokeType: "solid", strokeWidth: 2, arrow: "target" } },
          editor: { edgeKind: "sequence" },
        } as any,
      ],
    },
    flows: [
      {
        id: "flow-start-create",
        stableId: "flow-start-create",
        kind: "sequence",
        officialType: "Microflows$SequenceFlow",
        originObjectId: "start",
        destinationObjectId: "create-level",
        originConnectionIndex: 0,
        destinationConnectionIndex: 0,
        caseValues: [],
        isErrorHandler: false,
        line: { kind: "orthogonal", points: [], routing: { mode: "auto", bendPoints: [] }, style: { strokeType: "solid", strokeWidth: 2, arrow: "target" } },
        editor: { edgeKind: "sequence" },
      } as any,
      {
        id: "flow-create-change",
        stableId: "flow-create-change",
        kind: "sequence",
        officialType: "Microflows$SequenceFlow",
        originObjectId: "create-level",
        destinationObjectId: "change-level",
        originConnectionIndex: 0,
        destinationConnectionIndex: 0,
        caseValues: [],
        isErrorHandler: false,
        line: { kind: "orthogonal", points: [], routing: { mode: "auto", bendPoints: [] }, style: { strokeType: "solid", strokeWidth: 2, arrow: "target" } },
        editor: { edgeKind: "sequence" },
      } as any,
    ],
    security: { allowedModuleRoleIds: [], allowedRoleNames: [], applyEntityAccess: true },
    concurrency: { allowConcurrentExecution: true },
    exposure: { exportLevel: "module", markAsUsed: false },
    validation: { issues: [] },
    editor: { viewport: { x: 0, y: 0, zoom: 1 }, zoom: 1, selection: {} },
    audit: { version: "1", status: "draft" },
  };
}

describe("buildNodeUsageHighlights", () => {
  it("highlights upstream producers for selected consumers", () => {
    const result = buildNodeUsageHighlights(schema(), "change-level");

    expect(result.usedVariableNames.sort()).toEqual(["amount", "approvalLevel"]);
    expect(result.sourceNodeIds.sort()).toEqual(["create-level", "param-object"]);
  });

  it("highlights downstream consumers for selected producers", () => {
    const result = buildNodeUsageHighlights(schema(), "create-level");

    expect(result.outputVariableNames).toContain("approvalLevel");
    expect(result.consumerNodeIds.sort()).toEqual(["change-level"]);
  });

  it("treats loop iterator variables as outputs of the loop node", () => {
    const result = buildNodeUsageHighlights(schema(), "loop-1");

    expect(result.outputVariableNames.sort()).toEqual(["currentIndex", "item"]);
    expect(result.consumerNodeIds).toEqual(["inner-change"]);
  });
});

describe("buildVariableUsageHighlights", () => {
  it("highlights producer and consumers for a selected variable name", () => {
    const result = buildVariableUsageHighlights(schema(), "$approvalLevel");

    expect(result.sourceNodeIds).toEqual(["create-level"]);
    expect(result.consumerNodeIds.sort()).toEqual(["change-level", "inner-change", "loop-1"]);
    expect(result.usedVariableNames).toEqual(["approvalLevel"]);
  });

  it("accepts variable names without a leading dollar sign", () => {
    const result = buildVariableUsageHighlights(schema(), "amount");

    expect(result.sourceNodeIds).toEqual(["param-object"]);
    expect(result.consumerNodeIds).toEqual(["change-level"]);
  });
});

describe("buildNodeUsageHighlights with while loops", () => {
  it("treats $currentIndex as an output of while loops", () => {
    const s = schema();
    const whileLoop = {
      id: "while-loop",
      stableId: "while-loop",
      kind: "loopedActivity",
      officialType: "Microflows$LoopedActivity",
      documentation: "",
      errorHandlingType: "rollback",
      relativeMiddlePoint: { x: 520, y: 180 },
      size: { width: 320, height: 190 },
      editor: {},
      loopSource: {
        kind: "whileCondition",
        officialType: "Microflows$WhileLoopCondition",
        expression: expression("$approvalLevel != ''"),
      },
      objectCollection: {
        id: "while-body",
        officialType: "Microflows$MicroflowObjectCollection",
        objects: [
          {
            id: "while-inner",
            stableId: "while-inner",
            kind: "actionActivity",
            officialType: "Microflows$ActionActivity",
            caption: "While Change",
            autoGenerateCaption: false,
            backgroundColor: "default",
            disabled: false,
            relativeMiddlePoint: { x: 0, y: 0 },
            size: { width: 178, height: 76 },
            editor: {},
            action: {
              id: "action-while-inner",
              kind: "changeVariable",
              officialType: "Microflows$ChangeVariableAction",
              caption: "Change Variable",
              errorHandlingType: "rollback",
              documentation: "",
              editor: { category: "variable", iconKey: "variable", availability: "supported" },
              targetVariableName: "approvalLevel",
              newValueExpression: expression("$currentIndex + $amount"),
            },
          },
        ],
        flows: [],
      },
    } as any;
    s.objectCollection.objects.push(whileLoop);

    const result = buildNodeUsageHighlights(s, "while-loop");

    expect(result.outputVariableNames).toEqual(["currentIndex"]);
    expect(result.consumerNodeIds).toEqual(["while-inner"]);
  });
});
