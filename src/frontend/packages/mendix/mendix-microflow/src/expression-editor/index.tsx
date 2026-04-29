import { useMemo, useState } from "react";
import { Button, Space, Tag, TextArea, Typography } from "@douyinfe/semi-ui";
import { tokenizeExpression } from "../expressions/expression-tokenizer";
import { parseExpression } from "../expressions/expression-parser";
import { inferExpressionType } from "../expressions/expression-type-inference";

const { Text } = Typography;

export interface MicroflowExpressionEditorProps {
  value: string;
  expectedType?: string;
  readonly?: boolean;
  completions?: Array<{ label: string; insertText?: string }>;
  onChange?: (value: string) => void;
  onPreview?: (value: string) => Promise<unknown> | unknown;
  onFormat?: (value: string) => string;
}

export function ExpressionEditor({
  value,
  expectedType,
  readonly,
  completions = [],
  onChange,
  onPreview,
  onFormat,
}: MicroflowExpressionEditorProps) {
  const [preview, setPreview] = useState<string>();
  const tokens = useMemo(() => tokenizeExpression(value), [value]);
  const ast = useMemo(() => parseExpression(value), [value]);
  const inferred = useMemo(() => inferExpressionType(ast), [ast]);
  const diagnostics = [...tokens.flatMap(token => token.diagnostics ?? []), ...(ast.diagnostics ?? [])];
  const expectedMismatch = expectedType && inferred.kind !== "unknown" && inferred.kind !== expectedType;

  return (
    <Space vertical align="start" spacing={8} style={{ width: "100%" }}>
      <TextArea
        autosize
        value={value}
        readonly={readonly}
        placeholder="$variable.member or if $flag then 'yes' else 'no'"
        onChange={next => onChange?.(next)}
      />
      <Space wrap>
        <Tag color="blue">type: {inferred.kind}</Tag>
        {expectedType ? <Tag color={expectedMismatch ? "red" : "green"}>expected: {expectedType}</Tag> : null}
        <Tag color={diagnostics.length > 0 ? "red" : "green"}>diagnostics: {diagnostics.length}</Tag>
      </Space>
      <Space wrap>
        {completions.map(completion => (
          <Button key={completion.label} size="small" onClick={() => onChange?.(completion.insertText ?? completion.label)}>
            Completion: {completion.label}
          </Button>
        ))}
      </Space>
      {diagnostics.map((diagnostic: MicroflowExpressionDiagnostic, index) => (
        <Text key={`${diagnostic.code}-${index}`} type="danger">
          Diagnostic {diagnostic.code}: {diagnostic.message}
        </Text>
      ))}
      {expectedMismatch ? <Text type="danger">RUNTIME_EXPR_EXPECTED_TYPE_MISMATCH</Text> : null}
      {onFormat ? (
        <Button onClick={() => onChange?.(onFormat(value))}>
          Format
        </Button>
      ) : null}
      {onPreview ? (
        <Button
          onClick={async () => {
            const result = await onPreview(value);
            setPreview(JSON.stringify(result));
          }}
        >
          Preview
        </Button>
      ) : null}
      {preview ? <Text type="tertiary">{preview}</Text> : null}
    </Space>
  );
}

export { ExpressionEditor as MicroflowExpressionEditor };

interface MicroflowExpressionDiagnostic {
  code: string;
  message: string;
}
