import type {
  MicroflowCaseValue,
  MicroflowExpression,
  MicroflowFlow,
  MicroflowObject,
  MicroflowObjectCollection,
  MicroflowSchema,
  MicroflowSequenceFlow,
} from "../types";
import { caseValueIdentity, createBooleanCaseValue, createNoCaseValue, getCaseDisplayLabel } from "./case-utils";
import { collectFlowsRecursive } from "./object-utils";

function mapObjectCollection(
  collection: MicroflowObjectCollection,
  updater: (object: MicroflowObject) => MicroflowObject,
): MicroflowObjectCollection {
  return {
    ...collection,
    objects: collection.objects.map(object => {
      const next = updater(object);
      return next.kind === "loopedActivity"
        ? { ...next, objectCollection: mapObjectCollection(next.objectCollection, updater) }
        : next;
    }),
  };
}

function mapFlows(schema: MicroflowSchema, flowId: string, updater: (flow: MicroflowFlow) => MicroflowFlow): MicroflowSchema {
  const updateFlows = (flows: MicroflowFlow[] | undefined): MicroflowFlow[] | undefined => flows?.map(flow => flow.id === flowId ? updater(flow) : flow);
  const updateCollection = (collection: MicroflowObjectCollection): MicroflowObjectCollection => ({
    ...collection,
    flows: updateFlows(collection.flows),
    objects: collection.objects.map(object => object.kind === "loopedActivity"
      ? { ...object, objectCollection: updateCollection(object.objectCollection) }
      : object),
  });
  return {
    ...schema,
    flows: updateFlows(schema.flows) ?? schema.flows,
    objectCollection: updateCollection(schema.objectCollection),
  };
}

export function updateDecisionExpression(schema: MicroflowSchema, decisionObjectId: string, expression: MicroflowExpression): MicroflowSchema {
  return {
    ...schema,
    objectCollection: mapObjectCollection(schema.objectCollection, object => object.id === decisionObjectId && object.kind === "exclusiveSplit" && object.splitCondition.kind === "expression"
      ? { ...object, splitCondition: { ...object.splitCondition, expression } }
      : object),
  };
}

export function updateDecisionType(schema: MicroflowSchema, decisionObjectId: string, decisionType: "boolean" | "enumeration"): MicroflowSchema {
  return {
    ...schema,
    objectCollection: mapObjectCollection(schema.objectCollection, object => object.id === decisionObjectId && object.kind === "exclusiveSplit"
      ? {
          ...object,
          splitCondition: object.splitCondition.kind === "expression"
            ? {
                ...object.splitCondition,
                resultType: decisionType,
                enumerationQualifiedName: decisionType === "enumeration" ? object.splitCondition.enumerationQualifiedName ?? "" : undefined,
              }
            : object.splitCondition,
        }
      : object),
  };
}

export function getDecisionOutgoingFlows(schema: MicroflowSchema, decisionObjectId: string): MicroflowSequenceFlow[] {
  return collectFlowsRecursive(schema).filter((flow): flow is MicroflowSequenceFlow =>
    flow.kind === "sequence" &&
    flow.originObjectId === decisionObjectId &&
    !flow.isErrorHandler &&
    flow.editor.edgeKind === "decisionCondition"
  );
}

export function assignDecisionBranchCase(schema: MicroflowSchema, flowId: string, branchCase: MicroflowCaseValue): MicroflowSchema {
  return mapFlows(schema, flowId, flow => {
    if (flow.kind !== "sequence") {
      return flow;
    }
    return {
      ...flow,
      caseValues: [branchCase],
      editor: {
        ...flow.editor,
        edgeKind: branchCase.kind === "noCase" ? flow.editor.edgeKind : "decisionCondition",
        label: getCaseDisplayLabel(branchCase),
      },
    };
  });
}

export function assignDecisionBooleanCase(schema: MicroflowSchema, flowId: string, value: boolean): MicroflowSchema {
  return assignDecisionBranchCase(schema, flowId, createBooleanCaseValue(value));
}

export function releaseDecisionBranchCase(schema: MicroflowSchema, flowId: string): MicroflowSchema {
  return assignDecisionBranchCase(schema, flowId, createNoCaseValue());
}

export function updateFlowLabel(schema: MicroflowSchema, flowId: string, label: string): MicroflowSchema {
  return mapFlows(schema, flowId, flow => ({
    ...flow,
    editor: {
      ...flow.editor,
      label,
    },
  } as MicroflowFlow));
}

export function getDecisionBranchConflicts(schema: MicroflowSchema, decisionObjectId: string): Array<{ key: string; flowIds: string[] }> {
  const byKey = new Map<string, string[]>();
  for (const flow of getDecisionOutgoingFlows(schema, decisionObjectId)) {
    for (const caseValue of flow.caseValues) {
      const key = caseValueIdentity(caseValue);
      byKey.set(key, [...(byKey.get(key) ?? []), flow.id]);
    }
  }
  return [...byKey.entries()].filter(([, flowIds]) => flowIds.length > 1).map(([key, flowIds]) => ({ key, flowIds }));
}

export function updateMergeBehavior(schema: MicroflowSchema, mergeObjectId: string, behavior: "firstArrived"): MicroflowSchema {
  return {
    ...schema,
    objectCollection: mapObjectCollection(schema.objectCollection, object => object.id === mergeObjectId && object.kind === "exclusiveMerge"
      ? { ...object, mergeBehavior: { strategy: behavior } }
      : object),
  };
}

export function getMergeFlowSummary(schema: MicroflowSchema, mergeObjectId: string): { incoming: MicroflowFlow[]; outgoing: MicroflowFlow[] } {
  const flows = collectFlowsRecursive(schema);
  return {
    incoming: flows.filter(flow => flow.destinationObjectId === mergeObjectId),
    outgoing: flows.filter(flow => flow.originObjectId === mergeObjectId),
  };
}
