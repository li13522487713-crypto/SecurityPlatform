import type { MicroflowSchema, MicroflowValidationIssue } from "../schema/types";
import { flattenObjects, issue } from "./shared";

export function validateActions(schema: MicroflowSchema): MicroflowValidationIssue[] {
  const issues: MicroflowValidationIssue[] = [];
  for (const { object } of flattenObjects(schema.objectCollection)) {
    if (object.kind !== "actionActivity") {
      continue;
    }
    const action = object.action;
    if (!action.id || !action.kind || !action.officialType) {
      issues.push(issue("MF_ACTION_REQUIRED_FIELD_MISSING", "Action must have id, kind and officialType.", { objectId: object.id, actionId: action.id, fieldPath: "action" }));
    }
    if (action.kind === "retrieve") {
      if (!action.outputVariableName.trim()) {
        issues.push(issue("MF_ACTION_REQUIRED_FIELD_MISSING", "RetrieveAction.outputVariableName is required.", { objectId: object.id, actionId: action.id, fieldPath: "action.outputVariableName" }));
      }
      if (action.retrieveSource.kind === "database" && !action.retrieveSource.entityQualifiedName) {
        issues.push(issue("MF_ACTION_REQUIRED_FIELD_MISSING", "DatabaseRetrieveSource.entityQualifiedName is required.", { objectId: object.id, actionId: action.id, fieldPath: "action.retrieveSource.entityQualifiedName" }));
      }
      if (action.retrieveSource.kind === "association" && !action.retrieveSource.startVariableName.trim()) {
        issues.push(issue("MF_ACTION_REQUIRED_FIELD_MISSING", "AssociationRetrieveSource.startVariableName is required.", { objectId: object.id, actionId: action.id, fieldPath: "action.retrieveSource.startVariableName" }));
      }
    }
    if (action.kind === "restCall" && !action.request.urlExpression.text.trim()) {
      issues.push(issue("MF_ACTION_REQUIRED_FIELD_MISSING", "RestCallAction.request.urlExpression is required.", { objectId: object.id, actionId: action.id, fieldPath: "action.request.urlExpression" }));
    }
  }
  return issues;
}
