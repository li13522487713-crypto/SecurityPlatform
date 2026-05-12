import { describe, expect, it } from "vitest";
import type { MendixCompatMicroflow } from "../schema/types";
import { refreshDerivedState } from "./authoring-operations";
import { applyEditorGraphPatch, fromMendixCompat, toMendixCompat, toRuntimeDto } from "./microflow-adapters";

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

  it("keeps $currentSession in compat-derived system variables and runtime dto", () => {
    const schema = fromMendixCompat(missingCollectionCompat);
    const runtimeDto = toRuntimeDto(schema);

    expect(schema.variables?.systemVariables.$currentUser).toBeDefined();
    expect(schema.variables?.systemVariables.$currentSession?.dataType).toEqual({ kind: "object", entityQualifiedName: "System.Session" });
    expect(runtimeDto.variables?.systemVariables?.$currentSession?.dataType).toEqual({ kind: "object", entityQualifiedName: "System.Session" });
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

  it("adds $currentIndex to runtime variables for while loops", () => {
    const withWhileLoop = {
      ...missingCollectionCompat,
      objectCollection: {
        id: "root",
        officialType: "Microflows$MicroflowObjectCollection",
        objects: [
          {
            id: "loop-while",
            stableId: "loop-while",
            kind: "loopedActivity",
            officialType: "Microflows$LoopedActivity",
            caption: "While Loop",
            autoGenerateCaption: false,
            backgroundColor: "default",
            disabled: false,
            relativeMiddlePoint: { x: 0, y: 0 },
            size: { width: 120, height: 80 },
            editor: { category: "flow", iconKey: "loop", availability: "supported" },
            loopSource: {
              kind: "whileCondition",
              officialType: "Microflows$WhileLoopCondition",
              expression: { raw: "$retryCount < 5", references: { variables: [], entities: [], attributes: [], associations: [], enumerations: [], functions: [] }, diagnostics: [] },
            },
            objectCollection: {
              id: "loop-while-body",
              officialType: "Microflows$MicroflowObjectCollection",
              objects: [],
              flows: [],
            },
            isMultiOutput: false,
          } as unknown,
        ],
        flows: [],
      },
    } as unknown as MendixCompatMicroflow;

    const runtimeDto = toRuntimeDto(fromMendixCompat(withWhileLoop));

    expect(runtimeDto.variables?.systemVariables?.$currentIndex).toBeDefined();
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

  it("derives compat returnVariableName from end event returnValue on export", () => {
    const schema = fromMendixCompat({
      ...missingCollectionCompat,
      microflowReturnType: { $Type: "DataTypes$PrimitiveType", primitive: "String" },
      returnVariableName: "staleValue",
      objectCollection: {
        id: "root",
        officialType: "Microflows$MicroflowObjectCollection",
        objects: [
          {
            id: "end-1",
            stableId: "end-1",
            kind: "endEvent",
            officialType: "Microflows$EndEvent",
            caption: "End",
            relativeMiddlePoint: { x: 0, y: 0 },
            size: { width: 40, height: 40 },
            editor: { iconKey: "endEvent" },
            returnValue: {
              raw: "$amount",
              inferredType: { kind: "decimal" },
              references: { variables: ["$amount"], entities: [], attributes: [], associations: [], enumerations: [], functions: [] },
              diagnostics: [],
            },
            endBehavior: { type: "normalReturn" },
          } as unknown,
        ],
        flows: [],
      },
    } as unknown as MendixCompatMicroflow);

    const compat = toMendixCompat(schema);

    expect(schema.returnVariableName).toBe("amount");
    expect(compat.returnVariableName).toBe("amount");
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

  it("only exposes latestHttpResponse for restCall error handlers in compat-derived variables", () => {
    const schema = fromMendixCompat({
      ...missingCollectionCompat,
      objectCollection: {
        id: "root",
        officialType: "Microflows$MicroflowObjectCollection",
        objects: [
          {
            id: "rest-call",
            stableId: "rest-call",
            kind: "actionActivity",
            officialType: "Microflows$ActionActivity",
            caption: "REST",
            autoGenerateCaption: false,
            backgroundColor: "default",
            disabled: false,
            relativeMiddlePoint: { x: 0, y: 0 },
            size: { width: 160, height: 76 },
            editor: { category: "integration", iconKey: "rest", availability: "supported" },
            action: {
              id: "action-rest",
              kind: "restCall",
              officialType: "Microflows$RestCallAction",
              caption: "REST",
              errorHandlingType: "customWithRollback",
              documentation: "",
              editor: { category: "integration", iconKey: "rest", availability: "supported" },
              request: { method: "GET", urlExpression: { raw: "'https://example.com'" }, headers: [], queryParameters: [], body: { kind: "none" } },
              response: { handling: { kind: "ignore" } },
              timeoutSeconds: 30,
            },
          } as unknown,
          {
            id: "handle-rest-error",
            stableId: "handle-rest-error",
            kind: "actionActivity",
            officialType: "Microflows$ActionActivity",
            caption: "Handle Error",
            autoGenerateCaption: false,
            backgroundColor: "default",
            disabled: false,
            relativeMiddlePoint: { x: 200, y: 0 },
            size: { width: 160, height: 76 },
            editor: { category: "logging", iconKey: "logMessage", availability: "supported" },
            action: {
              id: "action-log",
              kind: "logMessage",
              officialType: "Microflows$LogMessageAction",
              caption: "Log",
              errorHandlingType: "rollback",
              documentation: "",
              editor: { category: "logging", iconKey: "logMessage", availability: "supported" },
              template: { text: "boom", arguments: [] },
            },
          } as unknown,
        ],
        flows: [],
      },
      flows: [
        {
          id: "rest-error-flow",
          stableId: "rest-error-flow",
          kind: "sequence",
          officialType: "Microflows$SequenceFlow",
          originObjectId: "rest-call",
          destinationObjectId: "handle-rest-error",
          originConnectionIndex: 0,
          destinationConnectionIndex: 0,
          caseValues: [],
          isErrorHandler: true,
          line: {
            kind: "orthogonal",
            points: [],
            routing: { mode: "auto", bendPoints: [] },
            style: { strokeType: "solid", strokeWidth: 2, arrow: "target" },
          },
          editor: { edgeKind: "errorHandler" },
        },
      ],
    } as unknown as MendixCompatMicroflow);

    expect(schema.variables?.errorVariables.$latestError).toBeDefined();
    expect(schema.variables?.errorVariables.$latestHttpResponse?.dataType).toEqual({ kind: "object", entityQualifiedName: "System.HttpResponse" });
    expect(schema.variables?.errorVariables.$latestSoapFault).toBeUndefined();
  });

  it("only exposes latestSoapFault for webServiceCall error handlers in compat-derived variables", () => {
    const schema = fromMendixCompat({
      ...missingCollectionCompat,
      objectCollection: {
        id: "root",
        officialType: "Microflows$MicroflowObjectCollection",
        objects: [
          {
            id: "soap-call",
            stableId: "soap-call",
            kind: "actionActivity",
            officialType: "Microflows$ActionActivity",
            caption: "SOAP",
            autoGenerateCaption: false,
            backgroundColor: "default",
            disabled: false,
            relativeMiddlePoint: { x: 0, y: 0 },
            size: { width: 160, height: 76 },
            editor: { category: "integration", iconKey: "webServiceCall", availability: "supported" },
            action: {
              id: "action-soap",
              kind: "webServiceCall",
              officialType: "Microflows$WebServiceCallAction",
              caption: "SOAP",
              errorHandlingType: "customWithRollback",
              documentation: "",
              editor: { category: "integration", iconKey: "webServiceCall", availability: "supported" },
              endpoint: "https://soap.example.com/service",
              operation: "SubmitOrder",
              outputVariableName: "soapResult",
            },
          } as unknown,
          {
            id: "handle-soap-error",
            stableId: "handle-soap-error",
            kind: "actionActivity",
            officialType: "Microflows$ActionActivity",
            caption: "Handle Error",
            autoGenerateCaption: false,
            backgroundColor: "default",
            disabled: false,
            relativeMiddlePoint: { x: 200, y: 0 },
            size: { width: 160, height: 76 },
            editor: { category: "logging", iconKey: "logMessage", availability: "supported" },
            action: {
              id: "action-log-soap",
              kind: "logMessage",
              officialType: "Microflows$LogMessageAction",
              caption: "Log",
              errorHandlingType: "rollback",
              documentation: "",
              editor: { category: "logging", iconKey: "logMessage", availability: "supported" },
              template: { text: "boom", arguments: [] },
            },
          } as unknown,
        ],
        flows: [],
      },
      flows: [
        {
          id: "soap-error-flow",
          stableId: "soap-error-flow",
          kind: "sequence",
          officialType: "Microflows$SequenceFlow",
          originObjectId: "soap-call",
          destinationObjectId: "handle-soap-error",
          originConnectionIndex: 0,
          destinationConnectionIndex: 0,
          caseValues: [],
          isErrorHandler: true,
          line: {
            kind: "orthogonal",
            points: [],
            routing: { mode: "auto", bendPoints: [] },
            style: { strokeType: "solid", strokeWidth: 2, arrow: "target" },
          },
          editor: { edgeKind: "errorHandler" },
        },
      ],
    } as unknown as MendixCompatMicroflow);

    expect(schema.variables?.errorVariables.$latestError).toBeDefined();
    expect(schema.variables?.errorVariables.$latestSoapFault?.dataType).toEqual({ kind: "object", entityQualifiedName: "System.SoapFault" });
    expect(schema.variables?.errorVariables.$latestHttpResponse).toBeUndefined();
  });
});
