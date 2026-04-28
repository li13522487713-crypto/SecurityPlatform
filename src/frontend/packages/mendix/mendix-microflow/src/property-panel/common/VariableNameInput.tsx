import { Button, Input, Space, Tag, Typography } from "@douyinfe/semi-ui";
import { useMemo } from "react";
import type { MicroflowAuthoringSchema, MicroflowDataType, MicroflowValidationIssue } from "../../schema";
import {
  buildVariableIndex,
  createUniqueVariableName,
  getExistingVariableNames,
  validateOutputVariableName,
} from "../../variables";
import { EMPTY_MICROFLOW_METADATA_CATALOG, useMetadataStatus, useMicroflowMetadataCatalog } from "../../metadata";
import { dataTypeLabel } from "../panel-shared";
import { FieldError } from "./FieldError";

const { Text } = Typography;

function toIssue(message: string, fieldPath: string, index: number): MicroflowValidationIssue {
  return {
    id: `local-variable-name-${fieldPath}-${index}`,
    severity: "error",
    message,
    fieldPath,
    code: "MF_VARIABLE_NAME_INVALID",
    source: "variable",
  };
}

export function VariableNameInput({
  value,
  onChange,
  schema,
  objectId,
  actionId,
  fieldPath,
  suggestedBaseName = "NewVariable",
  dataType,
  readonly,
  required,
  issues = [],
}: {
  value?: string;
  onChange: (next?: string) => void;
  schema: MicroflowAuthoringSchema;
  objectId: string;
  actionId?: string;
  fieldPath: string;
  suggestedBaseName?: string;
  dataType?: MicroflowDataType;
  readonly?: boolean;
  required?: boolean;
  issues?: MicroflowValidationIssue[];
}) {
  const catalog = useMicroflowMetadataCatalog() ?? EMPTY_MICROFLOW_METADATA_CATALOG;
  const { version } = useMetadataStatus();
  const variableIndex = useMemo(() => buildVariableIndex(schema, catalog), [catalog, schema, version]);
  const localIssues = validateOutputVariableName(required || value ? value ?? "" : "")
    .map((issue, index) => toIssue(issue.message, fieldPath, index));
  const duplicateIssues = (variableIndex.diagnostics ?? [])
    .filter(issue => issue.variableName === value && (issue.fieldPath === fieldPath || issue.code === "MF_VARIABLE_DUPLICATED" || issue.code === "MF_VARIABLE_PARAMETER_CONFLICT"))
    .map((issue, index) => toIssue(issue.message, fieldPath, index + localIssues.length));
  const allIssues = [...issues, ...localIssues, ...duplicateIssues];

  return (
    <div style={{ display: "grid", gap: 6, width: "100%" }} data-object-id={objectId} data-action-id={actionId}>
      <Space align="center" style={{ width: "100%" }}>
        <Input
          value={value ?? ""}
          disabled={readonly}
          placeholder={suggestedBaseName}
          onChange={next => onChange(next || undefined)}
        />
        <Button
          disabled={readonly}
          onClick={() => onChange(createUniqueVariableName(suggestedBaseName, getExistingVariableNames(schema, variableIndex)))}
        >
          Auto
        </Button>
      </Space>
      {dataType ? <Tag color="blue">{dataTypeLabel(dataType)}</Tag> : null}
      {duplicateIssues.length > 0 ? <Text size="small" type="warning">Duplicate variable name in current scope.</Text> : null}
      <FieldError issues={allIssues} />
    </div>
  );
}
