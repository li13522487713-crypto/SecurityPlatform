interface VariablePanelProps {
  visible: boolean;
  variables: Array<{ key: string; label: string; source: string }>;
  onClose: () => void;
}

export function VariablePanel(props: VariablePanelProps) {
  if (!props.visible) {
    return null;
  }

  return (
    <div className="wf-react-variable-panel">
      <div className="wf-react-variable-panel-header">
        <span>变量面板</span>
        <button type="button" onClick={props.onClose}>
          关闭
        </button>
      </div>
      <div className="wf-react-variable-list">
        {props.variables.map((item) => (
          <div key={`${item.source}.${item.key}`} className="wf-react-variable-item">
            <div className="wf-react-variable-name">{item.label}</div>
            <div className="wf-react-variable-path">{`{{${item.source}.${item.key}}}`}</div>
          </div>
        ))}
      </div>
    </div>
  );
}
