import { Input, Select, Typography } from "@douyinfe/semi-ui";
import type { MicroflowObject } from "../../schema";
import type { MicroflowMetadataCatalog } from "../../metadata";
import type { MicroflowVariableIndex } from "../../schema/types";
import { ExpressionEditor } from "../expression";
import type { MicroflowPropertyPanelProps } from "../types";
import { dataTypeLabel, expression, Field } from "../panel-shared";

const { Text } = Typography;

export function EventNodesForm({ props, object, metadata, variableIndex, patch }: {
  props: MicroflowPropertyPanelProps;
  object: MicroflowObject;
  metadata: MicroflowMetadataCatalog;
  variableIndex: MicroflowVariableIndex;
  patch: (next: MicroflowObject) => void;
}) {
  if (object.kind === "startEvent") {
    return (
      <Field label="Trigger">
        <Select
          value={object.trigger.type}
          disabled={props.readonly}
          style={{ width: "100%" }}
          onChange={type => patch({ ...object, trigger: { type: String(type) as typeof object.trigger.type } })}
          optionList={["manual", "pageEvent", "formSubmit", "workflowCall", "apiCall", "scheduled", "system"].map(value => ({ label: value, value }))}
        />
      </Field>
    );
  }
  if (object.kind === "endEvent") {
    return (
      <>
        <Field label="Return Type">
          <Input value={dataTypeLabel(props.schema.returnType)} disabled />
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
            onChange={returnValue => patch({ ...object, returnValue })}
          />
        </Field>
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
          <Input value={object.error.messageExpression?.raw ?? ""} disabled={props.readonly} onChange={raw => patch({ ...object, error: { ...object.error, messageExpression: raw ? expression(raw, { kind: "string" }) : undefined } })} />
        </Field>
      </>
    );
  }
  if (object.kind === "breakEvent" || object.kind === "continueEvent") {
    return <Text type="tertiary" size="small">This control event is valid only inside a loop body.</Text>;
  }
  return null;
}
