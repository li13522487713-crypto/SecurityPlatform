import { Editor } from "@coze-arch/bot-monaco-editor";
import { TextArea } from "@douyinfe/semi-ui";

interface SqlCodeEditorProps {
  value: string;
  onChange?: (value: string) => void;
  height?: number | string;
  readOnly?: boolean;
}

export function SqlCodeEditor({ value, onChange, height = 260, readOnly = false }: SqlCodeEditorProps) {
  if (!readOnly) {
    return (
      <div style={{ border: "1px solid var(--semi-color-border)", borderRadius: 6, overflow: "hidden" }}>
        <TextArea
          autosize={false}
          value={value}
          onChange={next => onChange?.(next)}
          style={{
            height,
            fontFamily: "ui-monospace, SFMono-Regular, Consolas, 'Liberation Mono', Menlo, monospace",
            fontSize: 13,
            lineHeight: 1.6,
            resize: "vertical"
          }}
        />
      </div>
    );
  }

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
