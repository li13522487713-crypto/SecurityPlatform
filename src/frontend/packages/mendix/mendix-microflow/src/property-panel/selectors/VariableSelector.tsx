import { Select, Typography } from "@douyinfe/semi-ui";
import { useMemo } from "react";
import { EMPTY_MICROFLOW_METADATA_CATALOG, useMicroflowMetadata } from "../../metadata";
import type { MicroflowAuthoringSchema, MicroflowDataType, MicroflowVariableSymbol } from "../../schema";
import {
  buildVariableIndex,
  getAvailableVariablesAtField,
  getVariableSymbols,
  resolveVariableReferenceFromIndex,
} from "../../variables";
import { VariableOptionLabel } from "./VariableOptionLabel";

const { Text } = Typography;

export function VariableSelector({
  schema,
  objectId,
  actionId,
  fieldPath = "",
  collectionId,
  value,
  onChange,
  allowedTypes,
  allowedTypeKinds,
  includeMaybe = true,
  includeSystem = true,
  includeErrorContext = true,
  includeReadonly = true,
  writableOnly = false,
  variableFilter,
  disabled,
  placeholder = "Select variable",
  scopeMode = "available",
  emptyMessage = "No variables available. Add a Create Variable node or Parameter first.",
}: {
  schema: MicroflowAuthoringSchema;
  objectId?: string;
  actionId?: string;
  fieldPath?: string;
  collectionId?: string;
  value?: string;
  onChange: (variableName?: string) => void;
  allowedTypes?: MicroflowDataType[];
  allowedTypeKinds?: MicroflowDataType["kind"][];
  includeMaybe?: boolean;
  includeSystem?: boolean;
  includeErrorContext?: boolean;
  includeReadonly?: boolean;
  writableOnly?: boolean;
  variableFilter?: (symbol: MicroflowVariableSymbol) => boolean;
  disabled?: boolean;
  placeholder?: string;
  scopeMode?: "available" | "index";
  emptyMessage?: string;
}) {
  const { catalog, loading, error, version } = useMicroflowMetadata();
  const effectiveCatalog = catalog ?? EMPTY_MICROFLOW_METADATA_CATALOG;
  const variableIndex = useMemo(() => buildVariableIndex(schema, effectiveCatalog), [schema, effectiveCatalog, version]);
  const variables = useMemo(() => {
    const sourceVariables = scopeMode === "index"
      ? getVariableSymbols(variableIndex)
      : objectId
        ? getAvailableVariablesAtField(schema, variableIndex, objectId, fieldPath, {
      allowedTypeKinds,
      allowedTypes,
      collectionId,
      includeErrorContext,
      includeMaybe,
      includeSystem,
      writableOnly: writableOnly || !includeReadonly,
        })
        : [];
    return sourceVariables
      .filter(symbol => includeSystem ? true : symbol.kind !== "system")
      .filter(symbol => includeErrorContext ? true : symbol.kind !== "errorContext" && symbol.kind !== "restResponse" && symbol.kind !== "soapFault")
      .filter(symbol => includeReadonly ? true : !symbol.readonly)
      .filter(symbol => writableOnly ? !symbol.readonly : true)
      .filter(symbol => !allowedTypeKinds?.length || allowedTypeKinds.includes(symbol.dataType.kind))
      .filter(symbol => !allowedTypes?.length || allowedTypes.some(type => type.kind === symbol.dataType.kind))
      .filter(symbol => variableFilter ? variableFilter(symbol) : true);
  }, [allowedTypeKinds, allowedTypes, collectionId, fieldPath, includeErrorContext, includeMaybe, includeReadonly, includeSystem, objectId, schema, scopeMode, variableFilter, variableIndex, writableOnly]);
  const current = value
    ? scopeMode === "index"
      ? variables.find(symbol => symbol.name === value)
      : objectId
        ? resolveVariableReferenceFromIndex(schema, variableIndex, { objectId, actionId, fieldPath, collectionId }, value)
        : null
    : null;
  const currentVisible = !value || Boolean(current);
  if (error) {
    return <Text type="danger" size="small">元数据加载失败：{error.message}</Text>;
  }
  if (loading && !catalog) {
    return <Select style={{ width: "100%" }} disabled placeholder="元数据加载中…" />;
  }
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
      {variables.length === 0 ? <Text size="small" type="tertiary">{emptyMessage}</Text> : null}
      {!currentVisible ? <Text size="small" type="danger">Current variable is not available in this scope.</Text> : null}
    </div>
  );
}
