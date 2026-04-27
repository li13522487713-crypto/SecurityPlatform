import { Input, Switch, TextArea, Typography } from "@douyinfe/semi-ui";
import type { MicroflowObject, MicroflowParameter } from "../../schema";
import { getParameterNameWarning } from "../../schema/utils";
import { FieldError } from "../common";
import { DataTypeSelector } from "../selectors";
import type { MicroflowPropertyPanelProps } from "../types";
import { getIssuesForField, getIssuesForObject, updateParameter } from "../utils";
import { expression, Field } from "../panel-shared";

const { Text } = Typography;

export function ParameterObjectForm({ props, object, issues, parameter }: {
  props: MicroflowPropertyPanelProps;
  object: MicroflowObject;
  issues: ReturnType<typeof getIssuesForObject>;
  parameter?: MicroflowParameter;
}) {
  if (object.kind !== "parameterObject") {
    return null;
  }
  const parameterName = parameter?.name ?? object.parameterName ?? "";
  const nameWarning = parameter ? getParameterNameWarning(props.schema, parameter.id, parameterName) : "Parameter definition is missing from schema.parameters.";
  const patchParameter = (parameterPatch: Partial<MicroflowParameter>) => {
    if (!parameter || !props.onSchemaChange) {
      return;
    }
    const nextSchema = updateParameter(props.schema, parameter.id, parameterPatch);
    props.onSchemaChange(nextSchema, "updateParameter");
  };
  return (
    <>
      <Field label="Parameter ID">
        <Input value={parameter?.id ?? object.parameterId} disabled />
      </Field>
      <Field label="Node ID">
        <Input value={object.id} disabled />
      </Field>
      <Field label="Parameter Name">
        <Input
          value={parameterName}
          disabled={props.readonly || !parameter}
          onChange={name => {
            patchParameter({ name });
          }}
        />
        {nameWarning ? <Text type="warning" size="small">{nameWarning}</Text> : null}
        <Text type="tertiary" size="small">Renaming a parameter does not rewrite existing expressions.</Text>
      </Field>
      <Field label="Data Type">
        <DataTypeSelector value={parameter?.dataType ?? { kind: "string" }} disabled={props.readonly || !parameter} allowVoid={false} onChange={dataType => patchParameter({ dataType })} />
        <FieldError issues={getIssuesForField(issues, "parameter.dataType")} />
        {parameter?.dataType.kind === "object" || parameter?.dataType.kind === "list" ? (
          <Text type="warning" size="small">Entity metadata will be connected in Stage 19.</Text>
        ) : null}
      </Field>
      <Field label="Required">
        <Switch checked={parameter?.required ?? false} disabled={props.readonly || !parameter} onChange={required => patchParameter({ required })} />
      </Field>
      <Field label="Default Value Expression">
        <Input value={parameter?.defaultValue?.raw ?? ""} disabled={props.readonly || !parameter} onChange={raw => patchParameter({ defaultValue: raw ? expression(raw, parameter?.dataType) : undefined })} />
        <Text type="tertiary" size="small">Default value is stored as text; Stage 12 does not evaluate expressions.</Text>
      </Field>
      <Field label="Example Value">
        <Input value={parameter?.exampleValue ?? ""} disabled={props.readonly || !parameter} onChange={exampleValue => patchParameter({ exampleValue })} />
      </Field>
      <Field label="Description">
        <TextArea value={parameter?.description ?? parameter?.documentation ?? ""} autosize disabled={props.readonly || !parameter} onChange={description => patchParameter({ description, documentation: description })} />
      </Field>
    </>
  );
}
