import type { MicroflowCaseValue, MicroflowSchema, MicroflowValidationIssue } from "../schema/types";
import { flattenObjects, issue } from "./shared";

function caseKey(caseValue: MicroflowCaseValue): string {
  if (caseValue.kind === "boolean") {
    return `boolean:${caseValue.value}`;
  }
  if (caseValue.kind === "enumeration") {
    return `enumeration:${caseValue.enumerationQualifiedName}:${caseValue.value}`;
  }
  if (caseValue.kind === "inheritance") {
    return `inheritance:${caseValue.entityQualifiedName}`;
  }
  return caseValue.kind;
}

export function validateDecisions(schema: MicroflowSchema): MicroflowValidationIssue[] {
  const issues: MicroflowValidationIssue[] = [];
  for (const { object } of flattenObjects(schema.objectCollection)) {
    if (object.kind === "exclusiveSplit" || object.kind === "inheritanceSplit") {
      const outgoing = schema.flows.filter(flow => flow.kind === "sequence" && flow.originObjectId === object.id && !flow.isErrorHandler);
      if (outgoing.length < 2) {
        issues.push(issue("MF_DECISION_BRANCH_MISSING", "Decision must have at least two outgoing SequenceFlows.", { objectId: object.id }));
      }
      const keys = outgoing.flatMap(flow => flow.kind === "sequence" ? flow.caseValues.map(caseKey) : []);
      if (new Set(keys).size !== keys.length) {
        issues.push(issue("MF_DECISION_DUPLICATE_CASE", "Decision cannot have duplicate caseValues.", { objectId: object.id }));
      }
      if (object.kind === "exclusiveSplit" && object.splitCondition.kind === "expression" && object.splitCondition.resultType === "boolean") {
        const bools = new Set(outgoing.flatMap(flow => flow.kind === "sequence" ? flow.caseValues : []).filter(item => item.kind === "boolean").map(item => item.value));
        if (!bools.has(true) || !bools.has(false)) {
          issues.push(issue("MF_DECISION_BRANCH_MISSING", "Boolean ExclusiveSplit must have true and false cases.", { objectId: object.id }));
        }
      }
      if (object.kind === "inheritanceSplit") {
        const inheritanceCases = outgoing
          .flatMap(flow => flow.caseValues)
          .filter((item): item is Extract<MicroflowCaseValue, { kind: "inheritance" }> => item.kind === "inheritance")
          .map(item => item.entityQualifiedName);
        if (new Set(inheritanceCases).size !== inheritanceCases.length) {
          issues.push(issue("MF_DECISION_DUPLICATE_CASE", "InheritanceSplit cannot have duplicate specialization branches.", { objectId: object.id }));
        }
      }
      continue;
    }
    if (object.kind === "exclusiveMerge") {
      const incoming = schema.flows.filter(flow => flow.kind === "sequence" && flow.destinationObjectId === object.id);
      const outgoing = schema.flows.filter(flow => flow.kind === "sequence" && flow.originObjectId === object.id);
      if (incoming.length < 2) {
        issues.push(issue("MF_DECISION_BRANCH_MISSING", "ExclusiveMerge must have at least two incoming SequenceFlows.", { objectId: object.id }));
      }
      if (outgoing.length < 1) {
        issues.push(issue("MF_DECISION_BRANCH_MISSING", "ExclusiveMerge must have one outgoing SequenceFlow.", { objectId: object.id }));
      }
    }
  }
  return issues;
}
