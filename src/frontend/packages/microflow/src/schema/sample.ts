import type { MicroflowActivityType, MicroflowEdge, MicroflowNode, MicroflowPort, MicroflowSchema, MicroflowTypeRef } from "./types";
import { buildAuthoringFieldsFromLegacy } from "../adapters";

const stringType: MicroflowTypeRef = { kind: "primitive", name: "String" };
const booleanType: MicroflowTypeRef = { kind: "primitive", name: "Boolean" };
const orderType: MicroflowTypeRef = { kind: "entity", name: "Sales.Order", entity: "Sales.Order" };
const orderLineType: MicroflowTypeRef = { kind: "entity", name: "Sales.OrderLine", entity: "Sales.OrderLine" };
const orderListType: MicroflowTypeRef = { kind: "list", name: "List<Sales.Order>", itemType: orderType };
const orderLineListType: MicroflowTypeRef = { kind: "list", name: "List<Sales.OrderLine>", itemType: orderLineType };
const memberType: MicroflowTypeRef = { kind: "entity", name: "University.Member", entity: "University.Member" };

const sequenceIn: MicroflowPort = { id: "in", label: "In", direction: "input", kind: "sequenceIn", cardinality: "one", edgeTypes: ["sequence", "decisionCondition", "objectTypeCondition", "errorHandler"] };
const sequenceOut: MicroflowPort = { id: "out", label: "Out", direction: "output", kind: "sequenceOut", cardinality: "one", edgeTypes: ["sequence"] };
const errorOut: MicroflowPort = { id: "error", label: "Error", direction: "output", kind: "errorOut", cardinality: "zeroOrOne", edgeTypes: ["errorHandler"] };
const decisionTrue: MicroflowPort = { id: "true", label: "True", direction: "output", kind: "decisionOut", cardinality: "one", edgeTypes: ["decisionCondition"] };
const decisionFalse: MicroflowPort = { id: "false", label: "False", direction: "output", kind: "decisionOut", cardinality: "one", edgeTypes: ["decisionCondition"] };
const objectTypeOut: MicroflowPort = { id: "objectType", label: "Object Type", direction: "output", kind: "objectTypeOut", cardinality: "oneOrMore", edgeTypes: ["objectTypeCondition"] };
const annotationOut: MicroflowPort = { id: "note", label: "Note", direction: "output", kind: "annotation", cardinality: "zeroOrMore", edgeTypes: ["annotation"] };

function activity(id: string, title: string, activityType: MicroflowActivityType, x: number, y: number, config: Record<string, unknown> = {}, supportsErrorFlow = true): MicroflowNode {
  return {
    id,
    type: "activity",
    kind: "activity",
    title,
    titleZh: title,
    category: "activities",
    position: { x, y },
    ports: supportsErrorFlow ? [sequenceIn, sequenceOut, errorOut] : [sequenceIn, sequenceOut],
    config: {
      activityType,
      supportsErrorFlow,
      errorHandling: supportsErrorFlow ? { mode: "rollback", errorVariableName: "latestError" } : undefined,
      ...config
    },
    render: { iconKey: activityType, shape: "roundedRect", tone: activityType === "logMessage" ? "warning" : "neutral", width: 190, height: 76 },
    propertyForm: { formKey: `activity:${activityType}`, sections: supportsErrorFlow ? ["General", "Error Handling", "Output"] : ["General"] }
  } as MicroflowNode;
}

function eventNode(id: string, type: "startEvent" | "endEvent" | "errorEvent" | "breakEvent" | "continueEvent", title: string, x: number, y: number, parentLoopId?: string): MicroflowNode {
  const isEnd = type === "endEvent";
  return {
    id,
    type,
    kind: "event",
    title,
    titleZh: title,
    category: "events",
    parentLoopId,
    position: { x, y },
    ports: type === "startEvent" ? [sequenceOut] : [sequenceIn],
    config: isEnd
      ? { returnType: booleanType, returnValue: { id: `${id}-return`, language: "mendix", text: title.toLowerCase().includes("true") || title.toLowerCase().includes("success") ? "true" : "false", expectedType: booleanType } }
      : {},
    render: { iconKey: type, shape: "event", tone: type === "startEvent" ? "success" : type === "errorEvent" ? "danger" : "warning", width: 132, height: 70 },
    propertyForm: { formKey: type, sections: ["General"] }
  } as MicroflowNode;
}

const nodes: MicroflowNode[] = [
  eventNode("start", "startEvent", "Start", 40, 220),
  {
    id: "param-order-id",
    type: "parameter",
    kind: "parameter",
    title: "Parameter: orderId",
    titleZh: "参数：orderId",
    category: "parameters",
    position: { x: 40, y: 80 },
    ports: [annotationOut],
    config: { parameter: { id: "param-order-id", name: "orderId", required: true, type: stringType } },
    render: { iconKey: "parameter", shape: "roundedRect", tone: "info", width: 172, height: 70 },
    propertyForm: { formKey: "parameter", sections: ["Parameter"] }
  },
  {
    id: "param-member",
    type: "parameter",
    kind: "parameter",
    title: "Parameter: member",
    titleZh: "参数：member",
    category: "parameters",
    position: { x: 40, y: 420 },
    ports: [annotationOut],
    config: { parameter: { id: "param-member", name: "member", required: false, type: memberType } },
    render: { iconKey: "parameter", shape: "roundedRect", tone: "info", width: 172, height: 70 },
    propertyForm: { formKey: "parameter", sections: ["Parameter"] }
  },
  activity("retrieve-order", "Retrieve Order", "objectRetrieve", 260, 215, { entity: "Sales.Order", resultVariableName: "order", range: "first" }),
  {
    id: "decision-processable",
    type: "decision",
    kind: "decision",
    title: "Order status processable?",
    titleZh: "订单状态是否可处理",
    category: "decisions",
    position: { x: 520, y: 205 },
    ports: [sequenceIn, decisionTrue, decisionFalse, errorOut],
    config: { resultType: "Boolean", expression: { id: "expr-processable", language: "mendix", text: "$order/Status = 'Pending'", expectedType: booleanType, referencedVariables: ["order"] } },
    render: { iconKey: "decision", shape: "diamond", tone: "warning", width: 160, height: 110 },
    propertyForm: { formKey: "decision", sections: ["Expression", "Branches"] }
  },
  activity("change-order", "Update Order", "objectChange", 800, 120, { objectVariableName: "order" }),
  activity("commit-order", "Commit Order", "objectCommit", 1040, 120, { objectVariableName: "order", withEvents: true, refreshClient: true }),
  activity("call-inventory-rest", "Check Inventory", "callRest", 1280, 120, { method: "POST", url: "/api/inventory/check", resultVariableName: "inventoryResult", errorHandling: { mode: "customWithRollback", errorVariableName: "latestError" } }),
  {
    id: "decision-stock",
    type: "decision",
    kind: "decision",
    title: "Inventory enough?",
    titleZh: "库存是否充足",
    category: "decisions",
    position: { x: 1530, y: 106 },
    ports: [sequenceIn, decisionTrue, decisionFalse, errorOut],
    config: { resultType: "Boolean", expression: { id: "expr-stock", language: "mendix", text: "$inventoryResult/available = true", expectedType: booleanType, referencedVariables: ["inventoryResult"] } },
    render: { iconKey: "decision", shape: "diamond", tone: "warning", width: 160, height: 110 },
    propertyForm: { formKey: "decision", sections: ["Expression", "Branches"] }
  },
  activity("update-payment", "Update Payment Status", "objectChange", 1800, 70, { objectVariableName: "order" }),
  activity("log-stock-shortage", "Inventory Not Enough", "logMessage", 1800, 270, { logLevel: "warn", messageExpression: { id: "expr-shortage", language: "plainText", text: "Inventory not enough", referencedVariables: ["orderId"] } }, false),
  activity("log-not-processable", "Order Not Processable", "logMessage", 800, 360, { logLevel: "warn", messageExpression: { id: "expr-not-processable", language: "plainText", text: "Order is not processable", referencedVariables: ["orderId"] } }, false),
  { id: "merge-success", type: "merge", kind: "merge", title: "Merge", titleZh: "合并", category: "decisions", position: { x: 2060, y: 74 }, ports: [sequenceIn, sequenceOut], config: { strategy: "firstAvailable" }, render: { iconKey: "merge", shape: "diamond", tone: "info", width: 112, height: 84 }, propertyForm: { formKey: "merge", sections: ["General"] } },
  eventNode("end-success", "endEvent", "End: return true", 2260, 80),
  eventNode("end-shortage", "endEvent", "End: return false", 2060, 280),
  eventNode("end-rejected", "endEvent", "End: return false", 1040, 365),
  activity("log-rest-error", "Log REST Error", "logMessage", 1280, 510, { logLevel: "error", messageExpression: { id: "expr-rest-error", language: "plainText", text: "$latestError/message", referencedVariables: ["latestError"] } }, false),
  eventNode("error-rest", "errorEvent", "Error Event", 1530, 515),
  activity("retrieve-order-lines", "Retrieve OrderLines", "objectRetrieve", 520, 650, { entity: "Sales.OrderLine", listVariableName: "orderLines" }),
  { id: "loop-order-lines", type: "loop", kind: "loop", title: "For Each OrderLine", titleZh: "遍历订单行", category: "loop", position: { x: 780, y: 640 }, ports: [sequenceIn, sequenceOut, errorOut], config: { loopType: "forEach", iterableVariableName: "orderLines", itemVariableName: "orderLine", indexVariableName: "currentIndex" }, render: { iconKey: "loop", shape: "loop", tone: "info", width: 200, height: 82 }, propertyForm: { formKey: "loop", sections: ["Collection", "Item"] } },
  { id: "decision-line-valid", type: "decision", kind: "decision", title: "Current line valid?", titleZh: "当前行是否有效", category: "decisions", parentLoopId: "loop-order-lines", position: { x: 1040, y: 640 }, ports: [sequenceIn, decisionTrue, decisionFalse], config: { resultType: "Boolean", expression: { id: "expr-line-valid", language: "mendix", text: "$orderLine/Quantity > 0", expectedType: booleanType, referencedVariables: ["orderLine"] } }, render: { iconKey: "decision", shape: "diamond", tone: "warning", width: 160, height: 110 }, propertyForm: { formKey: "decision", sections: ["Expression"] } },
  activity("change-order-line", "Change OrderLine", "objectChange", 1280, 620, { objectVariableName: "orderLine" }),
  { id: "decision-line-severe", type: "decision", kind: "decision", title: "Severe error found?", titleZh: "是否发现严重错误", category: "decisions", parentLoopId: "loop-order-lines", position: { x: 1530, y: 640 }, ports: [sequenceIn, decisionTrue, decisionFalse], config: { resultType: "Boolean", expression: { id: "expr-line-severe", language: "mendix", text: "$orderLine/HasSevereError = true", expectedType: booleanType, referencedVariables: ["orderLine"] } }, render: { iconKey: "decision", shape: "diamond", tone: "warning", width: 170, height: 110 }, propertyForm: { formKey: "decision", sections: ["Expression"] } },
  eventNode("continue-line", "continueEvent", "Continue Event", 1280, 780, "loop-order-lines"),
  eventNode("break-line", "breakEvent", "Break Event", 1800, 640, "loop-order-lines"),
  {
    id: "member-type",
    type: "objectTypeDecision",
    kind: "objectTypeDecision",
    title: "Member Type",
    titleZh: "成员类型",
    category: "decisions",
    position: { x: 260, y: 420 },
    ports: [sequenceIn, objectTypeOut, errorOut],
    config: { inputObject: "member", generalizedEntity: "University.Member" },
    render: { iconKey: "objectTypeDecision", shape: "diamond", tone: "warning", width: 170, height: 112 },
    propertyForm: { formKey: "objectTypeDecision", sections: ["General"] }
  },
  activity("cast-professor", "Cast Professor", "objectCast", 520, 390, { objectVariableName: "member", targetEntity: "University.Professor", resultVariableName: "professor" }),
  activity("cast-student", "Cast Student", "objectCast", 520, 500, { objectVariableName: "member", targetEntity: "University.Student", resultVariableName: "student" }),
  activity("log-member-empty", "Log Empty Member", "logMessage", 520, 610, { logLevel: "warn", messageExpression: { id: "expr-member-empty", language: "plainText", text: "member is empty", referencedVariables: ["member"] } }, false),
  { id: "annotation-error", type: "annotation", kind: "annotation", title: "Error Strategy", titleZh: "异常处理说明", category: "annotations", position: { x: 1040, y: 500 }, ports: [annotationOut], config: { title: "异常处理说明", text: "Call REST Service 的 errorOut 进入 Log Message，再进入 Error Event。Annotation Flow 可连接节点和错误处理边。" }, render: { iconKey: "annotation", shape: "annotation", tone: "neutral", width: 300, height: 118 }, propertyForm: { formKey: "annotation", sections: ["Text"] } }
];

const edges: MicroflowEdge[] = [
  { id: "e-start-retrieve", type: "sequence", sourceNodeId: "start", sourcePortId: "out", targetNodeId: "retrieve-order", targetPortId: "in" },
  { id: "e-retrieve-decision", type: "sequence", sourceNodeId: "retrieve-order", sourcePortId: "out", targetNodeId: "decision-processable", targetPortId: "in" },
  { id: "e-processable-yes", type: "decisionCondition", sourceNodeId: "decision-processable", sourcePortId: "true", targetNodeId: "change-order", targetPortId: "in", label: "是", conditionValue: { kind: "boolean", value: true }, branchOrder: 1 },
  { id: "e-processable-no", type: "decisionCondition", sourceNodeId: "decision-processable", sourcePortId: "false", targetNodeId: "log-not-processable", targetPortId: "in", label: "否", conditionValue: { kind: "boolean", value: false }, branchOrder: 2 },
  { id: "e-log-rejected-end", type: "sequence", sourceNodeId: "log-not-processable", sourcePortId: "out", targetNodeId: "end-rejected", targetPortId: "in" },
  { id: "e-change-commit", type: "sequence", sourceNodeId: "change-order", sourcePortId: "out", targetNodeId: "commit-order", targetPortId: "in" },
  { id: "e-commit-rest", type: "sequence", sourceNodeId: "commit-order", sourcePortId: "out", targetNodeId: "call-inventory-rest", targetPortId: "in" },
  { id: "e-rest-stock", type: "sequence", sourceNodeId: "call-inventory-rest", sourcePortId: "out", targetNodeId: "decision-stock", targetPortId: "in" },
  { id: "e-stock-enough", type: "decisionCondition", sourceNodeId: "decision-stock", sourcePortId: "true", targetNodeId: "update-payment", targetPortId: "in", label: "是", conditionValue: { kind: "boolean", value: true }, branchOrder: 1 },
  { id: "e-stock-shortage", type: "decisionCondition", sourceNodeId: "decision-stock", sourcePortId: "false", targetNodeId: "log-stock-shortage", targetPortId: "in", label: "否", conditionValue: { kind: "boolean", value: false }, branchOrder: 2 },
  { id: "e-shortage-end", type: "sequence", sourceNodeId: "log-stock-shortage", sourcePortId: "out", targetNodeId: "end-shortage", targetPortId: "in" },
  { id: "e-payment-merge", type: "sequence", sourceNodeId: "update-payment", sourcePortId: "out", targetNodeId: "merge-success", targetPortId: "in" },
  { id: "e-merge-end", type: "sequence", sourceNodeId: "merge-success", sourcePortId: "out", targetNodeId: "end-success", targetPortId: "in" },
  { id: "e-rest-error", type: "errorHandler", sourceNodeId: "call-inventory-rest", sourcePortId: "error", targetNodeId: "log-rest-error", targetPortId: "in", label: "Error", errorHandlingType: "customWithRollback", exposeLatestError: true, exposeLatestHttpResponse: true, logError: true },
  { id: "e-error-log-error-event", type: "sequence", sourceNodeId: "log-rest-error", sourcePortId: "out", targetNodeId: "error-rest", targetPortId: "in", label: "rethrow" },
  { id: "e-retrieve-lines", type: "sequence", sourceNodeId: "retrieve-order-lines", sourcePortId: "out", targetNodeId: "loop-order-lines", targetPortId: "in" },
  { id: "e-loop-body", type: "sequence", sourceNodeId: "loop-order-lines", sourcePortId: "out", targetNodeId: "decision-line-valid", targetPortId: "in", label: "body" },
  { id: "e-line-valid", type: "decisionCondition", sourceNodeId: "decision-line-valid", sourcePortId: "true", targetNodeId: "change-order-line", targetPortId: "in", conditionValue: { kind: "boolean", value: true }, label: "true" },
  { id: "e-line-invalid", type: "decisionCondition", sourceNodeId: "decision-line-valid", sourcePortId: "false", targetNodeId: "continue-line", targetPortId: "in", conditionValue: { kind: "boolean", value: false }, label: "false" },
  { id: "e-line-change-severe", type: "sequence", sourceNodeId: "change-order-line", sourcePortId: "out", targetNodeId: "decision-line-severe", targetPortId: "in" },
  { id: "e-severe-break", type: "decisionCondition", sourceNodeId: "decision-line-severe", sourcePortId: "true", targetNodeId: "break-line", targetPortId: "in", conditionValue: { kind: "boolean", value: true }, label: "true" },
  { id: "e-severe-continue", type: "decisionCondition", sourceNodeId: "decision-line-severe", sourcePortId: "false", targetNodeId: "continue-line", targetPortId: "in", conditionValue: { kind: "boolean", value: false }, label: "false" },
  { id: "e-member-professor", type: "objectTypeCondition", sourceNodeId: "member-type", sourcePortId: "objectType", targetNodeId: "cast-professor", targetPortId: "in", conditionValue: { kind: "objectType", entity: "University.Professor" }, label: "Professor", branchOrder: 1 },
  { id: "e-member-student", type: "objectTypeCondition", sourceNodeId: "member-type", sourcePortId: "objectType", targetNodeId: "cast-student", targetPortId: "in", conditionValue: { kind: "objectType", entity: "University.Student" }, label: "Student", branchOrder: 2 },
  { id: "e-member-empty", type: "objectTypeCondition", sourceNodeId: "member-type", sourcePortId: "objectType", targetNodeId: "log-member-empty", targetPortId: "in", conditionValue: { kind: "objectType", entity: "empty" }, label: "empty", branchOrder: 3 },
  { id: "e-note-rest", type: "annotation", sourceNodeId: "annotation-error", sourcePortId: "note", targetNodeId: "call-inventory-rest", targetPortId: "in", label: "error strategy", attachmentMode: "node", showInExport: true },
  { id: "e-note-error-flow", type: "annotation", sourceNodeId: "annotation-error", sourcePortId: "note", targetNodeId: "log-rest-error", targetPortId: "in", label: "handler", attachmentMode: "edge", showInExport: true }
];

const parameters = [
  { id: "param-order-id", stableId: "param-order-id", name: "orderId", required: true, type: stringType, dataType: { kind: "string" as const } },
  { id: "param-member", stableId: "param-member", name: "member", required: false, type: memberType, dataType: { kind: "object" as const, entityQualifiedName: "University.Member" } }
];

const variables = [
  { id: "var-orders", name: "orders", scope: "microflow" as const, type: orderListType },
  { id: "var-order", name: "order", scope: "microflow" as const, type: orderType },
  { id: "var-order-lines", name: "orderLines", scope: "microflow" as const, type: orderLineListType },
  { id: "var-order-line", name: "orderLine", scope: "node" as const, type: orderLineType },
  { id: "var-inventory", name: "inventoryResult", scope: "microflow" as const, type: { kind: "object" as const, name: "InventoryReservationResult" } },
  { id: "var-latest-error", name: "latestError", scope: "latestError" as const, type: { kind: "object" as const, name: "MicroflowError" } }
];

const viewport = { zoom: 0.55, offset: { x: 24, y: 78 } };
const authoringFields = buildAuthoringFieldsFromLegacy({
  id: "mf-order-process",
  name: "Order Processing Microflow",
  version: "v4",
  description: "Mendix-style microflow sample covering objectCollection, SequenceFlow, AnnotationFlow, ActionActivity, decisions, error handler, annotation and nested loop objects.",
  parameters,
  nodes,
  edges,
  viewport
});

export const sampleMicroflowSchema: MicroflowSchema = {
  ...authoringFields,
  id: "mf-order-process",
  name: "Order Processing Microflow",
  version: "v4",
  description: "Mendix-style microflow sample covering objectCollection, SequenceFlow, AnnotationFlow, ActionActivity, decisions, error handler, annotation and nested loop objects.",
  parameters,
  variables,
  nodes,
  edges,
  viewport
};
