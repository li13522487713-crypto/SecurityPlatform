interface KnowledgeMaintainContentProps {
  mode: "index" | "delete" | "ltm";
  knowledgeId?: number;
  action?: string;
}

export function KnowledgeMaintainContent(props: KnowledgeMaintainContentProps) {
  const modeLabel = props.mode === "index" ? "知识写入" : props.mode === "delete" ? "知识删除" : "长期记忆";
  return (
    <div className="wf-node-render-content">
      <div className="wf-node-render-kv">类型: {modeLabel}</div>
      {typeof props.knowledgeId === "number" ? <div className="wf-node-render-kv">知识库: {props.knowledgeId}</div> : null}
      {props.action ? <div className="wf-node-render-kv">动作: {props.action}</div> : null}
    </div>
  );
}
