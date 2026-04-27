import { Input, Select, TextArea, Typography } from "@douyinfe/semi-ui";
import type { MicroflowObject } from "../../schema";
import type { MicroflowMetadataCatalog } from "../../metadata";
import type { MicroflowVariableIndex } from "../../schema/types";
import { collectFlowsRecursive, updateEndEventReturnValue, updateMicroflowReturnType } from "../../schema/utils";
import { FieldError } from "../common";
import { ExpressionEditor } from "../expression";
import { DataTypeSelector } from "../selectors";
import type { MicroflowPropertyPanelProps } from "../types";
import { getIssuesForField, getIssuesForObject } from "../utils";
import { expression, Field } from "../panel-shared";

const { Text } = Typography;

export function EventNodesForm({ props, object, issues, metadata, variableIndex, patch }: {
  props: MicroflowPropertyPanelProps;
  object: MicroflowObject;
  issues: ReturnType<typeof getIssuesForObject>;
  metadata: MicroflowMetadataCatalog;
  variableIndex: MicroflowVariableIndex;
  patch: (next: MicroflowObject) => void;
}) {
  const flows = collectFlowsRecursive(props.schema);
  const objects = props.schema.objectCollection.objects;
  const startCount = objects.filter(item => item.kind === "startEvent").length;
  const endCount = objects.filter(item => item.kind === "endEvent").length;
  const incomingSummary = flows.filter(flow => flow.destinationObjectId === object.id).map(flow => `${flow.id}: ${flow.originObjectId}`).join("\n");
  const outgoingSummary = flows.filter(flow => flow.originObjectId === object.id).map(flow => `${flow.id}: ${flow.destinationObjectId}`).join("\n");
  if (object.kind === "startEvent") {
    return (
      <>
        <Field label="Trigger">
          <Select
            value={object.trigger.type}
            disabled={props.readonly}
            style={{ width: "100%" }}
            onChange={type => patch({ ...object, trigger: { type: String(type) as typeof object.trigger.type } })}
            optionList={["manual", "pageEvent", "formSubmit", "workflowCall", "apiCall", "scheduled", "system"].map(value => ({ label: value, value }))}
          />
        </Field>
        <Field label="Outgoing Flows">
          <TextArea value={outgoingSummary || "No outgoing flow"} autosize disabled />
        </Field>
        {startCount > 1 ? <Text type="warning" size="small">A microflow should contain only one StartEvent.</Text> : null}
      </>
    );
  }
  if (object.kind === "endEvent") {
    return (
      <>
        <Field label="Return Type">
          <DataTypeSelector
            value={props.schema.returnType}
            disabled={props.readonly}
            onChange={returnType => {
              props.onSchemaChange?.(updateMicroflowReturnType(props.schema, returnType), "updateReturnType");
            }}
          />
          <Text type="tertiary" size="small">Return type is stored on the microflow schema and shared by all EndEvents.</Text>
        </Field>
        <Field label="Incoming Flows">
          <TextArea value={incomingSummary || "No incoming flow"} autosize disabled />
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
      </>
    );
  }
  if (object.kind === "errorEvent") {
    return (
      <>
        <Field label="Error Variable">
          <Input value={object.error.sourceVariableName} disabled />
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
        </Field>
      </>
    );
  }
  if (object.kind === "breakEvent" || object.kind === "continueEvent") {
    return <Text type="tertiary" size="small">This control event is valid only inside a loop body.</Text>;
  }
  return null;
}
