import { Editor } from "@coze-arch/bot-monaco-editor";

interface SqlCodeEditorProps {
  value: string;
  onChange?: (value: string) => void;
  height?: number | string;
  readOnly?: boolean;
}

export function SqlCodeEditor({ value, onChange, height = 260, readOnly = false }: SqlCodeEditorProps) {
  return (
    <div style={{ border: "1px solid var(--semi-color-border)", borderRadius: 6, overflow: "hidden" }}>
      <Editor
        height={height}
        language="sql"
        value={value}
        theme="vs"
        options={{
          readOnly,
          minimap: { enabled: false },
          fontSize: 13,
          lineNumbersMinChars: 3,
          scrollBeyondLastLine: false,
          wordWrap: "on",
          automaticLayout: true
        }}
        onChange={next => onChange?.(next ?? "")}
      />
    </div>
  );
}
