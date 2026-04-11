import { Button, Input, Select } from "antd";

interface NodeDebugPanelProps {
  visible: boolean;
  running: boolean;
  nodeOptions: Array<{ value: string; label: string }>;
  selectedNodeKey: string;
  inputJson: string;
  output: string;
  onNodeChange: (value: string) => void;
  onInputJsonChange: (value: string) => void;
  onRun: () => void;
  onClose: () => void;
}

export function NodeDebugPanel(props: NodeDebugPanelProps) {
  if (!props.visible) {
    return null;
  }

  return (
    <div className="wf-react-debug-panel">
      <div className="wf-react-test-header">
        <span>单节点调试</span>
        <Button size="small" onClick={props.onClose}>
          关闭
        </Button>
      </div>
      <Select
        size="small"
        value={props.selectedNodeKey}
        options={props.nodeOptions}
        style={{ width: "100%", marginBottom: 8 }}
        onChange={props.onNodeChange}
      />
      <Input.TextArea rows={6} value={props.inputJson} onChange={(event) => props.onInputJsonChange(event.target.value)} />
      <div style={{ display: "flex", justifyContent: "flex-end", marginTop: 8 }}>
        <Button size="small" type="primary" loading={props.running} onClick={props.onRun}>
          调试
        </Button>
      </div>
      <Input.TextArea rows={8} value={props.output} readOnly style={{ marginTop: 8 }} />
    </div>
  );
}
