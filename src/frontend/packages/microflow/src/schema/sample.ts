import type { MicroflowEdge, MicroflowNode, MicroflowPort, MicroflowSchema, MicroflowTypeRef } from "./types";

const stringType: MicroflowTypeRef = { kind: "primitive", name: "String" };
const booleanType: MicroflowTypeRef = { kind: "primitive", name: "Boolean" };
const orderType: MicroflowTypeRef = { kind: "entity", name: "Sales.Order", entity: "Sales.Order" };
const orderListType: MicroflowTypeRef = { kind: "list", name: "List<Sales.Order>", itemType: orderType };

const sequenceIn: MicroflowPort = { id: "in", label: "In", direction: "input", edgeTypes: ["sequence", "error"] };
const sequenceOut: MicroflowPort = { id: "out", label: "Out", direction: "output", edgeTypes: ["sequence"] };
const errorOut: MicroflowPort = { id: "error", label: "Error", direction: "output", edgeTypes: ["error"] };

const nodes: MicroflowNode[] = [
  {
    id: "start",
    type: "startEvent",
    title: "Start",
    category: "event",
    position: { x: 40, y: 220 },
    ports: [sequenceOut],
    config: {},
    render: { iconKey: "startEvent", shape: "event", tone: "success", width: 116, height: 70 },
    propertyForm: { formKey: "event", sections: ["General"] }
  },
  {
    id: "param-order-id",
    type: "parameter",
    title: "Parameter: orderId",
    category: "parameter",
    position: { x: 220, y: 220 },
    ports: [sequenceIn, sequenceOut],
    config: { parameter: { id: "param-order-id", name: "orderId", required: true, type: stringType } },
    render: { iconKey: "parameter", shape: "roundedRect", tone: "info", width: 172, height: 70 },
    propertyForm: { formKey: "parameter", sections: ["Parameter"] }
  },
  {
    id: "retrieve-order",
    type: "activity",
    title: "Retrieve Order",
    category: "activity",
    position: { x: 460, y: 215 },
    ports: [sequenceIn, sequenceOut, errorOut],
    config: {
      activityType: "objectRetrieve",
      entity: "Sales.Order",
      listVariableName: "orders",
      range: "first",
      supportsErrorFlow: true,
      errorHandling: { mode: "customWithRollback", errorVariableName: "latestError", targetNodeId: "log-error" }
    },
    render: { iconKey: "objectRetrieve", shape: "roundedRect", tone: "neutral", width: 180, height: 76 },
    propertyForm: { formKey: "objectActivity", sections: ["Retrieve", "Range", "Error Handling"] }
  },
  {
    id: "decision-processable",
    type: "decision",
    title: "Order processable?",
    category: "decision",
    position: { x: 720, y: 205 },
    ports: [
      sequenceIn,
      { id: "true", label: "Yes", direction: "output", edgeTypes: ["sequence"] },
      { id: "false", label: "No", direction: "output", edgeTypes: ["sequence"] }
    ],
    config: {
      expression: {
        id: "expr-processable",
        language: "mendix",
        text: "$orders != empty and $order/Status = 'Pending'",
        expectedType: booleanType,
        referencedVariables: ["orders", "order"]
      }
    },
    render: { iconKey: "decision", shape: "diamond", tone: "warning", width: 152, height: 104 },
    propertyForm: { formKey: "decision", sections: ["Expression", "Branches"] }
  },
  {
    id: "log-not-processable",
    type: "activity",
    title: "Log Not Processable",
    category: "activity",
    position: { x: 980, y: 360 },
    ports: [sequenceIn, sequenceOut],
    config: {
      activityType: "logMessage",
      logLevel: "warn",
      messageExpression: { id: "expr-not-processable", language: "plainText", text: "Order is not processable", referencedVariables: ["orderId"] }
    },
    render: { iconKey: "logMessage", shape: "roundedRect", tone: "warning", width: 190, height: 76 },
    propertyForm: { formKey: "loggingActivity", sections: ["Message"] }
  },
  {
    id: "end-not-processable",
    type: "endEvent",
    title: "End: Rejected",
    category: "event",
    position: { x: 1240, y: 365 },
    ports: [sequenceIn],
    config: { returnValue: { id: "expr-rejected", language: "mendix", text: "false", expectedType: booleanType } },
    render: { iconKey: "endEvent", shape: "event", tone: "danger", width: 132, height: 70 },
    propertyForm: { formKey: "event", sections: ["Return"] }
  },
  {
    id: "change-order-status",
    type: "activity",
    title: "Change Order Status",
    category: "activity",
    position: { x: 980, y: 120 },
    ports: [sequenceIn, sequenceOut, errorOut],
    config: {
      activityType: "objectChange",
      objectVariableName: "order",
      valueExpression: { id: "expr-status", language: "mendix", text: "'Processing'", referencedVariables: ["order"] },
      supportsErrorFlow: true,
      errorHandling: { mode: "rollback", errorVariableName: "latestError" }
    },
    render: { iconKey: "objectChange", shape: "roundedRect", tone: "neutral", width: 190, height: 76 },
    propertyForm: { formKey: "objectActivity", sections: ["Assignments", "Error Handling"] }
  },
  {
    id: "commit-order",
    type: "activity",
    title: "Commit Order",
    category: "activity",
    position: { x: 1240, y: 120 },
    ports: [sequenceIn, sequenceOut, errorOut],
    config: {
      activityType: "objectCommit",
      objectVariableName: "order",
      withEvents: true,
      refreshClient: true,
      supportsErrorFlow: true,
      errorHandling: { mode: "rollback", errorVariableName: "latestError" }
    },
    render: { iconKey: "objectCommit", shape: "roundedRect", tone: "neutral", width: 180, height: 76 },
    propertyForm: { formKey: "objectActivity", sections: ["Commit", "Error Handling"] }
  },
  {
    id: "call-inventory-rest",
    type: "activity",
    title: "Call Inventory REST",
    category: "activity",
    position: { x: 1480, y: 120 },
    ports: [sequenceIn, sequenceOut, errorOut],
    config: {
      activityType: "callRest",
      method: "POST",
      url: "/api/inventory/reservations",
      timeoutMs: 5000,
      resultVariableName: "inventoryResult",
      supportsErrorFlow: true,
      errorHandling: { mode: "continue", errorVariableName: "latestError" }
    },
    render: { iconKey: "callRest", shape: "roundedRect", tone: "neutral", width: 190, height: 76 },
    propertyForm: { formKey: "integrationActivity", sections: ["Request", "Response", "Error Handling"] }
  },
  {
    id: "decision-stock",
    type: "decision",
    title: "Inventory enough?",
    category: "decision",
    position: { x: 1730, y: 106 },
    ports: [
      sequenceIn,
      { id: "true", label: "Enough", direction: "output", edgeTypes: ["sequence"] },
      { id: "false", label: "Shortage", direction: "output", edgeTypes: ["sequence"] }
    ],
    config: {
      expression: {
        id: "expr-stock",
        language: "mendix",
        text: "$inventoryResult/available = true",
        expectedType: booleanType,
        referencedVariables: ["inventoryResult"]
      }
    },
    render: { iconKey: "decision", shape: "diamond", tone: "warning", width: 152, height: 104 },
    propertyForm: { formKey: "decision", sections: ["Expression", "Branches"] }
  },
  {
    id: "log-stock-shortage",
    type: "activity",
    title: "Log Stock Shortage",
    category: "activity",
    position: { x: 1990, y: 270 },
    ports: [sequenceIn, sequenceOut],
    config: {
      activityType: "logMessage",
      logLevel: "warn",
      messageExpression: { id: "expr-shortage", language: "plainText", text: "Inventory is not enough", referencedVariables: ["orderId", "inventoryResult"] }
    },
    render: { iconKey: "logMessage", shape: "roundedRect", tone: "warning", width: 190, height: 76 },
    propertyForm: { formKey: "loggingActivity", sections: ["Message"] }
  },
  {
    id: "end-stock-shortage",
    type: "endEvent",
    title: "End: Shortage",
    category: "event",
    position: { x: 2260, y: 275 },
    ports: [sequenceIn],
    config: { returnValue: { id: "expr-shortage-return", language: "mendix", text: "false", expectedType: booleanType } },
    render: { iconKey: "endEvent", shape: "event", tone: "danger", width: 132, height: 70 },
    propertyForm: { formKey: "event", sections: ["Return"] }
  },
  {
    id: "change-payment-status",
    type: "activity",
    title: "Change Payment Status",
    category: "activity",
    position: { x: 1990, y: 60 },
    ports: [sequenceIn, sequenceOut, errorOut],
    config: {
      activityType: "objectChange",
      objectVariableName: "order",
      valueExpression: { id: "expr-payment", language: "mendix", text: "'Reserved'", referencedVariables: ["order"] },
      supportsErrorFlow: true,
      errorHandling: { mode: "rollback", errorVariableName: "latestError" }
    },
    render: { iconKey: "objectChange", shape: "roundedRect", tone: "neutral", width: 200, height: 76 },
    propertyForm: { formKey: "objectActivity", sections: ["Assignments", "Error Handling"] }
  },
  {
    id: "merge-success",
    type: "merge",
    title: "Merge",
    category: "merge",
    position: { x: 2260, y: 74 },
    ports: [sequenceIn, sequenceOut],
    config: { strategy: "firstAvailable" },
    render: { iconKey: "merge", shape: "diamond", tone: "info", width: 112, height: 84 },
    propertyForm: { formKey: "merge", sections: ["General"] }
  },
  {
    id: "end-success",
    type: "endEvent",
    title: "End: Success",
    category: "event",
    position: { x: 2460, y: 80 },
    ports: [sequenceIn],
    config: { returnValue: { id: "expr-success", language: "mendix", text: "true", expectedType: booleanType } },
    render: { iconKey: "endEvent", shape: "event", tone: "danger", width: 132, height: 70 },
    propertyForm: { formKey: "event", sections: ["Return"] }
  },
  {
    id: "log-error",
    type: "activity",
    title: "Log Error",
    category: "activity",
    position: { x: 720, y: 520 },
    ports: [sequenceIn, sequenceOut],
    config: {
      activityType: "logMessage",
      logLevel: "error",
      messageExpression: { id: "expr-error", language: "plainText", text: "Exception: $latestError/message", referencedVariables: ["latestError"] }
    },
    render: { iconKey: "logMessage", shape: "roundedRect", tone: "danger", width: 180, height: 76 },
    propertyForm: { formKey: "loggingActivity", sections: ["Message"] }
  },
  {
    id: "annotation-error",
    type: "annotation",
    title: "Annotation",
    category: "annotation",
    position: { x: 1240, y: 510 },
    ports: [{ id: "note", label: "Note", direction: "output", edgeTypes: ["annotation"] }],
    config: { text: "Error handling uses latestError. Retrieve/commit/integration failures can rollback, continue, or jump to custom logging branches." },
    render: { iconKey: "annotation", shape: "annotation", tone: "neutral", width: 320, height: 118 },
    propertyForm: { formKey: "annotation", sections: ["Text"] }
  }
];

const edges: MicroflowEdge[] = [
  { id: "e-start-param", type: "sequence", sourceNodeId: "start", targetNodeId: "param-order-id" },
  { id: "e-param-retrieve", type: "sequence", sourceNodeId: "param-order-id", targetNodeId: "retrieve-order" },
  { id: "e-retrieve-decision", type: "sequence", sourceNodeId: "retrieve-order", targetNodeId: "decision-processable" },
  { id: "e-retrieve-error", type: "error", sourceNodeId: "retrieve-order", sourcePortId: "error", targetNodeId: "log-error", label: "error" },
  { id: "e-not-processable", type: "sequence", sourceNodeId: "decision-processable", sourcePortId: "false", targetNodeId: "log-not-processable", label: "no" },
  { id: "e-log-rejected-end", type: "sequence", sourceNodeId: "log-not-processable", targetNodeId: "end-not-processable" },
  { id: "e-processable-change", type: "sequence", sourceNodeId: "decision-processable", sourcePortId: "true", targetNodeId: "change-order-status", label: "yes" },
  { id: "e-change-commit", type: "sequence", sourceNodeId: "change-order-status", targetNodeId: "commit-order" },
  { id: "e-commit-rest", type: "sequence", sourceNodeId: "commit-order", targetNodeId: "call-inventory-rest" },
  { id: "e-rest-stock", type: "sequence", sourceNodeId: "call-inventory-rest", targetNodeId: "decision-stock" },
  { id: "e-stock-shortage", type: "sequence", sourceNodeId: "decision-stock", sourcePortId: "false", targetNodeId: "log-stock-shortage", label: "shortage" },
  { id: "e-shortage-end", type: "sequence", sourceNodeId: "log-stock-shortage", targetNodeId: "end-stock-shortage" },
  { id: "e-stock-enough", type: "sequence", sourceNodeId: "decision-stock", sourcePortId: "true", targetNodeId: "change-payment-status", label: "enough" },
  { id: "e-payment-merge", type: "sequence", sourceNodeId: "change-payment-status", targetNodeId: "merge-success" },
  { id: "e-merge-end", type: "sequence", sourceNodeId: "merge-success", targetNodeId: "end-success" },
  { id: "e-note-rest", type: "annotation", sourceNodeId: "annotation-error", targetNodeId: "call-inventory-rest", label: "error strategy" }
];

export const sampleMicroflowSchema: MicroflowSchema = {
  id: "mf-order-process",
  name: "Order Processing Microflow",
  version: "v3",
  description: "Full order processing chain: retrieve order, validate status, commit changes, call inventory service, and return by outcome.",
  parameters: [{ id: "param-order-id", name: "orderId", required: true, type: stringType }],
  variables: [
    { id: "var-orders", name: "orders", scope: "microflow", type: orderListType },
    { id: "var-order", name: "order", scope: "microflow", type: orderType },
    { id: "var-inventory", name: "inventoryResult", scope: "microflow", type: { kind: "object", name: "InventoryReservationResult" } },
    { id: "var-latest-error", name: "latestError", scope: "latestError", type: { kind: "object", name: "MicroflowError" } }
  ],
  nodes,
  edges,
  viewport: { zoom: 0.58, offset: { x: 24, y: 90 } }
};
