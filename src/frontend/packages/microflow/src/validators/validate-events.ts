import type { MicroflowSchema, MicroflowValidationIssue } from "../schema/types";
import { flattenObjects, issue } from "./shared";

export function validateEvents(schema: MicroflowSchema): MicroflowValidationIssue[] {
  const issues: MicroflowValidationIssue[] = [];
  const flattened = flattenObjects(schema.objectCollection);
  const objects = flattened.map(item => item.object);
  const starts = objects.filter(object => object.kind === "startEvent");
  const ends = objects.filter(object => object.kind === "endEvent");
  if (starts.length === 0) {
    issues.push(issue("MF_START_MISSING", "Microflow must contain one StartEvent."));
  }
  if (starts.length > 1) {
    issues.push(issue("MF_START_DUPLICATED", "Microflow must contain only one StartEvent."));
  }
  if (ends.length === 0) {
    issues.push(issue("MF_END_MISSING", "Microflow must contain at least one EndEvent."));
  }
  for (const item of flattened) {
    const incoming = schema.flows.filter(flow => flow.destinationObjectId === item.object.id);
    const outgoing = schema.flows.filter(flow => flow.originObjectId === item.object.id);
    if (item.object.kind === "startEvent") {
      if (incoming.length > 0) {
        issues.push(issue("MF_START_HAS_INCOMING", "StartEvent cannot have incoming flows.", { objectId: item.object.id }));
      }
      if (item.loopObjectId) {
        issues.push(issue("MF_START_IN_LOOP", "StartEvent cannot be placed inside Loop.", { objectId: item.object.id }));
      }
    }
    if (item.object.kind === "endEvent") {
      if (outgoing.length > 0) {
        issues.push(issue("MF_END_HAS_OUTGOING", "EndEvent cannot have outgoing flows.", { objectId: item.object.id }));
      }
      if (item.loopObjectId) {
        issues.push(issue("MF_END_IN_LOOP", "EndEvent cannot be placed inside Loop.", { objectId: item.object.id }));
      }
      if (schema.returnType.kind === "void" && item.object.returnValue) {
        issues.push(issue("MF_END_RETURN_TYPE_MISMATCH", "Void microflow EndEvent must not have returnValue.", { objectId: item.object.id, fieldPath: "returnValue" }));
      }
      if (schema.returnType.kind !== "void" && !item.object.returnValue) {
        issues.push(issue("MF_END_RETURN_TYPE_MISMATCH", "Non-void microflow EndEvent must have returnValue.", { objectId: item.object.id, fieldPath: "returnValue" }));
      }
    }
    if (item.object.kind === "errorEvent" && outgoing.length > 0) {
      issues.push(issue("MF_ERROR_EVENT_OUTGOING", "ErrorEvent cannot have outgoing flows.", { objectId: item.object.id }));
    }
  }
  return issues;
}
