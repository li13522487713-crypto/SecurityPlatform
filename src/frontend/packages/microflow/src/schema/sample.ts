import type { MicroflowEdge, MicroflowNode, MicroflowSchema, MicroflowTypeRef } from "./types";

const stringType: MicroflowTypeRef = { kind: "primitive", name: "String" };
const orderType: MicroflowTypeRef = { kind: "entity", name: "Sales.Order", entity: "Sales.Order" };
const orderListType: MicroflowTypeRef = { kind: "list", name: "List<Sales.Order>", itemType: orderType };

const nodes: MicroflowNode[] = [
  {
    id: "param-order-id",
    type: "parameter",
    title: "OrderId",
    category: "parameter",
    position: { x: 40, y: 40 },
    ports: [{ id: "out", label: "Out", direction: "output", edgeTypes: ["sequence"] }],
    config: { parameter: { id: "param-order-id", name: "orderId", required: true, type: stringType } },
    render: { iconKey: "parameter", shape: "roundedRect", tone: "info", width: 150, height: 70 },
    propertyForm: { formKey: "parameter", sections: ["Parameter"] }
  },
  {
    id: "start",
    type: "startEvent",
    title: "Start",
    category: "event",
    position: { x: 40, y: 180 },
    ports: [{ id: "out", label: "Out", direction: "output", edgeTypes: ["sequence"] }],
    config: {},
    render: { iconKey: "startEvent", shape: "event", tone: "success", width: 116, height: 70 },
    propertyForm: { formKey: "event", sections: ["General"] }
  },
  {
    id: "retrieve-order",
    type: "activity",
    title: "Retrieve Order",
    category: "activity",
    position: { x: 230, y: 170 },
    ports: [
      { id: "in", label: "In", direction: "input", edgeTypes: ["sequence", "error"] },
      { id: "out", label: "Out", direction: "output", edgeTypes: ["sequence"] },
      { id: "error", label: "Error", direction: "output", edgeTypes: ["error"] }
    ],
    config: {
      activityType: "objectRetrieve",
      entity: "Sales.Order",
      listVariableName: "orders",
      supportsErrorFlow: true,
      errorHandling: { mode: "customWithRollback", errorVariableName: "latestError", targetNodeId: "log-error" }
    },
    render: { iconKey: "objectRetrieve", shape: "roundedRect", tone: "neutral", width: 172, height: 76 },
    propertyForm: { formKey: "objectActivity", sections: ["General", "Input", "Error Handling"] }
  },
  {
    id: "decision-found",
    type: "decision",
    title: "Found?",
    category: "decision",
    position: { x: 460, y: 160 },
    ports: [
      { id: "in", label: "In", direction: "input", edgeTypes: ["sequence"] },
      { id: "true", label: "True", direction: "output", edgeTypes: ["sequence"] },
      { id: "false", label: "False", direction: "output", edgeTypes: ["sequence"] }
    ],
    config: { expression: { id: "expr-found", language: "mendix", text: "$orders != empty", expectedType: { kind: "primitive", name: "Boolean" }, referencedVariables: ["orders"] } },
    render: { iconKey: "decision", shape: "diamond", tone: "warning", width: 132, height: 96 },
    propertyForm: { formKey: "decision", sections: ["Expression", "Outcomes"] }
  },
  {
    id: "change-order",
    type: "activity",
    title: "Change Order",
    category: "activity",
    position: { x: 690, y: 80 },
    ports: [
      { id: "in", label: "In", direction: "input", edgeTypes: ["sequence", "error"] },
      { id: "out", label: "Out", direction: "output", edgeTypes: ["sequence"] },
      { id: "error", label: "Error", direction: "output", edgeTypes: ["error"] }
    ],
    config: { activityType: "objectChange", objectVariableName: "order", supportsErrorFlow: true, errorHandling: { mode: "rollback", errorVariableName: "latestError" } },
    render: { iconKey: "objectChange", shape: "roundedRect", tone: "neutral", width: 172, height: 76 },
    propertyForm: { formKey: "objectActivity", sections: ["General", "Input", "Error Handling"] }
  },
  {
    id: "commit-order",
    type: "activity",
    title: "Commit Order",
    category: "activity",
    position: { x: 920, y: 80 },
    ports: [
      { id: "in", label: "In", direction: "input", edgeTypes: ["sequence", "error"] },
      { id: "out", label: "Out", direction: "output", edgeTypes: ["sequence"] },
      { id: "error", label: "Error", direction: "output", edgeTypes: ["error"] }
    ],
    config: { activityType: "objectCommit", objectVariableName: "order", supportsErrorFlow: true, errorHandling: { mode: "rollback", errorVariableName: "latestError" } },
    render: { iconKey: "objectCommit", shape: "roundedRect", tone: "neutral", width: 172, height: 76 },
    propertyForm: { formKey: "objectActivity", sections: ["General", "Input", "Error Handling"] }
  },
  {
    id: "call-rest",
    type: "activity",
    title: "Call REST",
    category: "activity",
    position: { x: 690, y: 260 },
    ports: [
      { id: "in", label: "In", direction: "input", edgeTypes: ["sequence", "error"] },
      { id: "out", label: "Out", direction: "output", edgeTypes: ["sequence"] },
      { id: "error", label: "Error", direction: "output", edgeTypes: ["error"] }
    ],
    config: { activityType: "callRest", method: "POST", url: "/api/orders/sync", supportsErrorFlow: true, errorHandling: { mode: "continue", errorVariableName: "latestError" } },
    render: { iconKey: "callRest", shape: "roundedRect", tone: "neutral", width: 172, height: 76 },
    propertyForm: { formKey: "integrationActivity", sections: ["Request", "Error Handling"] }
  },
  {
    id: "merge",
    type: "merge",
    title: "Merge",
    category: "merge",
    position: { x: 1160, y: 170 },
    ports: [
      { id: "in", label: "In", direction: "input", edgeTypes: ["sequence"] },
      { id: "out", label: "Out", direction: "output", edgeTypes: ["sequence"] }
    ],
    config: { strategy: "firstAvailable" },
    render: { iconKey: "merge", shape: "diamond", tone: "info", width: 112, height: 84 },
    propertyForm: { formKey: "merge", sections: ["General"] }
  },
  {
    id: "log-success",
    type: "activity",
    title: "Log Message",
    category: "activity",
    position: { x: 1360, y: 170 },
    ports: [
      { id: "in", label: "In", direction: "input", edgeTypes: ["sequence"] },
      { id: "out", label: "Out", direction: "output", edgeTypes: ["sequence"] }
    ],
    config: { activityType: "logMessage", messageExpression: { id: "expr-log", language: "plainText", text: "Order microflow completed", referencedVariables: ["orderId"] } },
    render: { iconKey: "logMessage", shape: "roundedRect", tone: "neutral", width: 172, height: 76 },
    propertyForm: { formKey: "loggingActivity", sections: ["Message"] }
  },
  {
    id: "end",
    type: "endEvent",
    title: "End",
    category: "event",
    position: { x: 1600, y: 180 },
    ports: [{ id: "in", label: "In", direction: "input", edgeTypes: ["sequence"] }],
    config: { returnValue: { id: "expr-return", language: "mendix", text: "true", expectedType: { kind: "primitive", name: "Boolean" } } },
    render: { iconKey: "endEvent", shape: "event", tone: "danger", width: 116, height: 70 },
    propertyForm: { formKey: "event", sections: ["Return"] }
  },
  {
    id: "log-error",
    type: "activity",
    title: "Log Error",
    category: "activity",
    position: { x: 460, y: 360 },
    ports: [
      { id: "in", label: "In", direction: "input", edgeTypes: ["error", "sequence"] },
      { id: "out", label: "Out", direction: "output", edgeTypes: ["sequence"] }
    ],
    config: { activityType: "logMessage", messageExpression: { id: "expr-error", language: "plainText", text: "Retrieve failed: $latestError/message", referencedVariables: ["latestError"] } },
    render: { iconKey: "logMessage", shape: "roundedRect", tone: "danger", width: 172, height: 76 },
    propertyForm: { formKey: "loggingActivity", sections: ["Message"] }
  },
  {
    id: "note",
    type: "annotation",
    title: "Annotation",
    category: "annotation",
    position: { x: 915, y: 350 },
    ports: [{ id: "note", label: "Note", direction: "output", edgeTypes: ["annotation"] }],
    config: { text: "This sample models a basic order synchronization microflow with validation, persistence, integration call, merge, and trace output." },
    render: { iconKey: "annotation", shape: "annotation", tone: "neutral", width: 260, height: 118 },
    propertyForm: { formKey: "annotation", sections: ["Text"] }
  }
];

const edges: MicroflowEdge[] = [
  { id: "e-start-retrieve", type: "sequence", sourceNodeId: "start", targetNodeId: "retrieve-order" },
  { id: "e-retrieve-decision", type: "sequence", sourceNodeId: "retrieve-order", targetNodeId: "decision-found" },
  { id: "e-retrieve-error", type: "error", sourceNodeId: "retrieve-order", sourcePortId: "error", targetNodeId: "log-error", label: "error" },
  { id: "e-found-change", type: "sequence", sourceNodeId: "decision-found", sourcePortId: "true", targetNodeId: "change-order", label: "found" },
  { id: "e-notfound-rest", type: "sequence", sourceNodeId: "decision-found", sourcePortId: "false", targetNodeId: "call-rest", label: "not found" },
  { id: "e-change-commit", type: "sequence", sourceNodeId: "change-order", targetNodeId: "commit-order" },
  { id: "e-commit-merge", type: "sequence", sourceNodeId: "commit-order", targetNodeId: "merge" },
  { id: "e-rest-merge", type: "sequence", sourceNodeId: "call-rest", targetNodeId: "merge" },
  { id: "e-error-merge", type: "sequence", sourceNodeId: "log-error", targetNodeId: "merge" },
  { id: "e-merge-log", type: "sequence", sourceNodeId: "merge", targetNodeId: "log-success" },
  { id: "e-log-end", type: "sequence", sourceNodeId: "log-success", targetNodeId: "end" },
  { id: "e-note-commit", type: "annotation", sourceNodeId: "note", targetNodeId: "commit-order", label: "persistence branch" }
];

export const sampleMicroflowSchema: MicroflowSchema = {
  id: "mf-order-sync-sample",
  name: "Order Sync Microflow",
  version: "0.1.0",
  description: "Sample Mendix-style microflow schema rendered by the Atlas Microflow editor.",
  parameters: [{ id: "param-order-id", name: "orderId", required: true, type: stringType }],
  variables: [
    { id: "var-orders", name: "orders", scope: "microflow", type: orderListType },
    { id: "var-order", name: "order", scope: "microflow", type: orderType },
    { id: "var-latest-error", name: "latestError", scope: "latestError", type: { kind: "object", name: "MicroflowError" } }
  ],
  nodes,
  edges,
  viewport: { zoom: 0.78, offset: { x: 24, y: 80 } }
};
