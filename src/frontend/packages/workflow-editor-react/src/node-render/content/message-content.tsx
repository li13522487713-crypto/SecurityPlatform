interface MessageContentProps {
  operation: string;
  conversationId?: number;
  messageId?: number;
}

export function MessageContent(props: MessageContentProps) {
  return (
    <div className="wf-node-render-content">
      <div className="wf-node-render-kv">操作: {props.operation}</div>
      <div className="wf-node-render-kv">会话: {typeof props.conversationId === "number" ? props.conversationId : "-"}</div>
      {typeof props.messageId === "number" ? <div className="wf-node-render-kv">消息: {props.messageId}</div> : null}
    </div>
  );
}
