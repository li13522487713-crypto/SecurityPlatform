import { Input, Select, Switch, TextArea, Tooltip, Typography } from "@douyinfe/semi-ui";

import type { MicroflowAuthoringSchema, MicroflowDataType, MicroflowParameter } from "../../schema";
import { updateMicroflowDocumentProperties } from "../utils";
import { dataTypeLabel, Field } from "../panel-shared";
import type { MicroflowPropertyPanelProps } from "../types";

const { Text, Title } = Typography;

const returnTypeOptionList = [
  { label: "void", value: "void" },
  { label: "boolean", value: "boolean" },
  { label: "integer", value: "integer" },
  { label: "long", value: "long" },
  { label: "decimal", value: "decimal" },
  { label: "string", value: "string" },
  { label: "dateTime", value: "dateTime" },
  { label: "json", value: "json" },
  { label: "binary", value: "binary" },
  { label: "object", value: "object" },
  { label: "list", value: "list" },
  { label: "fileDocument", value: "fileDocument" },
  { label: "unknown", value: "unknown" },
];

const listItemTypeOptionList = [
  { label: "boolean", value: "boolean" },
  { label: "integer", value: "integer" },
  { label: "long", value: "long" },
  { label: "decimal", value: "decimal" },
  { label: "string", value: "string" },
  { label: "dateTime", value: "dateTime" },
  { label: "json", value: "json" },
  { label: "binary", value: "binary" },
  { label: "object", value: "object" },
  { label: "enumeration", value: "enumeration" },
];

const exportLevelOptionList = [
  { label: "hidden", value: "hidden" },
  { label: "module", value: "module" },
  { label: "public", value: "public" },
];

function searchParameterText(parameters: string[] | undefined): string {
  return (parameters ?? []).join("\n");
}

function parseSearchParameterText(raw: string): string[] {
  return raw
    .split(/[\r\n,]+/u)
    .map(item => item.trim())
    .filter(Boolean);
}

function withDisabledReason(disabledReason: string, enabledHint: string, control: JSX.Element) {
  return (
    <Tooltip content={disabledReason || enabledHint}>
      <span style={{ display: "inline-flex", width: "100%" }}>{control}</span>
    </Tooltip>
  );
}

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

function returnTypeKind(dataType: MicroflowDataType): string {
  return dataType.kind;
}

function buildPrimitiveType(kind: string): MicroflowDataType {
  switch (kind) {
    case "boolean":
    case "integer":
    case "long":
    case "decimal":
    case "string":
    case "dateTime":
    case "json":
    case "binary":
    case "void":
      return { kind };
    case "unknown":
      return { kind: "unknown" };
    default:
      return { kind: "string" };
  }
}

function buildReturnType(kind: string, current: MicroflowDataType): MicroflowDataType {
  if (kind === "object") {
    return {
      kind: "object",
      entityQualifiedName: current.kind === "object" ? current.entityQualifiedName : "",
    };
  }
  if (kind === "fileDocument") {
    return {
      kind: "fileDocument",
      entityQualifiedName: current.kind === "fileDocument" ? current.entityQualifiedName : "",
    };
  }
  if (kind === "list") {
    return {
      kind: "list",
      itemType: current.kind === "list" ? current.itemType : { kind: "string" },
    };
  }
  return buildPrimitiveType(kind);
}

export function MicroflowDocumentPropertiesForm(props: MicroflowPropertyPanelProps) {
  const { schema, readonly } = props;
  const readonlyDisabledReason = readonly ? "Readonly mode cannot edit document properties." : "";
  const patchDocument = (patch: Partial<Pick<MicroflowAuthoringSchema, "description" | "documentation" | "returnType">> & {
    applyEntityAccess?: boolean;
    allowConcurrentExecution?: boolean;
    exposureExportLevel?: MicroflowAuthoringSchema["exposure"]["exportLevel"];
    exposureMarkAsUsed?: boolean;
    exposureAsMicroflowActionEnabled?: boolean;
    exposureAsMicroflowActionCaption?: string;
    exposureAsMicroflowActionCategory?: string;
    exposureAsWorkflowActionEnabled?: boolean;
    exposureAsWorkflowActionCaption?: string;
    exposureAsWorkflowActionCategory?: string;
    exposureUrlEnabled?: boolean;
    exposureUrlPath?: string;
    exposureUrlSearchParameters?: string[];
  }) => {
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
          {withDisabledReason(
            readonlyDisabledReason,
            "Return type",
            <Select
              value={returnTypeKind(schema.returnType)}
              disabled={readonly}
              style={{ width: "100%" }}
              optionList={returnTypeOptionList}
              onChange={value => patchDocument({ returnType: buildReturnType(String(value), schema.returnType) })}
            />
          )}
          {schema.returnType.kind === "object" ? (
            <Input
              value={schema.returnType.entityQualifiedName ?? ""}
              disabled={readonly}
              placeholder="Entity qualified name"
              onChange={entityQualifiedName => patchDocument({
                returnType: {
                  kind: "object",
                  entityQualifiedName,
                },
              })}
            />
          ) : null}
          {schema.returnType.kind === "fileDocument" ? (
            <Input
              value={schema.returnType.entityQualifiedName ?? ""}
              disabled={readonly}
              placeholder="FileDocument entity qualified name"
              onChange={entityQualifiedName => patchDocument({
                returnType: {
                  kind: "fileDocument",
                  entityQualifiedName,
                },
              })}
            />
          ) : null}
          {schema.returnType.kind === "list" ? (
            <>
              <Select
                value={schema.returnType.itemType.kind}
                disabled={readonly}
                style={{ width: "100%" }}
                optionList={listItemTypeOptionList}
                onChange={value => {
                  const kind = String(value);
                  if (kind === "object") {
                    patchDocument({
                      returnType: {
                        kind: "list",
                        itemType: {
                          kind: "object",
                          entityQualifiedName: "",
                        },
                      },
                    });
                    return;
                  }
                  if (kind === "enumeration") {
                    patchDocument({
                      returnType: {
                        kind: "list",
                        itemType: {
                          kind: "enumeration",
                          enumerationQualifiedName: "",
                        },
                      },
                    });
                    return;
                  }
                  patchDocument({
                    returnType: {
                      kind: "list",
                      itemType: buildPrimitiveType(kind),
                    },
                  });
                }}
              />
              {schema.returnType.itemType.kind === "object" ? (
                <Input
                  value={schema.returnType.itemType.entityQualifiedName ?? ""}
                  disabled={readonly}
                  placeholder="List item entity qualified name"
                  onChange={entityQualifiedName => patchDocument({
                    returnType: {
                      kind: "list",
                      itemType: {
                        kind: "object",
                        entityQualifiedName,
                      },
                    },
                  })}
                />
              ) : null}
              {schema.returnType.itemType.kind === "enumeration" ? (
                <Input
                  value={schema.returnType.itemType.enumerationQualifiedName ?? ""}
                  disabled={readonly}
                  placeholder="List item enumeration qualified name"
                  onChange={enumerationQualifiedName => patchDocument({
                    returnType: {
                      kind: "list",
                      itemType: {
                        kind: "enumeration",
                        enumerationQualifiedName,
                      },
                    },
                  })}
                />
              ) : null}
            </>
          ) : null}
          <Text type="tertiary" size="small">Current: {dataTypeLabel(schema.returnType as MicroflowDataType)}</Text>
        </Field>
        <Field label="Apply Entity Access">
          {withDisabledReason(
            readonlyDisabledReason,
            "Apply entity access",
            <Switch
              checked={schema.security.applyEntityAccess}
              disabled={readonly}
              onChange={checked => patchDocument({ applyEntityAccess: checked })}
            />
          )}
        </Field>
        <Field label="Allow parallel execution">
          {withDisabledReason(
            readonlyDisabledReason,
            "Allow parallel execution",
            <Switch
              checked={schema.concurrency.allowConcurrentExecution}
              disabled={readonly}
              onChange={checked => patchDocument({ allowConcurrentExecution: checked })}
            />
          )}
        </Field>
        <Field label="Export Level">
          {withDisabledReason(
            readonlyDisabledReason,
            "Export level",
            <Select
              value={schema.exposure.exportLevel}
              disabled={readonly}
              style={{ width: "100%" }}
              optionList={exportLevelOptionList}
              onChange={value => patchDocument({ exposureExportLevel: String(value) as MicroflowAuthoringSchema["exposure"]["exportLevel"] })}
            />
          )}
        </Field>
        <Field label="Mark As Used">
          {withDisabledReason(
            readonlyDisabledReason,
            "Mark as used",
            <Switch
              checked={schema.exposure.markAsUsed}
              disabled={readonly}
              onChange={checked => patchDocument({ exposureMarkAsUsed: checked })}
            />
          )}
        </Field>
        <Field label="Exposed as microflow action">
          {withDisabledReason(
            readonlyDisabledReason,
            "Expose as microflow action",
            <Switch
              checked={schema.exposure.asMicroflowAction?.enabled ?? false}
              disabled={readonly}
              onChange={checked => patchDocument({ exposureAsMicroflowActionEnabled: checked })}
            />
          )}
          <Input
            value={schema.exposure.asMicroflowAction?.caption ?? ""}
            disabled={readonly || !(schema.exposure.asMicroflowAction?.enabled ?? false)}
            placeholder="Action caption"
            onChange={caption => patchDocument({ exposureAsMicroflowActionCaption: caption })}
          />
          <Input
            value={schema.exposure.asMicroflowAction?.category ?? ""}
            disabled={readonly || !(schema.exposure.asMicroflowAction?.enabled ?? false)}
            placeholder="Action category"
            onChange={category => patchDocument({ exposureAsMicroflowActionCategory: category })}
          />
        </Field>
        <Field label="Exposed as workflow action">
          {withDisabledReason(
            readonlyDisabledReason,
            "Expose as workflow action",
            <Switch
              checked={schema.exposure.asWorkflowAction?.enabled ?? false}
              disabled={readonly}
              onChange={checked => patchDocument({ exposureAsWorkflowActionEnabled: checked })}
            />
          )}
          <Input
            value={schema.exposure.asWorkflowAction?.caption ?? ""}
            disabled={readonly || !(schema.exposure.asWorkflowAction?.enabled ?? false)}
            placeholder="Workflow action caption"
            onChange={caption => patchDocument({ exposureAsWorkflowActionCaption: caption })}
          />
          <Input
            value={schema.exposure.asWorkflowAction?.category ?? ""}
            disabled={readonly || !(schema.exposure.asWorkflowAction?.enabled ?? false)}
            placeholder="Workflow action category"
            onChange={category => patchDocument({ exposureAsWorkflowActionCategory: category })}
          />
        </Field>
        <Field label="URL exposure">
          {withDisabledReason(
            readonlyDisabledReason,
            "Expose as URL",
            <Switch
              checked={schema.exposure.url?.enabled ?? false}
              disabled={readonly}
              onChange={checked => patchDocument({ exposureUrlEnabled: checked })}
            />
          )}
          <Input
            value={schema.exposure.url?.path ?? ""}
            disabled={readonly || !(schema.exposure.url?.enabled ?? false)}
            placeholder="/p/my-microflow"
            onChange={path => patchDocument({ exposureUrlPath: path })}
          />
          <TextArea
            value={searchParameterText(schema.exposure.url?.searchParameters)}
            autosize
            disabled={readonly || !(schema.exposure.url?.enabled ?? false)}
            placeholder={"orderId\ncustomerId"}
            onChange={raw => patchDocument({ exposureUrlSearchParameters: parseSearchParameterText(raw) })}
          />
          <Text type="tertiary" size="small">One search parameter per line or separated by commas.</Text>
        </Field>
        <Field label="Description">
          <TextArea value={schema.description ?? ""} autosize disabled />
          <Text type="tertiary" size="small">Resource-level description remains read-only here; editable document notes are stored in schema.documentation.</Text>
        </Field>
        <Field label="Documentation">
          {withDisabledReason(
            readonlyDisabledReason,
            "Documentation",
            <TextArea
              value={schema.documentation ?? ""}
              autosize
              disabled={readonly}
              onChange={documentation => patchDocument({ documentation })}
            />
          )}
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
