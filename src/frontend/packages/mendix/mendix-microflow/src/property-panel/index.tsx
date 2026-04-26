import { useEffect, useMemo, useState, type ReactNode } from "react";
import { Empty, Input, InputNumber, Select, Space, Switch, Tag, TextArea, Typography, Button, Tabs } from "@douyinfe/semi-ui";
import { IconCopy, IconDelete, IconClose } from "@douyinfe/semi-icons";
import type {
  MicroflowAction,
  MicroflowActionActivity,
  MicroflowCaseValue,
  MicroflowDataType,
  MicroflowExpression,
  MicroflowFlow,
  MicroflowIterableListLoopSource,
  MicroflowObject,
  MicroflowParameter,
  MicroflowVariableSymbol,
  MicroflowWhileLoopCondition,
  MicroflowSequenceFlow
} from "../schema";
import { getCaseEditorKind, getCaseOptionsForSource, caseValueKey } from "../flowgram/adapters/flowgram-case-options";
import { defaultMicroflowActionRegistry, defaultMicroflowEdgeRegistry, defaultMicroflowObjectNodeRegistry } from "../node-registry";
import type { MicroflowPropertyTabKey } from "../schema/types";
import { collectFlowsRecursive, findObjectWithCollection } from "../schema/utils/object-utils";
import { getAssociationByQualifiedName, getAttributeByQualifiedName, getMicroflowById, getSpecializations, useMicroflowMetadata } from "../metadata";
import { buildVariableIndex, getObjectEntityQualifiedName, resolveVariableReferenceFromIndex } from "../variables";
import { FieldError, ValidationIssueList } from "./common";
import { ExpressionEditor } from "./expression";
import { AssociationSelector, AttributeSelector, DataTypeSelector, EntitySelector, EnumerationSelector, MicroflowSelector, VariableSelector } from "./selectors";
import type { MicroflowEdgePatch, MicroflowNodeFormRegistry, MicroflowNodePatch, MicroflowPropertyPanelProps } from "./types";
import { countIssuesBySeverity, getIssuesForField, getIssuesForFlow, getIssuesForObject, updateParameter } from "./utils";

export * from "./controls";
export * from "./types";
export * from "./common";
export * from "./utils";
export * from "./selectors";
export * from "./expression";

export function getMicroflowNodeFormKey(object: MicroflowObject): string {
  return object.kind === "actionActivity" ? `activity:${object.action.kind}` : object.kind;
}

export const microflowNodeFormRegistry: MicroflowNodeFormRegistry = {};

export function buildVariablesForPropertyPanel(schema: { variables?: Record<string, Record<string, MicroflowVariableSymbol>> }): MicroflowVariableSymbol[] {
  return Object.values(schema.variables ?? {}).flatMap(group => Object.values(group));
}

const { Text, Title } = Typography;

function expression(raw = "", inferredType?: MicroflowDataType): MicroflowExpression {
  return {
    raw,
    inferredType,
    references: { variables: [], entities: [], attributes: [], associations: [], enumerations: [], functions: [] },
    diagnostics: []
  };
}

function objectTitle(object: MicroflowObject): string {
  if (object.kind === "actionActivity") {
    return `${object.caption} (${object.action.kind})`;
  }
  return object.caption ?? object.kind;
}

function actionPatch(action: MicroflowAction, patch: Partial<MicroflowAction>): MicroflowAction {
  return { ...action, ...patch } as MicroflowAction;
}

function updateAction(activity: MicroflowActionActivity, patch: Partial<MicroflowAction>): MicroflowActionActivity {
  return { ...activity, action: actionPatch(activity.action, patch) };
}

const tabLabels: Record<MicroflowPropertyTabKey, string> = {
  properties: "Properties",
  documentation: "Documentation",
  errorHandling: "Error Handling",
  output: "Output",
  advanced: "Advanced",
};

function issuesFor(props: MicroflowPropertyPanelProps, objectId?: string, flowId?: string, actionId?: string) {
  if (flowId) {
    return getIssuesForFlow(props.validationIssues, flowId);
  }
  if (objectId) {
    return getIssuesForObject(props.validationIssues, objectId, actionId);
  }
  return [];
}

function Header({ props, title, subtitle, onDelete, onDuplicate }: {
  props: MicroflowPropertyPanelProps;
  title: string;
  subtitle: string;
  onDelete?: () => void;
  onDuplicate?: () => void;
}) {
  const counts = countIssuesBySeverity(props.selectedFlow
    ? getIssuesForFlow(props.validationIssues, props.selectedFlow.id)
    : props.selectedObject
      ? getIssuesForObject(props.validationIssues, props.selectedObject.id, props.selectedObject.kind === "actionActivity" ? props.selectedObject.action.id : undefined)
      : []);
  const runtimeStatus = props.selectedFlow
    ? runtimeStatusForFlow(props.traceFrames ?? [], props.selectedFlow.id)
    : props.selectedObject
      ? runtimeStatusForObject(props.traceFrames ?? [], props.selectedObject.id)
      : undefined;
  return (
    <div style={{ padding: 14, borderBottom: "1px solid var(--semi-color-border, #e5e6eb)", background: "var(--semi-color-bg-2, #fff)" }}>
      <Space align="start" style={{ width: "100%", justifyContent: "space-between" }}>
        <div style={{ minWidth: 0 }}>
          <Title heading={6} style={{ margin: 0 }}>{title}</Title>
          <Text size="small" type="tertiary">{subtitle}</Text>
          {runtimeStatus ? (
            <>
              <br />
              <Text size="small" type={runtimeStatus.status === "failed" ? "danger" : "tertiary"}>
                最近执行：{runtimeStatus.status} · {runtimeStatus.durationMs ?? 0}ms{runtimeStatus.errorMessage ? ` · ${runtimeStatus.errorMessage}` : ""}
              </Text>
            </>
          ) : null}
        </div>
        <Space>
          {runtimeStatus ? <Tag color={runtimeStatus.status === "failed" ? "red" : runtimeStatus.status === "success" || runtimeStatus.status === "visited" ? "green" : "grey"}>{runtimeStatus.status}</Tag> : <Tag color="grey">notRun</Tag>}
          {counts.errors > 0 ? <Tag color="red">{counts.errors} error</Tag> : null}
          {counts.warnings > 0 ? <Tag color="orange">{counts.warnings} warning</Tag> : null}
          {onDuplicate ? <Button icon={<IconCopy />} theme="borderless" onClick={onDuplicate} disabled={props.readonly} /> : null}
          {onDelete ? <Button icon={<IconDelete />} theme="borderless" type="danger" onClick={onDelete} disabled={props.readonly} /> : null}
          <Button icon={<IconClose />} theme="borderless" onClick={props.onClose} />
        </Space>
      </Space>
    </div>
  );
}

function runtimeStatusForObject(frames: MicroflowPropertyPanelProps["traceFrames"], objectId: string) {
  const frame = [...(frames ?? [])].reverse().find(item => item.objectId === objectId);
  if (!frame) {
    return undefined;
  }
  return { status: frame.status, durationMs: frame.durationMs, errorMessage: frame.error?.message };
}

function runtimeStatusForFlow(frames: MicroflowPropertyPanelProps["traceFrames"], flowId: string) {
  const frame = [...(frames ?? [])].reverse().find(item => item.incomingFlowId === flowId || item.outgoingFlowId === flowId);
  if (!frame) {
    return undefined;
  }
  return {
    status: frame.outgoingFlowId === flowId || frame.incomingFlowId === flowId ? "visited" : "notRun",
    durationMs: frame.durationMs,
    errorMessage: frame.error?.flowId === flowId ? frame.error.message : undefined,
  };
}

function Field({ label, children, issues }: { label: string; children: ReactNode; issues?: ReturnType<typeof getIssuesForField> }) {
  return (
    <label style={{ display: "grid", gap: 6 }}>
      <Text size="small" strong>{label}</Text>
      {children}
      <FieldError issues={issues} />
    </label>
  );
}

function getObjectTabs(object: MicroflowObject): MicroflowPropertyTabKey[] {
  if (object.kind === "actionActivity") {
    return defaultMicroflowActionRegistry.find(item => item.kind === object.action.kind)?.propertyTabs ?? ["properties", "documentation", "errorHandling", "output", "advanced"];
  }
  return defaultMicroflowObjectNodeRegistry.find(item => item.objectKind === object.kind)?.propertyTabs ?? ["properties", "documentation"];
}

function getFlowEdgeKind(flow: MicroflowFlow): "sequence" | "decisionCondition" | "objectTypeCondition" | "errorHandler" | "annotation" {
  if (flow.kind === "annotation") {
    return "annotation";
  }
  if (flow.isErrorHandler) {
    return "errorHandler";
  }
  return flow.editor.edgeKind;
}

function getFlowTabs(flow: MicroflowFlow): MicroflowPropertyTabKey[] {
  const edgeKind = getFlowEdgeKind(flow);
  return (defaultMicroflowEdgeRegistry.find(item => item.edgeKind === edgeKind)?.propertyTabs ?? ["properties", "documentation"]) as MicroflowPropertyTabKey[];
}

function PropertyTabs({
  tabs,
  activeKey,
  onChange,
}: {
  tabs: MicroflowPropertyTabKey[];
  activeKey: MicroflowPropertyTabKey;
  onChange: (key: MicroflowPropertyTabKey) => void;
}) {
  return (
    <Tabs
      activeKey={activeKey}
      size="small"
      style={{ padding: "0 14px", borderBottom: "1px solid var(--semi-color-border, #e5e6eb)" }}
      onChange={key => onChange(key as MicroflowPropertyTabKey)}
    >
      {tabs.map(tab => <Tabs.TabPane key={tab} itemKey={tab} tab={tabLabels[tab]} />)}
    </Tabs>
  );
}

function updateObjectDocumentation(object: MicroflowObject, documentation: string): MicroflowObject {
  return { ...object, documentation } as MicroflowObject;
}

function updateObjectAdvanced(object: MicroflowObject, patch: Record<string, unknown>): MicroflowObject {
  return {
    ...object,
    editor: {
      ...object.editor,
      advanced: {
        ...((object.editor as unknown as { advanced?: Record<string, unknown> }).advanced ?? {}),
        ...patch,
      },
    },
  } as MicroflowObject;
}

function dataTypeLabel(dataType?: MicroflowDataType): string {
  if (!dataType) {
    return "unknown";
  }
  if (dataType.kind === "enumeration") {
    return `enumeration:${dataType.enumerationQualifiedName}`;
  }
  if (dataType.kind === "object") {
    return `object:${dataType.entityQualifiedName}`;
  }
  if (dataType.kind === "list") {
    return `list:${dataType.elementType.kind}`;
  }
  return dataType.kind;
}

function dataTypeFromKey(kind: string): MicroflowDataType {
  if (kind === "boolean" || kind === "integer" || kind === "long" || kind === "decimal" || kind === "dateTime" || kind === "string") {
    return { kind };
  }
  return { kind: "string" };
}

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

function objectName(schema: MicroflowPropertyPanelProps["schema"], objectId: string): string {
  const object = findObjectWithCollection(schema, objectId)?.object;
  return object?.caption ?? object?.kind ?? objectId;
}

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

function genericOutputSummary(action: MicroflowAction): string | undefined {
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

function GenericActionFields({
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
  const catalog = useMicroflowMetadata();
  const variableIndex = useMemo(() => buildVariableIndex(schema, catalog), [schema, catalog.version]);
  const action = object.action;
  const fields = getGenericActionFields(action.kind);
  if (fields.length === 0 || ["retrieve", "createObject", "changeMembers", "commit", "delete", "rollback", "restCall", "logMessage", "callMicroflow", "createVariable", "changeVariable"].includes(action.kind)) {
    return null;
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
            </Field>
          );
        }
        if (field.control === "textarea") {
          return (
            <Field key={field.key} label={field.label} issues={fieldIssues}>
              <TextArea value={typeof value === "string" ? value : ""} autosize disabled={readonly} onChange={next => patchAction(field.key, next)} />
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
                metadata={catalog}
                variableIndex={variableIndex}
                objectId={object.id}
                actionId={action.id}
                fieldPath={`action.${field.key}`}
                expectedType={field.expectedType}
                required={field.required}
                readonly={readonly}
                onChange={next => patchAction(field.key, next)}
              />
            </Field>
          );
        }
        return (
          <Field key={field.key} label={field.label} issues={fieldIssues}>
            <Input value={typeof value === "string" ? value : ""} disabled={readonly} onChange={next => patchAction(field.key, next)} />
          </Field>
        );
      })}
    </>
  );
}

function ActionActivityFields({
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
  const action = object.action;
  const catalog = useMicroflowMetadata();
  const variableIndex = useMemo(() => buildVariableIndex(schema, catalog), [schema, catalog.version]);
  const variableEntity = (variableName?: string) => variableName
    ? getObjectEntityQualifiedName(resolveVariableReferenceFromIndex(schema, variableIndex, { objectId: object.id }, variableName)?.dataType)
    : undefined;
  const associationSource = action.kind === "retrieve" && action.retrieveSource.kind === "association"
    ? variableEntity(action.retrieveSource.startVariableName) ?? getAssociationByQualifiedName(catalog, action.retrieveSource.associationQualifiedName ?? undefined)?.sourceEntityQualifiedName
    : undefined;
  const [associationStartEntity, setAssociationStartEntity] = useState<string | undefined>(associationSource);
  const firstMemberEntity = action.kind === "changeMembers" && action.memberChanges[0]?.memberQualifiedName
    ? action.memberChanges[0].memberQualifiedName.split(".").slice(0, -1).join(".")
    : undefined;
  const inferredMemberEntity = action.kind === "changeMembers" ? variableEntity(action.changeVariableName) : undefined;
  const [memberEntity, setMemberEntity] = useState<string | undefined>(inferredMemberEntity ?? firstMemberEntity ?? "Sales.Order");
  const selectedMicroflow = action.kind === "callMicroflow" ? getMicroflowById(catalog, action.targetMicroflowId) : undefined;
  const patchObject = (next: MicroflowActionActivity) => onPatch({ object: next });
  return (
    <Space vertical align="start" style={{ width: "100%" }}>
      <Field label="Caption">
        <Input value={object.caption} disabled={readonly} onChange={caption => patchObject({ ...object, caption })} />
      </Field>
      <Field label="Auto Generate Caption">
        <Switch checked={object.autoGenerateCaption} disabled={readonly} onChange={autoGenerateCaption => patchObject({ ...object, autoGenerateCaption })} />
      </Field>
      <Field label="Background Color">
        <Select
          value={object.backgroundColor}
          disabled={readonly}
          style={{ width: "100%" }}
          onChange={value => patchObject({ ...object, backgroundColor: String(value) as MicroflowActionActivity["backgroundColor"] })}
          optionList={["default", "blue", "green", "orange", "red", "purple", "gray"].map(value => ({ label: value, value }))}
        />
      </Field>
      <Field label="Error Handling">
        <Select
          value={action.errorHandlingType}
          disabled={readonly || action.kind === "rollback"}
          style={{ width: "100%" }}
          onChange={value => patchObject(updateAction(object, { errorHandlingType: String(value) as MicroflowAction["errorHandlingType"] }))}
          optionList={["rollback", "customWithRollback", "customWithoutRollback", "continue"].map(value => ({ label: value, value }))}
        />
      </Field>

      {action.kind === "retrieve" ? (
        <>
          <Title heading={6} style={{ margin: "10px 0 0" }}>Retrieve Source</Title>
          <Field label="Output Variable">
            <Input value={action.outputVariableName} disabled={readonly} onChange={outputVariableName => patchObject(updateAction(object, { outputVariableName }))} />
          </Field>
          <Field label="Source Type">
            <Select
              value={action.retrieveSource.kind}
              disabled={readonly}
              style={{ width: "100%" }}
              onChange={value => {
                const source = String(value) === "association"
                  ? { kind: "association" as const, officialType: "Microflows$AssociationRetrieveSource" as const, associationQualifiedName: null, startVariableName: "" }
                  : {
                      kind: "database" as const,
                      officialType: "Microflows$DatabaseRetrieveSource" as const,
                      entityQualifiedName: null,
                      xPathConstraint: null,
                      sortItemList: { items: [] },
                      range: { kind: "all" as const, officialType: "Microflows$ConstantRange" as const, value: "all" as const }
                    };
                patchObject(updateAction(object, { retrieveSource: source }));
              }}
              optionList={[{ label: "From Database", value: "database" }, { label: "By Association", value: "association" }]}
            />
          </Field>
          {action.retrieveSource.kind === "database" ? (
            <>
              <Field label="Entity">
                <EntitySelector
                  value={action.retrieveSource.entityQualifiedName ?? ""}
                  disabled={readonly}
                  onlyPersistable
                  onChange={entityQualifiedName => patchObject(updateAction(object, { retrieveSource: { ...action.retrieveSource, entityQualifiedName: entityQualifiedName ?? null } }))}
                />
                <FieldError issues={getIssuesForField(issues, "action.retrieveSource.entityQualifiedName")} />
              </Field>
              <Field label="XPath Constraint">
                <ExpressionEditor
                  value={action.retrieveSource.xPathConstraint}
                  schema={schema}
                  metadata={catalog}
                  variableIndex={variableIndex}
                  objectId={object.id}
                  actionId={action.id}
                  fieldPath="action.retrieveSource.xPathConstraint"
                  expectedType={{ kind: "boolean" }}
                  readonly={readonly}
                  onChange={xPathConstraint => patchObject(updateAction(object, { retrieveSource: { ...action.retrieveSource, xPathConstraint } }))}
                />
              </Field>
              <Field label="Range">
                <Select
                  value={action.retrieveSource.range.kind}
                  disabled={readonly}
                  style={{ width: "100%" }}
                  onChange={value => {
                    const kind = String(value);
                    const range = kind === "custom"
                      ? { kind: "custom" as const, officialType: "Microflows$CustomRange" as const, limitExpression: expression("10", { kind: "integer" }), offsetExpression: expression("0", { kind: "integer" }) }
                      : { kind: kind as "all" | "first", officialType: "Microflows$ConstantRange" as const, value: kind as "all" | "first" };
                    patchObject(updateAction(object, { retrieveSource: { ...action.retrieveSource, range } }));
                  }}
                  optionList={[{ label: "All", value: "all" }, { label: "First", value: "first" }, { label: "Custom", value: "custom" }]}
                />
              </Field>
              <Field label="Sort Items (one per line: Entity.Attribute asc)">
                <Space vertical align="start" spacing={6} style={{ width: "100%" }}>
                  {action.retrieveSource.sortItemList.items.map((item, index) => (
                    <div key={`${item.attributeQualifiedName}-${index}`} style={{ display: "grid", gridTemplateColumns: "minmax(0, 1fr) 92px auto", gap: 6, width: "100%" }}>
                      <AttributeSelector
                        entityQualifiedName={action.retrieveSource.kind === "database" ? action.retrieveSource.entityQualifiedName ?? undefined : undefined}
                        value={item.attributeQualifiedName}
                        disabled={readonly}
                        onChange={attributeQualifiedName => patchObject(updateAction(object, {
                          retrieveSource: {
                            ...action.retrieveSource,
                            sortItemList: {
                              items: action.retrieveSource.sortItemList.items.map((row, rowIndex) => rowIndex === index ? { ...row, attributeQualifiedName: attributeQualifiedName ?? "" } : row),
                            },
                          },
                        }))}
                      />
                      <Select
                        value={item.direction}
                        disabled={readonly}
                        optionList={[{ label: "asc", value: "asc" }, { label: "desc", value: "desc" }]}
                        onChange={direction => patchObject(updateAction(object, {
                          retrieveSource: {
                            ...action.retrieveSource,
                            sortItemList: {
                              items: action.retrieveSource.sortItemList.items.map((row, rowIndex) => rowIndex === index ? { ...row, direction: String(direction) === "desc" ? "desc" : "asc" } : row),
                            },
                          },
                        }))}
                      />
                      <Button disabled={readonly} type="danger" theme="borderless" onClick={() => patchObject(updateAction(object, {
                        retrieveSource: {
                          ...action.retrieveSource,
                          sortItemList: { items: action.retrieveSource.sortItemList.items.filter((_, rowIndex) => rowIndex !== index) },
                        },
                      }))}>Delete</Button>
                    </div>
                  ))}
                  <Button disabled={readonly || !action.retrieveSource.entityQualifiedName} onClick={() => patchObject(updateAction(object, {
                    retrieveSource: {
                      ...action.retrieveSource,
                      sortItemList: { items: [...action.retrieveSource.sortItemList.items, { attributeQualifiedName: "", direction: "asc" }] },
                    },
                  }))}>Add sort item</Button>
                </Space>
              </Field>
            </>
          ) : (
            <>
              <Field label="Start Variable">
                <VariableSelector
                  schema={schema}
                  objectId={object.id}
                  fieldPath="action.retrieveSource.startVariableName"
                  allowedTypeKinds={["object"]}
                  value={action.retrieveSource.startVariableName}
                  disabled={readonly}
                  onChange={startVariableName => {
                    const nextStartVariableName = startVariableName ?? "";
                    setAssociationStartEntity(variableEntity(nextStartVariableName));
                    patchObject(updateAction(object, { retrieveSource: { ...action.retrieveSource, startVariableName: nextStartVariableName, associationQualifiedName: null } }));
                  }}
                />
                <FieldError issues={getIssuesForField(issues, "action.retrieveSource.startVariableName")} />
              </Field>
              <Field label="Association Start Entity">
                <EntitySelector
                  value={associationStartEntity}
                  disabled={readonly}
                  onChange={entityQualifiedName => {
                    setAssociationStartEntity(entityQualifiedName);
                    patchObject(updateAction(object, { retrieveSource: { ...action.retrieveSource, associationQualifiedName: null } }));
                  }}
                />
              </Field>
              <Field label="Association">
                <AssociationSelector
                  startEntityQualifiedName={associationStartEntity}
                  value={action.retrieveSource.associationQualifiedName ?? ""}
                  disabled={readonly}
                  onChange={associationQualifiedName => patchObject(updateAction(object, { retrieveSource: { ...action.retrieveSource, associationQualifiedName: associationQualifiedName ?? null } }))}
                />
                <FieldError issues={getIssuesForField(issues, "action.retrieveSource.associationQualifiedName")} />
              </Field>
            </>
          )}
        </>
      ) : null}

      {action.kind === "commit" || action.kind === "delete" || action.kind === "rollback" ? (
        <>
          <Title heading={6} style={{ margin: "10px 0 0" }}>{action.kind}</Title>
          <Field label="Object/List Variable">
            <VariableSelector
              schema={schema}
              objectId={object.id}
              fieldPath="action.objectOrListVariableName"
              allowedTypeKinds={["object", "list"]}
              value={action.objectOrListVariableName}
              disabled={readonly}
              onChange={objectOrListVariableName => patchObject(updateAction(object, { objectOrListVariableName: objectOrListVariableName ?? "" }))}
            />
            <FieldError issues={getIssuesForField(issues, "action.objectOrListVariableName")} />
          </Field>
          {"withEvents" in action ? (
            <Field label="With Events">
              <Switch checked={action.withEvents} disabled={readonly} onChange={withEvents => patchObject(updateAction(object, { withEvents }))} />
            </Field>
          ) : null}
          {"refreshInClient" in action ? (
            <Field label="Refresh In Client">
              <Switch checked={action.refreshInClient} disabled={readonly} onChange={refreshInClient => patchObject(updateAction(object, { refreshInClient }))} />
            </Field>
          ) : null}
        </>
      ) : null}

      {action.kind === "createObject" || action.kind === "changeMembers" ? (
        <>
          <Title heading={6} style={{ margin: "10px 0 0" }}>{action.kind === "createObject" ? "Create Object" : "Change Members"}</Title>
          {action.kind === "createObject" ? (
            <>
              <Field label="Entity">
                <EntitySelector value={action.entityQualifiedName} disabled={readonly} onChange={entityQualifiedName => patchObject(updateAction(object, { entityQualifiedName: entityQualifiedName ?? "" }))} />
                <FieldError issues={getIssuesForField(issues, "action.entityQualifiedName")} />
              </Field>
              <Field label="Output Variable">
                <Input value={action.outputVariableName} disabled={readonly} onChange={outputVariableName => patchObject(updateAction(object, { outputVariableName }))} />
              </Field>
            </>
          ) : (
            <Field label="Change Variable">
              <VariableSelector
                schema={schema}
                objectId={object.id}
                fieldPath="action.changeVariableName"
                allowedTypeKinds={["object"]}
                value={action.changeVariableName}
                disabled={readonly}
                onChange={changeVariableName => {
                  const nextChangeVariableName = changeVariableName ?? "";
                  setMemberEntity(variableEntity(nextChangeVariableName));
                  patchObject(updateAction(object, { changeVariableName: nextChangeVariableName }));
                }}
              />
              <FieldError issues={getIssuesForField(issues, "action.changeVariableName")} />
            </Field>
          )}
          <Field label="Member Changes (member|assignment|expression)">
            <Space vertical align="start" spacing={6} style={{ width: "100%" }}>
              <FieldError issues={getIssuesForField(issues, "action.memberChanges")} />
              {action.kind === "changeMembers" ? (
                <EntitySelector value={memberEntity} disabled={readonly} onChange={setMemberEntity} placeholder="Select changed object entity" />
              ) : null}
              {action.memberChanges.map((change, index) => (
                <div key={change.id} style={{ display: "grid", gridTemplateColumns: "minmax(0, 1fr) 90px minmax(0, 1fr) auto", gap: 6, width: "100%" }}>
                  <AttributeSelector
                    entityQualifiedName={action.kind === "createObject" ? action.entityQualifiedName : memberEntity}
                    value={change.memberQualifiedName}
                    disabled={readonly}
                    writableOnly
                    onChange={memberQualifiedName => patchObject(updateAction(object, {
                      memberChanges: action.memberChanges.map((row, rowIndex) => rowIndex === index ? { ...row, memberQualifiedName: memberQualifiedName ?? "" } : row),
                    }))}
                  />
                  <Select
                    value={change.assignmentKind}
                    disabled={readonly}
                    optionList={["set", "add", "remove", "clear"].map(value => ({ label: value, value }))}
                    onChange={assignmentKind => patchObject(updateAction(object, {
                      memberChanges: action.memberChanges.map((row, rowIndex) => rowIndex === index ? { ...row, assignmentKind: String(assignmentKind) as typeof row.assignmentKind } : row),
                    }))}
                  />
                  <ExpressionEditor
                    value={change.valueExpression}
                    schema={schema}
                    metadata={catalog}
                    variableIndex={variableIndex}
                    objectId={object.id}
                    actionId={action.id}
                    fieldPath={`action.memberChanges.${index}.valueExpression`}
                    expectedType={getAttributeByQualifiedName(catalog, change.memberQualifiedName)?.type}
                    required={change.assignmentKind !== "clear"}
                    disabled={readonly}
                    placeholder="Expression"
                    onChange={valueExpression => patchObject(updateAction(object, {
                      memberChanges: action.memberChanges.map((row, rowIndex) => rowIndex === index ? { ...row, valueExpression } : row),
                    }))}
                  />
                  <Button disabled={readonly} type="danger" theme="borderless" onClick={() => patchObject(updateAction(object, {
                    memberChanges: action.memberChanges.filter((_, rowIndex) => rowIndex !== index),
                  }))}>Delete</Button>
                </div>
              ))}
              <Button disabled={readonly || (action.kind === "createObject" ? !action.entityQualifiedName : !memberEntity)} onClick={() => patchObject(updateAction(object, {
                memberChanges: [...action.memberChanges, {
                  id: `member-change-${Date.now()}`,
                  memberQualifiedName: "",
                  memberKind: "attribute",
                  assignmentKind: "set",
                  valueExpression: expression(""),
                }],
              }))}>Add member change</Button>
            </Space>
          </Field>
          <Field label="Commit Enabled">
            <Switch checked={action.commit.enabled} disabled={readonly} onChange={enabled => patchObject(updateAction(object, { commit: { ...action.commit, enabled } }))} />
          </Field>
          <Field label="With Events">
            <Switch checked={action.commit.withEvents} disabled={readonly} onChange={withEvents => patchObject(updateAction(object, { commit: { ...action.commit, withEvents } }))} />
          </Field>
          <Field label="Refresh In Client">
            <Switch checked={action.commit.refreshInClient} disabled={readonly} onChange={refreshInClient => patchObject(updateAction(object, { commit: { ...action.commit, refreshInClient } }))} />
          </Field>
          {action.kind === "changeMembers" ? (
            <Field label="Validate Object">
              <Switch checked={action.validateObject} disabled={readonly} onChange={validateObject => patchObject(updateAction(object, { validateObject }))} />
            </Field>
          ) : null}
        </>
      ) : null}

      {action.kind === "restCall" ? (
        <>
          <Title heading={6} style={{ margin: "10px 0 0" }}>REST Request</Title>
          <Field label="Method">
            <Select
              value={action.request.method}
              disabled={readonly}
              style={{ width: "100%" }}
              onChange={method => patchObject(updateAction(object, { request: { ...action.request, method: String(method) as typeof action.request.method } }))}
              optionList={["GET", "POST", "PUT", "PATCH", "DELETE"].map(value => ({ label: value, value }))}
            />
          </Field>
          <Field label="URL Expression">
            <ExpressionEditor
              value={action.request.urlExpression}
              schema={schema}
              metadata={catalog}
              variableIndex={variableIndex}
              objectId={object.id}
              actionId={action.id}
              fieldPath="action.request.urlExpression"
              expectedType={{ kind: "string" }}
              required
              readonly={readonly}
              onChange={urlExpression => patchObject(updateAction(object, { request: { ...action.request, urlExpression } }))}
            />
          </Field>
          <Field label="Timeout Seconds">
            <InputNumber value={action.timeoutSeconds} disabled={readonly} onChange={timeoutSeconds => patchObject(updateAction(object, { timeoutSeconds: Number(timeoutSeconds) }))} />
          </Field>
          <Field label="Headers">
            <Space vertical align="start" spacing={6} style={{ width: "100%" }}>
              {action.request.headers.map((header, index) => (
                <div key={`${header.key}-${index}`} style={{ display: "grid", gridTemplateColumns: "120px minmax(0, 1fr)", gap: 6, width: "100%" }}>
                  <Input value={header.key} disabled={readonly} placeholder="Header" onChange={key => patchObject(updateAction(object, { request: { ...action.request, headers: action.request.headers.map((row, rowIndex) => rowIndex === index ? { ...row, key } : row) } }))} />
                  <ExpressionEditor
                    value={header.valueExpression}
                    schema={schema}
                    metadata={catalog}
                    variableIndex={variableIndex}
                    objectId={object.id}
                    actionId={action.id}
                    fieldPath={`action.request.headers.${index}.valueExpression`}
                    expectedType={{ kind: "string" }}
                    readonly={readonly}
                    onChange={valueExpression => patchObject(updateAction(object, { request: { ...action.request, headers: action.request.headers.map((row, rowIndex) => rowIndex === index ? { ...row, valueExpression } : row) } }))}
                  />
                </div>
              ))}
              <Button disabled={readonly} onClick={() => patchObject(updateAction(object, { request: { ...action.request, headers: [...action.request.headers, { key: "", valueExpression: expression("", { kind: "string" }) }] } }))}>Add header</Button>
            </Space>
          </Field>
          <Field label="Query Parameters">
            <Space vertical align="start" spacing={6} style={{ width: "100%" }}>
              {action.request.queryParameters.map((parameter, index) => (
                <div key={`${parameter.key}-${index}`} style={{ display: "grid", gridTemplateColumns: "120px minmax(0, 1fr)", gap: 6, width: "100%" }}>
                  <Input value={parameter.key} disabled={readonly} placeholder="Query" onChange={key => patchObject(updateAction(object, { request: { ...action.request, queryParameters: action.request.queryParameters.map((row, rowIndex) => rowIndex === index ? { ...row, key } : row) } }))} />
                  <ExpressionEditor
                    value={parameter.valueExpression}
                    schema={schema}
                    metadata={catalog}
                    variableIndex={variableIndex}
                    objectId={object.id}
                    actionId={action.id}
                    fieldPath={`action.request.queryParameters.${index}.valueExpression`}
                    expectedType={{ kind: "string" }}
                    readonly={readonly}
                    onChange={valueExpression => patchObject(updateAction(object, { request: { ...action.request, queryParameters: action.request.queryParameters.map((row, rowIndex) => rowIndex === index ? { ...row, valueExpression } : row) } }))}
                  />
                </div>
              ))}
              <Button disabled={readonly} onClick={() => patchObject(updateAction(object, { request: { ...action.request, queryParameters: [...action.request.queryParameters, { key: "", valueExpression: expression("", { kind: "string" }) }] } }))}>Add query</Button>
            </Space>
          </Field>
          <Field label="Body Type">
            <Select
              value={action.request.body.kind}
              disabled={readonly}
              style={{ width: "100%" }}
              onChange={kind => {
                const bodyKind = String(kind);
                const body = bodyKind === "json" || bodyKind === "text"
                  ? { kind: bodyKind as "json" | "text", expression: expression("", { kind: "string" }) }
                  : bodyKind === "form"
                    ? { kind: "form" as const, fields: [] }
                    : bodyKind === "mapping"
                      ? { kind: "mapping" as const, exportMappingQualifiedName: "" }
                      : { kind: "none" as const };
                patchObject(updateAction(object, { request: { ...action.request, body } }));
              }}
              optionList={["none", "json", "text", "form", "mapping"].map(value => ({ label: value, value }))}
            />
          </Field>
          {action.request.body.kind === "json" || action.request.body.kind === "text" ? (
            <Field label="Body Content">
              <ExpressionEditor
                value={action.request.body.expression}
                schema={schema}
                metadata={catalog}
                variableIndex={variableIndex}
                objectId={object.id}
                actionId={action.id}
                fieldPath="action.request.body.expression"
                expectedType={action.request.body.kind === "json" ? { kind: "json" } : { kind: "string" }}
                readonly={readonly}
                mode="multiline"
                onChange={nextExpression => patchObject(updateAction(object, { request: { ...action.request, body: { ...action.request.body, expression: nextExpression } } }))}
              />
            </Field>
          ) : null}
          <Field label="Response Handling">
            <Select
              value={action.response.handling.kind}
              disabled={readonly}
              style={{ width: "100%" }}
              onChange={kind => patchObject(updateAction(object, {
                response: {
                  ...action.response,
                  handling: String(kind) === "ignore"
                    ? { kind: "ignore" }
                    : { kind: String(kind) as "string" | "json", outputVariableName: action.response.handling.kind === "ignore" ? "Response" : action.response.handling.outputVariableName }
                }
              }))}
              optionList={[{ label: "Ignore", value: "ignore" }, { label: "String", value: "string" }, { label: "JSON", value: "json" }]}
            />
          </Field>
          {action.response.handling.kind !== "ignore" ? (
            <Field label="Response Output Variable">
              <Input
                value={action.response.handling.outputVariableName}
                disabled={readonly}
                onChange={outputVariableName => patchObject(updateAction(object, { response: { ...action.response, handling: { ...action.response.handling, outputVariableName } } }))}
              />
            </Field>
          ) : null}
        </>
      ) : null}

      {action.kind === "logMessage" ? (
        <>
          <Field label="Level">
            <Select
              value={action.level}
              disabled={readonly}
              style={{ width: "100%" }}
              onChange={level => patchObject(updateAction(object, { level: String(level) as typeof action.level }))}
              optionList={["trace", "debug", "info", "warning", "error", "critical"].map(value => ({ label: value, value }))}
            />
          </Field>
          <Field label="Template">
            <ExpressionEditor
              value={action.template.text}
              schema={schema}
              metadata={catalog}
              variableIndex={variableIndex}
              objectId={object.id}
              actionId={action.id}
              fieldPath="action.template.text"
              expectedType={{ kind: "string" }}
              readonly={readonly}
              mode="multiline"
              onChange={next => patchObject(updateAction(object, { template: { ...action.template, text: next.raw } }))}
            />
          </Field>
          <Field label="Log Node Name">
            <Input value={action.logNodeName} disabled={readonly} onChange={logNodeName => patchObject(updateAction(object, { logNodeName }))} />
          </Field>
          <Field label="Include Context Variables">
            <Switch checked={action.includeContextVariables} disabled={readonly} onChange={includeContextVariables => patchObject(updateAction(object, { includeContextVariables }))} />
          </Field>
          <Field label="Include TraceId">
            <Switch checked={action.includeTraceId} disabled={readonly} onChange={includeTraceId => patchObject(updateAction(object, { includeTraceId }))} />
          </Field>
        </>
      ) : null}

      {action.kind === "callMicroflow" ? (
        <>
          <Title heading={6} style={{ margin: "10px 0 0" }}>Call Microflow</Title>
          <Field label="Target Microflow">
            <MicroflowSelector
              value={action.targetMicroflowId}
              disabled={readonly}
              onChange={targetMicroflowId => {
                const target = getMicroflowById(catalog, targetMicroflowId);
                patchObject(updateAction(object, {
                  targetMicroflowId: targetMicroflowId ?? "",
                  targetMicroflowQualifiedName: target?.qualifiedName,
                  parameterMappings: target
                    ? target.parameters.map(parameter => action.parameterMappings.find(mapping => mapping.parameterName === parameter.name) ?? {
                        parameterName: parameter.name,
                        parameterType: parameter.type,
                        argumentExpression: expression(""),
                      })
                    : action.parameterMappings,
                }));
              }}
            />
            <FieldError issues={getIssuesForField(issues, "action.targetMicroflowId")} />
          </Field>
          {selectedMicroflow ? (
            <Field label="Target Signature">
              <TextArea
                disabled
                autosize
                value={`${selectedMicroflow.qualifiedName}\n${selectedMicroflow.parameters.map(parameter => `${parameter.name}: ${dataTypeLabel(parameter.type)}`).join("\n")}\nreturn: ${dataTypeLabel(selectedMicroflow.returnType)}`}
              />
            </Field>
          ) : null}
          <Field label="Parameter Mappings (name=expression)">
            <TextArea
              autosize
              disabled={readonly}
              value={action.parameterMappings.map(mapping => `${mapping.parameterName}=${mapping.argumentExpression.raw}`).join("\n")}
              onChange={value => patchObject(updateAction(object, {
                parameterMappings: value.split("\n").map(line => line.trim()).filter(Boolean).map(line => {
                  const [parameterName = "", raw = ""] = line.split("=");
                  return { parameterName, parameterType: { kind: "string" }, argumentExpression: expression(raw) };
                }),
              }))}
            />
          </Field>
          <Field label="Store Result">
            <Switch checked={action.returnValue.storeResult} disabled={readonly} onChange={storeResult => patchObject(updateAction(object, { returnValue: { ...action.returnValue, storeResult } }))} />
          </Field>
          <Field label="Return Variable Name">
            <Input value={action.returnValue.outputVariableName ?? ""} disabled={readonly || !action.returnValue.storeResult} onChange={outputVariableName => patchObject(updateAction(object, { returnValue: { ...action.returnValue, outputVariableName } }))} />
          </Field>
          <Field label="Call Mode">
            <Select
              value={action.callMode}
              disabled={readonly}
              style={{ width: "100%" }}
              onChange={callMode => patchObject(updateAction(object, { callMode: String(callMode) as typeof action.callMode }))}
              optionList={[{ label: "sync", value: "sync" }, { label: "asyncReserved", value: "asyncReserved" }]}
            />
          </Field>
        </>
      ) : null}

      {action.kind === "createVariable" ? (
        <>
          <Title heading={6} style={{ margin: "10px 0 0" }}>Create Variable</Title>
          <Field label="Variable Name">
            <Input value={action.variableName} disabled={readonly} onChange={variableName => patchObject(updateAction(object, { variableName }))} />
          </Field>
          <Field label="Data Type">
            <DataTypeSelector value={action.dataType} disabled={readonly} onChange={dataType => patchObject(updateAction(object, { dataType }))} />
            <FieldError issues={getIssuesForField(issues, "action.dataType")} />
          </Field>
          <Field label="Initial Value">
            <ExpressionEditor
              value={action.initialValue}
              schema={schema}
              metadata={catalog}
              variableIndex={variableIndex}
              objectId={object.id}
              actionId={action.id}
              fieldPath="action.initialValue"
              expectedType={action.dataType}
              readonly={readonly}
              onChange={initialValue => patchObject(updateAction(object, { initialValue }))}
            />
          </Field>
        </>
      ) : null}

      {action.kind === "changeVariable" ? (
        <>
          <Title heading={6} style={{ margin: "10px 0 0" }}>Change Variable</Title>
          <Field label="Target Variable">
            <VariableSelector
              schema={schema}
              objectId={object.id}
              fieldPath="action.variableName"
              value={action.variableName}
              disabled={readonly}
              onChange={variableName => patchObject(updateAction(object, { variableName: variableName ?? "" }))}
            />
            <FieldError issues={getIssuesForField(issues, "action.variableName")} />
          </Field>
          <Field label="New Value Expression">
            <ExpressionEditor
              value={action.valueExpression}
              schema={schema}
              metadata={catalog}
              variableIndex={variableIndex}
              objectId={object.id}
              actionId={action.id}
              fieldPath="action.valueExpression"
              expectedType={resolveVariableReferenceFromIndex(schema, variableIndex, { objectId: object.id, actionId: action.id, fieldPath: "action.variableName" }, action.variableName)?.dataType}
              required
              readonly={readonly}
              onChange={valueExpression => patchObject(updateAction(object, { valueExpression }))}
            />
          </Field>
        </>
      ) : null}
      <GenericActionFields schema={schema} object={object} issues={issues} readonly={readonly} onPatch={onPatch} />
    </Space>
  );
}

function ObjectPanel(props: MicroflowPropertyPanelProps) {
  const object = props.selectedObject;
  if (!object) {
    return null;
  }
  const catalog = useMicroflowMetadata();
  const variableIndex = useMemo(() => buildVariableIndex(props.schema, catalog), [props.schema, catalog.version]);
  const tabs = useMemo(() => getObjectTabs(object), [object]);
  const [activeTab, setActiveTab] = useState<MicroflowPropertyTabKey>(tabs[0] ?? "properties");
  useEffect(() => {
    setActiveTab(tabs[0] ?? "properties");
  }, [object.id, tabs]);
  const issues = issuesFor(props, object.id, undefined, object.kind === "actionActivity" ? object.action.id : undefined);
  const patch = (next: MicroflowObject) => props.onObjectChange(object.id, { object: next });
  const parameter = object.kind === "parameterObject"
    ? props.schema.parameters.find(item => item.id === object.parameterId)
    : undefined;
  const patchParameter = (parameterPatch: Partial<MicroflowParameter>) => {
    if (!parameter || !props.onSchemaChange) {
      return;
    }
    const nextSchema = updateParameter(props.schema, parameter.id, parameterPatch);
    props.onSchemaChange(nextSchema, "updateParameter");
  };
  return (
    <>
      <Header
        props={props}
        title={objectTitle(object)}
        subtitle={object.officialType}
        onDelete={() => props.onDeleteObject?.(object.id)}
        onDuplicate={() => props.onDuplicateObject?.(object.id)}
      />
      <PropertyTabs tabs={tabs} activeKey={activeTab} onChange={setActiveTab} />
      <div style={{ padding: 14, display: "grid", gap: 12 }}>
        <ValidationIssueList issues={issues} />
        {activeTab === "properties" ? (
          <>
        <Field label="Kind">
          <Input value={object.kind} disabled />
        </Field>
        <Field label="Caption">
          <Input value={object.caption ?? ""} disabled={props.readonly} onChange={caption => patch({ ...object, caption })} />
        </Field>
        <Field label="Documentation">
          <TextArea value={object.documentation ?? ""} autosize disabled={props.readonly} onChange={documentation => patch({ ...object, documentation })} />
        </Field>
        <Field label="Disabled">
          <Switch checked={Boolean(object.disabled)} disabled={props.readonly} onChange={disabled => patch({ ...object, disabled })} />
        </Field>
        {object.kind === "startEvent" ? (
          <Field label="Trigger">
            <Select
              value={object.trigger.type}
              disabled={props.readonly}
              style={{ width: "100%" }}
              onChange={type => patch({ ...object, trigger: { type: String(type) as typeof object.trigger.type } })}
              optionList={["manual", "pageEvent", "formSubmit", "workflowCall", "apiCall", "scheduled", "system"].map(value => ({ label: value, value }))}
            />
          </Field>
        ) : null}
        {object.kind === "endEvent" ? (
          <>
            <Field label="Return Type">
              <Input value={dataTypeLabel(props.schema.returnType)} disabled />
            </Field>
            <Field label="Return Value">
              <ExpressionEditor
                value={object.returnValue}
                schema={props.schema}
                metadata={catalog}
                variableIndex={variableIndex}
                objectId={object.id}
                fieldPath="returnValue"
                expectedType={props.schema.returnType}
                required={props.schema.returnType.kind !== "void"}
                readonly={props.readonly || props.schema.returnType.kind === "void"}
                onChange={returnValue => patch({ ...object, returnValue })}
              />
            </Field>
          </>
        ) : null}
        {object.kind === "errorEvent" ? (
          <>
            <Field label="Error Variable">
              <Input value={object.error.sourceVariableName} disabled />
            </Field>
            <Field label="Message Expression">
              <Input value={object.error.messageExpression?.raw ?? ""} disabled={props.readonly} onChange={raw => patch({ ...object, error: { ...object.error, messageExpression: raw ? expression(raw, { kind: "string" }) : undefined } })} />
            </Field>
          </>
        ) : null}
        {object.kind === "breakEvent" || object.kind === "continueEvent" ? (
          <Text type="tertiary" size="small">This control event is valid only inside a loop body.</Text>
        ) : null}
        {object.kind === "exclusiveSplit" ? (
          <>
            <Field label="Decision Type">
              <Select
                value={object.splitCondition.kind}
                disabled={props.readonly}
                style={{ width: "100%" }}
                onChange={kind => {
                  const nextKind = String(kind);
                  patch({
                    ...object,
                    splitCondition: nextKind === "rule"
                      ? { kind: "rule", ruleQualifiedName: "", parameterMappings: [], resultType: "boolean" }
                      : { kind: "expression", expression: expression("true", { kind: "boolean" }), resultType: "boolean" },
                  });
                }}
                optionList={[{ label: "expression", value: "expression" }, { label: "rule", value: "rule" }]}
              />
            </Field>
            {object.splitCondition.kind === "expression" ? (
              <>
                <Field label="Result Type">
                  <Select
                    value={object.splitCondition.resultType}
                    disabled={props.readonly}
                    style={{ width: "100%" }}
                    onChange={resultType => patch({
                      ...object,
                      splitCondition: {
                        ...object.splitCondition,
                        resultType: String(resultType) as "boolean" | "enumeration",
                        expression: expression(object.splitCondition.expression.raw, String(resultType) === "enumeration"
                          ? { kind: "enumeration", enumerationQualifiedName: object.splitCondition.enumerationQualifiedName ?? "" }
                          : { kind: "boolean" }),
                      },
                    })}
                    optionList={[{ label: "boolean", value: "boolean" }, { label: "enumeration", value: "enumeration" }]}
                  />
                </Field>
                {object.splitCondition.resultType === "enumeration" ? (
                  <Field label="Enumeration Type">
                    <EnumerationSelector value={object.splitCondition.enumerationQualifiedName} disabled={props.readonly} onChange={enumerationQualifiedName => patch({ ...object, splitCondition: { ...object.splitCondition, enumerationQualifiedName } })} />
                    <FieldError issues={getIssuesForField(issues, "splitCondition.enumerationQualifiedName")} />
                  </Field>
                ) : null}
                <Field label="Split Expression">
                  <ExpressionEditor
                    value={object.splitCondition.expression}
                    schema={props.schema}
                    metadata={catalog}
                    variableIndex={variableIndex}
                    objectId={object.id}
                    fieldPath="splitCondition.expression"
                    expectedType={object.splitCondition.resultType === "enumeration"
                      ? { kind: "enumeration", enumerationQualifiedName: object.splitCondition.enumerationQualifiedName ?? "" }
                      : { kind: "boolean" }}
                    required
                    readonly={props.readonly}
                    onChange={nextExpression => patch({
                      ...object,
                      splitCondition: {
                        ...object.splitCondition,
                        expression: nextExpression
                      } as typeof object.splitCondition
                    })}
                  />
                </Field>
              </>
            ) : (
              <Field label="Rule Reference">
                <Input value={object.splitCondition.ruleQualifiedName} disabled={props.readonly} onChange={ruleQualifiedName => patch({ ...object, splitCondition: { ...object.splitCondition, ruleQualifiedName } })} />
              </Field>
            )}
          </>
        ) : null}
        {object.kind === "inheritanceSplit" ? (
          <>
            <Field label="Input Object Variable">
              <Input value={object.inputObjectVariableName} disabled={props.readonly} onChange={inputObjectVariableName => patch({ ...object, inputObjectVariableName })} />
            </Field>
            <Field label="Generalized Entity">
              <EntitySelector value={object.generalizedEntityQualifiedName} disabled={props.readonly} onChange={generalizedEntityQualifiedName => {
                const specializations = getSpecializations(catalog, generalizedEntityQualifiedName);
                patch({
                  ...object,
                  generalizedEntityQualifiedName: generalizedEntityQualifiedName ?? "",
                  allowedSpecializations: specializations,
                  entity: { generalizedEntityQualifiedName: generalizedEntityQualifiedName ?? "", allowedSpecializations: specializations },
                });
              }} />
              <FieldError issues={getIssuesForField(issues, "generalizedEntityQualifiedName")} />
            </Field>
            <Field label="Allowed Specializations">
              <Select
                multiple
                filter
                value={object.allowedSpecializations}
                disabled={props.readonly || !object.generalizedEntityQualifiedName}
                style={{ width: "100%" }}
                optionList={getSpecializations(catalog, object.generalizedEntityQualifiedName).map(value => ({ label: value, value }))}
                onChange={selected => {
                  const allowedSpecializations = Array.isArray(selected) ? selected.map(String) : [];
                  patch({ ...object, allowedSpecializations, entity: { ...object.entity, allowedSpecializations } });
                }}
              />
              <FieldError issues={getIssuesForField(issues, "allowedSpecializations")} />
            </Field>
          </>
        ) : null}
        {object.kind === "exclusiveMerge" ? (
          <>
            <Field label="Merge Strategy">
              <Input value={object.mergeBehavior.strategy} disabled />
            </Field>
            <Field label="Incoming Count">
              <Input value={String(collectFlowsRecursive(props.schema).filter(flow => flow.destinationObjectId === object.id).length)} disabled />
            </Field>
            <Field label="Outgoing Count">
              <Input value={String(collectFlowsRecursive(props.schema).filter(flow => flow.originObjectId === object.id).length)} disabled />
            </Field>
          </>
        ) : null}
        {object.kind === "loopedActivity" ? (
          <>
            <Field label="Loop Source">
              <Select
                value={object.loopSource.kind}
                disabled={props.readonly}
                style={{ width: "100%" }}
                onChange={kind => patch({
                  ...object,
                  loopSource: String(kind) === "whileCondition"
                    ? { kind: "whileCondition", officialType: "Microflows$WhileLoopCondition", expression: expression("true", { kind: "boolean" }) }
                    : { kind: "iterableList", officialType: "Microflows$IterableList", listVariableName: "Items", iteratorVariableName: "Item", currentIndexVariableName: "$currentIndex" } as MicroflowIterableListLoopSource | MicroflowWhileLoopCondition
                })}
                optionList={[{ label: "Iterable List", value: "iterableList" }, { label: "While Condition", value: "whileCondition" }]}
              />
            </Field>
              {object.loopSource.kind === "iterableList" ? (
                <>
                  <Field label="List Variable">
                    <VariableSelector
                      schema={props.schema}
                      objectId={object.id}
                      fieldPath="loopSource.listVariableName"
                      allowedTypeKinds={["list"]}
                      value={object.loopSource.listVariableName}
                      disabled={props.readonly}
                      onChange={listVariableName => patch({ ...object, loopSource: { ...object.loopSource, listVariableName: listVariableName ?? "" } as MicroflowIterableListLoopSource })}
                    />
                    <FieldError issues={getIssuesForField(issues, "loopSource.listVariableName")} />
                  </Field>
                <Field label="Iterator Variable">
                  <Input value={object.loopSource.iteratorVariableName} disabled={props.readonly} onChange={iteratorVariableName => patch({ ...object, loopSource: { ...object.loopSource, iteratorVariableName } as MicroflowIterableListLoopSource })} />
                </Field>
                <Field label="Current Index Variable">
                  <Input value={object.loopSource.currentIndexVariableName} disabled />
                </Field>
              </>
            ) : (
              <Field label="While Expression">
                <ExpressionEditor
                  value={object.loopSource.expression}
                  schema={props.schema}
                  metadata={catalog}
                  variableIndex={variableIndex}
                  objectId={object.id}
                  fieldPath="loopSource.expression"
                  expectedType={{ kind: "boolean" }}
                  required
                  readonly={props.readonly}
                  onChange={nextExpression => patch({ ...object, loopSource: { ...object.loopSource, expression: nextExpression } as MicroflowWhileLoopCondition })}
                />
              </Field>
            )}
            <Field label="Body Summary">
              <Input
                value={[
                  `${object.objectCollection.objects.length} nodes`,
                  `${(object.objectCollection.flows ?? []).length || collectFlowsRecursive(props.schema).filter(flow =>
                    object.objectCollection.objects.some(child => child.id === flow.originObjectId) &&
                    object.objectCollection.objects.some(child => child.id === flow.destinationObjectId)
                  ).length} flows`,
                  `${object.objectCollection.objects.filter(child => child.kind === "breakEvent").length} break`,
                  `${object.objectCollection.objects.filter(child => child.kind === "continueEvent").length} continue`,
                ].join(" · ")}
                disabled
              />
            </Field>
          </>
        ) : null}
          {object.kind === "actionActivity" ? (
            <ActionActivityFields schema={props.schema} object={object} issues={issues} readonly={props.readonly} onPatch={payload => props.onObjectChange(object.id, payload)} />
          ) : null}
        {object.kind === "parameterObject" ? (
          <>
            <Field label="Parameter Name">
              <Input
                value={parameter?.name ?? object.parameterName ?? ""}
                disabled={props.readonly || !parameter}
                onChange={name => {
                  if (parameter && props.onSchemaChange) {
                    const nextSchema = updateParameter({
                      ...props.schema,
                      objectCollection: {
                        ...props.schema.objectCollection,
                        objects: props.schema.objectCollection.objects.map(item => item.id === object.id ? { ...object, caption: name, parameterName: name } : item),
                      },
                    }, parameter.id, { name });
                    props.onSchemaChange(nextSchema, "updateParameterObject");
                    return;
                  }
                  patch({ ...object, caption: name, parameterName: name });
                }}
              />
            </Field>
            <Field label="Data Type">
              <DataTypeSelector value={parameter?.dataType ?? { kind: "string" }} disabled={props.readonly || !parameter} onChange={dataType => patchParameter({ dataType })} />
              <FieldError issues={getIssuesForField(issues, "parameter.dataType")} />
            </Field>
            <Field label="Required">
              <Switch checked={parameter?.required ?? false} disabled={props.readonly || !parameter} onChange={required => patchParameter({ required })} />
            </Field>
            <Field label="Default Value">
              <Input value={parameter?.defaultValue?.raw ?? ""} disabled={props.readonly || !parameter} onChange={raw => patchParameter({ defaultValue: raw ? expression(raw, parameter?.dataType) : undefined })} />
            </Field>
            <Field label="Example Value">
              <Input value={parameter?.exampleValue ?? ""} disabled={props.readonly || !parameter} onChange={exampleValue => patchParameter({ exampleValue })} />
            </Field>
          </>
        ) : null}
        {object.kind === "annotation" ? (
          <>
            <Field label="Content">
              <TextArea value={object.text} autosize disabled={props.readonly} onChange={text => patch({ ...object, text })} />
            </Field>
            <Field label="Color Token">
              <Input value={object.editor.colorToken ?? ""} disabled={props.readonly} onChange={colorToken => patch({ ...object, editor: { ...object.editor, colorToken } })} />
            </Field>
            <Field label="Pinned">
              <Switch checked={Boolean((object.editor as unknown as { pinned?: boolean }).pinned)} disabled={props.readonly} onChange={pinned => patch(updateObjectAdvanced(object, { pinned }))} />
            </Field>
            <Field label="Export To Documentation">
              <Switch checked={(object.editor as unknown as { exportToDocumentation?: boolean }).exportToDocumentation ?? true} disabled={props.readonly} onChange={exportToDocumentation => patch(updateObjectAdvanced(object, { exportToDocumentation }))} />
            </Field>
          </>
        ) : null}
          </>
        ) : null}
        {activeTab === "documentation" ? (
          <Field label="Documentation">
            <TextArea value={object.documentation ?? ""} autosize disabled={props.readonly} onChange={documentation => patch(updateObjectDocumentation(object, documentation))} />
          </Field>
        ) : null}
        {activeTab === "errorHandling" ? (
          <>
            {object.kind === "actionActivity" ? (
              <Field label="Error Handling Type">
                <Select
                  value={object.action.errorHandlingType}
                  disabled={props.readonly}
                  style={{ width: "100%" }}
                  onChange={errorHandlingType => patch(updateAction(object, { errorHandlingType: String(errorHandlingType) as MicroflowAction["errorHandlingType"] }))}
                  optionList={["rollback", "customWithRollback", "customWithoutRollback", "continue"].map(value => ({ label: value, value }))}
                />
              </Field>
            ) : object.kind === "exclusiveSplit" || object.kind === "inheritanceSplit" || object.kind === "loopedActivity" ? (
              <Field label="Error Handling Type">
                <Select
                  value={object.errorHandlingType}
                  disabled={props.readonly}
                  style={{ width: "100%" }}
                  onChange={errorHandlingType => patch({ ...object, errorHandlingType: String(errorHandlingType) as typeof object.errorHandlingType })}
                  optionList={["rollback", "customWithRollback", "customWithoutRollback", "continue"].map(value => ({ label: value, value }))}
                />
              </Field>
            ) : (
              <Text type="tertiary">This object does not expose error handling.</Text>
            )}
            <Text type="tertiary" size="small">Custom error handler branches are represented by errorHandler flows. latestError is available on custom handlers.</Text>
          </>
        ) : null}
        {activeTab === "output" ? (
          <>
            {object.kind === "actionActivity" && object.action.kind === "retrieve" ? <Field label="Output Variable"><Input value={object.action.outputVariableName} disabled /></Field> : null}
            {object.kind === "actionActivity" && object.action.kind === "createVariable" ? <Field label="Output Variable"><Input value={object.action.variableName} disabled /></Field> : null}
            {object.kind === "actionActivity" && object.action.kind === "restCall" && object.action.response.handling.kind !== "ignore" ? <Field label="Output Variable"><Input value={object.action.response.handling.outputVariableName} disabled /></Field> : null}
            {object.kind === "actionActivity" && genericOutputSummary(object.action) ? <Field label="Output Spec"><Input value={genericOutputSummary(object.action)} disabled /></Field> : null}
            {object.kind === "parameterObject" ? <Field label="Parameter"><Input value={`${parameter?.name ?? object.parameterName ?? ""}: ${dataTypeLabel(parameter?.dataType)}`} disabled /></Field> : null}
            {object.kind === "loopedActivity" && object.loopSource.kind === "iterableList" ? <Field label="Loop Variables"><Input value={`${object.loopSource.iteratorVariableName}, ${object.loopSource.currentIndexVariableName}`} disabled /></Field> : null}
            {object.kind === "endEvent" ? <Field label="Return Value"><Input value={object.returnValue?.raw ?? ""} disabled /></Field> : null}
            {object.kind === "exclusiveSplit" || object.kind === "inheritanceSplit" ? <Field label="Branch Outputs"><TextArea value={collectFlowsRecursive(props.schema).filter(flow => flow.originObjectId === object.id).map(flow => `${flow.id}: ${flow.kind === "sequence" ? flow.caseValues.map(value => value.kind).join(",") || "pending" : "annotation"}`).join("\n")} disabled autosize /></Field> : null}
          </>
        ) : null}
        {activeTab === "advanced" ? (
          <>
            <Field label="Disabled">
              <Switch checked={Boolean(object.disabled)} disabled={props.readonly} onChange={disabled => patch({ ...object, disabled })} />
            </Field>
            <Field label="Performance Tag">
              <Input value={(object.editor as unknown as { advanced?: { performanceTag?: string } }).advanced?.performanceTag ?? ""} disabled={props.readonly} onChange={performanceTag => patch(updateObjectAdvanced(object, { performanceTag }))} />
            </Field>
            <Field label="Execution Timeout">
              <Input value={String((object.editor as unknown as { advanced?: { timeoutMs?: number } }).advanced?.timeoutMs ?? "")} disabled={props.readonly} onChange={timeoutMs => patch(updateObjectAdvanced(object, { timeoutMs: Number(timeoutMs) || undefined }))} />
            </Field>
            <Field label="Retry Enabled">
              <Switch checked={Boolean((object.editor as unknown as { advanced?: { retryEnabled?: boolean } }).advanced?.retryEnabled)} disabled={props.readonly} onChange={retryEnabled => patch(updateObjectAdvanced(object, { retryEnabled }))} />
            </Field>
          </>
        ) : null}
      </div>
    </>
  );
}

function flowPatch(flow: MicroflowFlow, patch: MicroflowEdgePatch): MicroflowFlow {
  return { ...flow, ...patch } as MicroflowFlow;
}

function CaseValueField({ props, flow, patch }: {
  props: MicroflowPropertyPanelProps;
  flow: MicroflowSequenceFlow;
  patch: (next: MicroflowFlow) => void;
}) {
  const caseKind = getCaseEditorKind(props.schema, flow.originObjectId);
  if (!caseKind || flow.isErrorHandler || flow.editor.edgeKind === "sequence") {
    return (
      <Field label="Case Values">
        <TextArea
          autosize
          disabled={props.readonly}
          value={flow.caseValues.map(item => item.kind === "boolean" ? item.persistedValue : item.kind === "enumeration" ? item.value : item.kind === "inheritance" ? item.entityQualifiedName : item.kind).join("\n")}
          onChange={value => patch(flowPatch(flow, {
            caseValues: value.split("\n").map(item => item.trim()).filter(Boolean).map(item => item === "true" || item === "false"
              ? { kind: "boolean" as const, officialType: "Microflows$EnumerationCase" as const, value: item === "true", persistedValue: item as "true" | "false" }
              : { kind: "enumeration" as const, officialType: "Microflows$EnumerationCase" as const, enumerationQualifiedName: "", value: item })
          }))}
        />
      </Field>
    );
  }
  const options = getCaseOptionsForSource(props.schema, flow.originObjectId, flow.id);
  const current = flow.caseValues[0];
  const currentKey = current ? caseValueKey(current) : undefined;
  return (
    <Field label={caseKind === "enumeration" ? "Enumeration Case" : caseKind === "objectType" ? "Object Type Case" : "Boolean Case"}>
      <Select
        value={currentKey}
        disabled={props.readonly}
        style={{ width: "100%" }}
        onChange={value => {
          const option = options.find(item => item.key === String(value));
          if (!option) {
            return;
          }
          patch(flowPatch(flow, {
            caseValues: [option.caseValue],
            editor: {
              ...flow.editor,
              edgeKind: caseKind === "objectType" ? "objectTypeCondition" : "decisionCondition",
              label: option.label,
            },
          }));
        }}
        optionList={options.map(option => ({
          label: option.reason ? `${option.label} - ${option.reason}` : option.label,
          value: option.key,
          disabled: option.disabled,
        }))}
      />
    </Field>
  );
}

function FlowPanel(props: MicroflowPropertyPanelProps) {
  const flow = props.selectedFlow;
  if (!flow) {
    return null;
  }
  const tabs = useMemo(() => getFlowTabs(flow), [flow]);
  const [activeTab, setActiveTab] = useState<MicroflowPropertyTabKey>(tabs[0] ?? "properties");
  useEffect(() => {
    setActiveTab(tabs[0] ?? "properties");
  }, [flow.id, tabs]);
  const issues = issuesFor(props, undefined, flow.id);
  const patch = (next: MicroflowFlow) => props.onFlowChange?.(flow.id, next);
  return (
    <>
      <Header
        props={props}
        title={flow.kind === "sequence" ? flow.editor.label ?? "Sequence Flow" : flow.editor.label ?? "Annotation Flow"}
        subtitle={flow.officialType}
        onDelete={() => props.onDeleteFlow?.(flow.id)}
      />
      <PropertyTabs tabs={tabs} activeKey={activeTab} onChange={setActiveTab} />
      <div style={{ padding: 14, display: "grid", gap: 12 }}>
        <ValidationIssueList issues={issues} />
        {activeTab === "properties" ? (
          <>
        <Field label="Origin Object">
          <Input value={objectName(props.schema, flow.originObjectId)} disabled />
        </Field>
        <Field label="Destination Object">
          <Input value={objectName(props.schema, flow.destinationObjectId)} disabled />
        </Field>
        <Field label="Runtime Effect">
          <Input value={flow.kind === "annotation" ? "annotationOnly" : flow.kind === "sequence" && flow.isErrorHandler ? "errorFlow" : "controlFlow"} disabled />
        </Field>
        <Field label="Origin Connection Index">
          <InputNumber value={flow.originConnectionIndex ?? 0} disabled={props.readonly} onChange={originConnectionIndex => patch(flowPatch(flow, { originConnectionIndex: Number(originConnectionIndex) }))} />
        </Field>
        <Field label="Destination Connection Index">
          <InputNumber value={flow.destinationConnectionIndex ?? 0} disabled={props.readonly} onChange={destinationConnectionIndex => patch(flowPatch(flow, { destinationConnectionIndex: Number(destinationConnectionIndex) }))} />
        </Field>
        <Field label="Line Routing">
          <Select
            value={flow.line.kind}
            disabled={props.readonly}
            style={{ width: "100%" }}
            onChange={kind => patch(flowPatch(flow, { line: { ...flow.line, kind: String(kind) as typeof flow.line.kind } }))}
            optionList={["orthogonal", "polyline", "bezier"].map(value => ({ label: value, value }))}
          />
        </Field>
        {flow.kind === "sequence" ? (
          <>
            <Field label="Editor Edge Kind">
              <Select
                value={flow.editor.edgeKind}
                disabled={props.readonly}
                style={{ width: "100%" }}
                onChange={edgeKind => patch(flowPatch(flow, { editor: { ...flow.editor, edgeKind: String(edgeKind) as MicroflowSequenceFlow["editor"]["edgeKind"] } }))}
                optionList={["sequence", "decisionCondition", "objectTypeCondition", "errorHandler"].map(value => ({ label: value, value }))}
              />
            </Field>
            <Field label="Error Handler">
              <Switch checked={flow.isErrorHandler} disabled={props.readonly} onChange={isErrorHandler => patch(flowPatch(flow, { isErrorHandler, editor: { ...flow.editor, edgeKind: isErrorHandler ? "errorHandler" : flow.editor.edgeKind } }))} />
            </Field>
            {flow.isErrorHandler ? (
              <>
                <Field label="Expose latestError">
                  <Switch checked={flow.exposeLatestError ?? true} disabled={props.readonly} onChange={exposeLatestError => patch(flowPatch(flow, { exposeLatestError }))} />
                </Field>
                <Field label="Expose latestHttpResponse">
                  <Switch checked={Boolean(flow.exposeLatestHttpResponse)} disabled={props.readonly} onChange={exposeLatestHttpResponse => patch(flowPatch(flow, { exposeLatestHttpResponse }))} />
                </Field>
                <Field label="Expose latestSoapFault">
                  <Switch checked={Boolean(flow.exposeLatestSoapFault)} disabled={props.readonly} onChange={exposeLatestSoapFault => patch(flowPatch(flow, { exposeLatestSoapFault }))} />
                </Field>
                <Field label="Error variable name">
                  <Input value={flow.targetErrorVariableName ?? ""} disabled={props.readonly} onChange={targetErrorVariableName => patch(flowPatch(flow, { targetErrorVariableName }))} />
                </Field>
              </>
            ) : null}
            <CaseValueField props={props} flow={flow} patch={patch} />
            <Field label="Branch Order">
              <InputNumber value={flow.editor.branchOrder ?? 0} disabled={props.readonly} onChange={branchOrder => patch(flowPatch(flow, { editor: { ...flow.editor, branchOrder: Number(branchOrder) } }))} />
            </Field>
            <Field label="Label">
              <Input value={flow.editor.label ?? ""} disabled={props.readonly} onChange={label => patch(flowPatch(flow, { editor: { ...flow.editor, label } }))} />
            </Field>
          </>
        ) : (
          <>
            <Field label="Show In Export">
              <Switch checked={flow.editor.showInExport} disabled={props.readonly} onChange={showInExport => patch(flowPatch(flow, { editor: { ...flow.editor, showInExport } }))} />
            </Field>
            <Field label="Attachment Mode">
              <Select
                value={flow.attachmentMode ?? "edge"}
                disabled={props.readonly}
                style={{ width: "100%" }}
                onChange={attachmentMode => patch(flowPatch(flow, { attachmentMode: String(attachmentMode) as "node" | "edge" | "canvas" }))}
                optionList={["node", "edge", "canvas"].map(value => ({ label: value, value }))}
              />
            </Field>
            <Field label="Label">
              <Input value={flow.editor.label ?? ""} disabled={props.readonly} onChange={label => patch(flowPatch(flow, { editor: { ...flow.editor, label } }))} />
            </Field>
            <Field label="Description">
              <TextArea value={flow.editor.description ?? ""} autosize disabled={props.readonly} onChange={description => patch(flowPatch(flow, { editor: { ...flow.editor, description } }))} />
            </Field>
          </>
        )}
          </>
        ) : null}
        {activeTab === "documentation" ? (
          <>
            <Field label="Label">
              <Input value={flow.editor.label ?? ""} disabled={props.readonly} onChange={label => patch(flowPatch(flow, { editor: { ...flow.editor, label } }))} />
            </Field>
            <Field label="Description">
              <TextArea value={flow.editor.description ?? ""} autosize disabled={props.readonly} onChange={description => patch(flowPatch(flow, { editor: { ...flow.editor, description } }))} />
            </Field>
          </>
        ) : null}
        {activeTab === "errorHandling" && flow.kind === "sequence" ? (
          <>
            <Field label="Error Handler">
              <Switch checked={flow.isErrorHandler} disabled={props.readonly} onChange={isErrorHandler => patch(flowPatch(flow, { isErrorHandler, editor: { ...flow.editor, edgeKind: isErrorHandler ? "errorHandler" : "sequence" } }))} />
            </Field>
            <Field label="Expose latestError">
              <Switch checked={flow.exposeLatestError ?? true} disabled={props.readonly} onChange={exposeLatestError => patch(flowPatch(flow, { exposeLatestError }))} />
            </Field>
            <Field label="Error variable name">
              <Input value={flow.targetErrorVariableName ?? "$latestError"} disabled={props.readonly} onChange={targetErrorVariableName => patch(flowPatch(flow, { targetErrorVariableName }))} />
            </Field>
          </>
        ) : null}
      </div>
    </>
  );
}

export function MicroflowPropertyPanel(props: MicroflowPropertyPanelProps) {
  if (!props.selectedObject && !props.selectedFlow) {
    return (
      <div style={{ height: "100%", display: "grid", placeItems: "center", padding: 24 }}>
        <Empty title="未选择对象" description="选择画布对象或连线后编辑 AuthoringSchema 字段。" />
      </div>
    );
  }
  return (
    <div style={{ height: "100%", minHeight: 0, overflow: "auto", background: "var(--semi-color-bg-1, #fff)" }}>
      {props.selectedFlow ? <FlowPanel {...props} /> : <ObjectPanel {...props} />}
    </div>
  );
}
