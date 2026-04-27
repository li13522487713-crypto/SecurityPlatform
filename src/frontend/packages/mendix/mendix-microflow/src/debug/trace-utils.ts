import type {
  MicroflowAuthoringSchema,
  MicroflowCaseValue,
  MicroflowFlow,
  MicroflowObject,
  MicroflowObjectCollection,
  MicroflowSequenceFlow,
} from "../schema/types";
import type { MicroflowTestRunOptions } from "./trace-types";

export function collectRuntimeObjects(collection: MicroflowObjectCollection): MicroflowObject[] {
  return collection.objects.flatMap(object => object.kind === "loopedActivity"
    ? [object, ...collectRuntimeObjects(object.objectCollection)]
    : [object]);
}

export function collectRuntimeFlows(schema: Pick<MicroflowAuthoringSchema, "flows" | "objectCollection">): MicroflowFlow[] {
  return [...schema.flows, ...collectCollectionFlows(schema.objectCollection)];
}

function collectCollectionFlows(collection: MicroflowObjectCollection): MicroflowFlow[] {
  return [
    ...(collection.flows ?? []),
    ...collection.objects.flatMap(object => object.kind === "loopedActivity" ? collectCollectionFlows(object.objectCollection) : []),
  ];
}

export function getStartEvent(
  schema: Pick<MicroflowAuthoringSchema, "objectCollection">,
  collection: MicroflowObjectCollection = schema.objectCollection
): MicroflowObject | undefined {
  return collection.objects.find(object => object.kind === "startEvent");
}

export function getOutgoingSequenceFlows(schema: MicroflowAuthoringSchema, objectId: string): MicroflowSequenceFlow[] {
  return collectRuntimeFlows(schema).filter((flow): flow is MicroflowSequenceFlow =>
    flow.kind === "sequence" && flow.originObjectId === objectId && !flow.isErrorHandler
  ).sort(byBranchOrder);
}

export function getOutgoingErrorHandlerFlows(schema: MicroflowAuthoringSchema, objectId: string): MicroflowSequenceFlow[] {
  return collectRuntimeFlows(schema).filter((flow): flow is MicroflowSequenceFlow =>
    flow.kind === "sequence" && flow.originObjectId === objectId && flow.isErrorHandler
  ).sort(byBranchOrder);
}

export function getNextNormalFlow(schema: MicroflowAuthoringSchema, objectId: string): MicroflowSequenceFlow | undefined {
  return getOutgoingSequenceFlows(schema, objectId).find(flow => flow.caseValues.length === 0) ?? getOutgoingSequenceFlows(schema, objectId)[0];
}

export function getFlowTargetObject(schema: MicroflowAuthoringSchema, flow: MicroflowFlow): MicroflowObject | undefined {
  return collectRuntimeObjects(schema.objectCollection).find(object => object.id === flow.destinationObjectId);
}

export function getFlowSourceObject(schema: MicroflowAuthoringSchema, flow: MicroflowFlow): MicroflowObject | undefined {
  return collectRuntimeObjects(schema.objectCollection).find(object => object.id === flow.originObjectId);
}

export function isTerminalObject(object: MicroflowObject): boolean {
  return object.kind === "endEvent" || object.kind === "errorEvent" || object.kind === "breakEvent" || object.kind === "continueEvent";
}

export function isExecutableObject(object: MicroflowObject): boolean {
  return object.kind !== "annotation" && object.kind !== "parameterObject";
}

export function selectDecisionFlow(
  schema: MicroflowAuthoringSchema,
  decisionObject: Extract<MicroflowObject, { kind: "exclusiveSplit" }>,
  options: MicroflowTestRunOptions = {},
): { flow?: MicroflowSequenceFlow; selectedCaseValue?: MicroflowCaseValue; warning?: string } {
  const outgoing = getOutgoingSequenceFlows(schema, decisionObject.id);
  if (outgoing.length === 0) {
    return {};
  }
  if (decisionObject.splitCondition.resultType === "enumeration") {
    const requested = options.enumerationCaseValue;
    const selected = requested
      ? outgoing.find(flow => flow.caseValues.some(value => value.kind === "enumeration" && value.value === requested))
      : outgoing.find(flow => flow.caseValues.some(value => value.kind === "enumeration"));
    const fallback = selected
      ?? outgoing.find(flow => flow.caseValues.some(value => value.kind === "empty"))
      ?? outgoing.find(flow => flow.caseValues.some(value => value.kind === "noCase"))
      ?? outgoing[0];
    return { flow: fallback, selectedCaseValue: fallback.caseValues[0] };
  }
  const expected = options.decisionBooleanResult ?? true;
  const selected = outgoing.find(flow => flow.caseValues.some(value => value.kind === "boolean" && value.value === expected));
  const fallback = selected ?? outgoing[0];
  return {
    flow: fallback,
    selectedCaseValue: selected?.caseValues.find(value => value.kind === "boolean" && value.value === expected) ?? fallback.caseValues[0],
    warning: selected ? undefined : `Boolean decision branch ${String(expected)} not found; first branch was used.`,
  };
}

export function selectObjectTypeFlow(
  schema: MicroflowAuthoringSchema,
  inheritanceSplit: Extract<MicroflowObject, { kind: "inheritanceSplit" }>,
  options: MicroflowTestRunOptions = {},
): { flow?: MicroflowSequenceFlow; selectedCaseValue?: MicroflowCaseValue; warning?: string } {
  const outgoing = getOutgoingSequenceFlows(schema, inheritanceSplit.id);
  if (outgoing.length === 0) {
    return {};
  }
  const requested = options.objectTypeCase;
  const selected = requested
    ? outgoing.find(flow => flow.caseValues.some(value => value.kind === "inheritance" && value.entityQualifiedName === requested))
    : outgoing.find(flow => flow.caseValues.some(value => value.kind === "inheritance"));
  const fallback = selected
    ?? outgoing.find(flow => flow.caseValues.some(value => value.kind === "empty"))
    ?? outgoing.find(flow => flow.caseValues.some(value => value.kind === "fallback"))
    ?? outgoing[0];
  return {
    flow: fallback,
    selectedCaseValue: fallback.caseValues[0],
    warning: requested && !selected ? `Object type case ${requested} not found; fallback branch was used.` : undefined,
  };
}

function byBranchOrder(left: MicroflowSequenceFlow, right: MicroflowSequenceFlow): number {
  return (left.editor.branchOrder ?? 0) - (right.editor.branchOrder ?? 0) || left.id.localeCompare(right.id);
}
