interface AgentContentProps {
  agentId?: string;
  message?: string;
}

export function AgentContent(props: AgentContentProps) {
  return (
    <div className="wf-node-render-content">
      <div className="wf-node-render-kv">Agent: {props.agentId || "-"}</div>
      <div className="wf-node-render-kv wf-node-render-ellipsis">消息: {props.message || "-"}</div>
    </div>
  );
}
