import type { MicroflowSchema, MicroflowValidationIssue } from "../schema/types";
import { objectMap, issue } from "./shared";

export function validateFlows(schema: MicroflowSchema): MicroflowValidationIssue[] {
  const issues: MicroflowValidationIssue[] = [];
  const objects = objectMap(schema);
  for (const flow of schema.flows) {
    const source = objects.get(flow.originObjectId);
    const target = objects.get(flow.destinationObjectId);
    if (!source) {
      issues.push(issue("MF_FLOW_ORIGIN_MISSING", "Flow originObjectId must reference an object.", { flowId: flow.id, fieldPath: "originObjectId" }));
    }
    if (!target) {
      issues.push(issue("MF_FLOW_DESTINATION_MISSING", "Flow destinationObjectId must reference an object.", { flowId: flow.id, fieldPath: "destinationObjectId" }));
    }
    if (flow.kind === "annotation") {
      if (source?.kind !== "annotation" && target?.kind !== "annotation") {
        issues.push(issue("MF_ANNOTATION_EDGE_ENDPOINT", "AnnotationFlow must connect to at least one Annotation.", { flowId: flow.id }));
      }
      continue;
    }
    if (source?.kind === "startEvent" && flow.isErrorHandler) {
      issues.push(issue("MF_START_ERROR_HANDLER", "StartEvent cannot create an error handler flow.", { flowId: flow.id, objectId: source.id }));
    }
    if (target?.kind === "startEvent") {
      issues.push(issue("MF_START_HAS_INCOMING", "StartEvent cannot have incoming SequenceFlow.", { flowId: flow.id, objectId: target.id }));
    }
    if (source && ["endEvent", "errorEvent", "breakEvent", "continueEvent"].includes(source.kind)) {
      issues.push(issue("MF_TERMINAL_HAS_OUTGOING", "Terminal events cannot have outgoing SequenceFlow.", { flowId: flow.id, objectId: source.id }));
    }
    if (source && ["parameterObject", "annotation"].includes(source.kind) || target && ["parameterObject", "annotation"].includes(target.kind)) {
      issues.push(issue("MF_NON_EXECUTABLE_SEQUENCE", "SequenceFlow cannot connect ParameterObject or Annotation.", { flowId: flow.id }));
    }
    if (target?.kind === "errorEvent" && !flow.isErrorHandler) {
      issues.push(issue("MF_ERROR_EVENT_REQUIRES_ERROR_FLOW", "ErrorEvent can only be reached by an error handler SequenceFlow.", { flowId: flow.id, objectId: target.id }));
    }
    if (flow.isErrorHandler && source && !["actionActivity", "loopedActivity", "exclusiveSplit", "inheritanceSplit"].includes(source.kind)) {
      issues.push(issue("MF_ERROR_FLOW_SOURCE", "isErrorHandler source must support errorHandling.", { flowId: flow.id }));
    }
    if (source?.kind === "exclusiveSplit" && flow.editor.edgeKind === "decisionCondition") {
      const booleanCases = flow.caseValues.filter(caseValue => caseValue.kind === "boolean");
      for (const caseValue of booleanCases) {
        const duplicates = schema.flows.filter(item =>
          item.kind === "sequence" &&
          item.id !== flow.id &&
          item.originObjectId === source.id &&
          item.caseValues.some(other => other.kind === "boolean" && other.value === caseValue.value)
        );
        if (duplicates.length > 0) {
          issues.push(issue("MF_DECISION_CASE_DUPLICATED", "Decision case values must be unique per source.", { flowId: flow.id, objectId: source.id }));
        }
      }
    }
    if (source?.kind === "inheritanceSplit" && flow.editor.edgeKind === "objectTypeCondition") {
      for (const caseValue of flow.caseValues.filter(caseValue => caseValue.kind === "inheritance")) {
        const duplicates = schema.flows.filter(item =>
          item.kind === "sequence" &&
          item.id !== flow.id &&
          item.originObjectId === source.id &&
          item.caseValues.some(other => other.kind === "inheritance" && other.entityQualifiedName === caseValue.entityQualifiedName)
        );
        if (duplicates.length > 0) {
          issues.push(issue("MF_OBJECT_TYPE_CASE_DUPLICATED", "Object type case values must be unique per source.", { flowId: flow.id, objectId: source.id }));
        }
      }
    }
  }
  return issues;
}
