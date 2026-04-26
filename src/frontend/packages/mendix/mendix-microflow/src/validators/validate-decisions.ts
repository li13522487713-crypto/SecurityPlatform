import type { MicroflowCaseValue, MicroflowSchema, MicroflowValidationIssue } from "../schema/types";
import { collectFlowsRecursive } from "../schema/utils/object-utils";
import { findEnumeration, getEnumerationValueKeys } from "../metadata";
import { getAllowedSpecializations } from "../flowgram/adapters/flowgram-case-options";
import { flattenObjects, issue } from "./shared";
import type { MicroflowValidatorContext } from "./validator-types";

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

export function validateDecisions(schema: MicroflowSchema, context: MicroflowValidatorContext): MicroflowValidationIssue[] {
  const { metadata } = context;
  const issues: MicroflowValidationIssue[] = [];
  const flows = collectFlowsRecursive(schema);
  for (const { object } of flattenObjects(schema.objectCollection)) {
    if (object.kind === "exclusiveSplit" || object.kind === "inheritanceSplit") {
      const outgoing = flows.filter(flow => flow.kind === "sequence" && flow.originObjectId === object.id && !flow.isErrorHandler);
      if (outgoing.length < 2) {
        issues.push(issue("MF_DECISION_BRANCH_MISSING", "Decision must have at least two outgoing SequenceFlows.", { objectId: object.id }));
      }
      const keys = outgoing.flatMap(flow => flow.kind === "sequence" ? flow.caseValues.map(caseKey) : []);
      if (new Set(keys).size !== keys.length) {
        issues.push(issue("MF_DECISION_DUPLICATE_CASE", "Decision cannot have duplicate caseValues.", { objectId: object.id }));
      }
      const seenCaseFlowIds = new Map<string, string>();
      for (const flow of outgoing) {
        if (flow.caseValues.length === 0) {
          issues.push(issue("MF_DECISION_CASE_MISSING", "Decision branch must define caseValues.", { flowId: flow.id, objectId: object.id, fieldPath: "caseValues" }));
        }
        for (const caseValue of flow.caseValues) {
          const key = caseKey(caseValue);
          const firstFlowId = seenCaseFlowIds.get(key);
          if (firstFlowId) {
            issues.push(issue("MF_DECISION_DUPLICATE_CASE", "Decision case value is already used by another flow.", { flowId: flow.id, objectId: object.id, fieldPath: "caseValues" }));
          } else {
            seenCaseFlowIds.set(key, flow.id);
          }
        }
      }
      if (object.kind === "exclusiveSplit" && object.splitCondition.kind === "expression" && object.splitCondition.resultType === "boolean") {
        for (const flow of outgoing) {
          for (const caseValue of flow.caseValues) {
            if (caseValue.kind !== "boolean") {
              issues.push(issue("MF_BOOLEAN_DECISION_CASE_KIND", "Boolean ExclusiveSplit only supports boolean case values.", { flowId: flow.id, objectId: object.id, fieldPath: "caseValues" }));
            }
          }
        }
        const bools = new Set(outgoing.flatMap(flow => flow.kind === "sequence" ? flow.caseValues : []).filter((item): item is Extract<MicroflowCaseValue, { kind: "boolean" }> => item !== undefined && item.kind === "boolean").map(item => item.value));
        if (!bools.has(true) || !bools.has(false)) {
          issues.push(issue("MF_DECISION_BRANCH_MISSING", "Boolean ExclusiveSplit must have true and false cases.", { objectId: object.id }));
        }
      }
      if (object.kind === "exclusiveSplit" && object.splitCondition.kind === "expression" && object.splitCondition.resultType === "enumeration") {
        const enumeration = findEnumeration(metadata, object.splitCondition.enumerationQualifiedName);
        if (!object.splitCondition.enumerationQualifiedName || !enumeration) {
          issues.push(issue("MF_ENUMERATION_DECISION_UNKNOWN_ENUMERATION", "Enumeration ExclusiveSplit must reference an existing enumeration.", { objectId: object.id, fieldPath: "splitCondition.enumerationQualifiedName" }));
        }
        for (const flow of outgoing) {
          for (const caseValue of flow.caseValues) {
            if (caseValue.kind !== "enumeration" && caseValue.kind !== "empty") {
              issues.push(issue("MF_ENUMERATION_DECISION_CASE_KIND", "Enumeration ExclusiveSplit only supports enumeration or empty case values.", { flowId: flow.id, objectId: object.id, fieldPath: "caseValues" }));
              continue;
            }
            if (caseValue.kind === "enumeration" && caseValue.enumerationQualifiedName !== object.splitCondition.enumerationQualifiedName) {
              issues.push(issue("MF_ENUMERATION_CASE_ENUMERATION_MISMATCH", "Enumeration case must use the source ExclusiveSplit enumeration.", { flowId: flow.id, objectId: object.id, fieldPath: "caseValues" }));
            }
            if (caseValue.kind === "enumeration" && enumeration && !getEnumerationValueKeys(metadata, enumeration.qualifiedName).includes(caseValue.value)) {
              issues.push(issue("MF_ENUMERATION_CASE_INVALID_VALUE", "Enumeration case value must belong to the selected enumeration.", { flowId: flow.id, objectId: object.id, fieldPath: "caseValues" }));
            }
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
        const allowed = getAllowedSpecializations(object, metadata);
        const inheritanceCases = outgoing
          .flatMap(flow => flow.caseValues)
          .filter((item): item is Extract<MicroflowCaseValue, { kind: "inheritance" }> => item !== undefined && item.kind === "inheritance")
          .map(item => item.entityQualifiedName);
        if (new Set(inheritanceCases).size !== inheritanceCases.length) {
          issues.push(issue("MF_DECISION_DUPLICATE_CASE", "InheritanceSplit cannot have duplicate specialization branches.", { objectId: object.id }));
        }
        for (const flow of outgoing) {
          for (const caseValue of flow.caseValues) {
            if (caseValue.kind !== "inheritance" && caseValue.kind !== "empty" && caseValue.kind !== "fallback") {
              issues.push(issue("MF_OBJECT_TYPE_CASE_KIND", "InheritanceSplit only supports inheritance, empty, or fallback case values.", { flowId: flow.id, objectId: object.id, fieldPath: "caseValues" }));
            }
          }
        }
        if (allowed.length > 0 && inheritanceCases.some(entity => !allowed.includes(entity))) {
          issues.push(issue("MF_OBJECT_TYPE_CASE_INVALID_SPECIALIZATION", "InheritanceSplit case must reference an allowed specialization.", { objectId: object.id }));
        }
      }
      continue;
    }
    if (object.kind === "exclusiveMerge") {
      const incoming = flows.filter(flow => flow.kind === "sequence" && flow.destinationObjectId === object.id);
      const outgoing = flows.filter(flow => flow.kind === "sequence" && flow.originObjectId === object.id);
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
