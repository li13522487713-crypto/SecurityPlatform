import { describe, expect, it } from "vitest";

import { createMetadataCatalog, EMPTY_MICROFLOW_METADATA_CATALOG } from "../metadata";
import { createObjectFromRegistry, createSequenceFlow } from "../adapters";
import { defaultMicroflowNodeRegistry, getMicroflowNodeRegistryKey } from "../node-registry";
import type { MicroflowObject, MicroflowSchema } from "../schema/types";
import { buildVariableIndex } from "../variables";
import { buildMicroflowExpressionCompletionOptions } from "../expression-editor/codemirror-microflow-expression";
import { inferExpressionType } from "./expression-type-inference";
import { parseExpression } from "./expression-parser";
import { validateExpression } from "./expression-validator";

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

function actionObject(key: string, id: string): Extract<MicroflowObject, { kind: "actionActivity" }> {
  const object = objectFrom(key, id);
  if (object.kind !== "actionActivity") {
    throw new Error(`Expected action activity for ${key}.`);
  }
  return object;
}

function schemaWith(
  objects: MicroflowObject[],
  flows: MicroflowSchema["flows"] = [],
): MicroflowSchema {
  return {
    schemaVersion: "1.0.0",
    mendixProfile: "mx10",
    id: "mf-expr",
    stableId: "mf-expr",
    name: "ExprTest",
    displayName: "ExprTest",
    moduleId: "module-a",
    parameters: [],
    returnType: { kind: "void" },
    objectCollection: { id: "root", officialType: "Microflows$MicroflowObjectCollection", objects, flows },
    flows,
    security: { allowedModuleRoleIds: [], allowedRoleNames: [], applyEntityAccess: true },
    concurrency: { allowConcurrentExecution: true },
    exposure: { exportLevel: "module", markAsUsed: false },
    validation: { issues: [] },
    editor: { viewport: { x: 0, y: 0, zoom: 1 }, zoom: 1, selection: {} },
    audit: { version: "1", status: "draft" },
  };
}

function rawExpression(raw: string) {
  return {
    raw,
    referencedVariables: [],
    references: { variables: [], entities: [], attributes: [], associations: [], enumerations: [], functions: [] },
    diagnostics: [],
  };
}

describe("expression validator", () => {
  const metadata = createMetadataCatalog({
    ...EMPTY_MICROFLOW_METADATA_CATALOG,
    entities: [
      { id: "System.User", name: "User", qualifiedName: "System.User", moduleName: "System", attributes: [], associations: [], specializations: [], isPersistable: false, isSystemEntity: true },
      { id: "System.Session", name: "Session", qualifiedName: "System.Session", moduleName: "System", attributes: [], associations: [], specializations: [], isPersistable: false, isSystemEntity: true },
      {
        id: "CRM.Customer",
        name: "Customer",
        qualifiedName: "CRM.Customer",
        moduleName: "CRM",
        attributes: [{ id: "CRM.Customer.Name", name: "Name", qualifiedName: "CRM.Customer.Name", type: { kind: "string" }, required: false }],
        associations: [{ associationQualifiedName: "CRM.Customer_Order", targetEntityQualifiedName: "CRM.Order", direction: "sourceToTarget", multiplicity: "oneToMany" }],
        specializations: [],
        isPersistable: true,
      },
      {
        id: "CRM.Order",
        name: "Order",
        qualifiedName: "CRM.Order",
        moduleName: "CRM",
        attributes: [{ id: "CRM.Order.Number", name: "Number", qualifiedName: "CRM.Order.Number", type: { kind: "string" }, required: false }],
        associations: [],
        specializations: [],
        isPersistable: true,
      },
    ],
    associations: [{
      id: "CRM.Customer_Order",
      name: "Customer_Order",
      qualifiedName: "CRM.Customer_Order",
      sourceEntityQualifiedName: "CRM.Customer",
      targetEntityQualifiedName: "CRM.Order",
      multiplicity: "oneToMany",
      direction: "sourceToTarget",
    }],
  });

  const start = objectFrom("startEvent", "start");
  const end = objectFrom("endEvent", "end", 240, 0);
  const schema = schemaWith([start, end]);
  const variableIndex = buildVariableIndex(schema, metadata);

  it("exposes $currentSession as a system variable", () => {
    expect(variableIndex.byName?.["$currentSession"]?.[0]?.source.kind).toBe("system");
  });

  it("flags slash division and accepts div operator", () => {
    const slashValidation = validateExpression({
      expression: "1 / 2",
      schema,
      metadata,
      variableIndex,
      context: { objectId: start.id, fieldPath: "test" },
    });
    const divInference = inferExpressionType({
      expression: "1 div 2",
      schema,
      metadata,
      variableIndex,
      objectId: start.id,
      fieldPath: "test",
    });

    expect(slashValidation.diagnostics.some(item => item.code === "MF_EXPR_USE_DIV_OPERATOR")).toBe(true);
    expect(divInference.inferredType.kind).toBe("decimal");
  });

  it("treats empty as a literal and validates supported functions", () => {
    const parsed = parseExpression("$currentUser = empty");
    const validation = validateExpression({
      expression: "toLowerCase('ABC')",
      schema,
      metadata,
      variableIndex,
      context: { objectId: start.id, fieldPath: "test", expectedType: { kind: "string" } },
    });

    expect(parsed.diagnostics).toEqual([]);
    expect(validation.diagnostics.some(item => item.code === "MF_EXPR_UNSUPPORTED_FUNCTION")).toBe(false);
  });

  it("supports if-then-else expressions and reports branch type mismatches", () => {
    const valid = validateExpression({
      expression: "if true then 'green' else 'red'",
      schema,
      metadata,
      variableIndex,
      context: { objectId: start.id, fieldPath: "test", expectedType: { kind: "string" } },
    });
    const invalid = validateExpression({
      expression: "if true then 'green' else 1",
      schema,
      metadata,
      variableIndex,
      context: { objectId: start.id, fieldPath: "test", expectedType: { kind: "string" } },
    });

    expect(valid.inferredType.kind).toBe("string");
    expect(valid.diagnostics.some(item => item.code === "MF_EXPR_TYPE_MISMATCH")).toBe(false);
    expect(invalid.diagnostics.some(item => item.code === "MF_EXPR_IF_BRANCH_TYPE_MISMATCH")).toBe(true);
  });

  it("validates multi-level association access paths", () => {
    const customerVarSchema = schemaWith([
      start,
      (() => {
        const base = createObjectFromRegistry(registry("activity:objectCreate"), { x: 120, y: 0 }, "customer-create") as MicroflowObject & { action: Record<string, unknown> };
        return {
          ...base,
          kind: "actionActivity",
          action: {
            ...base.action,
            entityQualifiedName: "CRM.Customer",
            outputVariableName: "customer",
            objectVariableName: "customer",
          },
        } as MicroflowObject;
      })(),
      end,
    ]);
    const customerIndex = buildVariableIndex(customerVarSchema, metadata);
    const validation = validateExpression({
      expression: "$customer/CRM.Customer_Order/CRM.Order/Number",
      schema: customerVarSchema,
      metadata,
      variableIndex: customerIndex,
      context: { objectId: "customer-create", fieldPath: "test" },
    });

    expect(validation.diagnostics.some(item => item.code === "MF_EXPR_MEMBER_NOT_FOUND")).toBe(false);
  });

  it("offers system variables and multi-level association paths in completion options", () => {
    const customerVarSchema = {
      ...schemaWith([start, end]),
      parameters: [
        {
          id: "param-customer",
          name: "customer",
          dataType: { kind: "object", entityQualifiedName: "CRM.Customer" } as const,
          required: true,
        },
      ],
    };
    const customerIndex = buildVariableIndex(customerVarSchema, metadata);
    const options = buildMicroflowExpressionCompletionOptions({
      schema: customerVarSchema,
      metadata,
      variableIndex: customerIndex,
      objectId: end.id,
      fieldPath: "test",
    });

    expect(options.some(option => option.value === "$currentSession")).toBe(true);
    expect(options.some(option => option.value === "$customer/Name")).toBe(true);
    expect(options.some(option => option.value === "$customer/CRM.Customer_Order/CRM.Order/Number")).toBe(true);
  });

  it("offers $currentIndex inside loop body completion options", () => {
    const inner = actionObject("activity:variableCreate", "loop-inner");
    const loop = objectFrom("loop", "loop-node");
    const createList = actionObject("activity:listCreate", "create-list");
    if (loop.kind !== "loopedActivity") {
      throw new Error("Expected loopedActivity.");
    }
    createList.action = {
      ...createList.action,
      outputListVariableName: "orders",
      entityQualifiedName: "CRM.Order",
      elementType: { kind: "object", entityQualifiedName: "CRM.Order" },
    } as typeof createList.action;
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
      iteratorVariableDataType: { kind: "object", entityQualifiedName: "CRM.Order" },
    };
    const loopSchema = schemaWith([start, createList, loop, end]);
    const loopIndex = buildVariableIndex(loopSchema, metadata);

    const options = buildMicroflowExpressionCompletionOptions({
      schema: loopSchema,
      metadata,
      variableIndex: loopIndex,
      objectId: "loop-inner",
      fieldPath: "test",
    });

    expect(options.some(option => option.value === "$currentIndex")).toBe(true);
    expect(options.some(option => option.value === "$orderItem")).toBe(true);
  });

  it("offers $latestError and $latestHttpResponse inside REST error-handler completion options", () => {
    const restCall = actionObject("activity:callRest", "rest-call");
    const logError = actionObject("activity:logMessage", "log-error");
    const errorFlow = createSequenceFlow({
      originObjectId: restCall.id,
      destinationObjectId: logError.id,
      isErrorHandler: true,
      editor: { edgeKind: "errorHandler", label: "error" },
    });
    const errorSchema = {
      ...schemaWith([start, restCall, logError, end], [
        createSequenceFlow({ originObjectId: start.id, destinationObjectId: restCall.id }),
        errorFlow,
      ]),
    };
    const errorIndex = buildVariableIndex(errorSchema, metadata);

    const options = buildMicroflowExpressionCompletionOptions({
      schema: errorSchema,
      metadata,
      variableIndex: errorIndex,
      objectId: "log-error",
      fieldPath: "action.template.text",
    });

    expect(options.some(option => option.value === "$latestError")).toBe(true);
    expect(options.some(option => option.value === "$latestHttpResponse")).toBe(true);
  });

  it("offers $latestError and $latestSoapFault inside webServiceCall error-handler completion options", () => {
    const soapCall = actionObject("activity:callWebService", "soap-call");
    const logError = actionObject("activity:logMessage", "log-soap-error");
    const errorFlow = createSequenceFlow({
      originObjectId: soapCall.id,
      destinationObjectId: logError.id,
      isErrorHandler: true,
      editor: { edgeKind: "errorHandler", label: "error" },
    });
    const errorSchema = {
      ...schemaWith([start, soapCall, logError, end], [
        createSequenceFlow({ originObjectId: start.id, destinationObjectId: soapCall.id }),
        errorFlow,
      ]),
    };
    const errorIndex = buildVariableIndex(errorSchema, metadata);

    const options = buildMicroflowExpressionCompletionOptions({
      schema: errorSchema,
      metadata,
      variableIndex: errorIndex,
      objectId: "log-soap-error",
      fieldPath: "action.template.text",
    });

    expect(options.some(option => option.value === "$latestError")).toBe(true);
    expect(options.some(option => option.value === "$latestSoapFault")).toBe(true);
  });

  it("offers filterList item variable inside condition expression completion options", () => {
    const createList = actionObject("activity:listCreate", "create-list");
    createList.action = {
      ...createList.action,
      outputListVariableName: "orders",
      listVariableName: "orders",
      elementType: { kind: "integer" },
      itemType: { kind: "integer" },
      listType: "mutable",
    } as typeof createList.action;
    const filterList = actionObject("activity:listFilter", "filter-list");
    filterList.action = {
      ...filterList.action,
      sourceListVariableName: "orders",
      listVariableName: "orders",
      outputVariableName: "positiveOrders",
      itemVariableName: "item",
      itemType: { kind: "integer" },
      conditionExpression: rawExpression("$item > 2"),
      filterExpression: rawExpression("$item > 2"),
    } as typeof filterList.action;
    const filterSchema = schemaWith(
      [start, createList, filterList, end],
      [
        createSequenceFlow({ originObjectId: start.id, destinationObjectId: createList.id }),
        createSequenceFlow({ originObjectId: createList.id, destinationObjectId: filterList.id }),
        createSequenceFlow({ originObjectId: filterList.id, destinationObjectId: end.id }),
      ],
    );
    const filterIndex = buildVariableIndex(filterSchema, metadata);

    const options = buildMicroflowExpressionCompletionOptions({
      schema: filterSchema,
      metadata,
      variableIndex: filterIndex,
      objectId: "filter-list",
      fieldPath: "action.conditionExpression",
    });

    expect(options.some(option => option.value === "$item")).toBe(true);
    expect(options.some(option => option.value === "$orders")).toBe(true);
  });

  it("offers changeList removeWhere item variable inside condition expression completion options", () => {
    const createList = actionObject("activity:listCreate", "create-list-for-remove-where");
    createList.action = {
      ...createList.action,
      outputListVariableName: "orders",
      listVariableName: "orders",
      elementType: { kind: "integer" },
      itemType: { kind: "integer" },
      listType: "mutable",
    } as typeof createList.action;
    const changeList = actionObject("activity:listChange", "change-list-remove-where");
    changeList.action = {
      ...changeList.action,
      targetListVariableName: "orders",
      operation: "removeWhere",
      conditionExpression: rawExpression("$item > 2"),
    } as typeof changeList.action;
    const removeWhereSchema = schemaWith(
      [start, createList, changeList, end],
      [
        createSequenceFlow({ originObjectId: start.id, destinationObjectId: createList.id }),
        createSequenceFlow({ originObjectId: createList.id, destinationObjectId: changeList.id }),
        createSequenceFlow({ originObjectId: changeList.id, destinationObjectId: end.id }),
      ],
    );
    const removeWhereIndex = buildVariableIndex(removeWhereSchema, metadata);

    const options = buildMicroflowExpressionCompletionOptions({
      schema: removeWhereSchema,
      metadata,
      variableIndex: removeWhereIndex,
      objectId: "change-list-remove-where",
      fieldPath: "action.conditionExpression",
    });

    expect(options.some(option => option.value === "$item")).toBe(true);
    expect(options.some(option => option.value === "$orders")).toBe(true);
  });

  it("offers listOperation filter item variable inside filter expression completion options", () => {
    const createList = actionObject("activity:listCreate", "create-list-for-list-operation-filter");
    createList.action = {
      ...createList.action,
      outputListVariableName: "orders",
      listVariableName: "orders",
      elementType: { kind: "integer" },
      itemType: { kind: "integer" },
      listType: "mutable",
    } as typeof createList.action;
    const listOperation = actionObject("activity:listOperation", "list-operation-filter");
    listOperation.action = {
      ...listOperation.action,
      leftListVariableName: "orders",
      sourceListVariableName: "orders",
      operation: "filter",
      filterExpression: rawExpression("$item > 2"),
      expression: rawExpression("$item > 2"),
      outputVariableName: "positiveOrders",
      outputListVariableName: "positiveOrders",
      outputElementType: { kind: "integer" },
    } as typeof listOperation.action;
    const filterSchema = schemaWith(
      [start, createList, listOperation, end],
      [
        createSequenceFlow({ originObjectId: start.id, destinationObjectId: createList.id }),
        createSequenceFlow({ originObjectId: createList.id, destinationObjectId: listOperation.id }),
        createSequenceFlow({ originObjectId: listOperation.id, destinationObjectId: end.id }),
      ],
    );
    const filterIndex = buildVariableIndex(filterSchema, metadata);

    const options = buildMicroflowExpressionCompletionOptions({
      schema: filterSchema,
      metadata,
      variableIndex: filterIndex,
      objectId: "list-operation-filter",
      fieldPath: "action.filterExpression",
    });

    expect(options.some(option => option.value === "$item")).toBe(true);
    expect(options.some(option => option.value === "$orders")).toBe(true);
  });

  it("offers listOperation map item variable inside map expression completion options", () => {
    const createList = actionObject("activity:listCreate", "create-list-for-list-operation-map");
    createList.action = {
      ...createList.action,
      outputListVariableName: "orders",
      listVariableName: "orders",
      elementType: { kind: "integer" },
      itemType: { kind: "integer" },
      listType: "mutable",
    } as typeof createList.action;
    const listOperation = actionObject("activity:listOperation", "list-operation-map");
    listOperation.action = {
      ...listOperation.action,
      leftListVariableName: "orders",
      sourceListVariableName: "orders",
      operation: "map",
      expression: rawExpression("$item + 1"),
      outputVariableName: "mappedOrders",
      outputListVariableName: "mappedOrders",
      outputElementType: { kind: "integer" },
    } as typeof listOperation.action;
    const mapSchema = schemaWith(
      [start, createList, listOperation, end],
      [
        createSequenceFlow({ originObjectId: start.id, destinationObjectId: createList.id }),
        createSequenceFlow({ originObjectId: createList.id, destinationObjectId: listOperation.id }),
        createSequenceFlow({ originObjectId: listOperation.id, destinationObjectId: end.id }),
      ],
    );
    const mapIndex = buildVariableIndex(mapSchema, metadata);

    const options = buildMicroflowExpressionCompletionOptions({
      schema: mapSchema,
      metadata,
      variableIndex: mapIndex,
      objectId: "list-operation-map",
      fieldPath: "action.expression",
    });

    expect(options.some(option => option.value === "$item")).toBe(true);
    expect(options.some(option => option.value === "$orders")).toBe(true);
  });

  it("offers listOperation sort item variable inside sort expression completion options", () => {
    const createList = actionObject("activity:listCreate", "create-list-for-list-operation-sort");
    createList.action = {
      ...createList.action,
      outputListVariableName: "orders",
      listVariableName: "orders",
      elementType: { kind: "object", entityQualifiedName: "Sales.Order" },
      itemType: { kind: "object", entityQualifiedName: "Sales.Order" },
      listType: "mutable",
    } as typeof createList.action;
    const listOperation = actionObject("activity:listOperation", "list-operation-sort");
    listOperation.action = {
      ...listOperation.action,
      leftListVariableName: "orders",
      sourceListVariableName: "orders",
      operation: "sort",
      sortExpression: rawExpression("$item/score"),
      sortKeys: [{ expression: rawExpression("$item/score"), direction: "asc" }],
      outputVariableName: "sortedOrders",
      outputListVariableName: "sortedOrders",
      outputElementType: { kind: "object", entityQualifiedName: "Sales.Order" },
    } as typeof listOperation.action;
    const sortSchema = schemaWith(
      [start, createList, listOperation, end],
      [
        createSequenceFlow({ originObjectId: start.id, destinationObjectId: createList.id }),
        createSequenceFlow({ originObjectId: createList.id, destinationObjectId: listOperation.id }),
        createSequenceFlow({ originObjectId: listOperation.id, destinationObjectId: end.id }),
      ],
    );
    const sortIndex = buildVariableIndex(sortSchema, metadata);

    const options = buildMicroflowExpressionCompletionOptions({
      schema: sortSchema,
      metadata,
      variableIndex: sortIndex,
      objectId: "list-operation-sort",
      fieldPath: "action.sortExpression",
    });

    expect(options.some(option => option.value === "$item")).toBe(true);
    expect(options.some(option => option.value === "$orders")).toBe(true);
  });

  it("offers sortList item variable inside sort expression completion options", () => {
    const createList = actionObject("activity:listCreate", "create-list-for-sort-list");
    createList.action = {
      ...createList.action,
      outputListVariableName: "orders",
      listVariableName: "orders",
      elementType: { kind: "object", entityQualifiedName: "Sales.Order" },
      itemType: { kind: "object", entityQualifiedName: "Sales.Order" },
      listType: "mutable",
    } as typeof createList.action;
    const sortList = actionObject("activity:listSort", "sort-list");
    sortList.action = {
      ...sortList.action,
      kind: "sortList",
      sourceListVariableName: "orders",
      listVariableName: "orders",
      sortExpression: rawExpression("$item/score"),
      direction: "asc",
      outputVariableName: "sortedOrders",
      outputElementType: { kind: "object", entityQualifiedName: "Sales.Order" },
    } as typeof sortList.action;
    const sortSchema = schemaWith(
      [start, createList, sortList, end],
      [
        createSequenceFlow({ originObjectId: start.id, destinationObjectId: createList.id }),
        createSequenceFlow({ originObjectId: createList.id, destinationObjectId: sortList.id }),
        createSequenceFlow({ originObjectId: sortList.id, destinationObjectId: end.id }),
      ],
    );
    const sortIndex = buildVariableIndex(sortSchema, metadata);

    const options = buildMicroflowExpressionCompletionOptions({
      schema: sortSchema,
      metadata,
      variableIndex: sortIndex,
      objectId: "sort-list",
      fieldPath: "action.sortExpression",
    });

    expect(options.some(option => option.value === "$item")).toBe(true);
    expect(options.some(option => option.value === "$orders")).toBe(true);
  });

  it("offers sortList item variable inside sort key expression completion options", () => {
    const createList = actionObject("activity:listCreate", "create-list-for-sort-list-sort-key");
    createList.action = {
      ...createList.action,
      outputListVariableName: "orders",
      listVariableName: "orders",
      elementType: { kind: "object", entityQualifiedName: "Sales.Order" },
      itemType: { kind: "object", entityQualifiedName: "Sales.Order" },
      listType: "mutable",
    } as typeof createList.action;
    const sortList = actionObject("activity:listSort", "sort-list-sort-key");
    sortList.action = {
      ...sortList.action,
      kind: "sortList",
      sourceListVariableName: "orders",
      listVariableName: "orders",
      sortKeys: [{ expression: rawExpression("$item/score"), direction: "asc" }],
      direction: "asc",
      outputVariableName: "sortedOrders",
      outputElementType: { kind: "object", entityQualifiedName: "Sales.Order" },
    } as typeof sortList.action;
    const sortSchema = schemaWith(
      [start, createList, sortList, end],
      [
        createSequenceFlow({ originObjectId: start.id, destinationObjectId: createList.id }),
        createSequenceFlow({ originObjectId: createList.id, destinationObjectId: sortList.id }),
        createSequenceFlow({ originObjectId: sortList.id, destinationObjectId: end.id }),
      ],
    );
    const sortIndex = buildVariableIndex(sortSchema, metadata);

    const options = buildMicroflowExpressionCompletionOptions({
      schema: sortSchema,
      metadata,
      variableIndex: sortIndex,
      objectId: "sort-list-sort-key",
      fieldPath: "action.sortKeys.0.expression",
    });

    expect(options.some(option => option.value === "$item")).toBe(true);
    expect(options.some(option => option.value === "$orders")).toBe(true);
  });

  it("infers newly completed function signatures", () => {
    const cases: Array<{ expression: string; expected: string }> = [
      { expression: "replaceFirst('abc','a','x')", expected: "string" },
      { expression: "urlEncode('a b')", expected: "string" },
      { expression: "urlDecode('a%20b')", expected: "string" },
      { expression: "htmlEncode('<a>')", expected: "string" },
      { expression: "addMonths(dateTime(2026,1,1,0,0,0),1)", expected: "dateTime" },
      { expression: "addYears(dateTime(2026,1,1,0,0,0),1)", expected: "dateTime" },
      { expression: "addHours(dateTime(2026,1,1,0,0,0),1)", expected: "dateTime" },
      { expression: "addMinutes(dateTime(2026,1,1,0,0,0),1)", expected: "dateTime" },
      { expression: "addSeconds(dateTime(2026,1,1,0,0,0),1)", expected: "dateTime" },
      { expression: "addWeeks(dateTime(2026,1,1,0,0,0),1)", expected: "dateTime" },
      { expression: "addQuarters(dateTime(2026,1,1,0,0,0),1)", expected: "dateTime" },
      { expression: "getYear(dateTime(2026,1,1,0,0,0))", expected: "integer" },
      { expression: "getMonth(dateTime(2026,1,1,0,0,0))", expected: "integer" },
      { expression: "getDay(dateTime(2026,1,1,0,0,0))", expected: "integer" },
      { expression: "getHour(dateTime(2026,1,1,0,0,0))", expected: "integer" },
      { expression: "getMinute(dateTime(2026,1,1,0,0,0))", expected: "integer" },
      { expression: "getSecond(dateTime(2026,1,1,0,0,0))", expected: "integer" },
      { expression: "getDayOfWeek(dateTime(2026,1,1,0,0,0))", expected: "integer" },
      { expression: "dateDiff(dateTime(2026,1,1,0,0,0),dateTime(2026,1,2,0,0,0),'day')", expected: "integer" },
      { expression: "currentDateTime()", expected: "dateTime" },
      { expression: "parseInteger('1')", expected: "integer" },
      { expression: "parseDecimal('1.23')", expected: "decimal" },
      { expression: "formatDecimal(1.23,'0.00')", expected: "string" },
    ];

    for (const item of cases) {
      const result = inferExpressionType({
        expression: item.expression,
        schema,
        metadata,
        variableIndex,
        objectId: start.id,
        fieldPath: "test",
      });
      expect(result.inferredType.kind, item.expression).toBe(item.expected);
    }
  });
});
