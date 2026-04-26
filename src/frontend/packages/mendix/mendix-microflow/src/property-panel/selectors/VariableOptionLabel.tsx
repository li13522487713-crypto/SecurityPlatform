import { Space, Tag, Typography } from "@douyinfe/semi-ui";
import type { MicroflowVariableSymbol } from "../../schema";
import { dataTypeLabel, variableSourceLabel } from "../../variables";

const { Text } = Typography;

export function VariableOptionLabel({ symbol }: { symbol: MicroflowVariableSymbol }) {
  return (
    <Space align="center" spacing={6}>
      <Text size="small">{symbol.name}</Text>
      <Text type="tertiary" size="small">{dataTypeLabel(symbol.dataType)}</Text>
      <Tag size="small">{variableSourceLabel(symbol)}</Tag>
      {symbol.visibility === "maybe" ? <Tag size="small" color="orange">maybe</Tag> : null}
      {symbol.readonly ? <Tag size="small" color="grey">readonly</Tag> : null}
    </Space>
  );
}
