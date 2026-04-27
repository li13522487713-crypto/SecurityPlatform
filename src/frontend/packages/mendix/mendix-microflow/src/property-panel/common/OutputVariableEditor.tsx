import type { MicroflowAuthoringSchema, MicroflowDataType, MicroflowValidationIssue } from "../../schema";
import { VariableNameInput } from "./VariableNameInput";

export function OutputVariableEditor({
  value,
  onChange,
  schema,
  objectId,
  actionId,
  fieldPath,
  suggestedBaseName,
  dataType,
  readonly,
  required,
  issues,
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
  return (
    <VariableNameInput
      value={value}
      onChange={onChange}
      schema={schema}
      objectId={objectId}
      actionId={actionId}
      fieldPath={fieldPath}
      suggestedBaseName={suggestedBaseName}
      dataType={dataType}
      readonly={readonly}
      required={required}
      issues={issues}
    />
  );
}
