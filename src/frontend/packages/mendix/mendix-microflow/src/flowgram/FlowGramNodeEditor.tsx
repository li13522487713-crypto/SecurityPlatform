import { Input, Button, Space } from "@douyinfe/semi-ui";
import { useState } from "react";

export interface FlowGramNodeEditorProps {
  initialValue: string;
  onSave: (value: string) => void;
  onCancel: () => void;
}

export const FlowGramNodeEditor = ({ initialValue, onSave, onCancel }: FlowGramNodeEditorProps) => {
  const [value, setValue] = useState(initialValue);
  return (
    <div style={{ position: "absolute", zIndex: 101, background: "var(--semi-color-bg-1)", padding: "8px", borderRadius: "4px", boxShadow: "0 4px 12px rgba(0,0,0,0.2)" }}>
      <Input value={value} onChange={setValue} autoFocus onKeyDown={e => e.key === "Enter" && onSave(value)} />
      <Space style={{ marginTop: "8px", width: "100%", justifyContent: "flex-end" }}>
        <Button size="small" onClick={onCancel}>取消</Button>
        <Button size="small" type="primary" onClick={() => onSave(value)}>保存</Button>
      </Space>
    </div>
  );
};
