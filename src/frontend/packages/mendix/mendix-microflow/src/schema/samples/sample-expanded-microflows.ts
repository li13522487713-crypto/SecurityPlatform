import { createActionActivityFromActionRegistry, createFlowFromEdgeRegistry } from "../../node-registry";
import type { MicroflowFlow, MicroflowSchema } from "../types";
import { sampleMicroflowSchema } from "../sample";

function cloneBase(id: string, name: string, description: string): MicroflowSchema {
  const schema = JSON.parse(JSON.stringify(sampleMicroflowSchema)) as MicroflowSchema;
  const startAndParameters = schema.objectCollection.objects.filter(object => object.kind === "startEvent" || object.kind === "parameterObject").slice(0, 3);
  const firstEnd = schema.objectCollection.objects.find(object => object.kind === "endEvent");
  return {
    ...schema,
    id,
    stableId: id,
    name,
    displayName: name,
    description,
    objectCollection: {
      ...schema.objectCollection,
      objects: firstEnd ? [...startAndParameters, { ...firstEnd, id: "end", stableId: "end", caption: "End" }] : startAndParameters,
    },
    flows: []
  };
}

function appendLinearActions(
  schema: MicroflowSchema,
  actions: Array<{ key: string; id: string; caption: string; x: number; y: number; patch?: Record<string, unknown> }>
): MicroflowSchema {
  const objects = [...schema.objectCollection.objects];
  const flows: MicroflowFlow[] = [];
  let previousId = "start";
  const endId = objects.find(object => object.kind === "endEvent")?.id ?? "end";
  for (const action of actions) {
    const activity = createActionActivityFromActionRegistry({
      actionRegistryKey: action.key,
      id: action.id,
      position: { x: action.x, y: action.y },
      overrides: {
        caption: action.caption,
        action: {
          ...createActionActivityFromActionRegistry({ actionRegistryKey: action.key, id: `${action.id}-tmp`, position: { x: action.x, y: action.y } }).action,
          ...(action.patch ?? {})
        }
      }
    });
    objects.push(activity);
    flows.push(createFlowFromEdgeRegistry({
      edgeKind: "sequence",
      originObjectId: previousId,
      destinationObjectId: activity.id
    }));
    previousId = activity.id;
  }
  flows.push(createFlowFromEdgeRegistry({
    edgeKind: "sequence",
    originObjectId: previousId,
    destinationObjectId: endId
  }));
  return {
    ...schema,
    objectCollection: { ...schema.objectCollection, objects },
    flows
  };
}

export const sampleApprovalFlowMicroflowSchema = appendLinearActions(
  cloneBase("sample-approval-flow", "SampleApprovalFlow", "Approval sample with workflow actions."),
  [
    { key: "callWorkflow", id: "approval-call-workflow", caption: "Call Workflow", x: 280, y: 180, patch: { targetWorkflowId: "WF_OrderApproval", contextObjectVariableName: "order", outputWorkflowVariableName: "workflowInstance" } },
    { key: "completeUserTask", id: "approval-complete-task", caption: "Complete User Task", x: 520, y: 180, patch: { userTaskVariableName: "approvalTask", outcome: "approved" } }
  ]
);

export const sampleRestErrorHandlingMicroflowSchema = appendLinearActions(
  cloneBase("sample-rest-error-handling", "SampleRestErrorHandling", "REST call with logging and error event sample."),
  [
    { key: "restCall", id: "rest-call", caption: "Call REST", x: 280, y: 180, patch: { response: { handling: { kind: "json", outputVariableName: "restResponse" } } } },
    { key: "logMessage", id: "rest-log", caption: "Log REST", x: 520, y: 180, patch: { template: { text: "REST completed", arguments: [] } } }
  ]
);

export const sampleLoopProcessingMicroflowSchema: MicroflowSchema = {
  ...JSON.parse(JSON.stringify(sampleMicroflowSchema)) as MicroflowSchema,
  id: "sample-loop-processing",
  stableId: "sample-loop-processing",
  name: "SampleLoopProcessing",
  displayName: "SampleLoopProcessing",
  description: "Loop sample with retrieve list, loop body, continue, break, and change object actions."
};

export const sampleObjectTypeDecisionMicroflowSchema = appendLinearActions(
  {
    ...JSON.parse(JSON.stringify(sampleMicroflowSchema)) as MicroflowSchema,
    id: "sample-object-type-decision",
    stableId: "sample-object-type-decision",
    name: "SampleObjectTypeDecision",
    displayName: "SampleObjectTypeDecision",
    description: "Object type decision sample with InheritanceSplit and cast object actions."
  },
  [
    { key: "cast", id: "cast-professor", caption: "Cast Professor", x: 280, y: 140, patch: { sourceObjectVariableName: "member", targetEntityQualifiedName: "University.Professor", outputVariableName: "professor" } },
    { key: "cast", id: "cast-student", caption: "Cast Student", x: 520, y: 220, patch: { sourceObjectVariableName: "member", targetEntityQualifiedName: "University.Student", outputVariableName: "student" } }
  ]
);

export const sampleListProcessingMicroflowSchema = appendLinearActions(
  cloneBase("sample-list-processing", "SampleListProcessing", "List sample with create, change, aggregate, and operation actions."),
  [
    { key: "createList", id: "list-create", caption: "Create List", x: 280, y: 180, patch: { entityQualifiedName: "Sales.Order", outputListVariableName: "orders" } },
    { key: "changeList", id: "list-change", caption: "Change List", x: 520, y: 180, patch: { targetListVariableName: "orders", operation: "add", objectVariableName: "order" } },
    { key: "aggregateList", id: "list-aggregate", caption: "Aggregate List", x: 760, y: 180, patch: { listVariableName: "orders", aggregateFunction: "count", outputVariableName: "orderCount" } },
    { key: "listOperation", id: "list-operation", caption: "List Operation", x: 1000, y: 180, patch: { leftListVariableName: "orders", operation: "filter", outputVariableName: "filteredOrders" } }
  ]
);

export const microflowSampleSchemas = [
  { key: "orderProcessing", title: "Order Processing", schema: sampleMicroflowSchema },
  { key: "approval", title: "Approval Flow", schema: sampleApprovalFlowMicroflowSchema },
  { key: "restErrorHandling", title: "REST Error Handling", schema: sampleRestErrorHandlingMicroflowSchema },
  { key: "loopProcessing", title: "Loop Processing", schema: sampleLoopProcessingMicroflowSchema },
  { key: "objectTypeDecision", title: "Object Type Decision", schema: sampleObjectTypeDecisionMicroflowSchema },
  { key: "listProcessing", title: "List Processing", schema: sampleListProcessingMicroflowSchema }
] as const;
