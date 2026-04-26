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
    if (action.kind === "createObject") {
      if (!action.entityQualifiedName.trim()) {
        issues.push(issue("MF_ACTION_REQUIRED_FIELD_MISSING", "CreateObjectAction.entityQualifiedName is required.", { objectId: object.id, actionId: action.id, fieldPath: "action.entityQualifiedName" }));
      }
      if (!action.outputVariableName.trim()) {
        issues.push(issue("MF_ACTION_REQUIRED_FIELD_MISSING", "CreateObjectAction.outputVariableName is required.", { objectId: object.id, actionId: action.id, fieldPath: "action.outputVariableName" }));
      }
    }
    if (action.kind === "changeMembers") {
      if (!action.changeVariableName.trim()) {
        issues.push(issue("MF_ACTION_REQUIRED_FIELD_MISSING", "ChangeMembersAction.changeVariableName is required.", { objectId: object.id, actionId: action.id, fieldPath: "action.changeVariableName" }));
      }
      if (action.memberChanges.length === 0) {
        issues.push(issue("MF_ACTION_REQUIRED_FIELD_MISSING", "ChangeMembersAction.memberChanges must include at least one member change.", { objectId: object.id, actionId: action.id, fieldPath: "action.memberChanges" }));
      }
    }
    if (action.kind === "commit" || action.kind === "delete" || action.kind === "rollback") {
      if (!action.objectOrListVariableName.trim()) {
        issues.push(issue("MF_ACTION_REQUIRED_FIELD_MISSING", `${action.officialType} requires objectOrListVariableName.`, { objectId: object.id, actionId: action.id, fieldPath: "action.objectOrListVariableName" }));
      }
    }
    if (action.kind === "callMicroflow") {
      if (!action.targetMicroflowId.trim()) {
        issues.push(issue("MF_ACTION_REQUIRED_FIELD_MISSING", "MicroflowCallAction.targetMicroflowId is required.", { objectId: object.id, actionId: action.id, fieldPath: "action.targetMicroflowId" }));
      }
      if (action.returnValue.storeResult && !action.returnValue.outputVariableName?.trim()) {
        issues.push(issue("MF_ACTION_REQUIRED_FIELD_MISSING", "MicroflowCallAction.returnValue.outputVariableName is required when storeResult=true.", { objectId: object.id, actionId: action.id, fieldPath: "action.returnValue.outputVariableName" }));
      }
    }
    if (action.kind === "restCall" && !(action.request.urlExpression.raw ?? action.request.urlExpression.text ?? "").trim()) {
      issues.push(issue("MF_ACTION_REQUIRED_FIELD_MISSING", "RestCallAction.request.urlExpression is required.", { objectId: object.id, actionId: action.id, fieldPath: "action.request.urlExpression" }));
    }
    if (action.kind === "restCall" && action.timeoutSeconds <= 0) {
      issues.push(issue("MF_ACTION_REQUIRED_FIELD_MISSING", "RestCallAction.timeoutSeconds must be greater than 0.", { objectId: object.id, actionId: action.id, fieldPath: "action.timeoutSeconds" }));
    }
    if (action.kind === "logMessage") {
      if (!action.logNodeName.trim()) {
        issues.push(issue("MF_ACTION_REQUIRED_FIELD_MISSING", "LogMessageAction.logNodeName is required.", { objectId: object.id, actionId: action.id, fieldPath: "action.logNodeName" }));
      }
      if (!action.template.text.trim()) {
        issues.push(issue("MF_ACTION_REQUIRED_FIELD_MISSING", "LogMessageAction.template.text is required.", { objectId: object.id, actionId: action.id, fieldPath: "action.template.text" }));
      }
    }
  }
  return issues;
}
