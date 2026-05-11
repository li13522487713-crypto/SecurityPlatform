import { useMemo } from "react";
import { Button, Card, Input, List, Space, Typography } from "@douyinfe/semi-ui";
import type { ExpressionParseSuggestion } from "../utils/expression-engine";

const { Text } = Typography;

export interface VariablePickerProps {
  title?: string;
  value: string;
  suggestions?: ExpressionParseSuggestion[];
  placeholder?: string;
  onChange: (next: string) => void;
  onPickSuggestion?: (suggestion: ExpressionParseSuggestion) => void;
}

export function VariablePicker({
  title = "Expression",
  value,
  suggestions = [],
  placeholder = "Type expression...",
  onChange,
  onPickSuggestion,
}: VariablePickerProps) {
  const visibleSuggestions = useMemo(
    () => suggestions.slice(0, 12),
    [suggestions],
  );

  return (
    <Card title={title}>
      <Space vertical align="start" style={{ width: "100%" }}>
        <Input value={value} placeholder={placeholder} onChange={next => onChange(String(next ?? ""))} />
        {visibleSuggestions.length > 0 ? (
          <List
            dataSource={visibleSuggestions}
            renderItem={item => (
              <List.Item>
                <Button
                  theme="borderless"
                  type="tertiary"
                  style={{ paddingInline: 0, textAlign: "left" }}
                  onClick={() => onPickSuggestion?.(item)}
                >
                  <Space spacing={8}>
                    <Text strong>{item.label}</Text>
                    <Text type="tertiary">{item.detail}</Text>
                  </Space>
                </Button>
              </List.Item>
            )}
          />
        ) : null}
      </Space>
    </Card>
  );
}
