import { Select, Typography } from "@douyinfe/semi-ui";
import { useMemo } from "react";
import { EMPTY_MICROFLOW_METADATA_CATALOG, useMicroflowMetadata } from "../../metadata";
import type { MicroflowAuthoringSchema, MicroflowDataType } from "../../schema";
import {
  buildVariableIndex,
  getAvailableVariablesAtField,
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
  disabled,
  placeholder = "Select variable",
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
  disabled?: boolean;
  placeholder?: string;
}) {
  const { catalog, loading, error, version } = useMicroflowMetadata();
  const effectiveCatalog = catalog ?? EMPTY_MICROFLOW_METADATA_CATALOG;
  const variableIndex = useMemo(() => buildVariableIndex(schema, effectiveCatalog), [schema, effectiveCatalog, version]);
  const variables = useMemo(() => {
    if (!objectId) {
      return [];
    }
    return getAvailableVariablesAtField(schema, variableIndex, objectId, fieldPath, {
      allowedTypeKinds,
      allowedTypes,
      collectionId,
      includeErrorContext,
      includeMaybe,
      includeSystem,
      writableOnly: writableOnly || !includeReadonly,
    });
  }, [allowedTypeKinds, allowedTypes, collectionId, fieldPath, includeErrorContext, includeMaybe, includeReadonly, includeSystem, objectId, schema, variableIndex, writableOnly]);
  const current = value && objectId
    ? resolveVariableReferenceFromIndex(schema, variableIndex, { objectId, actionId, fieldPath, collectionId }, value)
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
      {!currentVisible ? <Text size="small" type="danger">Current variable is not available in this scope.</Text> : null}
    </div>
  );
}
