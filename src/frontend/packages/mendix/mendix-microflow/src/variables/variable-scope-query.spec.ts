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

  it("adds $currentIndex inside while loop body scope", () => {
    const inner = actionActivity("while-inner", "innerOut");
    const loop = objectFrom("loop", "while-loop") as Extract<MicroflowObject, { kind: "loopedActivity" }>;
    loop.objectCollection = {
      ...loop.objectCollection,
      id: "while-body",
      objects: [inner],
      flows: [
        {
          id: "flow-while-body",
          kind: "sequence",
          officialType: "Microflows$SequenceFlow",
          originObjectId: "while-loop",
          destinationObjectId: "while-inner",
          originConnectionIndex: 2,
          destinationConnectionIndex: 0,
          caseValues: [],
          isErrorHandler: false,
          relativeMiddlePoint: { x: 0, y: 0 },
          size: { width: 0, height: 0 },
          backgroundColor: "default",
          disabled: false,
          editor: { edgeKind: "loopBody" },
        } as any,
      ],
    };
    loop.loopSource = {
      kind: "whileCondition",
      officialType: "Microflows$WhileLoopCondition",
      expression: {
        raw: "$retryCount < 5",
        referencedVariables: [],
        references: { variables: [], entities: [], attributes: [], associations: [], enumerations: [], functions: [] },
        diagnostics: [],
      },
    };
    const createRetry = actionActivity("node-retry", "retryOut");
    createRetry.action = {
      id: "action-retry",
      kind: "createVariable",
      officialType: "Microflows$CreateVariableAction",
      caption: "Create Variable",
      errorHandlingType: "rollback",
      documentation: "",
      editor: { category: "variable", iconKey: "variable", availability: "supported" },
      variableName: "retryCount",
      dataType: { kind: "integer" },
      initialValue: { raw: "0", referencedVariables: [], references: { variables: [], entities: [], attributes: [], associations: [], enumerations: [], functions: [] }, diagnostics: [] },
      readonly: false,
    } as any;
    const whileSchema = {
      ...schema(false),
      objectCollection: {
        ...schema(false).objectCollection,
        objects: [createRetry, loop],
        flows: [
          {
            id: "flow-retry-while",
            kind: "sequence",
            officialType: "Microflows$SequenceFlow",
            originObjectId: "node-retry",
            destinationObjectId: "while-loop",
            caseValues: [],
            isErrorHandler: false,
            relativeMiddlePoint: { x: 0, y: 0 },
            size: { width: 0, height: 0 },
            backgroundColor: "default",
            disabled: false,
            editor: { edgeKind: "sequence" },
          } as any,
        ],
      },
      flows: [
        {
          id: "flow-retry-while",
          kind: "sequence",
          officialType: "Microflows$SequenceFlow",
          originObjectId: "node-retry",
          destinationObjectId: "while-loop",
          caseValues: [],
          isErrorHandler: false,
          relativeMiddlePoint: { x: 0, y: 0 },
          size: { width: 0, height: 0 },
          backgroundColor: "default",
          disabled: false,
          editor: { edgeKind: "sequence" },
        } as any,
      ],
    };
    const index = buildVariableIndex({ schema: whileSchema, metadata: EMPTY_MICROFLOW_METADATA_CATALOG });
    const names = getVariablesForExpressionFromIndex(whileSchema, index, { objectId: "while-inner" }).map(item => item.name);

    expect(names).toContain("retryCount");
    expect(names).toContain("$currentIndex");
  });

  it("exposes $latestError on the first error-handler node", () => {
    const restCall = actionActivity("rest-call", "restResult");
    restCall.action = {
      id: "action-rest-call",
      kind: "restCall",
      officialType: "Microflows$RestCallAction",
      caption: "Call REST",
      errorHandlingType: "customWithRollback",
      documentation: "",
      editor: { category: "integration", iconKey: "rest", availability: "supported" },
      request: {
        method: "GET",
        urlExpression: { raw: "'https://example.com'", referencedVariables: [], references: { variables: [], entities: [], attributes: [], associations: [], enumerations: [], functions: [] }, diagnostics: [] },
        headers: [],
        queryParameters: [],
        body: { kind: "none" },
      },
      response: { handling: { kind: "ignore" } },
      timeoutSeconds: 30,
    } as any;
    const logError = actionActivity("log-error", "errorLog");
    logError.action = {
      ...logError.action,
      kind: "logMessage",
      officialType: "Microflows$LogMessageAction",
      template: {
        text: "REST failed",
        arguments: [],
      },
      logNodeName: "ErrorHandler",
      includeContextVariables: true,
      includeTraceId: false,
    } as any;
    const errorSchema = {
      ...schema(false),
      objectCollection: {
        ...schema(false).objectCollection,
        objects: [restCall, logError],
        flows: [],
      },
      flows: [
        {
          id: "flow-rest-error",
          kind: "sequence",
          officialType: "Microflows$SequenceFlow",
          originObjectId: "rest-call",
          destinationObjectId: "log-error",
          caseValues: [],
          isErrorHandler: true,
          relativeMiddlePoint: { x: 0, y: 0 },
          size: { width: 0, height: 0 },
          backgroundColor: "default",
          disabled: false,
          editor: { edgeKind: "errorHandler", label: "error" },
        } as any,
      ],
    };
    const index = buildVariableIndex({ schema: errorSchema, metadata: EMPTY_MICROFLOW_METADATA_CATALOG });
    const names = getVariablesForExpressionFromIndex(errorSchema, index, { objectId: "log-error", fieldPath: "action.template.text" }).map(item => item.name);

    expect(names).toContain("$latestError");
    expect(names).toContain("$latestHttpResponse");
  });

  it("exposes $latestSoapFault on the first webServiceCall error-handler node", () => {
    const soapCall = actionActivity("soap-call", "soapResult");
    soapCall.action = {
      id: "action-soap-call",
      kind: "webServiceCall",
      officialType: "Microflows$WebServiceCallAction",
      caption: "Call SOAP",
      errorHandlingType: "customWithRollback",
      documentation: "",
      editor: { category: "integration", iconKey: "webServiceCall", availability: "supported" },
      endpoint: "https://soap.example.com/service",
      operation: "SubmitOrder",
      outputVariableName: "soapResult",
    } as any;
    const logError = actionActivity("log-soap-error", "soapErrorLog");
    logError.action = {
      ...logError.action,
      kind: "logMessage",
      officialType: "Microflows$LogMessageAction",
      template: {
        text: "SOAP failed",
        arguments: [],
      },
      logNodeName: "SoapErrorHandler",
      includeContextVariables: true,
      includeTraceId: false,
    } as any;
    const errorSchema = {
      ...schema(false),
      objectCollection: {
        ...schema(false).objectCollection,
        objects: [soapCall, logError],
        flows: [],
      },
      flows: [
        {
          id: "flow-soap-error",
          kind: "sequence",
          officialType: "Microflows$SequenceFlow",
          originObjectId: "soap-call",
          destinationObjectId: "log-soap-error",
          caseValues: [],
          isErrorHandler: true,
          relativeMiddlePoint: { x: 0, y: 0 },
          size: { width: 0, height: 0 },
          backgroundColor: "default",
          disabled: false,
          editor: { edgeKind: "errorHandler", label: "error" },
        } as any,
      ],
    };
    const index = buildVariableIndex({ schema: errorSchema, metadata: EMPTY_MICROFLOW_METADATA_CATALOG });
    const names = getVariablesForExpressionFromIndex(errorSchema, index, { objectId: "log-soap-error", fieldPath: "action.template.text" }).map(item => item.name);

    expect(names).toContain("$latestError");
    expect(names).toContain("$latestSoapFault");
  });

  it("exposes filterList item variable inside condition expressions", () => {
    const createList = actionActivity("create-list", "orders");
    createList.action = {
      id: "action-create-list",
      kind: "createList",
      officialType: "Microflows$CreateListAction",
      caption: "Create List",
      errorHandlingType: "rollback",
      documentation: "",
      editor: { category: "list", iconKey: "list", availability: "supported" },
      outputListVariableName: "orders",
      listVariableName: "orders",
      elementType: { kind: "integer" },
      itemType: { kind: "integer" },
      listType: "mutable",
    } as any;
    const filterList = actionActivity("filter-list", "positiveOrders");
    filterList.action = {
      id: "action-filter-list",
      kind: "filterList",
      officialType: "Microflows$FilterListAction",
      caption: "Filter List",
      errorHandlingType: "rollback",
      documentation: "",
      editor: { category: "list", iconKey: "filter", availability: "supported" },
      sourceListVariableName: "orders",
      listVariableName: "orders",
      outputVariableName: "positiveOrders",
      itemVariableName: "item",
      itemType: { kind: "integer" },
      conditionExpression: { raw: "$item > 2" },
      filterExpression: { raw: "$item > 2" },
    } as any;
    const downstream = actionActivity("after-filter", "afterOut");
    const flowCreate = {
      id: "flow-create-filter",
      kind: "sequence",
      officialType: "Microflows$SequenceFlow",
      originObjectId: "create-list",
      destinationObjectId: "filter-list",
      caseValues: [],
      isErrorHandler: false,
      relativeMiddlePoint: { x: 0, y: 0 },
      size: { width: 0, height: 0 },
      backgroundColor: "default",
      disabled: false,
      editor: { edgeKind: "sequence" },
    } as any;
    const flowFilter = {
      id: "flow-filter-downstream",
      kind: "sequence",
      officialType: "Microflows$SequenceFlow",
      originObjectId: "filter-list",
      destinationObjectId: "after-filter",
      caseValues: [],
      isErrorHandler: false,
      relativeMiddlePoint: { x: 0, y: 0 },
      size: { width: 0, height: 0 },
      backgroundColor: "default",
      disabled: false,
      editor: { edgeKind: "sequence" },
    } as any;
    const filterSchema = {
      ...schema(false),
      objectCollection: {
        ...schema(false).objectCollection,
        objects: [createList, filterList, downstream],
        flows: [flowCreate, flowFilter],
      },
      flows: [flowCreate, flowFilter],
    };
    const index = buildVariableIndex({ schema: filterSchema, metadata: EMPTY_MICROFLOW_METADATA_CATALOG });
    const conditionNames = getVariablesForExpressionFromIndex(filterSchema, index, { objectId: "filter-list", fieldPath: "action.conditionExpression" }).map(item => item.name);
    const downstreamNames = getVariablesForExpressionFromIndex(filterSchema, index, { objectId: "after-filter" }).map(item => item.name);

    expect(conditionNames).toContain("item");
    expect(downstreamNames).toContain("positiveOrders");
    expect(downstreamNames).not.toContain("item");
  });

  it("exposes changeList removeWhere item variable inside condition expressions", () => {
    const createList = actionActivity("create-list", "orders");
    createList.action = {
      id: "action-create-list-change-list",
      kind: "createList",
      officialType: "Microflows$CreateListAction",
      caption: "Create List",
      errorHandlingType: "rollback",
      documentation: "",
      editor: { category: "list", iconKey: "list", availability: "supported" },
      outputListVariableName: "orders",
      listVariableName: "orders",
      elementType: { kind: "integer" },
      itemType: { kind: "integer" },
      listType: "mutable",
    } as any;
    const changeList = actionActivity("change-list", "resultOrders");
    changeList.action = {
      id: "action-change-list",
      kind: "changeList",
      officialType: "Microflows$ChangeListAction",
      caption: "Change List",
      errorHandlingType: "rollback",
      documentation: "",
      editor: { category: "list", iconKey: "changeList", availability: "supported" },
      targetListVariableName: "orders",
      operation: "removeWhere",
      conditionExpression: { raw: "$item > 2" },
    } as any;
    const flowCreate = {
      id: "flow-create-change",
      kind: "sequence",
      officialType: "Microflows$SequenceFlow",
      originObjectId: "create-list",
      destinationObjectId: "change-list",
      caseValues: [],
      isErrorHandler: false,
      relativeMiddlePoint: { x: 0, y: 0 },
      size: { width: 0, height: 0 },
      backgroundColor: "default",
      disabled: false,
      editor: { edgeKind: "sequence" },
    } as any;
    const changeSchema = {
      ...schema(false),
      objectCollection: {
        ...schema(false).objectCollection,
        objects: [createList, changeList],
        flows: [flowCreate],
      },
      flows: [flowCreate],
    };
    const index = buildVariableIndex({ schema: changeSchema, metadata: EMPTY_MICROFLOW_METADATA_CATALOG });
    const conditionNames = getVariablesForExpressionFromIndex(changeSchema, index, { objectId: "change-list", fieldPath: "action.conditionExpression" }).map(item => item.name);

    expect(conditionNames).toContain("item");
    expect(conditionNames).toContain("orders");
  });

  it("exposes listOperation filter item variable inside filter expressions", () => {
    const createList = actionActivity("create-list", "orders");
    createList.action = {
      id: "action-create-list-list-operation-filter",
      kind: "createList",
      officialType: "Microflows$CreateListAction",
      caption: "Create List",
      errorHandlingType: "rollback",
      documentation: "",
      editor: { category: "list", iconKey: "list", availability: "supported" },
      outputListVariableName: "orders",
      listVariableName: "orders",
      elementType: { kind: "integer" },
      itemType: { kind: "integer" },
      listType: "mutable",
    } as any;
    const listOperation = actionActivity("list-operation-filter", "positiveOrders");
    listOperation.action = {
      id: "action-list-operation-filter",
      kind: "listOperation",
      officialType: "Microflows$ListOperationAction",
      caption: "List Operation",
      errorHandlingType: "rollback",
      documentation: "",
      editor: { category: "list", iconKey: "listOperation", availability: "supported" },
      leftListVariableName: "orders",
      sourceListVariableName: "orders",
      operation: "filter",
      filterExpression: { raw: "$item > 2" },
      expression: { raw: "$item > 2" },
      outputVariableName: "positiveOrders",
      outputListVariableName: "positiveOrders",
      outputElementType: { kind: "integer" },
    } as any;
    const flowCreate = {
      id: "flow-create-list-operation-filter",
      kind: "sequence",
      officialType: "Microflows$SequenceFlow",
      originObjectId: "create-list",
      destinationObjectId: "list-operation-filter",
      caseValues: [],
      isErrorHandler: false,
      relativeMiddlePoint: { x: 0, y: 0 },
      size: { width: 0, height: 0 },
      backgroundColor: "default",
      disabled: false,
      editor: { edgeKind: "sequence" },
    } as any;
    const filterSchema = {
      ...schema(false),
      objectCollection: {
        ...schema(false).objectCollection,
        objects: [createList, listOperation],
        flows: [flowCreate],
      },
      flows: [flowCreate],
    };
    const index = buildVariableIndex({ schema: filterSchema, metadata: EMPTY_MICROFLOW_METADATA_CATALOG });
    const filterNames = getVariablesForExpressionFromIndex(filterSchema, index, { objectId: "list-operation-filter", fieldPath: "action.filterExpression" }).map(item => item.name);
    const legacyNames = getVariablesForExpressionFromIndex(filterSchema, index, { objectId: "list-operation-filter", fieldPath: "action.expression" }).map(item => item.name);

    expect(filterNames).toContain("item");
    expect(legacyNames).toContain("item");
    expect(filterNames).toContain("orders");
  });

  it("exposes listOperation map item variable inside map expressions", () => {
    const createList = actionActivity("create-list", "orders");
    createList.action = {
      id: "action-create-list-list-operation-map",
      kind: "createList",
      officialType: "Microflows$CreateListAction",
      caption: "Create List",
      errorHandlingType: "rollback",
      documentation: "",
      editor: { category: "list", iconKey: "list", availability: "supported" },
      outputListVariableName: "orders",
      listVariableName: "orders",
      elementType: { kind: "integer" },
      itemType: { kind: "integer" },
      listType: "mutable",
    } as any;
    const listOperation = actionActivity("list-operation-map", "mappedOrders");
    listOperation.action = {
      id: "action-list-operation-map",
      kind: "listOperation",
      officialType: "Microflows$ListOperationAction",
      caption: "List Operation",
      errorHandlingType: "rollback",
      documentation: "",
      editor: { category: "list", iconKey: "listOperation", availability: "supported" },
      leftListVariableName: "orders",
      sourceListVariableName: "orders",
      operation: "map",
      expression: { raw: "$item + 1" },
      outputVariableName: "mappedOrders",
      outputListVariableName: "mappedOrders",
      outputElementType: { kind: "integer" },
    } as any;
    const flowCreate = {
      id: "flow-create-list-operation-map",
      kind: "sequence",
      officialType: "Microflows$SequenceFlow",
      originObjectId: "create-list",
      destinationObjectId: "list-operation-map",
      caseValues: [],
      isErrorHandler: false,
      relativeMiddlePoint: { x: 0, y: 0 },
      size: { width: 0, height: 0 },
      backgroundColor: "default",
      disabled: false,
      editor: { edgeKind: "sequence" },
    } as any;
    const mapSchema = {
      ...schema(false),
      objectCollection: {
        ...schema(false).objectCollection,
        objects: [createList, listOperation],
        flows: [flowCreate],
      },
      flows: [flowCreate],
    };
    const index = buildVariableIndex({ schema: mapSchema, metadata: EMPTY_MICROFLOW_METADATA_CATALOG });
    const mapNames = getVariablesForExpressionFromIndex(mapSchema, index, { objectId: "list-operation-map", fieldPath: "action.expression" }).map(item => item.name);

    expect(mapNames).toContain("item");
    expect(mapNames).toContain("orders");
  });

  it("exposes listOperation sort item variable inside sort expressions", () => {
    const createList = actionActivity("create-list", "orders");
    createList.action = {
      id: "action-create-list-list-operation-sort",
      kind: "createList",
      officialType: "Microflows$CreateListAction",
      caption: "Create List",
      errorHandlingType: "rollback",
      documentation: "",
      editor: { category: "list", iconKey: "list", availability: "supported" },
      outputListVariableName: "orders",
      listVariableName: "orders",
      elementType: { kind: "object", entityQualifiedName: "Sales.Order" },
      itemType: { kind: "object", entityQualifiedName: "Sales.Order" },
      listType: "mutable",
    } as any;
    const listOperation = actionActivity("list-operation-sort", "sortedOrders");
    listOperation.action = {
      id: "action-list-operation-sort",
      kind: "listOperation",
      officialType: "Microflows$ListOperationAction",
      caption: "List Operation",
      errorHandlingType: "rollback",
      documentation: "",
      editor: { category: "list", iconKey: "listOperation", availability: "supported" },
      leftListVariableName: "orders",
      sourceListVariableName: "orders",
      operation: "sort",
      sortExpression: { raw: "$item/score" },
      sortKeys: [{ expression: { raw: "$item/score" }, direction: "asc" }],
      outputVariableName: "sortedOrders",
      outputListVariableName: "sortedOrders",
      outputElementType: { kind: "object", entityQualifiedName: "Sales.Order" },
    } as any;
    const flowCreate = {
      id: "flow-create-list-operation-sort",
      kind: "sequence",
      officialType: "Microflows$SequenceFlow",
      originObjectId: "create-list",
      destinationObjectId: "list-operation-sort",
      caseValues: [],
      isErrorHandler: false,
      relativeMiddlePoint: { x: 0, y: 0 },
      size: { width: 0, height: 0 },
      backgroundColor: "default",
      disabled: false,
      editor: { edgeKind: "sequence" },
    } as any;
    const sortSchema = {
      ...schema(false),
      objectCollection: {
        ...schema(false).objectCollection,
        objects: [createList, listOperation],
        flows: [flowCreate],
      },
      flows: [flowCreate],
    };
    const index = buildVariableIndex({ schema: sortSchema, metadata: EMPTY_MICROFLOW_METADATA_CATALOG });
    const sortNames = getVariablesForExpressionFromIndex(sortSchema, index, { objectId: "list-operation-sort", fieldPath: "action.sortExpression" }).map(item => item.name);
    const sortKeyNames = getVariablesForExpressionFromIndex(sortSchema, index, { objectId: "list-operation-sort", fieldPath: "action.sortKeys.0.expression" }).map(item => item.name);

    expect(sortNames).toContain("item");
    expect(sortKeyNames).toContain("item");
    expect(sortNames).toContain("orders");
  });

  it("exposes sortList item variable inside sort expressions", () => {
    const createList = actionActivity("create-list", "orders");
    createList.action = {
      id: "action-create-list-sort-list",
      kind: "createList",
      officialType: "Microflows$CreateListAction",
      caption: "Create List",
      errorHandlingType: "rollback",
      documentation: "",
      editor: { category: "list", iconKey: "list", availability: "supported" },
      outputListVariableName: "orders",
      listVariableName: "orders",
      elementType: { kind: "object", entityQualifiedName: "Sales.Order" },
      itemType: { kind: "object", entityQualifiedName: "Sales.Order" },
      listType: "mutable",
    } as any;
    const sortList = actionActivity("sort-list", "sortedOrders");
    sortList.action = {
      id: "action-sort-list",
      kind: "sortList",
      officialType: "Microflows$SortListAction",
      caption: "Sort List",
      errorHandlingType: "rollback",
      documentation: "",
      editor: { category: "list", iconKey: "sortList", availability: "supported" },
      sourceListVariableName: "orders",
      listVariableName: "orders",
      sortExpression: { raw: "$item/score" },
      direction: "asc",
      outputVariableName: "sortedOrders",
      outputElementType: { kind: "object", entityQualifiedName: "Sales.Order" },
    } as any;
    const flowCreate = {
      id: "flow-create-sort-list",
      kind: "sequence",
      officialType: "Microflows$SequenceFlow",
      originObjectId: "create-list",
      destinationObjectId: "sort-list",
      caseValues: [],
      isErrorHandler: false,
      relativeMiddlePoint: { x: 0, y: 0 },
      size: { width: 0, height: 0 },
      backgroundColor: "default",
      disabled: false,
      editor: { edgeKind: "sequence" },
    } as any;
    const sortSchema = {
      ...schema(false),
      objectCollection: {
        ...schema(false).objectCollection,
        objects: [createList, sortList],
        flows: [flowCreate],
      },
      flows: [flowCreate],
    };
    const index = buildVariableIndex({ schema: sortSchema, metadata: EMPTY_MICROFLOW_METADATA_CATALOG });
    const sortNames = getVariablesForExpressionFromIndex(sortSchema, index, { objectId: "sort-list", fieldPath: "action.sortExpression" }).map(item => item.name);

    expect(sortNames).toContain("item");
    expect(sortNames).toContain("orders");
  });
});
