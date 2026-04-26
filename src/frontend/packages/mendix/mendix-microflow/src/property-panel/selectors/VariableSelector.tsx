import { Select, Typography } from "@douyinfe/semi-ui";
import { useMemo } from "react";
import { useMicroflowMetadata } from "../../metadata";
import type { MicroflowAuthoringSchema, MicroflowDataType } from "../../schema";
import {
  buildVariableIndex,
  filterVariableByType,
  getAvailableVariablesAtField,
  resolveVariableReferenceFromIndex,
} from "../../variables";
import { VariableOptionLabel } from "./VariableOptionLabel";

const { Text } = Typography;

export function VariableSelector({
  schema,
  objectId,
  fieldPath = "",
  value,
  onChange,
  allowedTypeKinds,
  includeMaybe = true,
  includeSystem = true,
  includeErrorContext = true,
  disabled,
  placeholder = "Select variable",
}: {
  schema: MicroflowAuthoringSchema;
  objectId?: string;
  actionId?: string;
  fieldPath?: string;
  value?: string;
  onChange: (variableName?: string) => void;
  allowedTypes?: MicroflowDataType[];
  allowedTypeKinds?: MicroflowDataType["kind"][];
  includeMaybe?: boolean;
  includeSystem?: boolean;
  includeErrorContext?: boolean;
  disabled?: boolean;
  placeholder?: string;
}) {
  const catalog = useMicroflowMetadata();
  const variableIndex = useMemo(() => buildVariableIndex(schema, catalog), [schema, catalog.version]);
  const variables = useMemo(() => {
    if (!objectId) {
      return [];
    }
    return getAvailableVariablesAtField(schema, variableIndex, objectId, fieldPath)
      .filter(symbol => includeMaybe || symbol.visibility !== "maybe")
      .filter(symbol => includeSystem || symbol.kind !== "system")
      .filter(symbol => includeErrorContext || (symbol.kind !== "errorContext" && symbol.kind !== "restResponse" && symbol.kind !== "soapFault"))
      .filter(symbol => filterVariableByType(symbol, allowedTypeKinds));
  }, [allowedTypeKinds, fieldPath, includeErrorContext, includeMaybe, includeSystem, objectId, schema, variableIndex]);
  const current = value && objectId
    ? resolveVariableReferenceFromIndex(schema, variableIndex, { objectId, fieldPath }, value)
    : null;
  const currentVisible = !value || Boolean(current);
  return (
    <div style={{ display: "grid", gap: 4, width: "100%" }}>
      <Select
        value={value}
        disabled={disabled || !objectId}
        placeholder={placeholder}
        filter
        showClear
        style={{ width: "100%" }}
        onClear={() => onChange(undefined)}
        onChange={nextValue => onChange(nextValue ? String(nextValue) : undefined)}
        optionList={variables.map(symbol => ({
          value: symbol.name,
          label: symbol.name,
          showTick: true,
          render: () => <VariableOptionLabel symbol={symbol} />,
        }))}
      />
      {!currentVisible ? <Text size="small" type="danger">Current variable is not available in this scope.</Text> : null}
    </div>
  );
}
