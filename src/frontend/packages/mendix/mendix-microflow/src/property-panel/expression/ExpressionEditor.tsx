import { Select, Space, TextArea, Typography } from "@douyinfe/semi-ui";
import { lazy, Suspense, useEffect, useMemo, useRef, useState } from "react";
import { getEntityAttributes, getEnumerationValues, type MicroflowMetadataCatalog } from "../../metadata";
import type { MicroflowAuthoringSchema, MicroflowDataType, MicroflowExpression, MicroflowVariableIndex } from "../../schema";
import { createMicroflowExpression, expressionRaw, expressionTypeLabel, validateExpression } from "../../expressions";
import { getVariablesForExpressionFromIndex, variableSourceLabel, type MicroflowExpressionScopeContext } from "../../variables";
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

const LazyCodemirrorExpression = lazy(async () => import("../../expression-editor/codemirror-microflow-expression"));

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
  schema: MicroflowAuthoringSchema;
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
  const inputRef = useRef<HTMLTextAreaElement | null>(null);
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
  const insertOptions = useMemo(() => {
    const variableOptions = variables.flatMap(variable => {
    const suffix = [
      expressionTypeLabel(variable.dataType),
      variableSourceLabel(variable),
      variable.visibility === "maybe" ? "maybe" : undefined,
      variable.dataType.kind === "unknown" ? "unknown" : undefined,
    ].filter(Boolean).join(", ");
    const variableOption = {
      label: `$${variable.name} (${suffix})`,
      value: `$${variable.name}`,
    };
    if (variable.dataType.kind === "list" && variable.dataType.itemType.kind === "object") {
      return [
        variableOption,
        {
          label: `$${variable.name}/... (列表需要循环后访问成员)`,
          value: `$${variable.name}`,
          disabled: true,
        },
      ];
    }
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
    });
    const enumOptions = expectedType?.kind === "enumeration"
      ? getEnumerationValues(metadata, expectedType.enumerationQualifiedName).map(value => ({
          label: `${expectedType.enumerationQualifiedName}.${value.key} (enum)`,
          value: `${expectedType.enumerationQualifiedName}.${value.key}`,
        }))
      : [];
    const functionOptions = [
      { label: "empty($variable)", value: "empty()" },
      { label: "not empty($variable)", value: "not empty()" },
      { label: "if condition then value else value", value: "if true then  else " },
    ];
    return [...variableOptions, ...enumOptions, ...functionOptions];
  }, [expectedType, metadata, variables]);
  const nextExpression = (nextRaw: string) => onChange(createMicroflowExpression(nextRaw, validation.inferredType));
  const cmMinRows = mode === "multiline" ? minRows : 1;
  return (
    <Space vertical align="start" spacing={6} style={{ width: "100%" }}>
      <Suspense
        fallback={(
          <TextArea
            ref={inputRef}
            value={raw}
            disabled={readonly}
            autosize={mode === "multiline" ? { minRows } : { minRows: 1, maxRows: 3 }}
            placeholder={placeholder}
            onChange={nextExpression}
          />
        )}
      >
        <LazyCodemirrorExpression
          value={raw}
          onChange={nextExpression}
          readonly={readonly}
          minRows={cmMinRows}
        />
      </Suspense>
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
      {variables.length ? (
        <Text size="small" type="tertiary">
          Available variables: {variables.map(variable => `$${variable.name} (${expressionTypeLabel(variable.dataType)}, ${variableSourceLabel(variable)})`).join(", ")}
        </Text>
      ) : (
        <Text size="small" type="tertiary">Available variables: none in the current microflow scope.</Text>
      )}
      <Text size="small" type="tertiary">
        Expected: {expressionTypeLabel(expectedType)} · Inferred: {expressionTypeLabel(validation.inferredType)}
      </Text>
      <ExpressionDiagnostics
        diagnostics={validation.diagnostics}
        onDiagnosticClick={() => {
          const cm = document.querySelector(".microflow-expression-codemirror-host .cm-content") as HTMLElement | null;
          if (cm) {
            cm.focus();
          } else {
            inputRef.current?.focus();
          }
        }}
      />
    </Space>
  );
}
