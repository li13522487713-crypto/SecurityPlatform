import {
  createMicroflowFlowId,
  createObjectFromRegistry,
  createSequenceFlow,
} from "../adapters";
import {
  createUniqueMicroflowObjectId,
  microflowNodeRegistryByKey,
} from "../node-registry";
import type {
  MicroflowFlow,
  MicroflowSchema,
  MicroflowValidationIssue,
} from "../schema";
import {
  addFlowToCollection,
  addObjectToCollection,
  collectFlowsRecursive,
  findObjectWithCollection,
} from "../schema/utils/object-utils";
import { isBooleanExclusiveSplit } from "../schema/utils/exclusive-split-utils";

export function missingBooleanBranchValue(issue: MicroflowValidationIssue): boolean | undefined {
  if (issue.code === "MF_DECISION_BOOLEAN_TRUE_MISSING") {
    return true;
  }
  if (issue.code === "MF_DECISION_BOOLEAN_FALSE_MISSING") {
    return false;
  }
  const flowFix = issue.quickFixes?.find(item => item.kind === "createMissingFlow");
  const payload = flowFix?.payload as { caseKind?: string; value?: unknown } | undefined;
  return payload?.caseKind === "boolean" && typeof payload.value === "boolean" ? payload.value : undefined;
}

export function canApplyBooleanBranchQuickFix(issue: MicroflowValidationIssue): boolean {
  return Boolean(issue.objectId && missingBooleanBranchValue(issue) !== undefined);
}

export function createMissingBooleanBranch(schema: MicroflowSchema, issue: MicroflowValidationIssue): MicroflowSchema | undefined {
  const decisionId = issue.objectId;
  const value = missingBooleanBranchValue(issue);
  if (!decisionId || value === undefined) {
    return undefined;
  }
  const located = findObjectWithCollection(schema, decisionId);
  const decision = located?.object;
  if (!located || !decision || !isBooleanExclusiveSplit(decision)) {
    return undefined;
  }
  const existing = collectFlowsRecursive(schema).filter(
    (flow): flow is Extract<MicroflowFlow, { kind: "sequence" }> =>
      flow.kind === "sequence" && flow.originObjectId === decision.id && !flow.isErrorHandler
  );
  if (existing.some(flow => flow.caseValues.some(caseValue => caseValue.kind === "boolean" && caseValue.value === value))) {
    return undefined;
  }
  const targetKind = located.parentLoopObjectId ? "continueEvent" : "endEvent";
  const entry = microflowNodeRegistryByKey.get(targetKind);
  if (!entry) {
    return undefined;
  }
  const target = createObjectFromRegistry(
    entry,
    {
      x: decision.relativeMiddlePoint.x + 260,
      y: decision.relativeMiddlePoint.y + (value ? -110 : 110),
    },
    createUniqueMicroflowObjectId(schema, `${targetKind}-${value ? "true" : "false"}`)
  );
  const flow = createSequenceFlow({
    id: createMicroflowFlowId(schema, `flow-${value ? "true" : "false"}`),
    originObjectId: decision.id,
    destinationObjectId: target.id,
    edgeKind: "decisionCondition",
    originConnectionIndex: value ? 0 : 1,
    caseValues: [{ kind: "boolean", officialType: "Microflows$EnumerationCase", value, persistedValue: value ? "true" : "false" }],
    label: value ? "true" : "false",
  });
  const next = addObjectToCollection(schema, located.collectionId, target);
  return {
    ...addFlowToCollection(next, located.collectionId, flow),
    editor: {
      ...schema.editor,
      selection: { objectId: target.id, flowId: undefined },
    },
  };
}
