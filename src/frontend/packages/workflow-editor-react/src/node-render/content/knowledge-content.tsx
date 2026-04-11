interface KnowledgeContentProps {
  topK?: number;
  minScore?: number;
  knowledgeCount?: number;
}

export function KnowledgeContent(props: KnowledgeContentProps) {
  return (
    <div className="wf-node-render-content">
      <div className="wf-node-render-kv">知识库数: {props.knowledgeCount ?? 0}</div>
      <div className="wf-node-render-kv">TopK: {props.topK ?? 5}</div>
      <div className="wf-node-render-kv">最低分: {props.minScore ?? 0}</div>
    </div>
  );
}

