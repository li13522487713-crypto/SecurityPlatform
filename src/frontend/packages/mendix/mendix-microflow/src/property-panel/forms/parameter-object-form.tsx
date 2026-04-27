import { Input, Switch, Typography } from "@douyinfe/semi-ui";
import type { MicroflowObject, MicroflowParameter } from "../../schema";
import { FieldError } from "../common";
import { DataTypeSelector } from "../selectors";
import type { MicroflowPropertyPanelProps } from "../types";
import { getIssuesForField, getIssuesForObject, updateParameter } from "../utils";
import { expression, Field } from "../panel-shared";

const { Text } = Typography;

export function ParameterObjectForm({ props, object, issues, parameter, patch }: {
  props: MicroflowPropertyPanelProps;
  object: MicroflowObject;
  issues: ReturnType<typeof getIssuesForObject>;
  parameter?: MicroflowParameter;
  patch: (next: MicroflowObject) => void;
}) {
  if (object.kind !== "parameterObject") {
    return null;
  }
  const patchParameter = (parameterPatch: Partial<MicroflowParameter>) => {
    if (!parameter || !props.onSchemaChange) {
      return;
    }
    const nextSchema = updateParameter(props.schema, parameter.id, parameterPatch);
    props.onSchemaChange(nextSchema, "updateParameter");
  };
  return (
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
        {!(parameter?.name ?? object.parameterName ?? "").trim() ? <Text type="warning" size="small">Parameter name 为空，保存后会保留为待配置状态。</Text> : null}
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
  );
}
