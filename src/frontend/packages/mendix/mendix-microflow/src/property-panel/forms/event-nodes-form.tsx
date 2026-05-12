import { Input, Select, Space, TextArea, Tooltip, Typography } from "@douyinfe/semi-ui";
import type { MicroflowObject } from "../../schema";
import type { MicroflowMetadataCatalog } from "../../metadata";
import type { MicroflowVariableIndex } from "../../schema/types";
import { collectFlowsRecursive, collectLoopObjects, getBreakContinueWarnings, updateEndEventReturnValue, updateMicroflowReturnType } from "../../schema/utils";
import { FieldError } from "../common";
import { ExpressionEditor } from "../expression";
import { DataTypeSelector } from "../selectors";
import type { MicroflowPropertyPanelProps } from "../types";
import { getIssuesForField, getIssuesForObject } from "../utils";
import { expression, Field } from "../panel-shared";

const { Text } = Typography;

function renderLegalState(messages: string[], defaultMessage: string) {
  return (
    <Space vertical align="start" spacing={4}>
      {messages.length
        ? messages.map(message => <Text key={message} type="warning" size="small">{message}</Text>)
        : <Text type="tertiary" size="small">{defaultMessage}</Text>}
    </Space>
  );
}

function uniqueMessages(messages: string[]) {
  return Array.from(new Set(messages.filter(Boolean)));
}

function withDisabledReason(disabledReason: string, enabledHint: string, control: JSX.Element) {
  return (
    <Tooltip content={disabledReason || enabledHint}>
      <span style={{ display: "inline-flex", width: "100%" }}>{control}</span>
    </Tooltip>
  );
}

export function EventNodesForm({ props, object, issues, metadata, variableIndex, patch }: {
  props: MicroflowPropertyPanelProps;
  object: MicroflowObject;
  issues: ReturnType<typeof getIssuesForObject>;
  metadata: MicroflowMetadataCatalog;
  variableIndex: MicroflowVariableIndex;
  patch: (next: MicroflowObject) => void;
}) {
  const readonlyDisabledReason = props.readonly ? "Readonly mode cannot edit event settings." : "";
  const flows = collectFlowsRecursive(props.schema);
  const objects = props.schema.objectCollection.objects;
  const startCount = objects.filter(item => item.kind === "startEvent").length;
  const endCount = objects.filter(item => item.kind === "endEvent").length;
  const incomingSummary = flows.filter(flow => flow.destinationObjectId === object.id).map(flow => `${flow.id}: ${flow.originObjectId}`).join("\n");
  const outgoingSummary = flows.filter(flow => flow.originObjectId === object.id).map(flow => `${flow.id}: ${flow.destinationObjectId}`).join("\n");
  if (object.kind === "startEvent") {
    const legalStateMessages = uniqueMessages([
      ...issues.filter(issue => issue.code.startsWith("MF_START_")).map(issue => issue.message),
      ...(startCount > 1 ? ["A microflow should contain only one StartEvent."] : []),
    ]);
    return (
      <>
        <Field label="Trigger">
          {withDisabledReason(
            readonlyDisabledReason,
            "Trigger",
            <Select
              value={object.trigger.type}
              disabled={props.readonly}
              style={{ width: "100%" }}
              onChange={type => patch({ ...object, trigger: { type: String(type) as typeof object.trigger.type } })}
              optionList={["manual", "pageEvent", "formSubmit", "workflowCall", "apiCall", "scheduled", "system"].map(value => ({ label: value, value }))}
            />
          )}
        </Field>
        <Field label="Incoming Flows">
          <TextArea value={incomingSummary || "No incoming flow"} autosize disabled />
        </Field>
        <Field label="Outgoing Flows">
          <TextArea value={outgoingSummary || "No outgoing flow"} autosize disabled />
          {!outgoingSummary ? <Text type="warning" size="small">Start has no outgoing flow.</Text> : null}
        </Field>
        <Field label="Legal State">
          {renderLegalState(
            legalStateMessages,
            "Start Event is the single root entry. It cannot have incoming flows or be placed inside a Loop.",
          )}
        </Field>
      </>
    );
  }
  if (object.kind === "endEvent") {
    const legalStateMessages = uniqueMessages(issues.filter(issue => issue.code.startsWith("MF_END_")).map(issue => issue.message));
    return (
      <>
        <Field label="Return Type">
          {withDisabledReason(
            readonlyDisabledReason,
            "Return type",
            <DataTypeSelector
              value={props.schema.returnType}
              disabled={props.readonly}
              onChange={returnType => {
                props.onSchemaChange?.(updateMicroflowReturnType(props.schema, returnType), "updateReturnType");
              }}
            />
          )}
          <Text type="tertiary" size="small">Return type is stored on the microflow schema and shared by all EndEvents.</Text>
        </Field>
        <Field label="Incoming Flows">
          <TextArea value={incomingSummary || "No incoming flow"} autosize disabled />
        </Field>
        <Field label="Outgoing Flows">
          <TextArea value={outgoingSummary || "No outgoing flow"} autosize disabled />
        </Field>
        <Field label="Return Value">
          <ExpressionEditor
            value={object.returnValue}
            schema={props.schema}
            metadata={metadata}
            variableIndex={variableIndex}
            objectId={object.id}
            fieldPath="returnValue"
            expectedType={props.schema.returnType}
            required={props.schema.returnType.kind !== "void"}
            readonly={props.readonly || props.schema.returnType.kind === "void"}
            onChange={returnValue => {
              if (props.onSchemaChange) {
                props.onSchemaChange(updateEndEventReturnValue(props.schema, object.id, returnValue), "updateEndReturnValue");
                return;
              }
              patch({ ...object, returnValue });
            }}
          />
          <FieldError issues={getIssuesForField(issues, "returnValue")} />
          <Text type="tertiary" size="small">Stage 12 stores the expression text only; it does not evaluate expressions.</Text>
        </Field>
        {endCount > 1 ? <Text type="warning" size="small">Multiple EndEvents share the same schema-level returnType.</Text> : null}
        <Field label="Legal State">
          {renderLegalState(
            legalStateMessages,
            "End Event accepts incoming flows only. Multiple EndEvents are allowed, and non-void microflows should provide a return value.",
          )}
        </Field>
      </>
    );
  }
  if (object.kind === "errorEvent") {
    const legalStateMessages = uniqueMessages(issues.filter(issue => issue.code.startsWith("MF_ERROR_EVENT")).map(issue => issue.message));
    return (
      <>
        <Field label="Incoming Flows">
          <TextArea value={incomingSummary || "No incoming flow"} autosize disabled />
        </Field>
        <Field label="Outgoing Flows">
          <TextArea value={outgoingSummary || "No outgoing flow"} autosize disabled />
        </Field>
        <Field label="Error Variable">
          <Input value={object.error.sourceVariableName} disabled />
          <Text type="tertiary" size="small">Error Event only works at the end of an error handler flow and typically rethrows $latestError to the caller.</Text>
        </Field>
        <Field label="Message Expression">
          <ExpressionEditor
            value={object.error.messageExpression ?? expression("", { kind: "string" })}
            schema={props.schema}
            metadata={metadata}
            variableIndex={variableIndex}
            objectId={object.id}
            fieldPath="error.messageExpression"
            expectedType={{ kind: "string" }}
            readonly={props.readonly}
            onChange={messageExpression => patch({ ...object, error: { ...object.error, messageExpression } })}
          />
          <FieldError issues={getIssuesForField(issues, "error.messageExpression")} />
          <Text type="tertiary" size="small">If provided, this message becomes the rethrown error summary. Triggering Error Event rolls back the transaction.</Text>
        </Field>
        <Field label="Legal State">
          {renderLegalState(
            legalStateMessages,
            "Valid when reached only by an error handler SequenceFlow and used as a terminal rethrow node.",
          )}
        </Field>
      </>
    );
  }
  if (object.kind === "breakEvent" || object.kind === "continueEvent") {
    const loopObjects = collectLoopObjects(props.schema);
    const warnings = getBreakContinueWarnings(props.schema, object.id);
    const legalStateMessages = uniqueMessages([
      ...issues
        .filter(issue => issue.code.startsWith(object.kind === "breakEvent" ? "MF_BREAK_" : "MF_CONTINUE_"))
        .map(issue => issue.message),
      ...warnings,
    ]);
    const outgoingSummaryForControl = flows.filter(flow => flow.originObjectId === object.id).map(flow => `${flow.id}: ${flow.destinationObjectId}`).join("\n");
    return (
      <>
        <Field label="Incoming Flows">
          <TextArea value={incomingSummary || "No incoming flow"} autosize disabled />
        </Field>
        <Field label="Outgoing Flows">
          <TextArea value={outgoingSummaryForControl || "No outgoing flow"} autosize disabled />
        </Field>
        <Field label="Target Loop">
          {withDisabledReason(
            props.readonly ? "Readonly mode cannot edit event settings." : (loopObjects.length === 0 ? "No loop nodes available in this microflow." : ""),
            "Target loop",
            <Select
              value={object.targetLoopObjectId}
              disabled={props.readonly || loopObjects.length === 0}
              showClear
              style={{ width: "100%" }}
              placeholder={loopObjects.length === 1 ? `Implicit: ${loopObjects[0].caption ?? loopObjects[0].id}` : "Select target loop"}
              onClear={() => patch({ ...object, targetLoopObjectId: undefined })}
              onChange={targetLoopObjectId => patch({ ...object, targetLoopObjectId: targetLoopObjectId ? String(targetLoopObjectId) : undefined })}
              optionList={loopObjects.map(loop => ({ label: loop.caption ?? loop.id, value: loop.id }))}
            />
          )}
        </Field>
        <Field label="Legal State">
          {renderLegalState(
            legalStateMessages,
            `${object.kind === "breakEvent" ? "Break" : "Continue"} Event must resolve to a valid Loop body target and should not emit normal outgoing SequenceFlows.`,
          )}
        </Field>
      </>
    );
  }
  return null;
}
