import { Select, Space, TextArea, Typography } from "@douyinfe/semi-ui";
import { useEffect, useMemo, useState } from "react";
import { getEntityAttributes, type MicroflowMetadataCatalog } from "../../metadata";
import type { MicroflowDataType, MicroflowExpression, MicroflowSchema, MicroflowVariableIndex } from "../../schema";
import { createMicroflowExpression, expressionRaw, expressionTypeLabel, validateExpression } from "../../expressions";
import { getVariablesForExpressionFromIndex, type MicroflowExpressionScopeContext } from "../../variables";
import { ExpressionDiagnostics } from "./ExpressionDiagnostics";

const { Text } = Typography;

function useDebouncedValue<T>(value: T, delayMs: number): T {
  const [debounced, setDebounced] = useState(value);

  useEffect(() => {
    const timer = window.setTimeout(() => setDebounced(value), delayMs);
    return () => window.clearTimeout(timer);
  }, [delayMs, value]);

  return debounced;
}

function appendToken(raw: string, token: string): string {
  if (!raw.trim()) {
    return token;
  }
  return `${raw}${raw.endsWith(" ") ? "" : " "}${token}`;
}

export function ExpressionEditor({
  value,
  onChange,
  schema,
  metadata,
  variableIndex,
  objectId,
  actionId,
  flowId,
  fieldPath,
  expectedType,
  required,
  readonly,
  placeholder,
  minRows = 2,
  mode = "inline",
}: {
  value: MicroflowExpression | string | undefined;
  onChange: (next: MicroflowExpression) => void;
  schema: MicroflowSchema;
  metadata: MicroflowMetadataCatalog;
  variableIndex: MicroflowVariableIndex;
  objectId?: string;
  actionId?: string;
  flowId?: string;
  fieldPath?: string;
  expectedType?: MicroflowDataType;
  required?: boolean;
  readonly?: boolean;
  placeholder?: string;
  minRows?: number;
  mode?: "inline" | "multiline";
}) {
  const raw = expressionRaw(value);
  const debouncedRaw = useDebouncedValue(raw, 200);
  const context: MicroflowExpressionScopeContext = { objectId: objectId ?? "", actionId, fieldPath };
  const variables = useMemo(() => objectId ? getVariablesForExpressionFromIndex(schema, variableIndex, context) : [], [actionId, fieldPath, objectId, schema, variableIndex]);
  const validation = useMemo(() => validateExpression({
    expression: debouncedRaw,
    schema,
    metadata,
    variableIndex,
    context: { objectId, actionId, flowId, fieldPath, expectedType, required },
  }), [actionId, debouncedRaw, expectedType, fieldPath, flowId, metadata, objectId, required, schema, variableIndex]);
  const insertOptions = useMemo(() => variables.flatMap(variable => {
    const variableOption = {
      label: `$${variable.name} (${expressionTypeLabel(variable.dataType)})`,
      value: `$${variable.name}`,
    };
    if (variable.dataType.kind !== "object") {
      return [variableOption];
    }
    return [
      variableOption,
      ...getEntityAttributes(metadata, variable.dataType.entityQualifiedName).map(attribute => ({
        label: `$${variable.name}/${attribute.name} (${expressionTypeLabel(attribute.type)})`,
        value: `$${variable.name}/${attribute.name}`,
      })),
    ];
  }), [metadata, variables]);
  const nextExpression = (nextRaw: string) => onChange(createMicroflowExpression(nextRaw, validation.inferredType));
  return (
    <Space vertical align="start" spacing={6} style={{ width: "100%" }}>
      <TextArea
        value={raw}
        disabled={readonly}
        autosize={mode === "multiline" ? { minRows } : { minRows: 1, maxRows: 3 }}
        placeholder={placeholder}
        onChange={nextExpression}
      />
      <Select
        filter
        showClear
        disabled={readonly || !insertOptions.length}
        placeholder="Insert variable or attribute"
        style={{ width: "100%" }}
        value={undefined}
        optionList={insertOptions}
        onChange={token => {
          if (token) {
            nextExpression(appendToken(raw, String(token)));
          }
        }}
      />
      <Text size="small" type="tertiary">
        Expected: {expressionTypeLabel(expectedType)} · Inferred: {expressionTypeLabel(validation.inferredType)}
      </Text>
      <ExpressionDiagnostics diagnostics={validation.diagnostics} />
    </Space>
  );
}
