import { Button, Input, Space, Tag } from "antd";

interface TestRunPanelProps {
  visible: boolean;
  logs: string[];
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
          <Tag color="blue">流式运行</Tag>
          <Tag color="green">同步运行</Tag>
        </Space>
        <Space>
          <Button size="small" type="primary" onClick={props.onRun}>
            执行
          </Button>
          <Button size="small" onClick={props.onClose}>
            关闭
          </Button>
        </Space>
      </div>
      <Input.TextArea rows={5} placeholder='{"input":"hello"}' />
      <div className="wf-react-test-log">
        {props.logs.length === 0 ? <div className="wf-react-test-empty">暂无日志</div> : props.logs.map((line, index) => <div key={`${line}-${index}`}>{line}</div>)}
      </div>
    </div>
  );
}

