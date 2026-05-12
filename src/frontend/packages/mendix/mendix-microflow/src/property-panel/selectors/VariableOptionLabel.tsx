import { Space, Tag, Tooltip, Typography } from "@douyinfe/semi-ui";
import type { MicroflowVariableSymbol } from "../../schema";
import { dataTypeLabel } from "../../variables";

const { Text } = Typography;
const MAYBE_REASON_DEFAULT = "Variable is not definitely assigned on every normal path to this object.";

function scopeTag(scopeKind?: MicroflowVariableSymbol["scope"]["kind"]): { label: string; color: "blue" | "red" | "violet" | "grey" } {
  switch (scopeKind) {
    case "errorHandler":
      return { label: "Error scope", color: "red" };
    case "loop":
      return { label: "Loop scope", color: "violet" };
    case "global":
      return { label: "global", color: "blue" };
    case "downstream":
      return { label: "downstream", color: "blue" };
    case "branch":
      return { label: "branch", color: "blue" };
    case "system":
      return { label: "system", color: "blue" };
    default:
      return { label: scopeKind ?? "collection", color: "grey" };
  }
}

export function VariableOptionLabel({ symbol }: { symbol: MicroflowVariableSymbol }) {
  const scope = scopeTag(symbol.scope.kind);
  const maybeReason = (symbol.maybeReason ?? "").trim() || MAYBE_REASON_DEFAULT;
  return (
    <Space align="center" spacing={6}>
      <Text
        size="small"
        style={symbol.visibility === "maybe" ? { fontStyle: "italic", color: "var(--semi-color-warning)" } : undefined}
      >
        {symbol.name}
      </Text>
      <Text type="tertiary" size="small">{dataTypeLabel(symbol.dataType)}</Text>
      <Tag size="small" color={scope.color}>{scope.label}</Tag>
      {symbol.visibility === "maybe" ? (
        <Tooltip content={maybeReason}>
          <Tag size="small" color="orange">⚠ maybe</Tag>
        </Tooltip>
      ) : null}
      {symbol.dataType.kind === "unknown" ? <Tag size="small" color="red">unknown</Tag> : null}
      {symbol.readonly ? <Tag size="small" color="grey">readonly</Tag> : null}
    </Space>
  );
}
