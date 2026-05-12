import { Input, Switch, TextArea, Tooltip, Typography } from "@douyinfe/semi-ui";
import type { MicroflowObject, MicroflowParameter } from "../../schema";
import { getParameterNameWarning } from "../../schema/utils";
import { FieldError } from "../common";
import { DataTypeSelector } from "../selectors";
import type { MicroflowPropertyPanelProps } from "../types";
import { getIssuesForField, getIssuesForObject, updateParameterObjectConfig } from "../utils";
import { expression, Field } from "../panel-shared";

const { Text } = Typography;

function withDisabledReason(disabledReason: string, enabledHint: string, control: JSX.Element) {
  return (
    <Tooltip content={disabledReason || enabledHint}>
      <span style={{ display: "inline-flex", width: "100%" }}>{control}</span>
    </Tooltip>
  );
}

function parameterEntityLabel(parameter: MicroflowParameter | undefined): string | undefined {
  const dataType = parameter?.dataType;
  if (!dataType) {
    return undefined;
  }
  if (dataType.kind === "object") {
    return dataType.entityQualifiedName || "(select entity)";
  }
  if (dataType.kind === "list") {
    if (dataType.itemType.kind === "object") {
      return dataType.itemType.entityQualifiedName || "(select entity)";
    }
    return `List item type: ${dataType.itemType.kind}`;
  }
  return undefined;
}

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
  const entityLabel = parameterEntityLabel(parameter);
  const nameWarning = parameter ? getParameterNameWarning(props.schema, parameter.id, parameterName) : "Parameter definition is missing from schema.parameters.";
  const readonlyDisabledReason = props.readonly ? "Readonly mode cannot edit parameter settings." : !parameter ? "Parameter definition is missing from schema.parameters." : "";
  const patchParameter = (parameterPatch: Partial<MicroflowParameter>) => {
    if (!parameter || !props.onSchemaChange) {
      return;
    }
    const nextSchema = updateParameterObjectConfig(props.schema, object.id, parameterPatch);
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
      <Field label="Name">
        {withDisabledReason(
          readonlyDisabledReason,
          "Parameter name",
          <Input
            value={parameterName}
            disabled={props.readonly || !parameter}
            onChange={name => {
              patchParameter({ name });
            }}
          />
        )}
        {nameWarning ? <Text type="warning" size="small">{nameWarning}</Text> : null}
        <Text type="tertiary" size="small">Parameter rename rewrites parameter-scoped expressions and direct variable reference fields; unrelated shadowed loop/local variables stay unchanged.</Text>
      </Field>
      <Field label="Type">
        {withDisabledReason(
          readonlyDisabledReason,
          "Data type",
          <DataTypeSelector value={parameter?.dataType ?? { kind: "unknown", reason: "missing parameter type" }} disabled={props.readonly || !parameter} allowVoid={false} onChange={dataType => patchParameter({ dataType })} />
        )}
        <FieldError issues={getIssuesForField(issues, "parameter.dataType")} />
        {!parameter?.dataType || parameter.dataType.kind === "unknown" ? (
          <Text type="warning" size="small">Parameter type is empty or unknown.</Text>
        ) : null}
        {parameter?.dataType.kind === "object" || parameter?.dataType.kind === "list" ? (
          <Text type="warning" size="small">Object/List parameter types must use real metadata; no fallback entity is generated.</Text>
        ) : null}
      </Field>
      {entityLabel ? (
        <Field label="Entity">
          <Input value={entityLabel} disabled />
        </Field>
      ) : null}
      <Field label="Optional">
        {withDisabledReason(
          readonlyDisabledReason,
          "Optional",
          <Switch checked={!(parameter?.required ?? false)} disabled={props.readonly || !parameter} onChange={optional => patchParameter({ required: !optional })} />
        )}
      </Field>
      <Field label="Default Value Expression" issues={getIssuesForField(issues, "defaultValue")}>
        {withDisabledReason(
          readonlyDisabledReason,
          "Default value expression",
          <Input value={parameter?.defaultValue?.raw ?? ""} disabled={props.readonly || !parameter} onChange={raw => patchParameter({ defaultValue: raw ? expression(raw, parameter?.dataType) : undefined })} />
        )}
        <Text type="tertiary" size="small">Default value is stored as text; Stage 12 does not evaluate expressions.</Text>
      </Field>
      <Field label="Example Value">
        {withDisabledReason(
          readonlyDisabledReason,
          "Example value",
          <Input value={parameter?.exampleValue ?? ""} disabled={props.readonly || !parameter} onChange={exampleValue => patchParameter({ exampleValue })} />
        )}
      </Field>
      <Field label="Documentation">
        {withDisabledReason(
          readonlyDisabledReason,
          "Documentation",
          <TextArea value={parameter?.description ?? parameter?.documentation ?? ""} autosize disabled={props.readonly || !parameter} onChange={description => patchParameter({ description, documentation: description })} />
        )}
      </Field>
    </>
  );
}
