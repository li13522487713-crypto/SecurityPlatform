import type { MicroflowSchema, MicroflowValidationIssue } from "../schema/types";
import { flattenObjects, issue } from "./shared";
import type { MicroflowValidatorContext } from "./validator-types";

function textField(action: { [key: string]: unknown }, key: string): string {
  const value = action[key];
  return typeof value === "string" ? value.trim() : "";
}

function nestedText(action: { [key: string]: unknown }, key: string, nestedKey: string): string {
  const value = action[key];
  if (!value || typeof value !== "object") {
    return "";
  }
  const nested = (value as Record<string, unknown>)[nestedKey];
  return typeof nested === "string" ? nested.trim() : "";
}

function expressionText(value: unknown): string {
  if (!value || typeof value !== "object") {
    return "";
  }
  const record = value as Record<string, unknown>;
  return typeof record.raw === "string" ? record.raw.trim() : typeof record.text === "string" ? record.text.trim() : "";
}

function required(issueList: MicroflowValidationIssue[], code: string, message: string, objectId: string, actionId: string, fieldPath: string, ok: boolean): void {
  if (!ok) {
    issueList.push(issue(code, message, { objectId, actionId, fieldPath }));
  }
}

export function validateActions(schema: MicroflowSchema, _context: MicroflowValidatorContext): MicroflowValidationIssue[] {
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
    if (action.editor.availability === "nanoflowOnlyDisabled") {
      issues.push(issue("MF_ACTION_NANOFLOW_ONLY", `${action.officialType} is Nanoflow-only and cannot be used in a Microflow.`, { objectId: object.id, actionId: action.id, fieldPath: "action.kind" }));
    }
    if (action.editor.availability === "deprecated") {
      issues.push(issue("MF_ACTION_DEPRECATED", `${action.officialType} is deprecated.`, { objectId: object.id, actionId: action.id, fieldPath: "action.kind" }, "warning"));
    }
    if (action.kind === "cast") {
      required(issues, "MF_ACTION_REQUIRED_FIELD_MISSING", "CastAction.sourceObjectVariableName is required.", object.id, action.id, "action.sourceObjectVariableName", Boolean(textField(action, "sourceObjectVariableName")));
      required(issues, "MF_ACTION_REQUIRED_FIELD_MISSING", "CastAction.targetEntityQualifiedName is required.", object.id, action.id, "action.targetEntityQualifiedName", Boolean(textField(action, "targetEntityQualifiedName")));
      required(issues, "MF_ACTION_REQUIRED_FIELD_MISSING", "CastAction.outputVariableName is required.", object.id, action.id, "action.outputVariableName", Boolean(textField(action, "outputVariableName")));
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
    if (action.kind === "aggregateList") {
      required(issues, "MF_ACTION_REQUIRED_FIELD_MISSING", "AggregateListAction.listVariableName is required.", object.id, action.id, "action.listVariableName", Boolean(textField(action, "listVariableName")));
      required(issues, "MF_ACTION_REQUIRED_FIELD_MISSING", "AggregateListAction.outputVariableName is required.", object.id, action.id, "action.outputVariableName", Boolean(textField(action, "outputVariableName")));
      if (textField(action, "aggregateFunction") !== "count") {
        required(issues, "MF_ACTION_REQUIRED_FIELD_MISSING", "AggregateListAction.attributeQualifiedName is required for non-count aggregates.", object.id, action.id, "action.attributeQualifiedName", Boolean(textField(action, "attributeQualifiedName")));
      }
    }
    if (action.kind === "createList") {
      required(issues, "MF_ACTION_REQUIRED_FIELD_MISSING", "CreateListAction.entityQualifiedName is required.", object.id, action.id, "action.entityQualifiedName", Boolean(textField(action, "entityQualifiedName")));
      required(issues, "MF_ACTION_REQUIRED_FIELD_MISSING", "CreateListAction.outputListVariableName is required.", object.id, action.id, "action.outputListVariableName", Boolean(textField(action, "outputListVariableName")));
    }
    if (action.kind === "changeList") {
      required(issues, "MF_ACTION_REQUIRED_FIELD_MISSING", "ChangeListAction.targetListVariableName is required.", object.id, action.id, "action.targetListVariableName", Boolean(textField(action, "targetListVariableName")));
    }
    if (action.kind === "listOperation") {
      required(issues, "MF_ACTION_REQUIRED_FIELD_MISSING", "ListOperationAction.operation is required.", object.id, action.id, "action.operation", Boolean(textField(action, "operation")));
      required(issues, "MF_ACTION_REQUIRED_FIELD_MISSING", "ListOperationAction.outputVariableName is required.", object.id, action.id, "action.outputVariableName", Boolean(textField(action, "outputVariableName")));
    }
    if (action.kind === "callJavaAction") {
      required(issues, "MF_ACTION_REQUIRED_FIELD_MISSING", "JavaActionCallAction.javaActionQualifiedName is required.", object.id, action.id, "action.javaActionQualifiedName", Boolean(textField(action, "javaActionQualifiedName")));
    }
    if (action.kind === "showPage") {
      required(issues, "MF_ACTION_REQUIRED_FIELD_MISSING", "ShowPageAction.pageId is required.", object.id, action.id, "action.pageId", Boolean(textField(action, "pageId")));
    }
    if (action.kind === "showMessage") {
      required(issues, "MF_ACTION_REQUIRED_FIELD_MISSING", "ShowMessageAction.messageExpression is required.", object.id, action.id, "action.messageExpression", Boolean(expressionText(action.messageExpression)));
    }
    if (action.kind === "validationFeedback") {
      required(issues, "MF_ACTION_REQUIRED_FIELD_MISSING", "ValidationFeedbackAction.targetObjectVariableName is required.", object.id, action.id, "action.targetObjectVariableName", Boolean(textField(action, "targetObjectVariableName")));
      required(issues, "MF_ACTION_REQUIRED_FIELD_MISSING", "ValidationFeedbackAction.targetMemberQualifiedName is required.", object.id, action.id, "action.targetMemberQualifiedName", Boolean(textField(action, "targetMemberQualifiedName")));
      required(issues, "MF_ACTION_REQUIRED_FIELD_MISSING", "ValidationFeedbackAction.feedbackMessage is required.", object.id, action.id, "action.feedbackMessage", Boolean(textField(action, "feedbackMessage")));
    }
    if (action.kind === "downloadFile") {
      required(issues, "MF_ACTION_REQUIRED_FIELD_MISSING", "DownloadFileAction.fileDocumentVariableName is required.", object.id, action.id, "action.fileDocumentVariableName", Boolean(textField(action, "fileDocumentVariableName")));
    }
    if (action.kind === "webServiceCall") {
      required(issues, "MF_ACTION_REQUIRED_FIELD_MISSING", "WebServiceCallAction.webServiceQualifiedName is required.", object.id, action.id, "action.webServiceQualifiedName", Boolean(textField(action, "webServiceQualifiedName")));
      required(issues, "MF_ACTION_REQUIRED_FIELD_MISSING", "WebServiceCallAction.operationName is required.", object.id, action.id, "action.operationName", Boolean(textField(action, "operationName")));
    }
    if (action.kind === "importXml") {
      required(issues, "MF_ACTION_REQUIRED_FIELD_MISSING", "ImportXmlAction.sourceVariableName is required.", object.id, action.id, "action.sourceVariableName", Boolean(textField(action, "sourceVariableName")));
      required(issues, "MF_ACTION_REQUIRED_FIELD_MISSING", "ImportXmlAction.importMappingQualifiedName is required.", object.id, action.id, "action.importMappingQualifiedName", Boolean(textField(action, "importMappingQualifiedName")));
    }
    if (action.kind === "exportXml") {
      required(issues, "MF_ACTION_REQUIRED_FIELD_MISSING", "ExportXmlAction.sourceVariableName is required.", object.id, action.id, "action.sourceVariableName", Boolean(textField(action, "sourceVariableName")));
      required(issues, "MF_ACTION_REQUIRED_FIELD_MISSING", "ExportXmlAction.exportMappingQualifiedName is required.", object.id, action.id, "action.exportMappingQualifiedName", Boolean(textField(action, "exportMappingQualifiedName")));
      required(issues, "MF_ACTION_REQUIRED_FIELD_MISSING", "ExportXmlAction.outputVariableName is required.", object.id, action.id, "action.outputVariableName", Boolean(textField(action, "outputVariableName")));
    }
    if (action.kind === "callExternalAction") {
      required(issues, "MF_ACTION_REQUIRED_FIELD_MISSING", "CallExternalAction.consumedServiceQualifiedName is required.", object.id, action.id, "action.consumedServiceQualifiedName", Boolean(textField(action, "consumedServiceQualifiedName")));
      required(issues, "MF_ACTION_REQUIRED_FIELD_MISSING", "CallExternalAction.externalActionName is required.", object.id, action.id, "action.externalActionName", Boolean(textField(action, "externalActionName")));
    }
    if (action.kind === "restOperationCall") {
      required(issues, "MF_ACTION_REQUIRED_FIELD_MISSING", "RestOperationCall.consumedRestServiceQualifiedName is required.", object.id, action.id, "action.consumedRestServiceQualifiedName", Boolean(textField(action, "consumedRestServiceQualifiedName")));
      required(issues, "MF_ACTION_REQUIRED_FIELD_MISSING", "RestOperationCall.operationName is required.", object.id, action.id, "action.operationName", Boolean(textField(action, "operationName")));
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
    if (action.kind === "generateDocument") {
      required(issues, "MF_ACTION_REQUIRED_FIELD_MISSING", "GenerateDocumentAction.documentTemplateQualifiedName is required.", object.id, action.id, "action.documentTemplateQualifiedName", Boolean(textField(action, "documentTemplateQualifiedName")));
      required(issues, "MF_ACTION_REQUIRED_FIELD_MISSING", "GenerateDocumentAction.outputFileDocumentVariableName is required.", object.id, action.id, "action.outputFileDocumentVariableName", Boolean(textField(action, "outputFileDocumentVariableName")));
    }
    if (action.kind === "counter" || action.kind === "incrementCounter" || action.kind === "gauge") {
      required(issues, "MF_ACTION_REQUIRED_FIELD_MISSING", `${action.officialType}.metricName is required.`, object.id, action.id, "action.metricName", Boolean(textField(action, "metricName")));
      if (action.kind !== "incrementCounter") {
        required(issues, "MF_ACTION_REQUIRED_FIELD_MISSING", `${action.officialType}.valueExpression is required.`, object.id, action.id, "action.valueExpression", Boolean(expressionText(action.valueExpression)));
      }
    }
    if (action.kind === "mlModelCall") {
      required(issues, "MF_ACTION_REQUIRED_FIELD_MISSING", "MLModelCallAction.modelMappingQualifiedName is required.", object.id, action.id, "action.modelMappingQualifiedName", Boolean(textField(action, "modelMappingQualifiedName")));
    }
    if (action.kind === "callWorkflow") {
      required(issues, "MF_ACTION_REQUIRED_FIELD_MISSING", "CallWorkflowAction.targetWorkflowId is required.", object.id, action.id, "action.targetWorkflowId", Boolean(textField(action, "targetWorkflowId")));
    }
    if (action.kind === "changeWorkflowState") {
      required(issues, "MF_ACTION_REQUIRED_FIELD_MISSING", "ChangeWorkflowStateAction.workflowInstanceVariableName is required.", object.id, action.id, "action.workflowInstanceVariableName", Boolean(textField(action, "workflowInstanceVariableName")));
      required(issues, "MF_ACTION_REQUIRED_FIELD_MISSING", "ChangeWorkflowStateAction.operation is required.", object.id, action.id, "action.operation", Boolean(textField(action, "operation")));
    }
    if (["applyJumpToOption", "generateJumpToOptions", "retrieveWorkflowActivityRecords", "retrieveWorkflowContext", "showWorkflowAdminPage", "lockWorkflow", "unlockWorkflow", "notifyWorkflow"].includes(action.kind)) {
      required(issues, "MF_ACTION_REQUIRED_FIELD_MISSING", `${action.officialType}.workflowInstanceVariableName is required.`, object.id, action.id, "action.workflowInstanceVariableName", Boolean(textField(action, "workflowInstanceVariableName")));
    }
    if (action.kind === "completeUserTask" || action.kind === "showUserTaskPage") {
      required(issues, "MF_ACTION_REQUIRED_FIELD_MISSING", `${action.officialType}.userTaskVariableName is required.`, object.id, action.id, "action.userTaskVariableName", Boolean(textField(action, "userTaskVariableName")));
    }
    if (action.kind === "retrieveWorkflows") {
      required(issues, "MF_ACTION_REQUIRED_FIELD_MISSING", "RetrieveWorkflowsAction.outputListVariableName is required.", object.id, action.id, "action.outputListVariableName", Boolean(textField(action, "outputListVariableName")));
    }
    if (action.kind === "deleteExternalObject" || action.kind === "sendExternalObject") {
      required(issues, "MF_ACTION_REQUIRED_FIELD_MISSING", `${action.officialType}.externalObjectVariableName is required.`, object.id, action.id, "action.externalObjectVariableName", Boolean(textField(action, "externalObjectVariableName")));
      required(issues, "MF_ACTION_REQUIRED_FIELD_MISSING", `${action.officialType}.serviceOperationName is required.`, object.id, action.id, "action.serviceOperationName", Boolean(textField(action, "serviceOperationName")));
    }
  }
  return issues;
}
