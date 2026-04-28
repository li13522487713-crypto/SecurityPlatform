import { Input, TextArea, Typography } from "@douyinfe/semi-ui";

import type { MicroflowAuthoringSchema, MicroflowDataType, MicroflowParameter } from "../../schema";
import { updateMicroflowDocumentProperties } from "../utils";
import { dataTypeLabel, Field } from "../panel-shared";
import type { MicroflowPropertyPanelProps } from "../types";

const { Text, Title } = Typography;

function parameterSummary(parameters: MicroflowParameter[]): string {
  if (!parameters.length) {
    return "No parameters";
  }
  return parameters.map(parameter => {
    const required = parameter.required ? "required" : "optional";
    return `${parameter.name || "(missing name)"}: ${dataTypeLabel(parameter.dataType)} (${required})`;
  }).join("\n");
}

function auditSummary(schema: MicroflowAuthoringSchema): string {
  return [
    `status: ${schema.audit.status}`,
    `version: ${schema.audit.version}`,
    `createdAt: ${schema.audit.createdAt ?? "-"}`,
    `updatedAt: ${schema.audit.updatedAt ?? "-"}`,
  ].join("\n");
}

export function MicroflowDocumentPropertiesForm(props: MicroflowPropertyPanelProps) {
  const { schema, readonly } = props;
  const patchDocument = (patch: Partial<Pick<MicroflowAuthoringSchema, "description" | "documentation" | "returnType">>) => {
    props.onSchemaChange?.(updateMicroflowDocumentProperties(schema, patch), "updateMicroflowDocumentProperties");
  };
  return (
    <div style={{ height: "100%", minHeight: 0, overflow: "auto", background: "var(--semi-color-bg-1, #fff)" }}>
      <div style={{ padding: 14, borderBottom: "1px solid var(--semi-color-border, #e5e6eb)", background: "var(--semi-color-bg-2, #fff)" }}>
        <Title heading={6} style={{ margin: 0 }}>{schema.displayName || schema.name || "Microflow"}</Title>
        <Text size="small" type="tertiary">Microflow document properties</Text>
      </div>
      <div style={{ padding: 14, display: "grid", gap: 12 }}>
        <Field label="Microflow ID">
          <Input value={schema.id} disabled />
        </Field>
        <Field label="Name">
          <Input value={schema.name} disabled />
        </Field>
        <Field label="Display Name">
          <Input value={schema.displayName} disabled />
          <Text type="tertiary" size="small">Resource rename is handled by the resource editor, not this schema-bound form.</Text>
        </Field>
        <Field label="Qualified Name">
          <Input value={schema.moduleName ? `${schema.moduleName}.${schema.name}` : schema.name} disabled />
        </Field>
        <Field label="Schema Version">
          <Input value={schema.schemaVersion} disabled />
        </Field>
        <Field label="Return Type">
          <Input value={dataTypeLabel(schema.returnType as MicroflowDataType)} disabled />
        </Field>
        <Field label="Description">
          <TextArea value={schema.description ?? ""} autosize disabled />
          <Text type="tertiary" size="small">Resource-level description remains read-only here; editable document notes are stored in schema.documentation.</Text>
        </Field>
        <Field label="Documentation">
          <TextArea
            value={schema.documentation ?? ""}
            autosize
            disabled={readonly}
            onChange={documentation => patchDocument({ documentation })}
          />
        </Field>
        <Field label="Parameters">
          <TextArea value={parameterSummary(schema.parameters)} autosize disabled />
        </Field>
        <Field label="Audit">
          <TextArea value={auditSummary(schema)} autosize disabled />
        </Field>
        <Field label="Reference Count">
          <Input value="Unavailable in schema-bound editor context" disabled />
        </Field>
      </div>
    </div>
  );
}
