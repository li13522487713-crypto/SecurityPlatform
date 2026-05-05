import { Button, Select, Space, TextArea, Typography } from "@douyinfe/semi-ui";
import { lazy, Suspense, useEffect, useMemo, useRef, useState } from "react";
import type { MicroflowMetadataCatalog } from "../../metadata";
import type { MicroflowAuthoringSchema, MicroflowDataType, MicroflowExpression, MicroflowVariableIndex } from "../../schema";
import type { ContextVariableCandidate } from "../../inline-edit/shared/ContextVariablePicker";
import { createMicroflowExpression, expressionRaw, expressionTypeLabel, validateExpression } from "../../expressions";
import { getVariablesForExpressionFromIndex, variableSourceLabel, type MicroflowExpressionScopeContext } from "../../variables";
import {
  buildMicroflowExpressionCompletionOptions,
  validateMicroflowExpressionForEditor,
} from "../../expression-editor/codemirror-microflow-expression";
import { ExpressionDiagnostics } from "./ExpressionDiagnostics";
import { compositeConditionTokens, insertExpressionToken } from "./expression-token-insert";

const { Text } = Typography;

function useDebouncedValue<T>(value: T, delayMs: number): T {
  const [debounced, setDebounced] = useState(value);

  useEffect(() => {
    const timer = window.setTimeout(() => setDebounced(value), delayMs);
    return () => window.clearTimeout(timer);
  }, [delayMs, value]);

  return debounced;
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
  inlineCandidates,
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
  inlineCandidates?: ContextVariableCandidate[];
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
    if (inlineCandidates?.length) {
      return inlineCandidates.map(item => ({
        label: item.name,
        value: item.name,
      }));
    }
    return buildMicroflowExpressionCompletionOptions({
      schema,
      metadata,
      variableIndex,
      objectId,
      actionId,
      fieldPath,
      expectedType,
    });
  }, [actionId, expectedType, fieldPath, inlineCandidates, metadata, objectId, schema, variableIndex]);
  const cmDiagnostics = useMemo(() => validateMicroflowExpressionForEditor({
    value: debouncedRaw,
    schema,
    metadata,
    variableIndex,
    objectId,
    actionId,
    flowId,
    fieldPath,
    expectedType,
    required,
  }), [actionId, debouncedRaw, expectedType, fieldPath, flowId, metadata, objectId, required, schema, variableIndex]);
  const nextExpression = (nextRaw: string) => onChange(createMicroflowExpression(nextRaw, validation.inferredType));
  const cmMinRows = mode === "multiline" ? minRows : 1;
  const inlineMode = mode === "inline";
  return (
    <Space vertical align="start" spacing={inlineMode ? 4 : 6} style={{ width: "100%" }}>
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
          placeholder={placeholder}
          completionOptions={insertOptions}
          diagnostics={cmDiagnostics}
        />
      </Suspense>
      <Select
        filter
        showClear
        disabled={readonly || !insertOptions.length}
        placeholder="插入变量"
        style={{ width: "100%" }}
        value={undefined}
        optionList={insertOptions.slice(0, 20)}
        onChange={token => {
          if (token) {
            nextExpression(insertExpressionToken(raw, String(token)));
          }
        }}
      />
      {!inlineMode ? (
        <Space wrap spacing={4}>
          {compositeConditionTokens.map(token => (
            <Button
              key={token}
              size="small"
              disabled={readonly}
              aria-label={`insert expression token ${token}`}
              onClick={() => nextExpression(insertExpressionToken(raw, token))}
            >
              {token}
            </Button>
          ))}
        </Space>
      ) : null}
      <Text size="small" type="tertiary">
        {variables.length
          ? `变量: ${variables.slice(0, 4).map(variable => `$${variable.name}(${variableSourceLabel(variable)})`).join(", ")}${variables.length > 4 ? ` +${variables.length - 4}` : ""}`
          : "变量: none"}
      </Text>
      <Text size="small" type="tertiary">类型: {expressionTypeLabel(expectedType)} {"->"} {expressionTypeLabel(validation.inferredType)}</Text>
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
