interface ConversationContentProps {
  operation: string;
  userId?: number;
  conversationId?: number;
  agentId?: number;
  title?: string;
}

export function ConversationContent(props: ConversationContentProps) {
  const normalizedOperation =
    props.operation === "CreateConversation"
      ? "创建会话"
      : props.operation === "ConversationList"
        ? "查询会话"
        : props.operation === "ConversationUpdate"
          ? "更新会话"
          : props.operation === "ConversationDelete"
            ? "删除会话"
            : props.operation === "ConversationHistory"
              ? "会话历史"
              : "清空历史";

  return (
    <div className="wf-node-render-content">
      <div className="wf-node-render-kv">操作: {normalizedOperation}</div>
      {typeof props.agentId === "number" ? <div className="wf-node-render-kv">Agent: {props.agentId}</div> : null}
      {props.title ? <div className="wf-node-render-kv wf-node-render-ellipsis">标题: {props.title}</div> : null}
      <div className="wf-node-render-kv">用户: {typeof props.userId === "number" ? props.userId : "-"}</div>
      <div className="wf-node-render-kv">会话: {typeof props.conversationId === "number" ? props.conversationId : "-"}</div>
    </div>
  );
}
