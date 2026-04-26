import type {
  MicroflowAction,
  MicroflowActionActivity,
  MicroflowDataType,
  MicroflowExpression,
  MicroflowFlow,
  MicroflowLine,
  MicroflowObject,
  MicroflowObjectBase,
  MicroflowSchema,
  MicroflowVariableIndex
} from "./types";

const boolType: MicroflowDataType = { kind: "boolean" };
const stringType: MicroflowDataType = { kind: "string" };
const orderType: MicroflowDataType = { kind: "object", entityQualifiedName: "Sales.Order" };
const orderLineListType: MicroflowDataType = { kind: "list", itemType: { kind: "object", entityQualifiedName: "Sales.OrderLine" } };

function expr(raw: string, inferredType?: MicroflowDataType): MicroflowExpression {
  const variables = Array.from(raw.matchAll(/\$[A-Za-z_][\w]*/g)).map(match => match[0]);
  return {
    id: `expr-${Math.abs(raw.split("").reduce((total, char) => total + char.charCodeAt(0), 0))}`,
    raw,
    inferredType,
    references: { variables, entities: [], attributes: [], associations: [], enumerations: [], functions: [] },
    diagnostics: []
  };
}

function line(): MicroflowLine {
  return {
    kind: "orthogonal",
    points: [],
    routing: { mode: "auto", bendPoints: [] },
    style: { strokeType: "solid", strokeWidth: 2, arrow: "target" }
  };
}

function base(id: string, kind: MicroflowObject["kind"], officialType: string, caption: string, x: number, y: number, width = 176, height = 76): MicroflowObjectBase {
  return {
    id,
    stableId: id,
    kind,
    officialType,
    caption,
    documentation: "",
    relativeMiddlePoint: { x, y },
    size: { width, height },
    editor: { iconKey: kind }
  };
}

function actionBase(kind: MicroflowAction["kind"], officialType: string, category: MicroflowAction["editor"]["category"]): MicroflowAction["editor"] & Pick<MicroflowAction, "id" | "officialType" | "kind" | "errorHandlingType"> {
  return {
    id: `action-${kind}`,
    officialType,
    kind,
    errorHandlingType: "rollback",
    category,
    iconKey: kind,
    availability: "supported"
  } as MicroflowAction["editor"] & Pick<MicroflowAction, "id" | "officialType" | "kind" | "errorHandlingType">;
}

function activity(id: string, caption: string, x: number, y: number, action: MicroflowAction): MicroflowActionActivity {
  return {
    ...base(id, "actionActivity", "Microflows$ActionActivity", caption, x, y),
    kind: "actionActivity",
    officialType: "Microflows$ActionActivity",
    caption,
    autoGenerateCaption: false,
    backgroundColor: "default",
    disabled: false,
    action
  };
}

function sequence(id: string, originObjectId: string, destinationObjectId: string, extra: Partial<Extract<MicroflowFlow, { kind: "sequence" }>> = {}): Extract<MicroflowFlow, { kind: "sequence" }> {
  return {
    id,
    stableId: id,
    kind: "sequence",
    officialType: "Microflows$SequenceFlow",
    originObjectId,
    destinationObjectId,
    originConnectionIndex: 0,
    destinationConnectionIndex: 0,
    caseValues: [],
    isErrorHandler: false,
    line: line(),
    editor: { edgeKind: "sequence" },
    ...extra
  };
}

const variables: MicroflowVariableIndex = {
  parameters: {
    orderId: { name: "orderId", dataType: stringType, source: { kind: "parameter", parameterId: "param-order-id" }, scope: { collectionId: "root-collection" }, readonly: true },
    member: { name: "member", dataType: { kind: "object", entityQualifiedName: "University.Member" }, source: { kind: "parameter", parameterId: "param-member" }, scope: { collectionId: "root-collection" }, readonly: true }
  },
  localVariables: {},
  objectOutputs: {
    order: { name: "order", dataType: orderType, source: { kind: "actionOutput", objectId: "retrieve-order", actionId: "action-retrieve" }, scope: { collectionId: "root-collection", startObjectId: "retrieve-order" }, readonly: false }
  },
  listOutputs: {
    orderLines: { name: "orderLines", dataType: orderLineListType, source: { kind: "actionOutput", objectId: "retrieve-order-lines", actionId: "action-retrieve-lines" }, scope: { collectionId: "root-collection", startObjectId: "retrieve-order-lines" }, readonly: false }
  },
  loopVariables: {
    orderLine: { name: "orderLine", dataType: { kind: "object", entityQualifiedName: "Sales.OrderLine" }, source: { kind: "loopIterator", loopObjectId: "loop-order-lines" }, scope: { collectionId: "loop-order-lines-collection", loopObjectId: "loop-order-lines" }, readonly: true }
  },
  errorVariables: {
    $latestError: { name: "$latestError", dataType: { kind: "object", entityQualifiedName: "System.Error" }, source: { kind: "errorContext", flowId: "flow-rest-error" }, scope: { collectionId: "root-collection", errorHandlerFlowId: "flow-rest-error", startObjectId: "log-rest-error" }, readonly: true },
    $latestHttpResponse: { name: "$latestHttpResponse", dataType: { kind: "object", entityQualifiedName: "System.HttpResponse" }, source: { kind: "errorContext", flowId: "flow-rest-error" }, scope: { collectionId: "root-collection", errorHandlerFlowId: "flow-rest-error", startObjectId: "log-rest-error" }, readonly: true },
    $latestSoapFault: { name: "$latestSoapFault", dataType: { kind: "object", entityQualifiedName: "System.SoapFault" }, source: { kind: "errorContext", flowId: "flow-rest-error" }, scope: { collectionId: "root-collection", errorHandlerFlowId: "flow-rest-error", startObjectId: "log-rest-error" }, readonly: true }
  },
  systemVariables: {
    $currentUser: { name: "$currentUser", dataType: { kind: "object", entityQualifiedName: "System.User" }, source: { kind: "system", name: "$currentUser" }, scope: { collectionId: "root-collection" }, readonly: true },
    $currentIndex: { name: "$currentIndex", dataType: { kind: "integer" }, source: { kind: "system", name: "$currentIndex" }, scope: { collectionId: "loop-order-lines-collection", loopObjectId: "loop-order-lines" }, readonly: true }
  }
};

const objects: MicroflowObject[] = [
  { ...base("start", "startEvent", "Microflows$StartEvent", "Start", 40, 220, 132, 70), kind: "startEvent", officialType: "Microflows$StartEvent", trigger: { type: "manual" } },
  { ...base("param-order-id", "parameterObject", "Microflows$MicroflowParameterObject", "Parameter: orderId", 40, 80, 172, 70), kind: "parameterObject", officialType: "Microflows$MicroflowParameterObject", parameterId: "param-order-id" },
  { ...base("param-member", "parameterObject", "Microflows$MicroflowParameterObject", "Parameter: member", 40, 420, 172, 70), kind: "parameterObject", officialType: "Microflows$MicroflowParameterObject", parameterId: "param-member" },
  activity("retrieve-order", "Retrieve Order", 260, 215, {
    ...actionBase("retrieve", "Microflows$RetrieveAction", "object"),
    editor: { category: "object", iconKey: "retrieve", availability: "supported" },
    kind: "retrieve",
    officialType: "Microflows$RetrieveAction",
    outputVariableName: "order",
    retrieveSource: {
      kind: "database",
      officialType: "Microflows$DatabaseRetrieveSource",
      entityQualifiedName: "Sales.Order",
      xPathConstraint: expr("[OrderId = $orderId]", boolType),
      sortItemList: { items: [{ attributeQualifiedName: "Sales.Order.CreatedDate", direction: "desc" }] },
      range: { kind: "first", officialType: "Microflows$ConstantRange", value: "first" }
    }
  } as MicroflowAction),
  {
    ...base("decision-processable", "exclusiveSplit", "Microflows$ExclusiveSplit", "Order status processable?", 520, 205, 160, 110),
    kind: "exclusiveSplit",
    officialType: "Microflows$ExclusiveSplit",
    splitCondition: { kind: "expression", expression: expr("$order/Status = 'Pending'", boolType), resultType: "boolean" },
    errorHandlingType: "rollback"
  },
  activity("change-order", "Update Order", 800, 120, {
    ...actionBase("changeMembers", "Microflows$ChangeMembersAction", "object"),
    editor: { category: "object", iconKey: "changeMembers", availability: "supported" },
    kind: "changeMembers",
    officialType: "Microflows$ChangeMembersAction",
    changeVariableName: "order",
    memberChanges: [{ id: "change-status", memberQualifiedName: "Sales.Order.Status", memberKind: "attribute", valueExpression: expr("'Processing'", stringType), assignmentKind: "set" }],
    commit: { enabled: false, withEvents: true, refreshInClient: false },
    validateObject: true
  } as MicroflowAction),
  activity("commit-order", "Commit Order", 1040, 120, {
    ...actionBase("commit", "Microflows$CommitAction", "object"),
    editor: { category: "object", iconKey: "commit", availability: "supported" },
    kind: "commit",
    officialType: "Microflows$CommitAction",
    objectOrListVariableName: "order",
    withEvents: true,
    refreshInClient: true
  } as MicroflowAction),
  activity("call-inventory-rest", "Check Inventory", 1280, 120, {
    ...actionBase("restCall", "Microflows$RestCallAction", "integration"),
    editor: { category: "integration", iconKey: "restCall", availability: "supported" },
    kind: "restCall",
    officialType: "Microflows$RestCallAction",
    errorHandlingType: "customWithRollback",
    request: { method: "POST", urlExpression: expr("'/api/inventory/check'", stringType), headers: [], queryParameters: [], body: { kind: "json", expression: expr("{ orderId: $orderId }", { kind: "json" }) } },
    response: { handling: { kind: "json", outputVariableName: "inventoryResult" }, statusCodeVariableName: "inventoryStatus", headersVariableName: "inventoryHeaders" },
    timeoutSeconds: 20
  } as MicroflowAction),
  {
    ...base("decision-stock", "exclusiveSplit", "Microflows$ExclusiveSplit", "Inventory enough?", 1530, 106, 160, 110),
    kind: "exclusiveSplit",
    officialType: "Microflows$ExclusiveSplit",
    splitCondition: { kind: "expression", expression: expr("$inventoryResult/available = true", boolType), resultType: "boolean" },
    errorHandlingType: "rollback"
  },
  {
    ...base("loop-order-lines", "loopedActivity", "Microflows$LoopedActivity", "Loop Order Lines", 1780, 70, 230, 118),
    kind: "loopedActivity",
    officialType: "Microflows$LoopedActivity",
    documentation: "Nested collection sample: validate each line and branch to Break/Continue.",
    errorHandlingType: "continue",
    loopSource: { kind: "iterableList", officialType: "Microflows$IterableList", listVariableName: "orderLines", iteratorVariableName: "orderLine", currentIndexVariableName: "$currentIndex" },
    objectCollection: {
      id: "loop-order-lines-collection",
      officialType: "Microflows$MicroflowObjectCollection",
      objects: [
        activity("loop-log-line", "Log Line", 1810, 230, {
          ...actionBase("logMessage", "Microflows$LogMessageAction", "logging"),
          editor: { category: "logging", iconKey: "logMessage", availability: "supported" },
          kind: "logMessage",
          officialType: "Microflows$LogMessageAction",
          level: "debug",
          logNodeName: "OrderLine",
          template: { text: "Line index {$currentIndex}", arguments: [expr("$currentIndex", { kind: "integer" })] },
          includeContextVariables: true,
          includeTraceId: true
        } as MicroflowAction),
        { ...base("loop-decision-line", "exclusiveSplit", "Microflows$ExclusiveSplit", "Line valid?", 2070, 230, 150, 100), kind: "exclusiveSplit", officialType: "Microflows$ExclusiveSplit", splitCondition: { kind: "expression", expression: expr("$orderLine/Quantity > 0", boolType), resultType: "boolean" }, errorHandlingType: "rollback" },
        { ...base("continue-line", "continueEvent", "Microflows$ContinueEvent", "Continue", 2320, 170, 132, 70), kind: "continueEvent", officialType: "Microflows$ContinueEvent" },
        { ...base("break-line", "breakEvent", "Microflows$BreakEvent", "Break", 2320, 300, 132, 70), kind: "breakEvent", officialType: "Microflows$BreakEvent" }
      ]
    }
  },
  {
    ...base("inheritance-member", "inheritanceSplit", "Microflows$InheritanceSplit", "Member Type?", 2040, 92, 168, 108),
    kind: "inheritanceSplit",
    officialType: "Microflows$InheritanceSplit",
    inputObjectVariableName: "member",
    generalizedEntityQualifiedName: "University.Member",
    allowedSpecializations: ["University.Student", "University.Teacher"],
    entity: { generalizedEntityQualifiedName: "University.Member", allowedSpecializations: ["University.Student", "University.Teacher"] },
    errorHandlingType: "rollback"
  },
  activity("log-success", "Log Success", 2260, 86, {
    ...actionBase("logMessage", "Microflows$LogMessageAction", "logging"),
    editor: { category: "logging", iconKey: "logMessage", availability: "supported" },
    kind: "logMessage",
    officialType: "Microflows$LogMessageAction",
    level: "info",
    logNodeName: "Order",
    template: { text: "Order processed", arguments: [] },
    includeContextVariables: true,
    includeTraceId: true
  } as MicroflowAction),
  activity("log-stock-shortage", "Inventory Not Enough", 1800, 320, {
    ...actionBase("logMessage", "Microflows$LogMessageAction", "logging"),
    editor: { category: "logging", iconKey: "logMessage", availability: "supported" },
    kind: "logMessage",
    officialType: "Microflows$LogMessageAction",
    level: "warning",
    logNodeName: "Inventory",
    template: { text: "Inventory not enough for {$orderId}", arguments: [expr("$orderId", stringType)] },
    includeContextVariables: false,
    includeTraceId: true
  } as MicroflowAction),
  activity("log-rest-error", "Log REST Error", 1280, 330, {
    ...actionBase("logMessage", "Microflows$LogMessageAction", "logging"),
    editor: { category: "logging", iconKey: "logMessage", availability: "supported" },
    kind: "logMessage",
    officialType: "Microflows$LogMessageAction",
    level: "error",
    logNodeName: "Inventory",
    template: { text: "REST failed: {$latestError}", arguments: [expr("$latestError")] },
    includeContextVariables: true,
    includeTraceId: true
  } as MicroflowAction),
  { ...base("end-success", "endEvent", "Microflows$EndEvent", "End True", 2480, 95, 132, 70), kind: "endEvent", officialType: "Microflows$EndEvent", returnValue: expr("true", boolType), endBehavior: { type: "normalReturn" } },
  { ...base("end-false", "endEvent", "Microflows$EndEvent", "End False", 2050, 330, 132, 70), kind: "endEvent", officialType: "Microflows$EndEvent", returnValue: expr("false", boolType), endBehavior: { type: "normalReturn" } },
  { ...base("error-event", "errorEvent", "Microflows$ErrorEvent", "Error", 1540, 330, 132, 70), kind: "errorEvent", officialType: "Microflows$ErrorEvent", error: { sourceVariableName: "$latestError", messageExpression: expr("$latestError/message", stringType) } },
  { ...base("annotation-main", "annotation", "Microflows$Annotation", "Note", 520, 40, 260, 90), kind: "annotation", officialType: "Microflows$Annotation", text: "AuthoringSchema is the source of truth. EditorGraph is derived." }
];

const flows: MicroflowFlow[] = [
  sequence("flow-start-retrieve", "start", "retrieve-order"),
  sequence("flow-retrieve-decision", "retrieve-order", "decision-processable"),
  sequence("flow-processable-true", "decision-processable", "change-order", { caseValues: [{ kind: "boolean", officialType: "Microflows$EnumerationCase", value: true, persistedValue: "true" }], editor: { edgeKind: "decisionCondition", label: "true" } }),
  sequence("flow-processable-false", "decision-processable", "end-false", { caseValues: [{ kind: "boolean", officialType: "Microflows$EnumerationCase", value: false, persistedValue: "false" }], editor: { edgeKind: "decisionCondition", label: "false" } }),
  sequence("flow-change-commit", "change-order", "commit-order"),
  sequence("flow-commit-rest", "commit-order", "call-inventory-rest"),
  sequence("flow-rest-stock", "call-inventory-rest", "decision-stock"),
  sequence("flow-rest-error", "call-inventory-rest", "log-rest-error", { isErrorHandler: true, editor: { edgeKind: "errorHandler", label: "error" } }),
  sequence("flow-log-error-event", "log-rest-error", "error-event"),
  sequence("flow-stock-true", "decision-stock", "loop-order-lines", { caseValues: [{ kind: "boolean", officialType: "Microflows$EnumerationCase", value: true, persistedValue: "true" }], editor: { edgeKind: "decisionCondition", label: "true" } }),
  sequence("flow-stock-false", "decision-stock", "log-stock-shortage", { caseValues: [{ kind: "boolean", officialType: "Microflows$EnumerationCase", value: false, persistedValue: "false" }], editor: { edgeKind: "decisionCondition", label: "false" } }),
  sequence("flow-shortage-end", "log-stock-shortage", "end-false"),
  sequence("flow-loop-inheritance", "loop-order-lines", "inheritance-member"),
  sequence("flow-inheritance-student", "inheritance-member", "log-success", { caseValues: [{ kind: "inheritance", officialType: "Microflows$InheritanceCase", entityQualifiedName: "University.Student" }], editor: { edgeKind: "objectTypeCondition", label: "Student" } }),
  sequence("flow-inheritance-teacher", "inheritance-member", "log-success", { caseValues: [{ kind: "inheritance", officialType: "Microflows$InheritanceCase", entityQualifiedName: "University.Teacher" }], editor: { edgeKind: "objectTypeCondition", label: "Teacher" } }),
  sequence("flow-success-end", "log-success", "end-success"),
  { id: "annotation-flow-main", stableId: "annotation-flow-main", kind: "annotation", officialType: "Microflows$AnnotationFlow", originObjectId: "annotation-main", destinationObjectId: "decision-processable", originConnectionIndex: 0, destinationConnectionIndex: 0, line: line(), editor: { label: "model note", description: "AnnotationFlow persists in flows.", showInExport: true } }
];

export const sampleMicroflowSchema: MicroflowSchema = {
  schemaVersion: "1.0.0",
  mendixProfile: "mx11",
  id: "mf-order-process",
  stableId: "mf-order-process",
  name: "OrderProcessing",
  displayName: "Order Processing Microflow",
  description: "Native AuthoringSchema sample covering actions, decisions, loop scope, error handler and compat fields.",
  documentation: "Sample used by the microflow editor acceptance suite.",
  moduleId: "sales",
  moduleName: "Sales",
  version: "v3",
  parameters: [
    { id: "param-order-id", stableId: "param-order-id", name: "orderId", dataType: stringType, required: true, documentation: "Order identifier.", defaultValue: expr("''", stringType), exampleValue: "SO-1001" },
    { id: "param-member", stableId: "param-member", name: "member", dataType: { kind: "object", entityQualifiedName: "University.Member" }, required: false, documentation: "Member used by inheritance split." }
  ],
  returnType: boolType,
  returnVariableName: "isProcessed",
  objectCollection: { id: "root-collection", officialType: "Microflows$MicroflowObjectCollection", objects },
  flows,
  security: { applyEntityAccess: true, allowedModuleRoleIds: ["sales-user"], allowedRoleNames: ["Sales User"] },
  concurrency: { allowConcurrentExecution: false, errorMessage: "Order processing is already running.", errorMicroflowId: null },
  exposure: { exportLevel: "module", markAsUsed: true, asMicroflowAction: { enabled: true, caption: "Process Order", category: "Sales" }, url: { enabled: true, path: "/orders/process", searchParameters: ["orderId"] } },
  variables,
  validation: { issues: [] },
  editor: {
    viewport: { x: 0, y: 0, zoom: 0.82 },
    zoom: 0.82,
    activeBottomPanel: "problems",
    leftPanelCollapsed: false,
    rightPanelCollapsed: false,
    bottomPanelCollapsed: false,
    showMiniMap: true,
    gridEnabled: true,
    selection: {},
    layoutMode: "freeform"
  },
  audit: { version: "v3", status: "draft", createdBy: "System", createdAt: "2026-04-26T00:00:00.000Z", updatedBy: "System", updatedAt: "2026-04-26T00:00:00.000Z" }
};
