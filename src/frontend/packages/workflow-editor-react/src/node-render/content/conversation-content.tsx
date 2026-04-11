interface ConversationContentProps {
  operation: string;
  userId?: number;
  conversationId?: number;
}

export function ConversationContent(props: ConversationContentProps) {
  return (
    <div className="wf-node-render-content">
      <div className="wf-node-render-kv">操作: {props.operation}</div>
      <div className="wf-node-render-kv">用户: {typeof props.userId === "number" ? props.userId : "-"}</div>
      <div className="wf-node-render-kv">会话: {typeof props.conversationId === "number" ? props.conversationId : "-"}</div>
    </div>
  );
}
