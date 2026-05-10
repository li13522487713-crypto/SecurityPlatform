import { describe, expect, it } from "vitest";
import type { MendixCompatMicroflow } from "../schema/types";
import { refreshDerivedState } from "./authoring-operations";
import { applyEditorGraphPatch, fromMendixCompat, toRuntimeDto } from "./microflow-adapters";

const missingCollectionCompat = {
  $ID: "mf-root-missing",
  $Type: "Microflows$Microflow",
  $UnitID: "module-a",
  name: "MF_ROOT_MISSING",
  documentation: "",
  parameters: [],
  microflowReturnType: { $Type: "DataTypes$PrimitiveType", primitive: "Void" },
  returnVariableName: "",
  objectCollection: undefined,
  flows: undefined,
  applyEntityAccess: true,
  allowedModuleRoleIds: [],
  allowedRoleNames: [],
  allowConcurrentExecution: true,
  concurrencyErrorMessage: undefined,
  concurrencyErrorMicroflow: undefined,
  excluded: false,
  exportLevel: "UsableFromModule",
  markAsUsed: false,
  microflowActionInfo: null,
  workflowActionInfo: null,
  url: undefined,
  urlSearchParameters: undefined,
  stableId: "mf-root-missing",
} as unknown as MendixCompatMicroflow;

describe("microflow adapters compatibility", () => {
  it("builds authoring schema and runtime dto when object collection is missing", () => {
    const schema = fromMendixCompat(missingCollectionCompat);

    expect(schema.objectCollection.id).toBe("root-collection");
    expect(schema.objectCollection.objects).toHaveLength(0);
    expect(toRuntimeDto(schema).flows).toHaveLength(0);
    expect(toRuntimeDto(schema).variables?.all).toBeDefined();
  });

  it("tolerates looped activity without nested collection", () => {
    const withBrokenLoop = {
      ...missingCollectionCompat,
      objectCollection: {
        id: "root",
        officialType: "Microflows$MicroflowObjectCollection",
        objects: [
          {
            id: "loop-1",
            stableId: "loop-1",
            kind: "loopedActivity",
            officialType: "Microflows$LoopedActivity",
            caption: "Loop",
            autoGenerateCaption: false,
            backgroundColor: "default",
            disabled: false,
            relativeMiddlePoint: { x: 0, y: 0 },
            size: { width: 120, height: 80 },
            editor: { category: "flow", iconKey: "loop", availability: "supported" },
            loopSource: {
              kind: "iterableList",
              iteratorVariableName: "item",
              currentIndexVariableName: "$index",
              listVariableName: "items",
            },
            isMultiOutput: false,
          } as unknown,
        ],
        flows: [],
      },
    } as unknown as MendixCompatMicroflow;

    expect(() => fromMendixCompat(withBrokenLoop)).not.toThrow();
    const schema = fromMendixCompat(withBrokenLoop);
    const runtimeDto = toRuntimeDto(schema);

    expect(runtimeDto.objectCollection.id).toBe("root");
    expect(runtimeDto.variables?.loopVariables?.item).toBeDefined();
  });

  it("normalizes legacy bezier flow lines when reading mendix compat payloads", () => {
    const compatWithBezier = {
      ...missingCollectionCompat,
      flows: [
        {
          id: "flow-1",
          stableId: "flow-1",
          kind: "sequence",
          officialType: "Microflows$SequenceFlow",
          originObjectId: "start",
          destinationObjectId: "end",
          originConnectionIndex: 0,
          destinationConnectionIndex: 0,
          caseValues: [],
          isErrorHandler: false,
          line: {
            kind: "bezier",
            points: [],
            routing: { mode: "auto", bendPoints: [] },
            style: { strokeType: "solid", strokeWidth: 2, arrow: "target" },
          },
          editor: { edgeKind: "sequence" },
        },
      ],
    } as unknown as MendixCompatMicroflow;

    const schema = fromMendixCompat(compatWithBezier);

    expect(schema.flows[0]?.line.kind).toBe("orthogonal");
  });

  it("normalizes updated flow lines during editor graph patch application", () => {
    const schema = fromMendixCompat({
      ...missingCollectionCompat,
      flows: [
        {
          id: "flow-1",
          stableId: "flow-1",
          kind: "sequence",
          officialType: "Microflows$SequenceFlow",
          originObjectId: "start",
          destinationObjectId: "end",
          originConnectionIndex: 0,
          destinationConnectionIndex: 0,
          caseValues: [],
          isErrorHandler: false,
          line: {
            kind: "orthogonal",
            points: [],
            routing: { mode: "auto", bendPoints: [] },
            style: { strokeType: "solid", strokeWidth: 2, arrow: "target" },
          },
          editor: { edgeKind: "sequence" },
        },
      ],
    } as unknown as MendixCompatMicroflow);

    const next = applyEditorGraphPatch(schema, {
      updatedFlows: [
        {
          flowId: "flow-1",
          line: {
            kind: "bezier",
            points: [],
            routing: { mode: "auto", bendPoints: [] },
            style: { strokeType: "solid", strokeWidth: 2, arrow: "target" },
          },
        },
      ],
    });

    expect(next.flows[0]?.line.kind).toBe("orthogonal");
  });

  it("preserves design schema variables as an array when refreshing derived state", () => {
    const designSchema = {
      schemaVersion: "1.0.0",
      id: "mf-design",
      stableId: "mf-design",
      name: "MF_Design",
      displayName: "MF_Design",
      moduleId: "module-a",
      workflow: {
        nodes: [{ id: "start", type: "startEvent", data: { objectKind: "startEvent" } }],
        edges: [],
      },
      editor: { viewport: { x: 0, y: 0, zoom: 1 }, selection: {} },
      parameters: [],
      returnType: { kind: "void" },
      variables: [],
      validation: { issues: [] },
      audit: { version: "v1", status: "draft" },
    };

    const refreshed = refreshDerivedState(designSchema as never) as unknown as { variables: unknown };

    expect(Array.isArray(refreshed.variables)).toBe(true);
  });
});
