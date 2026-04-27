import { useMemo } from "react";
import { Input, Select, Switch, Tag, TextArea, Typography } from "@douyinfe/semi-ui";
import type { MicroflowAction, MicroflowActionActivity, MicroflowDataType, MicroflowExpression } from "../../schema";
import { EMPTY_MICROFLOW_METADATA_CATALOG, useMetadataStatus, useMicroflowMetadataCatalog } from "../../metadata";
import { buildVariableIndex } from "../../variables";
import { ExpressionEditor } from "../expression";
import type { MicroflowNodePatch, MicroflowPropertyPanelProps } from "../types";
import { getIssuesForField, getIssuesForObject } from "../utils";
import { expression, Field } from "../panel-shared";

const { Text, Title } = Typography;

type GenericActionField =
  | { key: string; label: string; control: "text"; required?: boolean }
  | { key: string; label: string; control: "textarea"; required?: boolean }
  | { key: string; label: string; control: "switch" }
  | { key: string; label: string; control: "select"; options: string[]; required?: boolean }
  | { key: string; label: string; control: "expression"; expectedType?: MicroflowDataType; required?: boolean };

const genericActionFieldDefinitions: Partial<Record<MicroflowAction["kind"], GenericActionField[]>> = {
  cast: [
    { key: "sourceObjectVariableName", label: "Source Object Variable", control: "text", required: true },
    { key: "targetEntityQualifiedName", label: "Target Entity", control: "text", required: true },
    { key: "outputVariableName", label: "Output Variable", control: "text", required: true }
  ],
  aggregateList: [
    { key: "listVariableName", label: "List Variable", control: "text", required: true },
    { key: "aggregateFunction", label: "Aggregate Function", control: "select", options: ["count", "sum", "average", "minimum", "maximum"], required: true },
    { key: "attributeQualifiedName", label: "Attribute", control: "text" },
    { key: "outputVariableName", label: "Output Variable", control: "text", required: true }
  ],
  createList: [
    { key: "entityQualifiedName", label: "Entity", control: "text", required: true },
    { key: "outputListVariableName", label: "Output List Variable", control: "text", required: true }
  ],
  changeList: [
    { key: "targetListVariableName", label: "Target List Variable", control: "text", required: true },
    { key: "operation", label: "Operation", control: "select", options: ["add", "remove", "clear", "replace"], required: true },
    { key: "objectVariableName", label: "Object Variable", control: "text" }
  ],
  listOperation: [
    { key: "leftListVariableName", label: "Left List Variable", control: "text", required: true },
    { key: "operation", label: "Operation", control: "select", options: ["union", "intersect", "subtract", "equals", "contains", "filter", "sort", "find", "head", "tail"], required: true },
    { key: "rightListVariableName", label: "Right List Variable", control: "text" },
    { key: "objectVariableName", label: "Object Variable", control: "text" },
    { key: "expression", label: "Expression", control: "expression" },
    { key: "outputVariableName", label: "Output Variable", control: "text", required: true }
  ],
  callJavaAction: [
    { key: "javaActionQualifiedName", label: "Java Action", control: "text", required: true }
  ],
  callJavaScriptAction: [
    { key: "javaScriptActionQualifiedName", label: "JavaScript Action", control: "text" }
  ],
  callNanoflow: [
    { key: "targetNanoflowId", label: "Nanoflow", control: "text" }
  ],
  closePage: [
    { key: "closeTarget", label: "Close Target", control: "select", options: ["current", "modal", "back"] },
    { key: "returnResult", label: "Return Result", control: "text" }
  ],
  downloadFile: [
    { key: "fileDocumentVariableName", label: "FileDocument Variable", control: "text", required: true },
    { key: "showFileInBrowser", label: "Show In Browser", control: "switch" }
  ],
  showHomePage: [
    { key: "pageTarget", label: "Page Target", control: "text" }
  ],
  showMessage: [
    { key: "messageType", label: "Message Type", control: "select", options: ["information", "warning", "error"] },
    { key: "blocking", label: "Blocking", control: "switch" },
    { key: "messageExpression", label: "Message Expression", control: "expression", expectedType: { kind: "string" }, required: true }
  ],
  showPage: [
    { key: "pageId", label: "Page", control: "text", required: true },
    { key: "openMode", label: "Open Mode", control: "select", options: ["current", "modal", "popup"] },
    { key: "title", label: "Title", control: "text" }
  ],
  validationFeedback: [
    { key: "targetObjectVariableName", label: "Target Object Variable", control: "text", required: true },
    { key: "targetMemberQualifiedName", label: "Target Member", control: "text", required: true },
    { key: "feedbackMessage", label: "Feedback Message", control: "textarea", required: true }
  ],
  synchronize: [
    { key: "scope", label: "Scope", control: "select", options: ["all", "entity", "object"] }
  ],
  webServiceCall: [
    { key: "webServiceQualifiedName", label: "Web Service", control: "text", required: true },
    { key: "operationName", label: "Operation", control: "text", required: true },
    { key: "requestMapping", label: "Request Mapping", control: "text" },
    { key: "responseMapping", label: "Response Mapping", control: "text" },
    { key: "outputVariableName", label: "Output Variable", control: "text" }
  ],
  importXml: [
    { key: "sourceType", label: "Source Type", control: "select", options: ["string", "fileDocument"] },
    { key: "sourceVariableName", label: "Source Variable", control: "text", required: true },
    { key: "importMappingQualifiedName", label: "Import Mapping", control: "text", required: true },
    { key: "outputVariableName", label: "Output Variable", control: "text" }
  ],
  exportXml: [
    { key: "sourceVariableName", label: "Source Variable", control: "text", required: true },
    { key: "exportMappingQualifiedName", label: "Export Mapping", control: "text", required: true },
    { key: "outputType", label: "Output Type", control: "select", options: ["string", "fileDocument"] },
    { key: "outputVariableName", label: "Output Variable", control: "text", required: true }
  ],
  callExternalAction: [
    { key: "consumedServiceQualifiedName", label: "Consumed Service", control: "text", required: true },
    { key: "externalActionName", label: "External Action", control: "text", required: true },
    { key: "returnVariableName", label: "Return Variable", control: "text" }
  ],
  restOperationCall: [
    { key: "consumedRestServiceQualifiedName", label: "Consumed REST Service", control: "text", required: true },
    { key: "operationName", label: "Operation", control: "text", required: true },
    { key: "outputVariableName", label: "Output Variable", control: "text" }
  ],
  generateDocument: [
    { key: "documentTemplateQualifiedName", label: "Document Template", control: "text", required: true },
    { key: "outputFileDocumentVariableName", label: "Output FileDocument Variable", control: "text", required: true }
  ],
  counter: [
    { key: "metricName", label: "Metric Name", control: "text", required: true },
    { key: "valueExpression", label: "Value Expression", control: "expression", expectedType: { kind: "integer" }, required: true }
  ],
  incrementCounter: [
    { key: "metricName", label: "Metric Name", control: "text", required: true }
  ],
  gauge: [
    { key: "metricName", label: "Metric Name", control: "text", required: true },
    { key: "valueExpression", label: "Value Expression", control: "expression", expectedType: { kind: "integer" }, required: true }
  ],
  mlModelCall: [
    { key: "modelMappingQualifiedName", label: "Model Mapping", control: "text", required: true },
    { key: "outputVariableName", label: "Output Variable", control: "text" }
  ],
  callWorkflow: [
    { key: "targetWorkflowId", label: "Target Workflow", control: "text", required: true },
    { key: "contextObjectVariableName", label: "Context Object Variable", control: "text" },
    { key: "outputWorkflowVariableName", label: "Output Workflow Variable", control: "text" }
  ],
  changeWorkflowState: [
    { key: "workflowInstanceVariableName", label: "Workflow Instance Variable", control: "text", required: true },
    { key: "operation", label: "Operation", control: "select", options: ["abort", "continue", "pause", "unpause", "restart", "retry"], required: true },
    { key: "reason", label: "Reason", control: "textarea" }
  ],
  completeUserTask: [
    { key: "userTaskVariableName", label: "User Task Variable", control: "text", required: true },
    { key: "outcome", label: "Outcome", control: "text" },
    { key: "validationResult", label: "Validation Result", control: "text" }
  ],
  retrieveWorkflowContext: [
    { key: "workflowInstanceVariableName", label: "Workflow Instance Variable", control: "text", required: true },
    { key: "contextEntityQualifiedName", label: "Context Entity", control: "text" },
    { key: "outputVariableName", label: "Output Variable", control: "text" }
  ],
  retrieveWorkflows: [
    { key: "contextObjectVariableName", label: "Context Object Variable", control: "text" },
    { key: "outputListVariableName", label: "Output List Variable", control: "text", required: true }
  ],
  notifyWorkflow: [
    { key: "workflowInstanceVariableName", label: "Workflow Instance Variable", control: "text", required: true },
    { key: "notificationName", label: "Notification Name", control: "text", required: true },
    { key: "payloadExpression", label: "Payload Expression", control: "expression" }
  ],
  deleteExternalObject: [
    { key: "externalObjectVariableName", label: "External Object Variable", control: "text", required: true },
    { key: "serviceOperationName", label: "Service Operation", control: "text", required: true }
  ],
  sendExternalObject: [
    { key: "externalObjectVariableName", label: "External Object Variable", control: "text", required: true },
    { key: "serviceOperationName", label: "Service Operation", control: "text", required: true },
    { key: "payloadMapping", label: "Payload Mapping", control: "textarea" }
  ]
};

const workflowInstanceOnlyFields: GenericActionField[] = [
  { key: "workflowInstanceVariableName", label: "Workflow Instance Variable", control: "text", required: true },
  { key: "targetVariableName", label: "Target Variable", control: "text" },
  { key: "outputVariableName", label: "Output Variable", control: "text" }
];

function getGenericActionFields(kind: MicroflowAction["kind"]): GenericActionField[] {
  if (genericActionFieldDefinitions[kind]) {
    return genericActionFieldDefinitions[kind] ?? [];
  }
  if (["applyJumpToOption", "generateJumpToOptions", "retrieveWorkflowActivityRecords", "showUserTaskPage", "showWorkflowAdminPage", "lockWorkflow", "unlockWorkflow"].includes(kind)) {
    return workflowInstanceOnlyFields;
  }
  return [];
}

function actionRecord(action: MicroflowAction): Record<string, unknown> {
  return action as unknown as Record<string, unknown>;
}

function isMissingRequiredValue(value: unknown): boolean {
  if (typeof value === "string") {
    return !value.trim();
  }
  if (value && typeof value === "object" && "raw" in value) {
    return !String((value as MicroflowExpression).raw ?? "").trim();
  }
  return value === undefined || value === null;
}

export function genericOutputSummary(action: MicroflowAction): string | undefined {
  const record = actionRecord(action);
  const candidates = [
    "outputVariableName",
    "outputListVariableName",
    "outputWorkflowVariableName",
    "returnVariableName",
    "outputFileDocumentVariableName"
  ];
  for (const key of candidates) {
    const value = record[key];
    if (typeof value === "string" && value.trim()) {
      return `${key}: ${value}`;
    }
  }
  const returnValue = record.returnValue;
  if (returnValue && typeof returnValue === "object") {
    const output = (returnValue as Record<string, unknown>).outputVariableName;
    if (typeof output === "string" && output.trim()) {
      return `returnValue.outputVariableName: ${output}`;
    }
  }
  return undefined;
}

export function GenericActionFields({
  schema,
  object,
  issues,
  readonly,
  onPatch
}: {
  schema: MicroflowPropertyPanelProps["schema"];
  object: MicroflowActionActivity;
  issues: ReturnType<typeof getIssuesForObject>;
  readonly?: boolean;
  onPatch: (patch: MicroflowNodePatch) => void;
}) {
  const catalog = useMicroflowMetadataCatalog();
  const { version: metadataVersion } = useMetadataStatus();
  const effectiveCatalog = catalog ?? EMPTY_MICROFLOW_METADATA_CATALOG;
  const variableIndex = useMemo(() => buildVariableIndex(schema, effectiveCatalog), [schema, effectiveCatalog, metadataVersion]);
  const action = object.action;
  const fields = getGenericActionFields(action.kind);
  const specializedKinds = ["retrieve", "createObject", "changeMembers", "commit", "delete", "rollback", "restCall", "logMessage", "callMicroflow", "createVariable", "changeVariable"];
  if (specializedKinds.includes(action.kind)) {
    return null;
  }
  if (fields.length === 0) {
    return (
      <>
        <Title heading={6} style={{ margin: "10px 0 0" }}>Unsupported action type</Title>
        <Text type="warning" size="small">当前 action kind 没有专用表单；以下 JSON 只读展示，属性面板不会崩溃。</Text>
        <TextArea value={JSON.stringify(action, null, 2)} autosize disabled />
      </>
    );
  }
  const patchAction = (key: string, value: unknown) => onPatch({ object: { ...object, action: { ...action, [key]: value } as MicroflowAction } });
  const record = actionRecord(action);
  return (
    <>
      <Title heading={6} style={{ margin: "10px 0 0" }}>{action.kind}</Title>
      {action.editor.availability !== "supported" ? <Tag color={action.editor.availability === "deprecated" ? "orange" : action.editor.availability === "beta" ? "blue" : "grey"}>{action.editor.availabilityReason ?? action.editor.availability}</Tag> : null}
      {fields.map(field => {
        const value = record[field.key];
        const fieldIssues = getIssuesForField(issues, `action.${field.key}`);
        if (field.control === "switch") {
          return (
            <Field key={field.key} label={field.label} issues={fieldIssues}>
              <Switch checked={Boolean(value)} disabled={readonly} onChange={next => patchAction(field.key, next)} />
            </Field>
          );
        }
        if (field.control === "select") {
          return (
            <Field key={field.key} label={field.label} issues={fieldIssues}>
              <Select
                value={typeof value === "string" ? value : field.options[0]}
                disabled={readonly}
                style={{ width: "100%" }}
                optionList={field.options.map(option => ({ label: option, value: option }))}
                onChange={next => patchAction(field.key, String(next))}
              />
              {field.required && isMissingRequiredValue(value) ? <Text type="warning" size="small">该字段为空，保存为待配置状态。</Text> : null}
            </Field>
          );
        }
        if (field.control === "textarea") {
          return (
            <Field key={field.key} label={field.label} issues={fieldIssues}>
              <TextArea value={typeof value === "string" ? value : ""} autosize disabled={readonly} onChange={next => patchAction(field.key, next)} />
              {field.required && isMissingRequiredValue(value) ? <Text type="warning" size="small">该字段为空，保存为待配置状态。</Text> : null}
            </Field>
          );
        }
        if (field.control === "expression") {
          const currentExpression = value && typeof value === "object" && "raw" in value ? value as MicroflowExpression : expression("", field.expectedType);
          return (
            <Field key={field.key} label={field.label} issues={fieldIssues}>
              <ExpressionEditor
                value={currentExpression}
                schema={schema}
                metadata={effectiveCatalog}
                variableIndex={variableIndex}
                objectId={object.id}
                actionId={action.id}
                fieldPath={`action.${field.key}`}
                expectedType={field.expectedType}
                required={field.required}
                readonly={readonly}
                onChange={next => patchAction(field.key, next)}
              />
              {field.required && isMissingRequiredValue(currentExpression) ? <Text type="warning" size="small">该表达式为空，保存为待配置状态。</Text> : null}
            </Field>
          );
        }
        return (
          <Field key={field.key} label={field.label} issues={fieldIssues}>
            <Input value={typeof value === "string" ? value : ""} disabled={readonly} onChange={next => patchAction(field.key, next)} />
            {field.required && isMissingRequiredValue(value) ? <Text type="warning" size="small">该字段为空，保存为待配置状态。</Text> : null}
          </Field>
        );
      })}
    </>
  );
}
