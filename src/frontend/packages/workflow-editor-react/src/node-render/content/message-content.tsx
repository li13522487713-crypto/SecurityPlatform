interface MessageContentProps {
  operation: string;
  conversationId?: number;
  messageId?: number;
  role?: string;
}

export function MessageContent(props: MessageContentProps) {
  const normalizedOperation =
    props.operation === "CreateMessage"
      ? "创建消息"
      : props.operation === "EditMessage"
        ? "修改消息"
        : "删除消息";

  return (
    <div className="wf-node-render-content">
      <div className="wf-node-render-kv">操作: {normalizedOperation}</div>
      {props.role ? <div className="wf-node-render-kv">角色: {props.role}</div> : null}
      <div className="wf-node-render-kv">会话: {typeof props.conversationId === "number" ? props.conversationId : "-"}</div>
      {typeof props.messageId === "number" ? <div className="wf-node-render-kv">消息: {props.messageId}</div> : null}
    </div>
  );
}
