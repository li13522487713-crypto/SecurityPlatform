import { AutoComplete, Input } from "antd";

interface VariableRefPickerProps {
  value: string;
  onChange: (next: string) => void;
  suggestions: Array<{ value: string; label?: string }>;
  multiline?: boolean;
  rows?: number;
  placeholder?: string;
}

export function VariableRefPicker(props: VariableRefPickerProps) {
  if (props.multiline) {
    return (
      <Input.TextArea
        rows={props.rows ?? 4}
        value={props.value}
        placeholder={props.placeholder ?? "支持 {{variable}} 引用"}
        onChange={(event) => props.onChange(event.target.value)}
      />
    );
  }

  return (
    <AutoComplete
      options={props.suggestions}
      value={props.value}
      onChange={(value) => props.onChange(value)}
      filterOption={(inputValue, option) => String(option?.value ?? "").toLowerCase().includes(inputValue.toLowerCase())}
    >
      <Input size="small" placeholder={props.placeholder ?? "支持 {{variable}} 引用"} />
    </AutoComplete>
  );
}

