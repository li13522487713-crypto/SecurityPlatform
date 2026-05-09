import { Button, Space } from "@douyinfe/semi-ui";

export function InlineQuickFix(props: {
  suggestions?: Array<{
    id: string;
    label: string;
    actionKind: string;
    fieldPath?: string;
    value?: unknown;
    flowId?: string;
    editType?: "text" | "select" | "variable" | "expression" | "condition" | "http" | "assignment" | "branch" | "json" | "mapping" | "approval" | "loop" | "outputMappings";
  }>;
  onApply?: (suggestion: {
    id: string;
    label: string;
    actionKind: string;
    fieldPath?: string;
    value?: unknown;
    flowId?: string;
    editType?: "text" | "select" | "variable" | "expression" | "condition" | "http" | "assignment" | "branch" | "json" | "mapping" | "approval" | "loop" | "outputMappings";
  }) => void;
}) {
  if (!props.suggestions?.length) {
    return null;
  }
  return (
    <Space wrap>
      {props.suggestions.map(suggestion => (
        <Button key={suggestion.id} size="small" onClick={() => props.onApply?.(suggestion)}>{suggestion.label}</Button>
      ))}
    </Space>
  );
}
