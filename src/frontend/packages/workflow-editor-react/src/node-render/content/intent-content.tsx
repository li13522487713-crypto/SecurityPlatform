interface IntentContentProps {
  model?: string;
  intents?: string[];
}

export function IntentContent(props: IntentContentProps) {
  return (
    <div className="wf-node-render-content">
      <div className="wf-node-render-kv">模型: {props.model || "-"}</div>
      <div className="wf-node-render-kv">意图数: {props.intents?.length ?? 0}</div>
    </div>
  );
}

