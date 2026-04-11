import { Button, Input, Select, Space, Tag } from "antd";

interface TestRunPanelProps {
  visible: boolean;
  logs: string[];
  running: boolean;
  source: "published" | "draft";
  mode: "stream" | "sync";
  inputJson: string;
  onInputJsonChange: (value: string) => void;
  onSourceChange: (value: "published" | "draft") => void;
  onModeChange: (value: "stream" | "sync") => void;
  onClose: () => void;
  onRun: () => void;
}

export function TestRunPanel(props: TestRunPanelProps) {
  if (!props.visible) {
    return null;
  }

  return (
    <div className="wf-react-test-panel">
      <div className="wf-react-test-header">
        <Space>
          <Tag color={props.mode === "stream" ? "blue" : "default"}>流式运行</Tag>
          <Tag color={props.mode === "sync" ? "green" : "default"}>同步运行</Tag>
          <Select<"stream" | "sync">
            size="small"
            value={props.mode}
            onChange={props.onModeChange}
            options={[
              { value: "stream", label: "stream" },
              { value: "sync", label: "sync" }
            ]}
            style={{ width: 88 }}
          />
          <Select<"published" | "draft">
            size="small"
            value={props.source}
            onChange={props.onSourceChange}
            options={[
              { value: "published", label: "published" },
              { value: "draft", label: "draft" }
            ]}
            style={{ width: 110 }}
          />
        </Space>
        <Space>
          <Button size="small" type="primary" loading={props.running} onClick={props.onRun}>
            {props.running ? "执行中" : "执行"}
          </Button>
          <Button size="small" onClick={props.onClose}>
            关闭
          </Button>
        </Space>
      </div>
      <Input.TextArea rows={5} value={props.inputJson} onChange={(event) => props.onInputJsonChange(event.target.value)} placeholder='{"input":"hello"}' />
      <div className="wf-react-test-log">
        {props.logs.length === 0 ? <div className="wf-react-test-empty">暂无日志</div> : props.logs.map((line, index) => <div key={`${line}-${index}`}>{line}</div>)}
      </div>
    </div>
  );
}

