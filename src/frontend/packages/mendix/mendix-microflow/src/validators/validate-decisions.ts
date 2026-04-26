import type { MicroflowCaseValue, MicroflowSchema, MicroflowValidationIssue } from "../schema/types";
import { findEnumeration, mockMicroflowMetadataCatalog } from "../metadata";
import { getAllowedSpecializations } from "../flowgram/adapters/flowgram-case-options";
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
        const bools = new Set(outgoing.flatMap(flow => flow.kind === "sequence" ? flow.caseValues : []).filter((item): item is Extract<MicroflowCaseValue, { kind: "boolean" }> => item !== undefined && item.kind === "boolean").map(item => item.value));
        if (!bools.has(true) || !bools.has(false)) {
          issues.push(issue("MF_DECISION_BRANCH_MISSING", "Boolean ExclusiveSplit must have true and false cases.", { objectId: object.id }));
        }
      }
      if (object.kind === "exclusiveSplit" && object.splitCondition.kind === "expression" && object.splitCondition.resultType === "enumeration") {
        const enumeration = findEnumeration(mockMicroflowMetadataCatalog, object.splitCondition.enumerationQualifiedName);
        if (!object.splitCondition.enumerationQualifiedName || !enumeration) {
          issues.push(issue("MF_ENUMERATION_DECISION_UNKNOWN_ENUMERATION", "Enumeration ExclusiveSplit must reference an existing enumeration.", { objectId: object.id, fieldPath: "splitCondition.enumerationQualifiedName" }));
        }
        for (const caseValue of outgoing.flatMap(flow => flow.caseValues).filter((item): item is Extract<MicroflowCaseValue, { kind: "enumeration" }> => item !== undefined && item.kind === "enumeration")) {
          if (caseValue.enumerationQualifiedName !== object.splitCondition.enumerationQualifiedName) {
            issues.push(issue("MF_ENUMERATION_CASE_ENUMERATION_MISMATCH", "Enumeration case must use the source ExclusiveSplit enumeration.", { objectId: object.id }));
          }
          if (enumeration && !enumeration.values.includes(caseValue.value)) {
            issues.push(issue("MF_ENUMERATION_CASE_INVALID_VALUE", "Enumeration case value must belong to the selected enumeration.", { objectId: object.id }));
          }
        }
      }
      if (object.kind === "inheritanceSplit") {
        if (!object.inputObjectVariableName) {
          issues.push(issue("MF_OBJECT_TYPE_INPUT_MISSING", "InheritanceSplit must define inputObjectVariableName.", { objectId: object.id, fieldPath: "inputObjectVariableName" }));
        }
        if (!object.entity.generalizedEntityQualifiedName) {
          issues.push(issue("MF_OBJECT_TYPE_GENERALIZATION_MISSING", "InheritanceSplit must define generalizedEntityQualifiedName.", { objectId: object.id, fieldPath: "entity.generalizedEntityQualifiedName" }));
        }
        const allowed = getAllowedSpecializations(object, mockMicroflowMetadataCatalog);
        const inheritanceCases = outgoing
          .flatMap(flow => flow.caseValues)
          .filter((item): item is Extract<MicroflowCaseValue, { kind: "inheritance" }> => item !== undefined && item.kind === "inheritance")
          .map(item => item.entityQualifiedName);
        if (new Set(inheritanceCases).size !== inheritanceCases.length) {
          issues.push(issue("MF_DECISION_DUPLICATE_CASE", "InheritanceSplit cannot have duplicate specialization branches.", { objectId: object.id }));
        }
        if (allowed.length > 0 && inheritanceCases.some(entity => !allowed.includes(entity))) {
          issues.push(issue("MF_OBJECT_TYPE_CASE_INVALID_SPECIALIZATION", "InheritanceSplit case must reference an allowed specialization.", { objectId: object.id }));
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
