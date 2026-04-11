import { Button, Input, Select, Space } from "antd";
import { useMemo, useState } from "react";

interface ExpressionEditorProps {
  value: string;
  onChange: (next: string) => void;
  suggestions: Array<{ value: string; label?: string }>;
  rows?: number;
  placeholder?: string;
}

export function ExpressionEditor(props: ExpressionEditorProps) {
  const [selectedRef, setSelectedRef] = useState<string>();
  const options = useMemo(
    () => props.suggestions.map((item) => ({ label: item.label ?? item.value, value: item.value })),
    [props.suggestions]
  );
  const multiline = (props.rows ?? 1) > 1;

  const insertReference = () => {
    if (!selectedRef) {
      return;
    }
    const base = props.value ?? "";
    const next = base.trim().length === 0 ? selectedRef : `${base} ${selectedRef}`;
    props.onChange(next);
  };

  return (
    <Space direction="vertical" style={{ width: "100%" }} size={6}>
      {multiline ? (
        <Input.TextArea
          rows={props.rows ?? 3}
          value={props.value}
          placeholder={props.placeholder ?? "支持表达式，例如 {{entry_1.output}} == \"ok\""}
          onChange={(event) => props.onChange(event.target.value)}
        />
      ) : (
        <Input
          value={props.value}
          placeholder={props.placeholder ?? "支持表达式，例如 {{entry_1.output}} == \"ok\""}
          onChange={(event) => props.onChange(event.target.value)}
        />
      )}
      <Space.Compact style={{ width: "100%" }}>
        <Select
          showSearch
          allowClear
          style={{ width: "100%" }}
          placeholder="插入变量引用"
          options={options}
          value={selectedRef}
          onChange={(value) => setSelectedRef(value)}
          filterOption={(input, option) => String(option?.value ?? "").toLowerCase().includes(input.toLowerCase())}
        />
        <Button onClick={insertReference}>插入</Button>
      </Space.Compact>
    </Space>
  );
}

